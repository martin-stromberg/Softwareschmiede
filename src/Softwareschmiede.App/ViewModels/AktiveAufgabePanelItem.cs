using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.App.ViewModels;

/// <summary>UI-Daten einer Aufgabe in der aktiven Aufgabenliste.</summary>
public sealed class AktiveAufgabePanelItem : ViewModelBase
{
    private bool _isAktiv;

    /// <summary>Eindeutige ID der Aufgabe.</summary>
    public Guid Id { get; init; }

    /// <summary>Titel der Aufgabe.</summary>
    public string Titel { get; init; } = string.Empty;

    /// <summary>Name des zugehörigen Projekts.</summary>
    public string ProjektName { get; init; } = string.Empty;

    /// <summary>Anzeigename des SCM-/SCI-Plugins oder gespeicherter Prefix als Fallback.</summary>
    public string? ScmPluginName { get; init; }

    /// <summary>Anzeigename des KI-Plugins oder gespeicherter Prefix als Fallback.</summary>
    public string? KiPluginName { get; init; }

    /// <summary>Optionaler Laufzeit-Substatus der aktiven CLI-Ausführung.</summary>
    public AufgabeLaufStatus? LaufStatus { get; init; }

    /// <summary>Aktueller Aufgabenstatus.</summary>
    public AufgabeStatus Status { get; init; }

    /// <summary>Aktive Lauf-ID der KI-Ausführung.</summary>
    public string? AktiveRunId { get; init; }

    /// <summary>Zeitstempel des letzten Heartbeats einer aktiven Ausführung.</summary>
    public DateTimeOffset? LastHeartbeatUtc { get; init; }

    /// <summary>Zeitstempel des letzten echten CLI-Prozessstarts.</summary>
    public DateTimeOffset? LetzterCliStartUtc { get; init; }

    /// <summary>Gibt an, ob diese Aufgabe aktuell im Inhaltsbereich angezeigt wird.</summary>
    public bool IsAktiv
    {
        get => _isAktiv;
        set => SetProperty(ref _isAktiv, value);
    }
}
