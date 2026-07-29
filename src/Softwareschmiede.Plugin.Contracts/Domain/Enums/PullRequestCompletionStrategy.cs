namespace Softwareschmiede.Domain.Enums;

/// <summary>Strategie fuer einen automatischen Pull-Request-Abschluss.</summary>
public enum PullRequestCompletionStrategy
{
    /// <summary>Pull Request direkt mergen.</summary>
    Merge,

    /// <summary>GitHub Auto-Merge aktivieren.</summary>
    AutoMerge,

    /// <summary>Pull Request nur genehmigen.</summary>
    ApprovalOnly
}
