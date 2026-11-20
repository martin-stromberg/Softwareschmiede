namespace Softwareschmiede.Domain.Enums;

/// <summary>Ergebnis der Benachrichtigungsauswertung pro Ereignis und Kanal.</summary>
public enum NotificationDecision
{
    /// <summary>Benachrichtigung wurde erfolgreich gesendet.</summary>
    Sent = 0,
    /// <summary>Benachrichtigung wurde unterdrückt.</summary>
    Suppressed = 1,
    /// <summary>Benachrichtigung wurde zurückgestellt.</summary>
    Deferred = 2,
    /// <summary>Versand der Benachrichtigung ist fehlgeschlagen.</summary>
    Failed = 3
}
