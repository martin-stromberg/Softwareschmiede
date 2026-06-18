namespace Softwareschmiede.Domain.ValueObjects;

/// <summary>Repräsentiert ein verfügbares Repository aus einer externen SCM-Quelle.</summary>
/// <param name="Name">Anzeigename des Repositories (z. B. "owner/repo" oder Verzeichnisname).</param>
/// <param name="UpdatedAt">Datum und Uhrzeit der letzten Aktualisierung des Repositories.</param>
/// <param name="NameWithOwner">Vollständiger Name des Repositories einschließlich des Besitzers (z. B. "owner/repo").</param>
/// <param name="Url">URL oder Pfad, der das Repository identifiziert (wird als RepositoryUrl gespeichert).</param>
public sealed record AvailableRepository(string Name, DateTime UpdatedAt, string NameWithOwner, string Url);
