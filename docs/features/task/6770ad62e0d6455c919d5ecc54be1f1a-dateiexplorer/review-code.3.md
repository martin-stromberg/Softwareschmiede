# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Überprüfung der 5 zuletzt behobenen Befunde

Alle fünf Fixes der vorherigen Runde sind korrekt, vollständig und ohne erkennbare Regression umgesetzt:

1. **TextDiffService OOM-Risiko** — `MaxLcsDiffLineCount = 5_000` schaltet in `ComputeLineOperations` vor der O(n·m)-Matrix-Allokation auf den positionsbasierten `ComputeBlockOperations`-Fallback um. Test `BuildDiff_ZeilenanzahlUeberSchwelle_LiefertBlockdiffOhneVolleLcsMatrix` deckt den Pfad ab. Korrekt.
2. **FileExplorerViewModel: umgangener Setter** — `InitialisierenAsync` und `AktualisierenAsync` brechen jetzt vor `_ausgewaehlterKnoten = null` explizit den laufenden `_dateiLadenCts` ab und feuern `OnPropertyChanged(nameof(AusgewaehlterKnoten))`. Tests `InitialisierenAsync_BrichtLaufendenDateiLadevorgangAb` und `AktualisierenCommand_BrichtLaufendenDateiLadevorgangAb` bestätigen den Token-Abbruch. Korrekt.
3. **DateiLadenAsync catch-Block** — setzt im Fehlerfall `DateiInhalt = "Datei konnte nicht geladen werden."` und ruft `ClearDiffZeilen()` innerhalb des Dispatchers auf. Test `DateiLadenAsync_FehlerBeimLaden_ZeigtHinweisUndLeertDiffZeilen` deckt es ab. Korrekt.
4. **BranchCommit INotifyPropertyChanged** — implementiert mit change-guarded Settern für `IsLoadingFiles`/`ErrorMessage`; `CommitAufklappenAsync` setzt beide, und `FileExplorerView.xaml` (Zeilen 98/100–105) bindet sie über `BoolToVisibilityConverter`/`NullOrEmptyToVisibilityConverter` (beide in `AppConverters.cs` vorhanden). Korrekt.
5. **ShowFileExplorerPanel-Caching** — im `Aufgabe`-Setter wird `_showFileExplorerPanel` einmalig via `Directory.Exists` ermittelt und gecacht; die Property liefert nur noch das Feld. Test `ShowFileExplorerPanel_WertBleibtGecachtNachdemVerzeichnisNachtraeglichGeloeschtWurde` bestätigt das Cache-Verhalten. Korrekt.

Die nachstehenden Befunde sind **keine** Regressionen der Fixes, sondern durch das vollständige Review des Gesamtzustands aufgedeckte, vorbestehende Qualitätsprobleme im Feature-Code.

## Befunde

### GitWorkspaceBrowserService.cs (GitWorkspaceBrowserService)

- **Speculative Generality / nicht konsumierte Berechnung** — `LoadSnapshotAsync` → `BuildSnapshot` berechnet auf jedem Laden im Vergleichsmodus die Eigenschaften `RootNodes`, `FlatFiles`, `CodeFiles`, `PlanningDocuments`, `ChangedFileCount` und `CommitCount` des `WorkspaceSnapshot`. Einziger Produktivkonsument von `LoadSnapshotAsync` ist `FileExplorerViewModel.LadeCommitsAsync` (Zeile 272), und der liest ausschließlich `snapshot.BranchCommits` (Zeile 276). Alle übrigen Snapshot-Eigenschaften werden nur von Tests (`GitWorkspaceBrowserServiceTests`) gelesen, nie von der UI. Damit laufen der zusätzliche `git status`-Aufruf (`ReadStatusEntriesAsync`), der komplette Baumaufbau (`InsertNode`/`IncrementAncestorCounts`/`SortNodes`) sowie die gesamte Klassifikationslogik (`IsPlanningDocumentNode`, `IsPlanningDocumentPath`, `IsPlanningDocumentPathFallback`, `IsCodeFileNode`, `IsCodeFilePath`, das Feld `CodeExtensions`) bei jedem Vergleichsmodus-Aufruf, ohne dass ihr Ergebnis jemals angezeigt oder anderweitig verwendet wird.

  Empfehlung: Gegen `plan.md`/`requirement.md` prüfen, ob diese Kategorisierung noch für das Feature vorgesehen ist. Falls nein: die unbenutzte Snapshot-Befüllung und die zugehörigen Hilfsmethoden (`ReadStatusEntriesAsync`, `BuildSnapshot`-Klassifikation, `IsPlanningDocument*`, `IsCodeFile*`, `CodeExtensions`) samt der nur diese Ausgabe prüfenden Tests entfernen, um Laufzeitkosten und Wartungsfläche zu senken. Falls doch benötigt: die Eigenschaften in der View anbinden.

- **Doppelter Code** — `IsPlanningDocumentPath` (Zeilen 758–779) und `IsPlanningDocumentPathFallback` (Zeilen 781–797) enthalten dieselbe Klassifikationslogik (Endung `.md` plus Präfix `docs/requirements|architecture|improvements`) und unterscheiden sich nur in der Pfad-Normalisierung (`Path.DirectorySeparatorChar`/`Path.Combine` gegenüber literalem `/`). Der Fallback wird in `BuildSnapshot` (Zeilen 592–597) als zweiter Anlauf verwendet, wenn der erste keine Treffer liefert.

  Empfehlung: Auf eine einzige Methode konsolidieren, die den Pfad einmal auf `/`-Trenner normalisiert und dann prüft; damit entfällt der doppelte Anlauf. (Wird gegenstandslos, falls der vorstehende Befund durch Entfernen der Klassifikation aufgelöst wird.)

## Geprüfte Dateien

- `src/Softwareschmiede/Application/Services/TextDiffService.cs`
- `src/Softwareschmiede/Application/Services/ITextDiffService.cs`
- `src/Softwareschmiede/Application/Services/GitWorkspaceBrowserService.cs`
- `src/Softwareschmiede/Application/Services/IGitWorkspaceBrowserService.cs`
- `src/Softwareschmiede/Domain/ValueObjects/BranchCommit.cs`
- `src/Softwareschmiede/Domain/ValueObjects/FileTextDiff.cs`
- `src/Softwareschmiede/Domain/ValueObjects/TextDiffLine.cs`
- `src/Softwareschmiede/Domain/ValueObjects/InlineDiffSegment.cs`
- `src/Softwareschmiede.App/ViewModels/FileExplorerViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/DateibrowserAnsichtsmodus.cs`
- `src/Softwareschmiede.App/Converters/DiffLineStatusToBrushConverter.cs`
- `src/Softwareschmiede.App/Controls/DiffViewer.xaml.cs`
- `src/Softwareschmiede.App/Views/FileExplorerView.xaml`
- `src/Softwareschmiede.App/Views/FileExplorerView.xaml.cs`
- `src/Softwareschmiede.Tests/Application/Services/TextDiffServiceTests.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/FileExplorerViewModelTests.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests.cs`
