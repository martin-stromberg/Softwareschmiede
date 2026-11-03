# Test-Ergebnisse

## Ergebnis

**Status:** Fehler vorhanden

Ausgeführte Lane: `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category!=OsInterface"` (nach `dotnet build Softwareschmiede.slnx`, erfolgreich, 0 Warnungen / 0 Fehler). Gesamtzeit: 3,1 Minuten. Coverage über `--collect:"XPlat Code Coverage"` erhoben.

Hinweis: Der einzige fehlgeschlagene Test ist ein Timing-Problem (Wartebedingung `WarteBisAsync` lief in ein 15-s-Timeout). Ein isolierter Wiederholungslauf desselben Tests war erfolgreich (282 ms) — vermutlich flaky unter Last, kein reproduzierbarer Produktfehler.

## Fehlgeschlagene Tests

### Softwareschmiede.Tests.App.ViewModels.KonsolenTestViewModelTests

- **Wiedergabe_PausierenUndFortsetzen** — `WarteBisAsync`-Timeout: „Expected DateTime.UtcNow to be before ... because die Bedingung wurde nicht rechtzeitig erfüllt" (KonsolenTestViewModelTests.cs:250); isoliert wiederholt bestanden — flaky

## Zusammenfassung

- Gesamt: 2163
- Bestanden: 2161
- Fehlgeschlagen: 1
- Übersprungen: 1 (`ArbeitsverzeichnisOeffnenServiceTests.Oeffne_AufNichtWindows_WirftPlatformNotSupportedException` — plattformbedingt übersprungen, Test gilt nur für Nicht-Windows)

Hinweis: Die `Category=OsInterface`-Lane (E2E/ConPTY/echte Prozessstarts) wurde gemäß Vorgabe nicht ausgeführt; `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` war gesetzt.

## Testabdeckung

**Abdeckung:** 27,8 % (Zeilen; 19.336 / 69.612 Zeilen, Branch: 58,4 %)

Generierte Dateien (`obj/`, `Migrations/`) sind aus der Tabelle ausgenommen. Aufgeführt sind Dateien unter 80 % Zeilenabdeckung.

