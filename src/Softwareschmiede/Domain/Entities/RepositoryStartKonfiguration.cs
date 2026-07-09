namespace Softwareschmiede.Domain.Entities;

/// <summary>Persistierte Startkonfiguration für ein Git-Repository.</summary>
public sealed class RepositoryStartKonfiguration
{
    /// <summary>Eindeutige ID der Konfiguration.</summary>
    public Guid Id { get; set; }

    /// <summary>Referenz auf das zugehörige Repository.</summary>
    public Guid GitRepositoryId { get; set; }

    /// <summary>Relativer Pfad zum Startskript im Repository.</summary>
    public string StartScriptRelativePath { get; set; } = string.Empty;

    /// <summary>Relativer Pfad zum Arbeitsverzeichnis innerhalb des Repositories; <c>null</c> bedeutet Repository-Root.</summary>
    public string? WorkingDirectoryRelativePath { get; set; }

    /// <summary>Gibt an, ob die Startkonfiguration aktiv verwendet wird.</summary>
    public bool Aktiv { get; set; } = true;

    /// <summary>Navigationseigenschaft zum Repository.</summary>
    public GitRepository GitRepository { get; set; } = null!;
}
