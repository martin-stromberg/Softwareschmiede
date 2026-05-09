using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.Domain.Entities;

/// <summary>Zentrale Organisationseinheit der Softwareschmiede.</summary>
public sealed class Projekt
{
    /// <summary>Eindeutige ID des Projekts.</summary>
    public Guid Id { get; set; }

    /// <summary>Name des Projekts.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optionale Beschreibung des Projekts.</summary>
    public string? Beschreibung { get; set; }

    /// <summary>Datum und Uhrzeit der Erstellung.</summary>
    public DateTimeOffset ErstellungsDatum { get; set; }

    /// <summary>Aktueller Status des Projekts.</summary>
    public ProjektStatus Status { get; set; }

    /// <summary>Zugeordnete Git-Repositories.</summary>
    public List<GitRepository> Repositories { get; set; } = [];

    /// <summary>Aufgaben des Projekts.</summary>
    public List<Aufgabe> Aufgaben { get; set; } = [];
}
