namespace Dusk
{
    // Wat is er nieuw (te zien bij Instellingen > Bijwerken). Zit in het programma zelf: ook zonder internet te lezen.
    // Bij een nieuwe versie bovenaan een blok toevoegen (de test controleert dat het bovenste blok Program.Version is).
    public static class Changelog
    {
        public class Version { public string Number, Date; public string[][] Lines; }

        static Version V(string n, string d, params string[][] lines) { Version v = new Version(); v.Number = n; v.Date = d; v.Lines = lines; return v; }

        // Nederlands en Engels; Spaans en Frans krijgen de Engelse regel
        static string[] L(string nl, string en) { return new string[] { nl, en }; }

        public static readonly Version[] All = {
            V("1.5.0", "8 november 2026",
                L("Aftellen groter: groot icoon en grote naam van wat er gaat gebeuren", "Bigger countdown: large icon and name of what is about to happen"),
                L("Nieuwe keuze: cijfer of klik voert meteen uit, zonder Enter (Sneltoets en timer)", "New option: a number or click runs right away, without Enter (Shortcut and timer)"),
                L("Bij Menu: klik op het pijltje voor korte uitleg bij elke knop", "In Menu: click the arrow for a short explanation of each button"),
                L("Nog gladdere letters op 4K-schermen", "Even smoother text on 4K screens")),
            V("1.4.0", "8 november 2026",
                L("Nieuw: Gebruiker wisselen, net als in het startmenu", "New: Switch user, just like the Start menu"),
                L("Instellingen > Menu: kies welke knoppen je ziet en sleep ze in je eigen volgorde", "Settings > Menu: choose which buttons you see and drag them into your own order"),
                L("Dezelfde knoppen in het rechtsklikmenu van het taakbalkicoon, met het gewone aftellen", "The same buttons in the taskbar icon's right-click menu, with the usual countdown")),
            V("1.3.2", "8 november 2026",
                L("Vette woorden weer halfvet, zoals in Windows 11", "Bold words are semibold again, like Windows 11"),
                L("Tekst in het lichte thema even krachtig als in het donkere", "Text in the light theme as strong as in the dark theme")),
            V("1.3.0", "8 november 2026",
                L("Nieuwe manier van tekst zetten: even scherp in licht, donker en elke kleur, zonder gekleurde randjes", "New text rendering: equally sharp in light, dark and every colour, without coloured fringes"),
                L("Grootte kiezen: klein, normaal, groot of extra groot (Uiterlijk)", "Choose a size: small, normal, large or extra large (Appearance)"),
                L("Rustiger Instellingen: alleen titels, geen uitlegregels meer", "Calmer Settings: titles only, no extra explanation lines"),
                L("Gebruik bijhouden op deze pc, om te zien wat wel en niet gebruikt wordt (Over)", "Keep usage stats on this PC, to see what is and is not used (About)")),
            V("1.2.2", "8 november 2026",
                L("Bijwerken kan niet meer vastlopen: geen los vraagvenster meer dat achter Dusk verdwijnt", "Updating can no longer get stuck: no separate question window hiding behind Dusk"),
                L("Staat Instellingen open, dan staat het venster niet meer altijd bovenop en gaat het mee weg met Instellingen", "While Settings is open, the window is no longer always on top and goes away with Settings")),
            V("1.2.1", "8 november 2026",
                L("Tekst wordt nu door Windows zelf gezet: even scherp als in Verkenner, ook in Instellingen", "Text is now drawn by Windows itself: as sharp as in File Explorer, also in Settings"),
                L("Mica toont alleen nog de kleur van je bureaublad, geen vreemde vormen meer", "Mica now only shows the colour of your desktop, no more odd shapes"),
                L("Effen bureaubladkleur of Windows Spotlight: geen oude achtergrondfoto meer", "Solid desktop colour or Windows Spotlight: no more old wallpaper photo"),
                L("Halfvette koppen, zoals in Windows 11", "Semibold headings, like Windows 11")),
            V("1.2.0", "7 november 2026",
                L("Scherpere en iets grotere tekst, net als in Windows zelf", "Sharper, slightly larger text, like Windows itself"),
                L("Gladde maan bij Slaapstand", "Smooth moon icon for Sleep"),
                L("Knoppen om naar de taakbalk te gaan en om Dusk te stoppen", "Buttons to minimise to the taskbar and to quit Dusk"),
                L("Dusk blijft in beeld als Instellingen open staat, zo zie je meteen wat er verandert", "Dusk stays visible while Settings is open, so you see changes right away")),
            V("1.1.0", "7 november 2026",
                L("Bijwerken met een klik, bij Instellingen > Bijwerken", "Update with one click, in Settings > Update"),
                L("Melding als er een nieuwe versie is", "A notice when there is a new version"),
                L("Hier: wat er in elke versie nieuw is", "Here: what is new in each version")),
            V("1.0.1", "7 november 2026",
                L("Tekst op de knoppen valt niet meer weg achter puntjes", "Button text is no longer cut off")),
            V("1.0.0", "7 november 2026",
                L("Eerste versie: slaapstand, sluimerstand, opnieuw opstarten en afsluiten", "First version: sleep, hibernate, restart and shut down"),
                L("Timer met draaiwieltjes, vaste tijden en aftellen bij Nu", "Timer with wheels, fixed times and a countdown for Now"),
                L("Vier talen, licht en donker, Mica, effen of schemer", "Four languages, light and dark, Mica, solid or dusk"))
        };

        public static string Text(string[] line)
        {
            string lang = Settings.Current != null ? Settings.Current.Language : "en";
            return lang == "nl" ? line[0] : line[1];
        }
    }
}
