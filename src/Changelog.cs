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
