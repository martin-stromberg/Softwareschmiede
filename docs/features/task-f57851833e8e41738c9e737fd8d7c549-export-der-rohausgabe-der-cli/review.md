# Plan-Review

## Ergebnis

**Status:** Vollständig umgesetzt

## Umgesetzte Planelemente

- [x] `IDialogService` (Interface) — um Methode `ShowSaveFileDialogAsync(...)` erweitert.
- [x] Methode `ShowSaveFileDialogAsync` in `WpfDialogService` — vorhanden (UI-Dispatcher + `SaveFileDialog` + `null` bei Abbruch).
- [x] Feld/Eigenschaft `KannCliRawExportieren` in `TaskDetailViewModel` — vorhanden.
- [x] Methode `ExportCliRawAsync(CancellationToken)` in `TaskDetailViewModel` — vorhanden (Dialog, Snapshot via `ProtokollService.GetByAufgabeAsync`, Filter auf `ProtokollTyp.CliOutput`, Dateischreiben).
- [x] Konstruktor `TaskDetailViewModel(...)` — registriert `ExportCliRawCommand`.
- [x] Validierungsregel `.raw`-Endung — vorhanden (`FehlerMeldung` bei ungültigem Zielpfad).
- [x] Fehlerpfad beim Export — vorhanden (Exception-Logging via `_logger`, `FehlerMeldung` wird gesetzt).
- [x] `TaskDetailView.xaml` (View) — CLI-Ribbon enthält Export-Button mit `AutomationName="CliRawExport"` und Binding `ExportCliRawCommand`.
- [x] `Softwareschmiede.Tests.E2E.Views.TaskDetailView` — Methoden `ExportCliRaw(...)` und `HandleSaveFileDialog(...)` vorhanden.
- [x] Unit-Test `ExportCliRawCommand_CanExecute_WhenAufgabeGeladen` — vorhanden.
- [x] Unit-Test `ExportCliRawAsync_ShouldAbort_WhenDialogCancelled` — vorhanden.
- [x] Unit-Test `ExportCliRawAsync_ShouldWriteOnlyCliOutput_InChronologicalOrder` — vorhanden.
- [x] Unit-Test `ExportCliRawAsync_ShouldSetFehlerMeldung_WhenFileWriteFails` — vorhanden.
- [x] Strukturtest `Xaml_ContainsCliRawExportButton` — vorhanden.
- [x] E2E-Test `CliRawExport_ErstelltRawDateiMitCliOutput_HappyPath_E2E` — vorhanden.
- [x] E2E-Test `CliRawExport_AbbruchErzeugtKeineDateiUndKeinenFehlerbanner_E2E` — vorhanden.

## Hinweise

- Die im Plan geforderten Planelemente sind im Codebestand unter `src/` vollständig vorhanden.
- Für die `.raw`-Fallback-Validierung existiert derzeit kein dedizierter Unit-Test; die Logik ist jedoch im ViewModel implementiert.
