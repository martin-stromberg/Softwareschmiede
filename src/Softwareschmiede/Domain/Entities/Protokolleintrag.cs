using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.Domain.Entities;

/// <summary>Protokolleintrag des KI-Entwicklungsprozesses einer Aufgabe.</summary>
public sealed class Protokolleintrag
{
    /// <summary>Eindeutige ID des Protokolleintrags.</summary>
    public Guid Id { get; set; }

    /// <summary>ID der zugehörigen Aufgabe.</summary>
    public Guid AufgabeId { get; set; }

    /// <summary>Typ des Protokolleintrags.</summary>
    public ProtokollTyp Typ { get; set; }

    /// <summary>Inhalt des Protokolleintrags.</summary>
    public string Inhalt { get; set; } = string.Empty;

    /// <summary>Name des beteiligten Agenten.</summary>
    public string? AgentName { get; set; }

    /// <summary>Zeitstempel des Eintrags.</summary>
    public DateTimeOffset Zeitstempel { get; set; }

    /// <summary>Navigationseigenschaft zur zugehörigen Aufgabe.</summary>
    public Aufgabe Aufgabe { get; set; } = null!;

    /// <summary>Zugehörige Testergebnisse (bei Typ TestErgebnis).</summary>
    public List<TestErgebnis> TestErgebnisse { get; set; } = [];
}
