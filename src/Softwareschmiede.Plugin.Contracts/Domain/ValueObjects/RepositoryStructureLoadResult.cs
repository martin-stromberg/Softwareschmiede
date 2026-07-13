namespace Softwareschmiede.Domain.ValueObjects;

/// <summary>Ergebnis eines Remote-Abrufs der Repository-Verzeichnisstruktur.</summary>
/// <param name="Status">Status des Abrufs.</param>
/// <param name="Entries">Geladene Repository-Einträge. Bei Fehlern leer.</param>
/// <param name="Message">Optionale Fehler- oder Hinweismeldung.</param>
public sealed record RepositoryStructureLoadResult(
    RepositoryStructureLoadStatus Status,
    IReadOnlyList<RepositoryDirectoryEntry> Entries,
    string? Message = null)
{
    /// <summary>Erzeugt ein erfolgreiches Ergebnis.</summary>
    public static RepositoryStructureLoadResult Success(IEnumerable<RepositoryDirectoryEntry> entries)
        => new(RepositoryStructureLoadStatus.Success, entries.ToList());

    /// <summary>Erzeugt ein Fehlerergebnis.</summary>
    public static RepositoryStructureLoadResult Failed(string? message = null)
        => new(RepositoryStructureLoadStatus.Failed, [], message);

    /// <summary>Erzeugt ein Ergebnis für nicht unterstützten Strukturabruf.</summary>
    public static RepositoryStructureLoadResult NotSupported(string? message = null)
        => new(RepositoryStructureLoadStatus.NotSupported, [], message);
}
