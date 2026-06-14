using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.Application.Services;

/// <summary>Einstellungen für Banner- und Ton-Benachrichtigungen.</summary>
public sealed record BenachrichtigungsEinstellungenDto(
    /// <summary>Modus für visuelle Banner-Benachrichtigungen.</summary>
    BenachrichtigungsModus BannerModus,
    /// <summary>Modus für Ton-Benachrichtigungen.</summary>
    BenachrichtigungsModus TonModus);

/// <summary>Metadaten einer benutzerdefinierten Audio-Datei.</summary>
public sealed record BenachrichtigungsAudioInfoDto(
    /// <summary>Gibt an, ob eine benutzerdefinierte Datei hochgeladen wurde.</summary>
    bool HatBenutzerdefinierteDatei,
    /// <summary>Dateiname der hochgeladenen Audio-Datei, falls vorhanden.</summary>
    string? Dateiname,
    /// <summary>MIME-Typ der Audio-Datei, falls vorhanden.</summary>
    string? MimeType,
    /// <summary>Dateigröße in Bytes, falls vorhanden.</summary>
    int? GroesseBytes);

/// <summary>Base64-kodierter Audio-Payload für die Wiedergabe im Client.</summary>
public sealed record BenachrichtigungsAudioPayload(
    /// <summary>MIME-Typ der Audio-Datei.</summary>
    string MimeType,
    /// <summary>Base64-kodierter Inhalt der Audio-Datei.</summary>
    string Base64Inhalt);
