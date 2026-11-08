using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Windows.Forms;

[assembly: System.Reflection.AssemblyTitle("Dusk")]
[assembly: System.Reflection.AssemblyProduct("Dusk")]
[assembly: System.Reflection.AssemblyCompany("Rida Abouhayan")]
[assembly: System.Reflection.AssemblyDescription("Afsluiten, slaapstand en sluimerstand met je toetsenbord")]
[assembly: System.Reflection.AssemblyVersion("1.5.1.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.5.1.0")]

namespace Dusk
{
    static class Program
    {
        public const string Version = "1.5.1";
        public const string RepoUrl = "https://github.com/ridaabouhayan-hue/dusk";
        public static bool SelfTest;

        [STAThread]
        static int Main(string[] args)
        {
            SelfTest = Array.IndexOf(args, "--zelftest") >= 0;
            if (SelfTest) return SelfTestRun(args);

            bool startup = Array.IndexOf(args, "--opstarten") >= 0;
            int action = ActionFromArgs(args);   // bijvoorbeeld "Dusk.exe --afsluiten" vanuit Logi Options+ of een snelkoppeling
            bool created;
            using (Mutex m = new Mutex(true, "Dusk_EenKeerStarten", out created))
            {
                if (!created)
                {
                    // Dusk draait al: dat exemplaar het venster laten tonen (of de actie laten starten) en zelf stoppen
                    if (App.ShowMessageId != 0) W.PostMessage(W.HWND_BROADCAST, App.ShowMessageId, new IntPtr(action + 1), IntPtr.Zero);
                    return 0;
                }
                SetDpiAwareness();
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e) { Log.Error("Onverwachte fout", e.Exception); Usage.Count("fout"); };
                AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e) { Log.Error("Fatale fout", e.ExceptionObject as Exception); Usage.Count("fout.fataal"); };
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Settings.Load();
                Usage.Count("gestart");
                Log.Info("Start Dusk " + Version + " op " + Environment.OSVersion + (startup ? " (met Windows)" : ""));
                App app = new App(startup);
                if (action >= 0) app.Flyout.StartNow((PowerAction)action);
                Application.Run(app);
                return 0;
            }
        }

        // Een actie meegeven bij het starten: --slaapstand, --sluimerstand, --herstarten, --afsluiten,
        // --vergrendelen, --afmelden, --wisselen (of de Engelse namen: --sleep, --shutdown, ...). -1 = geen.
        public static int ActionFromArgs(string[] args)
        {
            string[,] names = {
                { "slaapstand", "sleep" }, { "sluimerstand", "hibernate" }, { "herstarten", "restart" }, { "afsluiten", "shutdown" },
                { "vergrendelen", "lock" }, { "afmelden", "logoff" }, { "wisselen", "switchuser" } };
            PowerAction[] acts = { PowerAction.Sleep, PowerAction.Hibernate, PowerAction.Restart, PowerAction.Shutdown, PowerAction.Lock, PowerAction.Logoff, PowerAction.SwitchUser };
            foreach (string raw in args)
            {
                string a = raw.TrimStart('-', '/').ToLowerInvariant();
                for (int i = 0; i < acts.Length; i++)
                    if (a == names[i, 0] || a == names[i, 1]) return (int)acts[i];
            }
            return -1;
        }

        static void SetDpiAwareness()
        {
            try { W.SetProcessDpiAwarenessContext(new IntPtr(-4)); return; } catch (EntryPointNotFoundException) { }
            try { W.SetProcessDpiAwareness(2); return; } catch (EntryPointNotFoundException) { } catch (DllNotFoundException) { }
            try { W.SetProcessDPIAware(); } catch { }
        }

        // ---------------------------------------------------------------- zelftest
        // Tekent alle schermen naar plaatjes in out\schermen, zodat ze in GitHub te bekijken zijn,
        // en controleert of er onderweg niets misgaat.
        static int errors;

        static int SelfTestRun(string[] args)
        {
            string dir = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(Application.ExecutablePath)), "schermen");
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == "--map") dir = args[i + 1];
            Directory.CreateDirectory(dir);
            Settings.FilePath = Path.Combine(Path.GetTempPath(), "dusk-zelftest-instellingen.txt");
            Wallpaper.Override = TestWallpaper();
            Settings.Current = new Settings();
            Settings.Current.Language = "nl";
            App app = new App();
            Flyout fl = app.Flyout;
            Rectangle at = new Rectangle(1300, 420, 352, 420);

            Shot(dir, "01-kiezen-donker-mica", true, "mica", "lila", delegate { fl.TestSet(Flyout.View.Choose, 3, TimeChoice.Now, null, false); return Compose(fl, at); });
            Shot(dir, "02-kiezen-licht-mica", false, "mica", "lila", delegate { fl.TestSet(Flyout.View.Choose, 3, TimeChoice.After(30), null, false); return Compose(fl, at); });
            Shot(dir, "03-kiezen-donker-schemer", true, "dusk", "perzik", delegate { fl.TestSet(Flyout.View.Choose, 0, TimeChoice.At(23, 30), null, false); return Compose(fl, at); });
            Shot(dir, "04-kiezen-licht-effen", false, "solid", "blauw", delegate { fl.TestSet(Flyout.View.Choose, 2, TimeChoice.After(90), null, false); return Compose(fl, at); });
            Shot(dir, "05-tijd-kiezen", true, "mica", "lila", delegate { fl.TestSet(Flyout.View.Picker, 3, TimeChoice.Now, null, false); return Compose(fl, at); });
            Shot(dir, "05b-eigen-vaste-tijd", true, "mica", "lila", delegate { Settings.Current.Presets.Add("23:30"); fl.TestSet(Flyout.View.Choose, 3, TimeChoice.At(23, 30), null, false); Bitmap b = Compose(fl, at); Settings.Current.Presets.Remove("23:30"); return b; });
            Shot(dir, "06-foutmelding", true, "mica", "lila", delegate { fl.TestSet(Flyout.View.Choose, 1, TimeChoice.Now, Lang.T("err.hibernate"), true); return Compose(fl, at); });
            Shot(dir, "07-zeven-opties", true, "mica", "roze", delegate { Settings.Current.Hidden.Clear(); fl.TestSet(Flyout.View.Choose, 4, TimeChoice.Now, null, false); Bitmap b = Compose(fl, at); Settings.Current.Hidden = new List<PowerAction>(Settings.DefaultHidden); return b; });

            app.Scheduler.Start(PowerAction.Shutdown, DateTime.Now.AddMinutes(24).AddSeconds(12));
            Shot(dir, "08-timer-loopt", true, "mica", "lila", delegate { fl.TestSet(Flyout.View.Running, 3, TimeChoice.Now, null, false); return Compose(fl, at); });
            Shot(dir, "09-timer-licht", false, "mica", "lila", delegate { fl.TestSet(Flyout.View.Running, 3, TimeChoice.Now, null, false); return Compose(fl, at); });
            app.Scheduler.Cancel();
            Settings.Current.WarnMinutes = 1;
            app.Scheduler.Start(PowerAction.Sleep, DateTime.Now.AddSeconds(58));
            Shot(dir, "10-waarschuwing", true, "mica", "lila", delegate { app.Scheduler.OnTick(); fl.TestSet(Flyout.View.Running, 0, TimeChoice.Now, null, false); return Compose(fl, at); });
            app.Scheduler.Cancel();
            Shot(dir, "11-vijf-tellen", true, "mica", "lila", delegate { fl.TestSet(Flyout.View.Undo, 3, TimeChoice.Now, null, false); return Compose(fl, at); });
            Shot(dir, "11b-aftellen-licht-150", false, "mica", "lila", delegate { fl.TestSet(Flyout.View.Undo, 0, TimeChoice.Now, null, false); return Compose(fl, at, 1.5f); });

            Shot(dir, "15-kiezen-150-procent", true, "mica", "lila", delegate { fl.TestSet(Flyout.View.Choose, 3, TimeChoice.Now, null, false); return Compose(fl, at, 1.5f); });
            Shot(dir, "16-licht-150-procent", false, "mica", "lila", delegate { Settings.Current.Hidden = new List<PowerAction> { PowerAction.Hibernate }; fl.TestSet(Flyout.View.Choose, 3, TimeChoice.Now, null, false); Bitmap b = Compose(fl, at, 1.5f); Settings.Current.Hidden = new List<PowerAction>(Settings.DefaultHidden); return b; });
            Shot(dir, "17-licht-effen-blauw", false, "solid", "blauw", delegate { fl.TestSet(Flyout.View.Choose, 3, TimeChoice.After(30), null, false); return Compose(fl, at, 1.25f); });
            Settings.Current.Language = "en";
            Shot(dir, "12-engels", true, "mica", "lila", delegate { fl.TestSet(Flyout.View.Choose, 3, TimeChoice.After(45), null, false); return Compose(fl, at); });
            Settings.Current.Language = "fr";
            Shot(dir, "13-frans", false, "mica", "lila", delegate { fl.TestSet(Flyout.View.Choose, 3, TimeChoice.Now, null, false); return Compose(fl, at); });
            Settings.Current.Language = "es";
            Shot(dir, "14-spaans", true, "dusk", "lila", delegate { fl.TestSet(Flyout.View.Choose, 0, TimeChoice.Now, null, false); return Compose(fl, at); });
            Settings.Current.Language = "nl";

            for (int page = 0; page < SettingsForm.PageCount; page++)
            {
                int pg = page;
                Shot(dir, "2" + page + "-instellingen-" + page, true, "mica", "lila", delegate { return SettingsShot(app, pg); });
            }
            Shot(dir, "27-instellingen-uiterlijk-licht", false, "mica", "lila", delegate { return SettingsShot(app, 2); });
            Shot(dir, "28-instellingen-algemeen-licht", false, "solid", "blauw", delegate { return SettingsShot(app, 0); });
            Shot(dir, "29-instellingen-schemer", true, "dusk", "perzik", delegate { return SettingsShot(app, 3); });
            Shot(dir, "29c-menu-uitleg", true, "mica", "lila", delegate { return SettingsShot(app, SettingsForm.MenuPage, 1); });
            Shot(dir, "29b-menu-licht", false, "solid", "lila", delegate { Settings.Current.Hidden.Remove(PowerAction.SwitchUser); Bitmap b = SettingsShot(app, SettingsForm.MenuPage); Settings.Current.Hidden.Add(PowerAction.SwitchUser); return b; });

            // taakbalkicoontjes
            Shot(dir, "30-icoontjes", true, "solid", "lila", delegate { return Icons(); });

            Console.WriteLine(errors == 0 ? "Zelftest geslaagd" : "Zelftest: " + errors + " fout(en)");
            return errors == 0 ? 0 : 1;
        }

        delegate Bitmap Maker();

        static void Shot(string dir, string name, bool dark, string backdrop, string accent, Maker make)
        {
            try
            {
                Palette.ForceDark = dark;
                Settings.Current.Backdrop = backdrop;
                Settings.Current.Accent = accent;
                using (Bitmap b = make()) b.Save(Path.Combine(dir, name + ".png"), ImageFormat.Png);
                Console.WriteLine("ok   " + name);
            }
            catch (Exception e)
            {
                errors++;
                Console.Error.WriteLine("FOUT " + name + ": " + e);
            }
        }

        // Het venster op een stukje bureaublad, zodat je de schaduw en Mica ziet
        static Bitmap Compose(Flyout fl, Rectangle at) { return Compose(fl, at, 1f); }

        static Bitmap Compose(Flyout fl, Rectangle at, float scale)
        {
            using (Bitmap win = fl.Draw(scale, at))
            {
                int pad = 40;
                Bitmap b = new Bitmap(win.Width + 2 * pad, win.Height + 2 * pad, PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(b))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                    Rectangle wall = new Rectangle(at.X - pad - 18, at.Y - pad - 18, b.Width, b.Height);
                    g.DrawImage(Wallpaper.Override, new Rectangle(0, 0, b.Width, b.Height), wall, GraphicsUnit.Pixel);
                    g.DrawImage(win, pad, pad);
                }
                return b;
            }
        }

        static Bitmap SettingsShot(App app, int page) { return SettingsShot(app, page, -1); }

        static Bitmap SettingsShot(App app, int page, int expanded)
        {
            using (SettingsForm f = new SettingsForm(app))
            {
                f.Page = page;
                f.Expanded = expanded;
                Bitmap b = new Bitmap(f.ClientSize.Width, f.ClientSize.Height, PixelFormat.Format32bppRgb);
                using (Graphics g = Graphics.FromImage(b)) f.PaintAll(g, new Rectangle(500, 200, b.Width, b.Height));
                return b;
            }
        }

        static Bitmap Icons()
        {
            int[] sizes = { 16, 20, 24, 32, 48 };
            Bitmap b = new Bitmap(420, 140, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(b))
            {
                using (SolidBrush dark = new SolidBrush(Color.FromArgb(28, 28, 30))) g.FillRectangle(dark, 0, 0, 420, 70);
                using (SolidBrush light = new SolidBrush(Color.FromArgb(238, 238, 238))) g.FillRectangle(light, 0, 70, 420, 70);
                int x = 10;
                foreach (int sz in sizes)
                {
                    using (Bitmap i1 = Tray.Render(sz, 1f, true)) g.DrawImage(i1, x, 10);
                    using (Bitmap i2 = Tray.Render(sz, 0.55f, true)) g.DrawImage(i2, x + 36, 10 + 0);
                    using (Bitmap i3 = Tray.Render(sz, 1f, false)) g.DrawImage(i3, x, 80);
                    using (Bitmap i4 = Tray.Render(sz, 0.55f, false)) g.DrawImage(i4, x + 36, 80);
                    x += 82;
                }
            }
            return b;
        }

        // Een kleurrijke achtergrond zoals de standaard van Windows 11, voor de schermafbeeldingen
        static Bitmap TestWallpaper()
        {
            Bitmap b = new Bitmap(1920, 1080, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(b))
            {
                g.Clear(Color.FromArgb(11, 16, 38));
                Blob(g, new PointF(1380, 670), 820, Color.FromArgb(59, 91, 219));
                Blob(g, new PointF(540, 410), 700, Color.FromArgb(112, 72, 232));
                Blob(g, new PointF(1700, 300), 500, Color.FromArgb(14, 165, 233));
            }
            return b;
        }

        static void Blob(Graphics g, PointF c, float r, Color col)
        {
            using (GraphicsPath p = new GraphicsPath())
            {
                p.AddEllipse(c.X - r, c.Y - r, 2 * r, 2 * r);
                using (PathGradientBrush br = new PathGradientBrush(p))
                {
                    br.CenterColor = col;
                    br.SurroundColors = new[] { Color.FromArgb(0, col) };
                    g.FillPath(br, p);
                }
            }
        }
    }
}
