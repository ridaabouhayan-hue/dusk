using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace Dusk
{
    // Het hoofdvenster rechtsonder bij de taakbalk. Tekent zichzelf helemaal (zachte hoeken, schaduw, Mica).
    public class Flyout : Form
    {
        public enum View { Choose, Running, Undo }

        const int PanelW = 352, Pad = 12, Margin_ = 18;   // logische punten; Margin_ is ruimte voor de schaduw

        readonly App app;
        public View Mode = View.Choose;
        float s = 1f;                         // schaal van het scherm (125% = 1,25)
        int selected = 3;
        TimeChoice choice = TimeChoice.Now;
        string fieldText = "";
        bool fieldFocus, fieldSelectAll;
        string message; bool messageIsError;
        string hover, press;
        readonly List<KeyValuePair<RectangleF, string>> zones = new List<KeyValuePair<RectangleF, string>>();
        public DateTime LastAutoHide = DateTime.MinValue;
        bool passive;                         // getoond zonder je toetsenbord over te nemen (waarschuwing)
        DateTime undoEnd;
        PowerAction undoAction;
        readonly Timer anim = new Timer();
        Rectangle panelOnScreen;
        public static readonly int[] Presets = { 15, 30, 45, 60, 120 };

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

        // ---------------------------------------------------------------- welke opties staan er in het menu
        public List<PowerAction> Actions()
        {
            List<PowerAction> l = new List<PowerAction> { PowerAction.Sleep, PowerAction.Hibernate, PowerAction.Restart, PowerAction.Shutdown };
            if (Settings.Current.ShowLockLogoff) { l.Add(PowerAction.Lock); l.Add(PowerAction.Logoff); }
            return l;
        }

        public PowerAction SelectedAction
        {
            get { List<PowerAction> l = Actions(); return l[Math.Max(0, Math.Min(l.Count - 1, selected))]; }
        }

        public void ResetChoice()
        {
            List<PowerAction> l = Actions();
            int i = l.IndexOf(Settings.Current.DefaultAction);
            selected = i >= 0 ? i : 3;
            if (!Power.Available(SelectedAction)) selected = l.IndexOf(PowerAction.Shutdown);
            choice = TimeChoice.Now;
            fieldText = TimeInput.Label(choice);
            fieldFocus = false; fieldSelectAll = false;
            message = null;
            hover = null; press = null;
        }

        // ---------------------------------------------------------------- tonen en verbergen
        public void ShowFlyout(bool activate)
        {
            if (app.Scheduler.Active) Mode = View.Running;
            else if (Mode != View.Undo) { Mode = View.Choose; if (!Visible) ResetChoice(); }
            passive = !activate;
            Place();
            Render();
            if (!Visible) Show();
            W.SetWindowPos(Handle, W.HWND_TOPMOST, 0, 0, 0, 0, W.SWP_NOMOVE | W.SWP_NOSIZE | (activate ? 0 : W.SWP_NOACTIVATE));
            if (activate) { Activate(); W.ForceForeground(Handle); }
        }

        public void ShowMessage(string text, bool error)
        {
            message = text; messageIsError = error;
            if (Visible) Render();
        }

        public void HideFlyout()
        {
            if (Mode == View.Undo) { anim.Stop(); Mode = View.Choose; }
            passive = false;
            Hide();
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            // Klik je ergens anders, dan gaat het venster weg (zoals bij de klok of het volume van Windows).
            // Tijdens de 5 tellen van "Nu" blijft het staan, zodat je kunt zien wat er gaat gebeuren.
            if (Mode != View.Undo && !app.SettingsOpen) { HideFlyout(); LastAutoHide = DateTime.UtcNow; }
        }

        int ContentHeight()
        {
            switch (Mode)
            {
                case View.Running:
                    return Pad + 48 + 168 + ((app.Scheduler.Warned || message != null) ? 24 : 0) + 12 + 40 + Pad;
                case View.Undo:
                    return Pad + 48 + 152 + 22 + 20 + 14 + 40 + Pad;
                default:
                    int n = Actions().Count;
                    return Pad + 48 + n * 48 + (n - 1) * 2 + 17 + 36 + 10 + 30 + (message != null ? 26 : 0) + 14 + 40 + Pad;
            }
        }

        // Rechtsonder boven de taakbalk (of waar je taakbalk ook staat), op het hoofdscherm
        void Place()
        {
            Screen scr = Screen.PrimaryScreen;
            Rectangle wa = scr.WorkingArea;
            s = W.DpiScaleAt(new Point(wa.Right - 10, wa.Bottom - 10));
            int pw = (int)Math.Round(PanelW * s), ph = (int)Math.Round(ContentHeight() * s), gap = (int)Math.Round(12 * s);
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
            panelOnScreen = new Rectangle(x, y, pw, ph);
        }

        // ---------------------------------------------------------------- tekenen
        public Bitmap Draw(float scale, Rectangle onScreen)
        {
            s = scale;
            int m = (int)Math.Round(Margin_ * s);
            int pw = (int)Math.Round(PanelW * s), ph = (int)Math.Round(ContentHeight() * s);
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
                using (GraphicsPath clip = Ui.Round(panel, rad))
                {
                    GraphicsState st = g.Save();
                    g.SetClip(clip);
                    p.PaintBackdrop(g, Rectangle.Round(panel), onScreen, Settings.Current.Backdrop);
                    g.TranslateTransform(m, m);
                    switch (Mode)
                    {
                        case View.Running: DrawRunning(g, p); break;
                        case View.Undo: DrawUndo(g, p); break;
                        default: DrawChoose(g, p); break;
                    }
                    g.Restore(st);
                }
                Ui.Stroke(g, panel, rad, p.Border, Math.Max(1f, s));
            }
            // zones staan in panelcoordinaten; verschuif ze naar het venster
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
            Ui.Logo(g, R(Pad + 8, Pad + 8, 20, 20), p.Dark ? Color.FromArgb(224, 231, 255) : Color.FromArgb(55, 48, 163), 4.5f, 1f, Color.Empty);
            using (Font f = Ui.F(14, s, true)) Ui.Text(g, "Dusk", f, p.T1, R(Pad + 38, Pad + 2, 160, 32), -1);
            RectangleF gear = R(PanelW - Pad - 4 - 36, Pad + 2, 36, 36);
            Ui.Fill(g, gear, S(8), Hot("gear", p.Fill, p.Sel));
            Ui.Icon(g, "gear", new RectangleF(gear.X + S(9.5f), gear.Y + S(9.5f), S(17), S(17)), p.T2, 1.6f);
            Zone(gear, "gear");
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
                    bool sel = i == selected && !fieldFocus;
                    bool selDim = i == selected && fieldFocus;
                    bool ok = Power.Available(a);
                    RectangleF row = R(x, y, w, 48);
                    string id = "row" + i;
                    Color bg = sel ? p.Sel : selDim ? p.Fill : (hover == id && ok ? p.Fill : Color.Transparent);
                    Ui.Fill(g, row, S(8), bg);
                    if (i == selected) Ui.Fill(g, R(x, y + 15, 3, 18), S(1.5f), p.Accent);
                    Color tc = ok ? p.T1 : p.T3;
                    Ui.Icon(g, Ui.IconFor(a), R(x + 14, y + 14.5f, 19, 19), tc, i == selected ? 1.8f : 1.6f);
                    Ui.Text(g, Lang.ActionName(a), i == selected ? fb : f, tc, R(x + 47, y, w - 47 - 44, 48), -1);
                    if (!ok) Ui.Text(g, Lang.T("unavailable"), fs, p.T3, R(x + 120, y, w - 120 - 44, 48), 1);
                    Ui.Key(g, (i + 1).ToString(), R(x + w - 10 - 24, y + 12, 24, 24), s,
                        i == selected ? Color.FromArgb(p.Dark ? 80 : 90, p.T1) : p.Stroke.A > 0 ? Color.FromArgb(p.Dark ? 40 : 46, p.T1) : p.Stroke,
                        i == selected ? p.T1 : p.T3);
                    Zone(row, id);
                    y += 50;
                }
            }
            y -= 2;
            // scheidingslijn
            using (SolidBrush b = new SolidBrush(p.Stroke)) g.FillRectangle(b, R(Pad + 4, y + 8, PanelW - 2 * Pad - 8, 1f / s * Math.Max(1f, s)));
            y += 17;

            // Wanneer: [−] [ Nu ] [+]
            using (Font f = Ui.F(13, s, false)) Ui.Text(g, Lang.T("when"), f, p.T2, R(Pad + 4, y, 120, 36), -1);
            float right = PanelW - Pad - 4;
            RectangleF plus = R(right - 36, y, 36, 36), field = R(right - 36 - 8 - 112, y, 112, 36), minus = R(right - 36 - 8 - 112 - 8 - 36, y, 36, 36);
            DrawSmallButton(g, p, minus, "minus", "minus");
            DrawSmallButton(g, p, plus, "plus", "plus");
            DrawField(g, p, field);
            y += 36 + 10;

            // vaste tijden
            float cw = (PanelW - 2 * Pad - 8 - 4 * 6) / 5f;
            using (Font f = Ui.F(12, s, false))
            using (Font fb = Ui.F(12, s, true))
            {
                for (int i = 0; i < Presets.Length; i++)
                {
                    RectangleF c = R(Pad + 4 + i * (cw + 6), y, cw, 30);
                    bool on = choice.Valid && !choice.IsClock && choice.Minutes == Presets[i];
                    string id = "chip" + i;
                    Ui.Fill(g, c, S(6), on ? p.Sel : Hot(id, p.Fill, p.Sel));
                    Ui.Stroke(g, c, S(6), on ? Color.FromArgb(p.Dark ? 60 : 50, p.T1) : p.Stroke, Math.Max(1f, s));
                    Ui.Text(g, TimeInput.Duration(Presets[i]), on ? fb : f, on ? p.T1 : p.T2, c, 0);
                    Zone(c, id);
                }
            }
            y += 30;
            if (message != null)
            {
                using (Font f = Ui.F(12, s, false)) Ui.Text(g, message, f, messageIsError ? p.Danger : p.T2, R(Pad + 4, y + 8, PanelW - 2 * Pad - 8, 18), 0);
                y += 26;
            }
            y += 14;
            DrawGo(g, p, R(Pad, y, PanelW - 2 * Pad, 40), Lang.Phrase(SelectedAction, CurrentChoiceForLabel()), "go", true);
        }

        TimeChoice CurrentChoiceForLabel()
        {
            if (fieldFocus)
            {
                TimeChoice t = TimeInput.Parse(fieldText);
                if (t.Valid) return t;
            }
            return choice;
        }

        void DrawSmallButton(Graphics g, Palette p, RectangleF r, string icon, string id)
        {
            Ui.Fill(g, r, S(8), Hot(id, p.Fill, p.Sel));
            Ui.Stroke(g, r, S(8), p.Stroke, Math.Max(1f, s));
            Ui.Icon(g, icon, new RectangleF(r.X + r.Width / 2 - S(7), r.Y + r.Height / 2 - S(7), S(14), S(14)), p.T1, 2f);
            Zone(r, id);
        }

        void DrawField(Graphics g, Palette p, RectangleF r)
        {
            Color bg = p.Dark ? Color.FromArgb(fieldFocus ? 80 : 56, 0, 0, 0) : Color.FromArgb(fieldFocus ? 255 : 200, 255, 255, 255);
            Ui.Fill(g, r, S(8), bg);
            Ui.Stroke(g, r, S(8), p.Stroke, Math.Max(1f, s));
            // onderlijn: accentkleur als je erin typt, anders zacht
            using (GraphicsPath clip = Ui.Round(r, S(8)))
            {
                GraphicsState st = g.Save();
                g.SetClip(clip, CombineMode.Intersect);
                float lh = fieldFocus ? S(2) : Math.Max(1f, s);
                using (SolidBrush b = new SolidBrush(fieldFocus ? p.Accent : p.T3)) g.FillRectangle(b, r.X, r.Bottom - lh, r.Width, lh);
                g.Restore(st);
            }
            string txt = fieldFocus ? fieldText : TimeInput.Label(choice);
            using (Font f = Ui.F(14, s, true))
            {
                float tw = Ui.Width(g, txt, f);
                if (fieldFocus && fieldSelectAll && txt.Length > 0)
                    Ui.Fill(g, new RectangleF(r.X + (r.Width - tw) / 2 - S(3), r.Y + S(8), tw + S(6), r.Height - S(16)), S(3), Color.FromArgb(110, p.Accent));
                Ui.Text(g, txt, f, p.T1, r, 0);
                if (fieldFocus && !fieldSelectAll)
                {
                    float cx = r.X + (r.Width + tw) / 2 + S(2);
                    using (Pen pen = new Pen(p.T1, Math.Max(1f, s))) g.DrawLine(pen, cx, r.Y + S(10), cx, r.Bottom - S(10));
                }
            }
            Zone(r, "field");
        }

        void DrawGo(Graphics g, Palette p, RectangleF r, string label, string id, bool showEnter)
        {
            Color c = p.Accent;
            if (press == id) c = Blend(c, p.Dark ? Color.Black : Color.Black, 0.12f);
            else if (hover == id) c = Blend(c, Color.White, p.Dark ? 0.10f : 0.08f);
            Ui.Fill(g, r, S(8), c);
            using (Font f = Ui.F(14, s, true))
            using (Font fk = Ui.F(11, s, true))
            {
                float tw = Ui.Width(g, label, f);
                float kw = showEnter ? Ui.Width(g, "Enter", fk) + S(12) : 0;
                float gap = showEnter ? S(10) : 0;
                float total = tw + gap + kw;
                float x0 = r.X + (r.Width - total) / 2;
                Ui.Text(g, label, f, p.OnAccent, new RectangleF(x0 - S(2), r.Y, tw + S(4), r.Height), 0);
                if (showEnter)
                {
                    RectangleF k = new RectangleF(x0 + tw + gap, r.Y + r.Height / 2 - S(10), kw, S(20));
                    Ui.Fill(g, k, S(4), Color.FromArgb(30, p.OnAccent));
                    Ui.Text(g, "Enter", fk, p.OnAccent, k, 0);
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

        void DrawRunning(Graphics g, Palette p)
        {
            DrawHeader(g, p);
            Scheduler sc = app.Scheduler;
            float y = Pad + 48;
            PointF c = new PointF(S(PanelW / 2f), S(y + 78));
            Ring(g, p, c, S(70), S(6), sc.Fraction);
            using (Font f = Ui.F(12, s, false)) Ui.Text(g, Lang.ActionName(sc.Action), f, p.T2, new RectangleF(c.X - S(70), c.Y - S(40), S(140), S(18)), 0);
            using (Font f = Ui.Big(34, s)) Ui.Text(g, TimeInput.Clock(sc.Remaining), f, p.T1, new RectangleF(c.X - S(70), c.Y - S(22), S(140), S(44)), 0);
            using (Font f = Ui.F(12, s, false)) Ui.Text(g, Lang.F("running.at", sc.Target.ToString("HH:mm")), f, p.T3, new RectangleF(c.X - S(70), c.Y + S(22), S(140), S(18)), 0);
            y += 168;
            if (sc.Warned || message != null)
            {
                string t = message ?? Lang.T("warn.save");
                using (Font f = Ui.F(13, s, true)) Ui.Text(g, t, f, message != null && messageIsError ? p.Danger : p.T1, R(Pad, y, PanelW - 2 * Pad, 20), 0);
                y += 24;
            }
            y += 12;
            float bw = (PanelW - 2 * Pad - 2 * 8) / 3f;
            DrawOutlineButton(g, p, R(Pad, y, bw, 40), Lang.F("btn.add", Settings.Current.StepMinutes), "add");
            DrawOutlineButton(g, p, R(Pad + bw + 8, y, bw, 40), Lang.T("btn.cancel"), "cancel");
            DrawGo(g, p, R(Pad + 2 * (bw + 8), y, bw, 40), Lang.T("btn.donow"), "donow", false);
        }

        void DrawUndo(Graphics g, Palette p)
        {
            DrawHeader(g, p);
            float y = Pad + 48;
            double left = Math.Max(0, (undoEnd - DateTime.Now).TotalSeconds);
            float frac = (float)(left / Math.Max(1, Settings.Current.UndoSeconds));
            PointF c = new PointF(S(PanelW / 2f), S(y + 70));
            Ring(g, p, c, S(62), S(6), frac);
            using (Font f = Ui.Big(44, s)) Ui.Text(g, Math.Ceiling(left).ToString("0"), f, p.T1, new RectangleF(c.X - S(60), c.Y - S(30), S(120), S(60)), 0);
            y += 152;
            using (Font f = Ui.F(14, s, true)) Ui.Text(g, Lang.Phrase(undoAction, TimeChoice.Now), f, p.T1, R(Pad, y, PanelW - 2 * Pad, 22), 0);
            y += 22;
            using (Font f = Ui.F(12, s, false)) Ui.Text(g, Lang.T("undo.hint"), f, p.T3, R(Pad, y, PanelW - 2 * Pad, 20), 0);
            y += 20 + 14;
            DrawOutlineButton(g, p, R(Pad, y, PanelW - 2 * Pad, 40), Lang.T("btn.cancel"), "undo.cancel");
        }

        // Tekent opnieuw en zet het resultaat op het scherm
        public void Render()
        {
            if (!IsHandleCreated) CreateHandle();
            Place();
            int m = (int)Math.Round(Margin_ * s);
            using (Bitmap bmp = Draw(s, panelOnScreen))
            {
                Point pos = new Point(panelOnScreen.X - m, panelOnScreen.Y - m);
                PushLayered(bmp, pos);
            }
        }

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
            string h = HitTest(e.Location);
            Cursor = h != null ? (h == "field" ? Cursors.IBeam : Cursors.Hand) : Cursors.Default;
            if (h != hover) { hover = h; Render(); }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (hover != null || press != null) { hover = null; press = null; Render(); }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            press = HitTest(e.Location);
            if (press != null) Render();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left) return;
            string h = HitTest(e.Location);
            string was = press;
            press = null;
            if (h != null && h == was) ZoneClick(h);
            else Render();
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            // dubbelklik op een optie: meteen uitvoeren
            string h = HitTest(e.Location);
            if (Mode == View.Choose && h != null && h.StartsWith("row")) Go();
        }

        void ZoneClick(string id)
        {
            message = null;
            if (id == "gear") { app.OpenSettings(); return; }
            switch (Mode)
            {
                case View.Choose:
                    if (id.StartsWith("row"))
                    {
                        int i = int.Parse(id.Substring(3));
                        if (Power.Available(Actions()[i])) { selected = i; LeaveField(true); }
                    }
                    else if (id.StartsWith("chip")) { LeaveField(false); SetChoice(TimeChoice.After(Presets[int.Parse(id.Substring(4))])); }
                    else if (id == "minus") StepTime(-1);
                    else if (id == "plus") StepTime(1);
                    else if (id == "field") { fieldFocus = true; fieldSelectAll = true; fieldText = TimeInput.Label(choice); }
                    else if (id == "go") { Go(); return; }
                    break;
                case View.Running:
                    if (id == "add") app.Scheduler.Add(Settings.Current.StepMinutes);
                    else if (id == "cancel") { app.Scheduler.Cancel(); Mode = View.Choose; ResetChoice(); }
                    else if (id == "donow") { app.Execute(app.Scheduler.Action, true); return; }
                    break;
                case View.Undo:
                    if (id == "undo.cancel") { CancelUndo(); return; }
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
            }
            message = null;
            if (fieldFocus)
            {
                if (k == Keys.Enter) { if (LeaveField(false)) Go(); else Render(); return true; }
                if (k == Keys.Escape) { fieldFocus = false; fieldSelectAll = false; Render(); return true; }
                if (k == Keys.Tab || k == Keys.Up || k == Keys.Down)
                {
                    LeaveField(true);
                    if (k == Keys.Up) MoveSelection(-1); else if (k == Keys.Down) MoveSelection(1);
                    Render(); return true;
                }
                if (plus || minus) { LeaveField(true); StepTime(plus ? 1 : -1); Render(); return true; }
                if (k == Keys.Back)
                {
                    if (fieldSelectAll) fieldText = ""; else if (fieldText.Length > 0) fieldText = fieldText.Substring(0, fieldText.Length - 1);
                    fieldSelectAll = false; Render(); return true;
                }
                return false;   // tekens gaan naar OnKeyPress
            }
            if (k == Keys.Escape) { HideFlyout(); return true; }
            if (k == Keys.Up) { MoveSelection(-1); Render(); return true; }
            if (k == Keys.Down) { MoveSelection(1); Render(); return true; }
            if (k == Keys.Left || k == Keys.Right) { CyclePreset(k == Keys.Right ? 1 : -1); Render(); return true; }
            if (plus || minus) { StepTime(plus ? 1 : -1); Render(); return true; }
            if (k == Keys.Tab) { fieldFocus = true; fieldSelectAll = true; fieldText = TimeInput.Label(choice); Render(); return true; }
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

        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            base.OnKeyPress(e);
            if (Mode != View.Choose || !fieldFocus) return;
            char c = e.KeyChar;
            if (char.IsControl(c)) return;
            if (!(char.IsLetterOrDigit(c) || c == ':' || c == '.' || c == ',' || c == ' ')) return;
            if (fieldSelectAll) { fieldText = ""; fieldSelectAll = false; }
            if (fieldText.Length < 10) fieldText += c;
            e.Handled = true;
            Render();
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

        void SetChoice(TimeChoice t) { choice = t; fieldText = TimeInput.Label(t); }

        void StepTime(int dir) { LeaveField(false); SetChoice(TimeInput.Step(choice, Settings.Current.StepMinutes, dir, DateTime.Now)); }

        void CyclePreset(int dir)
        {
            // Nu, 15, 30, 45, 60, 120 met links en rechts
            int[] seq = { 0, 15, 30, 45, 60, 120 };
            int cur = choice.Valid && !choice.IsClock ? Array.IndexOf(seq, choice.Minutes) : -1;
            int next;
            if (cur < 0) next = dir > 0 ? 1 : 0;
            else next = Math.Max(0, Math.Min(seq.Length - 1, cur + dir));
            SetChoice(TimeChoice.After(seq[next]));
        }

        // Uit het tijdvak gaan. Geldige tekst wordt de nieuwe tijd; ongeldig: melding (of terug naar de oude tijd).
        bool LeaveField(bool revertIfBad)
        {
            if (!fieldFocus) return true;
            TimeChoice t = TimeInput.Parse(fieldText);
            fieldFocus = false; fieldSelectAll = false;
            if (t.Valid) { SetChoice(t); return true; }
            if (revertIfBad) { fieldText = TimeInput.Label(choice); return false; }
            fieldFocus = true;
            message = Lang.T("hint.time"); messageIsError = true;
            return false;
        }

        // Enter of de grote knop
        void Go()
        {
            if (!LeaveField(false)) { Render(); return; }
            PowerAction a = SelectedAction;
            if (!Power.Available(a)) { message = Lang.T(a == PowerAction.Hibernate ? "err.hibernate" : "err.sleep"); messageIsError = true; Render(); return; }
            if (choice.IsNow)
            {
                if (Settings.Current.UndoSeconds > 0)
                {
                    undoAction = a;
                    undoEnd = DateTime.Now.AddSeconds(Settings.Current.UndoSeconds);
                    Mode = View.Undo;
                    anim.Start();
                    Render();
                }
                else app.Execute(a, false);
                return;
            }
            app.Scheduler.Start(a, choice.TargetFrom(DateTime.Now));
            Mode = View.Running;
            Render();
        }

        void UndoTick()
        {
            if (DateTime.Now >= undoEnd)
            {
                anim.Stop();
                app.Execute(undoAction, false);
                return;
            }
            if (Visible) Render();
        }

        void CancelUndo()
        {
            anim.Stop();
            Mode = View.Choose;
            Render();
        }

        // ---------------------------------------------------------------- voor de zelftest
        public void TestSet(View v, int sel, TimeChoice t, bool focus, string text, string msg, bool err)
        {
            Mode = v; selected = sel; choice = t; fieldFocus = focus; fieldSelectAll = false; fieldText = text ?? TimeInput.Label(t);
            message = msg; messageIsError = err; hover = null;
            undoAction = PowerAction.Shutdown; undoEnd = DateTime.Now.AddSeconds(3.4);
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
