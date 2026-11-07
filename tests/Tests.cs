using System;
using System.Collections.Generic;

namespace Dusk
{
    // Tests voor alles wat niet van het scherm afhangt: tijd lezen, sneltoetsen, instellingen, timer, afsluitvlaggen, talen.
    static class Tests
    {
        static int failed, passed;

        static void Eq<T>(T expected, T actual, string what)
        {
            if (EqualityComparer<T>.Default.Equals(expected, actual)) { passed++; return; }
            failed++;
            Console.Error.WriteLine("MISLUKT " + what + ": verwacht <" + expected + ">, kreeg <" + actual + ">");
        }

        static void True(bool b, string what) { Eq(true, b, what); }

        static int Main()
        {
            Program.SelfTest = true;
            Settings.Current = new Settings();
            Settings.Current.Language = "nl";
            Run("talen", Languages);
            Run("tijd lezen", TimeParsing);
            Run("tijd tonen", TimeFormatting);
            Run("plus en min", Stepping);
            Run("sneltoetsen", HotkeyTests);
            Run("instellingen", SettingsTests);
            Run("afsluitvlaggen", PowerFlags);
            Run("timer", SchedulerTests);
            Run("bijwerken", UpdateTests);
            Console.WriteLine(passed + " geslaagd, " + failed + " mislukt");
            return failed == 0 ? 0 : 1;
        }

        static void Run(string name, Action a)
        {
            try { a(); }
            catch (Exception e) { failed++; Console.Error.WriteLine("FOUT in " + name + ": " + e); }
        }

        static void Languages()
        {
            foreach (string l in Settings.Languages)
            {
                True(Lang.All.ContainsKey(l), "taal " + l + " bestaat");
                Eq(Lang.KeyCount, Lang.All[l].Count, "aantal teksten " + l);
                foreach (KeyValuePair<string, string> kv in Lang.All[l])
                {
                    // Rida's regel: nooit het lange gedachtestreepje
                    True(kv.Value.IndexOf((char)0x2014) < 0, "geen lang streepje in " + l + "/" + kv.Key);
                    True(kv.Value.Trim().Length > 0, "tekst niet leeg " + l + "/" + kv.Key);
                }
            }
            Eq("Nu afsluiten", Lang.Phrase(PowerAction.Shutdown, TimeChoice.Now), "nu afsluiten");
            Eq("Afsluiten over 30 min", Lang.Phrase(PowerAction.Shutdown, TimeChoice.After(30)), "over 30");
            Eq("Slaapstand om 23:30", Lang.Phrase(PowerAction.Sleep, TimeChoice.At(23, 30)), "om 23:30");
            Settings.Current.Language = "en";
            Eq("Shut down in 1 h 30 min", Lang.Phrase(PowerAction.Shutdown, TimeChoice.After(90)), "engels");
            Settings.Current.Language = "nl";
        }

        static void TimeParsing()
        {
            Eq(true, TimeInput.Parse("").IsNow, "leeg is nu");
            Eq(true, TimeInput.Parse("Nu").IsNow, "Nu");
            Eq(true, TimeInput.Parse("now").IsNow, "now");
            Eq(true, TimeInput.Parse("0").IsNow, "0");
            Eq(30, TimeInput.Parse("30").Minutes, "30");
            Eq(30, TimeInput.Parse("30m").Minutes, "30m");
            Eq(90, TimeInput.Parse("90 min").Minutes, "90 min");
            Eq(60, TimeInput.Parse("1u").Minutes, "1u");
            Eq(90, TimeInput.Parse("1,5u").Minutes, "1,5u");
            Eq(90, TimeInput.Parse("1.5 h").Minutes, "1.5 h");
            Eq(120, TimeInput.Parse("2 uur").Minutes, "2 uur");
            Eq(90, TimeInput.Parse("1u30").Minutes, "1u30");
            Eq(1440, TimeInput.Parse("1440").Minutes, "max");
            Eq(false, TimeInput.Parse("1441").Valid, "boven max");
            Eq(false, TimeInput.Parse("1,5").Valid, "1,5 zonder eenheid");
            Eq(false, TimeInput.Parse("abc").Valid, "tekst");
            Eq(false, TimeInput.Parse("30 15").Valid, "twee getallen");
            TimeChoice c = TimeInput.Parse("23:30");
            True(c.Valid && c.IsClock && c.Hour == 23 && c.Minute == 30, "23:30");
            c = TimeInput.Parse("7.05");
            True(c.Valid && c.IsClock && c.Hour == 7 && c.Minute == 5, "7.05");
            Eq(false, TimeInput.Parse("24:00").Valid, "24:00");
            Eq(false, TimeInput.Parse("12:60").Valid, "12:60");

            DateTime now = new DateTime(2026, 11, 7, 22, 0, 0);
            Eq(new DateTime(2026, 11, 7, 23, 30, 0), TimeChoice.At(23, 30).TargetFrom(now), "vanavond");
            Eq(new DateTime(2026, 11, 8, 7, 0, 0), TimeChoice.At(7, 0).TargetFrom(now), "morgenochtend");
            Eq(new DateTime(2026, 11, 8, 22, 0, 0), TimeChoice.At(22, 0).TargetFrom(now), "zelfde minuut is morgen");
            Eq(now.AddMinutes(45), TimeChoice.After(45).TargetFrom(now), "over 45");
        }

