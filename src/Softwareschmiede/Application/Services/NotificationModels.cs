using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.Application.Services;

/// <summary>Einstellungen für Banner- und Ton-Benachrichtigungen.</summary>
/// <param name="BannerMode">Modus für visuelle Banner-Benachrichtigungen.</param>
/// <param name="SoundMode">Modus für Ton-Benachrichtigungen.</param>
public sealed record NotificationSettingsDto(
    NotificationMode BannerMode,
    NotificationMode SoundMode);

/// <summary>Metadaten einer benutzerdefinierten Audio-Datei.</summary>
/// <param name="HasCustomFile">Gibt an, ob eine benutzerdefinierte Datei hochgeladen wurde.</param>
/// <param name="FileName">Dateiname der hochgeladenen Audio-Datei, falls vorhanden.</param>
/// <param name="MimeType">MIME-Typ der Audio-Datei, falls vorhanden.</param>
/// <param name="SizeBytes">Dateigröße in Bytes, falls vorhanden.</param>
public sealed record NotificationAudioInfoDto(
    bool HasCustomFile,
    string? FileName,
    string? MimeType,
    int? SizeBytes);

/// <summary>Base64-kodierter Audio-Payload für die Wiedergabe im Client.</summary>
/// <param name="MimeType">MIME-Typ der Audio-Datei.</param>
/// <param name="Base64Content">Base64-kodierter Inhalt der Audio-Datei.</param>
public sealed record NotificationAudioPayload(
    string MimeType,
    string Base64Content);
