# Testlücken – Feature „Konfigurierbares Arbeitsverzeichnis für lokale Repositories“

## Identifizierte Lücken (Stand Analyse)

- [x] `ArbeitsverzeichnisSettingsService.ValidatePathForSave`
  - [x] Ungültige Zeichen im Pfad
  - [x] Fehlerpfad bei `Directory.CreateDirectory(...)`
- [x] `EntwicklungsprozessService.ProzessStartenAsync`
  - [x] Checkout eines vorhandenen Nicht-Default-Branches
  - [x] Löschen eines bereits existierenden Zielverzeichnisses vor Klonen
  - [x] Verhalten bei fehlendem Agentenpaket
- [x] Einstellungen-UI (`Einstellungen.razor.cs`)
  - [x] Initiales Laden inkl. Fallback-Hinweis
  - [x] Speichern mit Validierungsfehler
  - [x] Zurücksetzen auf Default
- [x] EF-Migration `202605090001_Add_AppEinstellung_Workdir`
  - [x] Rollback (`Down`) und erneute Anwendung (`Up`)

## Offene Restpunkte

- [ ] Optional: dedizierter DI-Smoketest für `Program.cs`-Registrierungen.
- [x] Für die Feature-Abnahme keine funktionalen Testlücken mehr offen.
