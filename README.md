# Dusk

Een kleine, lichte app voor Windows 11 om je pc af te sluiten, in slaapstand of sluimerstand te zetten of opnieuw op te starten. Met een sneltoets en je pijltjestoetsen, en een timer als je wilt.

![Dusk](assets/Dusk-256.png)

## Downloaden

Kijk bij **Releases** en kies `Dusk-Setup.exe`. De installer heeft geen beheerdersrechten nodig.

## Gebruik

- **Openen:** `Win + Shift + Q`, of klik op het icoon rechtsonder bij de klok.
- **Kiezen:** pijltjes omhoog en omlaag, of de cijfers 1 tot 4.
- **Wanneer:** standaard *Nu*. Met `+` en `−` gaat er 15 minuten bij of af, met links en rechts kies je een vaste tijd, en met `Tab` typ je zelf iets: `30`, `1,5u` of een tijdstip als `23:30`.
- **Uitvoeren:** `Enter` of de knop onderaan. Bij *Nu* heb je nog 5 seconden om met `Esc` te stoppen.
- **Timer loopt:** het icoon in de taakbalk dooft langzaam uit. Een minuut van tevoren krijg je een melding met *+15 min* en *Annuleren*.
- **Instellingen:** het tandwiel, `Ctrl + ,` of rechtsklik op het icoon. Taal (Nederlands, English, Español, Français), licht of donker, achtergrond (Mica, effen, schemer), accentkleur, sneltoets en meer.

## Goed om te weten

- Afsluiten gaat netjes: programma's met werk dat nog niet is opgeslagen mogen eerst vragen of je wilt opslaan. Geforceerd afsluiten kan in Instellingen.
- Slaapt je pc terwijl er een timer loopt en is de tijd voorbij als hij wakker wordt, dan voert Dusk de actie niet alsnog uit. Hij stopt de timer en meldt dat.
- Pc's met *Modern Standby* (veel laptops) gaan in slaapstand zoals het startmenu het doet.
- Dusk werkt helemaal offline. Instellingen en een klein logboek staan in `%APPDATA%\Dusk`.

## Zelf bouwen

Op Windows, zonder iets te installeren (de C#-compiler van .NET Framework 4.8 zit in Windows):

```
powershell -ExecutionPolicy Bypass -File build\build.ps1
```

GitHub Actions bouwt bij elke push, draait de tests, maakt schermafbeeldingen van alle schermen (tak `zelftest-schermen`) en de installer. Staat er een nieuw versienummer in `src/Program.cs`, dan komt er vanzelf een Release.

## Licentie

MIT, zie `LICENSE`.
