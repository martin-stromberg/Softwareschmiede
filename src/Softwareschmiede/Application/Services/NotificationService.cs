using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;

namespace Softwareschmiede.Application.Services;

/// <summary>
/// Koordiniert Benachrichtigungen (Banner und Ton) für Aufgaben-Statuswechsel.
/// </summary>
public sealed class NotificationService
{
    private readonly NotificationSettingsService _settingsService;
    private readonly NotificationAuditService _auditService;
    private readonly INotificationAudioService? _audioService;
    private readonly INotificationBannerService? _bannerService;
    private readonly IUserContextService _benutzerkontextService;
    private readonly ILogger<NotificationService> _logger;

    /// <inheritdoc cref="NotificationService"/>
    public NotificationService(
        NotificationSettingsService settingsService,
        NotificationAuditService auditService,
        IUserContextService benutzerkontextService,
        ILogger<NotificationService> logger,
        INotificationAudioService? audioService = null,
        INotificationBannerService? bannerService = null)
    {
        _settingsService = settingsService;
        _auditService = auditService;
        _benutzerkontextService = benutzerkontextService;
        _logger = logger;
        _audioService = audioService;
        _bannerService = bannerService;
    }

    /// <summary>
    /// Prüft ob eine Benachrichtigung für den gegebenen Kanal ausgelöst werden soll,
    /// und sendet sie wenn nötig.
    /// </summary>
    public async Task DispatchAsync(
        Guid workItemId,
        WorkItemStatus neuerStatus,
        CancellationToken ct = default)
    {
        var benutzerId = _benutzerkontextService.GetUserId();
        var settings = await _settingsService.GetAsync(benutzerId, ct);
        var eventId = Guid.NewGuid();

        await DispatchFuerKanalAsync(
            eventId,
            workItemId,
            benutzerId,
            NotificationChannel.Banner,
            settings.BannerMode,
            neuerStatus,
            ct);

        await DispatchFuerKanalAsync(
            eventId,
            workItemId,
            benutzerId,
            NotificationChannel.Sound,
            settings.SoundMode,
            neuerStatus,
            ct);
    }

    /// <summary>
    /// Zeigt eine Banner-Benachrichtigung für eine Aufgabe an.
    /// Delegiert an <see cref="INotificationBannerService"/> sofern verfügbar; sonst Logging-Fallback.
    /// </summary>
    public async Task ShowBannerAsync(Guid workItemId, string message, CancellationToken ct = default)
    {
        if (_bannerService is not null)
        {
            await _bannerService.ShowAsync(message, ct);
            return;
        }

        _logger.LogInformation(
            "Banner-Benachrichtigung für Aufgabe {AufgabeId}: {Message}",
            workItemId,
            message);
    }

    /// <summary>
    /// Spielt eine Audiodatei ab (MP3, WAV oder OGG).
    /// Delegiert an <see cref="INotificationAudioService"/> sofern verfügbar.
    /// </summary>
    public async Task PlayAudioAsync(string filePath, CancellationToken ct = default)
    {
        if (_audioService is null)
        {
            _logger.LogDebug("Audio-Service nicht verfügbar. Datei: {FilePath}", filePath);
            return;
        }

        if (!File.Exists(filePath))
        {
            _logger.LogWarning("Audiodatei nicht gefunden: {FilePath}", filePath);
            return;
        }

        try
        {
            await _audioService.PlayAudioAsync(filePath, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Abspielen der Audiodatei '{FilePath}'.", filePath);
        }
    }

    private async Task DispatchFuerKanalAsync(
        Guid eventId,
        Guid workItemId,
        string benutzerId,
        NotificationChannel kanal,
        NotificationMode mode,
        WorkItemStatus neuerStatus,
        CancellationToken ct)
    {
        if (mode == NotificationMode.Disabled)
        {
            await _auditService.LogAsync(
                eventId,
                workItemId,
                benutzerId,
                kanal,
                mode,
                NotificationDecision.Suppressed,
                "Modus ist Deaktiviert.",
                ct);
            return;
        }

        try
        {
            switch (kanal)
            {
                case NotificationChannel.Banner:
                    await ShowBannerAsync(
                        workItemId,
                        $"Aufgabe: Status geändert zu {neuerStatus.ToDisplayText()}",
                        ct);
                    break;
                case NotificationChannel.Sound:
                    var audio = await _settingsService.GetAudioPayloadAsync(benutzerId, ct);
                    if (audio is not null)
                    {
                        var tempPfad = await WriteTemporaryAudioFileAsync(audio.Base64Inhalt, audio.MimeType, ct);
                        if (tempPfad is not null)
                        {
                            try
                            {
                                await PlayAudioAsync(tempPfad, ct);
                            }
                            finally
                            {
                                DeleteFileSafely(tempPfad);
                            }
                        }
                    }
                    else
                    {
                        _logger.LogDebug("Keine Audiodatei für Benutzer konfiguriert; Ton-Benachrichtigung übersprungen.");
                    }

                    break;
            }

            await _auditService.LogAsync(
                eventId,
                workItemId,
                benutzerId,
                kanal,
                mode,
                NotificationDecision.Sent,
                $"Status: {neuerStatus.ToDisplayText()}",
                ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Fehler beim Versenden der {Kanal}-Benachrichtigung für Aufgabe {AufgabeId}.",
                kanal,
                workItemId);

            await _auditService.LogAsync(
                eventId,
                workItemId,
                benutzerId,
                kanal,
                mode,
                NotificationDecision.Failed,
                ex.Message,
                ct);
        }
    }

    private void DeleteFileSafely(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Temporäre Audiodatei konnte nicht gelöscht werden: {Pfad}", path);
        }
    }

    private static async Task<string?> WriteTemporaryAudioFileAsync(string base64, string mimeType, CancellationToken ct)
    {
        try
        {
            var extension = mimeType.ToLowerInvariant() switch
            {
                "audio/mpeg" or "audio/mp3" => ".mp3",
                "audio/wav" or "audio/x-wav" or "audio/wave" => ".wav",
                "audio/ogg" or "application/ogg" => ".ogg",
                _ => ".bin"
            };

            var tempPfad = Path.Combine(Path.GetTempPath(), $"softwareschmiede-audio-{Guid.NewGuid():N}{extension}");
            var bytes = Convert.FromBase64String(base64);
            await File.WriteAllBytesAsync(tempPfad, bytes, ct);
            return tempPfad;
        }
        catch
        {
            return null;
        }
    }
}
