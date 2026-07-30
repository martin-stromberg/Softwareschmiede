namespace Softwareschmiede.Domain.Enums;

/// <summary>Mergebarkeit oder Abschlusszustand eines Pull Requests.</summary>
public enum PullRequestMergeStatus
{
    /// <summary>Mergebarkeit ist noch nicht bekannt.</summary>
    Unknown,

    /// <summary>Pull Request kann gemergt werden.</summary>
    Mergeable,

    /// <summary>Pull Request ist durch Regeln oder fehlende Voraussetzungen blockiert.</summary>
    Blocked,

    /// <summary>Pull Request hat Merge-Konflikte.</summary>
    Conflicting,

    /// <summary>Pull Request wurde gemergt.</summary>
    Merged
}
