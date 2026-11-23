using System.Windows.Input;
using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.App.ViewModels;

/// <summary>UI-Daten einer Aufgabe in der aktiven Aufgabenliste.</summary>
public sealed class ActiveWorkItemPanelItem : ViewModelBase
{
    private bool _isActive;

    /// <summary>Eindeutige ID der Aufgabe.</summary>
    public Guid Id { get; init; }

    /// <summary>Titel der Aufgabe.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Name des zugehörigen Repositorys (<see cref="Domain.Entities.GitRepository.RepositoryName"/>), Fallback Projektname.</summary>
    public string RepositoryName { get; init; } = string.Empty;

    /// <summary>Anzeigename des SCM-/SCI-Plugins oder gespeicherter Prefix als Fallback.</summary>
    public string? ScmPluginName { get; init; }

    /// <summary>Unicode-Glyph des SCM-Plugins (ToolTip: <see cref="ScmPluginName"/>).</summary>
    public string? ScmPluginSymbol { get; init; }

    /// <summary>Anzeigename des KI-Plugins oder gespeicherter Prefix als Fallback.</summary>
    public string? AiPluginName { get; init; }

    /// <summary>Unicode-Glyph des KI-Plugins (ToolTip: <see cref="AiPluginName"/>).</summary>
    public string? AiPluginSymbol { get; init; }

    /// <summary>Gruppenname im Repository-Modus (Repository-Name oder <see cref="WorkItemListLayout.WithoutRepository"/>).</summary>
    public string RepositoryGroupName { get; init; } = string.Empty;

    /// <summary><see langword="true"/>, wenn <see cref="RepositoryGroupName"/> die Fallback-Gruppe ist (sortiert ans Ende).</summary>
    public bool RepositoryGroupIsFallback { get; init; }

    /// <summary>Gruppenname im KI-Modus (Plugin-Name, roher Prefix oder <see cref="WorkItemListLayout.NotExecuted"/>).</summary>
    public string AiGroupName { get; init; } = string.Empty;

    /// <summary><see langword="true"/>, wenn <see cref="AiGroupName"/> die Fallback-Gruppe ist (sortiert ans Ende).</summary>
    public bool AiGroupIsFallback { get; init; }

    /// <summary>Auftragsart der Aufgabe (<see cref="Domain.Enums.WorkItemKind"/>) — Typ-Markierung auf der Kachel.</summary>
    public WorkItemKind Kind { get; init; }

    /// <summary>Gruppenname im Auftragsart-Modus (deutscher Anzeigename der <see cref="Kind"/>).</summary>
    public string KindGroupName { get; init; } = string.Empty;

    /// <summary>Sortierschlüssel der Auftragsart im Auftragsart-Modus (Enum-Wert; <c>Unspecified</c> sortiert ans Ende).</summary>
    public int KindSortOrder => Kind == WorkItemKind.Unspecified ? int.MaxValue : (int)Kind;

    /// <summary>Sichtbare Typ-Markierung der Kachel (leer, wenn <see cref="KindSymbol"/> <c>null</c> ist — bei <see cref="WorkItemKind.Unspecified"/> und unbekannten Werten).</summary>
    public string KindLabel { get; init; } = string.Empty;

    /// <summary>Unicode-Glyph der Auftragsart für das Kachel-Badge (<c>null</c> bei <see cref="WorkItemKind.Unspecified"/>/unbekannt).</summary>
    public string? KindSymbol { get; init; }

    /// <summary>Sortierzeitpunkt der Liste (letzte Ausführung bzw. Erstellung).</summary>
    public DateTimeOffset SortTimestamp { get; init; }

    /// <summary>Optionaler Laufzeit-Substatus der aktiven CLI-Ausführung.</summary>
    public WorkItemRunStatus? RunStatus { get; init; }

    /// <summary>Live-Substatus aus der Laufzeitquelle (<see cref="Domain.Interfaces.IRunningAutomationStatusSource.GetLiveRunStatus"/>); <c>null</c> = kein laufender gehandelter Prozess.</summary>
    public WorkItemRunStatus? LiveRunStatus { get; init; }

    /// <summary>Aktueller Aufgabenstatus.</summary>
    public WorkItemStatus Status { get; init; }

    /// <summary>Persistierter Lebenszyklusstatus der KI-Ausführung.</summary>
    public WorkItemExecutionStatus ExecutionStatus { get; init; } = WorkItemExecutionStatus.NotStarted;

    /// <summary>Aktive Lauf-ID der KI-Ausführung.</summary>
    public string? ActiveRunId { get; init; }

    /// <summary>Zeitstempel des letzten Heartbeats einer aktiven Ausführung.</summary>
    public DateTimeOffset? LastHeartbeatUtc { get; init; }

    /// <summary>Zeitstempel des letzten echten CLI-Prozessstarts.</summary>
    public DateTimeOffset? LastCliStartUtc { get; init; }

    /// <summary>Optionaler UTC-Zeitpunkt, bis zu dem die Aufgabe pausiert ist (null oder Vergangenheit = nicht pausiert).</summary>
    public DateTimeOffset? PausedUntilUtc { get; init; }

    /// <summary>Gibt an, ob die Aufgabe aktuell pausiert ist (<see cref="PausedUntilUtc"/> liegt in der Zukunft).</summary>
    public bool IsPaused => PausedUntilUtc > DateTimeOffset.UtcNow;

    /// <summary>Gibt an, ob für diese Aufgabe aktuell ein zeitgesteuerter Prompt in der Warteschlange steht.</summary>
    public bool HasScheduledPrompt { get; init; }

    /// <summary>Anzahl offener To-Dos dieser Aufgabe.</summary>
    public int OpenTodoCount { get; init; }

    /// <summary>Anzeigetext für die offenen To-Dos.</summary>
    public string OpenTodoLabelText => OpenTodoCount == 1 ? "1 Todo" : $"{OpenTodoCount} Todos";

    /// <summary>Öffnet den read-only Dialog mit den offenen To-Dos dieser Aufgabe.</summary>
    public ICommand? ShowOpenTodosCommand { get; init; }

    /// <summary>Gibt an, ob diese Aufgabe aktuell im Inhaltsbereich angezeigt wird.</summary>
    public bool IsActive
    {
        get => _isActive;
        set => SetProperty(ref _isActive, value);
    }
}
