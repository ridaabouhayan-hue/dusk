using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Dusk
{
    // Houdt bij hoe Dusk gebruikt wordt, zodat we kunnen zien wat wel en niet gebruikt wordt
    // (wat kan weg, wat mag erbij). Alleen tellers, in %APPDATA%\Dusk\gebruik.txt, op deze pc.
    // Er gaat niets naar internet. Uit te zetten in Instellingen > Over.
    public static class Usage
    {
        static readonly object Gate = new object();
        static SortedDictionary<string, int> counts;
        public static string FilePath = Path.Combine(Log.Dir, "gebruik.txt");

        public static void Count(string key)
        {
            if (!Settings.Current.TrackUsage || Program.SelfTest) return;
            try
            {
                lock (Gate)
                {
                    Load();
                    int n;
                    counts.TryGetValue(key, out n);
                    counts[key] = n + 1;
                    counts["laatst"] = int.Parse(DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
                    if (!counts.ContainsKey("sinds")) counts["sinds"] = counts["laatst"];
                    Save();
                }
            }
            catch (Exception e) { Log.Error("Gebruik bijhouden", e); }
        }

        public static int Get(string key)
        {
            lock (Gate)
            {
                try { Load(); } catch { return 0; }
                int n;
                return counts.TryGetValue(key, out n) ? n : 0;
            }
        }

        // Alle tellers die met dit begin starten bij elkaar opgeteld (bijvoorbeeld alle acties)
        public static int Sum(string prefix)
        {
            lock (Gate)
            {
                try { Load(); } catch { return 0; }
                int t = 0;
                foreach (KeyValuePair<string, int> kv in counts) if (kv.Key.StartsWith(prefix, StringComparison.Ordinal)) t += kv.Value;
                return t;
            }
        }

        static void Load()
        {
            if (counts != null) return;
            counts = Parse(File.Exists(FilePath) ? File.ReadAllText(FilePath, Encoding.UTF8) : "");
        }

        static void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            File.WriteAllText(FilePath, Serialize(counts), Encoding.UTF8);
        }

        public static SortedDictionary<string, int> Parse(string text)
        {
            SortedDictionary<string, int> d = new SortedDictionary<string, int>(StringComparer.Ordinal);
            foreach (string raw in text.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                int eq = line.IndexOf('=');
                int n;
                if (eq > 0 && int.TryParse(line.Substring(eq + 1).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out n))
                    d[line.Substring(0, eq).Trim()] = n;
            }
            return d;
        }

        public static string Serialize(SortedDictionary<string, int> d)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# Gebruik van Dusk op deze pc (alleen tellers, er gaat niets naar internet)");
            foreach (KeyValuePair<string, int> kv in d) sb.AppendLine(kv.Key + "=" + kv.Value.ToString(CultureInfo.InvariantCulture));
            return sb.ToString();
        }

        // Voor de test: de tellers in het geheugen opnieuw laten inlezen
        public static void Reset() { lock (Gate) counts = null; }
    }
}
