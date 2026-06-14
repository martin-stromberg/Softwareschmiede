namespace Softwareschmiede.Domain.Enums;

/// <summary>Ergebnis der Benachrichtigungsauswertung pro Ereignis und Kanal.</summary>
public enum BenachrichtigungsEntscheidung
{
    /// <summary>Benachrichtigung wurde erfolgreich gesendet.</summary>
    Gesendet = 0,
    /// <summary>Benachrichtigung wurde unterdrückt.</summary>
    Unterdrueckt = 1,
    /// <summary>Benachrichtigung wurde zurückgestellt.</summary>
    Zurueckgestellt = 2,
    /// <summary>Versand der Benachrichtigung ist fehlgeschlagen.</summary>
    Fehlgeschlagen = 3
}
