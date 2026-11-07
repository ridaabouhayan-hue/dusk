using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Dusk
{
    // Wat je in het tijdvak typt: "Nu", "30", "1,5u", "90m" of een tijdstip als "23:30".
    public struct TimeChoice
    {
        public bool Valid;
        public bool IsClock;        // een tijdstip (23:30) in plaats van een aantal minuten
        public int Minutes;         // bij een aantal minuten; 0 = nu
        public int Hour, Minute;    // bij een tijdstip

        public static TimeChoice Now { get { TimeChoice t = new TimeChoice(); t.Valid = true; return t; } }
        public static TimeChoice After(int minutes) { TimeChoice t = new TimeChoice(); t.Valid = true; t.Minutes = minutes; return t; }
        public static TimeChoice At(int h, int m) { TimeChoice t = new TimeChoice(); t.Valid = true; t.IsClock = true; t.Hour = h; t.Minute = m; return t; }

        public bool IsNow { get { return Valid && !IsClock && Minutes == 0; } }

        // Het moment waarop de actie moet gebeuren. Een tijdstip dat vandaag al voorbij is, is morgen.
        public DateTime TargetFrom(DateTime now)
        {
            if (!IsClock) return now.AddMinutes(Minutes);
            DateTime t = new DateTime(now.Year, now.Month, now.Day, Hour, Minute, 0, now.Kind);
            if (t <= now) t = t.AddDays(1);
            return t;
        }
    }

    public static class TimeInput
    {
        public const int MaxMinutes = 24 * 60;
        static readonly CultureInfo C = CultureInfo.InvariantCulture;
        static readonly string[] NowWords = { "nu", "now", "ahora", "maintenant", "ya" };

        public static TimeChoice Parse(string text)
        {
            TimeChoice bad = new TimeChoice();
            if (text == null) return TimeChoice.Now;
            string s = text.Trim().ToLowerInvariant();
            if (s.Length == 0 || Array.IndexOf(NowWords, s) >= 0) return TimeChoice.Now;

            // tijdstip: 23:30, 23.30, 7:05, 0:15
            Match m = Regex.Match(s, @"^(\d{1,2})[:.](\d{2})$");
            if (m.Success)
            {
                int h = int.Parse(m.Groups[1].Value, C), mi = int.Parse(m.Groups[2].Value, C);
                if (h > 23 || mi > 59) return bad;
                return TimeChoice.At(h, mi);
            }

            // aantal uren of minuten: 30, 30m, 30 min, 1u, 1,5 u, 2h, 1u30
            m = Regex.Match(s, @"^(\d{1,4}(?:[.,]\d{1,2})?)\s*(m|min|mins|minuten|minutes|minutos|u|uur|h|hr|hrs|hours|horas|heure|heures)?\s*(\d{1,2})?\s*(m|min)?$");
            if (!m.Success) return bad;
            double num;
            if (!double.TryParse(m.Groups[1].Value.Replace(',', '.'), NumberStyles.Float, C, out num)) return bad;
            string unit = m.Groups[2].Value;
            bool hours = unit.StartsWith("u") || unit.StartsWith("h");
            double minutes = hours ? num * 60 : num;
            if (m.Groups[3].Success && m.Groups[3].Value.Length > 0)
            {
                if (!hours) return bad;   // "30 15" betekent niets
                minutes += int.Parse(m.Groups[3].Value, C);
            }
            else if (m.Groups[4].Success && m.Groups[4].Value.Length > 0) return bad;
            if (!hours && m.Groups[1].Value.IndexOfAny(new[] { '.', ',' }) >= 0) return bad;   // "1,5" zonder eenheid is onduidelijk
            int total = (int)Math.Round(minutes);
            if (total < 0 || total > MaxMinutes) return bad;
            return TimeChoice.After(total);
        }

        // "15 min", "1 u", "1 u 30 min", of "Nu"
        public static string Label(TimeChoice t)
        {
            if (!t.Valid) return "?";
            if (t.IsClock) return t.Hour.ToString("00", C) + ":" + t.Minute.ToString("00", C);
            if (t.Minutes == 0) return Lang.T("now");
            return Duration(t.Minutes);
        }

        public static string Duration(int minutes)
        {
            string h = Lang.T("unit.h");
            if (minutes < 60) return minutes.ToString(C) + " min";
            int hh = minutes / 60, mm = minutes % 60;
            if (mm == 0) return hh.ToString(C) + " " + h;
            return hh.ToString(C) + " " + h + " " + mm.ToString(C) + " min";
        }

        // Aftelklok: "4:05" onder het uur, "1:04:05" daarboven
        public static string Clock(TimeSpan left)
        {
            if (left < TimeSpan.Zero) left = TimeSpan.Zero;
            int total = (int)Math.Ceiling(left.TotalSeconds);
            int h = total / 3600, m = (total / 60) % 60, s = total % 60;
            if (h > 0) return h.ToString(C) + ":" + m.ToString("00", C) + ":" + s.ToString("00", C);
            return m.ToString(C) + ":" + s.ToString("00", C);
        }

        // Uit de draaiwieltjes: "over" uren en minuten, of "om" een tijdstip
        public static TimeChoice FromPicker(bool at, int h, int m)
        {
            if (at) return TimeChoice.At(((h % 24) + 24) % 24, ((m % 60) + 60) % 60);
            int total = Math.Max(0, Math.Min(MaxMinutes, h * 60 + m));
            return TimeChoice.After(total);
        }

        // Tekst voor een vaste knop ("15", "120", "23:30")
        public static string PresetText(TimeChoice t)
        {
            if (t.IsClock) return t.Hour.ToString("00", C) + ":" + t.Minute.ToString("00", C);
            return t.Minutes.ToString(C);
        }

        // Volgende stap met + of − (in stappen van 'step' minuten, nooit onder nu of boven 24 uur)
        public static TimeChoice Step(TimeChoice t, int step, int dir, DateTime now)
        {
            int cur;
            if (!t.Valid) cur = 0;
            else if (t.IsClock) cur = (int)Math.Round((t.TargetFrom(now) - now).TotalMinutes);
            else cur = t.Minutes;
            int next;
            if (dir > 0) next = (cur / step + 1) * step;
            else next = cur % step == 0 ? cur - step : cur / step * step;
            next = Math.Max(0, Math.Min(MaxMinutes, next));
            return TimeChoice.After(next);
        }
    }
}
