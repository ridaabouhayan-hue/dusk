using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace Dusk
{
    // Houdt alles bij elkaar: de timer, het venster, het icoon in de taakbalk en de sneltoets.
    public class App : ApplicationContext
    {
        public readonly Scheduler Scheduler = new Scheduler();
        public Flyout Flyout;
        public Tray Tray;
        SettingsForm settings;
        readonly MessageWindow msgWindow;
        static int showMsg = -1;
        public static int ShowMessageId
        {
            get
            {
                if (showMsg < 0) { try { showMsg = W.RegisterWindowMessage("Dusk_Tonen"); } catch { showMsg = 0; } }
                return showMsg;
            }
        }
        public string ActiveHotkey;

        public bool SettingsOpen { get { return settings != null && !settings.IsDisposed && settings.Visible; } }

        // Alleen voor de zelftest: het venster zonder icoon, sneltoets of berichten
        public App()
        {
            Flyout = new Flyout(this);
        }

        public App(bool startup)
        {
            Flyout = new Flyout(this);
            Tray = new Tray(this);
            msgWindow = new MessageWindow(this);
            Power.MessageHandle = msgWindow.Handle;
            RegisterHotkey(!startup);

            Scheduler.Changed += delegate { Tray.Update(); if (Flyout.Visible) { if (!Scheduler.Active && Flyout.Mode == Flyout.View.Running) { Flyout.Mode = Flyout.View.Choose; Flyout.ResetChoice(); } Flyout.Render(); } };
            Scheduler.Tick += delegate { Tray.Tick(); if (Flyout.Visible && Flyout.Mode == Flyout.View.Running) Flyout.Render(); };
            Scheduler.Warn += delegate { if (!Flyout.Visible) Flyout.ShowFlyout(false, Flyout.Origin.Passive); else Flyout.Render(); };
            Scheduler.Due += delegate { Execute(Scheduler.Action, false); };
            Scheduler.Resumed += delegate { Tray.Balloon(Lang.T("note.resumed")); };

            if (!startup) Flyout.ShowFlyout(true, Flyout.Origin.Launch);

            // Net bijgewerkt? Even melden. Daarna af en toe kijken of er een nieuwe versie is.
            IntPtr force = Flyout.Handle;   // venster aanmaken, zodat antwoorden van de achtergrond hier terechtkomen
            ui = SynchronizationContext.Current;
            int flag = Updates.ReadFlag();
            Updates.CleanUp();
            if (flag == 1) Tray.Balloon(Lang.F("upd.done", Program.Version));
            else if (flag == -1) Tray.Balloon(Lang.T("upd.fail"));
            updateTimer.Interval = 12 * 60 * 60 * 1000;
            updateTimer.Tick += delegate { AutoCheck(0); };
            updateTimer.Start();
            AutoCheck(20000);
        }

        // ---------------------------------------------------------------- bijwerken met een klik
        SynchronizationContext ui;
        readonly System.Windows.Forms.Timer updateTimer = new System.Windows.Forms.Timer();
        public Updates.Info PendingUpdate;
        public bool Updating;

        void OnUi(Action a) { if (ui != null) ui.Post(delegate { try { a(); } catch (Exception e) { Log.Error("Bijwerken", e); } }, null); }

        void AutoCheck(int delay)
        {
            if (!Settings.Current.UpdateCheck || PendingUpdate != null) return;
            Updates.CheckLater(delay, delegate(Updates.Info info) { OnUi(delegate { UpdateFound(info, false); }); });
        }

        void UpdateFound(Updates.Info info, bool fromSettings)
        {
            bool wasNew = PendingUpdate == null || PendingUpdate.Version != info.Version;
            PendingUpdate = info;
            if (settings != null && !settings.IsDisposed) settings.Invalidate();
            if (!fromSettings && wasNew) Tray.Balloon(Lang.F("upd.found", info.Version), true);
        }

        // result: 1 = er is iets nieuws, 0 = je hebt de nieuwste, -1 = kon niet kijken
        public void CheckForUpdateNow(Action<int> result)
        {
            Updates.CheckLater(0,
                delegate(Updates.Info info) { OnUi(delegate { UpdateFound(info, true); result(1); }); },
                delegate { OnUi(delegate { result(0); }); },
                delegate { OnUi(delegate { result(-1); }); });
        }

        // Downloaden, de controlecode nakijken, de installer stil laten lopen; die start Dusk daarna weer
        public void InstallUpdate(IWin32Window owner)
        {
            Updates.Info info = PendingUpdate;
            if (info == null || Updating) return;
            if (info.SetupUrl == null || info.Sha256 == null)
            {
                // geen controlecode bij deze versie: dan niet zelf installeren, alleen de pagina openen
                try { if (info.PageUrl.StartsWith("https://github.com/")) System.Diagnostics.Process.Start(info.PageUrl); } catch { }
                return;
            }
            Updating = true;
            string q = Lang.F("upd.confirm", info.Version) + (Scheduler.Active ? " " + Lang.T("upd.confirm.timer") : "");
            if (MessageBox.Show(owner, q, "Dusk", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) { Updating = false; return; }
            Tray.Balloon(Lang.T("upd.downloading"));
            Updates.Download(info, delegate(string file)
            {
                OnUi(delegate
                {
                    Updating = false;
                    if (file == null) { Tray.Balloon(Lang.T("upd.fail")); return; }
                    try
                    {
                        Updates.WriteFlag(info.Version);
                        System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo(file, "/SILENT /SUPPRESSMSGBOXES /NORESTART /NOCANCEL /BIJWERKEN");
                        psi.UseShellExecute = true;
                        System.Diagnostics.Process.Start(psi);
                        Log.Info("Bijwerken naar " + info.Version);
                        Quit();   // de installer vervangt het programma en start het daarna weer
                    }
                    catch (Exception ex)
                    {
                        Log.Error("Installer starten", ex);
                        try { System.IO.File.Delete(Updates.FlagPath); } catch { }
                        Tray.Balloon(Lang.T("upd.fail"));
                    }
                });
            });
        }

        public void RegisterHotkey(bool tell)
        {
            string wanted = Settings.Current.Hotkey;
            ActiveHotkey = Hotkeys.Register(msgWindow.Handle, wanted);
            if (ActiveHotkey != null && ActiveHotkey != wanted && tell)
                Tray.Balloon(Lang.F("note.hotkey", wanted, ActiveHotkey));
            Tray.Update();
        }

        // Tijdens het kiezen van een nieuwe sneltoets mag de oude niet afgaan
        public void SuspendHotkey() { if (msgWindow != null) W.UnregisterHotKey(msgWindow.Handle, Hotkeys.Id); }

        // Probeert precies deze sneltoets. Lukt het, dan wordt hij bewaard; anders komt de oude terug.
        public bool TryHotkey(string text)
        {
            if (msgWindow != null && Hotkeys.TryRegisterExact(msgWindow.Handle, text))
            {
                Settings.Current.Hotkey = text;
                ActiveHotkey = text;
                SettingsChanged();
                return true;
            }
            RestoreHotkey();
            return false;
        }

        public void RestoreHotkey() { if (msgWindow != null) RegisterHotkey(false); }

        // Sneltoets of klik op het icoon: tonen, of weer verbergen als hij al vooraan staat
        public void Toggle(Flyout.Origin origin)
        {
            if (Flyout.Visible && Form.ActiveForm == Flyout) Flyout.HideFlyout();
            // net verborgen omdat je op het icoon klikte: dan niet meteen weer openen
            else if ((DateTime.UtcNow - Flyout.LastAutoHide).TotalMilliseconds < 400) return;
            else Flyout.ShowFlyout(true, origin);
        }

        public void OpenSettings() { OpenSettings(-1); }

        bool previewShown;   // venster alleen getoond om mee te kijken terwijl Instellingen open staat

        public void OpenSettings(int page)
        {
            bool fresh = settings == null || settings.IsDisposed;
            if (fresh)
            {
                settings = new SettingsForm(this);
                settings.FormClosed += delegate
                {
                    settings = null;
                    if (previewShown && Flyout.Visible && Form.ActiveForm != Flyout) Flyout.HideFlyout();
                    previewShown = false;
                };
            }
            if (page >= 0) { settings.Page = page; settings.Invalidate(); }
            settings.Show();
            // Het venster blijft (of komt) in beeld, zodat je meteen ziet wat een instelling doet
            if (!Flyout.Visible) { Flyout.ShowFlyout(false, Flyout.Origin.Passive); previewShown = true; }
            if (fresh) KeepApart();
            settings.Activate();
            W.ForceForeground(settings.Handle);
        }

        // Instellingen veranderd: alles opnieuw tekenen in de nieuwe taal, kleur of met de nieuwe sneltoets
        // Instellingen en het venster niet over elkaar heen: Instellingen schuift dan naar links
        void KeepApart()
        {
            try
            {
                Rectangle f = Flyout.Bounds, sb = settings.Bounds;
                if (!f.IntersectsWith(sb)) return;
                Rectangle wa = Screen.FromRectangle(f).WorkingArea;
                int x = Math.Max(wa.Left + 8, f.Left - 16 - sb.Width);
                int y = Math.Max(wa.Top + 8, Math.Min(wa.Bottom - sb.Height - 8, sb.Top));
                settings.Location = new Point(x, y);
            }
            catch { }
        }

        // Het kruisje in het venster: Dusk helemaal stoppen (bij een lopende timer eerst vragen)
        public void QuitAsk(IWin32Window owner)
        {
            if (Scheduler.Active && MessageBox.Show(owner, Lang.T("quit.timer"), "Dusk", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            Quit();
        }

        public void SettingsChanged()
        {
            Settings.Current.Save();
            Tray.Update();
            if (Flyout.Visible) Flyout.Render();
        }

        public void ThemeChanged()
        {
            Tray.Update();
            if (Flyout.Visible) Flyout.Render();
            if (settings != null && !settings.IsDisposed) settings.ApplyTheme();
        }

        // Voert de actie uit. Eerst het venster weg, zodat het niet in beeld staat als de pc weer wakker wordt.
        public void Execute(PowerAction a, bool cancelTimer)
        {
            if (cancelTimer && Scheduler.Active) Scheduler.Cancel();
            Flyout.HideFlyout();
            System.Windows.Forms.Timer t = new System.Windows.Forms.Timer();
            t.Interval = 150;
            t.Tick += delegate
            {
                t.Stop(); t.Dispose();
                string err = Power.Run(a, Settings.Current.Forced);
                if (err != null)
                {
                    Flyout.Mode = Flyout.View.Choose;
                    Flyout.ResetChoice();
                    Flyout.ShowFlyout(true, Flyout.Origin.Launch);
                    Flyout.ShowMessage(Lang.T(err), true);
                }
            };
            t.Start();
        }

        public bool Quitting;

        public void Quit()
        {
            Quitting = true;
            updateTimer.Stop();
            Scheduler.Cancel();
            Tray.Dispose();
            if (settings != null && !settings.IsDisposed) settings.Close();
            W.UnregisterHotKey(msgWindow.Handle, Hotkeys.Id);
            msgWindow.DestroyHandle();
            Flyout.Close();
            ExitThread();
        }

        // Onzichtbaar venstertje dat de sneltoets en berichten van Windows ontvangt
        class MessageWindow : NativeWindow
        {
            readonly App app;

            public MessageWindow(App app)
            {
                this.app = app;
                CreateParams cp = new CreateParams();
                cp.Caption = "Dusk berichten";
                CreateHandle(cp);
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == W.WM_HOTKEY && m.WParam.ToInt32() == Hotkeys.Id) { app.Toggle(Flyout.Origin.Hotkey); return; }
                if (m.Msg == ShowMessageId && ShowMessageId != 0) { app.Flyout.ShowFlyout(true, Flyout.Origin.Launch); return; }
                if (m.Msg == W.WM_SETTINGCHANGE)
                {
                    string area = m.LParam != IntPtr.Zero ? System.Runtime.InteropServices.Marshal.PtrToStringUni(m.LParam) : null;
                    if (area == "ImmersiveColorSet") app.ThemeChanged();
                }
                base.WndProc(ref m);
            }
        }
    }
}
