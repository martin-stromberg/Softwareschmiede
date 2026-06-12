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
    private readonly IBenachrichtigungsAudioService? _audioService;
    private readonly IBenachrichtigungsBannerService? _bannerService;
    private readonly IBenutzerkontextService _benutzerkontextService;
    private readonly ILogger<BenachrichtigungsService> _logger;

    /// <inheritdoc cref="BenachrichtigungsService"/>
    public BenachrichtigungsService(
        BenachrichtigungsEinstellungenService einstellungenService,
        BenachrichtigungsAuditService auditService,
        IBenutzerkontextService benutzerkontextService,
        ILogger<BenachrichtigungsService> logger,
        IBenachrichtigungsAudioService? audioService = null,
        IBenachrichtigungsBannerService? bannerService = null)
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
        Guid aufgabeId,
        AufgabeStatus neuerStatus,
        CancellationToken ct = default)
    {
        var benutzerId = _benutzerkontextService.GetBenutzerId();
        var einstellungen = await _einstellungenService.GetAsync(benutzerId, ct);
        var ereignisId = Guid.NewGuid();

        await DispatchFuerKanalAsync(
            ereignisId,
            aufgabeId,
            benutzerId,
            BenachrichtigungsKanal.Banner,
            einstellungen.BannerModus,
            neuerStatus,
            ct);

        await DispatchFuerKanalAsync(
            ereignisId,
            aufgabeId,
            benutzerId,
            BenachrichtigungsKanal.Ton,
            einstellungen.TonModus,
            neuerStatus,
            ct);
    }

    /// <summary>
    /// Zeigt eine Banner-Benachrichtigung für eine Aufgabe an.
    /// Delegiert an <see cref="IBenachrichtigungsBannerService"/> sofern verfügbar; sonst Logging-Fallback.
    /// </summary>
    public async Task ShowBannerAsync(Guid aufgabeId, string message, CancellationToken ct = default)
    {
        if (_bannerService is not null)
        {
            await _bannerService.ShowAsync(message, ct);
            return;
        }

        _logger.LogInformation(
            "Banner-Benachrichtigung für Aufgabe {AufgabeId}: {Message}",
            aufgabeId,
            message);
    }

    /// <summary>
    /// Spielt eine Audiodatei ab (MP3, WAV oder OGG).
    /// Delegiert an <see cref="IBenachrichtigungsAudioService"/> sofern verfügbar.
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
        Guid aufgabeId,
        string benutzerId,
        BenachrichtigungsKanal kanal,
        BenachrichtigungsModus modus,
        AufgabeStatus neuerStatus,
        CancellationToken ct)
    {
        if (modus == BenachrichtigungsModus.Deaktiviert)
        {
            await _auditService.LogAsync(
                ereignisId,
                aufgabeId,
                benutzerId,
                kanal,
                modus,
                BenachrichtigungsEntscheidung.Unterdrueckt,
                "Modus ist Deaktiviert.",
                ct);
            return;
        }

        try
        {
            switch (kanal)
            {
                case BenachrichtigungsKanal.Banner:
                    await ShowBannerAsync(
                        aufgabeId,
                        $"Aufgabe: Status geändert zu {neuerStatus}",
                        ct);
                    break;
                case BenachrichtigungsKanal.Ton:
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
                aufgabeId,
                benutzerId,
                kanal,
                modus,
                BenachrichtigungsEntscheidung.Gesendet,
                $"Status: {neuerStatus}",
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
                aufgabeId);

            await _auditService.LogAsync(
                ereignisId,
                aufgabeId,
                benutzerId,
                kanal,
                modus,
                BenachrichtigungsEntscheidung.Fehlgeschlagen,
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
