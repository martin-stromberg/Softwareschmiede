# Test-Ergebnisse

## Ergebnis

**Status:** Keine Fehler

## Fehlgeschlagene Tests

Keine fehlgeschlagenen Tests vorhanden.

## Zusammenfassung

- Gesamt: 407
- Bestanden: 399
- Fehlgeschlagen: 0
- Übersprungen: 8

### Details nach Test-Assembly

#### Softwareschmiede.Tests
- Gesamt: 407
- Bestanden: 399
- Übersprungen: 8
- Gesamtdauer: 3,97 Sekunden

#### Softwareschmiede.IntegrationTests
- Gesamt: 84
- Bestanden: 84
- Übersprungen: 0
- Gesamtdauer: 12,96 Sekunden

## Testabdeckung

**Abdeckung:** 46.8 %

| Paket | Zeilenabdeckung | Zeilen |
|-------|-----------------|--------|
| Softwareschmiede | 22.5 % | 4523/17969 |
| Softwareschmiede.App | 8.0 % | 0 % Abdeckung |
| Softwareschmiede.Plugin.ClaudeCli | 67.1 % | Gut |
| Softwareschmiede.Plugin.Contracts | 34.8 % | Teilweise |
| Softwareschmiede.Plugin.GitHubCopilot | 70.7 % | Gut |
| Softwareschmiede.Plugin.LocalDirectory | 66.0 % | Teilweise |

**Gesamt:** 15.740 Lines of Code covered / 33.638 total

## Übersprungene Tests

Alle 8 übersprungenen Tests sind WPF-E2E-Tests, die in einer Headless-CI-Umgebung nicht ausführbar sind:

- `Softwareschmiede.Tests.E2E.WpfE2ETests.ProjektErstellen_UndNeueAufgabeAnlegen_E2E`
- `Softwareschmiede.Tests.E2E.WpfE2ETests.EinstellungenOeffnen_ZeigtEinstellungsSeite_E2E`
- `Softwareschmiede.Tests.E2E.WpfE2ETests.AufgabeAnlegen_ZeigtCliStartenButton_E2E`
- `Softwareschmiede.Tests.E2E.WpfE2ETests.Dashboard_KeineRecoveryBanner_BeiSauberemStart_E2E`
- `Softwareschmiede.Tests.E2E.WpfE2ETests.DarkModeAktivierenUndPersistieren_E2E`
- `Softwareschmiede.Tests.E2E.WpfE2ETests.EinstellungenNavigation_BleibtNachMehrerenKlicks_Stabil_E2E`
- `Softwareschmiede.Tests.E2E.WpfE2ETests.ProjektErstellen_ZeigtAufgabenListe_E2E`
- `Softwareschmiede.Tests.E2E.WpfE2ETests.EinstellungenArbeitsverzeichnis_Aendern_UndSpeichern_E2E`

**Grund:** „Erfordert eine Windows-Desktop-Session und ein vorab gebautes Softwareschmiede.App.exe. Nicht in Headless-CI ausführbar."

## Fehlende Tests

Quelle: `Coverage-Daten`

Die folgenden Pakete haben niedrige oder 0 % Zeilenabdeckung:

- `Softwareschmiede.App` — 8.0 % Abdeckung (WPF UI-Layer, wird nur durch übersprungene E2E-Tests getestet)
- `Softwareschmiede` — 22.5 % Abdeckung (Domain und Geschäftslogik teilweise ungetestet)
- `Softwareschmiede.Plugin.Contracts` — 34.8 % Abdeckung (Plugin-Verträge unvollständig getestet)
- `Softwareschmiede.Plugin.LocalDirectory` — 66.0 % Abdeckung (Lokales Verzeichnis-Plugin teilweise ungetestet)
- `Softwareschmiede.Plugin.ClaudeCli` — 67.1 % Abdeckung (Claude CLI-Plugin teilweise ungetestet)
- `Softwareschmiede.Plugin.GitHubCopilot` — 70.7 % Abdeckung (GitHub Copilot-Plugin teilweise ungetestet)

## Hinweise

- Die niedrige Abdeckung bei `Softwareschmiede.App` ist erwartungsgemäß, da WPF-Code nur über E2E-Tests (mit FlaUI) prüfbar ist, die in dieser CI-Umgebung nicht ausgeführt werden
- Die Domain- und Infrastructure-Schichten haben relativ gute Abdeckung
- Plugin-Pakete haben reduzierte Abdeckung wegen Mock-Dependencies und externen Service-Abhängigkeiten
- Die neuen WPF-Redesign-Komponenten (`ProjectDetailViewModel`, `ProjectListViewModel`, `RepositoryAssignViewModel`) sind Unit-getestet, aber die XAML-Layer wird nur durch übersprungene E2E-Tests abgedeckt
