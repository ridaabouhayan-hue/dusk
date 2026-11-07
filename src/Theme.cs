using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using Microsoft.Win32;

namespace Dusk
{
    // Kleuren voor licht en donker, de accentkleur en de achtergrond (Mica, effen, schemer).
    public class Palette
    {
        public static readonly string[] AccentKeys = { "lila", "perzik", "roze", "blauw", "windows" };

        public bool Dark;
        public Color Accent, OnAccent;          // knopkleur en de tekst op de knop
        public Color T1, T2, T3;                // tekst: hoofd, tweede, derde
        public Color Fill, Sel, Stroke, Border; // vlakjes, gekozen regel, randjes, rand van het venster
        public Color Solid;                     // effen achtergrond
        public Color Danger;

        // Voor zelftest en schermafbeeldingen kun je licht of donker vastzetten
        public static bool? ForceDark;

        public static bool SystemDark()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    object v = k != null ? k.GetValue("AppsUseLightTheme") : null;
                    if (v is int) return (int)v == 0;
                }
            }
            catch { }
            return true;
        }

        public static Palette Current()
        {
            Settings s = Settings.Current;
            bool dark = ForceDark.HasValue ? ForceDark.Value : s.Theme == "dark" || (s.Theme == "system" && SystemDark());
            return For(dark, s.Accent);
        }

        public static Palette For(bool dark, string accent)
        {
            Palette p = new Palette();
            p.Dark = dark;
            if (dark)
            {
                p.T1 = Color.White; p.T2 = Color.FromArgb(201, 255, 255, 255); p.T3 = Color.FromArgb(158, 255, 255, 255);
                p.Fill = Color.FromArgb(16, 255, 255, 255); p.Sel = Color.FromArgb(24, 255, 255, 255);
                p.Stroke = Color.FromArgb(22, 255, 255, 255); p.Border = Color.FromArgb(34, 255, 255, 255);
                p.Solid = Color.FromArgb(32, 32, 34); p.Danger = Color.FromArgb(255, 153, 164);
            }
            else
            {
                p.T1 = Color.FromArgb(26, 26, 26); p.T2 = Color.FromArgb(184, 0, 0, 0); p.T3 = Color.FromArgb(140, 0, 0, 0);
                p.Fill = Color.FromArgb(10, 0, 0, 0); p.Sel = Color.FromArgb(16, 0, 0, 0);
                p.Stroke = Color.FromArgb(22, 0, 0, 0); p.Border = Color.FromArgb(30, 0, 0, 0);
                p.Solid = Color.FromArgb(243, 243, 243); p.Danger = Color.FromArgb(196, 43, 28);
            }
            p.Accent = AccentColor(accent, dark);
            p.OnAccent = Luma(p.Accent) > 0.55 ? Color.Black : Color.White;
            return p;
        }

        // Pastel op donker, een diepere tint op licht (anders is het streepje slecht te zien)
        public static Color AccentColor(string key, bool dark)
        {
            switch (key)
            {
                case "perzik": return dark ? Color.FromArgb(255, 179, 138) : Color.FromArgb(176, 74, 16);
                case "roze": return dark ? Color.FromArgb(240, 171, 252) : Color.FromArgb(160, 32, 140);
                case "blauw": return dark ? Color.FromArgb(96, 205, 255) : Color.FromArgb(0, 95, 184);
                case "windows": return WindowsAccent(dark);
                default: return dark ? Color.FromArgb(185, 168, 255) : Color.FromArgb(98, 70, 214);
            }
        }

        // De accentkleur die je in Windows hebt gekozen (zelfde tinten als Windows zelf gebruikt)
        public static Color WindowsAccent(bool dark)
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Accent"))
                {
                    byte[] pal = k != null ? k.GetValue("AccentPalette") as byte[] : null;
                    if (pal != null && pal.Length >= 32)
                    {
                        int i = dark ? 1 : 4;   // licht 2 op donker, donker 1 op licht
                        return Color.FromArgb(pal[i * 4], pal[i * 4 + 1], pal[i * 4 + 2]);
                    }
                }
            }
            catch { }
            return dark ? Color.FromArgb(96, 205, 255) : Color.FromArgb(0, 95, 184);
        }

        public static double Luma(Color c)
        {
            return (0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B) / 255.0;
        }

        // ---------------------------------------------------------------- achtergrond van het venster
        // panel: het vlak in het venster; onScreen: waar dat vlak op het scherm staat (voor Mica)
        public void PaintBackdrop(Graphics g, Rectangle panel, Rectangle onScreen, string mode)
        {
            if (mode == "dusk")
            {
                Color a = Dark ? Color.FromArgb(29, 26, 46) : Color.FromArgb(244, 241, 251);
                Color b = Dark ? Color.FromArgb(23, 21, 31) : Color.FromArgb(248, 246, 250);
                Color c = Dark ? Color.FromArgb(34, 23, 32) : Color.FromArgb(251, 240, 236);
                using (LinearGradientBrush br = new LinearGradientBrush(new Rectangle(panel.X, panel.Y - 1, panel.Width, panel.Height + 2), a, c, 90f))
                {
                    ColorBlend cb = new ColorBlend(3);
                    cb.Colors = new[] { a, b, c };
                    cb.Positions = new[] { 0f, 0.6f, 1f };
                    br.InterpolationColors = cb;
                    g.FillRectangle(br, panel);
                }
                return;
            }
            if (mode == "mica")
            {
                Bitmap blur = Wallpaper.BlurredCrop(onScreen);
                if (blur != null)
                {
                    InterpolationMode old = g.InterpolationMode;
                    g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                    using (ImageAttributes ia = new ImageAttributes())
                    {
                        ia.SetWrapMode(WrapMode.TileFlipXY);   // geen donkere randjes bij het opschalen
                        g.DrawImage(blur, panel, 0, 0, blur.Width, blur.Height, GraphicsUnit.Pixel, ia);
                    }
                    g.InterpolationMode = old;
                    // De tint van Mica: het bureaublad schemert er zacht doorheen
                    using (SolidBrush tint = new SolidBrush(Dark ? Color.FromArgb(214, 30, 30, 34) : Color.FromArgb(206, 243, 243, 245)))
                        g.FillRectangle(tint, panel);
                    return;
                }
            }
            using (SolidBrush br = new SolidBrush(Solid)) g.FillRectangle(br, panel);
        }
    }

    // Je bureaubladachtergrond, verkleind en vervaagd, voor het Mica-effect.
    static class Wallpaper
    {
        static Bitmap source;          // verkleinde kopie van de achtergrond
        static string sourceKey;
        static Bitmap lastBlur;
        static Rectangle lastRect;

        // Voor de zelftest: een vaste achtergrond in plaats van die van de pc
        public static Bitmap Override;

        static string FilePath()
        {
            string t = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Windows\Themes\TranscodedWallpaper");
            if (File.Exists(t)) return t;
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop"))
                {
                    string p = k != null ? k.GetValue("WallPaper") as string : null;
                    if (!string.IsNullOrEmpty(p) && File.Exists(p)) return p;
                }
            }
            catch { }
            return null;
        }

        static Bitmap Source()
        {
            if (Override != null) return Override;
            string f = FilePath();
            if (f == null) return null;
            string key;
            try { key = f + "|" + File.GetLastWriteTimeUtc(f).Ticks; } catch { return null; }
            if (key == sourceKey) return source;
            try
            {
                // Lezen via een kopie in het geheugen, zodat het bestand niet op slot blijft staan
                byte[] bytes = File.ReadAllBytes(f);
                using (MemoryStream ms = new MemoryStream(bytes))
                using (Image img = Image.FromStream(ms))
                {
                    // Klein houden: 480 punten breed is ruim genoeg voor een vervaagde achtergrond
                    int w = Math.Min(480, img.Width), h = Math.Max(1, (int)Math.Round(img.Height * (w / (double)img.Width)));
                    Bitmap small = new Bitmap(w, h, PixelFormat.Format32bppArgb);
                    using (Graphics g = Graphics.FromImage(small))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                        g.DrawImage(img, 0, 0, w, h);
                    }
                    if (source != null) source.Dispose();
                    source = small;
                    sourceKey = key;
                    lastRect = Rectangle.Empty;
                }
            }
            catch (Exception e)
            {
                Log.Error("Achtergrond lezen mislukt", e);
                sourceKey = key; source = null;
            }
            return source;
        }

        // Het stukje achtergrond achter het venster, sterk vervaagd (zoals Windows 'vullen' het neerzet)
        public static Bitmap BlurredCrop(Rectangle onScreen)
        {
            Bitmap src = Source();
            if (src == null) return null;
            if (lastBlur != null && lastRect == onScreen) return lastBlur;
            Rectangle scr;
            try { scr = System.Windows.Forms.Screen.FromRectangle(onScreen).Bounds; }
            catch { scr = new Rectangle(0, 0, 1920, 1080); }
            if (Override != null) scr = new Rectangle(0, 0, Override.Width, Override.Height);
            // vullen: zo groot dat het hele scherm bedekt is, in het midden
            double scale = Math.Max(scr.Width / (double)src.Width, scr.Height / (double)src.Height);
            double offX = (scr.Width - src.Width * scale) / 2, offY = (scr.Height - src.Height * scale) / 2;
            RectangleF crop = new RectangleF(
                (float)((onScreen.X - scr.X - offX) / scale), (float)((onScreen.Y - scr.Y - offY) / scale),
                (float)(onScreen.Width / scale), (float)(onScreen.Height / scale));
            int bw = Math.Max(2, onScreen.Width / 28), bh = Math.Max(2, onScreen.Height / 28);
            Bitmap blur = new Bitmap(bw, bh, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(blur))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                using (ImageAttributes ia = new ImageAttributes())
                {
                    ia.SetWrapMode(WrapMode.TileFlipXY);
                    g.DrawImage(src, new Rectangle(0, 0, bw, bh), crop.X, crop.Y, crop.Width, crop.Height, GraphicsUnit.Pixel, ia);
                }
            }
            if (lastBlur != null) lastBlur.Dispose();
            lastBlur = blur;
            lastRect = onScreen;
            return blur;
        }
    }
}
