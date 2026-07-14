# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### TextDiffService.cs (TextDiffService)

- **Efficiency / Speculative Generality** — `ComputeLineOperations` (Zeilen 77-129) allokiert die vollständige LCS-DP-Matrix `int[n + 1, m + 1]`. `BuildDiff` wird aus `FileExplorerViewModel.DateiLadenAsync` mit dem Commit-Vorschauinhalt aufgerufen, der in `GitWorkspaceBrowserService.LoadCommitPreviewAsync` erst bei `MaxInlineBytes = 1_048_576` (1 MB) abgeschnitten wird. Eine 1-MB-Quelldatei mit z. B. 30.000 Zeilen führt zu einer Matrix von 30.000 × 30.000 `int` ≈ 3,6 GB und damit zu `OutOfMemoryException`/LOH-Thrashing. Es existiert keine Zeilen-Obergrenze.

  Empfehlung: Vor der O(n·m)-LCS-Berechnung eine Zeilenanzahl-Obergrenze prüfen (analog zu `MaxWorkingTreeNodeCount`) und bei Überschreitung auf einen einfachen, speicherschonenden Fallback (z. B. reiner Added/Removed-Blockdiff oder Hinweistext „Diff zu groß") ausweichen, oder einen speicher-effizienten Diff-Algorithmus (Myers mit linearem Speicher) verwenden.

### FileExplorerViewModel.cs (FileExplorerViewModel)

- **Fehlerbehandlung** — `InitialisierenAsync` (Zeilen 98-115) und `AktualisierenAsync` (Zeilen 214-227) setzen `_ausgewaehlterKnoten = null` direkt über das Backing-Field (unter Umgehung des Setters, der `_dateiLadenCts` abbricht). Ein zum Zeitpunkt des Aufrufs noch laufender `DateiLadenAsync`-Vorgang wird dadurch **nicht** abgebrochen. Kehrt dessen Git-Aufruf danach zurück, überschreibt der Dispatcher-Callback `DateiInhalt`/`DiffZeilen` mit dem veralteten Inhalt der vorherigen Auswahl – obwohl `InitialisierenAsync`/`AktualisierenAsync` diese gerade geleert hat (z. B. beim Wechsel der Aufgabe).

  Empfehlung: In `InitialisierenAsync` und `AktualisierenAsync` den laufenden Ladevorgang wie im `AusgewaehlterKnoten`-Setter über `_dateiLadenCts?.Cancel()`/`Dispose()` abbrechen, bevor der Zustand zurückgesetzt wird.

- **Fehlerbehandlung** — `DateiLadenAsync` (Zeilen 196-199): Der allgemeine `catch (Exception)`-Block protokolliert den Fehler nur, lässt aber `DateiInhalt` und `DiffZeilen` unverändert. Schlägt das Laden der Vorschau fehl, bleibt der Inhalt der zuvor gewählten Datei sichtbar, ohne dass der Nutzer den Fehler erkennt (irreführende, veraltete Anzeige).

  Empfehlung: Im `catch` (über den Dispatcher) `DateiInhalt` auf einen aussagekräftigen Hinweistext setzen und `ClearDiffZeilen()` aufrufen.

- **Fehlerbehandlung** — `CommitAufklappenAsync` (Zeilen 144-149): Schlägt das Laden der Commit-Dateien fehl, wird `commit.ErrorMessage` gesetzt, aber die WPF-Ansicht (`FileExplorerView.xaml`) bindet weder `ErrorMessage` noch `IsLoadingFiles`, und `BranchCommit` implementiert kein `INotifyPropertyChanged`. Der Fehler wird dadurch in der WPF-Oberfläche nie sichtbar – der Commit erscheint dem Nutzer einfach als leer. (In der bestehenden Blazor-`CommitTreePresenter`-Ansicht werden diese Felder genutzt; im neuen WPF-Kontext sind sie wirkungslos.)

  Empfehlung: Ladefehler und Lade-/Leerzustand eines Commits in der WPF-Ansicht sichtbar machen (z. B. Fehler-/Ladehinweis im `HierarchicalDataTemplate` des `BranchCommit`), oder bewusst dokumentieren, dass diese Zustände im WPF-Explorer nicht dargestellt werden.

### BranchCommit.cs (BranchCommit)

- **Feature Envy / Temporäres Feld (WPF-Kontext)** — Die View-State-Felder `IsExpanded`, `IsLoadingFiles`, `ErrorMessage` (Zeilen 18-27) werden von `FileExplorerViewModel.CommitAufklappenAsync` geschrieben, aber ohne `INotifyPropertyChanged` und ohne Bindung in `FileExplorerView.xaml` im WPF-Explorer nie beobachtet. Sie stammen aus der bestehenden Blazor-Nutzung; im neu hinzugefügten WPF-Pfad sind die Zuweisungen wirkungslos.

  Empfehlung: Entweder die Felder in der WPF-Ansicht tatsächlich binden/darstellen (siehe Befund zu `CommitAufklappenAsync`) oder das absichtliche Nichtdarstellen dokumentieren; keine wirkungslosen Zustandszuweisungen im WPF-ViewModel belassen.

### TaskDetailViewModel.cs (TaskDetailViewModel)

- **Efficiency** — `ShowFileExplorerPanel` (Zeile 333) ruft bei jedem Property-Zugriff synchron `Directory.Exists(_aufgabe.LokalerKlonPfad)` auf. Da die Property im `Aufgabe`-Setter und über Binding-Refreshs mehrfach ausgewertet wird, entstehen wiederholte Dateisystem-Zugriffe auf dem UI-Thread.

  Empfehlung: Das Ergebnis der `Directory.Exists`-Prüfung einmalig beim Setzen von `Aufgabe`/`LokalerKlonPfad` in ein Feld cachen und dieses in der Property zurückgeben.

## Geprüfte Dateien

- `src/Softwareschmiede/Application/Services/GitWorkspaceBrowserService.cs` (neu: `LoadWorkingTreeAsync`, `WalkWorkingTreeDirectory`)
- `src/Softwareschmiede/Application/Services/IGitWorkspaceBrowserService.cs`
- `src/Softwareschmiede/Application/Services/ITextDiffService.cs`
- `src/Softwareschmiede/Application/Services/TextDiffService.cs`
- `src/Softwareschmiede/Domain/ValueObjects/BranchCommit.cs`
- `src/Softwareschmiede/Domain/ValueObjects/FileTextDiff.cs`
- `src/Softwareschmiede/Domain/ValueObjects/InlineDiffSegment.cs`
- `src/Softwareschmiede/Domain/ValueObjects/TextDiffLine.cs`
- `src/Softwareschmiede.App/App.xaml`
- `src/Softwareschmiede.App/App.xaml.cs`
- `src/Softwareschmiede.App/Themes/DarkTheme.xaml`
- `src/Softwareschmiede.App/Themes/LightTheme.xaml`
- `src/Softwareschmiede.App/Controls/DiffViewer.xaml`
- `src/Softwareschmiede.App/Controls/DiffViewer.xaml.cs`
- `src/Softwareschmiede.App/Converters/DiffLineStatusToBrushConverter.cs`
- `src/Softwareschmiede.App/ViewModels/DateibrowserAnsichtsmodus.cs`
- `src/Softwareschmiede.App/ViewModels/FileExplorerViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs`
- `src/Softwareschmiede.App/Views/FileExplorerView.xaml`
- `src/Softwareschmiede.App/Views/FileExplorerView.xaml.cs`
- `src/Softwareschmiede.App/Views/TaskDetailView.xaml`
- `src/Softwareschmiede.Tests/App/Converters/DiffLineStatusToBrushConverterTests.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/FileExplorerViewModelTests.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests_ZeitgesteuerterPrompt.cs`
- `src/Softwareschmiede.Tests/Application/Services/GitWorkspaceBrowserServiceTests.cs`
- `src/Softwareschmiede.Tests/Application/Services/GitWorkspaceBrowserServiceWorkingTreeTests.cs`
- `src/Softwareschmiede.Tests/Application/Services/TextDiffServiceTests.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_FileExplorer.cs`
- `src/Softwareschmiede.Tests/Helpers/TaskDetailViewModelTestFactory.cs`
