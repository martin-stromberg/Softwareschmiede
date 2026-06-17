namespace Softwareschmiede.Domain.Enums;

/// <summary>Status einer Aufgabe im Entwicklungsprozess.</summary>
public enum AufgabeStatus
{
    /// <summary>Aufgabe wurde erstellt und wartet auf Bearbeitung.</summary>
    Neu,

    /// <summary>Aufgabe wurde gestartet (Branch erstellt, CLI läuft oder sollte laufen).</summary>
    Gestartet,

    /// <summary>CLI hat Rate-Limit erreicht; wartet auf Wiederaufnahme.</summary>
    Wartend,

    /// <summary>Aufgabe wurde beendet (erfolgreich oder mit Fehler).</summary>
    Beendet,

    /// <summary>Aufgabe wurde archiviert und ist nicht mehr aktiv.</summary>
    Archiviert
}
