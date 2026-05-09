namespace Softwareschmiede.Domain.ValueObjects;

/// <summary>Pull Request aus einem Git-Provider.</summary>
/// <param name="Nummer">PR-Nummer im Provider.</param>
/// <param name="Titel">Titel des Pull Requests.</param>
/// <param name="Url">URL des Pull Requests im Provider.</param>
/// <param name="BranchName">Name des Quell-Branches.</param>
public sealed record PullRequest(
    int Nummer,
    string Titel,
    string Url,
    string BranchName
);
