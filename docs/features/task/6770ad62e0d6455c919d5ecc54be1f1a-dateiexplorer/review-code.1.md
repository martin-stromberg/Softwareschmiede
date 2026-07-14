# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### DiffLineStatusToBrushConverter.cs (DiffLineStatusToBrushConverter) / DiffViewer.xaml (DiffViewer)

- **Hardcodierte Werte / Einheitlichkeit** — Die Diff-Zeilenfarben sind als feste Hex-Literale codiert: `#E8F5E9`, `#FFEBEE`, `#FFF3E0` (DiffLineStatusToBrushConverter.cs, Zeilen 12–14) sowie `#FFCC80` für geänderte Inline-Segmente (DiffViewer.xaml, Zeile 51). Alle sind helle Pastelltöne. Die App unterstützt jedoch Dark- und Light-Theme (`src/Softwareschmiede.App/Themes/DarkTheme.xaml`, `LightTheme.xaml`) und bindet Vorder-/Hintergründe sonst durchgängig über `DynamicResource` (z. B. `PrimaryTextBrush`, `SurfaceBrush`). Im Dark-Theme wird heller Text (`PrimaryTextBrush`) auf diese hellen festen Hintergründe gerendert und ist praktisch unlesbar.

  Empfehlung: Theme-spezifische Brushes (`DiffAddedBrush`, `DiffRemovedBrush`, `DiffModifiedBrush`, `DiffInlineChangedBrush`) in `DarkTheme.xaml`/`LightTheme.xaml` definieren und im Converter bzw. XAML über `DynamicResource` referenzieren statt fester Hex-Werte.

### FileExplorerViewModel.cs (FileExplorerViewModel)

- **Fehlerbehandlung / Kopplung (Race Condition)** — Der Setter von `AusgewaehlterKnoten` (Zeilen 35–45) startet `DateiLadenAsync` als Fire-and-Forget mit `CancellationToken.None`. Es wird kein vorheriger Ladevorgang abgebrochen. Wählt der Nutzer schnell nacheinander Datei A und dann Datei B, laufen beide `LoadPreviewAsync`/`LoadCommitPreviewAsync`-Aufrufe parallel; der langsamere (A) kann den Dispatcher-Update nach B ausführen und `DateiInhalt`/`DiffZeilen` mit veraltetem Inhalt überschreiben. `TaskDetailViewModel` löst genau dieses Problem bereits über eine `CancellationTokenSource` (`_ladenCts`).

  Empfehlung: Analog zu `TaskDetailViewModel._ladenCts` eine `CancellationTokenSource` pro Auswahlwechsel führen, den vorherigen Ladevorgang abbrechen und dessen Token an `DateiLadenAsync` übergeben.

- **Fehlerbehandlung (stale Anzeige)** — Wird im Baum ein Verzeichnisknoten (oder `null`) ausgewählt, kehrt `DateiLadenAsync` sofort zurück (Zeile 157), ohne `DateiInhalt` bzw. `DiffZeilen` zu leeren. Die rechte Vorschau zeigt weiterhin den Inhalt/Diff der zuvor gewählten Datei an.

  Empfehlung: Beim Selektieren eines Verzeichnisses oder von `null` `DateiInhalt = null` setzen und `ClearDiffZeilen()` aufrufen.

- **Kopplung / fragiler Code** — In `CommitAufklappenAsync` (Zeilen 134–139) wird der Commit-Knoten per `CommitGruppen.RemoveAt(index)` + `CommitGruppen.Insert(index, commit)` entfernt und neu eingefügt, nur um die TreeView zum Neuzeichnen zu zwingen. Ursache ist, dass `BranchCommit` kein `INotifyPropertyChanged` implementiert und `commit.Files.AddRange(...)` daher keine Aktualisierung auslöst. Der Umweg ist fragil (Selektions-/Expand-Zustand kann verloren gehen) und verschleiert die eigentliche Ursache.

  Empfehlung: `BranchCommit.Files` als `ObservableCollection` führen oder `BranchCommit` change-notifizierbar machen, sodass das Remove/Insert entfallen kann.

### GitWorkspaceBrowserService.cs (GitWorkspaceBrowserService)

