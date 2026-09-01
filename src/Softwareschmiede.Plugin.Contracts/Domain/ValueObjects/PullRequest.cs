namespace Softwareschmiede.Domain.ValueObjects;

using Softwareschmiede.Domain.Enums;

/// <summary>Pull Request aus einem Git-Provider.</summary>
/// <param name="Nummer">PR-Nummer im Provider.</param>
/// <param name="Titel">Titel des Pull Requests.</param>
/// <param name="Url">URL des Pull Requests im Provider.</param>
/// <param name="BranchName">Name des Quell-Branches.</param>
/// <param name="Provider">Provider des Pull Requests.</param>
/// <param name="RepositoryId">Repository-Identifier beim Provider.</param>
/// <param name="ProviderPullRequestId">Optionale eindeutige Provider-ID.</param>
/// <param name="TargetBranch">Name des Ziel-Branches.</param>
/// <param name="HeadSha">Head-SHA des Pull Requests.</param>
/// <param name="SourceRepositoryId">Repository-Identifier des Quell-Repositories.</param>
/// <param name="SourceRepositoryUrl">Clone-URL des Quell-Repositories.</param>
/// <param name="SourceRef">Vom Provider fetchbare Quell-Referenz.</param>
/// <param name="Body">Beschreibungstext des Pull Requests.</param>
public sealed record PullRequest(
    int Nummer,
    string Titel,
    string Url,
    string BranchName,
    PullRequestProvider Provider = PullRequestProvider.GitHub,
    string? RepositoryId = null,
    string? ProviderPullRequestId = null,
    string? TargetBranch = null,
    string? HeadSha = null,
    string? SourceRepositoryId = null,
    string? SourceRepositoryUrl = null,
    string? SourceRef = null,
    string? Body = null
);