        static void TimeFormatting()
        {
            Eq("1:05", TimeInput.Clock(TimeSpan.FromSeconds(65)), "1:05");
            Eq("1:00:05", TimeInput.Clock(TimeSpan.FromSeconds(3605)), "1:00:05");
            Eq("0:01", TimeInput.Clock(TimeSpan.FromSeconds(0.2)), "naar boven afronden");
            Eq("0:00", TimeInput.Clock(TimeSpan.FromSeconds(-3)), "nooit negatief");
            Eq("Nu", TimeInput.Label(TimeChoice.Now), "label nu");
            Eq("45 min", TimeInput.Label(TimeChoice.After(45)), "45 min");
            Eq("1 uur", TimeInput.Label(TimeChoice.After(60)), "1 uur");
            Eq("1 uur 30 min", TimeInput.Label(TimeChoice.After(90)), "1 uur 30");
            Eq(90, TimeInput.FromPicker(false, 1, 30).Minutes, "wieltjes over");
            True(TimeInput.FromPicker(true, 23, 45).IsClock, "wieltjes om");
            Eq(true, TimeInput.FromPicker(false, 0, 0).IsNow, "wieltjes nul is nu");
            Eq("23:30", TimeInput.PresetText(TimeChoice.At(23, 30)), "vaste knop tijdstip");
            Eq("90", TimeInput.PresetText(TimeChoice.After(90)), "vaste knop minuten");
            Eq("07:05", TimeInput.Label(TimeChoice.At(7, 5)), "07:05");
        }

        static void Stepping()
        {
            DateTime now = new DateTime(2026, 11, 7, 22, 0, 0);
            Eq(15, TimeInput.Step(TimeChoice.Now, 15, 1, now).Minutes, "nu +");
            Eq(0, TimeInput.Step(TimeChoice.Now, 15, -1, now).Minutes, "nu - blijft nu");
            Eq(30, TimeInput.Step(TimeChoice.After(20), 15, 1, now).Minutes, "20 +");
            Eq(15, TimeInput.Step(TimeChoice.After(20), 15, -1, now).Minutes, "20 -");
            Eq(0, TimeInput.Step(TimeChoice.After(15), 15, -1, now).Minutes, "15 -");
            Eq(1440, TimeInput.Step(TimeChoice.After(1440), 15, 1, now).Minutes, "niet boven 24 uur");
            Eq(105, TimeInput.Step(TimeChoice.At(23, 30), 15, 1, now).Minutes, "tijdstip + wordt minuten");
        }

