# Test-Ergebnisse

## Ergebnis

**Status:** Fehler vorhanden

Hinweis: Beide Fehlschläge sind dokumentierte Umgebungs-Flakes ohne Bezug zur Branch-Änderung (siehe Einordnung unten). Der in Iteration 2 dokumentierte Inter-Test-Race in `ScmEventDispatchTests_*` ist in diesem Lauf **nicht** wieder aufgetreten — die seither eingeführte Fixture-Isolierung (`ScmEventExecutionLimitService.ReservierungenZuruecksetzen()` in `src/Softwareschmiede.Tests/Helpers/ScmDispatchFixture.cs:201`) greift.

## Fehlgeschlagene Tests

### Softwareschmiede.Tests / KonsolenTestViewModelTests.cs

- **Softwareschmiede.Tests.App.ViewModels.KonsolenTestViewModelTests.WiedergabeNeustarten_AusSchrittmodus_SpieltAbPosition0** — Expected sut.IstWiedergabeAktiv to be True, but found False. *(Bekannter Umgebungs-Flake; isolierter Nachlauf bestanden.)*

### Softwareschmiede.Tests / TerminalControlTests.KeyInput.cs

- **Softwareschmiede.Tests.App.Controls.TerminalControlTests.OnPreviewKeyDown_MausankerDannShiftRechts_ZeichnetAuswahlUndKopiertText** — System.Runtime.InteropServices.COMException: OpenClipboard fehlgeschlagen (0x800401D0 (CLIPBRD_E_CANT_OPEN)). *(Dokumentierter Umgebungs-Flake; schlägt in dieser Sandbox auch im isolierten Nachlauf reproduzierbar fehl — 2× verifiziert, jeweils CLIPBRD_E_CANT_OPEN nach ~15 s internem Retry. Die Windows-Zwischenablage ist in dieser Session nicht zuverlässig zugreifbar; kein Bezug zur Branch-Änderung, der Test berührt ausschließlich TerminalControl/Clipboard.)*

**Einordnung:** Beide Tests stehen in der Projektregel-Liste der bekannten Umgebungs-Flakes (`KonsolenTestViewModelTests.WiedergabeNeustarten_*`, `TerminalControlTests.OnPreviewKeyDown_*` / CLIPBRD_E_CANT_OPEN). Kein `ScmEventDispatchTests_*`-Fehlschlag in diesem Lauf.

## Zusammenfassung

| Projekt | Gesamt | Bestanden | Fehlgeschlagen | Übersprungen |
|---------|--------|-----------|----------------|--------------|
| Softwareschmiede.Tests (`Category!=OsInterface`) | 2694 | 2689 | 2 | 3 |
| Softwareschmiede.IntegrationTests (`Category!=OsInterface`) | 352 | 352 | 0 | 0 |
| **Summe** | **3046** | **3041** | **2** | **3** |

- Build vor Testlauf: `dotnet build Softwareschmiede.slnx` — erfolgreich (0 Warnungen, 0 Fehler)
- `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` war bei allen Läufen gesetzt
- OS-Interface-Tests (`Category=OsInterface`) sind nicht Teil dieses Laufs
- Isolierte Nachläufe: `KonsolenTestViewModelTests.WiedergabeNeustarten_AusSchrittmodus_SpieltAbPosition0` → bestanden; `TerminalControlTests.OnPreviewKeyDown_MausankerDannShiftRechts_ZeichnetAuswahlUndKopiertText` → 2× fehlgeschlagen (CLIPBRD_E_CANT_OPEN, deterministisch in dieser Umgebung)

## Testabdeckung

**Abdeckung:** 90,50 % (68.647 / 75.856 Zeilen; gemergt aus beiden Cobertura-Reports)

Geänderte Dateien des Branches: `AufgabeRecoveryService.cs` 96,0 %, `EntwicklungsprozessService.cs` 83,4 %, `KiAusfuehrungsStatusKlassifikation.cs` 100 %, `ScmEventExecutionLimitService.cs` 100 %, `ScmEventOrchestrator.cs` 80,2 %.

Nur Dateien unter 80 % Zeilenabdeckung (generierte Dateien unter `obj/`, `.g.cs`, `.xaml` und `AssemblyInfo.cs` ausgenommen):

