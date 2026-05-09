namespace Softwareschmiede.Domain.Enums;

/// <summary>Status einer Aufgabe im Entwicklungsprozess.</summary>
public enum AufgabeStatus
{
    /// <summary>Aufgabe wurde erstellt und wartet auf Bearbeitung.</summary>
    Offen,

    /// <summary>Aufgabe wird manuell bearbeitet.</summary>
    InBearbeitung,

    /// <summary>KI-Agent ist aktiv und bearbeitet die Aufgabe.</summary>
    KiAktiv,

    /// <summary>Automatisierte Tests werden ausgeführt.</summary>
    TestsLaufen,

    /// <summary>Aufgabe wurde erfolgreich abgeschlossen.</summary>
    Abgeschlossen,

    /// <summary>Aufgabe ist fehlgeschlagen.</summary>
    Fehlgeschlagen,

    /// <summary>Aufgabe wurde archiviert und ist nicht mehr aktiv.</summary>
    Archiviert
}
