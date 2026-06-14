namespace Softwareschmiede.Domain.Enums;

/// <summary>Filtertyp für die Aufgabenliste.</summary>
public enum AufgabenFilterTyp
{
    /// <summary>Zeigt alle Aufgaben.</summary>
    Alle,

    /// <summary>Zeigt nur aktive Aufgaben (nicht archiviert).</summary>
    Aktiv,

    /// <summary>Zeigt nur archivierte Aufgaben.</summary>
    Archiviert
}
