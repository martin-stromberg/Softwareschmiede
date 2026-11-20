using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.Infrastructure.Terminal;

/// <summary>
/// Zentrales Mapping des Laufzeitstatus einer CLI-Sitzung (<see cref="CliRuntimeStatus"/>) auf den
/// persistierten Domain-Substatus <see cref="WorkItemRunStatus"/>. Einzige Quelle der Wahrheit für
/// diese Abbildung — vorher existierte sie als identischer Switch an mehreren Stellen
/// (CliProcessManager, TaskDetailViewModel, AiExecutionService) und konnte bei einer
/// Enum-Erweiterung auseinanderlaufen.
/// </summary>
public static class CliRuntimeStatusExtensions
{
    /// <summary>
    /// Bildet einen <see cref="CliRuntimeStatus"/> auf <see cref="WorkItemRunStatus"/> ab:
    /// <see cref="CliRuntimeStatus.WaitingForInput"/> → <see cref="WorkItemRunStatus.WaitingForInput"/>,
    /// <see cref="CliRuntimeStatus.Running"/> → <see cref="WorkItemRunStatus.Running"/>,
    /// <see cref="CliRuntimeStatus.Inactive"/> → <c>null</c> (kein persistierbarer Substatus — das
    /// Beenden des Laufs läuft über die separaten <c>CliProcessStatus</c>-Ereignisse).
    /// Der <c>null</c>-Rückgabewert hat je nach Aufrufkontext unterschiedliche Bedeutung; Aufrufer
    /// wählen ihren Fallback selbst (z. B. <c>?? WorkItemRunStatus.Running</c> in
    /// <c>AiExecutionService.GetLiveRunStatus</c>: kein/aktiver Substatus meldet sich dort als
    /// „Läuft").
    /// </summary>
    /// <param name="status">Der abzubildende Laufzeitstatus.</param>
    /// <returns>Der persistierbare Substatus, oder <c>null</c>.</returns>
    public static WorkItemRunStatus? ToWorkItemRunStatus(this CliRuntimeStatus status)
        => status switch
        {
            CliRuntimeStatus.WaitingForInput => WorkItemRunStatus.WaitingForInput,
            CliRuntimeStatus.Running => WorkItemRunStatus.Running,
            _ => null
        };
}
