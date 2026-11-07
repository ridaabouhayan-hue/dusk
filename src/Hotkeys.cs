using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace Dusk
{
    // Sneltoetsen als tekst ("Win+Shift+Q") lezen en schrijven, en registreren bij Windows.
    public static class Hotkeys
    {
        public static bool Parse(string text, out int mods, out int vk)
        {
            mods = 0; vk = 0;
            if (string.IsNullOrEmpty(text)) return false;
            string[] parts = text.Split('+');
            for (int i = 0; i < parts.Length; i++)
            {
                string p = parts[i].Trim();
                if (p.Length == 0) return false;
                bool last = i == parts.Length - 1;
                string l = p.ToLowerInvariant();
                if (!last)
                {
                    if (l == "win") mods |= W.MOD_WIN;
                    else if (l == "ctrl" || l == "control") mods |= W.MOD_CONTROL;
                    else if (l == "alt") mods |= W.MOD_ALT;
                    else if (l == "shift") mods |= W.MOD_SHIFT;
                    else return false;
                    continue;
                }
                Keys k;
                if (p.Length == 1 && char.IsDigit(p[0])) k = (Keys)((int)Keys.D0 + (p[0] - '0'));
                else if (!Enum.TryParse(p, true, out k)) return false;
                if (!Allowed(k)) return false;
                vk = (int)k;
            }
            if (vk == 0) return false;
            // Zonder Ctrl, Alt of Win zou je de toets niet meer gewoon kunnen typen (F13 en hoger mogen wel alleen)
            if ((mods & (W.MOD_WIN | W.MOD_CONTROL | W.MOD_ALT)) == 0 && !(vk >= (int)Keys.F13 && vk <= (int)Keys.F24)) return false;
            return true;
        }

        static bool Allowed(Keys k)
        {
            if (k >= Keys.A && k <= Keys.Z) return true;
            if (k >= Keys.D0 && k <= Keys.D9) return true;
            if (k >= Keys.F1 && k <= Keys.F24) return true;
            switch (k)
            {
                case Keys.Space: case Keys.End: case Keys.Home: case Keys.Insert: case Keys.Delete:
                case Keys.PageUp: case Keys.PageDown: case Keys.Pause: case Keys.Escape:
                    return k != Keys.Escape;
            }
            return false;
        }

        public static string Format(int mods, int vk)
        {
            List<string> p = new List<string>();
            if ((mods & W.MOD_WIN) != 0) p.Add("Win");
            if ((mods & W.MOD_CONTROL) != 0) p.Add("Ctrl");
            if ((mods & W.MOD_ALT) != 0) p.Add("Alt");
            if ((mods & W.MOD_SHIFT) != 0) p.Add("Shift");
            Keys k = (Keys)vk;
            if (k >= Keys.D0 && k <= Keys.D9) p.Add(((char)('0' + (k - Keys.D0))).ToString());
            else p.Add(k.ToString());
            return string.Join("+", p.ToArray());
        }

        // Als je eigen sneltoets al bezet is, proberen we deze reserves (Windows houdt veel Win-combinaties voor zichzelf).
        public static readonly string[] Fallbacks = { "Win+Shift+Q", "Win+Alt+Q", "Ctrl+Shift+Alt+Q" };

        public const int Id = 0x5153;

        // Registreert de sneltoets. Geeft de sneltoets terug die het werd, of null als niets lukte.
        public static string Register(IntPtr hwnd, string wanted)
        {
            W.UnregisterHotKey(hwnd, Id);
            List<string> tries = new List<string>();
            tries.Add(wanted);
            foreach (string f in Fallbacks) if (!tries.Contains(f)) tries.Add(f);
            foreach (string t in tries)
            {
                int m, vk;
                if (!Parse(t, out m, out vk)) continue;
                if (W.RegisterHotKey(hwnd, Id, m | W.MOD_NOREPEAT, vk))
                {
                    if (t != wanted) Log.Info("Sneltoets " + wanted + " is bezet, " + t + " gebruikt");
                    return t;
                }
            }
            Log.Info("Geen sneltoets kunnen registreren");
            return null;
        }

        // Alleen deze ene sneltoets proberen (bij het wijzigen in Instellingen)
        public static bool TryRegisterExact(IntPtr hwnd, string text)
        {
            int m, vk;
            if (!Parse(text, out m, out vk)) return false;
            W.UnregisterHotKey(hwnd, Id);
            return W.RegisterHotKey(hwnd, Id, m | W.MOD_NOREPEAT, vk);
        }
    }
}
