using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.Domain.ValueObjects;

/// <summary>Aktueller Provider-Status eines Pull Requests.</summary>
public sealed record PullRequestStatusInfo(
    PullRequestProvider Provider,
    string RepositoryId,
    int Nummer,
    string? ProviderPullRequestId,
    string Url,
    string Titel,
    string SourceBranch,
    string TargetBranch,
    string? HeadSha,
    string? MergeCommitSha,
    PullRequestStatus Status,
    PullRequestMergeStatus MergeStatus,
    DateTimeOffset CheckedAtUtc);
