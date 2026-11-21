using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.Domain.Entities;

/// <summary>Protokolleintrag des KI-Entwicklungsprozesses einer Aufgabe.</summary>
public sealed class LogEntry
{
    /// <summary>Eindeutige ID des Protokolleintrags.</summary>
    public Guid Id { get; set; }

    /// <summary>ID der zugehörigen Aufgabe.</summary>
    public Guid WorkItemId { get; set; }

    /// <summary>Typ des Protokolleintrags.</summary>
    public LogEntryType Type { get; set; }

    /// <summary>Inhalt des Protokolleintrags.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Name des beteiligten Agenten.</summary>
    public string? AgentName { get; set; }

    /// <summary>Zeitstempel des Eintrags.</summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>Navigationseigenschaft zur zugehörigen Aufgabe.</summary>
    public WorkItem WorkItem { get; set; } = null!;

    /// <summary>Zugehörige Testergebnisse (bei Typ TestErgebnis).</summary>
    public List<TestResult> TestResults { get; set; } = [];

    /// <summary>Optionales Diff-Ergebnis, das auf diesen Protokolleintrag referenziert.</summary>
    public DiffResult? DiffResult { get; set; }
}
