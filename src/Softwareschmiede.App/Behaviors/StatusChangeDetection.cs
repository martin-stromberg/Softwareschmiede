namespace Softwareschmiede.App.Behaviors;

/// <summary>Merkt je Aufgabe den zuletzt beobachteten Status und erkennt echte Statuswechsel.</summary>
public sealed class StatusChangeDetection
{
    private readonly Dictionary<Guid, string?> _lastStatus = new();

    /// <summary>Prüft, ob sich der Status einer Aufgabe gegenüber der letzten Beobachtung geändert hat, und merkt den neuen Status.</summary>
    /// <param name="workItemId">Die ID der Aufgabe.</param>
    /// <param name="newStatus">Der aktuell beobachtete Status.</param>
    /// <returns><c>false</c> bei Erstbeobachtung oder unverändertem Status, <c>true</c> bei einem echten Wechsel.</returns>
    public bool HasSichChanged(Guid workItemId, string? newStatus)
    {
        var hasSichChanged = _lastStatus.TryGetValue(workItemId, out var bisherigerStatus) && bisherigerStatus != newStatus;
        _lastStatus[workItemId] = newStatus;
        return hasSichChanged;
    }

    /// <summary>Entfernt die gemerkte Beobachtung für eine Aufgabe, z. B. wenn das zugehörige UI-Element entladen wird.</summary>
    /// <param name="workItemId">Die ID der Aufgabe.</param>
    public void Remove(Guid workItemId) => _lastStatus.Remove(workItemId);
}
