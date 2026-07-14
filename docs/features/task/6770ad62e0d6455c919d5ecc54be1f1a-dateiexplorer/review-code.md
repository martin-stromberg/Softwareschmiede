# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Zusammenfassung der Nacharbeitsprüfung

Die in dieser Runde durchgeführte Löschung der ungenutzten Kategorisierungslogik aus
`WorkspaceSnapshot` (`RootNodes`, `FlatFiles`, `CodeFiles`, `PlanningDocuments`, `ChangedFileCount`)
sowie der zugehörigen Duplikation (`IsPlanningDocumentPath`/`IsPlanningDocumentPathFallback`,
`IsCodeFilePath`, `ReadStatusEntriesAsync`) ist für den **kompilierten und ausgeführten Code
vollständig und sauber**:

- Kein produktiver Code (WPF-Views/XAML, ViewModels, Services) referenziert die entfernten
  Member mehr (global geprüft, keine Treffer).
- Kein kompilierter Test referenziert entfernte Member (App-/Application-Testordner sauber).
- Keine ungenutzten `using`-Direktiven in den geprüften neuen Dateien.
- Es wurde **keine vom WPF-UI benötigte Funktionalität** entfernt: Die WPF-Oberfläche
  (`FileExplorerView.xaml`) bindet ausschließlich `Name`/`Children` (WorkspaceFileNode) bzw.
  `Subject`/`ShortSha`/`IsLoadingFiles`/`ErrorMessage`/`Files` (BranchCommit) und hat die
  gelöschte Kategorisierung nie konsumiert.

Es verbleiben jedoch zwei Rest-Befunde derselben „berechnet-aber-nie-gelesen"-Familie, die im
Zuge dieser Aufräumrunde konsequenterweise ebenfalls hätten entfernt werden sollen, sowie eine
Beobachtung zu verwaisten (nicht kompilierten) Blazor-Testdateien.

## Befunde

### GitWorkspaceBrowserService.cs (GitWorkspaceBrowserService)

- **Toter Code** — `IncrementAncestorCounts` (Zeilen 544–556), aufgerufen aus
  `BuildCommitFileTree` (Zeile 501), pflegt pro Verzeichnisknoten die Aggregation
  `WorkspaceFileNode.ChangedFileCount`. Dieser Wert wird von **keinem kompilierten Konsumenten**
  gelesen: Die WPF-`TreeView`-Templates in `FileExplorerView.xaml` zeigen keinen Änderungszähler
  an, und die einzigen Lesezugriffe auf `ChangedFileCount` liegen in den **nicht kompilierten**
  Blazor-Tests unter `src/Softwareschmiede.Tests/Components/**` (per `<Compile Remove>` aus dem
  Build ausgeschlossen). Es handelt sich damit um genau dieselbe Kategorisierungs-/
  Aggregationslogik, die diese Runde aus `WorkspaceSnapshot` entfernt hat — nur auf Knotenebene
  übrig geblieben.

  Empfehlung: `IncrementAncestorCounts` und den Aufruf in `BuildCommitFileTree` (Zeile 501)
  entfernen. Die dann vollständig schreib-nur-genutzte Eigenschaft
  `WorkspaceFileNode.ChangedFileCount` (`src/Softwareschmiede/Domain/ValueObjects/WorkspaceFileNode.cs`,
  Zeile 37) ebenfalls entfernen (wird nur noch von den nicht kompilierten Blazor-Tests referenziert,
  daher build-neutral).

### TextDiffService.cs / FileTextDiff.cs (TextDiffService, FileTextDiff)

- **Toter Code / Speculative Generality** — `FileTextDiff.AddedCount`, `RemovedCount` und
  `ModifiedCount` sowie die zugehörige Zählerlogik in `TextDiffService.BuildDiff`
  (`addedCount`/`removedCount`/`modifiedCount`, Zeilen 19–21, 54, 61, 68 und Rückgabe Zeile 73)
  werden von der Anwendung nicht konsumiert: `FileExplorerViewModel.DateiLadenAsync` nutzt nur
  `diff.Lines` (Zeile 185–186). Die drei Zähler werden ausschließlich von `TextDiffServiceTests`
  ausgewertet.

  Empfehlung: Niedrige Priorität. Entweder als bewusst getesteten Bestandteil des Value-Objects
  belassen (dann so dokumentieren) oder — konsistent zur Aufräumzielsetzung dieser Runde — die
  drei Zähler aus `FileTextDiff`, der Zählerlogik in `BuildDiff` und den entsprechenden
  Test-Assertions entfernen.

