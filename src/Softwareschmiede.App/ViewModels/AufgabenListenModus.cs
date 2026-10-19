namespace Softwareschmiede.App.ViewModels;

/// <summary>Anzeigemodus der aktiven Aufgabenliste (Seitenleiste und Dashboard).</summary>
public enum AufgabenListenModus
{
    /// <summary>Flache Liste, sortiert nach letzter Ausführung (bisheriges Verhalten).</summary>
    Datum,

    /// <summary>Gruppiert nach Repository-Name, innerhalb der Gruppe nach letzter Ausführung.</summary>
    Repository,

    /// <summary>Gruppiert nach aktiver bzw. zuletzt verwendeter KI, innerhalb der Gruppe nach letzter Ausführung.</summary>
    Ki
}
