namespace Softwareschmiede.Domain.Entities;

/// <summary>Git-Repository, das einem Projekt zugeordnet ist.</summary>
public sealed class GitRepository
{
    /// <summary>Eindeutige ID des Repositories.</summary>
    public Guid Id { get; set; }

    /// <summary>ID des zugehörigen Projekts.</summary>
    public Guid ProjektId { get; set; }

    /// <summary>Plugin-Typ, z.B. "GitHub".</summary>
    public string PluginTyp { get; set; } = string.Empty;

    /// <summary>URL des Repositories.</summary>
    public string RepositoryUrl { get; set; } = string.Empty;

    /// <summary>Name des Repositories.</summary>
    public string RepositoryName { get; set; } = string.Empty;

    /// <summary>Gibt an, ob das Repository aktiv ist.</summary>
    public bool Aktiv { get; set; } = true;

    /// <summary>Optionale Startkonfiguration für Repository-Startskripte.</summary>
    public RepositoryStartKonfiguration? StartKonfiguration { get; set; }

    /// <summary>Navigationseigenschaft zum übergeordneten Projekt.</summary>
    public Projekt Projekt { get; set; } = null!;
}