- **Fehlerbehandlung (still geschluckte Exception)** — In `WalkWorkingTreeDirectory` (Zeilen 245–248) werden `UnauthorizedAccessException` und `IOException` beim Aufzählen eines Verzeichnisses gefangen und ohne jegliches Logging verworfen. Nicht zugängliche Verzeichnisse verschwinden damit kommentarlos aus dem Arbeitsbaum; eine Fehldiagnose ist später kaum möglich.

  Empfehlung: Im `catch` mindestens ein `_logger.LogDebug`/`LogWarning` mit betroffenem Pfad und Exception protokollieren. (Da `WalkWorkingTreeDirectory` derzeit `static` ist, müsste der Logger durchgereicht oder die Methode zur Instanzmethode gemacht werden.)

### TextDiffService.cs (TextDiffService)

- **Effizienz / fehlende Vorbedingung** — `ComputeLineOperations` (Zeilen 77–129) allokiert eine vollständige LCS-Matrix `int[n+1, m+1]` über alle Zeilen. Im Standard-Vorschaupfad greift zwar die Größenschranke `MaxInlineBytes` (1 MB), aber `LoadCommitPreviewAsync` (GitWorkspaceBrowserService.cs, Zeilen 147–199) lädt Commit-Inhalte via `git show` ganz ohne Größen-/Zeilenlimit und übergibt sie an `BuildDiff`. Bei einer großen im Commit geänderten Datei (z. B. 10.000 Zeilen alt × 10.000 neu) entsteht eine Matrix mit ~100 Mio. `int`-Einträgen (~400 MB) — Risiko von exzessivem Speicherverbrauch bzw. `OutOfMemoryException`.

  Empfehlung: Im Commit-Vorschaupfad vor `BuildDiff` eine Zeilen-/Größenobergrenze prüfen (analog `MaxInlineBytes`) und bei Überschreitung einen Hinweistext statt eines Diffs anzeigen.

### GitWorkspaceBrowserServiceWorkingTreeTests.cs (GitWorkspaceBrowserServiceWorkingTreeTests)

- **Testqualität (Namensgebung)** — Der Test `LoadWorkingTreeAsync_UngueltigerPfad_LiefertLeerOderFehler` (Zeile 66) suggeriert im Namen zwei mögliche Ergebnisse ("Leer oder Fehler"), prüft aber ausschließlich das leere Ergebnis (`nodes.Should().BeEmpty()`). Der Name beschreibt das tatsächlich getestete Verhalten nicht eindeutig.

  Empfehlung: Umbenennen zu z. B. `LoadWorkingTreeAsync_NichtExistierenderPfad_LiefertLeereListe`.

## Geprüfte Dateien

- `src/Softwareschmiede/Application/Services/GitWorkspaceBrowserService.cs` (nur neue Methode `LoadWorkingTreeAsync`/`WalkWorkingTreeDirectory`)
- `src/Softwareschmiede/Application/Services/IGitWorkspaceBrowserService.cs`
- `src/Softwareschmiede/Application/Services/TextDiffService.cs`
- `src/Softwareschmiede/Application/Services/ITextDiffService.cs`
- `src/Softwareschmiede/Domain/ValueObjects/FileTextDiff.cs`
- `src/Softwareschmiede/Domain/ValueObjects/TextDiffLine.cs`
- `src/Softwareschmiede/Domain/ValueObjects/InlineDiffSegment.cs`
- `src/Softwareschmiede.App/ViewModels/FileExplorerViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/DateibrowserAnsichtsmodus.cs`
- `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs` (Diff-Anteile Dateiexplorer)
- `src/Softwareschmiede.App/Views/FileExplorerView.xaml`
- `src/Softwareschmiede.App/Views/FileExplorerView.xaml.cs`
- `src/Softwareschmiede.App/Views/TaskDetailView.xaml` (Diff-Anteile)
- `src/Softwareschmiede.App/Controls/DiffViewer.xaml`
- `src/Softwareschmiede.App/Controls/DiffViewer.xaml.cs`
- `src/Softwareschmiede.App/Converters/DiffLineStatusToBrushConverter.cs`
- `src/Softwareschmiede.App/App.xaml` / `App.xaml.cs` (DI-Registrierungen)
- `src/Softwareschmiede.Tests/App/ViewModels/FileExplorerViewModelTests.cs`
- `src/Softwareschmiede.Tests/Application/Services/TextDiffServiceTests.cs`
- `src/Softwareschmiede.Tests/Application/Services/GitWorkspaceBrowserServiceWorkingTreeTests.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_FileExplorer.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests.cs` (Diff-Anteile)
- `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests_ZeitgesteuerterPrompt.cs` (Diff-Anteile)
- `src/Softwareschmiede.Tests/Helpers/TaskDetailViewModelTestFactory.cs` (Diff-Anteile)
