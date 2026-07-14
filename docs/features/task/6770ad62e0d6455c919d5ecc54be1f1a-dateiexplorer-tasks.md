# Tasks: Dateiexplorer für Aufgabendetailansicht

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Datenmodell | `TextDiffLine` Value Object in `Domain/ValueObjects` anlegen | Erledigt | `TextDiffServiceTests.BuildDiff_HinzugefuegteZeile_LiefertAddedStatus` |
| 2 | Datenmodell | `InlineDiffSegment` Value Object in `Domain/ValueObjects` anlegen | Erledigt | `TextDiffServiceTests.BuildDiff_GeaenderteZeile_LiefertModifiedMitInlineSegmenten` |
| 3 | Datenmodell | `FileTextDiff` Value Object in `Domain/ValueObjects` anlegen | Erledigt | `TextDiffServiceTests.BuildDiff_IdentischerInhalt_LiefertNurContextZeilen` |
| 4 | Datenmodell | Enum `DateibrowserAnsichtsmodus` (`Standard`, `Vergleich`) in `App/ViewModels` anlegen | Erledigt | `FileExplorerViewModelTests.VergleichCommand_LaedtCommitsAusSnapshot` |
| 5 | Logik | `ITextDiffService` Interface in `Application/Services` anlegen | Erledigt | `FileExplorerViewModelTests.DateiAuswahl_Vergleich_ErzeugtDiffZeilen` |
| 6 | Logik | `TextDiffService` (Zeilendiff inkl. Modified-Paarung + Inline-Segmente) implementieren | Erledigt | `TextDiffServiceTests` (alle 5 Tests) |
| 7 | Logik | `IGitWorkspaceBrowserService` um `LoadWorkingTreeAsync` erweitern | Erledigt | `GitWorkspaceBrowserServiceWorkingTreeTests.LoadWorkingTreeAsync_ListetDateienUndVerzeichnisse` |
| 8 | Logik | `GitWorkspaceBrowserService.LoadWorkingTreeAsync` implementieren (Directory-Walk, `.git`-Ausschluss, Knoten-Obergrenze) | Erledigt | `GitWorkspaceBrowserServiceWorkingTreeTests.LoadWorkingTreeAsync_SchliesstGitVerzeichnisAus` |
| 9 | Logik | `FileExplorerViewModel` mit Zustand (Baum, Commits, Auswahl, Inhalt, Diff, Modus) anlegen | Erledigt | `FileExplorerViewModelTests.Standard_LaedtWurzelknotenUeberWorkingTree` |
| 10 | Logik | Commands `StandardAnsichtCommand`, `VergleichCommand`, `AktualisierenCommand` + `InitialisierenAsync`/`DateiLadenAsync`/`CommitAufklappenAsync` im `FileExplorerViewModel` | Erledigt | `FileExplorerViewModelTests.CommitAufklappen_LaedtGeaenderteDateien`, `AktualisierenCommand_LaedtAktuellenModusNeu` |
| 11 | Logik | `TaskDetailViewModel`: `DetailAnsicht.Dateibrowser`, `IsFileExplorerViewSelected`, `ShowFileExplorerPanel`, `DateiViewCommand`, `FileExplorer`-Property, Konstruktor-Abhängigkeit, `WaehleAnsicht`/`Aufgabe`-Setter anpassen | Erledigt | `TaskDetailViewModelTests.DateiViewCommand_SetztFileExplorerAnsicht`, `ShowFileExplorerPanel_NurBeiVorhandenemKlonPfad` |
| 12 | UI | `DiffLineStatusToBrushConverter` in `App/Converters` anlegen und in `App.xaml` registrieren | Erledigt | Kein direkter Test (Nutzung im `DiffViewer`, indirekt über `E2E_FileExplorer`) |
| 13 | UI | `DiffViewer`-UserControl (`App/Controls`) mit Zeilennummern, Statusfarben, Inline-`Run`s, `Lines`-DependencyProperty | Erledigt | Kein direkter Test (indirekt über `E2E_FileExplorer.DateiViewButton_ZeigtExplorerMitBaumUndModeButtons_E2E`) |
| 14 | UI | `FileExplorerView`-UserControl (`App/Views`): Mode-Buttons, `TreeView` (`HierarchicalDataTemplate`), `GridSplitter`, rechter Inhalt/`DiffViewer` | Erledigt | `E2E_FileExplorer.DateiViewButton_ZeigtExplorerMitBaumUndModeButtons_E2E` |
| 15 | UI | `TaskDetailView.xaml`: „Dateien"-Button (`DateiViewButton`) + `FileExplorerView`-Panel einbinden | Erledigt | `E2E_FileExplorer.DateiViewButton_ZeigtExplorerMitBaumUndModeButtons_E2E` |
| 16 | Konfiguration | DI-Registrierungen in `App.xaml.cs`: `IGitWorkspaceBrowserService`, `ITextDiffService`, `FileExplorerViewModel` | Erledigt | Kein direkter Test (App-Start-Abhängigkeit, indirekt über `E2E_FileExplorer`) |
| 17 | Tests | `TaskDetailViewModelTestFactory` um `FileExplorerViewModel`-Abhängigkeit erweitern | Erledigt | `TaskDetailViewModelTests` (kompiliert/läuft über die Factory) |
| 18 | Tests | `TextDiffServiceTests` (identisch, Added, Removed, Modified+Inline, leer) | Erledigt | `TextDiffServiceTests` (5 Tests vorhanden) |
| 19 | Tests | `GitWorkspaceBrowserServiceWorkingTreeTests` (Baumaufzählung, `.git`-Ausschluss, ungültiger Pfad) | Erledigt | `GitWorkspaceBrowserServiceWorkingTreeTests` (3 Tests vorhanden) |
| 20 | Tests | `FileExplorerViewModelTests` (Standard-Laden, Dateiauswahl, Binär/zu groß, Vergleich, Commit-Aufklappen, Diff, Aktualisieren) | Erledigt | `FileExplorerViewModelTests` (7 Tests vorhanden) |
| 21 | Tests | `TaskDetailViewModelTests`-Erweiterung (`DateiViewCommand`, `ShowFileExplorerPanel`-Gating) | Erledigt | `TaskDetailViewModelTests.DateiViewCommand_SetztFileExplorerAnsicht`, `ShowFileExplorerPanel_NurBeiVorhandenemKlonPfad` |
| 22 | E2E-Tests | `E2E_FileExplorer`: „Dateien"-Register umschalten, Baum + Mode-Buttons sichtbar | Erledigt | `E2E_FileExplorer.DateiViewButton_ZeigtExplorerMitBaumUndModeButtons_E2E` |
