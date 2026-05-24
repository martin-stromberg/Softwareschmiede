# Testlückenanalyse – DiffViewer für geänderte Dateien

## Kontext
- Feature: **Korrekte Diff-Anzeige im DiffViewer für geänderte Dateien**
- Fokus: dateispezifische Diff-Auflösung in `AufgabeDetail` sowie Rendering-/Interaktionslogik im `DiffViewer`
- Stand: Relevante Tests wurden bereits ergänzt (`AufgabeDetailWorkspacePreviewBunitTests`, `DiffViewerBunitTests`)

## Bereits abgedeckte Kernfälle
- Dateiauswahl mit korrekter Anzeige eines dateispezifischen Diffs.
- Wechsel zwischen Dateien mit konsistenter Vorschau (kein stale Preview).
- Validierung/Not-Found-Szenarien im `DiffViewer`.
- Parameterwechsel mit erneuter Diff-Ladung.

## Verbleibende Testlücken

### 1) Dateispezifische Diff-Auflösung in `AufgabeDetail`
**Risiko:** hoch  
**Betroffene Logik:** `ResolveSelectedWorkspaceDiffResultIdAsync`, `LadeWorkspaceAsync`, `WorkspaceNodeClickedAsync`

**Fehlende Tests:**
- `ResolveSelectedWorkspaceDiffResultIdAsync` gibt `null` zurück, wenn `RelativePath` leer/whitespace ist.
- Fallback von `RelativePath` auf `SourceRelativePath`, wenn kein Diff für den primären Pfad existiert.
- Kein Fallback-Query, wenn `SourceRelativePath` leer oder identisch zu `RelativePath` ist.
- Fehlerpfad bei fehlgeschlagenem Diff-Lookup über `AufgabeService`.
- `LadeWorkspaceAsync`: Verhalten ohne lokalen Klonpfad (Fehlerhinweis + Reset von Preview/Diff-ID).
- `LadeWorkspaceAsync`: Fehlerpfad bei Exception aus `IGitWorkspaceBrowserService.LoadSnapshotAsync`.
- Wiederherstellung einer zuvor selektierten Datei nach Reload (inkl. `ExpandPath`/`FindNode`).
- Klick auf Verzeichnis in `WorkspaceNodeClickedAsync`: Expand/Collapse + Rücksetzen von Preview/Diff-ID.

### 2) `DiffViewer` Lifecycle, Cancellation und Events
**Risiko:** hoch  
**Betroffene Logik:** `OnParametersSetAsync`, `LoadDiffAsync`, Handler (`HandleViewModeChange`, `HandleSearch`, Navigation, Export), `DisposeAsync`

**Fehlende Tests:**
- Loading-State wird während laufender Diff-Ladung gerendert.
- Guard-Pfad bei unveränderter `DiffResultId` lädt nicht erneut.
- Race-/Cancellation-Verhalten bei schnellem Wechsel der `DiffResultId` (`loadingVersion`).
- Exception-Pfad aus `DiffService.GetDiffAsync` zeigt generische Fehlermeldung.
- Eventpfade:
  - View-Mode-Wechsel
  - Suche setzen/leeren
  - Navigation Next/Previous
  - Zeilenselektion
  - Export-Handler
- `IAsyncDisposable.DisposeAsync` storniert laufende Ladung und entsorgt CTS.

### 3) Rendering-Subkomponenten des DiffViewers
**Risiko:** mittel bis hoch  
**Betroffene Komponenten:** `DiffLine`, `DiffToolbar`, `DiffContent`, `DiffHeader`, `DiffFooter`

**Fehlende Tests:**
- `DiffLine`: Status-Mapping (Added/Removed/Modified/Context/Unknown), Zeilennummern inkl. `N/A`, Checkbox-/Copy-Callbacks, ARIA-Label.
- `DiffToolbar`: View-Mode-Buttons, Suchinteraktion (`Enter`, `Shift+Enter`, `Escape`), Clear-Search, Filter-Toggles, Export-/Copy-Aktionen.
- `DiffContent`: Empty-State, Suchfilter (case-insensitive), Caching-Pfad, Delegation von Child-Callbacks, Fehlerpfad in Copy-Handler.
- `DiffHeader`/`DiffFooter`: Statusanzeige (`Pending`, `Cached`, `Error`, `Unknown`) und Metadaten-Branches (`GeneratedAt`, `ExpiresAt`, Versions-/Generatorinfos).
- `DiffFooter`: JS-Interop-Pfade `ScrollToTop`/`ScrollToBottom` inkl. Fehlerpfad.

## Priorisierte Empfehlung
1. `AufgabeDetail`-Fallback- und Fehlerpfade für dateispezifische Diff-Auflösung.
2. `DiffViewer`-Lifecycle/Cancellation/Events.
3. Rendering-Subkomponenten (`DiffToolbar`, `DiffLine`, `DiffContent`, `DiffHeader`, `DiffFooter`).
