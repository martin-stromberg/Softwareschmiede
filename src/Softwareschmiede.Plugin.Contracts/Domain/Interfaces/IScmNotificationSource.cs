using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Domain.Interfaces;

/// <summary>Optionale Fähigkeit eines SCM-Plugins, die Notifications-Inbox des separaten Kontos zu lesen.
/// Liefert rohe Inbox-Einträge pro Host; die Referenz-Ableitung und Repository-Zuordnung bleibt im Host.</summary>
public interface IScmNotificationSource
{
    /// <summary>Listet die Notification-Einträge des Hosts; <paramref name="sinceUtc"/> begrenzt auf
    /// Einträge mit Aktivität ab diesem Zeitpunkt (<c>null</c> = ohne zeitliche Einschränkung).</summary>
    Task<IReadOnlyList<ScmNotification>> EnumerateNotificationsAsync(string host,
        DateTimeOffset? sinceUtc, CancellationToken ct = default);
}
