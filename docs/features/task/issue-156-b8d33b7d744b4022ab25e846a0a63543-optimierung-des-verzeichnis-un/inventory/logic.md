# Service-Implementierung

## `GitWorkspaceBrowserService`
Datei: `src/Softwareschmiede/Application/Services/GitWorkspaceBrowserService.cs`

Sealed Service-Implementierung von `IGitWorkspaceBrowserService`. Lädt Git-Status, Baum und Dateivorschauen aus dem lokalen Arbeitsverzeichnis.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `LoadSnapshotAsync` | public | Lädt Workspace-Snapshot (Commits, Status) |
| `LoadPreviewAsync` | public | Lädt Dateivorschau mit Größen- und Binär-Checks |
| `LoadCommitFilesAsync` | public | Lädt Dateibaum aus `git diff-tree` |
| `LoadCommitPreviewAsync` | public | Lädt Dateivorschau aus Git-Commit |
| `LoadWorkingTreeAsync` | public | **Hauptmethode für Arbeitsbaum: ruft `WalkWorkingTreeDirectory` rekursiv auf** |
| `WalkWorkingTreeDirectory` (private) | private | **Rekursive Directory-Enumeration, lädt ALLE Ebenen unbegrenzt** |
| `BuildCommitFileTree` | private | Baut Dateibaum aus `git diff-tree`-Output |
| `InsertNode` | private | Hilfsmethode zum Einordnen von Dateiknoten in Hierarchie |
| `SortNodes` | private | Sortiert Knoten alphabetisch, Verzeichnisse vor Dateien |

**Konstanten:**
- `MaxWorkingTreeNodeCount = 100_000` — Obergrenze für Knoten (Abbruch wenn überschritten)
- `BinaryProbeBytes = 8_192` — Größe des Probes für Binär-Erkennung
- `MaxInlineBytes = 1_048_576` (1 MB) — Größenlimit für Inline-Vorschau
- `GitDirectoryName = ".git"` — wird bei der Enumeration übersprungen

**Kritische Verhaltensweisen für Lazy-Loading:**
1. `WalkWorkingTreeDirectory` (Zeilen 241–303): 
   - Traversiert rekursiv **alle** Unterverzeichnisse
   - Setzt `ChildrenLoaded = true` auf alle Directory-Knoten (Zeile 280)
   - **Problem:** Dies ist für Lazy-Loading ungeeignet — alle Knoten werden auf einmal geladen

2. Fehler-Handling: 
   - `UnauthorizedAccessException` und `IOException` bei Directory-Enumeration werden geloggt und übersprungen (Zeilen 255–259)

**Abonnierte/Publizierte Events:** keine

**Fehlende Implementierung:**
- `LoadSubtreeAsync` — muss analog zu `WalkWorkingTreeDirectory` implementiert werden, aber mit `maxDepth`-Limitierung für nur eine Ebene
- Anpassung `LoadWorkingTreeAsync`: muss `maxInitialDepth`-Parameter nutzen, um Rekursion zu begrenzen