| Datei | Abdeckung |
|-------|-----------|
| src/Softwareschmiede.App/Behaviors/StatusUebergangsAnimation.cs | 0.0 % |
| src/Softwareschmiede.App/Controls/ActiveTasksListControl.xaml.cs | 0.0 % |
| src/Softwareschmiede.App/Controls/DiffViewer.xaml.cs | 0.0 % |
| src/Softwareschmiede.App/Controls/PluginDetailPanel.xaml.cs | 0.0 % |
| src/Softwareschmiede.App/Controls/RecoveryBannerControl.xaml.cs | 0.0 % |
| src/Softwareschmiede.App/Controls/RibbonButtonBase.cs | 0.0 % |
| src/Softwareschmiede.App/Controls/RibbonButtonStyleHelper.cs | 0.0 % |
| src/Softwareschmiede.App/Controls/RibbonGroup.xaml.cs | 0.0 % |
| src/Softwareschmiede.App/Controls/RibbonLargeButton.xaml.cs | 0.0 % |
| src/Softwareschmiede.App/Controls/RibbonSmallButton.xaml.cs | 0.0 % |
| src/Softwareschmiede.App/Controls/RibbonSplitButton.xaml.cs | 0.0 % |
| src/Softwareschmiede.App/Services/PluginSelectionDialogService.cs | 0.0 % |
| src/Softwareschmiede.App/Services/Testing/FixtureCliUpdateSafetyService.cs | 0.0 % |
| src/Softwareschmiede.App/Services/Testing/ProtokollierenderUpdateService.cs | 0.0 % |
| src/Softwareschmiede.App/Services/Testing/RecordingApplicationShutdownService.cs | 0.0 % |
| src/Softwareschmiede.App/Services/Testing/RecordingUpdateProcessLauncher.cs | 0.0 % |
| src/Softwareschmiede.App/Services/Testing/ScmE2EApiClient.cs | 0.0 % |
| src/Softwareschmiede.App/Services/Testing/ScmE2ECredentialStore.cs | 0.0 % |
| src/Softwareschmiede.App/Services/Testing/ScmE2EGitRunner.cs | 0.0 % |
| src/Softwareschmiede.App/Services/Testing/ScmE2EPluginManager.cs | 0.0 % |
| src/Softwareschmiede.App/Services/Testing/ScmE2ERuntimeControl.cs | 0.0 % |
| src/Softwareschmiede.App/Services/Testing/ScmE2ETerminalSessionFactory.cs | 0.0 % |
| src/Softwareschmiede.App/Services/Testing/ScmE2ETestConfiguration.cs | 0.0 % |
| src/Softwareschmiede.App/Services/WpfApplicationShutdownService.cs | 0.0 % |
| src/Softwareschmiede.App/Services/WpfAudioService.cs | 0.0 % |
| src/Softwareschmiede.App/Services/WpfBannerService.cs | 0.0 % |
| src/Softwareschmiede.App/Services/WpfDialogService.cs | 0.0 % |
| src/Softwareschmiede.App/Services/WpfUpdateProgressDialogService.cs | 0.0 % |
| src/Softwareschmiede.App/ViewModels/PluginSelectionDialogViewModel.cs | 0.0 % |
| src/Softwareschmiede.App/ViewModels/SolutionSelectionDialogViewModel.cs | 0.0 % |
| src/Softwareschmiede.App/Views/ArbeitsverzeichnisBearbeitenDialog.xaml.cs | 0.0 % |
| src/Softwareschmiede.App/Views/AufgabePausierenDialog.xaml.cs | 0.0 % |
| src/Softwareschmiede.App/Views/DashboardView.xaml.cs | 0.0 % |
| src/Softwareschmiede.App/Views/FileExplorerView.xaml.cs | 0.0 % |
| src/Softwareschmiede.App/Views/HelpTextDialog.xaml.cs | 0.0 % |
| src/Softwareschmiede.App/Views/IssueSelectionDialog.xaml.cs | 0.0 % |
| src/Softwareschmiede.App/Views/KonsolenTestDialog.xaml.cs | 0.0 % |
| src/Softwareschmiede.App/Views/MainWindow.xaml.cs | 0.0 % |
| src/Softwareschmiede.App/Views/OpenTodosDialog.xaml.cs | 0.0 % |
| src/Softwareschmiede.App/Views/PluginSelectionDialog.xaml.cs | 0.0 % |
| src/Softwareschmiede.App/Views/PluginSettingEntryEditHelper.cs | 0.0 % |
| src/Softwareschmiede.App/Views/PluginSettingFieldTemplateSelector.cs | 0.0 % |
| src/Softwareschmiede.App/Views/ProjectDetailView.xaml.cs | 0.0 % |
| src/Softwareschmiede.App/Views/ProjectListView.xaml.cs | 0.0 % |
| src/Softwareschmiede.App/Views/RepositoryAssignDialog.xaml.cs | 0.0 % |
| src/Softwareschmiede.App/Views/ScmEventQueueView.xaml.cs | 0.0 % |
| src/Softwareschmiede.App/Views/SolutionSelectionDialog.xaml.cs | 0.0 % |
| src/Softwareschmiede.App/Views/TodoListView.xaml.cs | 0.0 % |
| src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/AgentInfo.cs | 0.0 % |
| src/Softwareschmiede/Application/Services/DirectoryAccessGuard.cs | 0.0 % |
| src/Softwareschmiede/Application/Services/GitBranchNameValidator.cs | 0.0 % |
| src/Softwareschmiede/Application/Services/GitKlonHelper.cs | 0.0 % |
| src/Softwareschmiede/Application/Services/KiAufgabenAbschlussEreignis.cs | 0.0 % |
| src/Softwareschmiede/Application/Services/KiAufgabenBenachrichtigungsHub.cs | 0.0 % |
| src/Softwareschmiede/Domain/Entities/PluginKonfiguration.cs | 0.0 % |
| src/Softwareschmiede/Domain/Entities/ScmTaskLease.cs | 0.0 % |
| src/Softwareschmiede/Domain/Exceptions/DirectoryAccessException.cs | 0.0 % |
| src/Softwareschmiede/Domain/Exceptions/SoftwareschmiedeException.cs | 0.0 % |
| src/Softwareschmiede/Domain/ValueObjects/WorkspaceNodeRow.cs | 0.0 % |
| src/Softwareschmiede/Infrastructure/Data/SoftwareschmiededDbContextDesignTimeFactory.cs | 0.0 % |
| src/Softwareschmiede/Infrastructure/Services/BenutzerkontextService.cs | 0.0 % |
| src/Softwareschmiede/Infrastructure/Services/SystemProzessStarter.cs | 0.0 % |
| src/Softwareschmiede/Infrastructure/Services/WindowsCredentialStore.cs | 0.0 % |
| src/Softwareschmiede/Infrastructure/Terminal/PseudoConsole.cs | 0.0 % |
| src/Softwareschmiede/Infrastructure/Terminal/PseudoConsoleProcessStarter.cs | 0.0 % |
| src/Softwareschmiede/Infrastructure/Terminal/Win32PseudoConsoleProcessLauncher.cs | 0.0 % |
| src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IGitPlugin.cs | 3.2 % |
| src/Softwareschmiede.App/App.xaml.cs | 3.7 % |
| src/Softwareschmiede.App/Services/Testing/UpdateE2ETestConfiguration.cs | 7.1 % |
| src/Softwareschmiede.App/Views/SettingsView.xaml.cs | 7.6 % |
| src/Softwareschmiede/Infrastructure/Services/SystemShutdownService.cs | 14.7 % |
| src/Softwareschmiede.App/Views/TaskDetailView.xaml.cs | 20.2 % |
| src/Softwareschmiede.App/Services/Testing/UpdateSettingsReadFailureInterceptor.cs | 21.8 % |
| src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmRequirement.cs | 24.2 % |
| src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/PullRequestProviderDescriptor.cs | 31.0 % |
| src/Softwareschmiede/Application/Services/BenachrichtigungsService.cs | 31.1 % |
| src/Softwareschmiede/Infrastructure/Services/CliRunner.cs | 31.4 % |
| src/Softwareschmiede.App/Services/Testing/UpdateE2ETestKontext.cs | 36.4 % |
| src/Softwareschmiede/Application/Services/ScmCliWorkspaceTrust.cs | 46.8 % |
| src/Softwareschmiede.App/Services/Testing/UpdateE2ESzenario.cs | 47.4 % |
| src/Softwareschmiede/Migrations/20260507051631_202507_Fix_DateTimeOffset_SQLiteOrdering.cs | 50.0 % |
| src/Softwareschmiede.App/Controls/KeyToVt100Encoder.cs | 53.2 % |
| src/Softwareschmiede.App/Services/Testing/UpdateE2EProtokoll.cs | 53.3 % |
| src/Softwareschmiede/Infrastructure/Terminal/SimulatedPseudoConsoleProcessLauncher.cs | 54.0 % |
| plugins/Softwareschmiede.Plugin.KiSimulator/KiSimulatorPlugin.cs | 55.9 % |
| src/Softwareschmiede.App/Controls/StatusIndicatorControl.xaml.cs | 56.6 % |
| src/Softwareschmiede.App/ViewModels/ProjectListViewModel.cs | 60.5 % |
| src/Softwareschmiede.Plugin.Contracts/Domain/Abstractions/CliKiPluginBase.cs | 60.6 % |
| src/Softwareschmiede/Application/Services/DiffService.cs | 61.5 % |
| plugins/Softwareschmiede.Plugin.BitBucket/BitBucketPlugin.cs | 64.1 % |
| src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs | 67.1 % |
| src/Softwareschmiede.App/ViewModels/TodoListViewModel.cs | 67.8 % |
| src/Softwareschmiede/Application/Services/PeriodicBackgroundService.cs | 67.9 % |
| src/Softwareschmiede/Domain/ValueObjects/FilePreview.cs | 70.0 % |
| src/Softwareschmiede.App/ViewModels/ProjectDetailViewModel.cs | 70.0 % |
| src/Softwareschmiede/Domain/Enums/InvalidStatusTransitionException.cs | 71.4 % |
| src/Softwareschmiede.App/Converters/AppConverters.cs | 72.3 % |
| plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.EventResponses.cs | 73.1 % |
| src/Softwareschmiede.App/Views/IssueCreateDialog.xaml.cs | 73.3 % |
| src/Softwareschmiede.App/ViewModels/ViewModelBase.cs | 74.5 % |
| src/Softwareschmiede/Infrastructure/Services/Updates/UpdateScriptService.cs | 74.7 % |
| src/Softwareschmiede.App/ViewModels/ScmEventQueueViewModel.cs | 74.8 % |
| src/Softwareschmiede/Application/Services/CliOutputProtokollWriter.cs | 75.1 % |
| src/Softwareschmiede.Plugin.Contracts/Domain/Abstractions/GitPluginBase.cs | 76.3 % |
| src/Softwareschmiede.App/Converters/WorkspaceFileNodeIconConverter.cs | 76.5 % |
| src/Softwareschmiede/Application/Services/ScmBackmergeMonitoringService.cs | 76.5 % |
| src/Softwareschmiede.App/Services/Testing/UpdateFixtureHttpMessageHandler.cs | 76.9 % |
| src/Softwareschmiede/Infrastructure/Terminal/AnsiSequenceParser.cs | 77.2 % |
| src/Softwareschmiede/Infrastructure/Terminal/PseudoConsoleSession.cs | 77.5 % |
| src/Softwareschmiede/Domain/Entities/PullRequestReferenz.cs | 78.1 % |
| src/Softwareschmiede/Infrastructure/Terminal/ScmCoordinatedTerminalSession.cs | 78.1 % |
| src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs | 79.1 % |
| src/Softwareschmiede.App/ViewModels/DashboardViewModel.cs | 79.7 % |

