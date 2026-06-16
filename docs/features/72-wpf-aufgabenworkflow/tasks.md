# Tasks: Aufgabenworkflow Optimierung

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Datenmodell | `AufgabeStatus` Enum: `ArbeitsverzeichnisEingerichtet` entfernen | Offen | — |
| 2 | Datenmodell | `AufgabeStatus` Enum: `InArbeit` entfernen | Offen | — |
| 3 | Datenbank | Migration `20260610000001_UpdateAufgabeStatusEnum` validieren und ggf. anpassen | Offen | — |
| 4 | Logik | `AufgabeService.ValidateStatusTransition` anpassen für neue erlaubte Übergänge | Offen | Unit Test |
| 5 | Logik | `AufgabeService.StartenAsync` anpassen (Status auf `Gestartet` setzen) | Offen | Unit Test |
| 6 | Logik | `PluginDefaultSettingsService.GetProjectDefaultPluginPrefixAsync` implementieren | Offen | Unit Test |
| 7 | Logik | `PluginDefaultSettingsService.SaveProjectDefaultPluginPrefixAsync` implementieren | Offen | Unit Test |
| 8 | Logik | `PluginSelectionService.ResolveDevelopmentAutomationPluginWithProjectScopeAsync` implementieren | Offen | Unit Test |
| 9 | Logik | `EntwicklungsprozessService.ProzessStartenUndCliStartenAsync` implementieren (kombinierte Klone + CLI-Start) | Offen | Unit Test |
| 10 | Logik | `EntwicklungsprozessService.ProzessStartenUndCliStartenAsync` Fehlerrollback implementieren | Offen | Unit Test |
| 11 | Service | `PluginSelectionDialogService` Klasse anlegen (Facade) | Offen | — |
| 12 | Service | `PluginSelectionResult` Value Object anlegen | Offen | — |
| 13 | UI-Komponenten | `PluginSelectionDialog.xaml` anlegen (WPF UserControl) | Offen | — |
| 14 | UI-Komponenten | `PluginSelectionDialog.xaml.cs` Code-Behind implementieren | Offen | — |
| 15 | UI-Komponenten | `PluginSelectionDialogViewModel` Klasse anlegen | Offen | Unit Test |
| 16 | ViewModel | `TaskDetailViewModel.StartenCommand` anlegen | Offen | Unit Test |
| 17 | ViewModel | `TaskDetailViewModel.StartenAsync` Methode implementieren | Offen | Unit Test |
| 18 | ViewModel | `TaskDetailViewModel.PluginAendernCommand` anlegen | Offen | Unit Test |
| 19 | ViewModel | `TaskDetailViewModel.PluginWechselAsync` Methode implementieren | Offen | Unit Test |
| 20 | ViewModel | `TaskDetailViewModel.LadenAsync` erweitern (automatischer CLI-Neustart) | Offen | Unit Test |
| 21 | UI | `TaskDetailView.xaml` Ribbon: neue `StartenCommand` Button hinzufügen | Offen | E2E Test |
| 22 | UI | `TaskDetailView.xaml` Ribbon: neue `PluginAendernCommand` Button hinzufügen | Offen | E2E Test |
| 23 | UI | `TaskDetailView.xaml` Ribbon: alte `StatusGestartetSetzenCommand` Button entfernen | Offen | E2E Test |
| 24 | UI | `TaskDetailView.xaml` Ribbon: alte `CliStartenCommand` Button entfernen | Offen | E2E Test |
| 25 | Fehlerbehandlung | `TaskDetailViewModel.StartenAsync` Fehlerbehandlung implementieren (Dialog, MessageBox) | Offen | Unit Test |
| 26 | Fehlerbehandlung | `TaskDetailViewModel.PluginWechselAsync` Fehlerbehandlung implementieren (Dialog, MessageBox) | Offen | Unit Test |
| 27 | Tests | `AufgabeStatusTransitionTests.TestStatusTransitions_NeuToGestartet_Direct_Succeeds` schreiben | Offen | Unit Test |
| 28 | Tests | `AufgabeStatusTransitionTests.TestStatusValidation_GestartetToWartend_IsAllowed` schreiben | Offen | Unit Test |
| 29 | Tests | `AufgabeStatusTransitionTests.TestStatusValidation_GestartetToBeendet_IsAllowed` schreiben | Offen | Unit Test |
| 30 | Tests | `AufgabeServiceTests.TestStartenAsync_UpdatesStatusToGestartet` schreiben | Offen | Unit Test |
| 31 | Tests | `EntwicklungsprozessServiceTests.TestProzessStartenUndCliStartenAsync_Success` schreiben | Offen | Unit Test |
| 32 | Tests | `EntwicklungsprozessServiceTests.TestProzessStartenUndCliStartenAsync_RepositoryCloneFails_RollbackStatus` schreiben | Offen | Unit Test |
| 33 | Tests | `EntwicklungsprozessServiceTests.TestProzessStartenUndCliStartenAsync_CliStartFails_RollbackStatus` schreiben | Offen | Unit Test |
| 34 | Tests | `PluginDefaultSettingsServiceTests.TestGetProjectDefaultPluginPrefix_ReturnsStoredValue` schreiben | Offen | Unit Test |
| 35 | Tests | `PluginDefaultSettingsServiceTests.TestSaveProjectDefaultPluginPrefix_StoresInAppEinstellung` schreiben | Offen | Unit Test |
| 36 | Tests | `PluginSelectionServiceTests.TestResolvePluginWithProjectScope_UsesProjectDefault` schreiben | Offen | Unit Test |
| 37 | Tests | `TaskDetailViewModelTests.TestStartenCommand_CanExecute_StatusNeuNotCliRunning` schreiben | Offen | Unit Test |
| 38 | Tests | `TaskDetailViewModelTests.TestStartenAsync_ShowsDialogIfNoPluginSelected` schreiben | Offen | Unit Test |
| 39 | Tests | `TaskDetailViewModelTests.TestStartenAsync_SavesProjectDefaultIfCheckboxActivated` schreiben | Offen | Unit Test |
| 40 | Tests | `TaskDetailViewModelTests.TestStartenAsync_InvokesCombinedProcess_StartsCliUponSuccess` schreiben | Offen | Unit Test |
| 41 | Tests | `TaskDetailViewModelTests.TestPluginWechselCommand_CanExecute_CliRunning` schreiben | Offen | Unit Test |
| 42 | Tests | `TaskDetailViewModelTests.TestPluginWechselAsync_StopsCliAndStartsNew` schreiben | Offen | Unit Test |
| 43 | Tests | `TaskDetailViewModelTests.TestLoadAsync_AutoRestartsCli_StatusGestartetNoRunningProcess` schreiben | Offen | Unit Test |
| 44 | Tests | `AufgabeStatusTransitionTests.*` existierende Tests anpassen (alte Status entfernt) | Offen | Unit Test |
| 45 | Tests | `TaskDetailViewModelTests.ShowCliPanel_IsTrue_WhenStatusInArbeit` entfernen (Status existiert nicht mehr) | Offen | — |
| 46 | Tests | `AufgabeServiceTests.StartenAsync_*` bestehende Tests anpassen (Status Assertion) | Offen | Unit Test |
| 47 | Tests | `EntwicklungsprozessServiceTests.ProzessStartenAsync_*` bestehende Tests anpassen (Status Assertion) | Offen | Unit Test |
| 48 | E2E-Tests | `E2E_AufgabeStarten` schreiben: direkte Übergabe "Starten" mit Klone + CLI-Start | Offen | E2E Test |
| 49 | E2E-Tests | `E2E_PluginSelectionDialog` schreiben: Dialog-Anzeige bei fehlendem Plugin | Offen | E2E Test |
| 50 | E2E-Tests | `E2E_PluginProjectDefault` schreiben: Projekt-Standard-Speicherung | Offen | E2E Test |
| 51 | E2E-Tests | `E2E_PluginProjectDefault_NextTask` schreiben: nächste Aufgabe nutzt Projekt-Standard | Offen | E2E Test |
| 52 | E2E-Tests | `E2E_PluginWechsel` schreiben: Plugin-Wechsel mit Dialog + Prozess-Neustarts | Offen | E2E Test |
| 53 | E2E-Tests | `E2E_AutoStartCli` schreiben: automatischer CLI-Neustart bei Ansicht laden | Offen | E2E Test |
| 54 | E2E-Tests | `E2E_RibbonMenuItems` schreiben: neue/entfernte Buttons sichtbar | Offen | E2E Test |
| 55 | E2E-Tests | `E2E_TaskDetailNavigation.*` bestehende Tests anpassen (Button-Selektoren) | Offen | E2E Test |
| 56 | E2E-Tests | `E2E_CreateNewTaskNavigation.*` bestehende Tests anpassen (neuer Workflow) | Offen | E2E Test |