        static void HotkeyTests()
        {
            int m, vk;
            True(Hotkeys.Parse("Win+Shift+Q", out m, out vk), "Win+Shift+Q");
            Eq(W.MOD_WIN | W.MOD_SHIFT, m, "modifiers");
            Eq((int)System.Windows.Forms.Keys.Q, vk, "Q");
            Eq("Win+Shift+Q", Hotkeys.Format(m, vk), "terug naar tekst");
            True(Hotkeys.Parse("ctrl+alt+5", out m, out vk), "cijfer");
            Eq("Ctrl+Alt+5", Hotkeys.Format(m, vk), "cijfer als tekst");
            True(Hotkeys.Parse("F13", out m, out vk), "F13 alleen mag");
            Eq(false, Hotkeys.Parse("Q", out m, out vk), "alleen Q");
            Eq(false, Hotkeys.Parse("Shift+Q", out m, out vk), "alleen Shift");
            Eq(false, Hotkeys.Parse("Win+", out m, out vk), "geen toets");
            Eq(false, Hotkeys.Parse("Hyper+Q", out m, out vk), "onbekend");
            Eq(false, Hotkeys.Parse("Ctrl+Escape", out m, out vk), "Esc niet");
            foreach (string f in Hotkeys.Fallbacks) True(Hotkeys.Parse(f, out m, out vk), "reserve " + f);
        }

        static void SettingsTests()
        {
            Settings a = new Settings();
            a.Language = "fr"; a.Theme = "light"; a.Backdrop = "dusk"; a.Accent = "roze"; a.Hotkey = "Ctrl+Alt+End";
            a.StepMinutes = 10; a.WarnMinutes = 5; a.DelaySeconds = 30; a.OpenAt = "last"; a.LastX = -1200; a.LastY = 300; a.Presets = new List<string> { "45", "23:30" }; a.Forced = true; a.Hidden = new List<PowerAction> { PowerAction.Hibernate }; a.Order = new List<PowerAction> { PowerAction.SwitchUser, PowerAction.Shutdown, PowerAction.Sleep, PowerAction.Hibernate, PowerAction.Restart, PowerAction.Lock, PowerAction.Logoff }; a.DefaultAction = PowerAction.Lock;
            a.Zoom = 130; a.TrackUsage = false;
            Settings b = Settings.Parse(a.Serialize());
            Eq(a.Serialize(), b.Serialize(), "heen en terug");
            Settings c = Settings.Parse("taal=xx\nthema=paars\nstap=7\nsneltoets=Q\nkapot\n=leeg\nstandaard=4\n");
            Settings d = new Settings();
            Eq(d.Theme, c.Theme, "onbekend thema");
            Eq(d.StepMinutes, c.StepMinutes, "onbekende stap");
            Eq(d.Hotkey, c.Hotkey, "ongeldige sneltoets");
            Eq(PowerAction.Shutdown, c.DefaultAction, "vergrendelen zonder de optie aan");
            Eq(d.Serialize(), Settings.Parse(null).Serialize(), "leeg bestand");
            Settings e = Settings.Parse("vaste=15,abc,15,0,23:30,60,90,120,240\nwachttijd=7\nopenen=boven\n");
            Eq("15,23:30,60,90,120", string.Join(",", e.Presets.ToArray()), "vaste tijden: geldig, uniek, max 5");
            Eq(10, e.DelaySeconds, "onbekende wachttijd");
            Eq("smart", e.OpenAt, "onbekende plek");
            Eq(100, Settings.Parse("grootte=250\n").Zoom, "onbekende grootte");
            // menu: volgorde, verborgen, oude instelling, nooit leeg
            Settings m1 = Settings.Parse("volgorde=shutdown,sleep,bestaatniet,sleep\nverborgen=hibernate\n");
            Eq("shutdown,sleep,restart,lock,logoff,switchuser", string.Join(",", m1.MenuActions().ConvertAll(Lang.Key).ToArray()), "menu: volgorde, onbekend weg, ontbrekende achteraan");
            Settings m2 = Settings.Parse("vergrendelen=1\n");
            True(m2.MenuActions().Contains(PowerAction.Lock) && m2.MenuActions().Contains(PowerAction.Logoff), "oude instelling vergrendelen=1");
            True(!m2.MenuActions().Contains(PowerAction.SwitchUser), "gebruiker wisselen standaard uit");
            Settings m3 = Settings.Parse("verborgen=sleep,hibernate,restart,shutdown,lock,logoff,switchuser\nstandaard=0\n");
            Eq(1, m3.MenuActions().Count, "menu nooit leeg");
            Eq(PowerAction.Shutdown, m3.DefaultAction, "standaard staat in het menu");
            Eq(4, new Settings().MenuActions().Count, "standaard vier opties");
            Settings m4 = Settings.Parse("verborgen=\nstandaard=6\n");
            Eq(PowerAction.SwitchUser, m4.DefaultAction, "gebruiker wisselen als standaard");
            Eq(115, Settings.Parse("grootte=115\n").Zoom, "grootte 115");
            Eq(true, new Settings().TrackUsage, "gebruik standaard aan");
            // gebruik: heen en terug, kapotte regels negeren
            SortedDictionary<string, int> u = Usage.Parse("# kop\nactie.afsluiten=3\nkapot\ntimer.gestart=x\ngestart=12\n");
            Eq(2, u.Count, "gebruik: alleen geldige regels");
            Eq(3, u["actie.afsluiten"], "gebruik: teller");
            Eq(Usage.Serialize(u), Usage.Serialize(Usage.Parse(Usage.Serialize(u))), "gebruik heen en terug");
            DateTime now = new DateTime(2026, 11, 7, 17, 0, 0);
            Eq("Na Enter: 10 seconden aftellen, dan afsluiten", Lang.Info(PowerAction.Shutdown, TimeChoice.Now, 10, now), "info aftellen");
            Eq("Na Enter: meteen vergrendelen", Lang.Info(PowerAction.Lock, TimeChoice.Now, 0, now), "info meteen");
            Eq("Afsluiten over 30 min, om 17:30", Lang.Info(PowerAction.Shutdown, TimeChoice.After(30), 10, now), "info timer");
            Eq("Slaapstand om 23:30, over 6 uur 30 min", Lang.Info(PowerAction.Sleep, TimeChoice.At(23, 30), 10, now), "info tijdstip");
        }

