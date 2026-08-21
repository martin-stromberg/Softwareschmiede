using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.Application.Services;

/// <summary>Einstellungen für Banner- und Ton-Benachrichtigungen.</summary>
/// <param name="BannerModus">Modus für visuelle Banner-Benachrichtigungen.</param>
/// <param name="TonModus">Modus für Ton-Benachrichtigungen.</param>
public sealed record BenachrichtigungsEinstellungenDto(
    BenachrichtigungsModus BannerModus,
    BenachrichtigungsModus TonModus);

/// <summary>Metadaten einer benutzerdefinierten Audio-Datei.</summary>
/// <param name="HatBenutzerdefinierteDatei">Gibt an, ob eine benutzerdefinierte Datei hochgeladen wurde.</param>
/// <param name="Dateiname">Dateiname der hochgeladenen Audio-Datei, falls vorhanden.</param>
/// <param name="MimeType">MIME-Typ der Audio-Datei, falls vorhanden.</param>
/// <param name="GroesseBytes">Dateigröße in Bytes, falls vorhanden.</param>
public sealed record BenachrichtigungsAudioInfoDto(
    bool HatBenutzerdefinierteDatei,
    string? Dateiname,
    string? MimeType,
    int? GroesseBytes);

/// <summary>Base64-kodierter Audio-Payload für die Wiedergabe im Client.</summary>
/// <param name="MimeType">MIME-Typ der Audio-Datei.</param>
/// <param name="Base64Inhalt">Base64-kodierter Inhalt der Audio-Datei.</param>
public sealed record BenachrichtigungsAudioPayload(
    string MimeType,
    string Base64Inhalt);
