# Tasks: Export der Rohausgabe der CLI

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Dialog-Abstraktion | `IDialogService.ShowSaveFileDialogAsync(title, filter, defaultFileName, initialDirectory, ct)` ergänzen | Erledigt | `TaskDetailViewModelTests_CliRawExport.ExportCliRawAsync_ShouldAbort_WhenDialogCancelled` |
| 2 | Dialog-Implementierung | `WpfDialogService.ShowSaveFileDialogAsync(...)` mit `SaveFileDialog` auf UI-Dispatcher implementieren | Erledigt | `CliRawExport_ErstelltRawDateiMitCliOutput_HappyPath_E2E` |
| 3 | ViewModel | Eigenschaft `TaskDetailViewModel.KannCliRawExportieren` ergänzen | Erledigt | `TaskDetailViewModelTests_CliRawExport.ExportCliRawCommand_CanExecute_WhenAufgabeGeladen` |
| 4 | ViewModel | Command `ExportCliRawCommand` im Konstruktor registrieren (`AsyncRelayCommand`) | Erledigt | `TaskDetailViewModelTests_CliRawExport.ExportCliRawCommand_CanExecute_WhenAufgabeGeladen` |
| 5 | ViewModel | `TaskDetailViewModel.ExportCliRawAsync(CancellationToken)` ergänzen (Dialog öffnen, Snapshot laden, filtern, schreiben) | Erledigt | `TaskDetailViewModelTests_CliRawExport.ExportCliRawAsync_ShouldWriteOnlyCliOutput_InChronologicalOrder` |
| 6 | Validierung | Export-Abbruch (`null`/leer) beendet Vorgang ohne Fehleranzeige | Erledigt | `TaskDetailViewModelTests_CliRawExport.ExportCliRawAsync_ShouldAbort_WhenDialogCancelled` |
| 7 | Validierung | Fallback-Prüfung `.raw`-Dateiendung im ViewModel | Erledigt | Kein direkter Test |
| 8 | Validierung | Exportinhalt enthält ausschließlich `ProtokollTyp.CliOutput` in persistierter Reihenfolge | Erledigt | `TaskDetailViewModelTests_CliRawExport.ExportCliRawAsync_ShouldWriteOnlyCliOutput_InChronologicalOrder` |
| 9 | Fehlerbehandlung | Dateischreibfehler werden geloggt und setzen `FehlerMeldung` | Erledigt | `TaskDetailViewModelTests_CliRawExport.ExportCliRawAsync_ShouldSetFehlerMeldung_WhenFileWriteFails` |
| 10 | Dateiformat | Schreiben als UTF-8 ohne BOM | Erledigt | `TaskDetailViewModelTests_CliRawExport.ExportCliRawAsync_ShouldWriteOnlyCliOutput_InChronologicalOrder` |
| 11 | View | `TaskDetailView.xaml`: neuer CLI-Ribbon-Button mit `AutomationName="CliRawExport"` und Binding `ExportCliRawCommand` | Erledigt | `TaskDetailViewTests.Xaml_ContainsCliRawExportButton` |
| 12 | E2E-View-Objekt | `Softwareschmiede.Tests.E2E.Views.TaskDetailView.ExportCliRaw(...)` ergänzen | Erledigt | `CliRawExport_ErstelltRawDateiMitCliOutput_HappyPath_E2E` |
| 13 | E2E-View-Objekt | `Softwareschmiede.Tests.E2E.Views.TaskDetailView.HandleSaveFileDialog(...)` ergänzen | Erledigt | `CliRawExport_AbbruchErzeugtKeineDateiUndKeinenFehlerbanner_E2E` |
| 14 | Unit-Test | `ExportCliRawCommand_CanExecute_WhenAufgabeGeladen` ergänzen | Erledigt | `TaskDetailViewModelTests_CliRawExport.ExportCliRawCommand_CanExecute_WhenAufgabeGeladen` |
| 15 | Unit-Test | `ExportCliRawAsync_ShouldAbort_WhenDialogCancelled` ergänzen | Erledigt | `TaskDetailViewModelTests_CliRawExport.ExportCliRawAsync_ShouldAbort_WhenDialogCancelled` |
| 16 | Unit-Test | `ExportCliRawAsync_ShouldWriteOnlyCliOutput_InChronologicalOrder` ergänzen | Erledigt | `TaskDetailViewModelTests_CliRawExport.ExportCliRawAsync_ShouldWriteOnlyCliOutput_InChronologicalOrder` |
| 17 | Unit-Test | `ExportCliRawAsync_ShouldSetFehlerMeldung_WhenFileWriteFails` ergänzen | Erledigt | `TaskDetailViewModelTests_CliRawExport.ExportCliRawAsync_ShouldSetFehlerMeldung_WhenFileWriteFails` |
| 18 | UI-/Strukturtest | `TaskDetailViewTests.Xaml_ContainsCliRawExportButton` ergänzen | Erledigt | `TaskDetailViewTests.Xaml_ContainsCliRawExportButton` |
| 19 | E2E-Test | `E2E_CliRawExport.cs`: `CliRawExport_ErstelltRawDateiMitCliOutput_HappyPath_E2E` ergänzen | Erledigt | `CliRawExport_ErstelltRawDateiMitCliOutput_HappyPath_E2E` |
| 20 | E2E-Test | `E2E_CliRawExport.cs`: `CliRawExport_AbbruchErzeugtKeineDateiUndKeinenFehlerbanner_E2E` ergänzen | Erledigt | `CliRawExport_AbbruchErzeugtKeineDateiUndKeinenFehlerbanner_E2E` |
| 21 | Bestehende Tests | `TaskDetailViewTests` um Assertions für den neuen Export-Button erweitern | Erledigt | `TaskDetailViewTests.Xaml_ContainsCliRawExportButton` |
