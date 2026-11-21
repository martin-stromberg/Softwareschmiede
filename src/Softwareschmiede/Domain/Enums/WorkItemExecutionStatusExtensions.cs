namespace Softwareschmiede.Domain.Enums;

/// <summary>Erweiterungsmethoden fuer den persistenten KI-Ausfuehrungsstatus.</summary>
public static class WorkItemExecutionStatusExtensions
{
    /// <summary>Gibt an, ob fuer die Aufgabe eine KI-Ausfuehrung explizit gestartet werden darf.</summary>
    public static bool MayStartExecution(this WorkItemExecutionStatus executionStatus, WorkItemStatus workItemStatus)
        => workItemStatus is not WorkItemStatus.Completed and not WorkItemStatus.Archived
            && executionStatus is (WorkItemExecutionStatus.NotStarted or WorkItemExecutionStatus.Completed);

    /// <summary>Gibt an, ob die CLI-Ansicht fuer die Aufgabe angezeigt werden soll. Dies ist der Fall, wenn die Aufgabe aktiv oder wartend ist und die KI-Ausfuehrung aktiv oder beendet ist.</summary>
    /// <param name="executionStatus">Der zu pruefende KI-Ausfuehrungsstatus.</param>
    /// <param name="workItemStatus">Der zu pruefende Aufgabenstatus.</param>
    /// <returns><c>true</c>, wenn die CLI-Ansicht angezeigt werden soll; andernfalls <c>false</c>.</returns>
    public static bool ShouldShowCli(this WorkItemExecutionStatus executionStatus, WorkItemStatus workItemStatus)
        => workItemStatus.IsActiveOrWaiting()
            && executionStatus is (WorkItemExecutionStatus.Active or WorkItemExecutionStatus.Completed);
}