        static void PowerFlags()
        {
            Eq(Power.EWX_SHUTDOWN | Power.EWX_POWEROFF | Power.EWX_HYBRID_SHUTDOWN | Power.EWX_FORCEIFHUNG,
                Power.ExitFlags(PowerAction.Shutdown, false, true), "afsluiten zoals startmenu");
            Eq(Power.EWX_SHUTDOWN | Power.EWX_POWEROFF | Power.EWX_FORCEIFHUNG,
                Power.ExitFlags(PowerAction.Shutdown, false, false), "afsluiten zonder snel opstarten");
            Eq(Power.EWX_SHUTDOWN | Power.EWX_POWEROFF | Power.EWX_FORCE,
                Power.ExitFlags(PowerAction.Shutdown, true, false), "geforceerd");
            Eq(Power.EWX_REBOOT | Power.EWX_FORCEIFHUNG, Power.ExitFlags(PowerAction.Restart, false, true), "herstarten nooit hybride");
            Eq(Power.EWX_LOGOFF | Power.EWX_FORCEIFHUNG, Power.ExitFlags(PowerAction.Logoff, false, false), "afmelden");
            bool threw = false;
            try { Power.ExitFlags(PowerAction.Sleep, false, false); } catch (ArgumentException) { threw = true; }
            True(threw, "slaapstand is geen afsluitactie");

            byte[] caps = new byte[76];
            caps[5] = 1; caps[8] = 1;
            PowerCaps pc = PowerCaps.FromBytes(caps);
            True(pc.CanSleep && pc.CanHibernate && !pc.ModernStandby, "S3 en sluimerbestand");
            caps = new byte[76]; caps[20] = 1;
            pc = PowerCaps.FromBytes(caps);
            True(pc.CanSleep && !pc.CanHibernate && pc.ModernStandby, "Modern Standby");
            True(!PowerCaps.FromBytes(new byte[4]).CanSleep, "te kort");
        }

