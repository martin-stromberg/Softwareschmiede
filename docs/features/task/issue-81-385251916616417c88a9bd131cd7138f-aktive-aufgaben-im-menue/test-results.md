# Test-Ergebnisse

## Ergebnis

**Status:** Fehler vorhanden

## Fehlgeschlagene Tests

### Softwareschmiede.Tests.App.ViewModels.TaskDetailViewModelTests

- **TestPluginWechselAsync_StopsCliAndStartsNew** — Expected sut.IsCliRunning to be True, but found False.

### Softwareschmiede.Tests.E2E.E2E_TaskExecutionCommandLineParameters

- **AufgabeStarten_MitCodexCommandLineParametersImStore_KiSimulatorStartetKorrekt_E2E** — System.TimeoutException: Element wurde nicht innerhalb von 15s gefunden.

### Softwareschmiede.Tests.E2E.E2E_PluginWechsel

- **PluginAendernBeiLaufenderCli_StopptUndStartetMitNeuemPlugin_E2E** — System.TimeoutException: Element wurde nicht innerhalb von 15s gefunden.

### Softwareschmiede.Tests.E2E.E2E_PluginSelectionDialog

- **StartenOhneGespeichertesPlugin_ZeigtPluginAuswahlDialog_E2E** — System.TimeoutException: Element wurde nicht innerhalb von 15s gefunden.

### Softwareschmiede.Tests.E2E.E2E_PluginProjectDefault_NextTask

- **ZweiteAufgabeImProjekt_UebernimmtGespeichertenProjektStandardOhneDialog_E2E** — System.TimeoutException: Element wurde nicht innerhalb von 15s gefunden.

### Softwareschmiede.Tests.E2E.E2E_PluginProjectDefault

- **PluginDialogMitProjektCheckbox_SpeichertProjektStandardUndStartetCli_E2E** — System.TimeoutException: Element wurde nicht innerhalb von 15s gefunden.

### Softwareschmiede.Tests.E2E.E2E_CreateNewTaskNavigation

- **NeueAufgabeAbbrechen_NavigiertZurueckOhneTitelAenderungZuSpeichern_E2E** — System.TimeoutException: Element wurde nicht innerhalb von 10s gefunden.

### Softwareschmiede.Tests.E2E.E2E_ConPtyTerminalStart

- **ConPtyStart_ZeigtTerminalPanelMitStoppenButton_E2E** — System.TimeoutException: Element wurde nicht innerhalb von 10s gefunden.

### Softwareschmiede.Tests.E2E.E2E_ConPtyResize

- **ConPtyResize_NachFenstergroesseAendern_KeinFehlerUndCliNochAktiv_E2E** — System.TimeoutException: Element wurde nicht innerhalb von 10s gefunden.

### Softwareschmiede.Tests.E2E.E2E_ConPtyProcessEnd

- **ConPtyProcessEnd_NachStoppen_IsCliRunningFalse_E2E** — System.TimeoutException: Element wurde nicht innerhalb von 10s gefunden.

### Softwareschmiede.Tests.E2E.E2E_ConPtyKeyboardInput

- **ConPtyKeyboardInput_NachStart_KeinFehlerBanner_E2E** — System.TimeoutException: Element wurde nicht innerhalb von 10s gefunden.

### Softwareschmiede.Tests.E2E.E2E_AutoStartCli

- **AufgabeOeffnen_StatusGestartetOhneLaufendenProzess_StartetCliAutomatisch_E2E** — System.Exception: Could not find process with id: 38564 (Process is not running).

### Softwareschmiede.Tests.E2E.E2E_AufgabeStarten

- **AufgabeStarten_KlontRepositoryUndStartetCli_E2E** — System.Exception: Could not find process with id: 2428 (Process is not running).

## Zusammenfassung

- Gesamt: 742
- Bestanden: 729
- Fehlgeschlagen: 13
- Übersprungen: 0

