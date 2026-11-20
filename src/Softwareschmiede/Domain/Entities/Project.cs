using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.Domain.Entities;

/// <summary>Zentrale Organisationseinheit der Softwareschmiede.</summary>
public sealed class Project
{
    /// <summary>Eindeutige ID des Projekts.</summary>
    public Guid Id { get; set; }

    /// <summary>Name des Projekts.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optionale Beschreibung des Projekts.</summary>
    public string? Description { get; set; }

    /// <summary>Datum und Uhrzeit der Erstellung.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Aktueller Status des Projekts.</summary>
    public ProjectStatus Status { get; set; }

    /// <summary>Zugeordnete Git-Repositories.</summary>
    public List<GitRepository> Repositories { get; set; } = [];

    /// <summary>Aufgaben des Projekts.</summary>
    public List<WorkItem> WorkItems { get; set; } = [];
}
