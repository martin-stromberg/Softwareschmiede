namespace Softwareschmiede.Domain.Interfaces;

/// <summary>
/// Plattformspezifische Abstraktion für die Anzeige von Banner-Benachrichtigungen.
/// Die WPF-Implementierung verwendet Windows Toast Notifications.
/// </summary>
public interface IBenachrichtigungsBannerService
{
    /// <summary>Zeigt eine Banner-Benachrichtigung mit der angegebenen Nachricht an.</summary>
    Task ShowAsync(string message, CancellationToken ct = default);
}
