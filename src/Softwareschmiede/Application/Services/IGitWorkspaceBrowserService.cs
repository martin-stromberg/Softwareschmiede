using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Application.Services;

/// <summary>Service zum Laden des lokalen Repository-Browsers.</summary>
public interface IGitWorkspaceBrowserService
{
    /// <summary>Lädt den aktuellen Workspace-Zustand für ein Repository.</summary>
    /// <param name="repositoryPath">Pfad des lokalen Repositories.</param>
    /// <param name="ct">Cancellation Token.</param>
    Task<WorkspaceSnapshot> LoadSnapshotAsync(string repositoryPath, CancellationToken ct = default);

    /// <summary>Lädt die Vorschau für eine selektierte Datei.</summary>
    /// <param name="repositoryPath">Pfad des lokalen Repositories.</param>
    /// <param name="node">Ausgewählter Dateiknoten.</param>
    /// <param name="ct">Cancellation Token.</param>
    Task<FilePreview> LoadPreviewAsync(string repositoryPath, WorkspaceFileNode node, CancellationToken ct = default);

    /// <summary>Lädt commit-spezifische Dateiknoten für einen Branch-Commit.</summary>
    /// <param name="repositoryPath">Pfad des lokalen Repositories.</param>
    /// <param name="commitSha">SHA des Commits.</param>
    /// <param name="ct">Cancellation Token.</param>
    Task<IReadOnlyList<WorkspaceFileNode>> LoadCommitFilesAsync(string repositoryPath, string commitSha, CancellationToken ct = default);

    /// <summary>Lädt die Vorschau einer Datei innerhalb eines bestimmten Commits.</summary>
    /// <param name="repositoryPath">Pfad des lokalen Repositories.</param>
    /// <param name="node">Ausgewählter Commit-Dateiknoten.</param>
    /// <param name="ct">Cancellation Token.</param>
    Task<FilePreview> LoadCommitPreviewAsync(string repositoryPath, WorkspaceFileNode node, CancellationToken ct = default);

    /// <summary>Lädt den Arbeitsbaum eines geklonten Repositories bis zu einer maximalen Tiefe (Verzeichnisse und Dateien, <c>.git</c> ausgeschlossen). Verzeichnisse auf der Grenztiefe erhalten <see cref="WorkspaceFileNode.ChildrenLoaded"/> <c>false</c> und einen Platzhalter-Kindknoten für Lazy-Loading.</summary>
    /// <param name="repositoryPath">Pfad des lokalen Repositories.</param>
    /// <param name="maxInitialDepth">Maximale Anzahl initial geladener Ebenen (0 = nur Wurzelebene).</param>
    /// <param name="ct">Cancellation Token.</param>
    /// <returns>Die Wurzelknoten des Arbeitsbaums, oder eine leere Liste, wenn der Pfad nicht existiert.</returns>
    Task<IReadOnlyList<WorkspaceFileNode>> LoadWorkingTreeAsync(string repositoryPath, int maxInitialDepth = 2, CancellationToken ct = default);

    /// <summary>Lädt eine einzelne Ebene unmittelbarer Kinder unterhalb <paramref name="parentPath"/> nach (Lazy-Loading beim Aufklappen eines Verzeichnisknotens).</summary>
    /// <param name="repositoryPath">Pfad des lokalen Repositories.</param>
    /// <param name="parentPath">Relativer Pfad des Verzeichnisses, dessen Kinder geladen werden sollen.</param>
    /// <param name="depth">Ebene, die den zurückgegebenen Knoten zugewiesen wird.</param>
    /// <param name="ct">Cancellation Token.</param>
    /// <returns>Die unmittelbaren Kindknoten von <paramref name="parentPath"/>, oder eine leere Liste, wenn der Pfad nicht existiert.</returns>
    Task<IReadOnlyList<WorkspaceFileNode>> LoadSubtreeAsync(string repositoryPath, string parentPath, int depth, CancellationToken ct = default);
}
