using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace Dusk
{
    // Het hoofdvenster. Tekent zichzelf helemaal (zachte hoeken, schaduw, Mica) en is in elke stand even groot.
    public class Flyout : Form
    {
        public enum View { Choose, Running, Undo, Picker }
        public enum Origin { Tray, Hotkey, Launch, Passive }

        const int PanelW = 352, Pad = 12, Margin_ = 18;   // logische punten; Margin_ is ruimte voor de schaduw

        readonly App app;
        public View Mode = View.Choose;
        float s = 1f;                         // schaal van het scherm (125% = 1,25)
        int selected = 3;
        TimeChoice choice = TimeChoice.Now;
        string message; bool messageIsError;
        string hover, press;
        readonly List<KeyValuePair<RectangleF, string>> zones = new List<KeyValuePair<RectangleF, string>>();
        bool passive;                         // getoond zonder je toetsenbord over te nemen (waarschuwing)
        DateTime undoEnd;
        PowerAction undoAction;
        readonly Timer anim = new Timer();
        Point panelPos;                       // linksboven van het vlak op het scherm
        bool anchorBottom;                    // bij de taakbalk: groeit naar boven
        public DateTime LastAutoHide = DateTime.MinValue;

        // draaiwieltjes voor de tijd
        bool pickAt; int pickH, pickM, pickCol; string pickBuf = ""; bool pickForPreset;

        // venster verslepen
        bool dragging; Point dragStart, dragPanelStart;

        public Flyout(App app)
        {
            this.app = app;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            KeyPreview = true;
            Text = "Dusk";
            anim.Interval = 40;
            anim.Tick += delegate { if (Mode == View.Undo) UndoTick(); };
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= W.WS_EX_LAYERED | W.WS_EX_TOOLWINDOW | W.WS_EX_TOPMOST;
                return cp;
            }
        }

        protected override bool ShowWithoutActivation { get { return passive; } }

        // ---------------------------------------------------------------- opties in het menu
        public List<PowerAction> Actions()
        {
            return Settings.Current.MenuActions();
        }

        public PowerAction SelectedAction
        {
            get { List<PowerAction> l = Actions(); return l[Math.Max(0, Math.Min(l.Count - 1, selected))]; }
        }

        public void ResetChoice()
        {
            List<PowerAction> l = Actions();
            int i = l.IndexOf(Settings.Current.DefaultAction);
            if (i < 0) i = l.IndexOf(PowerAction.Shutdown);
            selected = Math.Max(0, i);
            if (!Power.Available(SelectedAction))
            {
                // standaardkeuze kan hier niet: de eerste optie die wel kan
                selected = 0;
                for (int k = 0; k < l.Count; k++) if (Power.Available(l[k])) { selected = k; break; }
            }
            choice = TimeChoice.Now;
            message = null;
            hover = null; press = null;
        }

        List<TimeChoice> Presets()
        {
            List<TimeChoice> l = new List<TimeChoice>();
            foreach (string p in Settings.Current.Presets) { TimeChoice t = TimeInput.Parse(p); if (t.Valid && !t.IsNow) l.Add(t); }
            return l;
        }

        // ---------------------------------------------------------------- tonen en verbergen
        public void ShowFlyout(bool activate, Origin origin)
        {
            if (app.Scheduler.Active) Mode = View.Running;
            else if (Mode != View.Undo) { if (!Visible) { Mode = View.Choose; ResetChoice(); } else if (Mode == View.Running) Mode = View.Choose; }
            passive = !activate;
            if (!Visible) { lastOrigin = origin; Place(origin); }
            Render();
            if (!Visible) Show();
            W.SetWindowPos(Handle, companionOf != IntPtr.Zero ? W.HWND_TOP : W.HWND_TOPMOST, 0, 0, 0, 0, W.SWP_NOMOVE | W.SWP_NOSIZE | (activate ? 0 : W.SWP_NOACTIVATE));
            if (activate) { Activate(); W.ForceForeground(Handle); }
        }

        // Andere grootte gekozen: opnieuw neerzetten op dezelfde soort plek
        Origin lastOrigin = Origin.Launch;

        public void Rescale()
        {
            if (!Visible) return;
            Place(lastOrigin);
            lastHeight = 0;
            Render();
        }

        // Terwijl Instellingen open staat hoort het venster bij Instellingen en staat het niet altijd bovenop.
        // Zo komen vragen van Dusk en andere programma's er gewoon overheen, en gaat het mee weg als je
        // Instellingen minimaliseert of een ander programma kiest.
        IntPtr companionOf = IntPtr.Zero;

        public void Companion(IntPtr owner)
        {
            companionOf = owner;
            if (!IsHandleCreated) return;
            TopMost = false;
            W.SetOwner(Handle, owner);
            W.SetWindowPos(Handle, W.HWND_NOTOPMOST, 0, 0, 0, 0, W.SWP_NOMOVE | W.SWP_NOSIZE | W.SWP_NOACTIVATE);
        }

        // Weer los en altijd bovenop (moet gebeuren voordat Instellingen dichtgaat, anders gaat dit venster mee)
        public void Standalone()
        {
            if (companionOf == IntPtr.Zero) return;
            companionOf = IntPtr.Zero;
            if (!IsHandleCreated) return;
            W.SetOwner(Handle, IntPtr.Zero);
            TopMost = true;
        }

        public void ShowMessage(string text, bool error)
        {
            message = text; messageIsError = error;
            if (Visible) Render();
        }

        public void HideFlyout()
        {
            if (Mode == View.Undo) { anim.Stop(); Mode = View.Choose; }
            if (Mode == View.Picker) Mode = View.Choose;
            passive = false; dragging = false;
            Hide();
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            // Klik je ergens anders, dan gaat het venster weg (zoals de klok of het volume van Windows).
            // Tijdens het aftellen blijft het staan, zodat je ziet wat er gaat gebeuren.
            if (Mode != View.Undo && !app.SettingsOpen && !menuOpen) { HideFlyout(); LastAutoHide = DateTime.UtcNow; }
        }

        // Altijd even groot, in elke stand: dan springt er niets als je wisselt
        int Height_() { int n = Actions().Count; return Pad + 48 + n * 48 + (n - 1) * 2 + 17 + 36 + 8 + 30 + 10 + 20 + 10 + 40 + Pad; }
        float BtnY { get { return Height_() - Pad - 40; } }
        float InfoY { get { return BtnY - 10 - 20; } }
        float ChipsY { get { return InfoY - 10 - 30; } }
        float TimeY { get { return ChipsY - 8 - 36; } }

        // Waar komt het venster? Bij de taakbalk, in het midden, of waar je hem het laatst neerzette.
        void Place(Origin origin)
        {
            string mode = Settings.Current.OpenAt;
            bool tray = mode == "tray" || (mode == "smart" && (origin == Origin.Tray || origin == Origin.Passive));
            if (origin == Origin.Passive && mode != "last") tray = mode != "center";
            Screen scr = tray || mode == "last" ? Screen.PrimaryScreen : Screen.FromPoint(Cursor.Position);
            if (mode == "last" && Settings.Current.LastX != int.MinValue)
            {
                Point p = new Point(Settings.Current.LastX, Settings.Current.LastY);
                scr = Screen.FromPoint(p);
                s = W.DpiScaleAt(p) * Settings.Current.Zoom / 100f;
                Rectangle wa0 = scr.WorkingArea;
                int w0 = (int)Math.Round(PanelW * s), h0 = (int)Math.Round(Height_() * s);
                panelPos = new Point(Math.Max(wa0.Left, Math.Min(wa0.Right - w0, p.X)), Math.Max(wa0.Top, Math.Min(wa0.Bottom - h0, p.Y)));
                anchorBottom = false;
                return;
            }
            Rectangle wa = scr.WorkingArea;
            s = W.DpiScaleAt(new Point(wa.Left + wa.Width / 2, wa.Top + wa.Height / 2)) * Settings.Current.Zoom / 100f;
            int pw = (int)Math.Round(PanelW * s), ph = (int)Math.Round(Height_() * s), gap = (int)Math.Round(12 * s);
            if (!tray && mode != "last")
            {
                panelPos = new Point(wa.Left + (wa.Width - pw) / 2, wa.Top + (wa.Height - ph) / 2);
                anchorBottom = false;
                return;
            }
            uint edge = 3;
            try
            {
                W.APPBARDATA abd = new W.APPBARDATA();
                abd.cbSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(W.APPBARDATA));
                if (W.SHAppBarMessage(5, ref abd) != IntPtr.Zero) edge = abd.uEdge;   // ABM_GETTASKBARPOS
            }
            catch { }
            int x = edge == 0 ? wa.Left + gap : wa.Right - gap - pw;
            int y = edge == 1 ? wa.Top + gap : wa.Bottom - gap - ph;
            panelPos = new Point(x, y);
            anchorBottom = edge != 1;
        }

        Rectangle PanelOnScreen()
        {
            int pw = (int)Math.Round(PanelW * s), ph = (int)Math.Round(Height_() * s);
            return new Rectangle(panelPos.X, panelPos.Y, pw, ph);
        }

        // ---------------------------------------------------------------- tekenen
        public Bitmap Draw(float scale, Rectangle onScreen)
        {
            s = scale;
            int m = (int)Math.Round(Margin_ * s);
            int pw = (int)Math.Round(PanelW * s), ph = (int)Math.Round(Height_() * s);
            Bitmap bmp = new Bitmap(pw + 2 * m, ph + 2 * m, PixelFormat.Format32bppArgb);
            Palette p = Palette.Current();
            zones.Clear();
            using (Graphics g = Graphics.FromImage(bmp))
            {
                Ui.Setup(g);
                g.Clear(Color.Transparent);
                RectangleF panel = new RectangleF(m, m, pw, ph);
                float rad = 12 * s;
                Ui.Shadow(g, panel, rad, s, p.Dark);
                g.Flush();
                // De inhoud eerst op een dichte laag: daar kan Windows de tekst zo scherp zetten als in Verkenner
                using (Bitmap inner = new Bitmap(pw, ph, PixelFormat.Format32bppRgb))
                {
                    using (Graphics gi = Graphics.FromImage(inner))
                    {
                        Ui.Setup(gi);
                        string mode = Settings.Current.Backdrop;
                        p.PaintBackdrop(gi, new Rectangle(0, 0, pw, ph), onScreen, mode);
                        Ui.Back = p.TextBack(mode);
                        switch (Mode)
                        {
                            case View.Running: DrawRunning(gi, p); break;
                            case View.Undo: DrawUndo(gi, p); break;
                            case View.Picker: DrawPicker(gi, p); break;
                            default: DrawChoose(gi, p); break;
                        }
                    }
                    Ui.PutRounded(bmp, inner, m, m, rad);
                }
                Ui.Stroke(g, panel, rad, p.Border, Math.Max(1f, s));
            }
            for (int i = 0; i < zones.Count; i++)
            {
                RectangleF r = zones[i].Key; r.Offset(m, m);
                zones[i] = new KeyValuePair<RectangleF, string>(r, zones[i].Value);
            }
            return bmp;
        }

        float S(float v) { return v * s; }
        RectangleF R(float x, float y, float w, float h) { return new RectangleF(x * s, y * s, w * s, h * s); }
        void Zone(RectangleF r, string id) { zones.Add(new KeyValuePair<RectangleF, string>(r, id)); }
        Color Hot(string id, Color normal, Color hot) { return hover == id ? hot : normal; }

        void DrawHeader(Graphics g, Palette p)
        {
            Zone(R(0, 0, PanelW - Pad - 3 * 34 - 4, Pad + 40), "drag");   // aan de bovenkant kun je het venster verslepen
            Ui.Logo(g, R(Pad + 8, Pad + 8, 20, 20), p.Dark ? Color.FromArgb(224, 231, 255) : Color.FromArgb(55, 48, 163), 4.5f, 1f, Color.Empty);
            using (Font f = Ui.F(14, s, true)) Ui.Text(g, "Dusk", f, p.T1, R(Pad + 38, Pad + 2, 140, 32), -1);
            // rechts: instellingen, naar de taakbalk, helemaal stoppen
            float bx = PanelW - Pad - 2;
            string[] ids = { "quit", "min", "gear" };
            string[] icons = { "close", "minus", "gear" };
            for (int i = 0; i < 3; i++)
            {
                bx -= 32;
                RectangleF b = R(bx, Pad + 4, 32, 32);
                Color bg = hover == ids[i] ? (ids[i] == "quit" ? Color.FromArgb(196, 43, 28) : p.Sel) : (ids[i] == "gear" ? p.Fill : Color.Transparent);
                Ui.Fill(g, b, S(8), bg);
                Color ic = hover == "quit" && ids[i] == "quit" ? Color.White : p.T2;
                float isz = ids[i] == "gear" ? 18 : 14;
                Ui.Icon(g, icons[i], new RectangleF(b.X + (b.Width - S(isz)) / 2, b.Y + (b.Height - S(isz)) / 2, S(isz), S(isz)), ic, ids[i] == "gear" ? 1.6f : 1.8f);
                Zone(b, ids[i]);
                bx -= 2;
            }
        }

        // De regel boven de knop: wat er gebeurt als je op Enter drukt (of een foutmelding)
        void DrawInfo(Graphics g, Palette p, string text, bool error)
        {
            using (Font f = Ui.F(12, s, error)) Ui.Text(g, text, f, error ? p.Danger : p.T2, R(Pad, InfoY, PanelW - 2 * Pad, 20), 0);
        }

        void DrawChoose(Graphics g, Palette p)
        {
            DrawHeader(g, p);
            List<PowerAction> acts = Actions();
            float y = Pad + 48;
            float x = Pad, w = PanelW - 2 * Pad;
            using (Font f = Ui.F(14, s, false))
            using (Font fb = Ui.F(14, s, true))
            using (Font fs = Ui.F(12, s, false))
            {
                for (int i = 0; i < acts.Count; i++)
                {
                    PowerAction a = acts[i];
                    bool sel = i == selected;
                    bool ok = Power.Available(a);
                    RectangleF row = R(x, y, w, 48);
                    string id = "row" + i;
                    Ui.Fill(g, row, S(8), sel ? p.Sel : (hover == id && ok ? p.Fill : Color.Transparent));
                    if (sel) Ui.Fill(g, R(x, y + 15, 3, 18), S(1.5f), p.Accent);
                    Color tc = ok ? p.T1 : p.T3;
                    Ui.ActionIcon(g, a, R(x + 13, y + 13, 22, 22), sel ? p.Accent : tc);
                    Ui.Text(g, Lang.ActionName(a), sel ? fb : f, tc, R(x + 47, y, w - 47 - 44, 48), -1);
                    if (!ok) Ui.Text(g, Lang.T("unavailable"), fs, p.T3, R(x + 150, y, w - 150 - 44, 48), 1);
                    Ui.Key(g, (i + 1).ToString(), R(x + w - 10 - 24, y + 12, 24, 24), s,
                        Color.FromArgb(sel ? (p.Dark ? 80 : 90) : (p.Dark ? 40 : 46), p.T1), sel ? p.T1 : p.T3);
                    Zone(row, id);
                    y += 50;
                }
            }
            using (SolidBrush b = new SolidBrush(p.Stroke)) g.FillRectangle(b, Pad * s + S(4), (TimeY - 9) * s, (PanelW - 2 * Pad - 8) * s, Math.Max(1f, s));

            // [−] [ Nu ] [+]
            float right = PanelW - Pad - 4, left = Pad + 4;
            RectangleF minus = R(left, TimeY, 36, 36), plus = R(right - 36, TimeY, 36, 36);
            RectangleF field = R(left + 36 + 8, TimeY, right - left - 2 * 36 - 16, 36);
            DrawSmallButton(g, p, minus, "minus", "minus");
            DrawSmallButton(g, p, plus, "plus", "plus");
            Color bg = p.Dark ? Color.FromArgb(56, 0, 0, 0) : Color.FromArgb(200, 255, 255, 255);
            Ui.Fill(g, field, S(8), hover == "field" ? Blend(bg, p.Dark ? Color.White : Color.Black, 0.04f) : bg);
            Ui.Stroke(g, field, S(8), p.Stroke, Math.Max(1f, s));
            using (GraphicsPath clip = Ui.Round(field, S(8)))
            {
                GraphicsState st = g.Save();
                g.SetClip(clip, CombineMode.Intersect);
                using (SolidBrush b = new SolidBrush(p.T3)) g.FillRectangle(b, field.X, field.Bottom - Math.Max(1f, s), field.Width, Math.Max(1f, s));
                g.Restore(st);
            }
            using (Font f = Ui.F(14, s, true)) Ui.Text(g, TimeInput.Label(choice), f, p.T1, field, 0);
            Zone(field, "field");

            // vaste tijden, en een + om er zelf een toe te voegen
            List<TimeChoice> pre = Presets();
            bool canAdd = pre.Count < Settings.MaxPresets;
            int slots = Math.Max(1, pre.Count + (canAdd ? 1 : 0));
            float cw = (PanelW - 2 * Pad - 8 - (slots - 1) * 6) / (float)slots;
            using (Font f = Ui.F(12, s, false))
            using (Font fb = Ui.F(12, s, true))
            {
                for (int i = 0; i < slots; i++)
                {
                    RectangleF c = R(left + i * (cw + 6), ChipsY, cw, 30);
                    bool isAdd = i >= pre.Count;
                    string id = isAdd ? "addpreset" : "chip" + i;
                    bool on = !isAdd && Same(pre[i], choice);
                    Ui.Fill(g, c, S(6), on ? p.Sel : Hot(id, p.Fill, p.Sel));
                    Ui.Stroke(g, c, S(6), on ? Color.FromArgb(p.Dark ? 60 : 50, p.T1) : p.Stroke, Math.Max(1f, s));
                    if (isAdd) Ui.Icon(g, "plus", new RectangleF(c.X + c.Width / 2 - S(7), c.Y + c.Height / 2 - S(7), S(14), S(14)), p.T2, 2f);
                    else Ui.Text(g, TimeInput.Label(pre[i]), on ? fb : f, on ? p.T1 : p.T2, c, 0);
                    Zone(c, id);
                }
            }

            if (message != null) DrawInfo(g, p, message, messageIsError);
            else DrawInfo(g, p, Lang.Info(SelectedAction, choice, Settings.Current.DelaySeconds, DateTime.Now), false);
            DrawGo(g, p, R(Pad, BtnY, PanelW - 2 * Pad, 40), choice.IsNow ? Lang.ActionName(SelectedAction) : Lang.T("btn.start"), "go", true);
        }

        static bool Same(TimeChoice a, TimeChoice b)
        {
            if (!a.Valid || !b.Valid || a.IsClock != b.IsClock) return false;
            return a.IsClock ? a.Hour == b.Hour && a.Minute == b.Minute : a.Minutes == b.Minutes;
        }

        void DrawSmallButton(Graphics g, Palette p, RectangleF r, string icon, string id)
        {
            Ui.Fill(g, r, S(8), Hot(id, p.Fill, p.Sel));
            Ui.Stroke(g, r, S(8), p.Stroke, Math.Max(1f, s));
            Ui.Icon(g, icon, new RectangleF(r.X + r.Width / 2 - S(7), r.Y + r.Height / 2 - S(7), S(14), S(14)), p.T1, 2f);
            Zone(r, id);
        }

        void DrawGo(Graphics g, Palette p, RectangleF r, string label, string id, bool showEnter)
        {
            Color c = p.Accent;
            if (press == id) c = Blend(c, Color.Black, 0.12f);
            else if (hover == id) c = Blend(c, Color.White, p.Dark ? 0.10f : 0.08f);
            Ui.Fill(g, r, S(8), c);
            using (Font f = Ui.F(14, s, true))
            using (Font fk = Ui.F(11, s, true))
            {
                float tw = Ui.Width(g, label, f);
                float kw = showEnter ? Ui.Width(g, "Enter", fk) + S(12) : 0;
                float gap = showEnter ? S(10) : 0;
                float x0 = r.X + (r.Width - (tw + gap + kw)) / 2;
                Ui.TextAt(g, label, f, p.OnAccent, x0, r);
                if (showEnter)
                {
                    RectangleF k = new RectangleF(x0 + tw + gap, r.Y + r.Height / 2 - S(10), kw, S(20));
                    Ui.Fill(g, k, S(4), Color.FromArgb(30, p.OnAccent));
                    Ui.TextAt(g, "Enter", fk, p.OnAccent, k.X + S(6), k);
                }
            }
            Zone(r, id);
        }

        void DrawOutlineButton(Graphics g, Palette p, RectangleF r, string label, string id)
        {
            Ui.Fill(g, r, S(8), press == id ? p.Sel : Hot(id, p.Fill, p.Sel));
            Ui.Stroke(g, r, S(8), p.Stroke, Math.Max(1f, s));
            using (Font f = Ui.F(13, s, false)) Ui.Text(g, label, f, p.T1, r, 0);
            Zone(r, id);
        }

        static Color Blend(Color a, Color b, float t)
        {
            return Color.FromArgb(a.A, (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }

        void Ring(Graphics g, Palette p, PointF c, float r, float width, float fraction)
        {
            RectangleF rr = new RectangleF(c.X - r, c.Y - r, 2 * r, 2 * r);
            using (Pen t = new Pen(p.Dark ? Color.FromArgb(34, 255, 255, 255) : Color.FromArgb(26, 0, 0, 0), width)) g.DrawEllipse(t, rr);
            if (fraction <= 0.001f) return;
            using (Pen a = new Pen(p.Accent, width))
            {
                a.StartCap = LineCap.Round; a.EndCap = LineCap.Round;
                g.DrawArc(a, rr, -90, 360 * Math.Min(1f, fraction));
            }
        }

        // Midden van het vrije vlak tussen de kop en de regel boven de knop
        PointF Center(out float radius)
        {
            float top = Pad + 48, bottom = InfoY - 10;
            radius = Math.Min(84, (bottom - top) / 2 - 12);
            return new PointF(S(PanelW / 2f), S((top + bottom) / 2));
        }

        void DrawRunning(Graphics g, Palette p)
        {
            DrawHeader(g, p);
            Scheduler sc = app.Scheduler;
            float r;
            PointF c = Center(out r);
            Ring(g, p, c, S(r), S(6), sc.Fraction);
            Ui.ActionIcon(g, sc.Action, new RectangleF(c.X - S(11), c.Y - S(58), S(22), S(22)), p.Accent);
            using (Font f = Ui.F(12, s, false)) Ui.Text(g, Lang.ActionName(sc.Action), f, p.T2, new RectangleF(c.X - S(80), c.Y - S(34), S(160), S(18)), 0);
            using (Font f = Ui.Big(38, s)) Ui.Text(g, TimeInput.Clock(sc.Remaining), f, p.T1, new RectangleF(c.X - S(80), c.Y - S(18), S(160), S(46)), 0);
            using (Font f = Ui.F(12, s, false)) Ui.Text(g, Lang.F("running.at", sc.Target.ToString("HH:mm")), f, p.T3, new RectangleF(c.X - S(80), c.Y + S(28), S(160), S(18)), 0);
            if (message != null) DrawInfo(g, p, message, messageIsError);
            else if (sc.Warned) DrawInfo(g, p, Lang.T("warn.save"), false);
            float bw = (PanelW - 2 * Pad - 2 * 8) / 3f;
            DrawOutlineButton(g, p, R(Pad, BtnY, bw, 40), Lang.F("btn.add", Settings.Current.StepMinutes), "add");
            DrawOutlineButton(g, p, R(Pad + bw + 8, BtnY, bw, 40), Lang.T("btn.cancel"), "cancel");
            DrawGo(g, p, R(Pad + 2 * (bw + 8), BtnY, bw, 40), Lang.T("btn.donow"), "donow", false);
        }

        void DrawUndo(Graphics g, Palette p)
        {
            DrawHeader(g, p);
            double left = Math.Max(0, (undoEnd - DateTime.Now).TotalSeconds);
            float frac = (float)(left / Math.Max(1, Settings.Current.DelaySeconds));
            float r;
            PointF c = Center(out r);
            Ring(g, p, c, S(r), S(6), frac);
            Ui.ActionIcon(g, undoAction, new RectangleF(c.X - S(11), c.Y - S(58), S(22), S(22)), p.Accent);
            using (Font f = Ui.F(12, s, false)) Ui.Text(g, Lang.ActionName(undoAction), f, p.T2, new RectangleF(c.X - S(80), c.Y - S(34), S(160), S(18)), 0);
            using (Font f = Ui.Big(48, s)) Ui.Text(g, Math.Ceiling(left).ToString("0"), f, p.T1, new RectangleF(c.X - S(80), c.Y - S(20), S(160), S(56)), 0);
            DrawInfo(g, p, Lang.T("undo.hint"), false);
            float bw = (PanelW - 2 * Pad - 8) / 2f;
            DrawOutlineButton(g, p, R(Pad, BtnY, bw, 40), Lang.T("btn.cancel"), "undo.cancel");
            DrawGo(g, p, R(Pad + bw + 8, BtnY, bw, 40), Lang.T("btn.donow"), "undo.now", false);
        }

        // Draaiwieltjes: uren en minuten, "over" of "om". Scrollen, pijltjes of gewoon typen.
        void DrawPicker(Graphics g, Palette p)
        {
            DrawHeader(g, p);
            float y = Pad + 52;
            // Over | Om
            float segW = 220, segX = (PanelW - segW) / 2;
            RectangleF seg = R(segX, y, segW, 34);
            Ui.Fill(g, seg, S(8), p.Fill);
            Ui.Stroke(g, seg, S(8), p.Stroke, Math.Max(1f, s));
            for (int i = 0; i < 2; i++)
            {
                RectangleF part = R(segX + 3 + i * (segW - 6) / 2, y + 3, (segW - 6) / 2, 28);
                bool on = (i == 1) == pickAt;
                if (on) Ui.Fill(g, part, S(6), p.Sel);
                using (Font f = Ui.F(13, s, on)) Ui.Text(g, Lang.T(i == 0 ? "picker.in" : "picker.at"), f, on ? p.T1 : p.T2, part, 0);
                Zone(part, "pmode" + i);
            }
            float top = y + 34 + 12, bottom = InfoY - 10;
            float colW = 104, gapW = 28;
            float cx = PanelW / 2f;
            float midY = (top + bottom) / 2;
            for (int col = 0; col < 2; col++)
            {
                float x = col == 0 ? cx - gapW / 2 - colW : cx + gapW / 2;
                int val = col == 0 ? pickH : pickM;
                int prev = Wrap(col, val, -1), next = Wrap(col, val, 1);
                RectangleF area = R(x, top, colW, bottom - top);
                Zone(area, "pcol" + col);
                bool focus = pickCol == col;
                RectangleF mid = R(x, midY - 30, colW, 60);
                Ui.Fill(g, mid, S(10), focus ? p.Sel : p.Fill);
                if (focus) Ui.Stroke(g, mid, S(10), p.Accent, S(1.5f));
                string cur = pickCol == col && pickBuf.Length > 0 ? pickBuf : val.ToString("00");
                using (Font fb = Ui.Big(36, s)) Ui.Text(g, cur, fb, p.T1, mid, 0);
                using (Font fs = Ui.F(20, s, false))
                {
                    RectangleF up = R(x, midY - 30 - 46, colW, 40), dn = R(x, midY + 30 + 6, colW, 40);
                    if (prev >= 0) Ui.Text(g, prev.ToString("00"), fs, p.T3, up, 0);
                    if (next >= 0) Ui.Text(g, next.ToString("00"), fs, p.T3, dn, 0);
                    Zone(up, "pup" + col); Zone(dn, "pdn" + col);
                }
                using (Font fl = Ui.F(11, s, false)) Ui.Text(g, Lang.T(col == 0 ? "picker.h" : "picker.m"), fl, p.T3, R(x, midY + 30 + 44, colW, 16), 0);
            }
            using (Font fc = Ui.Big(32, s)) Ui.Text(g, ":", fc, p.T2, R(cx - gapW / 2, midY - 30, gapW, 56), 0);
            TimeChoice t = TimeInput.FromPicker(pickAt, pickH, pickM);
            string info = pickForPreset ? Lang.F("picker.preset", TimeInput.Label(t)) : Lang.Info(SelectedAction, t, Settings.Current.DelaySeconds, DateTime.Now);
            DrawInfo(g, p, info, false);
            float bw = (PanelW - 2 * Pad - 8) / 2f;
            DrawOutlineButton(g, p, R(Pad, BtnY, bw, 40), Lang.T("btn.cancel"), "pcancel");
            DrawGo(g, p, R(Pad + bw + 8, BtnY, bw, 40), Lang.T("btn.done"), "pdone", true);
        }

        // Volgende of vorige waarde van een wieltje. Uren "over": 0 tot 24, zonder rondgaan.
        int Wrap(int col, int val, int dir)
        {
            if (col == 1)
            {
                // minuten springen per 5 (typen kan elke minuut)
                int v5 = dir > 0 ? (val / 5 + 1) * 5 : (val % 5 == 0 ? val - 5 : val / 5 * 5);
                return (v5 % 60 + 60) % 60;
            }
            if (pickAt) return ((val + dir) % 24 + 24) % 24;
            int v = val + dir;
            return v < 0 || v > 24 ? -1 : v;
        }

        void PickStep(int col, int dir, bool big)
        {
            pickBuf = "";
            if (col == 0) pickH = pickAt ? ((pickH + dir) % 24 + 24) % 24 : Math.Max(0, Math.Min(24, pickH + dir));
            else
            {
                int step = big ? 5 : 1;
                int m = pickM + dir * step;
                if (big) m = dir > 0 ? (pickM / 5 + 1) * 5 : (pickM % 5 == 0 ? pickM - 5 : pickM / 5 * 5);
                if (m >= 60) { m -= 60; if (!pickAt && pickH < 24) pickH++; else if (pickAt) pickH = (pickH + 1) % 24; }
                if (m < 0) { m += 60; if (!pickAt && pickH > 0) pickH--; else if (pickAt) pickH = (pickH + 23) % 24; else m = 0; }
                pickM = m;
            }
            if (!pickAt && pickH == 24) pickM = 0;
        }

        void OpenPicker(bool forPreset)
        {
            Usage.Count(forPreset ? "vaste-tijd.toegevoegd" : "tijd-kiezen");
            pickForPreset = forPreset;
            pickBuf = ""; pickCol = 0;
            if (choice.IsClock) { pickAt = true; pickH = choice.Hour; pickM = choice.Minute; }
            else if (choice.Valid && choice.Minutes > 0) { pickAt = false; pickH = choice.Minutes / 60; pickM = choice.Minutes % 60; }
            else
            {
                // standaard: om een rond tijdstip zo'n half uur van nu, of over 30 minuten
                pickAt = false; pickH = 0; pickM = 30;
            }
            Mode = View.Picker;
        }

        void PickerToggleMode(bool at)
        {
            if (at == pickAt) return;
            pickAt = at; pickBuf = "";
            DateTime now = DateTime.Now;
            if (at)
            {
                DateTime t = now.AddMinutes(30);
                pickH = t.Hour; pickM = (t.Minute / 5) * 5;
            }
            else { pickH = 0; pickM = 30; }
        }

        void PickerDone()
        {
            CommitBuffer();
            TimeChoice t = TimeInput.FromPicker(pickAt, pickH, pickM);
            if (pickForPreset)
            {
                if (!t.IsNow)
                {
                    string txt = TimeInput.PresetText(t);
                    List<string> l = Settings.Current.Presets;
                    if (!l.Contains(txt) && l.Count < Settings.MaxPresets) { l.Add(txt); app.SettingsChanged(); }
                    choice = t;
                }
            }
            else choice = t;
            Mode = View.Choose;
        }

        void CommitBuffer()
        {
            if (pickBuf.Length == 0) return;
            int v = int.Parse(pickBuf);
            if (pickCol == 0) pickH = pickAt ? Math.Min(23, v) : Math.Min(24, v);
            else pickM = Math.Min(59, v);
            if (!pickAt && pickH == 24) pickM = 0;
            pickBuf = "";
        }

        // ---------------------------------------------------------------- op het scherm zetten
        public void Render()
        {
            if (!IsHandleCreated) CreateHandle();
            Rectangle pr = PanelOnScreen();
            if (anchorBottom && Visible && lastHeight > 0 && lastHeight != pr.Height)
            {
                panelPos = new Point(panelPos.X, panelPos.Y + lastHeight - pr.Height);   // bij de taakbalk: onderkant blijft staan
                pr = PanelOnScreen();
            }
            lastHeight = pr.Height;
            int m = (int)Math.Round(Margin_ * s);
            using (Bitmap bmp = Draw(s, pr))
                PushLayered(bmp, new Point(pr.X - m, pr.Y - m));
        }
        int lastHeight;

        void PushLayered(Bitmap bmp, Point pos)
        {
            IntPtr screenDc = W.GetDC(IntPtr.Zero);
            IntPtr memDc = W.CreateCompatibleDC(screenDc);
            IntPtr hBmp = IntPtr.Zero, old = IntPtr.Zero;
            try
            {
                hBmp = bmp.GetHbitmap(Color.FromArgb(0));
                old = W.SelectObject(memDc, hBmp);
                W.SIZE size = new W.SIZE(bmp.Width, bmp.Height);
                W.POINT src = new W.POINT(0, 0), dst = new W.POINT(pos.X, pos.Y);
                W.BLENDFUNCTION bf = new W.BLENDFUNCTION();
                bf.BlendOp = 0; bf.SourceConstantAlpha = 255; bf.AlphaFormat = 1;
                W.UpdateLayeredWindow(Handle, screenDc, ref dst, ref size, memDc, ref src, 0, ref bf, W.ULW_ALPHA);
            }
            finally
            {
                if (old != IntPtr.Zero) W.SelectObject(memDc, old);
                if (hBmp != IntPtr.Zero) W.DeleteObject(hBmp);
                W.DeleteDC(memDc);
                W.ReleaseDC(IntPtr.Zero, screenDc);
            }
        }

        // ---------------------------------------------------------------- muis
        string HitTest(Point pt)
        {
            for (int i = zones.Count - 1; i >= 0; i--) if (zones[i].Key.Contains(pt)) return zones[i].Value;
            return null;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (dragging)
            {
                Point c = Cursor.Position;
                panelPos = new Point(dragPanelStart.X + c.X - dragStart.X, dragPanelStart.Y + c.Y - dragStart.Y);
                anchorBottom = false;
                Render();
                return;
            }
            string h = HitTest(e.Location);
            Cursor = h == null ? Cursors.Default : h == "drag" ? Cursors.SizeAll : h.StartsWith("pcol") ? Cursors.SizeNS : Cursors.Hand;
            if (h == "drag" || (h != null && h.StartsWith("pcol"))) h = null;
            if (h != hover) { hover = h; Render(); }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (!dragging && (hover != null || press != null)) { hover = null; press = null; Render(); }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            string h = HitTest(e.Location);
            if (h == "drag")
            {
                dragging = true; dragStart = Cursor.Position; dragPanelStart = panelPos;
                return;
            }
            press = h;
            if (press != null) Render();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (dragging)
            {
                dragging = false;
                if (Settings.Current.OpenAt == "last") { Settings.Current.LastX = panelPos.X; Settings.Current.LastY = panelPos.Y; app.SettingsChanged(); }
                return;
            }
            string h = HitTest(e.Location);
            if (e.Button == MouseButtons.Right)
            {
                if (Mode == View.Choose && h != null && h.StartsWith("chip")) PresetMenu(int.Parse(h.Substring(4)), e.Location);
                return;
            }
            if (e.Button != MouseButtons.Left) return;
            string was = press;
            press = null;
            if (h != null && h == was) ZoneClick(h);
            else Render();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            string h = HitTest(e.Location);
            int dir = e.Delta > 0 ? -1 : 1;   // omhoog scrollen = vorige waarde (zoals bij een draaiwiel)
            if (Mode == View.Picker && h != null && (h.StartsWith("pcol") || h.StartsWith("pup") || h.StartsWith("pdn")))
            {
                int col = h[h.Length - 1] - '0';
                pickCol = col; PickStep(col, dir, true); Render();
            }
            else if (Mode == View.Choose && h == "field") { StepTime(-dir); Render(); }
        }

        bool menuOpen;

        void PresetMenu(int index, Point at)
        {
            ContextMenuStrip m = new ContextMenuStrip();
            Palette p = Palette.Current();
            m.ShowImageMargin = false;
            m.BackColor = p.Dark ? Color.FromArgb(44, 44, 46) : Color.FromArgb(249, 249, 249);
            m.ForeColor = p.T1;
            try { m.Font = new Font(Ui.Family, 9.5f); } catch { }
            m.Items.Add(Lang.T("menu.remove"), null, delegate
            {
                List<string> l = Settings.Current.Presets;
                if (index < l.Count) { l.RemoveAt(index); app.SettingsChanged(); }
                Render();
            }).ForeColor = p.T1;
            menuOpen = true;
            m.Closed += delegate { menuOpen = false; BeginInvoke((Action)delegate { m.Dispose(); }); };
            m.Show(this, at);
        }

        void ZoneClick(string id)
        {
            message = null;
            if (id == "gear") { app.OpenSettings(); return; }
            if (id == "min") { HideFlyout(); return; }          // naar de taakbalk, Dusk blijft klaarstaan
            if (id == "quit") { app.QuitAsk(this); return; }    // Dusk helemaal stoppen
            switch (Mode)
            {
                case View.Choose:
                    if (id.StartsWith("row"))
                    {
                        int i = int.Parse(id.Substring(3));
                        if (Power.Available(Actions()[i])) selected = i;
                    }
                    else if (id.StartsWith("chip"))
                    {
                        List<TimeChoice> pre = Presets();
                        int i = int.Parse(id.Substring(4));
                        if (i < pre.Count) { choice = Same(choice, pre[i]) ? TimeChoice.Now : pre[i]; Usage.Count("vaste-tijd." + TimeInput.Label(pre[i]).Replace(" ", "")); }   // nog een keer klikken = terug naar Nu
                    }
                    else if (id == "addpreset") OpenPicker(true);
                    else if (id == "minus") StepTime(-1);
                    else if (id == "plus") StepTime(1);
                    else if (id == "field") OpenPicker(false);
                    else if (id == "go") { Go(); return; }
                    break;
                case View.Running:
                    if (id == "add") app.Scheduler.Add(Settings.Current.StepMinutes);
                    else if (id == "cancel") { app.Scheduler.Cancel(); Usage.Count("timer.geannuleerd"); Mode = View.Choose; ResetChoice(); }
                    else if (id == "donow") { app.Execute(app.Scheduler.Action, true); return; }
                    break;
                case View.Undo:
                    if (id == "undo.cancel") { CancelUndo(); return; }
                    if (id == "undo.now") { anim.Stop(); app.Execute(undoAction, false); return; }
                    break;
                case View.Picker:
                    if (id == "pmode0") PickerToggleMode(false);
                    else if (id == "pmode1") PickerToggleMode(true);
                    else if (id.StartsWith("pup")) { pickCol = id[3] - '0'; PickStep(pickCol, -1, pickCol == 1); }
                    else if (id.StartsWith("pdn")) { pickCol = id[3] - '0'; PickStep(pickCol, 1, pickCol == 1); }
                    else if (id.StartsWith("pcol")) { CommitBuffer(); pickCol = id[4] - '0'; }
                    else if (id == "pcancel") Mode = View.Choose;
                    else if (id == "pdone") PickerDone();
                    break;
            }
            Render();
        }

        // ---------------------------------------------------------------- toetsenbord
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (HandleKey(keyData)) return true;
            return base.ProcessCmdKey(ref msg, keyData);
        }

        public bool HandleKey(Keys keyData)
        {
            Keys k = keyData & Keys.KeyCode;
            bool ctrl = (keyData & Keys.Control) != 0;
            if (ctrl && k == Keys.Oemcomma) { app.OpenSettings(); return true; }
            bool plus = k == Keys.Add || k == Keys.Oemplus;
            bool minus = k == Keys.Subtract || k == Keys.OemMinus;
            switch (Mode)
            {
                case View.Undo:
                    if (k == Keys.Escape || k == Keys.Back || k == Keys.Delete) { CancelUndo(); return true; }
                    if (k == Keys.Enter) { anim.Stop(); app.Execute(undoAction, false); return true; }
                    return true;
                case View.Running:
                    if (k == Keys.Escape) { HideFlyout(); return true; }
                    if (plus) { app.Scheduler.Add(Settings.Current.StepMinutes); Render(); return true; }
                    if (k == Keys.Delete || k == Keys.Back) { app.Scheduler.Cancel(); Mode = View.Choose; ResetChoice(); Render(); return true; }
                    if (k == Keys.Enter) { app.Execute(app.Scheduler.Action, true); return true; }
                    return false;
                case View.Picker:
                    return PickerKey(k);
            }
            message = null;
            if (k == Keys.Escape) { HideFlyout(); return true; }
            if (k == Keys.Up) { MoveSelection(-1); Render(); return true; }
            if (k == Keys.Down) { MoveSelection(1); Render(); return true; }
            if (k == Keys.Left || k == Keys.Right) { CyclePreset(k == Keys.Right ? 1 : -1); Render(); return true; }
            if (plus || minus) { StepTime(plus ? 1 : -1); Render(); return true; }
            if (k == Keys.Tab || k == Keys.Space) { OpenPicker(false); Render(); return true; }
            if (k == Keys.Enter) { Go(); return true; }
            int digit = -1;
            if (k >= Keys.D1 && k <= Keys.D9 && (keyData & Keys.Shift) == 0) digit = k - Keys.D1;
            else if (k >= Keys.NumPad1 && k <= Keys.NumPad9) digit = k - Keys.NumPad1;
            if (digit >= 0)
            {
                List<PowerAction> l = Actions();
                if (digit < l.Count && Power.Available(l[digit])) { selected = digit; Render(); }
                return true;
            }
            return false;
        }

        bool PickerKey(Keys k)
        {
            if (k == Keys.Escape) { Mode = View.Choose; Render(); return true; }
            if (k == Keys.Enter) { PickerDone(); Render(); return true; }
            if (k == Keys.Up) { PickStep(pickCol, -1, pickCol == 1); Render(); return true; }
            if (k == Keys.Down) { PickStep(pickCol, 1, pickCol == 1); Render(); return true; }
            if (k == Keys.Left || k == Keys.Right || k == Keys.Tab) { CommitBuffer(); pickCol = k == Keys.Left ? 0 : (k == Keys.Right ? 1 : 1 - pickCol); Render(); return true; }
            if (k == Keys.Space) { PickerToggleMode(!pickAt); Render(); return true; }
            if (k == Keys.Back) { pickBuf = pickBuf.Length > 0 ? pickBuf.Substring(0, pickBuf.Length - 1) : ""; Render(); return true; }
            int d = -1;
            if (k >= Keys.D0 && k <= Keys.D9) d = k - Keys.D0;
            else if (k >= Keys.NumPad0 && k <= Keys.NumPad9) d = k - Keys.NumPad0;
            if (d >= 0)
            {
                // typen: twee cijfers per wieltje, daarna door naar de minuten
                pickBuf += d.ToString();
                int max = pickCol == 0 ? (pickAt ? 23 : 24) : 59;
                if (pickBuf.Length == 2 || int.Parse(pickBuf) * 10 > max)
                {
                    CommitBuffer();
                    if (pickCol == 0) pickCol = 1;
                }
                Render();
                return true;
            }
            return true;
        }

        void MoveSelection(int dir)
        {
            List<PowerAction> l = Actions();
            for (int n = 0; n < l.Count; n++)
            {
                selected = (selected + dir + l.Count) % l.Count;
                if (Power.Available(l[selected])) return;
            }
        }

        void StepTime(int dir) { choice = TimeInput.Step(choice, Settings.Current.StepMinutes, dir, DateTime.Now); }

        // Links en rechts: Nu en dan de vaste tijden
        void CyclePreset(int dir)
        {
            List<TimeChoice> seq = new List<TimeChoice> { TimeChoice.Now };
            seq.AddRange(Presets());
            int cur = seq.FindIndex(t => Same(t, choice));
            int next = cur < 0 ? (dir > 0 ? 1 : 0) : Math.Max(0, Math.Min(seq.Count - 1, cur + dir));
            if (next < seq.Count) choice = seq[next];
        }

        // Enter of de grote knop
        void Go()
        {
            PowerAction a = SelectedAction;
            if (!Power.Available(a)) { message = Lang.T(a == PowerAction.Hibernate ? "err.hibernate" : "err.sleep"); messageIsError = true; Render(); return; }
            if (choice.IsNow)
            {
                if (Settings.Current.DelaySeconds > 0)
                {
                    undoAction = a;
                    undoEnd = DateTime.Now.AddSeconds(Settings.Current.DelaySeconds);
                    Mode = View.Undo;
                    anim.Start();
                    Render();
                }
                else app.Execute(a, false);
                return;
            }
            app.Scheduler.Start(a, choice.TargetFrom(DateTime.Now));
            Usage.Count("timer.gestart");
            Mode = View.Running;
            Render();
        }

        void UndoTick()
        {
            if (DateTime.Now >= undoEnd) { anim.Stop(); app.Execute(undoAction, false); return; }
            if (Visible) Render();
        }

        void CancelUndo()
        {
            Usage.Count("aftellen.gestopt");
            anim.Stop();
            Mode = View.Choose;
            Render();
        }

        // Vanuit het menu van het taakbalkicoon: meteen deze actie, met het gewone aftellen zodat je nog kunt stoppen
        public void StartNow(PowerAction a)
        {
            if (!Power.Available(a)) return;
            if (Settings.Current.DelaySeconds <= 0) { app.Execute(a, false); return; }
            Mode = View.Choose;
            ShowFlyout(true, Origin.Tray);
            undoAction = a;
            undoEnd = DateTime.Now.AddSeconds(Settings.Current.DelaySeconds);
            Mode = View.Undo;
            anim.Start();
            Render();
        }

        // ---------------------------------------------------------------- voor de zelftest
        public void TestSet(View v, int sel, TimeChoice t, string msg, bool err)
        {
            Mode = v; selected = sel; choice = t;
            message = msg; messageIsError = err; hover = null;
            undoAction = PowerAction.Shutdown; undoEnd = DateTime.Now.AddSeconds(7.4);
            if (v == View.Picker) { OpenPicker(false); pickAt = true; pickH = 23; pickM = 30; pickCol = 1; }
        }

        // Alt+F4 sluit het venster niet echt (dan zou Dusk stuk zijn), maar verbergt het
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing && !app.Quitting) { e.Cancel = true; HideFlyout(); return; }
            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) anim.Dispose();
            base.Dispose(disposing);
        }
    }
}
