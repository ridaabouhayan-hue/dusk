using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace Dusk
{
    // Tekenhulpjes: lettertypes, afgeronde vlakken, icoontjes en het logo.
    static class Ui
    {
        static string family, display;

        public static string Family
        {
            get { if (family == null) PickFonts(); return family; }
        }

        public static string Display
        {
            get { if (display == null) PickFonts(); return display; }
        }

        static void PickFonts()
        {
            HashSet<string> have = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try { using (InstalledFontCollection c = new InstalledFontCollection()) foreach (FontFamily f in c.Families) have.Add(f.Name); }
            catch { }
            family = have.Contains("Segoe UI Variable Text") ? "Segoe UI Variable Text" : have.Contains("Segoe UI") ? "Segoe UI" : FontFamily.GenericSansSerif.Name;
            display = have.Contains("Segoe UI Variable Display") ? "Segoe UI Variable Display" : family;
        }

        public static Font F(float px, float s, bool bold)
        {
            return new Font(Family, px * s, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);
        }

        public static Font Big(float px, float s)
        {
            return new Font(Display, px * s, FontStyle.Bold, GraphicsUnit.Pixel);
        }

        public static void Setup(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        }

        public static GraphicsPath Round(RectangleF r, float rad) { return Svg.RoundRect(r, rad); }

        public static void Fill(Graphics g, RectangleF r, float rad, Color c)
        {
            if (c.A == 0) return;
            using (GraphicsPath p = Round(r, rad)) using (SolidBrush b = new SolidBrush(c)) g.FillPath(b, p);
        }

        public static void Stroke(Graphics g, RectangleF r, float rad, Color c, float w)
        {
            if (c.A == 0) return;
            RectangleF rr = new RectangleF(r.X + w / 2, r.Y + w / 2, r.Width - w, r.Height - w);
            using (GraphicsPath p = Round(rr, Math.Max(0, rad - w / 2))) using (Pen pen = new Pen(c, w)) g.DrawPath(pen, p);
        }

        static readonly StringFormat LeftMid = Fmt(StringAlignment.Near), CenterMid = Fmt(StringAlignment.Center), RightMid = Fmt(StringAlignment.Far);

        static StringFormat Fmt(StringAlignment h)
        {
            StringFormat f = new StringFormat(StringFormatFlags.NoWrap);
            f.Alignment = h; f.LineAlignment = StringAlignment.Center; f.Trimming = StringTrimming.EllipsisCharacter;
            return f;
        }

        public static void Text(Graphics g, string s, Font f, Color c, RectangleF r, int align)
        {
            using (SolidBrush b = new SolidBrush(c))
                g.DrawString(s, f, b, r, align < 0 ? LeftMid : align == 0 ? CenterMid : RightMid);
        }

        // Tekst die over meerdere regels mag lopen (uitleg in Instellingen)
        public static void Wrap(Graphics g, string s, Font f, Color c, RectangleF r)
        {
            using (SolidBrush b = new SolidBrush(c))
            using (StringFormat sf = new StringFormat())
            {
                sf.Trimming = StringTrimming.EllipsisWord;
                g.DrawString(s, f, b, r, sf);
            }
        }

        // Tekst precies vanaf x, zonder de extra ruimte die DrawString normaal links en rechts neemt.
        // Hoort bij Width(): samen meten en tekenen ze hetzelfde (anders valt tekst op Windows weg achter "...").
        static readonly StringFormat Tight = MakeTight();

        static StringFormat MakeTight()
        {
            StringFormat f = (StringFormat)StringFormat.GenericTypographic.Clone();
            f.FormatFlags |= StringFormatFlags.NoWrap | StringFormatFlags.MeasureTrailingSpaces;
            f.LineAlignment = StringAlignment.Center;
            f.Trimming = StringTrimming.None;
            return f;
        }

        public static void TextAt(Graphics g, string s, Font f, Color c, float x, RectangleF row)
        {
            using (SolidBrush b = new SolidBrush(c)) g.DrawString(s, f, b, new RectangleF(x, row.Y, 10000, row.Height), Tight);
        }

        public static float Width(Graphics g, string s, Font f)
        {
            return g.MeasureString(s, f, 10000, Tight).Width;
        }

        // Toetsje met een cijfer of letter erin
        public static void Key(Graphics g, string label, RectangleF r, float s, Color border, Color text)
        {
            Stroke(g, r, 6 * s, border, Math.Max(1f, s));
            using (Font f = F(12, s, false)) Text(g, label, f, text, r, 0);
        }

        // Zachte schaduw rond het venster (een paar lagen, steeds lichter)
        public static void Shadow(Graphics g, RectangleF panel, float rad, float s, bool dark)
        {
            int layers = 10;
            for (int i = layers; i >= 1; i--)
            {
                float grow = i * 1.6f * s;
                int a = (int)((dark ? 9 : 5) * (1 - (i - 1) / (float)layers));
                RectangleF r = new RectangleF(panel.X - grow, panel.Y - grow + 6 * s, panel.Width + 2 * grow, panel.Height + 2 * grow);
                Fill(g, r, rad + grow, Color.FromArgb(Math.Max(1, a), 0, 0, 0));
            }
        }

        // ---------------------------------------------------------------- icoontjes (24 bij 24, lijnen)
        static readonly Dictionary<string, string> Paths = new Dictionary<string, string>
        {
            { "sleep", "M20 14.5A8 8 0 1 1 9.5 4a6.5 6.5 0 0 0 10.5 10.5z" },
            { "hibernate", "M4.5 14h15a1.5 1.5 0 0 1 1.5 1.5v3a1.5 1.5 0 0 1-1.5 1.5h-15A1.5 1.5 0 0 1 3 18.5v-3A1.5 1.5 0 0 1 4.5 14z M12 3v8 M8.5 7.5L12 11l3.5-3.5" },
            { "restart", "M20 12a8 8 0 1 1-2.3-5.6 M20 4v4h-4" },
            { "shutdown", "M12 3v8 M6.3 6.8a8 8 0 1 0 11.4 0" },
            { "lock", "M5 11h14a2 2 0 0 1 2 2v7a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-7a2 2 0 0 1 2-2z M7 11V7a5 5 0 0 1 10 0v4" },
            { "logoff", "M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4 M16 17l5-5-5-5 M21 12H9" },
            // tandwiel (Lucide "settings", ISC-licentie)
            { "gear", "M12.22 2h-.44a2 2 0 0 0-2 2v.18a2 2 0 0 1-1 1.73l-.43.25a2 2 0 0 1-2 0l-.15-.08a2 2 0 0 0-2.73.73l-.22.38a2 2 0 0 0 .73 2.73l.15.1a2 2 0 0 1 1 1.72v.51a2 2 0 0 1-1 1.74l-.15.09a2 2 0 0 0-.73 2.73l.22.38a2 2 0 0 0 2.73.73l.15-.08a2 2 0 0 1 2 0l.43.25a2 2 0 0 1 1 1.73V20a2 2 0 0 0 2 2h.44a2 2 0 0 0 2-2v-.18a2 2 0 0 1 1-1.73l.43-.25a2 2 0 0 1 2 0l.15.08a2 2 0 0 0 2.73-.73l.22-.39a2 2 0 0 0-.73-2.73l-.15-.08a2 2 0 0 1-1-1.74v-.5a2 2 0 0 1 1-1.74l.15-.09a2 2 0 0 0 .73-2.73l-.22-.38a2 2 0 0 0-2.73-.73l-.15.08a2 2 0 0 1-2 0l-.43-.25a2 2 0 0 1-1-1.73V4a2 2 0 0 0-2-2z M15 12a3 3 0 1 1-6 0a3 3 0 1 1 6 0z" },
            // sluimerstand: een sneeuwvlok (winterslaap: alles bewaard, helemaal geen stroom)
            { "snow", "M12 2v20 M3.3 7l17.4 10 M3.3 17L20.7 7 M9 3.5l3 2.5 3-2.5 M9 20.5l3-2.5 3 2.5 M3.6 10.4l3.6-1.4-.6-3.8 M20.4 13.6l-3.6 1.4.6 3.8 M3.6 13.6l3.6 1.4-.6 3.8 M20.4 10.4l-3.6-1.4.6-3.8" },
            { "minus", "M5 12h14" },
            { "plus", "M5 12h14 M12 5v14" },
            { "close", "M6 6l12 12 M18 6L6 18" },
            { "chevron", "M6 9l6 6 6-6" },
            { "check", "M5 12.5l4.5 4.5L19 7.5" },
            { "download", "M12 3v12 M7 10l5 5 5-5 M5 20h14" }
        };

        static readonly Dictionary<string, GraphicsPath> Cache = new Dictionary<string, GraphicsPath>();

        // Slaapstand: een volle maan(sikkel) met zzz. Sluimerstand: sneeuwvlok. Duidelijk verschillend van elkaar.
        public static void ActionIcon(Graphics g, PowerAction a, RectangleF box, Color c)
        {
            switch (a)
            {
                case PowerAction.Sleep:
                    {
                        GraphicsState st = g.Save();
                        float k = Math.Min(box.Width, box.Height) / 24f;
                        g.TranslateTransform(box.X + (box.Width - 24 * k) / 2, box.Y + (box.Height - 24 * k) / 2);
                        g.ScaleTransform(k, k);
                        // volle maansikkel: een cirkel waar een tweede cirkel rechtsboven uit is gehaald
                        using (GraphicsPath outer = new GraphicsPath())
                        using (GraphicsPath inner = new GraphicsPath())
                        {
                            outer.AddEllipse(2.5f, 5f, 16.5f, 16.5f);
                            inner.AddEllipse(9.2f, 1.6f, 13f, 13f);
                            using (Region r = new Region(outer))
                            using (SolidBrush b = new SolidBrush(c))
                            {
                                r.Exclude(inner);
                                g.FillRegion(b, r);
                            }
                        }
                        using (Pen p = new Pen(c, 1.7f))
                        {
                            p.StartCap = LineCap.Round; p.EndCap = LineCap.Round; p.LineJoin = LineJoin.Round;
                            g.DrawLines(p, new[] { new PointF(16.5f, 2.2f), new PointF(21f, 2.2f), new PointF(16.5f, 6.8f), new PointF(21f, 6.8f) });
                        }
                        g.Restore(st);
                        return;
                    }
                case PowerAction.Hibernate: Icon(g, "snow", box, c, 1.8f); return;
                default: Icon(g, Lang.Key(a), box, c, 1.9f); return;
            }
        }

        public static void Icon(Graphics g, string name, RectangleF box, Color c, float strokeAt24)
        {
            GraphicsPath p;
            if (!Cache.TryGetValue(name, out p)) { p = Svg.Path(Paths[name]); Cache[name] = p; }
            float k = Math.Min(box.Width, box.Height) / 24f;
            GraphicsState st = g.Save();
            g.TranslateTransform(box.X + (box.Width - 24 * k) / 2, box.Y + (box.Height - 24 * k) / 2);
            g.ScaleTransform(k, k);
            using (Pen pen = new Pen(c, strokeAt24))
            {
                pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round; pen.LineJoin = LineJoin.Round;
                g.DrawPath(pen, p);
            }
            g.Restore(st);
        }

        // ---------------------------------------------------------------- het logo: aan-uitknop met schemerring
        public static readonly Color DuskA = Color.FromArgb(251, 146, 60), DuskB = Color.FromArgb(232, 121, 249), DuskC = Color.FromArgb(129, 140, 248);

        static LinearGradientBrush DuskBrush(RectangleF box)
        {
            // van linksonder (oranje) naar rechtsboven (paars), net als in het ontwerp
            float k = box.Width / 48f;
            PointF a = new PointF(box.X + 8 * k, box.Y + 40 * k), b = new PointF(box.X + 40 * k, box.Y + 12 * k);
            LinearGradientBrush br = new LinearGradientBrush(a, b, DuskA, DuskC);
            ColorBlend cb = new ColorBlend(3);
            cb.Colors = new[] { DuskA, DuskB, DuskC };
            cb.Positions = new[] { 0f, 0.55f, 1f };
            br.InterpolationColors = cb;
            br.WrapMode = WrapMode.TileFlipXY;
            return br;
        }

        // progress: 1 = hele ring (normaal); kleiner = timer loopt, de ring dooft uit
        public static void Logo(Graphics g, RectangleF box, Color bar, float strokeAt48, float progress, Color track)
        {
            float k = box.Width / 48f;
            float w = strokeAt48 * k;
            // ring: middelpunt (24, 25.78), straal 15.5, opening bovenaan (zoals het pad M16 12.5A15.5 15.5 0 1 0 32 12.5)
            PointF c = new PointF(box.X + 24 * k, box.Y + 25.78f * k);
            float r = 15.5f * k;
            RectangleF rr = new RectangleF(c.X - r, c.Y - r, 2 * r, 2 * r);
            const float start = -58.9f, sweep = 297.8f;   // van rechtsboven met de klok mee rond naar linksboven
            using (LinearGradientBrush br = DuskBrush(box))
            using (Pen pen = new Pen(br, w))
            {
                pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round;
                if (progress >= 0.999f) g.DrawArc(pen, rr, start, sweep);
                else
                {
                    using (Pen tp = new Pen(track, w)) { tp.StartCap = LineCap.Round; tp.EndCap = LineCap.Round; g.DrawArc(tp, rr, start, sweep); }
                    float sw = Math.Max(0.5f, sweep * Math.Max(0f, progress));
                    g.DrawArc(pen, rr, start + sweep - sw, sw);
                }
            }
            using (Pen p = new Pen(bar, w))
            {
                p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
                g.DrawLine(p, box.X + 24 * k, box.Y + 7 * k, box.X + 24 * k, box.Y + 21 * k);
            }
        }
    }
}
