# Plan-Review

## Ergebnis

**Status:** Vollständig umgesetzt

## Umgesetzte Planelemente

### Neue Value Objects (`Domain/ValueObjects`)

- [x] `TextDiffLine` (record) — angelegt: `Content`, `Status` (`DiffLineStatus`), `OldLineNumber` (`int?`), `NewLineNumber` (`int?`), `InlineSegments` (`IReadOnlyList<InlineDiffSegment>`)
- [x] `InlineDiffSegment` (record) — angelegt: `Text`, `IsChanged`
- [x] `FileTextDiff` (record) — angelegt: `Lines`, `AddedCount`, `RemovedCount`, `ModifiedCount`

### Enum

- [x] `DateibrowserAnsichtsmodus` (`App/ViewModels`) — angelegt mit Werten `Standard`, `Vergleich`

### Application-Layer Services

- [x] `ITextDiffService` (`Application/Services`) — Interface mit `BuildDiff(string?, string?)`
- [x] `TextDiffService` (`Application/Services`) — LCS-basierter Zeilendiff inkl. Modified-Paarung und Präfix/Suffix-Inline-Segmenten
- [x] `IGitWorkspaceBrowserService.LoadWorkingTreeAsync(string, CancellationToken)` — neue Interface-Methode vorhanden
- [x] `GitWorkspaceBrowserService.LoadWorkingTreeAsync` — Directory-Walk mit `.git`-Ausschluss (`GitDirectoryName`), Knoten-Obergrenze (`MaxWorkingTreeNodeCount`), Wiederverwendung von `SortNodes`

### Presentation Model

- [x] `FileExplorerViewModel` (`App/ViewModels`) — angelegt: Zustand (`Wurzelknoten`, `CommitGruppen`, `DiffZeilen`, `AusgewaehlterKnoten`, `DateiInhalt`, `AktuellerModus`)
- [x] Commands `StandardAnsichtCommand`, `VergleichCommand`, `AktualisierenCommand` — vorhanden
- [x] Methoden `InitialisierenAsync`, `CommitAufklappenAsync`, `DateiLadenAsync` (privat) — vorhanden

### UI-Komponenten

- [x] `DiffLineStatusToBrushConverter` (`App/Converters`) — grün/rot/orange/transparent, in `App.xaml` registriert
- [x] `DiffViewer` (`App/Controls`) — `ItemsControl` mit Zeilennummernspalten, Status-Hintergrund, Inline-Segmente, `Lines`-DependencyProperty
- [x] `FileExplorerView` (`App/Views`) — Mode-Buttons (Standard/Vergleich/Aktualisieren), `TreeView` mit `HierarchicalDataTemplate`, `GridSplitter`, rechter Inhalt/`DiffViewer`

### Änderungen an bestehenden Klassen

- [x] `TaskDetailViewModel` — `DetailAnsicht.Dateibrowser`, `IsFileExplorerViewSelected`, `ShowFileExplorerPanel`, `DateiViewCommand`, `FileExplorer`-Property, Konstruktor-Abhängigkeit `FileExplorerViewModel`, `WaehleAnsicht`-Behandlung inkl. Fallback auf `Info`, `Aufgabe`-Setter feuert `ShowFileExplorerPanel` und ruft `InitialisierenAsync`
- [x] `TaskDetailView.xaml` — „Dateien"-Button (`AutomationProperties.Name="DateiViewButton"`) + `FileExplorerView`-Panel mit `DataContext="{Binding FileExplorer}"`
- [x] `App.xaml` — `DiffLineStatusToBrushConverter` registriert
- [x] `App.xaml.cs` — `AddScoped<IGitWorkspaceBrowserService, GitWorkspaceBrowserService>`, `AddSingleton<ITextDiffService, TextDiffService>`, `AddTransient<FileExplorerViewModel>`

### Tests

- [x] `TaskDetailViewModelTestFactory` — um `FileExplorerViewModel` (mit gemockten `IGitWorkspaceBrowserService`/`ITextDiffService`) erweitert
- [x] `TextDiffServiceTests` — 5 geplante Tests vorhanden
- [x] `GitWorkspaceBrowserServiceWorkingTreeTests` — 3 geplante Tests vorhanden
- [x] `FileExplorerViewModelTests` — 7 geplante Tests vorhanden
- [x] `TaskDetailViewModelTests`-Erweiterung — `DateiViewCommand_SetztFileExplorerAnsicht`, `ShowFileExplorerPanel_NurBeiVorhandenemKlonPfad`
- [x] `E2E_FileExplorer` — `DateiViewButton_ZeigtExplorerMitBaumUndModeButtons_E2E`

## Offene Aufgaben

Keine. Alle 22 Aufgaben der Tasks-Datei sind im Code auffindbar und vollständig umgesetzt.

## Hinweise

- Der Plan wählte bewusst die Wiederverwendung von `WorkspaceFileNode`/`IGitWorkspaceBrowserService` statt der in der Bestandsaufnahme genannten neuen Objekte (`FileTreeNode`, `DateibrowserService`, `GitDiffParserService`, `CommitDiffGroup`, `FileChange`). Diese sind daher plankonform **nicht** angelegt — kein Mangel.
- Die Aufgaben 12, 13 und 16 (Converter, `DiffViewer`, DI-Registrierung) haben keinen direkten Unit-Test; sie werden indirekt durch den E2E-Test `E2E_FileExplorer` bzw. den App-Start abgedeckt. Das entspricht der Planvorgabe für testarme, rein deklarative UI-/DI-Elemente.
- Dieses Review prüft ausschließlich Plan-Vollständigkeit (Vorhandensein im Code), keine Testausführung. Ein Build-/Testlauf zur Bestätigung der grünen Tests wurde im Rahmen dieses Reviews nicht durchgeführt.
- Der `TextDiffService` implementiert das geplante Präfix/Suffix-basierte Inline-Highlighting (Wortabschnitts-Granularität); das feinere Token-/LCS-basierte Inline-Highlighting ist plangemäß Folgeaufgabe und nicht Teil dieser Umsetzung.
