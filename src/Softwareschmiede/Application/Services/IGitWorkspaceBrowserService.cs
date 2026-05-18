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
}