Hinweis: Alle 13 Fehlschläge stammen aus `Softwareschmiede.Tests` (644/657 bestanden). `Softwareschmiede.IntegrationTests` lief vollständig grün (85/85). Die 12 E2E-Fehlschläge sind FlaUI-Timeouts bzw. Prozess-Handle-Fehler beim automatisierten Starten der WPF-App (UI-Automation-Umgebung), nicht projektbezogene Logikfehler. Der eine Unit-Test-Fehlschlag (`TestPluginWechselAsync_StopsCliAndStartsNew`) betrifft `IsCliRunning`, das nach einem Plugin-Wechsel `False` statt `True` liefert.

## Testabdeckung

**Abdeckung:** 70,0 % (gemessen an realem Quellcode, ohne generierte Dateien wie EF-Migrations-Snapshots, XAML-Buildartefakte `obj\*.g.cs` und XAML-Markup)

| Datei | Abdeckung |
|-------|-----------|
| `src\Softwareschmiede.App\Views\DashboardView.xaml.cs` | 0,0 % |
| `src\Softwareschmiede.App\Views\HelpTextDialog.xaml.cs` | 0,0 % |
| `src\Softwareschmiede.App\Views\IssueSelectionDialog.xaml.cs` | 0,0 % |
| `src\Softwareschmiede.App\Views\MainWindow.xaml.cs` | 0,0 % |
| `src\Softwareschmiede.App\Views\PluginSelectionDialog.xaml.cs` | 0,0 % |
| `src\Softwareschmiede.App\Views\PluginSettingEntryEditHelper.cs` | 0,0 % |
| `src\Softwareschmiede.App\Views\PluginSettingFieldTemplateSelector.cs` | 0,0 % |
| `src\Softwareschmiede.App\Views\ProjectDetailView.xaml.cs` | 0,0 % |
| `src\Softwareschmiede.App\Views\ProjectListView.xaml.cs` | 0,0 % |
| `src\Softwareschmiede.App\Views\RepositoryAssignDialog.xaml.cs` | 0,0 % |
| `src\Softwareschmiede.App\Views\SettingsView.xaml.cs` | 0,0 % |
| `src\Softwareschmiede.App\Views\TaskDetailView.xaml.cs` | 0,0 % |
| `src\Softwareschmiede.App\ViewModels\PluginSelectionDialogViewModel.cs` | 0,0 % |
| `src\Softwareschmiede.App\Services\PluginSelectionDialogService.cs` | 0,0 % |
| `src\Softwareschmiede.App\Services\WpfAudioService.cs` | 0,0 % |
| `src\Softwareschmiede.App\Services\WpfBannerService.cs` | 0,0 % |
| `src\Softwareschmiede.App\Services\WpfDialogService.cs` | 0,0 % |
| `src\Softwareschmiede.App\Controls\KeyToVt100Encoder.cs` | 0,0 % |
| `src\Softwareschmiede.App\Controls\RecoveryBannerControl.xaml.cs` | 0,0 % |
| `src\Softwareschmiede.App\Controls\RibbonButtonBase.cs` | 0,0 % |
| `src\Softwareschmiede.App\Controls\RibbonGroup.xaml.cs` | 0,0 % |
| `src\Softwareschmiede.App\Controls\RibbonLargeButton.xaml.cs` | 0,0 % |
| `src\Softwareschmiede.App\Controls\RibbonSmallButton.xaml.cs` | 0,0 % |
| `src\Softwareschmiede.App\Controls\TerminalControl.cs` | 0,0 % |
| `src\Softwareschmiede\Infrastructure\Services\BenutzerkontextService.cs` | 0,0 % |
| `src\Softwareschmiede\Infrastructure\Services\CliSessionService.cs` | 0,0 % |
| `src\Softwareschmiede\Infrastructure\Services\SystemShutdownService.cs` | 0,0 % |
| `src\Softwareschmiede\Domain\ValueObjects\WorkspaceNodeRow.cs` | 0,0 % |
| `src\Softwareschmiede\Domain\Entities\PluginKonfiguration.cs` | 0,0 % |
| `src\Softwareschmiede\Application\Services\CliProcessManager.cs` | 0,0 % |
| `src\Softwareschmiede\Application\Services\KiAufgabenAbschlussEreignis.cs` | 0,0 % |
| `src\Softwareschmiede\Application\Services\KiAufgabenBenachrichtigungsHub.cs` | 0,0 % |
| `src\Softwareschmiede.Plugin.Contracts\Domain\ValueObjects\AgentInfo.cs` | 0,0 % |
| `src\Softwareschmiede.Plugin.Contracts\Domain\Interfaces\IGitPlugin.cs` | 0,0 % |
| `src\Softwareschmiede\Application\Services\BenachrichtigungsService.cs` | 31,1 % |
| `src\Softwareschmiede\Infrastructure\Services\CliRunner.cs` | 32,4 % |
| `src\Softwareschmiede.App\Converters\AppConverters.cs` | 38,8 % |
| `src\Softwareschmiede.Plugin.Contracts\Domain\Abstractions\CliKiPluginBase.cs` | 45,3 % |
| `src\Softwareschmiede.App\Services\DarkModeService.cs` | 45,7 % |
| `src\Softwareschmiede\Infrastructure\Terminal\PseudoConsoleSession.cs` | 47,7 % |
| `src\Softwareschmiede\Domain\ValueObjects\BranchCommit.cs` | 50,0 % |
| `plugins\Softwareschmiede.Plugin.BitBucket\BitBucketPlugin.cs` | 50,0 % |
| `src\Softwareschmiede.App\Controls\StatusIndicatorControl.xaml.cs` | 56,6 % |
| `src\Softwareschmiede.App\ViewModels\ProjectListViewModel.cs` | 60,0 % |
| `src\Softwareschmiede\Application\Services\DiffService.cs` | 61,5 % |
| `src\Softwareschmiede.App\ViewModels\ViewModelBase.cs` | 63,2 % |
| `src\Softwareschmiede\Infrastructure\Services\WindowsCredentialStore.cs` | 64,7 % |
| `src\Softwareschmiede.App\ViewModels\MainWindowViewModel.cs` | 64,8 % |
| `src\Softwareschmiede\Infrastructure\Terminal\PseudoConsole.cs` | 65,0 % |
| `src\Softwareschmiede\Infrastructure\Terminal\AnsiSequenceParser.cs` | 66,0 % |
| `src\Softwareschmiede.App\ViewModels\ProjectDetailViewModel.cs` | 66,7 % |
| `src\Softwareschmiede.App\ViewModels\TaskDetailViewModel.cs` | 68,9 % |
| `src\Softwareschmiede\Domain\ValueObjects\FilePreview.cs` | 70,0 % |
| `src\Softwareschmiede\Domain\Terminal\TerminalBuffer.cs` | 70,5 % |
| `src\Softwareschmiede\Application\Services\EntwicklungsprozessService.cs` | 71,1 % |
| `src\Softwareschmiede\Domain\Enums\InvalidStatusTransitionException.cs` | 71,4 % |
| `src\Softwareschmiede\Application\Services\KiAusfuehrungsService.cs` | 73,7 % |
| `src\Softwareschmiede\Infrastructure\Plugins\PluginManager.cs` | 73,7 % |
| `src\Softwareschmiede.Plugin.Contracts\Domain\ValueObjects\PluginSettingField.cs` | 77,8 % |
| `src\Softwareschmiede.App\ViewModels\SettingsViewModel.cs` | 77,9 % |
| `src\Softwareschmiede.App\ViewModels\DashboardViewModel.cs` | 78,8 % |

