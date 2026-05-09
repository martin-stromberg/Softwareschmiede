using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.Domain.Entities;

/// <summary>Ergebnis eines einzelnen automatisierten Tests.</summary>
public sealed class TestErgebnis
{
    /// <summary>Eindeutige ID des Testergebnisses.</summary>
    public Guid Id { get; set; }

    /// <summary>ID des zugehörigen Protokolleintrags.</summary>
    public Guid ProtokollEintragId { get; set; }

    /// <summary>Name des Tests.</summary>
    public string TestName { get; set; } = string.Empty;

    /// <summary>Status des Tests.</summary>
    public TestStatus Status { get; set; }

    /// <summary>Fehlermeldung bei fehlgeschlagenem Test.</summary>
    public string? Fehlermeldung { get; set; }

    /// <summary>Dauer des Testlaufs.</summary>
    public TimeSpan Dauer { get; set; }

    /// <summary>Navigationseigenschaft zum zugehörigen Protokolleintrag.</summary>
    public Protokolleintrag Protokolleintrag { get; set; } = null!;
}
