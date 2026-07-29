namespace Softwareschmiede.Domain.ValueObjects;

/// <summary>Ergebnis eines Pull-Request-Abschlussversuchs.</summary>
public sealed record PullRequestCompletionResult(
    bool Success,
    bool Blocked,
    string? Message,
    string? MergeCommitSha,
    bool PullRequestMerged = true)
{
    /// <summary>Erzeugt ein erfolgreiches Ergebnis.</summary>
    public static PullRequestCompletionResult Completed(string? mergeCommitSha, string? message = null)
        => new(true, false, message, mergeCommitSha);

    /// <summary>Erzeugt ein erfolgreiches Approval-Ergebnis ohne Merge.</summary>
    public static PullRequestCompletionResult Approved(string? message = null)
        => new(true, false, message, null, false);

    /// <summary>Erzeugt ein erfolgreiches Warte-Ergebnis ohne Merge.</summary>
    public static PullRequestCompletionResult WaitingForMerge(string? message = null)
        => new(true, false, message, null, false);

    /// <summary>Erzeugt ein blockiertes Ergebnis.</summary>
    public static PullRequestCompletionResult BlockedResult(string message)
        => new(false, true, message, null);

    /// <summary>Erzeugt ein fehlgeschlagenes Ergebnis.</summary>
    public static PullRequestCompletionResult Failed(string message)
        => new(false, false, message, null);
}
