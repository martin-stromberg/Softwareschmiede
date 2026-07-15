namespace Softwareschmiede.Domain.ValueObjects;

/// <summary>Zur Laufzeit ermittelter Workspace-Zustand.</summary>
public sealed class WorkspaceSnapshot
{
    /// <summary>Pfad des zugrunde liegenden Repositories.</summary>
    public string RepositoryPath { get; init; } = string.Empty;

    /// <summary>Anzahl Commits im aktuellen Branch.</summary>
    public int CommitCount { get; init; }

    /// <summary>Commits des aktuellen Branches relativ zur Basisreferenz.</summary>
    public IReadOnlyList<BranchCommit> BranchCommits { get; init; } = [];

    /// <summary>Optionaler Fehlertext, wenn das Repository nicht geladen werden konnte.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Gibt an, ob die Snapshot-Ermittlung fehlgeschlagen ist.</summary>
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    /// <summary>Erstellt einen Snapshot, der einen Fehler repräsentiert.</summary>
    public static WorkspaceSnapshot FromError(string message) => new()
    {
        ErrorMessage = message,
    };
}
