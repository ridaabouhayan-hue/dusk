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

        // De kleur waar de tekst ongeveer op staat (nodig om halfdoorzichtige tekst te mengen)
        public Color TextBack(string mode)
        {
            if (mode == "dusk") return Dark ? Color.FromArgb(25, 23, 36) : Color.FromArgb(246, 244, 250);
            if (mode == "mica") return Dark ? Color.FromArgb(32, 32, 36) : Color.FromArgb(240, 240, 243);
            return Solid;
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
                    PixelOffsetMode oldp = g.PixelOffsetMode;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    using (ImageAttributes ia = new ImageAttributes())
                    {
                        ia.SetWrapMode(WrapMode.TileFlipXY);   // geen donkere randjes bij het opschalen
                        g.DrawImage(blur, panel, 0, 0, blur.Width, blur.Height, GraphicsUnit.Pixel, ia);
                    }
                    g.InterpolationMode = old;
                    g.PixelOffsetMode = oldp;
                    // De tint van Mica: alleen de kleur van het bureaublad schemert er zacht doorheen, geen vormen
                    using (SolidBrush tint = new SolidBrush(Dark ? Color.FromArgb(222, 32, 32, 36) : Color.FromArgb(212, 240, 240, 243)))
                        g.FillRectangle(tint, panel);
                    return;
                }
            }
            using (SolidBrush br = new SolidBrush(Solid)) g.FillRectangle(br, panel);
        }
    }

    // Je bureaubladachtergrond, heel sterk vervaagd, voor het Mica-effect.
    // Net als bij Windows zie je alleen nog de kleuren van je achtergrond, geen vormen.
    static class Wallpaper
    {
        static Bitmap screenBlur;      // het hele scherm, klein en vervaagd
        static string blurKey;
        static Bitmap lastCrop;
        static Rectangle lastRect;
        static string lastKey;
        static DateTime checkedAt;
        static string stateCache;

        // Voor de zelftest: een vaste achtergrond in plaats van die van de pc
        public static Bitmap Override;

        // Wat voor achtergrond je in Windows hebt: een foto (pad), een effen kleur, of iets wat we niet kunnen lezen
        public static string State()
        {
            if (Override != null) return "test";
            if (stateCache != null && (DateTime.UtcNow - checkedAt).TotalSeconds < 2) return stateCache;
            checkedAt = DateTime.UtcNow;
            stateCache = ReadState();
            return stateCache;
        }

        static string ReadState()
        {
            try
            {
                int type = 0;
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Wallpapers"))
                {
                    object v = k != null ? k.GetValue("BackgroundType") : null;
                    if (v is int) type = (int)v;
                }
                string wp = null;
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop"))
                    wp = k != null ? k.GetValue("WallPaper") as string : null;
                // 1 = effen kleur; een leeg pad betekent ook geen foto
                if (type == 1 || (type == 0 && string.IsNullOrEmpty(wp))) return "color:" + SolidColor().ToArgb();
                // 3 = Windows Spotlight: dat plaatje staat niet op een vaste plek, dus geen oude foto tonen
                if (type == 3) return "none";
                string t = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Windows\Themes\TranscodedWallpaper");
                string f = File.Exists(t) ? t : (!string.IsNullOrEmpty(wp) && File.Exists(wp) ? wp : null);
                if (f == null) return "none";
                return "file:" + f + "|" + File.GetLastWriteTimeUtc(f).Ticks;
            }
            catch (Exception e)
            {
                Log.Error("Achtergrond bepalen mislukt", e);
                return "none";
            }
        }

        static Color SolidColor()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Control Panel\Colors"))
                {
                    string v = k != null ? k.GetValue("Background") as string : null;
                    if (v != null)
                    {
                        string[] parts = v.Split(' ');
                        if (parts.Length == 3) return Color.FromArgb(int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[2]));
                    }
                }
            }
            catch { }
            return Color.Black;
        }

        // Het hele scherm met je achtergrond erop ("vullen"), heel klein en daarna nog eens vervaagd
        static Bitmap ScreenBlur(Rectangle scr, string state)
        {
            string key = state + "|" + scr.Width + "x" + scr.Height;
            if (key == blurKey) return screenBlur;
            Bitmap result = null;
            if (state.StartsWith("color:"))
            {
                result = new Bitmap(2, 2, PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(result)) g.Clear(Color.FromArgb(int.Parse(state.Substring(6))));
            }
            else if (state == "test" || state.StartsWith("file:"))
            {
                try
                {
                    Image img;
                    if (state == "test") img = (Image)Override.Clone();
                    else
                    {
                        string f = state.Substring(5, state.LastIndexOf('|') - 5);
                        // Lezen via een kopie in het geheugen, zodat het bestand niet op slot blijft staan
                        img = Image.FromStream(new MemoryStream(File.ReadAllBytes(f)));
                    }
                    using (img)
                    {
                        int bw = 48, bh = Math.Max(2, (int)Math.Round(48.0 * scr.Height / scr.Width));
                        result = new Bitmap(bw, bh, PixelFormat.Format32bppArgb);
                        double scale = Math.Max(scr.Width / (double)img.Width, scr.Height / (double)img.Height);
                        double cw = scr.Width / scale, ch = scr.Height / scale;
                        RectangleF src = new RectangleF((float)((img.Width - cw) / 2), (float)((img.Height - ch) / 2), (float)cw, (float)ch);
                        using (Graphics g = Graphics.FromImage(result))
                        {
                            g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                            using (ImageAttributes ia = new ImageAttributes())
                            {
                                ia.SetWrapMode(WrapMode.TileFlipXY);
                                g.DrawImage(img, new Rectangle(0, 0, bw, bh), src.X, src.Y, src.Width, src.Height, GraphicsUnit.Pixel, ia);
                            }
                        }
                        BoxBlur(result, 3);
                        BoxBlur(result, 3);
                        BoxBlur(result, 3);
                    }
                }
                catch (Exception e)
                {
                    Log.Error("Achtergrond lezen mislukt", e);
                    if (result != null) result.Dispose();
                    result = null;
                }
            }
            if (screenBlur != null) screenBlur.Dispose();
            screenBlur = result;
            blurKey = key;
            lastRect = Rectangle.Empty;
            return screenBlur;
        }

        // Eenvoudige vervaging: elke pixel wordt het gemiddelde van zijn buren (drie keer = bijna Gauss)
        public static void BoxBlur(Bitmap b, int r)
        {
            int w = b.Width, h = b.Height;
            Rectangle all = new Rectangle(0, 0, w, h);
            BitmapData d = b.LockBits(all, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            int[] px = new int[w * h];
            try
            {
                for (int y = 0; y < h; y++) System.Runtime.InteropServices.Marshal.Copy(new IntPtr(d.Scan0.ToInt64() + (long)y * d.Stride), px, y * w, w);
                int[] tmp = new int[w * h];
                Pass(px, tmp, w, h, r, true);
                Pass(tmp, px, w, h, r, false);
                for (int y = 0; y < h; y++) System.Runtime.InteropServices.Marshal.Copy(px, y * w, new IntPtr(d.Scan0.ToInt64() + (long)y * d.Stride), w);
            }
            finally { b.UnlockBits(d); }
        }

        static void Pass(int[] src, int[] dst, int w, int h, int r, bool horizontal)
        {
            int n = horizontal ? w : h, lines = horizontal ? h : w;
            for (int line = 0; line < lines; line++)
            {
                for (int i = 0; i < n; i++)
                {
                    int sa = 0, sr = 0, sg = 0, sb = 0, cnt = 0;
                    for (int k = -r; k <= r; k++)
                    {
                        int j = Math.Max(0, Math.Min(n - 1, i + k));
                        int c = horizontal ? src[line * w + j] : src[j * w + line];
                        sa += (c >> 24) & 255; sr += (c >> 16) & 255; sg += (c >> 8) & 255; sb += c & 255; cnt++;
                    }
                    int v = ((sa / cnt) << 24) | ((sr / cnt) << 16) | ((sg / cnt) << 8) | (sb / cnt);
                    if (horizontal) dst[line * w + i] = v; else dst[i * w + line] = v;
                }
            }
        }

        // Het stukje vervaagde achtergrond achter het venster
        public static Bitmap BlurredCrop(Rectangle onScreen)
        {
            string state = State();
            if (state == "none") return null;
            Rectangle scr;
            try { scr = System.Windows.Forms.Screen.FromRectangle(onScreen).Bounds; }
            catch { scr = new Rectangle(0, 0, 1920, 1080); }
            if (Override != null) scr = new Rectangle(0, 0, Override.Width, Override.Height);
            Bitmap sb = ScreenBlur(scr, state);
            if (sb == null) return null;
            string key = blurKey;
            if (lastCrop != null && lastRect == onScreen && lastKey == key) return lastCrop;
            double fx = sb.Width / (double)scr.Width, fy = sb.Height / (double)scr.Height;
            RectangleF crop = new RectangleF((float)((onScreen.X - scr.X) * fx), (float)((onScreen.Y - scr.Y) * fy),
                (float)(onScreen.Width * fx), (float)(onScreen.Height * fy));
            // Op een tussenmaat uitsnijden; het venster schaalt dit daarna glad op
            int cw = Math.Max(2, onScreen.Width / 8), ch = Math.Max(2, onScreen.Height / 8);
            Bitmap c = new Bitmap(cw, ch, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(c))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                using (ImageAttributes ia = new ImageAttributes())
                {
                    ia.SetWrapMode(WrapMode.TileFlipXY);
                    g.DrawImage(sb, new Rectangle(0, 0, cw, ch), crop.X, crop.Y, crop.Width, crop.Height, GraphicsUnit.Pixel, ia);
                }
            }
            if (lastCrop != null) lastCrop.Dispose();
            lastCrop = c;
            lastRect = onScreen;
            lastKey = key;
            return c;
        }
    }
}
