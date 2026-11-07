using System;
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

        public void OpenSettings()
        {
            Flyout.HideFlyout();
            if (settings == null || settings.IsDisposed)
            {
                settings = new SettingsForm(this);
                settings.FormClosed += delegate { settings = null; };
            }
            settings.Show();
            settings.Activate();
            W.ForceForeground(settings.Handle);
        }

        // Instellingen veranderd: alles opnieuw tekenen in de nieuwe taal, kleur of met de nieuwe sneltoets
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
            Timer t = new Timer();
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
