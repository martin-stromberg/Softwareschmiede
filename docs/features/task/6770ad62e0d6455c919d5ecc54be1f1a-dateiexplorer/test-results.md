# Test-Ergebnisse

## Ergebnis

**Status:** Fehler vorhanden

## Fehlgeschlagene Tests

### E2E-Tests (4 Fehlschläge)

- **Softwareschmiede.Tests.E2E.WpfE2ETests.ProjektErstellen_UndNeueAufgabeAnlegen_E2E** — TimeoutException: Element wurde nicht gefunden (25,22 s)
- **Softwareschmiede.Tests.E2E.ProjectDetailE2ETests.NeuanlageAbbrechen_ErstesProjektNochAufrufbar_E2E** — TimeoutException: Element wurde nicht gefunden (2:07,20 min)
- **Softwareschmiede.Tests.E2E.E2E_TaskWechselUeberMenue.AufgabeWechselUeberSeitenleiste_ZeigtNeueAufgabeMitEigenerCli_E2E** — TimeoutException: Element wurde nicht gefunden (3:32,71 min)
- **Softwareschmiede.Tests.E2E.E2E_CreateNewTaskNavigation.NeueAufgabeErstellenUndSpeichern_ErscheintInListeUndNavigiertZuruec** — TimeoutException: Element wurde nicht gefunden (5:37,52 min)

## Zusammenfassung

- Gesamt: 921
- Bestanden: 916
- Fehlgeschlagen: 4
- Übersprungen: 1

## Testabdeckung

**Abdeckung:** 33 % Gesamtzeilenabdeckung (9.221 von 28.143 Zeilen getestet)

| Paket | Abdeckung |
|-------|-----------|
| Softwareschmiede.App | 56 % |
| Softwareschmiede.Plugin.Contracts | 58 % |
| Softwareschmiede.Plugin.BitBucket | 66 % |
| Softwareschmiede | 72 % |
| Softwareschmiede.Plugin.GitHub | 86 % |
| Softwareschmiede.Infrastructure | 87 % |
| Softwareschmiede.IntegrationTests | 93 % |
| Softwareschmiede.Plugin.LocalDirectory | 98 % |
| Softwareschmiede.Tests | 100 % |
| Softwareschmiede.Plugin.Contracts.Impl | 100 % |

## Fehlende Tests

**Quelle:** Coverage-Daten

Dateien mit 0 % Zeilenabdeckung (160 insgesamt). Top 20:

- `plugins/Softwareschmiede.Plugin.BitBucket/BitBucketPlugin.cs` — 0 % Abdeckung
- `plugins/Softwareschmiede.Plugin.ClaudeCli/ClaudeCliPlugin.cs` — 0 % Abdeckung
- `plugins/Softwareschmiede.Plugin.Codex/CodexPlugin.cs` — 0 % Abdeckung
- `plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.cs` — 0 % Abdeckung
- `plugins/Softwareschmiede.Plugin.GitHubCopilot/GitHubCopilotPlugin.cs` — 0 % Abdeckung
- `plugins/Softwareschmiede.Plugin.LocalDirectory/LocalDirectoryPlugin.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/Behaviors/StatusUebergangsAnimation.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/Controls/RibbonButtonBase.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/Converters/AppConverters.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/Services/DarkModeService.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/Services/PluginSelectionDialogService.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/Services/WpfAudioService.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/Services/WpfBannerService.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/Services/WpfDialogService.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/ViewModels/FileExplorerViewModel.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/ViewModels/PluginSelectionDialogViewModel.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/ViewModels/ProjectListViewModel.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/ViewModels/SettingsViewModel.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/ViewModels/ViewModelBase.cs` — 0 % Abdeckung

**Hinweis:** Die 160 Dateien mit 0% Abdeckung sind hauptsächlich UI-Layer-Komponenten (ViewModels, Services, Controls, Behaviors) und Plugin-Implementierungen. Diese werden typischerweise durch E2E-Tests statt Unit-Tests getestet. Die 4 fehlgeschlagenen Tests sind alle E2E-Tests, was darauf hindeutet, dass die Sandbox-Umgebung UI-Automatisierungstests beeinflusst.
