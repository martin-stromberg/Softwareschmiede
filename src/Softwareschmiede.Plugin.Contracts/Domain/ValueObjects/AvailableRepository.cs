namespace Softwareschmiede.Domain.ValueObjects;

/// <summary>Repräsentiert ein verfügbares Repository aus einer externen SCM-Quelle.</summary>
/// <param name="Name">Anzeigename des Repositories (z. B. "owner/repo" oder Verzeichnisname).</param>
/// <param name="Url">URL oder Pfad, der das Repository identifiziert (wird als RepositoryUrl gespeichert).</param>
public sealed record AvailableRepository(string Name, string Url);
