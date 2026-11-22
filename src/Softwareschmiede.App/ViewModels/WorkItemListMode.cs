namespace Softwareschmiede.App.ViewModels;

/// <summary>Anzeigemodus der aktiven Aufgabenliste (Seitenleiste und Dashboard).</summary>
public enum WorkItemListMode
{
    /// <summary>Flache Liste, sortiert nach letzter Ausführung (bisheriges Verhalten).</summary>
    Date,

    /// <summary>Gruppiert nach Repository-Name, innerhalb der Gruppe nach letzter Ausführung.</summary>
    Repository,

    /// <summary>Gruppiert nach aktiver bzw. zuletzt verwendeter KI, innerhalb der Gruppe nach letzter Ausführung.</summary>
    Ai,

    /// <summary>Gruppiert nach Auftragsart (<see cref="Domain.Enums.WorkItemKind"/>), innerhalb der Gruppe nach letzter Ausführung.</summary>
    Kind
}