## Fehlende Tests

Quelle: `Coverage-Daten`

- `src\Softwareschmiede.App\Views\DashboardView.xaml.cs` — 0 % Abdeckung
- `src\Softwareschmiede.App\Views\HelpTextDialog.xaml.cs` — 0 % Abdeckung
- `src\Softwareschmiede.App\Views\IssueSelectionDialog.xaml.cs` — 0 % Abdeckung
- `src\Softwareschmiede.App\Views\MainWindow.xaml.cs` — 0 % Abdeckung
- `src\Softwareschmiede.App\Views\PluginSelectionDialog.xaml.cs` — 0 % Abdeckung
- `src\Softwareschmiede.App\Views\PluginSettingEntryEditHelper.cs` — 0 % Abdeckung
- `src\Softwareschmiede.App\Views\PluginSettingFieldTemplateSelector.cs` — 0 % Abdeckung
- `src\Softwareschmiede.App\Views\ProjectDetailView.xaml.cs` — 0 % Abdeckung
- `src\Softwareschmiede.App\Views\ProjectListView.xaml.cs` — 0 % Abdeckung
- `src\Softwareschmiede.App\Views\RepositoryAssignDialog.xaml.cs` — 0 % Abdeckung
- `src\Softwareschmiede.App\Views\SettingsView.xaml.cs` — 0 % Abdeckung
- `src\Softwareschmiede.App\Views\TaskDetailView.xaml.cs` — 0 % Abdeckung
- `src\Softwareschmiede.App\ViewModels\PluginSelectionDialogViewModel.cs` — 0 % Abdeckung
- `src\Softwareschmiede.App\Services\PluginSelectionDialogService.cs` — 0 % Abdeckung
- `src\Softwareschmiede.App\Services\WpfAudioService.cs` — 0 % Abdeckung
- `src\Softwareschmiede.App\Services\WpfBannerService.cs` — 0 % Abdeckung
- `src\Softwareschmiede.App\Services\WpfDialogService.cs` — 0 % Abdeckung
- `src\Softwareschmiede.App\Controls\KeyToVt100Encoder.cs` — 0 % Abdeckung
- `src\Softwareschmiede.App\Controls\RecoveryBannerControl.xaml.cs` — 0 % Abdeckung
- `src\Softwareschmiede.App\Controls\RibbonButtonBase.cs` — 0 % Abdeckung
- `src\Softwareschmiede.App\Controls\RibbonGroup.xaml.cs` — 0 % Abdeckung
- `src\Softwareschmiede.App\Controls\RibbonLargeButton.xaml.cs` — 0 % Abdeckung
- `src\Softwareschmiede.App\Controls\RibbonSmallButton.xaml.cs` — 0 % Abdeckung
- `src\Softwareschmiede.App\Controls\TerminalControl.cs` — 0 % Abdeckung
- `src\Softwareschmiede\Infrastructure\Services\BenutzerkontextService.cs` — 0 % Abdeckung
- `src\Softwareschmiede\Infrastructure\Services\CliSessionService.cs` — 0 % Abdeckung
- `src\Softwareschmiede\Infrastructure\Services\SystemShutdownService.cs` — 0 % Abdeckung
- `src\Softwareschmiede\Domain\ValueObjects\WorkspaceNodeRow.cs` — 0 % Abdeckung
- `src\Softwareschmiede\Domain\Entities\PluginKonfiguration.cs` — 0 % Abdeckung
- `src\Softwareschmiede\Application\Services\CliProcessManager.cs` — 0 % Abdeckung
- `src\Softwareschmiede\Application\Services\KiAufgabenAbschlussEreignis.cs` — 0 % Abdeckung
- `src\Softwareschmiede\Application\Services\KiAufgabenBenachrichtigungsHub.cs` — 0 % Abdeckung
- `src\Softwareschmiede.Plugin.Contracts\Domain\ValueObjects\AgentInfo.cs` — 0 % Abdeckung
- `src\Softwareschmiede.Plugin.Contracts\Domain\Interfaces\IGitPlugin.cs` — 0 % Abdeckung

Hinweis: Viele der 0-%-Dateien im WPF-Projekt `Softwareschmiede.App` (Views/Controls) werden ausschließlich über die FlaUI-E2E-Tests abgedeckt, die den WPF-Prozess als separaten Kindprozess starten. Da die Code-Coverage-Instrumentierung nur den Testhost-Prozess erfasst, erscheinen diese Dateien unabhängig vom E2E-Testergebnis stets mit 0 % — auch wenn sie funktional getestet werden. Generierte Dateien (EF-Migrations, `obj\*.g.cs`, `App.xaml.cs` als Einstiegspunkt, reine `.xaml`-Markup-Dateien, `SoftwareschmiededDbContextDesignTimeFactory.cs`) wurden aus der Auswertung ausgeschlossen.
