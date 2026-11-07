using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace Dusk
{
    // Tekenhulpjes: lettertypes, afgeronde vlakken, icoontjes en het logo.
    static class Ui
    {
        static string family, display, familyBold, displayBold;

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
            // Vet zoals Windows 11 het doet: halfvet (Semibold) als dat er is
            familyBold = have.Contains(family + " Semibold") ? family + " Semibold" : null;
            displayBold = have.Contains(display + " Semibold") ? display + " Semibold" : null;
        }

        public static Font F(float px, float s, bool bold)
        {
            string fam = Family;
            if (bold && familyBold != null) return new Font(familyBold, (px + Bump(px)) * s, FontStyle.Regular, GraphicsUnit.Pixel);
            return new Font(fam, (px + Bump(px)) * s, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);
        }

        // Kleine tekst een tikje groter, zodat alles goed leesbaar is (12 wordt 13, 14 wordt 15)
        static float Bump(float px) { return px <= 16 ? 1f : 0f; }

        public static Font Big(float px, float s)
        {
            string fam = Display;
            if (displayBold != null) return new Font(displayBold, px * s, FontStyle.Regular, GraphicsUnit.Pixel);
            return new Font(fam, px * s, FontStyle.Bold, GraphicsUnit.Pixel);
        }

        public static void Setup(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            // ClearType: zo scherp als de tekst van Windows zelf. Kan alleen op een dichte achtergrond,
            // en dat is overal waar we tekst zetten (het vlak van het venster is altijd dicht).
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
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

        // Tekst tekenen we met de tekstmotor van Windows zelf (GDI, zoals Verkenner en Instellingen).
        // Die zet de letters precies op de pixels en is daardoor veel scherper dan DrawString.
        // GDI kent geen doorzichtige tekst: een halfdoorzichtige kleur mengen we vooraf met de achtergrond.
        public static Color Back = Color.FromArgb(32, 32, 34);

        public static Color Solid(Color c)
        {
            if (c.A == 255) return c;
            float a = c.A / 255f;
            return Color.FromArgb(255,
                (int)Math.Round(c.R * a + Back.R * (1 - a)),
                (int)Math.Round(c.G * a + Back.G * (1 - a)),
                (int)Math.Round(c.B * a + Back.B * (1 - a)));
        }

        const TextFormatFlags Base = TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine |
            TextFormatFlags.VerticalCenter | TextFormatFlags.PreserveGraphicsClipping | TextFormatFlags.PreserveGraphicsTranslateTransform;

        static Rectangle Px(RectangleF r)
        {
            return Rectangle.FromLTRB((int)Math.Round(r.Left), (int)Math.Round(r.Top), (int)Math.Round(r.Right), (int)Math.Round(r.Bottom));
        }

        public static void Text(Graphics g, string s, Font f, Color c, RectangleF r, int align)
        {
            TextFormatFlags fl = Base | TextFormatFlags.EndEllipsis |
                (align < 0 ? TextFormatFlags.Left : align == 0 ? TextFormatFlags.HorizontalCenter : TextFormatFlags.Right);
            TextRenderer.DrawText(g, s, f, Px(r), Solid(c), fl);
        }

        // Tekst die over meerdere regels mag lopen (uitleg in Instellingen)
        public static void Wrap(Graphics g, string s, Font f, Color c, RectangleF r)
        {
            TextFormatFlags fl = TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl |
                TextFormatFlags.EndEllipsis | TextFormatFlags.PreserveGraphicsClipping | TextFormatFlags.PreserveGraphicsTranslateTransform;
            TextRenderer.DrawText(g, s, f, Px(r), Solid(c), fl);
        }

        // Tekst precies vanaf x. Hoort bij Width(): samen meten en tekenen ze hetzelfde,
        // zodat een knop altijd breed genoeg is voor zijn tekst.
        public static void TextAt(Graphics g, string s, Font f, Color c, float x, RectangleF row)
        {
            TextRenderer.DrawText(g, s, f, Px(new RectangleF(x, row.Y, 10000, row.Height)), Solid(c), Base | TextFormatFlags.Left);
        }

        public static float Width(Graphics g, string s, Font f)
        {
            return TextRenderer.MeasureText(g, s, f, new Size(100000, 100000), TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width;
        }

        // Zet een dichte laag met afgeronde hoeken op het venster. De hoeken worden per pixel
        // zacht gemaakt (randje half doorzichtig), de rest wordt pixel voor pixel gekopieerd, dus niets vervaagt.
        public static void PutRounded(Bitmap dst, Bitmap src, int ox, int oy, float rad)
        {
            int w = src.Width, h = src.Height;
            // het hele doelplaatje vastzetten (een stukje vastzetten gaat niet overal goed) en zelf verschuiven
            System.Drawing.Imaging.BitmapData dd = dst.LockBits(new Rectangle(0, 0, dst.Width, dst.Height), System.Drawing.Imaging.ImageLockMode.ReadWrite, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            System.Drawing.Imaging.BitmapData sd = src.LockBits(new Rectangle(0, 0, w, h), System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
            try
            {
                int[] drow = new int[w], srow = new int[w];
                for (int y = 0; y < h; y++)
                {
                    System.Runtime.InteropServices.Marshal.Copy(new IntPtr(dd.Scan0.ToInt64() + (long)(y + oy) * dd.Stride + ox * 4), drow, 0, w);
                    System.Runtime.InteropServices.Marshal.Copy(new IntPtr(sd.Scan0.ToInt64() + (long)y * sd.Stride), srow, 0, w);
                    for (int x = 0; x < w; x++)
                    {
                        float cov = Coverage(x + 0.5f, y + 0.5f, w, h, rad);
                        int sc = srow[x] | unchecked((int)0xFF000000);
                        if (cov >= 1f) { drow[x] = sc; continue; }
                        if (cov <= 0f) continue;
                        int dc = drow[x];
                        float da = ((dc >> 24) & 255) / 255f;
                        float oa = cov + da * (1 - cov);
                        int r = Mix((sc >> 16) & 255, (dc >> 16) & 255, cov, da, oa);
                        int gg = Mix((sc >> 8) & 255, (dc >> 8) & 255, cov, da, oa);
                        int b = Mix(sc & 255, dc & 255, cov, da, oa);
                        int a = (int)Math.Round(oa * 255);
                        drow[x] = (a << 24) | (r << 16) | (gg << 8) | b;
                    }
                    System.Runtime.InteropServices.Marshal.Copy(drow, 0, new IntPtr(dd.Scan0.ToInt64() + (long)(y + oy) * dd.Stride + ox * 4), w);
                }
            }
            finally { dst.UnlockBits(dd); src.UnlockBits(sd); }
        }

        static int Mix(int s, int d, float cov, float da, float oa)
        {
            if (oa <= 0) return 0;
            return Math.Max(0, Math.Min(255, (int)Math.Round((s * cov + d * da * (1 - cov)) / oa)));
        }

        // Hoeveel van een pixel binnen een afgeronde rechthoek valt (0 tot 1)
        public static float Coverage(float px, float py, int w, int h, float rad)
        {
            float cx = px < rad ? rad : px > w - rad ? w - rad : px;
            float cy = py < rad ? rad : py > h - rad ? h - rad : py;
            if (cx == px || cy == py) return 1f;
            float dx = px - cx, dy = py - cy;
            float d = (float)Math.Sqrt(dx * dx + dy * dy);
            return Math.Max(0f, Math.Min(1f, rad - d + 0.5f));
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
                        // volle maansikkel als pad (gladde randen; een Region zou kartelig worden)
                        using (GraphicsPath moon = Svg.Path("M9.9 5.04A8.25 8.25 0 1 0 18.98 13.81A6.8 6.8 0 0 1 9.9 5.04z"))
                        using (SolidBrush b = new SolidBrush(c)) g.FillPath(b, moon);
                        using (Pen p = new Pen(c, 1.7f))
                        {
                            p.StartCap = LineCap.Round; p.EndCap = LineCap.Round; p.LineJoin = LineJoin.Round;
                            g.DrawLines(p, new[] { new PointF(16.8f, 1.6f), new PointF(21.2f, 1.6f), new PointF(16.8f, 5.8f), new PointF(21.2f, 5.8f) });
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
