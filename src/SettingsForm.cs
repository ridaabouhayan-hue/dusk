using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Dusk
{
    // Instellingen: een gewoon venster, helemaal zelf getekend in dezelfde stijl als het hoofdvenster.
    public class SettingsForm : Form
    {
        const int LogicalW = 720, LogicalH = 540, NavW = 184;
        readonly App app;
        float s = 1f;
        public int Page;                    // 0 algemeen, 1 uiterlijk, 2 sneltoets en timer, 3 bijwerken, 4 over
        public const int UpdatePage = 3;
        int updState; DateTime updCheckedAt;   // 0 nog niet gekeken, 1 bezig, 2 nieuwste, 3 mislukt
        string hover;
        bool capturing; string captureError;
        readonly List<Hit> hits = new List<Hit>();

        class Hit { public RectangleF R; public string Id; public Action Do; }

        public SettingsForm(App app)
        {
            this.app = app;
            Text = "Dusk";
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.None;
            KeyPreview = true;
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            s = Program.SelfTest ? 1f : W.DpiScaleAt(Cursor.Position);
            ClientSize = new Size((int)(LogicalW * s), (int)(LogicalH * s));
            ApplyTheme();
        }

        public void ApplyTheme()
        {
            Palette p = Palette.Current();
            BackColor = p.Solid;
            if (IsHandleCreated) W.DarkTitleBar(Handle, p.Dark);
            Text = "Dusk, " + Lang.T("set.title").ToLowerInvariant();
            Invalidate();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            W.DarkTitleBar(Handle, Palette.Current().Dark);
        }

        protected override void OnMove(EventArgs e) { base.OnMove(e); if (Settings.Current.Backdrop == "mica") Invalidate(); }

        // ---------------------------------------------------------------- tekenen
        protected override void OnPaint(PaintEventArgs e)
        {
            PaintAll(e.Graphics, IsHandleCreated ? RectangleToScreen(ClientRectangle) : new Rectangle(0, 0, ClientSize.Width, ClientSize.Height));
        }

        public void PaintAll(Graphics g, Rectangle onScreen)
        {
            Palette p = Palette.Current();
            Ui.Setup(g);
            hits.Clear();
            Rectangle all = new Rectangle(0, 0, (int)(LogicalW * s), (int)(LogicalH * s));
            p.PaintBackdrop(g, all, onScreen, Settings.Current.Backdrop);
            Ui.Back = p.TextBack(Settings.Current.Backdrop);

            // links: logo en menu
            Ui.Logo(g, R(20, 18, 22, 22), p.Dark ? Color.FromArgb(224, 231, 255) : Color.FromArgb(55, 48, 163), 4.5f, 1f, Color.Empty);
            using (Font f = Ui.F(14, s, true)) Ui.Text(g, "Dusk", f, p.T1, R(50, 14, 120, 30), -1);
            string[] nav = { "nav.general", "nav.look", "nav.keys", "nav.update", "nav.about" };
            for (int i = 0; i < nav.Length; i++)
            {
                RectangleF r = R(10, 58 + i * 40, NavW - 20, 36);
                string id = "nav" + i;
                bool on = Page == i;
                Ui.Fill(g, r, S(8), on ? p.Sel : hover == id ? p.Fill : Color.Transparent);
                if (on) Ui.Fill(g, new RectangleF(r.X, r.Y + S(10), S(3), S(16)), S(1.5f), p.Accent);
                using (Font f = Ui.F(13, s, on)) Ui.Text(g, Lang.T(nav[i]), f, on ? p.T1 : p.T2, new RectangleF(r.X + S(14), r.Y, r.Width - S(16), r.Height), -1);
                int page = i;
                Add(r, id, delegate { Page = page; if (capturing) { capturing = false; app.RestoreHotkey(); } captureError = null; });
            }

            float x = NavW, w = LogicalW - NavW - 24;
            using (Font f = Ui.F(22, s, true)) Ui.Text(g, Lang.T(nav[Page]), f, p.T1, R(x, 14, w, 36), -1);
            float y = 64;
            switch (Page)
            {
                case 0: PageGeneral(g, p, x, y, w); break;
                case 1: PageLook(g, p, x, y, w); break;
                case 2: PageKeys(g, p, x, y, w); break;
                case 3: PageUpdate(g, p, x, y, w); break;
                default: PageAbout(g, p, x, y, w); break;
            }
        }

        float S(float v) { return v * s; }
        RectangleF R(float x, float y, float w, float h) { return new RectangleF(x * s, y * s, w * s, h * s); }

        void Add(RectangleF r, string id, Action a) { hits.Add(new Hit { R = r, Id = id, Do = a }); }

        // Een kaart met titel en uitleg links; geeft het vlak terug waar rechts de knop komt
        RectangleF Card(Graphics g, Palette p, float x, float y, float w, string title, string desc)
        {
            RectangleF r = R(x, y, w, 64);
            Ui.Fill(g, r, S(10), p.Fill);
            Ui.Stroke(g, r, S(10), p.Stroke, Math.Max(1f, s));
            using (Font f = Ui.F(14, s, false)) Ui.Text(g, title, f, p.T1, R(x + 16, y + 11, w * 0.66f, 22), -1);
            using (Font f = Ui.F(12, s, false)) Ui.Text(g, desc, f, p.T3, R(x + 16, y + 33, w * 0.66f, 20), -1);
            return R(x + w * 0.68f, y, w * 0.32f - 16, 64);
        }

        void Toggle(Graphics g, Palette p, RectangleF area, bool on, string id, Action flip)
        {
            RectangleF pill = new RectangleF(area.Right - S(40), area.Y + area.Height / 2 - S(10), S(40), S(20));
            if (on) Ui.Fill(g, pill, S(10), p.Accent);
            else Ui.Stroke(g, pill, S(10), p.T2, Math.Max(1f, s));
            float d = S(12);
            RectangleF knob = new RectangleF(on ? pill.Right - S(4) - d : pill.X + S(4), pill.Y + S(4), d, d);
            using (SolidBrush b = new SolidBrush(on ? p.OnAccent : p.T2)) g.FillEllipse(b, knob);
            using (Font f = Ui.F(13, s, false)) Ui.Text(g, Lang.T(on ? "on" : "off"), f, p.T2, new RectangleF(area.X, area.Y, area.Width - S(52), area.Height), 1);
            Add(new RectangleF(area.X, area.Y + S(12), area.Width, area.Height - S(24)), id, flip);
        }

        // Knop met de huidige keuze en een pijltje; klik opent een lijstje
        void Choice(Graphics g, Palette p, RectangleF area, string value, string id, string[] labels, Action<int> pick)
        {
            using (Font f = Ui.F(13, s, false))
            {
                float tw = Ui.Width(g, value, f);
                float bw = Math.Min(area.Width, tw + S(44));
                RectangleF b = new RectangleF(area.Right - bw, area.Y + area.Height / 2 - S(16), bw, S(32));
                Ui.Fill(g, b, S(6), hover == id ? p.Sel : p.Fill);
                Ui.Stroke(g, b, S(6), p.Stroke, Math.Max(1f, s));
                Ui.Text(g, value, f, p.T1, new RectangleF(b.X + S(12), b.Y, b.Width - S(36), b.Height), -1);
                Ui.Icon(g, "chevron", new RectangleF(b.Right - S(26), b.Y + S(9), S(14), S(14)), p.T2, 1.8f);
                Add(b, id, delegate { ShowMenu(b, labels, pick); });
            }
        }

        void ShowMenu(RectangleF at, string[] labels, Action<int> pick)
        {
            Palette p = Palette.Current();
            ContextMenuStrip m = new ContextMenuStrip();
            m.ShowImageMargin = false;
            m.BackColor = p.Dark ? Color.FromArgb(44, 44, 46) : Color.FromArgb(249, 249, 249);
            m.ForeColor = p.T1;
            try { m.Font = new Font(Ui.Family, 10f); } catch { }
            for (int i = 0; i < labels.Length; i++)
            {
                int idx = i;
                ToolStripItem it = m.Items.Add(labels[i], null, delegate { pick(idx); app.SettingsChanged(); Invalidate(); });
                it.ForeColor = p.T1;
                it.Padding = new Padding(4, 4, 16, 4);
            }
            m.Closed += delegate { BeginInvoke((Action)delegate { m.Dispose(); }); };
            m.Show(this, new Point((int)at.X, (int)at.Bottom + 2));
        }

        void PageGeneral(Graphics g, Palette p, float x, float y, float w)
        {
            Settings st = Settings.Current;
            string[] langs = new string[Settings.Languages.Length];
            for (int i = 0; i < langs.Length; i++) langs[i] = Lang.Name(Settings.Languages[i]);
            RectangleF a = Card(g, p, x, y, w, Lang.T("set.lang"), "Nederlands, English, Español, Français");
            Choice(g, p, a, Lang.Name(st.Language), "lang", langs, delegate(int i) { st.Language = Settings.Languages[i]; ApplyTheme(); });
            y += 70;
            a = Card(g, p, x, y, w, Lang.T("set.autostart"), Lang.T("set.autostart.d"));
            bool auto = Settings.Autostart;
            Toggle(g, p, a, auto, "auto", delegate { Settings.Autostart = !auto; });
            y += 70;
            List<PowerAction> acts = app.Flyout.Actions();
            string[] names = new string[acts.Count];
            for (int i = 0; i < acts.Count; i++) names[i] = Lang.ActionName(acts[i]);
            a = Card(g, p, x, y, w, Lang.T("set.default"), Lang.T("set.default.d"));
            Choice(g, p, a, Lang.ActionName(st.DefaultAction), "default", names, delegate(int i) { st.DefaultAction = acts[i]; });
            y += 70;
            a = Card(g, p, x, y, w, Lang.T("set.lock"), Lang.T("set.lock.d"));
            Toggle(g, p, a, st.ShowLockLogoff, "lock", delegate
            {
                st.ShowLockLogoff = !st.ShowLockLogoff;
                if (!st.ShowLockLogoff && (st.DefaultAction == PowerAction.Lock || st.DefaultAction == PowerAction.Logoff)) st.DefaultAction = PowerAction.Shutdown;
                app.SettingsChanged();
            });
            y += 70;
            string[] opens = new string[Settings.OpenChoices.Length];
            for (int i = 0; i < opens.Length; i++) opens[i] = Lang.T("open." + Settings.OpenChoices[i]);
            a = Card(g, p, x, y, w, Lang.T("set.open"), st.OpenAt == "smart" ? Lang.T("open.smart.d") : Lang.T("set.open.d"));
            Choice(g, p, a, Lang.T("open." + st.OpenAt), "open", opens, delegate(int i) { st.OpenAt = Settings.OpenChoices[i]; });
            y += 70;
            a = Card(g, p, x, y, w, Lang.T("set.forced"), Lang.T("set.forced.d"));
            Toggle(g, p, a, st.Forced, "forced", delegate { st.Forced = !st.Forced; app.SettingsChanged(); });
        }

        // Een tegel met een klein voorbeeld van het venster
        void Tile(Graphics g, Palette p, RectangleF r, string label, string sub, bool on, string id, Action pick, bool previewDark, string backdrop)
        {
            Ui.Fill(g, r, S(10), on ? p.Sel : hover == id ? p.Fill : p.Fill);
            if (on) Ui.Stroke(g, r, S(10), p.Accent, S(2));
            else Ui.Stroke(g, r, S(10), p.Stroke, Math.Max(1f, s));
            RectangleF pv = new RectangleF(r.X + S(8), r.Y + S(8), r.Width - S(16), S(60));
            using (GraphicsPath clip = Ui.Round(pv, S(6)))
            {
                GraphicsState gs = g.Save();
                g.SetClip(clip, CombineMode.Intersect);
                using (LinearGradientBrush wall = new LinearGradientBrush(pv, Color.FromArgb(59, 91, 219), Color.FromArgb(112, 72, 232), 35f)) g.FillRectangle(wall, pv);
                Palette pp = Palette.For(previewDark, Settings.Current.Accent);
                RectangleF win = new RectangleF(pv.X + pv.Width * 0.2f, pv.Y + S(12), pv.Width * 0.6f, pv.Height);
                using (GraphicsPath wp = Ui.Round(win, S(6)))
                {
                    GraphicsState g2 = g.Save();
                    g.SetClip(wp, CombineMode.Intersect);
                    if (backdrop == "mica")
                    {
                        using (LinearGradientBrush b = new LinearGradientBrush(win, Color.FromArgb(59, 91, 219), Color.FromArgb(112, 72, 232), 35f)) g.FillRectangle(b, win);
                        using (SolidBrush t = new SolidBrush(previewDark ? Color.FromArgb(200, 30, 30, 34) : Color.FromArgb(200, 243, 243, 245))) g.FillRectangle(t, win);
                    }
                    else pp.PaintBackdrop(g, Rectangle.Round(win), Rectangle.Round(win), backdrop);
                    g.Restore(g2);
                }
                using (SolidBrush ln = new SolidBrush(Color.FromArgb(previewDark ? 80 : 70, pp.T1)))
                {
                    g.FillRectangle(ln, win.X + S(8), win.Y + S(9), win.Width * 0.5f, S(4));
                    g.FillRectangle(ln, win.X + S(8), win.Y + S(18), win.Width * 0.7f, S(4));
                }
                using (SolidBrush ac = new SolidBrush(pp.Accent)) g.FillRectangle(ac, win.X + S(8), win.Y + S(27), win.Width * 0.4f, S(4));
                g.Restore(gs);
            }
            using (Font f = Ui.F(13, s, on)) Ui.Text(g, label, f, p.T1, new RectangleF(r.X, pv.Bottom + S(6), r.Width, S(20)), 0);
            if (sub != null) using (Font f = Ui.F(11, s, false)) Ui.Text(g, sub, f, p.T3, new RectangleF(r.X, pv.Bottom + S(24), r.Width, S(16)), 0);
            Add(r, id, delegate { pick(); app.SettingsChanged(); app.ThemeChanged(); });
        }

        void PageLook(Graphics g, Palette p, float x, float y, float w)
        {
            Settings st = Settings.Current;
            bool sysDark = Palette.ForceDark.HasValue ? Palette.ForceDark.Value : Palette.SystemDark();
            using (Font f = Ui.F(13, s, true)) Ui.Text(g, Lang.T("set.theme"), f, p.T1, R(x, y, w, 20), -1);
            y += 24;
            float tw = (w - 2 * 10) / 3f;
            string[] themes = { "system", "light", "dark" };
            string[] tl = { Lang.T("theme.system"), Lang.T("theme.light"), Lang.T("theme.dark") };
            for (int i = 0; i < 3; i++)
            {
                string th = themes[i];
                bool dark = th == "dark" || (th == "system" && sysDark);
                Tile(g, p, R(x + i * (tw + 10), y, tw, 112), tl[i], i == 0 ? Lang.T("theme.system.d") : null, st.Theme == th, "th" + i, delegate { st.Theme = th; ApplyTheme(); }, dark, st.Backdrop);
            }
            y += 124;
            using (Font f = Ui.F(13, s, true)) Ui.Text(g, Lang.T("set.backdrop"), f, p.T1, R(x, y, w, 20), -1);
            y += 24;
            string[] bds = { "mica", "solid", "dusk" };
            string[] bl = { Lang.T("bd.mica"), Lang.T("bd.solid"), Lang.T("bd.dusk") };
            string[] bs = { Lang.T("bd.mica.d"), Lang.T("bd.solid.d"), Lang.T("bd.dusk.d") };
            for (int i = 0; i < 3; i++)
            {
                string bd = bds[i];
                Tile(g, p, R(x + i * (tw + 10), y, tw, 112), bl[i], bs[i], st.Backdrop == bd, "bd" + i, delegate { st.Backdrop = bd; Invalidate(); }, p.Dark, bd);
            }
            y += 124;
            using (Font f = Ui.F(13, s, true)) Ui.Text(g, Lang.T("set.accent"), f, p.T1, R(x, y, w, 20), -1);
            y += 28;
            for (int i = 0; i < Palette.AccentKeys.Length; i++)
            {
                string key = Palette.AccentKeys[i];
                RectangleF c = R(x + 6 + i * 72, y, 36, 36);
                bool on = st.Accent == key;
                if (on)
                {
                    RectangleF ring = new RectangleF(c.X - S(4), c.Y - S(4), c.Width + S(8), c.Height + S(8));
                    using (Pen pen = new Pen(p.T1, S(2))) g.DrawEllipse(pen, ring);
                }
                using (SolidBrush b = new SolidBrush(Palette.AccentColor(key, p.Dark))) g.FillEllipse(b, c);
                if (key == "windows") Ui.Icon(g, "check", new RectangleF(c.X + S(9), c.Y + S(9), S(18), S(18)), Color.FromArgb(160, Palette.AccentColor(key, p.Dark).GetBrightness() > 0.55f ? Color.Black : Color.White), 2f);
                using (Font f = Ui.F(11, s, on)) Ui.Text(g, Lang.T("acc." + key), f, p.T2, new RectangleF(c.X - S(18), c.Bottom + S(6), c.Width + S(36), S(16)), 0);
                Add(new RectangleF(c.X - S(8), c.Y - S(6), c.Width + S(16), c.Height + S(28)), "acc" + i, delegate { st.Accent = key; app.SettingsChanged(); app.ThemeChanged(); });
            }
        }

        void PageKeys(Graphics g, Palette p, float x, float y, float w)
        {
            Settings st = Settings.Current;
            RectangleF a = Card(g, p, x, y, w, Lang.T("set.hotkey"), capturing ? (captureError ?? Lang.T("set.press")) : (captureError ?? Lang.T("set.hotkey.d")));
            using (Font f = Ui.F(12, s, false))
            {
                string hk = capturing ? "..." : (app.ActiveHotkey ?? st.Hotkey);
                string change = Lang.T("set.change");
                float cw = Ui.Width(g, change, f) + S(24);
                RectangleF cb = new RectangleF(a.Right - cw, a.Y + a.Height / 2 - S(16), cw, S(32));
                Ui.Fill(g, cb, S(6), capturing ? p.Accent : hover == "hk" ? p.Sel : p.Fill);
                Ui.Stroke(g, cb, S(6), p.Stroke, Math.Max(1f, s));
                Ui.Text(g, change, f, capturing ? p.OnAccent : p.T1, cb, 0);
                float kw = Ui.Width(g, hk, f) + S(18);
                RectangleF kb = new RectangleF(cb.X - S(10) - kw, a.Y + a.Height / 2 - S(13), kw, S(26));
                Ui.Stroke(g, kb, S(5), Color.FromArgb(p.Dark ? 70 : 60, p.T1), Math.Max(1f, s));
                Ui.Text(g, hk, f, p.T1, kb, 0);
                Add(cb, "hk", delegate { capturing = !capturing; captureError = null; if (capturing) app.SuspendHotkey(); else app.RestoreHotkey(); });
            }
            y += 70;
            string[] steps = new string[Settings.Steps.Length];
            for (int i = 0; i < steps.Length; i++) steps[i] = Settings.Steps[i] + " min";
            a = Card(g, p, x, y, w, Lang.T("set.step"), Lang.T("set.step.d"));
            Choice(g, p, a, st.StepMinutes + " min", "step", steps, delegate(int i) { st.StepMinutes = Settings.Steps[i]; });
            y += 70;
            string[] warns = new string[Settings.WarnChoices.Length];
            for (int i = 0; i < warns.Length; i++) warns[i] = Settings.WarnChoices[i] == 0 ? Lang.T("warn.off") : Settings.WarnChoices[i] + " min";
            a = Card(g, p, x, y, w, Lang.T("set.warn"), Lang.T("set.warn.d"));
            Choice(g, p, a, st.WarnMinutes == 0 ? Lang.T("warn.off") : st.WarnMinutes + " min", "warn", warns, delegate(int i) { st.WarnMinutes = Settings.WarnChoices[i]; });
            y += 70;
            string[] delays = new string[Settings.DelayChoices.Length];
            for (int i = 0; i < delays.Length; i++) delays[i] = DelayLabel(Settings.DelayChoices[i]);
            a = Card(g, p, x, y, w, Lang.T("set.delay"), Lang.T("set.delay.d"));
            Choice(g, p, a, DelayLabel(st.DelaySeconds), "delay", delays, delegate(int i) { st.DelaySeconds = Settings.DelayChoices[i]; });
            y += 70;
            List<string> shown = new List<string>();
            foreach (string pr in st.Presets) shown.Add(TimeInput.Label(TimeInput.Parse(pr)));
            a = Card(g, p, x, y, w, Lang.T("set.presets") + ": " + string.Join(", ", shown.ToArray()), Lang.T("set.presets.d"));
            using (Font f = Ui.F(13, s, false))
            {
                string lbl = Lang.T("btn.reset");
                float bw = Ui.Width(g, lbl, f) + S(28);
                RectangleF b = new RectangleF(a.Right - bw, a.Y + a.Height / 2 - S(16), bw, S(32));
                Ui.Fill(g, b, S(6), hover == "presets" ? p.Sel : p.Fill);
                Ui.Stroke(g, b, S(6), p.Stroke, Math.Max(1f, s));
                Ui.Text(g, lbl, f, p.T1, b, 0);
                Add(b, "presets", delegate { st.Presets = new List<string>(Settings.DefaultPresets); app.SettingsChanged(); });
            }
        }

        void PageUpdate(Graphics g, Palette p, float x, float y, float w)
        {
            Updates.Info pu = app.PendingUpdate;
            string title, sub, icon; Color dot;
            if (pu != null) { title = Lang.F("upd.ready", pu.Version); sub = Lang.T("upd.ready.d") + (app.Scheduler.Active ? " " + Lang.T("upd.confirm.timer") : ""); icon = "download"; dot = p.Accent; }
            else if (updState == 1) { title = Lang.T("upd.checking"); sub = Lang.T("upd.checking.d"); icon = "restart"; dot = p.Sel; }
            else if (updState == 2) { title = Lang.T("upd.latest"); sub = Lang.F("upd.latest.d", Program.Version, updCheckedAt.ToString("HH:mm")); icon = "check"; dot = Color.FromArgb(22, 163, 74); }
            else if (updState == 3) { title = Lang.T("upd.failed"); sub = Lang.T("upd.failed.d"); icon = "close"; dot = p.Danger; }
            else { title = "Dusk " + Program.Version; sub = Lang.T("upd.idle.d"); icon = "restart"; dot = p.Sel; }
            RectangleF card = R(x, y, w, 88);
            Ui.Fill(g, card, S(12), p.Fill);
            Ui.Stroke(g, card, S(12), pu != null ? p.Accent : p.Stroke, pu != null ? S(1.5f) : Math.Max(1f, s));
            RectangleF circle = R(x + 18, y + 22, 44, 44);
            using (SolidBrush b = new SolidBrush(dot)) g.FillEllipse(b, circle);
            Color onDot = pu != null ? p.OnAccent : (updState == 2 || updState == 3) ? Color.White : p.T1;
            Ui.Icon(g, icon, new RectangleF(circle.X + S(12), circle.Y + S(12), S(20), S(20)), onDot, 2.2f);
            string bl = app.Updating ? Lang.T("upd.busy") : pu != null ? Lang.T("upd.install") : Lang.T("upd.check");
            using (Font fb = Ui.F(13, s, true))
            {
                float bw = Ui.Width(g, bl, fb) + S(36);
                RectangleF btn = new RectangleF(card.Right - S(18) - bw, card.Y + card.Height / 2 - S(18), bw, S(36));
                bool primary = pu != null;
                Ui.Fill(g, btn, S(8), primary ? (hover == "upd" ? Color.FromArgb(255, Math.Min(255, p.Accent.R + 12), Math.Min(255, p.Accent.G + 12), Math.Min(255, p.Accent.B + 12)) : p.Accent) : (hover == "upd" ? p.Sel : p.Fill));
                if (!primary) Ui.Stroke(g, btn, S(8), p.Stroke, Math.Max(1f, s));
                Ui.TextAt(g, bl, fb, primary ? p.OnAccent : p.T1, btn.X + S(18), btn);
                Add(btn, "upd", delegate
                {
                    if (app.Updating) return;
                    if (app.PendingUpdate != null) { app.InstallUpdate(this); Invalidate(); }
                    else if (updState != 1)
                    {
                        updState = 1; Invalidate();
                        app.CheckForUpdateNow(delegate(int r)
                        {
                            if (IsDisposed) return;
                            updState = r == 1 ? 0 : r == 0 ? 2 : 3;
                            updCheckedAt = DateTime.Now;
                            Invalidate();
                        });
                    }
                });
                float tx = circle.Right + S(16), tw = btn.X - S(12) - tx;
                using (Font ft = Ui.F(15, s, true)) Ui.Text(g, title, ft, p.T1, new RectangleF(tx, card.Y + S(20), tw, S(24)), -1);
                using (Font fs = Ui.F(12, s, false)) Ui.Text(g, sub, fs, p.T3, new RectangleF(tx, card.Y + S(46), tw, S(20)), -1);
            }
            y += 98;
            RectangleF a = Card(g, p, x, y, w, Lang.T("upd.notify"), Lang.T("upd.notify.d"));
            Settings st = Settings.Current;
            Toggle(g, p, a, st.UpdateCheck, "updnotify", delegate { st.UpdateCheck = !st.UpdateCheck; app.SettingsChanged(); });
            y += 78;
            using (Font f = Ui.F(13, s, true)) Ui.Text(g, Lang.T("upd.news"), f, p.T1, R(x, y, w, 20), -1);
            y += 26;
            foreach (Changelog.Version v in Changelog.All)
            {
                float h = 34 + v.Lines.Length * 22 + 8;
                if (y + h > LogicalH - 12) break;   // wat niet past laten we weg (de nieuwste staan bovenaan)
                RectangleF box = R(x, y, w, h);
                Ui.Fill(g, box, S(10), p.Fill);
                float hx = x + 14;
                using (Font fb = Ui.F(14, s, true))
                {
                    float vw = Ui.Width(g, v.Number, fb) / s;
                    Ui.TextAt(g, v.Number, fb, p.T1, S(hx), R(hx, y + 6, vw + 4, 24));
                    hx += vw + 10;
                }
                if (v.Number == Program.Version)
                {
                    using (Font fl = Ui.F(11, s, true))
                    {
                        string lab = Lang.T("upd.yours");
                        float lw = Ui.Width(g, lab, fl) / s + 16;
                        RectangleF lr = R(hx, y + 9, lw, 18);
                        Ui.Fill(g, lr, S(9), p.Accent);
                        Ui.TextAt(g, lab, fl, p.OnAccent, lr.X + S(8), lr);
                        hx += lw + 10;
                    }
                }
                using (Font fd = Ui.F(12, s, false)) Ui.Text(g, v.Date, fd, p.T3, R(hx, y + 6, 200, 24), -1);
                using (Font fl = Ui.F(12, s, false))
                {
                    for (int i = 0; i < v.Lines.Length; i++)
                    {
                        float ly = y + 34 + i * 22;
                        using (SolidBrush b = new SolidBrush(p.Accent)) g.FillEllipse(b, R(x + 18, ly + 8, 5, 5));
                        Ui.Text(g, Changelog.Text(v.Lines[i]), fl, p.T2, R(x + 32, ly, w - 44, 22), -1);
                    }
                }
                y += h + 8;
            }
        }

        static string DelayLabel(int sec) { return sec == 0 ? Lang.T("delay.off") : sec + " " + Lang.T("unit.s"); }

        void PageAbout(Graphics g, Palette p, float x, float y, float w)
        {
            RectangleF tile = R(x, y + 4, 72, 72);
            Ui.Fill(g, tile, S(16), Color.FromArgb(11, 16, 32));
            Ui.Logo(g, new RectangleF(tile.X + S(12), tile.Y + S(12), S(48), S(48)), Color.FromArgb(224, 231, 255), 4f, 1f, Color.Empty);
            using (Font f = Ui.F(20, s, true)) Ui.Text(g, "Dusk", f, p.T1, R(x + 88, y + 8, w - 88, 30), -1);
            using (Font f = Ui.F(13, s, false)) Ui.Text(g, Lang.F("about.version", Program.Version), f, p.T2, R(x + 88, y + 40, w - 88, 22), -1);
            y += 96;
            using (Font f = Ui.F(13, s, false))
            {
                Ui.Wrap(g, Lang.T("about.text"), f, p.T1, R(x, y, w, 44));
                Ui.Wrap(g, Lang.T("about.privacy"), f, p.T2, R(x, y + 50, w, 24));
            }
            y += 92;
            string[] labels = { Lang.T("about.log"), Lang.T("about.github") };
            float bx = x;
            for (int i = 0; i < 2; i++)
            {
                using (Font f = Ui.F(13, s, false))
                {
                    float bw = Ui.Width(g, labels[i], f) / s + 32;
                    RectangleF b = R(bx, y, bw, 34);
                    string id = "ab" + i;
                    Ui.Fill(g, b, S(6), hover == id ? p.Sel : p.Fill);
                    Ui.Stroke(g, b, S(6), p.Stroke, Math.Max(1f, s));
                    Ui.Text(g, labels[i], f, p.T1, b, 0);
                    int which = i;
                    Add(b, id, delegate
                    {
                        try
                        {
                            if (which == 0) { System.IO.Directory.CreateDirectory(Log.Dir); Process.Start("explorer.exe", "\"" + Log.Dir + "\""); }
                            else Process.Start(Program.RepoUrl);
                        }
                        catch (Exception ex) { Log.Error("Openen mislukt", ex); }
                    });
                    bx += bw + 10;
                }
            }
        }

        // ---------------------------------------------------------------- muis en toetsen
        Hit HitAt(Point pt)
        {
            for (int i = hits.Count - 1; i >= 0; i--) if (hits[i].R.Contains(pt)) return hits[i];
            return null;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Hit h = HitAt(e.Location);
            string id = h != null ? h.Id : null;
            Cursor = h != null ? Cursors.Hand : Cursors.Default;
            if (id != hover) { hover = id; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); if (hover != null) { hover = null; Invalidate(); } }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button != MouseButtons.Left) return;
            Hit h = HitAt(e.Location);
            if (h != null) { h.Do(); Invalidate(); }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            Keys k = keyData & Keys.KeyCode;
            if (capturing)
            {
                if (k == Keys.Escape) { capturing = false; captureError = null; app.RestoreHotkey(); Invalidate(); return true; }
                if (k == Keys.ControlKey || k == Keys.ShiftKey || k == Keys.Menu || k == Keys.LWin || k == Keys.RWin) return true;
                int mods = 0;
                if ((keyData & Keys.Control) != 0) mods |= W.MOD_CONTROL;
                if ((keyData & Keys.Alt) != 0) mods |= W.MOD_ALT;
                if ((keyData & Keys.Shift) != 0) mods |= W.MOD_SHIFT;
                if ((W.GetAsyncKeyState(0x5B) & 0x8000) != 0 || (W.GetAsyncKeyState(0x5C) & 0x8000) != 0) mods |= W.MOD_WIN;
                string text = Hotkeys.Format(mods, (int)k);
                int m2, vk2;
                if (!Hotkeys.Parse(text, out m2, out vk2)) { captureError = Lang.T("set.press"); Invalidate(); return true; }
                capturing = false;
                captureError = app.TryHotkey(text) ? null : Lang.T("set.taken");
                Invalidate();
                return true;
            }
            if (k == Keys.Escape) { Close(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (capturing) app.RestoreHotkey();
            base.OnFormClosed(e);
        }
    }
}
