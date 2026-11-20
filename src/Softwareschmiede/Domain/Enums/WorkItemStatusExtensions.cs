namespace Softwareschmiede.Domain.Enums;

/// <summary>Erweiterungsmethoden für <see cref="WorkItemStatus"/>.</summary>
public static class WorkItemStatusExtensions
{
    /// <summary>Die Stati, die eine Aufgabe als aktiv (laufend) oder wartend kennzeichnen.</summary>
    public static readonly WorkItemStatus[] ActiveOrWaitingStatuses = [WorkItemStatus.Started, WorkItemStatus.Waiting];

    /// <summary>Gibt an, ob der Status eine aktive oder wartende Aufgabe kennzeichnet (Gestartet oder Wartend).</summary>
    /// <param name="status">Der zu prüfende Status.</param>
    /// <returns><c>true</c>, wenn der Status <see cref="WorkItemStatus.Started"/> oder <see cref="WorkItemStatus.Waiting"/> ist; andernfalls <c>false</c>.</returns>
    public static bool IsActiveOrWaiting(this WorkItemStatus status)
        => ActiveOrWaitingStatuses.Contains(status);

    /// <summary>Deutscher Anzeigetext für protokollierte und benutzerseitig sichtbare Statusmeldungen.</summary>
    public static string ToDisplayText(this WorkItemStatus status) => status switch
    {
        WorkItemStatus.New => "Neu",
        WorkItemStatus.Started => "Gestartet",
        WorkItemStatus.Waiting => "Wartend",
        WorkItemStatus.Completed => "Beendet",
        WorkItemStatus.Archived => "Archiviert",
        _ => status.ToString()
    };
}
