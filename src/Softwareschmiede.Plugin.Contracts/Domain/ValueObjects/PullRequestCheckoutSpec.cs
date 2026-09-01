namespace Softwareschmiede.Domain.ValueObjects;

/// <summary>Vollstaendige Angaben zum Checkout einer Pull-Request-Quelle.</summary>
public sealed record PullRequestCheckoutSpec(
    string TargetRepositoryId,
    string TargetRepositoryUrl,
    string SourceRepositoryId,
    string? SourceRepositoryUrl,
    string SourceBranch,
    string? SourceRef,
    string? HeadSha);