| Datei | Abdeckung |
|-------|-----------|
| `src/Softwareschmiede.App/App.xaml` | 0.0 % |
| `src/Softwareschmiede.App/Views/ArbeitsverzeichnisBearbeitenDialog.xaml.cs` | 0.0 % |
| `src/Softwareschmiede.App/Views/ArbeitsverzeichnisBearbeitenDialog.xaml` | 0.0 % |
| `src/Softwareschmiede.App/Views/AufgabePausierenDialog.xaml.cs` | 0.0 % |
| `src/Softwareschmiede.App/Views/AufgabePausierenDialog.xaml` | 0.0 % |
| `src/Softwareschmiede.App/Views/DashboardView.xaml` | 0.0 % |
| `src/Softwareschmiede.App/Views/DashboardView.xaml.cs` | 0.0 % |
| `src/Softwareschmiede.App/Views/FileExplorerView.xaml.cs` | 0.0 % |
| `src/Softwareschmiede.App/Views/FileExplorerView.xaml` | 0.0 % |
| `src/Softwareschmiede.App/Views/HelpTextDialog.xaml.cs` | 0.0 % |
| `src/Softwareschmiede.App/Views/HelpTextDialog.xaml` | 0.0 % |
| `src/Softwareschmiede.App/Views/IssueSelectionDialog.xaml.cs` | 0.0 % |
| `src/Softwareschmiede.App/Views/IssueSelectionDialog.xaml` | 0.0 % |
| `src/Softwareschmiede.App/Views/KonsolenTestDialog.xaml.cs` | 0.0 % |
| `src/Softwareschmiede.App/Views/KonsolenTestDialog.xaml` | 0.0 % |
| `src/Softwareschmiede.App/Views/MainWindow.xaml.cs` | 0.0 % |
| `src/Softwareschmiede.App/Views/MainWindow.xaml` | 0.0 % |
| `src/Softwareschmiede.App/Views/OpenTodosDialog.xaml` | 0.0 % |
| `src/Softwareschmiede.App/Views/OpenTodosDialog.xaml.cs` | 0.0 % |
| `src/Softwareschmiede.App/Views/PluginSelectionDialog.xaml.cs` | 0.0 % |
| `src/Softwareschmiede.App/Views/PluginSelectionDialog.xaml` | 0.0 % |
| `src/Softwareschmiede.App/Views/PluginSettingEntryEditHelper.cs` | 0.0 % |
| `src/Softwareschmiede.App/Views/PluginSettingFieldTemplateSelector.cs` | 0.0 % |
| `src/Softwareschmiede.App/Views/ProjectDetailView.xaml.cs` | 0.0 % |
| `src/Softwareschmiede.App/Views/ProjectDetailView.xaml` | 0.0 % |
| `src/Softwareschmiede.App/Views/ProjectListView.xaml` | 0.0 % |
| `src/Softwareschmiede.App/Views/ProjectListView.xaml.cs` | 0.0 % |
| `src/Softwareschmiede.App/Views/RepositoryAssignDialog.xaml.cs` | 0.0 % |
| `src/Softwareschmiede.App/Views/RepositoryAssignDialog.xaml` | 0.0 % |
| `src/Softwareschmiede.App/Views/ScmEventQueueView.xaml.cs` | 0.0 % |
| `src/Softwareschmiede.App/Views/ScmEventQueueView.xaml` | 0.0 % |
| `src/Softwareschmiede.App/Views/SettingsView.xaml` | 0.0 % |
| `src/Softwareschmiede.App/Views/SolutionSelectionDialog.xaml.cs` | 0.0 % |
| `src/Softwareschmiede.App/Views/SolutionSelectionDialog.xaml` | 0.0 % |
| `src/Softwareschmiede.App/Views/TaskDetailView.xaml` | 0.0 % |
| `src/Softwareschmiede.App/Views/TodoListView.xaml` | 0.0 % |
| `src/Softwareschmiede.App/Views/TodoListView.xaml.cs` | 0.0 % |
| `src/Softwareschmiede.App/ViewModels/PluginSelectionDialogViewModel.cs` | 0.0 % |
| `src/Softwareschmiede.App/ViewModels/SolutionSelectionDialogViewModel.cs` | 0.0 % |
| `src/Softwareschmiede.App/Services/PluginSelectionDialogService.cs` | 0.0 % |
| `src/Softwareschmiede.App/Services/WpfApplicationShutdownService.cs` | 0.0 % |
| `src/Softwareschmiede.App/Services/WpfAudioService.cs` | 0.0 % |
| `src/Softwareschmiede.App/Services/WpfBannerService.cs` | 0.0 % |
| `src/Softwareschmiede.App/Services/WpfDialogService.cs` | 0.0 % |
| `src/Softwareschmiede.App/Services/WpfUpdateProgressDialogService.cs` | 0.0 % |
| `src/Softwareschmiede.App/Services/Testing/FixtureCliUpdateSafetyService.cs` | 0.0 % |
| `src/Softwareschmiede.App/Services/Testing/ProtokollierenderUpdateService.cs` | 0.0 % |
| `src/Softwareschmiede.App/Services/Testing/RecordingApplicationShutdownService.cs` | 0.0 % |
| `src/Softwareschmiede.App/Services/Testing/RecordingUpdateProcessLauncher.cs` | 0.0 % |
| `src/Softwareschmiede.App/Services/Testing/ScmE2EApiClient.cs` | 0.0 % |
| `src/Softwareschmiede.App/Services/Testing/ScmE2ECredentialStore.cs` | 0.0 % |
| `src/Softwareschmiede.App/Services/Testing/ScmE2EGitRunner.cs` | 0.0 % |
| `src/Softwareschmiede.App/Services/Testing/ScmE2EPluginManager.cs` | 0.0 % |
| `src/Softwareschmiede.App/Services/Testing/ScmE2ERuntimeControl.cs` | 0.0 % |
| `src/Softwareschmiede.App/Services/Testing/ScmE2ETerminalSessionFactory.cs` | 0.0 % |
| `src/Softwareschmiede.App/Services/Testing/ScmE2ETestConfiguration.cs` | 0.0 % |
| `src/Softwareschmiede.App/Controls/ActiveTasksListControl.xaml.cs` | 0.0 % |
| `src/Softwareschmiede.App/Controls/ActiveTasksListControl.xaml` | 0.0 % |
| `src/Softwareschmiede.App/Controls/DiffViewer.xaml.cs` | 0.0 % |
| `src/Softwareschmiede.App/Controls/DiffViewer.xaml` | 0.0 % |
| `src/Softwareschmiede.App/Controls/PluginDetailPanel.xaml.cs` | 0.0 % |
| `src/Softwareschmiede.App/Controls/PluginDetailPanel.xaml` | 0.0 % |
| `src/Softwareschmiede.App/Controls/RecoveryBannerControl.xaml.cs` | 0.0 % |
| `src/Softwareschmiede.App/Controls/RecoveryBannerControl.xaml` | 0.0 % |
| `src/Softwareschmiede.App/Controls/RibbonButtonBase.cs` | 0.0 % |
| `src/Softwareschmiede.App/Controls/RibbonButtonStyleHelper.cs` | 0.0 % |
| `src/Softwareschmiede.App/Controls/RibbonGroup.xaml.cs` | 0.0 % |
| `src/Softwareschmiede.App/Controls/RibbonGroup.xaml` | 0.0 % |
| `src/Softwareschmiede.App/Controls/RibbonLargeButton.xaml` | 0.0 % |
| `src/Softwareschmiede.App/Controls/RibbonLargeButton.xaml.cs` | 0.0 % |
| `src/Softwareschmiede.App/Controls/RibbonSmallButton.xaml` | 0.0 % |
| `src/Softwareschmiede.App/Controls/RibbonSmallButton.xaml.cs` | 0.0 % |
| `src/Softwareschmiede.App/Controls/RibbonSplitButton.xaml.cs` | 0.0 % |
| `src/Softwareschmiede.App/Controls/RibbonSplitButton.xaml` | 0.0 % |
| `src/Softwareschmiede.App/Behaviors/StatusUebergangsAnimation.cs` | 0.0 % |
| `src/Softwareschmiede/Infrastructure/Terminal/PseudoConsole.cs` | 0.0 % |
| `src/Softwareschmiede/Infrastructure/Terminal/PseudoConsoleProcessStarter.cs` | 0.0 % |
| `src/Softwareschmiede/Infrastructure/Terminal/Win32PseudoConsoleProcessLauncher.cs` | 0.0 % |
| `src/Softwareschmiede/Infrastructure/Services/BenutzerkontextService.cs` | 0.0 % |
| `src/Softwareschmiede/Infrastructure/Services/CliRunner.cs` | 0.0 % |
| `src/Softwareschmiede/Infrastructure/Services/SystemProzessStarter.cs` | 0.0 % |
| `src/Softwareschmiede/Infrastructure/Services/SystemShutdownService.cs` | 0.0 % |
| `src/Softwareschmiede/Infrastructure/Services/WindowsCredentialStore.cs` | 0.0 % |
| `src/Softwareschmiede/Infrastructure/Data/SoftwareschmiededDbContextDesignTimeFactory.cs` | 0.0 % |
| `src/Softwareschmiede/Domain/ValueObjects/WorkspaceNodeRow.cs` | 0.0 % |
| `src/Softwareschmiede/Domain/Exceptions/DirectoryAccessException.cs` | 0.0 % |
| `src/Softwareschmiede/Domain/Exceptions/SoftwareschmiedeException.cs` | 0.0 % |
| `src/Softwareschmiede/Domain/Entities/PluginKonfiguration.cs` | 0.0 % |
| `src/Softwareschmiede/Domain/Entities/ScmTaskLease.cs` | 0.0 % |
| `src/Softwareschmiede/Application/Services/DirectoryAccessGuard.cs` | 0.0 % |
| `src/Softwareschmiede/Application/Services/GitBranchNameValidator.cs` | 0.0 % |
| `src/Softwareschmiede/Application/Services/GitKlonHelper.cs` | 0.0 % |
| `src/Softwareschmiede/Application/Services/KiAufgabenAbschlussEreignis.cs` | 0.0 % |
| `src/Softwareschmiede/Application/Services/KiAufgabenBenachrichtigungsHub.cs` | 0.0 % |
| `src/Softwareschmiede/Application/Services/ScmEventExchangeService.cs` | 0.0 % |
| `src/Softwareschmiede/Application/Services/ScmEventMonitoringService.cs` | 0.0 % |
| `src/Softwareschmiede/Application/Services/ScmEventTaskResolver.cs` | 0.0 % |
| `src/Softwareschmiede/Application/Services/ScmTaskCompletionMonitoringService.cs` | 0.0 % |
| `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/AgentInfo.cs` | 0.0 % |
| `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IGitPlugin.cs` | 3.3 % |
| `src/Softwareschmiede.App/App.xaml.cs` | 4.1 % |
| `src/Softwareschmiede.App/Services/Testing/UpdateE2ETestConfiguration.cs` | 7.1 % |
| `src/Softwareschmiede.App/Views/SettingsView.xaml.cs` | 7.6 % |
| `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/PullRequestProviderDescriptor.cs` | 13.8 % |
| `src/Softwareschmiede.App/Views/TaskDetailView.xaml.cs` | 20.2 % |
| `src/Softwareschmiede.App/Services/Testing/UpdateSettingsReadFailureInterceptor.cs` | 21.8 % |
| `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmRequirement.cs` | 24.2 % |
| `src/Softwareschmiede/Application/Services/BenachrichtigungsService.cs` | 31.1 % |
| `src/Softwareschmiede.App/Services/Testing/UpdateE2ETestKontext.cs` | 36.4 % |
| `src/Softwareschmiede/Application/Services/ScmCliAccessEvaluator.cs` | 44.7 % |
| `src/Softwareschmiede/Application/Services/ScmRestrictedProcessAccess.cs` | 44.9 % |
| `src/Softwareschmiede.App/Services/Testing/UpdateE2ESzenario.cs` | 45.9 % |
| `src/Softwareschmiede/Application/Services/ScmCliWorkspaceTrust.cs` | 46.8 % |
| `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs` | 50.6 % |
| `src/Softwareschmiede/Application/Services/ScmEventOrchestrator.cs` | 51.8 % |
| `src/Softwareschmiede.App/Controls/KeyToVt100Encoder.cs` | 53.2 % |
| `src/Softwareschmiede.App/Services/Testing/UpdateE2EProtokoll.cs` | 53.3 % |
| `src/Softwareschmiede.Plugin.Contracts/Domain/Abstractions/GitPluginBase.cs` | 53.4 % |
| `src/Softwareschmiede/Infrastructure/Terminal/SimulatedPseudoConsoleProcessLauncher.cs` | 54.0 % |
| `plugins/Softwareschmiede.Plugin.KiSimulator/KiSimulatorPlugin.cs` | 55.9 % |
| `src/Softwareschmiede.App/Controls/StatusIndicatorControl.xaml.cs` | 56.6 % |
| `src/Softwareschmiede.App/ViewModels/ScmEventQueueViewModel.cs` | 57.7 % |
| `plugins/Softwareschmiede.Plugin.BitBucket/BitBucketPlugin.cs` | 57.8 % |
| `plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.EventResponses.cs` | 59.2 % |
| `src/Softwareschmiede.App/ViewModels/ProjectListViewModel.cs` | 60.5 % |
| `src/Softwareschmiede.Plugin.Contracts/Domain/Abstractions/CliKiPluginBase.cs` | 60.6 % |
| `src/Softwareschmiede/Application/Services/DiffService.cs` | 61.5 % |
| `src/Softwareschmiede.App/ViewModels/TodoListViewModel.cs` | 67.8 % |
| `src/Softwareschmiede/Application/Services/PeriodicBackgroundService.cs` | 67.9 % |
| `src/Softwareschmiede/Application/Services/PullRequestReferenzService.cs` | 68.5 % |
| `src/Softwareschmiede/Application/Services/ScmEventOutbox.cs` | 69.6 % |
| `src/Softwareschmiede/Domain/ValueObjects/FilePreview.cs` | 70.0 % |
| `src/Softwareschmiede.App/ViewModels/ProjectDetailViewModel.cs` | 70.0 % |
| `src/Softwareschmiede/Domain/Enums/InvalidStatusTransitionException.cs` | 71.4 % |
| `src/Softwareschmiede.App/Views/IssueCreateDialog.xaml.cs` | 73.3 % |
| `src/Softwareschmiede.App/ViewModels/ViewModelBase.cs` | 74.5 % |
| `src/Softwareschmiede/Infrastructure/Services/Updates/UpdateScriptService.cs` | 74.7 % |
| `src/Softwareschmiede/Application/Services/CliOutputProtokollWriter.cs` | 75.1 % |
| `src/Softwareschmiede/Application/Services/AufgabeRecoveryService.cs` | 75.8 % |
| `src/Softwareschmiede.App/Services/Testing/UpdateFixtureHttpMessageHandler.cs` | 76.2 % |
| `src/Softwareschmiede.App/Converters/WorkspaceFileNodeIconConverter.cs` | 76.5 % |
| `src/Softwareschmiede/Infrastructure/Terminal/AnsiSequenceParser.cs` | 77.2 % |
| `src/Softwareschmiede/Infrastructure/Terminal/PseudoConsoleSession.cs` | 77.7 % |
| `src/Softwareschmiede/Infrastructure/Terminal/ScmCoordinatedTerminalSession.cs` | 78.1 % |
| `src/Softwareschmiede/Domain/Entities/PullRequestReferenz.cs` | 78.1 % |
| `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs` | 78.7 % |
| `src/Softwareschmiede/Infrastructure/Plugins/PluginManager.cs` | 79.2 % |
| `src/Softwareschmiede.App/ViewModels/DashboardViewModel.cs` | 79.7 % |

