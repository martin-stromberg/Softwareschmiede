using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.Domain.ValueObjects;

/// <summary>Optionen fuer einen Pull-Request-Abschluss beim Provider.</summary>
public sealed record PullRequestCompletionOptions(
    PullRequestCompletionStrategy Strategy = PullRequestCompletionStrategy.Merge,
    PullRequestMergeMethod MergeMethod = PullRequestMergeMethod.Squash,
    bool AllowProtectedBranchBypass = false);
