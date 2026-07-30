namespace Softwareschmiede.Domain.Enums;

/// <summary>Merge-Methode fuer einen Pull Request.</summary>
public enum PullRequestMergeMethod
{
    /// <summary>Merge-Commit verwenden.</summary>
    Merge,

    /// <summary>Commits squashen.</summary>
    Squash,

    /// <summary>Commits rebasen.</summary>
    Rebase
}