        static void UpdateTests()
        {
            Eq(Program.Version, Changelog.All[0].Number, "bovenste blok in Wat is er nieuw is deze versie");
            True(Updates.IsNewer("1.1.1", "1.1.0"), "nieuwer");
            True(!Updates.IsNewer("1.0.9", "1.1.0"), "ouder");
            True(Updates.IsNewer("v2.0", "1.9.9"), "met v ervoor");
            True(!Updates.IsNewer("onzin", "1.0.0"), "onzin");
            string body = "{\"tag_name\":\"v1.2.0\",\"html_url\":\"https://github.com/ridaabouhayan-hue/dusk/releases/tag/v1.2.0\",\"assets\":[" +
                "{\"name\":\"Dusk-Setup.exe\",\"digest\":\"sha256:" + new string('a', 64) + "\",\"browser_download_url\":\"https://github.com/ridaabouhayan-hue/dusk/releases/download/v1.2.0/Dusk-Setup.exe\"}," +
                "{\"name\":\"Dusk.exe\",\"digest\":\"sha256:" + new string('b', 64) + "\",\"browser_download_url\":\"https://github.com/ridaabouhayan-hue/dusk/releases/download/v1.2.0/Dusk.exe\"}]}";
            Updates.Info info = Updates.Parse(body);
            True(info != null, "antwoord gelezen");
            Eq("1.2.0", info.Version, "versie");
            Eq(new string('a', 64), info.Sha256, "controlecode van de installer, niet van de exe");
            True(info.SetupUrl.EndsWith("/Dusk-Setup.exe"), "installer");
            string evil = body.Replace("https://github.com/ridaabouhayan-hue/dusk/releases/download/v1.2.0/Dusk-Setup.exe", "https://example.com/Dusk-Setup.exe");
            True(Updates.Parse(evil).SetupUrl == null, "andere site: niet zelf installeren");
            True(Updates.Parse("{}") == null, "leeg antwoord");
        }

        static void SchedulerTests()
        {
            DateTime t = new DateTime(2026, 11, 7, 22, 0, 0);
            Scheduler s = new Scheduler();
            s.Clock = delegate { return t; };
            int warn = 0, due = 0, resumed = 0;
            s.Warn += delegate { warn++; };
            s.Due += delegate { due++; };
            s.Resumed += delegate { resumed++; };
            Settings.Current.WarnMinutes = 1;

            s.Start(PowerAction.Shutdown, t.AddMinutes(5));
            True(s.Active, "loopt");
            Eq(1f, s.Fraction, "begint vol");
            t = t.AddMinutes(3); s.OnTick();   // eerst gewone tikken (anders lijkt het alsof de pc sliep)
            for (int i = 0; i < 118; i++) { t = t.AddSeconds(0.5); s.OnTick(); }
            Eq(0, warn, "nog geen waarschuwing met 61 seconden over");
            for (int i = 0; i < 120; i++) { t = t.AddSeconds(0.5); s.OnTick(); }
            Eq(1, warn, "waarschuwing bij 1 minuut");
            Eq(0, due, "nog niet uitgevoerd met 1 seconde over");
            for (int i = 0; i < 4; i++) { t = t.AddSeconds(0.5); s.OnTick(); }
            Eq(1, due, "uitgevoerd op tijd");
            Eq(false, s.Active, "gestopt na uitvoeren");
            Eq(1, warn, "maar een keer gewaarschuwd");

            // De pc sliep langer dan de timer: niet alsnog uitvoeren
            s.Start(PowerAction.Restart, t.AddMinutes(10));
            t = t.AddMinutes(30); s.OnTick();
            Eq(1, resumed, "gemeld na slaapstand");
            Eq(1, due, "niet alsnog uitgevoerd");
            Eq(false, s.Active, "gestopt");

            // Tijd erbij
            s.Start(PowerAction.Shutdown, t.AddMinutes(1));
            s.Add(15);
            Eq(t.AddMinutes(16), s.Target, "15 minuten erbij");
            s.Cancel();
            Eq(false, s.Active, "geannuleerd");

            // Korter dan de waarschuwing: geen extra melding
            int before = warn;
            s.Start(PowerAction.Sleep, t.AddSeconds(40));
            t = t.AddSeconds(0.5); s.OnTick();
            Eq(before, warn, "geen waarschuwing bij een korte timer");
            s.Cancel();
            s.Dispose();
        }
    }
}
