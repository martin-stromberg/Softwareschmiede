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
    // Hinweis: Diese AppId muss als AUMID (Application User Model ID) in der Windows-Registry
    // registriert sein, damit Toast-Benachrichtigungen funktionieren. Bei nicht-paketierten
    // Installationen (ohne MSIX/AppX) ist die AUMID nicht automatisch registriert, was dazu führt,
    // dass ToastNotificationManager.CreateToastNotifier() einen COM-Fehler wirft.
    // Der Fehler wird in ShowAsync() abgefangen und protokolliert; die Anwendung läuft weiter,
    // aber Toast-Benachrichtigungen erscheinen nicht. Für eine vollständige Unterstützung muss
    // die Anwendung entweder als MSIX paketiert oder die AUMID manuell über die Registry registriert werden.
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
        if (ct.IsCancellationRequested)
        {
            return Task.FromCanceled(ct);
        }

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
