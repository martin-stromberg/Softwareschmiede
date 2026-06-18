using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.Domain.Entities;

/// <summary>Audit-Eintrag einer Benachrichtigungsentscheidung.</summary>
public sealed class BenachrichtigungsDispatchLog
{
    /// <summary>Primärschlüssel.</summary>
    public Guid Id { get; set; }

    /// <summary>Eindeutige ID des Benachrichtigungsereignisses.</summary>
    public Guid EreignisId { get; set; }

    /// <summary>ID der zugehörigen KI-Aufgabe.</summary>
    public Guid AufgabeId { get; set; }

    /// <summary>Benutzerkennung des Empfängers.</summary>
    public string BenutzerId { get; set; } = string.Empty;

    /// <summary>Verwendeter Benachrichtigungskanal.</summary>
    public BenachrichtigungsKanal Kanal { get; set; }

    /// <summary>Eingestellter Benachrichtigungsmodus zum Zeitpunkt des Versands.</summary>
    public BenachrichtigungsModus Modus { get; set; }

    /// <summary>Ergebnis der Benachrichtigungsauswertung.</summary>
    public BenachrichtigungsEntscheidung Entscheidung { get; set; }

    /// <summary>Begründung der Entscheidung.</summary>
    public string Grund { get; set; } = string.Empty;

    /// <summary>Zeitstempel der Erstellung.</summary>
    public DateTimeOffset ErstelltAm { get; set; }
}