## Fehlende Tests

Quelle: `Coverage-Daten` (Dateien mit 0 % Zeilenabdeckung; generierte Dateien `obj/` und `Migrations/` ausgenommen). Hinweis: Viele der 0-%-Dateien sind WPF-Views/Controls und E2E-Infrastruktur (`Services/Testing/*`, `Infrastructure/Terminal/*`), die primär über die hier nicht ausgeführte `OsInterface`-E2E-Lane abgedeckt werden.

- `src/Softwareschmiede.App/App.xaml` — 0 % Abdeckung
- `src/Softwareschmiede.App/Views/ArbeitsverzeichnisBearbeitenDialog.xaml.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/Views/ArbeitsverzeichnisBearbeitenDialog.xaml` — 0 % Abdeckung
- `src/Softwareschmiede.App/Views/AufgabePausierenDialog.xaml.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/Views/AufgabePausierenDialog.xaml` — 0 % Abdeckung
- `src/Softwareschmiede.App/Views/DashboardView.xaml` — 0 % Abdeckung
- `src/Softwareschmiede.App/Views/DashboardView.xaml.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/Views/FileExplorerView.xaml.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/Views/FileExplorerView.xaml` — 0 % Abdeckung
- `src/Softwareschmiede.App/Views/HelpTextDialog.xaml.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/Views/HelpTextDialog.xaml` — 0 % Abdeckung
- `src/Softwareschmiede.App/Views/IssueSelectionDialog.xaml.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/Views/IssueSelectionDialog.xaml` — 0 % Abdeckung
- `src/Softwareschmiede.App/Views/KonsolenTestDialog.xaml.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/Views/KonsolenTestDialog.xaml` — 0 % Abdeckung
- `src/Softwareschmiede.App/Views/MainWindow.xaml.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/Views/MainWindow.xaml` — 0 % Abdeckung
- `src/Softwareschmiede.App/Views/OpenTodosDialog.xaml` — 0 % Abdeckung
- `src/Softwareschmiede.App/Views/OpenTodosDialog.xaml.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/Views/PluginSelectionDialog.xaml.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/Views/PluginSelectionDialog.xaml` — 0 % Abdeckung
- `src/Softwareschmiede.App/Views/PluginSettingEntryEditHelper.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/Views/PluginSettingFieldTemplateSelector.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/Views/ProjectDetailView.xaml.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/Views/ProjectDetailView.xaml` — 0 % Abdeckung
- `src/Softwareschmiede.App/Views/ProjectListView.xaml` — 0 % Abdeckung
- `src/Softwareschmiede.App/Views/ProjectListView.xaml.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/Views/RepositoryAssignDialog.xaml.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/Views/RepositoryAssignDialog.xaml` — 0 % Abdeckung
- `src/Softwareschmiede.App/Views/ScmEventQueueView.xaml.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/Views/ScmEventQueueView.xaml` — 0 % Abdeckung
- `src/Softwareschmiede.App/Views/SettingsView.xaml` — 0 % Abdeckung
- `src/Softwareschmiede.App/Views/SolutionSelectionDialog.xaml.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/Views/SolutionSelectionDialog.xaml` — 0 % Abdeckung
- `src/Softwareschmiede.App/Views/TaskDetailView.xaml` — 0 % Abdeckung
- `src/Softwareschmiede.App/Views/TodoListView.xaml` — 0 % Abdeckung
- `src/Softwareschmiede.App/Views/TodoListView.xaml.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/ViewModels/PluginSelectionDialogViewModel.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/ViewModels/SolutionSelectionDialogViewModel.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/Services/PluginSelectionDialogService.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/Services/WpfApplicationShutdownService.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/Services/WpfAudioService.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/Services/WpfBannerService.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/Services/WpfDialogService.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/Services/WpfUpdateProgressDialogService.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/Services/Testing/FixtureCliUpdateSafetyService.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/Services/Testing/ProtokollierenderUpdateService.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/Services/Testing/RecordingApplicationShutdownService.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/Services/Testing/RecordingUpdateProcessLauncher.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/Services/Testing/ScmE2EApiClient.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/Services/Testing/ScmE2ECredentialStore.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/Services/Testing/ScmE2EGitRunner.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/Services/Testing/ScmE2EPluginManager.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/Services/Testing/ScmE2ERuntimeControl.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/Services/Testing/ScmE2ETerminalSessionFactory.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/Services/Testing/ScmE2ETestConfiguration.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/Controls/ActiveTasksListControl.xaml.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/Controls/ActiveTasksListControl.xaml` — 0 % Abdeckung
- `src/Softwareschmiede.App/Controls/DiffViewer.xaml.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/Controls/DiffViewer.xaml` — 0 % Abdeckung
- `src/Softwareschmiede.App/Controls/PluginDetailPanel.xaml.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/Controls/PluginDetailPanel.xaml` — 0 % Abdeckung
- `src/Softwareschmiede.App/Controls/RecoveryBannerControl.xaml.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/Controls/RecoveryBannerControl.xaml` — 0 % Abdeckung
- `src/Softwareschmiede.App/Controls/RibbonButtonBase.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/Controls/RibbonButtonStyleHelper.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/Controls/RibbonGroup.xaml.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/Controls/RibbonGroup.xaml` — 0 % Abdeckung
- `src/Softwareschmiede.App/Controls/RibbonLargeButton.xaml` — 0 % Abdeckung
- `src/Softwareschmiede.App/Controls/RibbonLargeButton.xaml.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/Controls/RibbonSmallButton.xaml` — 0 % Abdeckung
- `src/Softwareschmiede.App/Controls/RibbonSmallButton.xaml.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/Controls/RibbonSplitButton.xaml.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/Controls/RibbonSplitButton.xaml` — 0 % Abdeckung
- `src/Softwareschmiede.App/Behaviors/StatusUebergangsAnimation.cs` — 0 % Abdeckung
- `src/Softwareschmiede/Infrastructure/Terminal/PseudoConsole.cs` — 0 % Abdeckung
- `src/Softwareschmiede/Infrastructure/Terminal/PseudoConsoleProcessStarter.cs` — 0 % Abdeckung
- `src/Softwareschmiede/Infrastructure/Terminal/Win32PseudoConsoleProcessLauncher.cs` — 0 % Abdeckung
- `src/Softwareschmiede/Infrastructure/Services/BenutzerkontextService.cs` — 0 % Abdeckung
- `src/Softwareschmiede/Infrastructure/Services/CliRunner.cs` — 0 % Abdeckung
- `src/Softwareschmiede/Infrastructure/Services/SystemProzessStarter.cs` — 0 % Abdeckung
- `src/Softwareschmiede/Infrastructure/Services/SystemShutdownService.cs` — 0 % Abdeckung
- `src/Softwareschmiede/Infrastructure/Services/WindowsCredentialStore.cs` — 0 % Abdeckung
- `src/Softwareschmiede/Infrastructure/Data/SoftwareschmiededDbContextDesignTimeFactory.cs` — 0 % Abdeckung
- `src/Softwareschmiede/Domain/ValueObjects/WorkspaceNodeRow.cs` — 0 % Abdeckung
- `src/Softwareschmiede/Domain/Exceptions/DirectoryAccessException.cs` — 0 % Abdeckung
- `src/Softwareschmiede/Domain/Exceptions/SoftwareschmiedeException.cs` — 0 % Abdeckung
- `src/Softwareschmiede/Domain/Entities/PluginKonfiguration.cs` — 0 % Abdeckung
- `src/Softwareschmiede/Domain/Entities/ScmTaskLease.cs` — 0 % Abdeckung
- `src/Softwareschmiede/Application/Services/DirectoryAccessGuard.cs` — 0 % Abdeckung
- `src/Softwareschmiede/Application/Services/GitBranchNameValidator.cs` — 0 % Abdeckung
- `src/Softwareschmiede/Application/Services/GitKlonHelper.cs` — 0 % Abdeckung
- `src/Softwareschmiede/Application/Services/KiAufgabenAbschlussEreignis.cs` — 0 % Abdeckung
- `src/Softwareschmiede/Application/Services/KiAufgabenBenachrichtigungsHub.cs` — 0 % Abdeckung
- `src/Softwareschmiede/Application/Services/ScmEventExchangeService.cs` — 0 % Abdeckung
- `src/Softwareschmiede/Application/Services/ScmEventMonitoringService.cs` — 0 % Abdeckung
- `src/Softwareschmiede/Application/Services/ScmEventTaskResolver.cs` — 0 % Abdeckung
- `src/Softwareschmiede/Application/Services/ScmTaskCompletionMonitoringService.cs` — 0 % Abdeckung
- `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/AgentInfo.cs` — 0 % Abdeckung
