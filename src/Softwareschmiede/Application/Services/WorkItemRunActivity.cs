namespace Softwareschmiede.Application.Services;

/// <summary>Ermittelt einheitlich, ob der CLI-Lauf einer Aufgabe anhand des Heartbeats aktuell als aktiv gilt.</summary>
public static class WorkItemRunActivity
{
    /// <summary>
    /// Liefert <c>true</c>, wenn <paramref name="activeRunId"/> gesetzt ist und
    /// <paramref name="lastHeartbeatUtc"/> jünger als <see cref="WorkItemRecoveryService.HeartbeatTimeoutMinutes"/> ist.
    /// </summary>
    /// <param name="activeRunId">Die aktive Lauf-ID der Aufgabe, sofern vorhanden.</param>
    /// <param name="lastHeartbeatUtc">Zeitpunkt des letzten Heartbeats in UTC, sofern vorhanden.</param>
    /// <param name="nowUtc">Der als "jetzt" zu verwendende Zeitpunkt in UTC.</param>
    /// <returns><c>true</c>, wenn der Lauf als aktiv gilt, sonst <c>false</c>.</returns>
    public static bool IsActive(string? activeRunId, DateTimeOffset? lastHeartbeatUtc, DateTimeOffset nowUtc)
        => activeRunId != null
            && lastHeartbeatUtc != null
            && nowUtc - lastHeartbeatUtc.Value < TimeSpan.FromMinutes(WorkItemRecoveryService.HeartbeatTimeoutMinutes);
}
