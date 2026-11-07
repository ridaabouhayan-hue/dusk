using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.Win32;

namespace Dusk
{
    // Instellingen in %APPDATA%\Dusk\instellingen.txt, als simpele regels "sleutel=waarde".
    public class Settings
    {
        public string Language = DefaultLanguage();
        public string Theme = "system";        // system, light, dark
        public string Backdrop = "mica";       // mica, solid, dusk
        public string Accent = "lila";         // lila, perzik, roze, blauw, windows
        public string Hotkey = "Win+Shift+Q";
        public int StepMinutes = 15;
        public int WarnMinutes = 1;            // 0 = geen waarschuwing
        public int DelaySeconds = 10;          // aftellen voordat "Nu" echt gebeurt; 0 = meteen
        public string OpenAt = "smart";        // smart, tray, center, last
        public int LastX = int.MinValue, LastY = int.MinValue;   // laatste plek (schermpunten)
        public List<string> Presets = new List<string>(DefaultPresets);
        public bool UpdateCheck = true;         // melding als er een nieuwe versie is
        public bool Forced = false;
        // Het menu in Dusk: alle opties in jouw volgorde, en welke uit staan
        public List<PowerAction> Order = new List<PowerAction>(AllActions);
        public List<PowerAction> Hidden = new List<PowerAction>(DefaultHidden);
        public PowerAction DefaultAction = PowerAction.Shutdown;
        public int Zoom = 100;                  // grootte van het venster en Instellingen, in procenten
        public bool TrackUsage = true;          // gebruik bijhouden in gebruik.txt (alleen op deze pc)

        public static readonly PowerAction[] AllActions = { PowerAction.Sleep, PowerAction.Hibernate, PowerAction.Restart, PowerAction.Shutdown, PowerAction.Lock, PowerAction.Logoff, PowerAction.SwitchUser };
        public static readonly PowerAction[] DefaultHidden = { PowerAction.Lock, PowerAction.Logoff, PowerAction.SwitchUser };

        // Wat er in Dusk staat, in jouw volgorde. Nooit leeg: dan blijft Afsluiten staan.
        public List<PowerAction> MenuActions()
        {
            List<PowerAction> l = new List<PowerAction>();
            foreach (PowerAction a in Order) if (!Hidden.Contains(a)) l.Add(a);
            if (l.Count == 0) l.Add(PowerAction.Shutdown);
            return l;
        }

        // Zet de standaardkeuze op iets wat ook echt in het menu staat
        public void FixDefault()
        {
            List<PowerAction> m = MenuActions();
            if (!m.Contains(DefaultAction)) DefaultAction = m.Contains(PowerAction.Shutdown) ? PowerAction.Shutdown : m[0];
        }

        static string KeyList(List<PowerAction> l)
        {
            List<string> parts = new List<string>();
            foreach (PowerAction a in l) parts.Add(Lang.Key(a));
            return string.Join(",", parts.ToArray());
        }

        static List<PowerAction> FromKeys(string v)
        {
            List<PowerAction> l = new List<PowerAction>();
            foreach (string part in v.Split(','))
            {
                PowerAction a;
                if (Lang.TryFromKey(part.Trim(), out a) && !l.Contains(a)) l.Add(a);
            }
            return l;
        }

        public static readonly string[] Languages = { "nl", "en", "es", "fr" };
        public static readonly int[] Steps = { 5, 10, 15, 30 };
        public static readonly int[] WarnChoices = { 0, 1, 2, 5 };
        public static readonly int[] DelayChoices = { 0, 5, 10, 15, 30 };
        public static readonly string[] OpenChoices = { "smart", "tray", "center", "last" };
        public static readonly int[] ZoomChoices = { 90, 100, 115, 130 };
        public static readonly string[] DefaultPresets = { "15", "30", "60", "120" };
        public const int MaxPresets = 5;

        public static Settings Current = new Settings();
        public static string FilePath = Path.Combine(Log.Dir, "instellingen.txt");
        static readonly CultureInfo C = CultureInfo.InvariantCulture;

        public static string DefaultLanguage()
        {
            string two = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            return Array.IndexOf(Languages, two) >= 0 ? two : "en";
        }

        public string Serialize()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("taal=" + Language);
            sb.AppendLine("thema=" + Theme);
            sb.AppendLine("achtergrond=" + Backdrop);
            sb.AppendLine("accent=" + Accent);
            sb.AppendLine("sneltoets=" + Hotkey);
            sb.AppendLine("stap=" + StepMinutes.ToString(C));
            sb.AppendLine("waarschuwing=" + WarnMinutes.ToString(C));
            sb.AppendLine("wachttijd=" + DelaySeconds.ToString(C));
            sb.AppendLine("openen=" + OpenAt);
            if (LastX != int.MinValue) sb.AppendLine("plek=" + LastX.ToString(C) + "," + LastY.ToString(C));
            sb.AppendLine("vaste=" + string.Join(",", Presets.ToArray()));
            sb.AppendLine("bijwerken=" + (UpdateCheck ? "1" : "0"));
            sb.AppendLine("geforceerd=" + (Forced ? "1" : "0"));
            sb.AppendLine("volgorde=" + KeyList(Order));
            sb.AppendLine("verborgen=" + KeyList(Hidden));
            sb.AppendLine("standaard=" + ((int)DefaultAction).ToString(C));
            sb.AppendLine("grootte=" + Zoom.ToString(C));
            sb.AppendLine("gebruik=" + (TrackUsage ? "1" : "0"));
            return sb.ToString();
        }

