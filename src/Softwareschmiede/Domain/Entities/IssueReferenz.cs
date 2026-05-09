namespace Softwareschmiede.Domain.Entities;

/// <summary>Referenz auf ein Issue aus dem Git-Provider.</summary>
public sealed class IssueReferenz
{
    /// <summary>Eindeutige ID der Issue-Referenz.</summary>
    public Guid Id { get; set; }

    /// <summary>ID der zugehörigen Aufgabe.</summary>
    public Guid AufgabeId { get; set; }

    /// <summary>Nummer des Issues im Git-Provider.</summary>
    public int? IssueNummer { get; set; }

    /// <summary>Titel des Issues.</summary>
    public string Titel { get; set; } = string.Empty;

    /// <summary>Beschreibungstext des Issues.</summary>
    public string? Body { get; set; }

    /// <summary>JSON-Array der Labels des Issues.</summary>
    public string LabelsJson { get; set; } = "[]";

    /// <summary>Milestone des Issues.</summary>
    public string? Milestone { get; set; }

    /// <summary>URL des Issues im Git-Provider.</summary>
    public string? IssueUrl { get; set; }

    /// <summary>Navigationseigenschaft zur zugehörigen Aufgabe.</summary>
    public Aufgabe Aufgabe { get; set; } = null!;
}
