namespace Softwareschmiede.Domain.Entities;

/// <summary>To-Do-Element einer Aufgabe.</summary>
public sealed class Todo
{
    /// <summary>Eindeutige ID des To-Dos.</summary>
    public Guid Id { get; set; }

    /// <summary>ID der zugehörigen Aufgabe.</summary>
    public Guid WorkItemId { get; set; }

    /// <summary>Text des To-Dos.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Zeitstempel der Fertigstellung (null = offen).</summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>Gibt an, ob das To-Do offen ist (noch nicht erledigt).</summary>
    public bool IstOffen => CompletedAt is null;

    /// <summary>Erstellungszeitstempel des To-Dos.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Navigation zur Aufgabe.</summary>
    public WorkItem WorkItem { get; set; } = null!;
}
