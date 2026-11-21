namespace Softwareschmiede.Domain.Enums;

/// <summary>Filtertyp für die Aufgabenliste.</summary>
public enum WorkItemFilterType
{
    /// <summary>Zeigt alle Aufgaben.</summary>
    All,

    /// <summary>Zeigt nur aktive Aufgaben (nicht archiviert).</summary>
    Active,

    /// <summary>Zeigt nur archivierte Aufgaben.</summary>
    Archived
}
