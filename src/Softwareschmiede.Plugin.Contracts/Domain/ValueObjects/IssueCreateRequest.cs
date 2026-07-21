namespace Softwareschmiede.Domain.ValueObjects;

/// <summary>Anfrage zum providerunabhängigen Anlegen eines Issues.</summary>
/// <param name="Title">Titel des Issues.</param>
/// <param name="Body">Beschreibung des Issues.</param>
public sealed record IssueCreateRequest(string Title, string? Body);