        // Onbekende of kapotte regels negeren: dan blijft de standaardwaarde staan.
        public static Settings Parse(string text)
        {
            Settings s = new Settings();
            if (string.IsNullOrEmpty(text)) return s;
            bool oldLock = false, hiddenSeen = false;
            foreach (string raw in text.Split('\n'))
            {
                string line = raw.Trim();
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                string k = line.Substring(0, eq).Trim(), v = line.Substring(eq + 1).Trim();
                int n;
                bool isNum = int.TryParse(v, NumberStyles.Integer, C, out n);
                switch (k)
                {
                    case "taal": if (Array.IndexOf(Languages, v) >= 0) s.Language = v; break;
                    case "thema": if (v == "system" || v == "light" || v == "dark") s.Theme = v; break;
                    case "achtergrond": if (v == "mica" || v == "solid" || v == "dusk") s.Backdrop = v; break;
                    case "accent": if (Array.IndexOf(Palette.AccentKeys, v) >= 0) s.Accent = v; break;
                    case "sneltoets": { int m, vk; if (Hotkeys.Parse(v, out m, out vk)) s.Hotkey = v; break; }
                    case "stap": if (isNum && Array.IndexOf(Steps, n) >= 0) s.StepMinutes = n; break;
                    case "waarschuwing": if (isNum && Array.IndexOf(WarnChoices, n) >= 0) s.WarnMinutes = n; break;
                    case "wachttijd": if (isNum && Array.IndexOf(DelayChoices, n) >= 0) s.DelaySeconds = n; break;
                    case "openen": if (Array.IndexOf(OpenChoices, v) >= 0) s.OpenAt = v; break;
                    case "grootte": if (isNum && Array.IndexOf(ZoomChoices, n) >= 0) s.Zoom = n; break;
                    case "gebruik": s.TrackUsage = v != "0"; break;
                    case "plek":
                        {
                            string[] xy = v.Split(',');
                            int px, py;
                            if (xy.Length == 2 && int.TryParse(xy[0], NumberStyles.Integer, C, out px) && int.TryParse(xy[1], NumberStyles.Integer, C, out py)) { s.LastX = px; s.LastY = py; }
                            break;
                        }
                    case "vaste":
                        {
                            List<string> l = new List<string>();
                            foreach (string part in v.Split(','))
                            {
                                string pp = part.Trim();
                                TimeChoice t = TimeInput.Parse(pp);
                                if (pp.Length > 0 && t.Valid && !t.IsNow && !l.Contains(pp) && l.Count < MaxPresets) l.Add(pp);
                            }
                            s.Presets = l;
                            break;
                        }
                    case "geforceerd": s.Forced = v == "1"; break;
                    case "bijwerken": s.UpdateCheck = v != "0"; break;
                    case "vergrendelen": oldLock = v == "1"; break;   // van voor versie 1.4
                    case "volgorde":
                        {
                            List<PowerAction> o = FromKeys(v);
                            foreach (PowerAction a in AllActions) if (!o.Contains(a)) o.Add(a);   // nieuwe opties achteraan
                            s.Order = o;
                            break;
                        }
                    case "verborgen": s.Hidden = FromKeys(v); hiddenSeen = true; break;
                    case "standaard": if (isNum && n >= 0 && n <= 6) s.DefaultAction = (PowerAction)n; break;
                }
            }
            // oud instellingenbestand: "vergrendelen en afmelden tonen" stond aan
            if (!hiddenSeen && oldLock) { s.Hidden.Remove(PowerAction.Lock); s.Hidden.Remove(PowerAction.Logoff); }
            s.FixDefault();
            return s;
        }

        public static void Load()
        {
            try { if (File.Exists(FilePath)) Current = Parse(File.ReadAllText(FilePath, Encoding.UTF8)); }
            catch (Exception e) { Log.Error("Instellingen laden mislukt", e); }
        }

        public void Save()
        {
            if (Program.SelfTest) return;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                string tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, Serialize(), Encoding.UTF8);
                if (File.Exists(FilePath)) File.Replace(tmp, FilePath, null);
                else File.Move(tmp, FilePath);
            }
            catch (Exception e) { Log.Error("Instellingen bewaren mislukt", e); }
        }

        // ---------------------------------------------------------------- Starten met Windows
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        public static bool Autostart
        {
            get
            {
                try
                {
                    using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey))
                        return k != null && k.GetValue("Dusk") != null;
                }
                catch { return false; }
            }
            set
            {
                if (Program.SelfTest) return;
                try
                {
                    using (RegistryKey k = Registry.CurrentUser.CreateSubKey(RunKey))
                    {
                        if (value) k.SetValue("Dusk", "\"" + System.Windows.Forms.Application.ExecutablePath + "\" --opstarten");
                        else k.DeleteValue("Dusk", false);
                    }
                }
                catch (Exception e) { Log.Error("Starten met Windows aanpassen mislukt", e); }
            }
        }
    }
}
