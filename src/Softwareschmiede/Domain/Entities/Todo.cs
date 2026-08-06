namespace Softwareschmiede.Domain.Entities;

/// <summary>To-Do-Element einer Aufgabe.</summary>
public sealed class Todo
{
    /// <summary>Eindeutige ID des To-Dos.</summary>
    public Guid Id { get; set; }

    /// <summary>ID der zugehörigen Aufgabe.</summary>
    public Guid AufgabeId { get; set; }

    /// <summary>Text des To-Dos.</summary>
    public string Beschreibung { get; set; } = string.Empty;

    /// <summary>Zeitstempel der Fertigstellung (null = offen).</summary>
    public DateTimeOffset? ErledigtAm { get; set; }

    /// <summary>Gibt an, ob das To-Do offen ist (noch nicht erledigt).</summary>
    public bool IstOffen => ErledigtAm is null;

    /// <summary>Erstellungszeitstempel des To-Dos.</summary>
    public DateTimeOffset ErstellungsDatum { get; set; }

    /// <summary>Navigation zur Aufgabe.</summary>
    public Aufgabe Aufgabe { get; set; } = null!;
}
