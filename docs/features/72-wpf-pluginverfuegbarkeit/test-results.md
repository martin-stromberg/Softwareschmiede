# Test-Ergebnisse

## Ergebnis

**Status:** Fehler vorhanden

## Fehlgeschlagene Tests

### Softwareschmiede.Tests.E2E.WpfE2ETests

- **EinstellungenOeffnen_ZeigtEinstellungsSeite_E2E** — System.TimeoutException: Element wurde nicht innerhalb von 10s gefunden
- **AufgabeAnlegen_ZeigtCliStartenButton_E2E** — System.TimeoutException: Element wurde nicht innerhalb von 10s gefunden
- **Dashboard_KeineRecoveryBanner_BeiSauberemStart_E2E** — System.TimeoutException: Element wurde nicht innerhalb von 10s gefunden
- **DarkModeAktivierenUndPersistieren_E2E** — System.Exception: Could not find process with id
- **EinstellungenNavigation_BleibtNachMehrerenKlicks_Stabil_E2E** — System.Exception: Could not find process with id
- **ProjektErstellen_ZeigtAufgabenListe_E2E** — System.Exception: Could not find process with id
- **EinstellungenArbeitsverzeichnis_Aendern_UndSpeichern_E2E** — System.Exception: Could not find process with id

### Softwareschmiede.Tests.E2E.ProjectDetailE2ETests

- **RepositoryZuweisenDialog_ScmPluginListe_EnthaeltErwartetePlugins_E2E** — System.Exception: Could not find process with id
- **ProjektNamenAendern_KachelAktualisiert_UndErneutoeffnen_E2E** — System.Exception: Could not find process with id
- **AufgabeNeuAnlegen_ErscheintInAufgabenliste_E2E** — System.Exception: Could not find process with id
- **ProjektBearbeitenUndSpeichern_AktualisierterNameBleibt_E2E** — System.Exception: Could not find process with id
- **AufgabenFiltern_OverlayOeffnetUndSchliesst_E2E** — System.Exception: Could not find process with id
- **RepositoryZuweisen_DialogOeffnetUndSchliessbarPerAbbrechen_E2E** — System.TimeoutException: Element wurde nicht innerhalb von 10s gefunden
- **ZurueckZurUebersicht_SchliesstOverlayUndZeigtListe_E2E** — System.TimeoutException: Element wurde nicht innerhalb von 10s gefunden

## Zusammenfassung

- Gesamt: 427
- Bestanden: 413
- Fehlgeschlagen: 14
- Übersprungen: 0

## Testabdeckung

**Abdeckung:**
- Softwareschmiede.Tests: 25.6% (4676 / 18231 Zeilen)
- Softwareschmiede.IntegrationTests: 71.5% (11217 / 15678 Zeilen)

| Modul | Abdeckung | Zeilen |
|-------|-----------|--------|
| Softwareschmiede.Tests | 25.6% | 4676 / 18231 |
| Softwareschmiede.IntegrationTests | 71.5% | 11217 / 15678 |

## Fehlende Tests

Quelle: `Coverage-Daten`

### WPF App & UI-Komponenten (0% Abdeckung)

- `src\Softwareschmiede.App\App.xaml.cs` — 0% Abdeckung
- `src\Softwareschmiede.App\Controls\ProcessWindowHost.cs` — 0% Abdeckung
- `src\Softwareschmiede.App\Controls\RecoveryBannerControl.xaml.cs` — 0% Abdeckung
- `src\Softwareschmiede.App\Controls\RibbonGroup.xaml.cs` — 0% Abdeckung
- `src\Softwareschmiede.App\Controls\RibbonLargeButton.xaml.cs` — 0% Abdeckung
- `src\Softwareschmiede.App\Controls\RibbonSmallButton.xaml.cs` — 0% Abdeckung
- `src\Softwareschmiede.App\Controls\StatusIndicatorControl.xaml.cs` — 0% Abdeckung
- `src\Softwareschmiede.App\Converters\AppConverters.cs` — 0% Abdeckung
- `src\Softwareschmiede.App\Services\DarkModeService.cs` — 0% Abdeckung
- `src\Softwareschmiede.App\Services\WpfAudioService.cs` — 0% Abdeckung
- `src\Softwareschmiede.App\Services\WpfBannerService.cs` — 0% Abdeckung
- `src\Softwareschmiede.App\Services\WpfDialogService.cs` — 0% Abdeckung
- `src\Softwareschmiede.App\ViewModels\DashboardViewModel.cs` — 0% Abdeckung
- `src\Softwareschmiede.App\ViewModels\MainWindowViewModel.cs` — 0% Abdeckung
- `src\Softwareschmiede.App\ViewModels\NavigationViewModel.cs` — 0% Abdeckung
- `src\Softwareschmiede.App\ViewModels\PluginSettingsViewModel.cs` — 0% Abdeckung
- `src\Softwareschmiede.App\ViewModels\ProjectDetailViewModel.cs` — 0% Abdeckung
- `src\Softwareschmiede.App\ViewModels\ProjectListViewModel.cs` — 0% Abdeckung
- `src\Softwareschmiede.App\ViewModels\SettingsViewModel.cs` — 0% Abdeckung
- `src\Softwareschmiede.App\ViewModels\TaskDetailViewModel.cs` — 0% Abdeckung
- `src\Softwareschmiede.App\ViewModels\TaskListViewModel.cs` — 0% Abdeckung
- `src\Softwareschmiede.App\ViewModels\ViewModelBase.cs` — 0% Abdeckung

### Plugin-Implementierungen (0% Abdeckung)

- `plugins\Softwareschmiede.Plugin.ClaudeCli\ClaudeCliPlugin.cs` — 0% Abdeckung
- `plugins\Softwareschmiede.Plugin.GitHubCopilot\GitHubCopilotPlugin.cs` — 0% Abdeckung
- `plugins\Softwareschmiede.Plugin.LocalDirectory\LocalDirectoryPlugin.cs` — 0% Abdeckung

### Weitere Service-Klassen (0% Abdeckung)

- `src\Softwareschmiede.App\Views\DashboardView.xaml.cs` — 0% Abdeckung
- `src\Softwareschmiede.App\Views\MainWindow.xaml.cs` — 0% Abdeckung
- `src\Softwareschmiede.App\Views\PluginSettingsView.xaml.cs` — 0% Abdeckung
- `src\Softwareschmiede.App\Views\ProjectDetailView.xaml.cs` — 0% Abdeckung
- `src\Softwareschmiede.App\Views\ProjectListView.xaml.cs` — 0% Abdeckung
- `src\Softwareschmiede.App\Views\RepositoryAssignDialog.xaml.cs` — 0% Abdeckung
- `src\Softwareschmiede.App\Views\SettingsView.xaml.cs` — 0% Abdeckung
- `src\Softwareschmiede.App\Views\TaskDetailView.xaml.cs` — 0% Abdeckung
- `src\Softwareschmiede.App\Views\TaskListView.xaml.cs` — 0% Abdeckung

**Gesamt: 109 Dateien mit 0% Abdeckung** (Generierte Dateien, XAML und Migrations ausgeschlossen)
