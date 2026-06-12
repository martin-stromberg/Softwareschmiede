using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Interfaces;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace Softwareschmiede.App.Services;

/// <summary>
/// WPF-Implementierung von <see cref="IBenachrichtigungsBannerService"/>.
/// Verwendet die Windows Toast Notification API für Desktop-Banner.
/// </summary>
public sealed class WpfBannerService : IBenachrichtigungsBannerService
{
    private const string AppId = "Softwareschmiede";

    private readonly ILogger<WpfBannerService> _logger;

    /// <inheritdoc cref="WpfBannerService"/>
    public WpfBannerService(ILogger<WpfBannerService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task ShowAsync(string message, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        try
        {
            var toastXml = ToastNotificationManager.GetTemplateContent(ToastTemplateType.ToastText02);

            var textNodes = toastXml.GetElementsByTagName("text");
            ((XmlElement)textNodes[0]).AppendChild(toastXml.CreateTextNode("Softwareschmiede"));
            ((XmlElement)textNodes[1]).AppendChild(toastXml.CreateTextNode(message));

            var toast = new ToastNotification(toastXml);
            ToastNotificationManager.CreateToastNotifier(AppId).Show(toast);

            _logger.LogDebug("Toast-Benachrichtigung angezeigt: {Message}", message);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Toast-Benachrichtigung konnte nicht angezeigt werden: {Message}", message);
        }

        return Task.CompletedTask;
    }
}
