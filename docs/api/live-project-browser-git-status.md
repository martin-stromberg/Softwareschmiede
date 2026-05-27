# Live Project Browser mit Git-Status – Technischer Contract

## Übersicht

Der Contract beschreibt die **interne API** für Repository-Baum, Branch-Commit-Anzeige und Dateivorschau in `AufgabeDetail`.  
Für dieses Feature wurden **keine neuen öffentlichen REST-Endpunkte** eingeführt.

## Implementierung und Contract

- Contract: `src/Softwareschmiede/Application/Services/IGitWorkspaceBrowserService.cs`
- Implementierung: `src/Softwareschmiede/Application/Services/GitWorkspaceBrowserService.cs`
- UI-Orchestrierung: `src/Softwareschmiede/Components/Pages/Aufgaben/AufgabeDetail.razor(.cs)`
- Commit-Tree-Zustandslogik: `src/Softwareschmiede/Components/Pages/Aufgaben/CommitTreePresenter.cs`

## Öffentliche HTTP-Bezüge

- Kein eigener Browser-Endpunkt.
- Diff-Rendering nutzt weiterhin bestehende Diff-API: [diff.md](./diff.md) (`GET /api/diff/{id}`).

## Interne API-Operationen

| Operation | Zweck | Rückgabe |
|---|---|---|
| `LoadSnapshotAsync(repositoryPath)` | Lädt Commit-Zähler relativ zum Basis-Branch, Branch-Commits und Working-Tree-Änderungen. | `WorkspaceSnapshot` |
| `LoadPreviewAsync(repositoryPath, node)` | Lädt Vorschau für Working-Tree-Dateien. | `FilePreview` |
| `LoadCommitFilesAsync(repositoryPath, commitSha)` | Lädt Commit-Dateibaum lazy für einen Branch-Commit (`git diff-tree ...`). | `IReadOnlyList<WorkspaceFileNode>` |
| `LoadCommitPreviewAsync(repositoryPath, node)` | Lädt Commit-spezifische Datei-/Vorversion per `git show <sha>:path` und `<sha>^:path`. | `FilePreview` |

## Feature-Fokus: Branch-Commit-Anzeige + Commit-Diff-Preview

- `WorkspaceSnapshot.BranchCommits` liefert die sichtbaren Commit-Knoten im Dateibaum.
- Commit-Knoten werden in der UI lazy geladen (`CommitNodeClickedAsync` → `LoadCommitFilesAsync`).
- Commit-Dateien tragen `CommitSha`; dadurch wird Commit-Vorschau statt Working-Tree-Vorschau erzwungen (`CommitTreePresenter.RequiresCommitPreview`).
- Für Commit-Dateien wird **kein** `DiffResultId` aufgelöst; die Vorschau kommt direkt aus Git-Inhalten des Commits.

## Architekturentscheidungen

- Basis-Referenz wird robust aufgelöst (`origin/HEAD`, Fallback `origin/main|origin/master|main|master`).
- Commit-Zähler basiert auf `baseReference..HEAD` (nicht global auf alle Commits).
- Commit-Dateibaum wird aus `git diff-tree --root --no-commit-id -r --name-status -z` aufgebaut, inkl. Rename/Copy.
- Commit-Preview liest `current` und `original` aus Git-Objekten; Binärinhalt wird über Null-Byte im Text erkannt.

## Testabdeckung (Auszug)

- `GitWorkspaceBrowserServiceTests`
  - `LoadSnapshotAsync_ShouldReturnZeroBranchCommits_WhenNoBaseRefCanBeDetected`
  - `LoadCommitFilesAsync_ShouldBuildTreeAndAssignCommitSha`
  - `LoadCommitPreviewAsync_ShouldLoadCurrentAndOriginalVersions`
  - `LoadCommitPreviewAsync_ShouldReturnBinaryHint_WhenCommitContentContainsNullCharacter`
- `CommitTreePresenterTests`
  - Lazy Load nur einmal, Retry/Error-Verhalten, Flattening der Commit-Knoten
- `AufgabeDetailWorkspacePreviewBunitTests`
  - Commit-Lazy-Load inkl. Retry
  - Commit-Preview-Flow bei Dateiselektion
  - dateispezifische Diff-Auflösung für Working-Tree-Dateien

## Bekannte Grenzen

- Kein öffentlicher HTTP-Endpunkt für Commit-Baum/-Preview (nur In-Process-Servicecontract).
- Bei nicht auflösbarer Basisreferenz sind `CommitCount = 0` und `BranchCommits = []`.
- Commit-Preview nutzt keine 1-MB-Grenze; große Textantworten werden aus `git show` als String übernommen.
- Wenn `commitSha^` nicht existiert (z. B. Root-Commit), ist `OriginalContent` ggf. `null`.
- Keine Schreiboperationen (read-only).

## Verwandte Dokumentation

- [Branch-Commit-Anzeige + Commit-Diff-Preview](./branch-commit-diff-preview.md)
- [Diff Viewer](./diff-viewer.md)
- [Requirements: Live Project Browser](../requirements/live-project-browser-git-status-requirements-analysis.md)
- [Architecture: Live Project Browser](../architecture/live-project-browser-git-status-architecture-blueprint.md)
- [Improvement Review: Live Project Browser](../improvements/live-project-browser-git-status-architecture-review.md)
- [Requirements: Korrekte Diff-Anzeige](../requirements/diffviewer-correct-diff-display-requirements-analysis.md)
- [Architecture: Korrekte Diff-Anzeige](../architecture/diffviewer-correct-diff-display-architecture-blueprint.md)
- [Improvement Review: Korrekte Diff-Anzeige](../improvements/diffviewer-correct-diff-display-architecture-review.md)
