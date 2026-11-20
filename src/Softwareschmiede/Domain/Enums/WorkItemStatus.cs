namespace Softwareschmiede.Domain.Enums;

/// <summary>Status einer Aufgabe im Entwicklungsprozess.</summary>
public enum WorkItemStatus
{
    /// <summary>Aufgabe wurde erstellt und wartet auf Bearbeitung.</summary>
    New,

    /// <summary>Aufgabe wurde gestartet (Branch erstellt, CLI läuft oder sollte laufen).</summary>
    Started,

    /// <summary>CLI hat Rate-Limit erreicht; wartet auf Wiederaufnahme.</summary>
    Waiting,

    /// <summary>Aufgabe wurde beendet (erfolgreich oder mit Fehler).</summary>
    Completed,

    /// <summary>Aufgabe wurde archiviert und ist nicht mehr aktiv.</summary>
    Archived
}
