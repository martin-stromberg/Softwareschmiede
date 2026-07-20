# Service-Interfaces

## `IGitWorkspaceBrowserService`
Datei: `src/Softwareschmiede/Application/Services/IGitWorkspaceBrowserService.cs`

Service-Interface zum Laden des lokalen Repository-Browsers. Abstraktion über Git-Backend und Arbeitsbaum-Operationen.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `LoadSnapshotAsync` | `repositoryPath: string, ct: CancellationToken = default` | `Task<WorkspaceSnapshot>` | Lädt aktuellen Workspace-Zustand (Commit-Zähler, Branch-Commits) |
| `LoadPreviewAsync` | `repositoryPath: string, node: WorkspaceFileNode, ct: CancellationToken = default` | `Task<FilePreview>` | Lädt Dateivorschau für ausgewählten Knoten im Standardmodus |
| `LoadCommitFilesAsync` | `repositoryPath: string, commitSha: string, ct: CancellationToken = default` | `Task<IReadOnlyList<WorkspaceFileNode>>` | Lädt Dateiknoten für einen bestimmten Commit (Vergleichsmodus) |
| `LoadCommitPreviewAsync` | `repositoryPath: string, node: WorkspaceFileNode, ct: CancellationToken = default` | `Task<FilePreview>` | Lädt Dateivorschau aus einem Commit |
| `LoadWorkingTreeAsync` | `repositoryPath: string, ct: CancellationToken = default` | `Task<IReadOnlyList<WorkspaceFileNode>>` | Lädt vollständigen Arbeitsbaum (Verzeichnisse + Dateien, `.git` ausgeschlossen). **Derzeit unbegrenzte Rekursion!** |

**Fehlende Methoden (laut Anforderung):**
- `LoadSubtreeAsync(repositoryPath: string, parentPath: string, depth: int, ct: CancellationToken = default) : Task<IReadOnlyList<WorkspaceFileNode>>` — Lädt eine einzelne Ebene unterhalb eines Knotens beim Aufklappen im Lazy-Loading-Modus.

**Erforderliche Signatur-Änderung:**
- `LoadWorkingTreeAsync` sollte um optionalen `maxInitialDepth: int = 2`-Parameter erweitert werden, um nur die obersten zwei Ebenen zu laden statt unbegrenzt.
