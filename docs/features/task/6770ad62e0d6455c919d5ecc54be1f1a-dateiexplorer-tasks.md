# Tasks: Dateiexplorer für Aufgabendetailansicht

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Datenmodell | `TextDiffLine` Value Object in `Domain/ValueObjects` anlegen | Offen | — |
| 2 | Datenmodell | `InlineDiffSegment` Value Object in `Domain/ValueObjects` anlegen | Offen | — |
| 3 | Datenmodell | `FileTextDiff` Value Object in `Domain/ValueObjects` anlegen | Offen | — |
| 4 | Datenmodell | Enum `DateibrowserAnsichtsmodus` (`Standard`, `Vergleich`) in `App/ViewModels` anlegen | Offen | — |
| 5 | Logik | `ITextDiffService` Interface in `Application/Services` anlegen | Offen | — |
| 6 | Logik | `TextDiffService` (Zeilendiff inkl. Modified-Paarung + Inline-Segmente) implementieren | Offen | — |
| 7 | Logik | `IGitWorkspaceBrowserService` um `LoadWorkingTreeAsync` erweitern | Offen | — |
| 8 | Logik | `GitWorkspaceBrowserService.LoadWorkingTreeAsync` implementieren (Directory-Walk, `.git`-Ausschluss, Knoten-Obergrenze) | Offen | — |
| 9 | Logik | `FileExplorerViewModel` mit Zustand (Baum, Commits, Auswahl, Inhalt, Diff, Modus) anlegen | Offen | — |
| 10 | Logik | Commands `StandardAnsichtCommand`, `VergleichCommand`, `AktualisierenCommand` + `InitialisierenAsync`/`DateiLadenAsync`/`CommitAufklappenAsync` im `FileExplorerViewModel` | Offen | — |
| 11 | Logik | `TaskDetailViewModel`: `DetailAnsicht.Dateibrowser`, `IsFileExplorerViewSelected`, `ShowFileExplorerPanel`, `DateiViewCommand`, `FileExplorer`-Property, Konstruktor-Abhängigkeit, `WaehleAnsicht`/`Aufgabe`-Setter anpassen | Offen | — |
| 12 | UI | `DiffLineStatusToBrushConverter` in `App/Converters` anlegen und in `App.xaml` registrieren | Offen | — |
| 13 | UI | `DiffViewer`-UserControl (`App/Controls`) mit Zeilennummern, Statusfarben, Inline-`Run`s, `Lines`-DependencyProperty | Offen | — |
| 14 | UI | `FileExplorerView`-UserControl (`App/Views`): Mode-Buttons, `TreeView` (`HierarchicalDataTemplate`), `GridSplitter`, rechter Inhalt/`DiffViewer` | Offen | — |
| 15 | UI | `TaskDetailView.xaml`: „Dateien"-Button (`DateiViewButton`) + `FileExplorerView`-Panel einbinden | Offen | — |
| 16 | Konfiguration | DI-Registrierungen in `App.xaml.cs`: `IGitWorkspaceBrowserService`, `ITextDiffService`, `FileExplorerViewModel` | Offen | — |
| 17 | Tests | `TaskDetailViewModelTestFactory` um `FileExplorerViewModel`-Abhängigkeit erweitern | Offen | — |
| 18 | Tests | `TextDiffServiceTests` (identisch, Added, Removed, Modified+Inline, leer) | Offen | — |
| 19 | Tests | `GitWorkspaceBrowserServiceWorkingTreeTests` (Baumaufzählung, `.git`-Ausschluss, ungültiger Pfad) | Offen | — |
| 20 | Tests | `FileExplorerViewModelTests` (Standard-Laden, Dateiauswahl, Binär/zu groß, Vergleich, Commit-Aufklappen, Diff, Aktualisieren) | Offen | — |
| 21 | Tests | `TaskDetailViewModelTests`-Erweiterung (`DateiViewCommand`, `ShowFileExplorerPanel`-Gating) | Offen | — |
| 22 | E2E-Tests | `E2E_FileExplorer`: „Dateien"-Register umschalten, Baum + Mode-Buttons sichtbar | Offen | — |
