namespace Softwareschmiede.Domain.Entities;

/// <summary>Persistierte Initialisierungskonfiguration für ein Git-Repository.</summary>
public sealed class RepositoryInitialisierungKonfiguration
{
    /// <summary>Eindeutige ID der Konfiguration.</summary>
    public Guid Id { get; set; }

    /// <summary>Referenz auf das zugehörige Repository.</summary>
    public Guid GitRepositoryId { get; set; }

    /// <summary>Relativer Pfad zum Initialisierungsskript im Repository.</summary>
    public string InitialisierungsskriptRelativePath { get; set; } = string.Empty;

    /// <summary>Gibt an, ob die Initialisierungskonfiguration aktiv verwendet wird.</summary>
    public bool Aktiv { get; set; } = true;

    /// <summary>Navigationseigenschaft zum Repository.</summary>
    public GitRepository GitRepository { get; set; } = null!;
}
