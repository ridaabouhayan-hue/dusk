using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Dusk
{
    // Het icoon rechtsonder bij de klok, met een menu onder de rechtermuisknop.
    // Loopt er een timer, dan dooft de ring van het icoon langzaam uit.
    public class Tray : IDisposable
    {
        readonly App app;
        readonly NotifyIcon icon = new NotifyIcon();
        readonly ContextMenuStrip menu = new ContextMenuStrip();
        IntPtr hIcon = IntPtr.Zero;
        int lastStep = -1;

        public Tray(App app)
        {
            this.app = app;
            icon.MouseClick += delegate(object s, MouseEventArgs e) { if (e.Button == MouseButtons.Left) app.Toggle(Flyout.Origin.Tray); };
            icon.ContextMenuStrip = menu;
            menu.Opening += delegate { BuildMenu(); };
            icon.BalloonTipClicked += delegate { if (balloonIsUpdate) app.OpenSettings(SettingsForm.UpdatePage); };
            icon.Visible = true;
            Update();
        }

        void BuildMenu()
        {
            Palette p = Palette.For(TaskbarDark(), Settings.Current.Accent);
            menu.Items.Clear();
            menu.Renderer = new ToolStripProfessionalRenderer(new MenuColors(p));
            menu.BackColor = p.Dark ? Color.FromArgb(44, 44, 46) : Color.FromArgb(249, 249, 249);
            menu.ForeColor = p.T1;
            menu.ShowImageMargin = false;
            try { menu.Font = new Font(Ui.Family, 9.5f); } catch { }
            menu.Items.Add(Lang.T("tray.open"), null, delegate { app.Flyout.ShowFlyout(true, Flyout.Origin.Tray); }).Font = new Font(menu.Font, FontStyle.Bold);
            if (app.Scheduler.Active)
            {
                menu.Items.Add(Lang.F("tray.add", Settings.Current.StepMinutes), null, delegate { app.Scheduler.Add(Settings.Current.StepMinutes); });
                menu.Items.Add(Lang.T("tray.cancel"), null, delegate { app.Scheduler.Cancel(); });
            }
            // jouw knoppen uit Instellingen > Menu, in jouw volgorde
            menu.Items.Add(new ToolStripSeparator());
            foreach (PowerAction a in Settings.Current.MenuActions())
            {
                PowerAction act = a;
                ToolStripItem it = menu.Items.Add(Lang.ActionName(a), null, delegate { Usage.Count("taakbalkmenu." + Lang.Key(act)); app.Flyout.StartNow(act); });
                it.Enabled = Power.Available(a);
            }
            if (app.PendingUpdate != null)
                menu.Items.Add(Lang.F("tray.update", app.PendingUpdate.Version), null, delegate { app.OpenSettings(SettingsForm.UpdatePage); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(Lang.T("tray.settings"), null, delegate { app.OpenSettings(); });
            menu.Items.Add(Lang.T("tray.exit"), null, delegate { app.Quit(); });
            foreach (ToolStripItem it in menu.Items) { it.ForeColor = p.T1; it.Padding = new Padding(4, 3, 12, 3); }
        }

        // De taakbalk heeft een eigen licht/donker-instelling in Windows
        public static bool TaskbarDark()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    object v = k != null ? k.GetValue("SystemUsesLightTheme") : null;
                    if (v is int) return (int)v == 0;
                }
            }
            catch { }
            return true;
        }

        public void Update()
        {
            lastStep = -1;
            Tick();
            string tip = "Dusk";
            if (app.Scheduler.Active)
                tip = Lang.ActionName(app.Scheduler.Action) + " " + Lang.F("at", app.Scheduler.Target.ToString("HH:mm"));
            else if (app.ActiveHotkey != null)
                tip = "Dusk (" + app.ActiveHotkey + ")";
            icon.Text = tip.Length > 63 ? tip.Substring(0, 63) : tip;
        }

        // Het icoon alleen opnieuw tekenen als de ring zichtbaar verandert (60 stapjes)
        public void Tick()
        {
            float frac = app.Scheduler.Active ? app.Scheduler.Fraction : 1f;
            int step = app.Scheduler.Active ? (int)Math.Round(frac * 60) : 100;
            if (step == lastStep) return;
            lastStep = step;
            int size = SystemInformation.SmallIconSize.Width;
            using (Bitmap bmp = Render(size, frac, TaskbarDark()))
            {
                IntPtr h = bmp.GetHicon();
                icon.Icon = Icon.FromHandle(h);
                if (hIcon != IntPtr.Zero) W.DestroyIcon(hIcon);   // anders loopt het geheugen langzaam vol
                hIcon = h;
            }
        }

        public static Bitmap Render(int size, float fraction, bool darkTaskbar)
        {
            Bitmap bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                Ui.Setup(g);
                g.Clear(Color.Transparent);
                float stroke = size <= 16 ? 5.5f : 5f;
                Color bar = darkTaskbar ? Color.FromArgb(240, 242, 255) : Color.FromArgb(40, 36, 120);
                Color track = darkTaskbar ? Color.FromArgb(90, 255, 255, 255) : Color.FromArgb(70, 0, 0, 0);
                Ui.Logo(g, new RectangleF(0, 0, size, size), bar, stroke, fraction, track);
            }
            return bmp;
        }

        bool balloonIsUpdate;

        public void Balloon(string text) { Balloon(text, false); }

        public void Balloon(string text, bool isUpdate)
        {
            balloonIsUpdate = isUpdate;
            try { icon.ShowBalloonTip(isUpdate ? 9000 : 6000, "Dusk", text, ToolTipIcon.Info); } catch { }
        }

        public void Dispose()
        {
            icon.Visible = false;
            icon.Dispose();
            menu.Dispose();
            if (hIcon != IntPtr.Zero) { W.DestroyIcon(hIcon); hIcon = IntPtr.Zero; }
        }

        // Kleuren van het menu in licht of donker
        class MenuColors : ProfessionalColorTable
        {
            readonly Color bg, hot, line;
            public MenuColors(Palette p)
            {
                bg = p.Dark ? Color.FromArgb(44, 44, 46) : Color.FromArgb(249, 249, 249);
                hot = p.Dark ? Color.FromArgb(62, 62, 66) : Color.FromArgb(234, 234, 234);
                line = p.Dark ? Color.FromArgb(70, 70, 74) : Color.FromArgb(220, 220, 220);
            }
            public override Color ToolStripDropDownBackground { get { return bg; } }
            public override Color MenuBorder { get { return line; } }
            public override Color MenuItemBorder { get { return hot; } }
            public override Color MenuItemSelected { get { return hot; } }
            public override Color MenuItemSelectedGradientBegin { get { return hot; } }
            public override Color MenuItemSelectedGradientEnd { get { return hot; } }
            public override Color SeparatorDark { get { return line; } }
            public override Color SeparatorLight { get { return bg; } }
            public override Color ImageMarginGradientBegin { get { return bg; } }
            public override Color ImageMarginGradientMiddle { get { return bg; } }
            public override Color ImageMarginGradientEnd { get { return bg; } }
        }
    }
}
