using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;

namespace Softwareschmiede.Application.Services;

/// <summary>
/// Koordiniert Benachrichtigungen (Banner und Ton) für Aufgaben-Statuswechsel.
/// </summary>
public sealed class BenachrichtigungsService
{
    private readonly BenachrichtigungsEinstellungenService _einstellungenService;
    private readonly BenachrichtigungsAuditService _auditService;
    private readonly INotificationAudioService? _audioService;
    private readonly INotificationBannerService? _bannerService;
    private readonly IUserContextService _benutzerkontextService;
    private readonly ILogger<BenachrichtigungsService> _logger;

    /// <inheritdoc cref="BenachrichtigungsService"/>
    public BenachrichtigungsService(
        BenachrichtigungsEinstellungenService einstellungenService,
        BenachrichtigungsAuditService auditService,
        IUserContextService benutzerkontextService,
        ILogger<BenachrichtigungsService> logger,
        INotificationAudioService? audioService = null,
        INotificationBannerService? bannerService = null)
    {
        _einstellungenService = einstellungenService;
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
        var einstellungen = await _einstellungenService.GetAsync(benutzerId, ct);
        var ereignisId = Guid.NewGuid();

        await DispatchFuerKanalAsync(
            ereignisId,
            workItemId,
            benutzerId,
            NotificationChannel.Banner,
            einstellungen.BannerMode,
            neuerStatus,
            ct);

        await DispatchFuerKanalAsync(
            ereignisId,
            workItemId,
            benutzerId,
            NotificationChannel.Sound,
            einstellungen.SoundMode,
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
        Guid ereignisId,
        Guid workItemId,
        string benutzerId,
        NotificationChannel kanal,
        NotificationMode modus,
        WorkItemStatus neuerStatus,
        CancellationToken ct)
    {
        if (modus == NotificationMode.Disabled)
        {
            await _auditService.LogAsync(
                ereignisId,
                workItemId,
                benutzerId,
                kanal,
                modus,
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
                    var audio = await _einstellungenService.GetAudioPayloadAsync(benutzerId, ct);
                    if (audio is not null)
                    {
                        var tempPfad = await SchreibeTemporaereAudioDateiAsync(audio.Base64Inhalt, audio.MimeType, ct);
                        if (tempPfad is not null)
                        {
                            try
                            {
                                await PlayAudioAsync(tempPfad, ct);
                            }
                            finally
                            {
                                LöscheDateiSicher(tempPfad);
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
                ereignisId,
                workItemId,
                benutzerId,
                kanal,
                modus,
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
                ereignisId,
                workItemId,
                benutzerId,
                kanal,
                modus,
                NotificationDecision.Failed,
                ex.Message,
                ct);
        }
    }

    private void LöscheDateiSicher(string pfad)
    {
        try
        {
            File.Delete(pfad);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Temporäre Audiodatei konnte nicht gelöscht werden: {Pfad}", pfad);
        }
    }

    private static async Task<string?> SchreibeTemporaereAudioDateiAsync(string base64, string mimeType, CancellationToken ct)
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
