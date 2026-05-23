namespace Softwareschmiede.Domain.Enums;

/// <summary>Ergebnis der Benachrichtigungsauswertung pro Ereignis und Kanal.</summary>
public enum BenachrichtigungsEntscheidung
{
    Gesendet = 0,
    Unterdrueckt = 1,
    Zurueckgestellt = 2,
    Fehlgeschlagen = 3
}
