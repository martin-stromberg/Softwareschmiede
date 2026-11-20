using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.Application.Services;

/// <summary>Parameterbündel der <see cref="KiAusfuehrungsStatusKlassifikation"/>.</summary>
/// <param name="Status">Aktueller Aufgabenstatus.</param>
/// <param name="ExecutionStatus">Persistierter Lebenszyklusstatus der KI-Ausführung.</param>
/// <param name="ActiveRunId">Aktive Lauf-ID der KI-Ausführung.</param>
/// <param name="LastHeartbeatUtc">Zeitstempel des letzten Heartbeats einer aktiven Ausführung.</param>
/// <param name="RunStatus">Persistierter Laufzeit-Substatus der aktiven CLI-Ausführung.</param>
/// <param name="LiveLaufStatus">Live-Substatus aus der Laufzeitquelle (<c>null</c> = kein laufender gehandelter Prozess).</param>
/// <param name="HasScheduledPrompt">Gibt an, ob ein zeitgesteuerter Prompt in der Warteschlange steht.</param>
/// <param name="PausedUntilUtc">UTC-Zeitpunkt, bis zu dem die Aufgabe pausiert ist (null oder Vergangenheit = nicht pausiert).</param>
public sealed record KiAusfuehrungsStatusDaten(
    WorkItemStatus Status,
    WorkItemExecutionStatus ExecutionStatus,
    string? ActiveRunId,
    DateTimeOffset? LastHeartbeatUtc,
    WorkItemRunStatus? RunStatus,
    WorkItemRunStatus? LiveLaufStatus,
    bool HasScheduledPrompt,
    DateTimeOffset? PausedUntilUtc)
{
    /// <summary>
    /// Erstellt die Klassifikations-Eingabe aus einer <see cref="WorkItem"/>-Entität —
    /// gemeinsame Fabrik für alle Aufrufstellen, die die persistierten Aufgabenfelder
    /// auf die positionsabhängige Parameterliste mappen.
    /// </summary>
    /// <param name="workItem">Quell-Entität.</param>
    /// <param name="liveLaufStatus">Live-Substatus aus der Laufzeitquelle (<c>null</c> = kein laufender gehandelter Prozess).</param>
    /// <param name="hasScheduledPrompt">Gibt an, ob ein zeitgesteuerter Prompt in der Warteschlange steht.</param>
    public static KiAusfuehrungsStatusDaten VonAufgabe(
        WorkItem workItem,
        WorkItemRunStatus? liveLaufStatus,
        bool hasScheduledPrompt)
        => new(
            workItem.Status,
            workItem.ExecutionStatus,
            workItem.ActiveRunId,
            workItem.LastHeartbeatUtc,
            workItem.RunStatus,
            liveLaufStatus,
            hasScheduledPrompt,
            workItem.PausedUntilUtc);
}
