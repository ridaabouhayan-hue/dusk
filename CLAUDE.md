# Dusk: werkafspraken voor Claude Code

Dusk is een lichte afsluit-app voor Windows 11 (slaapstand, sluimerstand, opnieuw opstarten, afsluiten, met timer) voor Rida Abouhayan. Het is zijn eigen losstaande app, los van Nexus of Language: geen Nexus-naam, -kleuren of -logo.

## Werkafspraken met Rida
- Schrijf in het Nederlands. Gebruik nooit het lange gedachtestreepje, ook niet in code-commentaar, teksten of commits (de tests controleren de teksten).
- Vraag eerst voordat je iets groots bouwt of verandert. Begin pas als Rida "begin met bouwen" zegt.
- Rida is geen programmeur. Leg kort en in gewone woorden uit wat je doet.
- Neem zelf de technische beslissingen en vraag alleen wat alleen Rida kan weten.

## Ontwerp (gekozen in het design canvas, november 2026)
- Hoofdscherm "B1+": venster rechtsonder bij de taakbalk, 352 punten breed, hoeken van 12, zachte schaduw.
- Opties onder elkaar met cijfers. Standaard vier (slaapstand, sluimerstand, opnieuw opstarten, afsluiten); vergrendelen, afmelden en gebruiker wisselen kun je aanzetten. In Instellingen > Menu kies je welke en sleep je de volgorde (`Settings.Order`, `Settings.Hidden`). Het rechtsklikmenu van het taakbalkicoon volgt dezelfde lijst.
- Venster is in elke stand even groot (kiezen, timer, aftellen, tijd kiezen).
- Timer: standaard Nu, + en − in stappen (standaard 15 min), vaste tijden 15 min, 30 min, 1 uur, 2 uur plus eigen vaste tijden via + (max 5). Geen "Wanneer"-label.
- Tijd kiezen met twee draaiwieltjes (uren, minuten), "Over" of "Om", scrollen, pijltjes of typen.
- Korte knop ("Afsluiten" of "Start timer") met Enter; erboven een regel die zegt wat er gebeurt.
- Aftellen bij Nu (standaard 10 seconden, instelbaar) zodat je nog kunt stoppen; groot icoon en grote naam van de actie. Optie "Cijfer of klik voert meteen uit" (`Settings.DirectRun`, standaard uit).
- Instellingen > Menu: per knop een pijltje dat korte uitleg uitklapt (`desc.*` in lang.py).
- Openen: Slim (taakbalk bij klik op het icoon, anders midden), bij de taakbalk, midden, of laatste plek (verslepen aan de bovenkant).
- Iconen: slaapstand een volle maan met zz, sluimerstand een sneeuwvlok (winterslaap), tandwiel voor instellingen.
- Thema volgt Windows. Achtergrond: Mica (vervaagd bureaublad), effen of schemer. Accent standaard lila; geen groen.
- Logo: concept 7 "Schemerring", een aan-uitknop met een ring die verloopt van oranje via roze naar paars, zonder sterretjes.

## Techniek
- C# 5 met WinForms op .NET Framework 4.8 (zit in Windows). Geen nieuwere C#-taal gebruiken: `build/build.sh` controleert dat met Mono (`-langversion:5`).
- Alles wordt zelf getekend. Het hoofdvenster is een gelaagd venster (UpdateLayeredWindow).
- Stroom (`src/Power.cs`): SetSuspendState voor slaap en sluimer, maar bij Modern Standby het scherm uit (NtPowerInformation ScreenOff, zoals het startmenu). Nooit `rundll32 powrprof.dll,SetSuspendState`. Afsluiten met ExitWindowsEx, hybride als Snel opstarten aan staat.
- Teksten in vier talen: pas `build/lang.py` aan en draai het; dat schrijft `src/Lang.cs`.
- Tekst: altijd via `Ui.Text`/`TextAt`/`Wrap` (witte GDI-tekst als masker, grijs gladgemaakt, ingekleurd; vanaf schaal 1,4 drie keer zo groot gezet en teruggeschaald, `Ui.SetScale`). Geen DrawString of TextRenderer los gebruiken.
- Gebruik en fouten: `Usage.Count("...")` schrijft tellers naar `%APPDATA%\Dusk\gebruik.txt`, fouten gaan naar `log.txt`. Alleen lokaal, uit te zetten in Over.
- Tests: `tests/Tests.cs`. Zelftest met schermafbeeldingen: `Dusk.exe --zelftest --map <map>`.

## GitHub
- Publieke repository `dusk`. GitHub Actions op `windows-latest`: bouwen, tests, zelftest, opstarttest, installer (Inno Setup, per gebruiker in %LOCALAPPDATA%\Programs\Dusk).
- Release gaat vanzelf bij een nieuw versienummer (`Version` in `src/Program.cs`). Werk dan ook `docs/RELEASE.md` bij en zet bovenaan `src/Changelog.cs` een blok voor de nieuwe versie (de test controleert dat).
- Bijwerken met een klik (`src/Update.cs`, zelfde aanpak als NexusPen): Dusk vraagt GitHub om de laatste Release, downloadt `Dusk-Setup.exe`, controleert de SHA-256 uit de asset-digest van GitHub en start de installer met `/SILENT ... /BIJWERKEN`; de installer start Dusk daarna weer.
