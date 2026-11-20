namespace Softwareschmiede.App.Behaviors;

/// <summary>Merkt je Aufgabe den zuletzt beobachteten Status und erkennt echte Statuswechsel.</summary>
public sealed class StatusAenderungsErkennung
{
    private readonly Dictionary<Guid, string?> _letzterStatus = new();

    /// <summary>Prüft, ob sich der Status einer Aufgabe gegenüber der letzten Beobachtung geändert hat, und merkt den neuen Status.</summary>
    /// <param name="workItemId">Die ID der Aufgabe.</param>
    /// <param name="newStatus">Der aktuell beobachtete Status.</param>
    /// <returns><c>false</c> bei Erstbeobachtung oder unverändertem Status, <c>true</c> bei einem echten Wechsel.</returns>
    public bool HatSichGeaendert(Guid workItemId, string? newStatus)
    {
        var hatSichGeaendert = _letzterStatus.TryGetValue(workItemId, out var bisherigerStatus) && bisherigerStatus != newStatus;
        _letzterStatus[workItemId] = newStatus;
        return hatSichGeaendert;
    }

    /// <summary>Entfernt die gemerkte Beobachtung für eine Aufgabe, z. B. wenn das zugehörige UI-Element entladen wird.</summary>
    /// <param name="workItemId">Die ID der Aufgabe.</param>
    public void Remove(Guid workItemId) => _letzterStatus.Remove(workItemId);
}