Anmerkung: Die 0 %-Einträge betreffen überwiegend WPF-Code-Behind/Views und WPF-/Windows-nahe Infrastruktur (`Wpf*`, `PseudoConsole*`, `WindowsCredentialStore` usw.) sowie E2E-Test-Infrastruktur (`Services/Testing/*`), die nur über `Category=OsInterface`-E2E-Tests abgedeckt wird — diese Kategorie war in diesem Lauf bewusst ausgeschlossen.

## Fehlende Tests

Quelle: `Coverage-Daten` (Dateien mit 0 % Zeilenabdeckung, generierte Dateien ausgenommen)

- `src/Softwareschmiede.App/Behaviors/StatusUebergangsAnimation.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/Controls/ActiveTasksListControl.xaml.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/Controls/DiffViewer.xaml.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/Controls/PluginDetailPanel.xaml.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/Controls/RecoveryBannerControl.xaml.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/Controls/RibbonButtonBase.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/Controls/RibbonButtonStyleHelper.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/Controls/RibbonGroup.xaml.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/Controls/RibbonLargeButton.xaml.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/Controls/RibbonSmallButton.xaml.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/Controls/RibbonSplitButton.xaml.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/Services/PluginSelectionDialogService.cs` — 0 % Abdeckung
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
- `src/Softwareschmiede.App/Services/WpfApplicationShutdownService.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/Services/WpfAudioService.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/Services/WpfBannerService.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/Services/WpfDialogService.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/Services/WpfUpdateProgressDialogService.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/ViewModels/PluginSelectionDialogViewModel.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/ViewModels/SolutionSelectionDialogViewModel.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/Views/ArbeitsverzeichnisBearbeitenDialog.xaml.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/Views/AufgabePausierenDialog.xaml.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/Views/DashboardView.xaml.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/Views/FileExplorerView.xaml.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/Views/HelpTextDialog.xaml.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/Views/IssueSelectionDialog.xaml.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/Views/KonsolenTestDialog.xaml.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/Views/MainWindow.xaml.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/Views/OpenTodosDialog.xaml.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/Views/PluginSelectionDialog.xaml.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/Views/PluginSettingEntryEditHelper.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/Views/PluginSettingFieldTemplateSelector.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/Views/ProjectDetailView.xaml.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/Views/ProjectListView.xaml.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/Views/RepositoryAssignDialog.xaml.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/Views/ScmEventQueueView.xaml.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/Views/SolutionSelectionDialog.xaml.cs` — 0 % Abdeckung
- `src/Softwareschmiede.App/Views/TodoListView.xaml.cs` — 0 % Abdeckung
- `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/AgentInfo.cs` — 0 % Abdeckung
- `src/Softwareschmiede/Application/Services/DirectoryAccessGuard.cs` — 0 % Abdeckung
- `src/Softwareschmiede/Application/Services/GitBranchNameValidator.cs` — 0 % Abdeckung
- `src/Softwareschmiede/Application/Services/GitKlonHelper.cs` — 0 % Abdeckung
- `src/Softwareschmiede/Application/Services/KiAufgabenAbschlussEreignis.cs` — 0 % Abdeckung
- `src/Softwareschmiede/Application/Services/KiAufgabenBenachrichtigungsHub.cs` — 0 % Abdeckung
- `src/Softwareschmiede/Domain/Entities/PluginKonfiguration.cs` — 0 % Abdeckung
- `src/Softwareschmiede/Domain/Entities/ScmTaskLease.cs` — 0 % Abdeckung
- `src/Softwareschmiede/Domain/Exceptions/DirectoryAccessException.cs` — 0 % Abdeckung
- `src/Softwareschmiede/Domain/Exceptions/SoftwareschmiedeException.cs` — 0 % Abdeckung
- `src/Softwareschmiede/Domain/ValueObjects/WorkspaceNodeRow.cs` — 0 % Abdeckung
- `src/Softwareschmiede/Infrastructure/Data/SoftwareschmiededDbContextDesignTimeFactory.cs` — 0 % Abdeckung
- `src/Softwareschmiede/Infrastructure/Services/BenutzerkontextService.cs` — 0 % Abdeckung
- `src/Softwareschmiede/Infrastructure/Services/SystemProzessStarter.cs` — 0 % Abdeckung
- `src/Softwareschmiede/Infrastructure/Services/WindowsCredentialStore.cs` — 0 % Abdeckung
- `src/Softwareschmiede/Infrastructure/Terminal/PseudoConsole.cs` — 0 % Abdeckung
- `src/Softwareschmiede/Infrastructure/Terminal/PseudoConsoleProcessStarter.cs` — 0 % Abdeckung
- `src/Softwareschmiede/Infrastructure/Terminal/Win32PseudoConsoleProcessLauncher.cs` — 0 % Abdeckung