### src/Softwareschmiede.Tests/Components/Pages/Aufgaben/* (verwaiste Blazor-bUnit-Tests)

- **Toter Code (Beobachtung, kein Build-/Testfehler)** — Die Dateien
  `AufgabeDetailWorkspacePreviewBunitTests.cs`, `AufgabeDetailFolgePromptTests.cs` und
  `AufgabeDetailGitActionsBunitTests.cs` initialisieren `WorkspaceSnapshot` weiterhin mit den in
  dieser Runde entfernten Membern (`RootNodes`, `FlatFiles`, `CodeFiles`, `PlanningDocuments`,
  `ChangedFileCount`) und rendern eine nicht mehr existierende Blazor-Komponente `AufgabeDetail`
  (kein `.razor` im Repo). **Wichtig:** Diese Dateien werden über
  `<Compile Remove="Components\**\*.cs" />` in `Softwareschmiede.Tests.csproj` (Zeile 41) vom
  Build ausgeschlossen — es entsteht daher **kein Kompilierfehler und kein fehlschlagender Test**.
  Es handelt sich um vorbestehende, aus der entfernten Blazor-UI verwaiste Testdateien
  (außerhalb des engeren Branch-Diffs), deren Referenzen durch das Trimmen von `WorkspaceSnapshot`
  nun endgültig ins Leere zeigen.

  Empfehlung: Optionaler, separater Aufräumschritt — das gesamte Verzeichnis
  `src/Softwareschmiede.Tests/Components/` löschen, da es toten Testcode einer nicht mehr
  vorhandenen Blazor-Oberfläche enthält. Für die aktuelle Nacharbeit nicht blockierend.

## Geprüfte Dateien

- `src/Softwareschmiede/Application/Services/GitWorkspaceBrowserService.cs`
- `src/Softwareschmiede/Application/Services/IGitWorkspaceBrowserService.cs`
- `src/Softwareschmiede/Application/Services/ITextDiffService.cs`
- `src/Softwareschmiede/Application/Services/TextDiffService.cs`
- `src/Softwareschmiede/Domain/ValueObjects/WorkspaceSnapshot.cs`
- `src/Softwareschmiede/Domain/ValueObjects/BranchCommit.cs`
- `src/Softwareschmiede/Domain/ValueObjects/FileTextDiff.cs`
- `src/Softwareschmiede/Domain/ValueObjects/InlineDiffSegment.cs`
- `src/Softwareschmiede/Domain/ValueObjects/TextDiffLine.cs`
- `src/Softwareschmiede/Domain/ValueObjects/WorkspaceFileNode.cs` (Kontext; nicht im Branch-Diff)
- `src/Softwareschmiede.App/ViewModels/FileExplorerViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/DateibrowserAnsichtsmodus.cs`
- `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs` (Integrationsdiff)
- `src/Softwareschmiede.App/Views/FileExplorerView.xaml`
- `src/Softwareschmiede.App/Views/FileExplorerView.xaml.cs`
- `src/Softwareschmiede.App/Controls/DiffViewer.xaml.cs`
- `src/Softwareschmiede.App/Converters/DiffLineStatusToBrushConverter.cs`
- `src/Softwareschmiede.App/App.xaml.cs` (DI-Registrierungen)
- `src/Softwareschmiede.Tests/Application/Services/GitWorkspaceBrowserServiceWorkingTreeTests.cs`
- `src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj` (Compile-Ausschluss verifiziert)
- Querprüfung: `src/Softwareschmiede.Tests/Components/Pages/Aufgaben/*` (nicht kompiliert)
