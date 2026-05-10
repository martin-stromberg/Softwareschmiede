namespace Softwareschmiede.Domain.ValueObjects;

/// <summary>Issue aus einem Git-Provider.</summary>
/// <param name="Nummer">Issue-Nummer im Provider.</param>
/// <param name="Titel">Titel des Issues.</param>
/// <param name="Body">Beschreibungstext des Issues.</param>
/// <param name="Labels">Labels des Issues.</param>
/// <param name="Milestone">Milestone des Issues.</param>
/// <param name="IssueUrl">URL des Issues im Provider.</param>
public sealed record Issue(
    int Nummer,
    string Titel,
    string? Body,
    IReadOnlyList<string> Labels,
    string? Milestone,
    string? IssueUrl
);
