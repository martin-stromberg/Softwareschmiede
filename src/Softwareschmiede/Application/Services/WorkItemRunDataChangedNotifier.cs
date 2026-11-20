namespace Softwareschmiede.Application.Services;

/// <summary>UI-neutraler Notifier fuer persistierte Aenderungen an Aufgaben-Laufdaten.</summary>
public sealed class WorkItemRunDataChangedNotifier
{
    /// <summary>Wird ausgeloest, nachdem Laufdaten einer Aufgabe erfolgreich persistiert wurden.</summary>
    public event Action<Guid>? RunDataChanged;

    /// <summary>Meldet geaenderte Laufdaten fuer die angegebene Aufgabe.</summary>
    /// <param name="workItemId">ID der Aufgabe mit geaenderten Laufdaten.</param>
    public void NotifyRunDataChanged(Guid workItemId)
    {
        RunDataChanged?.Invoke(workItemId);
    }
}
