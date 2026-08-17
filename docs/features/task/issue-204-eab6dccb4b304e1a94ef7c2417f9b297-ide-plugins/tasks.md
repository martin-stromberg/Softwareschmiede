# Tasks: Split-Button-Muster für IDE-Öffnen-Funktion

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | UI-Komponente | `RibbonSplitButton.xaml` (Komponente) anlegen | Erledigt | — |
| 2 | UI-Komponente | `RibbonSplitButton.xaml.cs` (Code-Behind) implementieren | Erledigt | — |
| 3 | ViewModel | `TaskDetailViewModel`: Neue Property `KannIdeAuswaehlen` hinzufügen | Erledigt | `TaskDetailViewModelTests_IdeAuswahl.KannIdeAuswaehlen_WhenMultipleEntryPoints_ReturnsTrue` |
| 4 | ViewModel | `TaskDetailViewModel`: Neue Property `VerfuegbareEinstiegspunkte` hinzufügen (optional) | Erledigt | `TaskDetailViewModelTests_IdeAuswahl.VerfuegbareEinstiegspunkte_UpdatedAfterOeffneIde` |
| 5 | ViewModel | `TaskDetailViewModel`: Neues Kommando `OeffneIdeAuswahlCommand` anlegen | Erledigt | `TaskDetailViewModelTests_IdeAuswahl.OeffneIdeAuswahlCommand_ExecuteAsync_RuftOeffneIdeAuswahlAsyncAuf` |
| 6 | ViewModel | `TaskDetailViewModel`: Methode `OeffneIdeAuswahlAsync` implementieren | Erledigt | `TaskDetailViewModelTests_IdeAuswahl.OeffneIdeAuswahlCommand_ExecuteAsync_RuftOeffneIdeAuswahlAsyncAuf` |
| 7 | ViewModel | `TaskDetailViewModel`: Callback-Methode `waehleEntryPointAsync` implementieren | Erledigt | `TaskDetailViewModelTests_IdeAuswahl.WaehleEntryPointAsync_WithMultipleEntryPoints_ShowsDialogAndReturnsSelected` |
| 8 | ViewModel | `TaskDetailViewModel`: Hilfsmethode `AktualisiereVerfuegbareEinstiegspunkteAsync` implementieren | Erledigt | Implizit in `TaskDetailViewModelTests_IdeAuswahl` (alle Tests) |
| 9 | View | `TaskDetailView.xaml`: IDE-Button von `RibbonLargeButton` zu `RibbonSplitButton` ersetzen | Erledigt | `E2E_TaskDetailView_IdeAuswahl.IdeAuswahl_KeineEinstiegspunkteUndDropdownAbbruch_E2E` |
| 10 | Unit-Tests | `TaskDetailViewModelTests_IdeAuswahl`: Test für `OeffneIdeAuswahlCommand`-Ausführung | Erledigt | `TaskDetailViewModelTests_IdeAuswahl.OeffneIdeAuswahlCommand_ExecuteAsync_RuftOeffneIdeAuswahlAsyncAuf` |
| 11 | Unit-Tests | `TaskDetailViewModelTests_IdeAuswahl`: Test für `OeffneIdeAuswahlCommand` CanExecute | Erledigt | `TaskDetailViewModelTests_IdeAuswahl.OeffneIdeAuswahlCommand_CanExecute_WhenKannIdeOeffnenFalse_ReturnsFalse` |
| 12 | Unit-Tests | `TaskDetailViewModelTests_IdeAuswahl`: Tests für `KannIdeAuswaehlen` (1/≥2/0 Einstiegspunkte) | Erledigt | `TaskDetailViewModelTests_IdeAuswahl.KannIdeAuswaehlen_*` (3 Tests) |
| 13 | Unit-Tests | `TaskDetailViewModelTests_IdeAuswahl`: Test für `waehleEntryPointAsync` mit mehreren Einstiegspunkten | Erledigt | `TaskDetailViewModelTests_IdeAuswahl.WaehleEntryPointAsync_WithMultipleEntryPoints_ShowsDialogAndReturnsSelected` |
| 14 | Unit-Tests | `TaskDetailViewModelTests_IdeAuswahl`: Test für `waehleEntryPointAsync` Dialog-Abbruch | Erledigt | `TaskDetailViewModelTests_IdeAuswahl.WaehleEntryPointAsync_WithDialogAbort_ReturnsNull` |
| 15 | Unit-Tests | `TaskDetailViewModelTests_IdeAuswahl`: Test für DisplayName-Nutzung in Dialog | Erledigt | `TaskDetailViewModelTests_IdeAuswahl.WaehleEntryPointAsync_UsesDisplayNameInDialog` |
| 16 | Unit-Tests | `TaskDetailViewModelTests_IdeAuswahl`: Test für `VerfuegbareEinstiegspunkte`-Update | Erledigt | `TaskDetailViewModelTests_IdeAuswahl.VerfuegbareEinstiegspunkte_UpdatedAfterOeffneIde` |
| 17 | Unit-Tests | `TaskDetailViewModelTests_IdeAuswahl`: Test für Fehlerbehandlung ohne Einstiegspunkte | Erledigt | `TaskDetailViewModelTests_IdeAuswahl.OeffneIdeAuswahlAsync_WithNoEntryPoints_ShowsError` |
| 18 | Unit-Tests | Bestehende `TaskDetailViewModelTests`: Verifizieren dass `OeffneIdeCommand`-Tests funktionieren | Erledigt | `TaskDetailViewModelTests.OeffneIdeCommand_*` (existierende Tests bestehen) |
| 19 | E2E-Tests | `E2E_TaskDetailView_IdeAuswahl.cs`: Test für Fehler-Szenario (0 Einstiegspunkte) | Erledigt | `E2E_TaskDetailView_IdeAuswahl.IdeAuswahl_KeineEinstiegspunkteUndDropdownAbbruch_E2E` |
| 20 | E2E-Tests | `E2E_TaskDetailView_IdeAuswahl.cs`: Test für Dropdown-Sichtbarkeit bei mehreren Einstiegspunkten | Erledigt | `E2E_TaskDetailView_IdeAuswahl.IdeAuswahl_KeineEinstiegspunkteUndDropdownAbbruch_E2E` (Phase 3) |
| 21 | E2E-Tests | `E2E_TaskDetailView_IdeAuswahl.cs`: Test für Dialog-Abbruch-Szenario | Erledigt | `E2E_TaskDetailView_IdeAuswahl.IdeAuswahl_KeineEinstiegspunkteUndDropdownAbbruch_E2E` (Phase 3) |

---

## Zusammenfassung

Alle 21 Aufgaben aus dem Umsetzungsplan sind vollständig erledigt. Die Implementierung umfasst:

- **UI-Komponente:** `RibbonSplitButton` mit Haupt- und Dropdown-Button
- **ViewModel-Logik:** `TaskDetailViewModel` um Auswahl-Kommando und Callback erweitert
- **View-Anpassung:** `TaskDetailView.xaml` nutzt neue `RibbonSplitButton`
- **Unit-Tests:** 10 neue Unit-Tests in `TaskDetailViewModelTests_IdeAuswahl`, alle Tests in `TaskDetailViewModelTests` funktionieren
- **E2E-Tests:** 1 umfassender E2E-Test mit 3 Phasen (Fehler-Szenario, Dropdown-Sichtbarkeit, Dialog-Interaktion)

Testnachweis: Alle Tests bestehen grün.
