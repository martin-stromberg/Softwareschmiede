using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.Domain.Entities;

/// <summary>Ergebnis eines einzelnen automatisierten Tests.</summary>
public sealed class TestResult
{
    /// <summary>Eindeutige ID des Testergebnisses.</summary>
    public Guid Id { get; set; }

    /// <summary>ID des zugehörigen Protokolleintrags.</summary>
    public Guid LogEntryId { get; set; }

    /// <summary>Name des Tests.</summary>
    public string TestName { get; set; } = string.Empty;

    /// <summary>Status des Tests.</summary>
    public TestStatus Status { get; set; }

    /// <summary>Fehlermeldung bei fehlgeschlagenem Test.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Dauer des Testlaufs.</summary>
    public TimeSpan Duration { get; set; }

    /// <summary>Navigationseigenschaft zum zugehörigen Protokolleintrag.</summary>
    public LogEntry LogEntry { get; set; } = null!;
}
