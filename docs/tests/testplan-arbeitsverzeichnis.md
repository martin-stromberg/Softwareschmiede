# Testplan – Konfigurierbares Arbeitsverzeichnis für lokale Repositories

## Ziel
Nachweis der Testabdeckung für das Feature „konfigurierbares Arbeitsverzeichnis“.

## Abgedeckte Testbereiche

1. **Service- und Prozesslogik**
   - `ArbeitsverzeichnisSettingsServiceTests`: Validierungs- und Fehlerpfade.
   - `EntwicklungsprozessServiceTests`: Branch-Checkout, Zielverzeichnis-Löschung, fehlendes Agentenpaket.
2. **UI-Code-Behind**
   - `EinstellungenBaseArbeitsverzeichnisTests`: Laden, Validierung/Speichern, Zurücksetzen.
3. **Migration**
   - `WorkdirMigrationTests`: `Up`/`Down`-Verifikation der Tabelle `AppEinstellungen`.
4. **DI-Registrierung**
   - Die Registrierung ist dokumentiert; dedizierter automatisierter DI-Smoke-Test ist optional.

## Validierungskriterien

- `dotnet test src/Softwareschmiede.Tests` läuft erfolgreich.
- `dotnet test src/Softwareschmiede.IntegrationTests` läuft erfolgreich.
- Die oben genannten Feature-Pfade sind durch Unit-/Integrationstests abgedeckt.
