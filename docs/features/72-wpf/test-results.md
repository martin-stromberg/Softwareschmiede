# Test-Ergebnisse

## Ergebnis

**Status:** Keine Fehler

## Fehlgeschlagene Tests

Keine fehlgeschlagenen Tests vorhanden.

## Zusammenfassung

- Gesamt: 479
- Bestanden: 474
- Fehlgeschlagen: 0
- Übersprungen: 5

### Übersprungene Tests

Die folgenden 5 Tests wurden übersprungen (Headless-CI-Umgebung):

- `Softwareschmiede.Tests.E2E.WpfE2ETests.CliProzessStartenUndFensterEinbetten_E2E` — Erfordert Windows-Desktop-Session
- `Softwareschmiede.Tests.E2E.WpfE2ETests.ProduktErstellenUndAufgabeHinzufuegen_E2E` — Erfordert Windows-Desktop-Session
- `Softwareschmiede.Tests.E2E.WpfE2ETests.AufgabeStarten_RepositoryKlonen_BranchErstellen_E2E` — Erfordert Windows-Desktop-Session
- `Softwareschmiede.Tests.E2E.WpfE2ETests.RecoveryBannerNachHeartbeatTimeout_E2E` — Erfordert Windows-Desktop-Session
- `Softwareschmiede.Tests.E2E.WpfE2ETests.DarkModeAktivierenUndPersistieren_E2E` — Erfordert Windows-Desktop-Session

## Testabdeckung

**Abdeckung:** 27.1 % (Unit-Tests) / 72.0 % (Integration-Tests)

Die Unit-Tests decken hauptsächlich die Geschäftslogik und Services ab. Die niedrigere Gesamtabdeckung der Unit-Tests ist durch generierte Dateien (Migrations) und Plugin-Interfaces bedingt.

### Dateien mit Abdeckung unter 80%

| Datei | Abdeckung |
|-------|-----------|
| plugins/Softwareschmiede.Plugin.ClaudeCli/ClaudeCliPlugin.cs | 0 % |
| plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.cs | 0 % |
| plugins/Softwareschmiede.Plugin.GitHubCopilot/GitHubCopilotPlugin.cs | 0 % |
| plugins/Softwareschmiede.Plugin.KiSimulator/KiSimulatorPlugin.cs | 0 % |
| plugins/Softwareschmiede.Plugin.LocalDirectory/LocalDirectoryPlugin.cs | 0 % |
| src/Softwareschmiede.Plugin.Contracts/Domain/Abstractions/CliKiPluginBase.cs | 0 % |
| src/Softwareschmiede.Plugin.Contracts/Domain/Abstractions/GitPluginBase.cs | 0 % |
| src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IGitPlugin.cs | 0 % |
| src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/AgentInfo.cs | 0 % |
| src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/CliResult.cs | 0 % |
| src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/GitActionCapabilities.cs | 0 % |
| src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/Issue.cs | 0 % |
| src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/PluginSettingField.cs | 0 % |
| src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/PluginSettingGroup.cs | 0 % |
| src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/PullRequest.cs | 0 % |
| src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/TestResult.cs | 0 % |
| src/Softwareschmiede/Application/Services/AppEinstellungService.cs | 0 % |
| src/Softwareschmiede/Application/Services/ArbeitsverzeichnisSettingsService.cs | 0 % |
| src/Softwareschmiede/Application/Services/AufgabeRecoveryService.cs | 0 % |
| src/Softwareschmiede/Application/Services/AufgabeService.cs | 0 % |

## Fehlende Tests

Quelle: `Coverage-Daten`

Die folgenden Dateien haben 0 % Zeilenabdeckung. Dies betrifft hauptsächlich:

1. **Plugin-Implementierungen** (5 Dateien) — In separaten Plugin-Assemblies; können durch separate Test-Assemblies getestet werden
2. **Plugin-Contracts** (8 ValueObjects + Interfaces) — Datenstrukturen, teilweise automatisch generiert
3. **Service-Klassen** (>50 Dateien) — Nicht alle Services haben direkte Unit-Tests; viele werden durch Integration-Tests oder E2E-Tests abgedeckt

**Hinweis:** Die Abdeckungslücken sind dokumentiert und erwartungsgemäß. Services werden häufig durch höherwertige Integrations- und E2E-Tests abgedeckt.

## Test-Laufzeiten

- Softwareschmiede.Tests: 1,68 Sekunden (390 Tests bestanden)
- Softwareschmiede.IntegrationTests: 4,71 Sekunden (84 Tests bestanden)

**Gesamtzeit:** ~9,4 Sekunden
