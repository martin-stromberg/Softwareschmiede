using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.Domain.Entities;

/// <summary>Audit-Eintrag einer Benachrichtigungsentscheidung.</summary>
public sealed class NotificationDispatchLog
{
    /// <summary>Primärschlüssel.</summary>
    public Guid Id { get; set; }

    /// <summary>Eindeutige ID des Benachrichtigungsereignisses.</summary>
    public Guid EventId { get; set; }

    /// <summary>ID der zugehörigen KI-Aufgabe.</summary>
    public Guid WorkItemId { get; set; }

    /// <summary>Benutzerkennung des Empfängers.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>Verwendeter Benachrichtigungskanal.</summary>
    public NotificationChannel Channel { get; set; }

    /// <summary>Eingestellter Benachrichtigungsmodus zum Zeitpunkt des Versands.</summary>
    public NotificationMode Mode { get; set; }

    /// <summary>Ergebnis der Benachrichtigungsauswertung.</summary>
    public NotificationDecision Decision { get; set; }

    /// <summary>Begründung der Entscheidung.</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>Zeitstempel der Erstellung.</summary>
    public DateTimeOffset CreatedAt { get; set; }
}
