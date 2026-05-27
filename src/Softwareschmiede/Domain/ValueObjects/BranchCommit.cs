namespace Softwareschmiede.Domain.ValueObjects;

/// <summary>Repräsentiert einen Branch-Commit im Repository-Explorer.</summary>
public sealed class BranchCommit
{
    public string Sha { get; init; } = string.Empty;

    public string ShortSha { get; init; } = string.Empty;

    public string Subject { get; init; } = string.Empty;

    public bool IsExpanded { get; set; }

    public bool ChildrenLoaded { get; set; }

    public bool IsLoadingFiles { get; set; }

    public string? ErrorMessage { get; set; }

    public List<WorkspaceFileNode> Files { get; set; } = [];
}
