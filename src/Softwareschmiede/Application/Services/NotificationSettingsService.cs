using Microsoft.EntityFrameworkCore;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Infrastructure.Data;

namespace Softwareschmiede.Application.Services;

/// <summary>Verwaltet benutzerbezogene Einstellungen und Audio-Dateien für KI-Benachrichtigungen.</summary>
public sealed class NotificationSettingsService
{
    /// <summary>Maximale erlaubte Audio-Dateigröße in Bytes (10 MB).</summary>
    public const int MaxAudioFileSizeBytes = 10 * 1024 * 1024;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3",
        ".wav",
        ".ogg"
    };

    private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "audio/mpeg",
        "audio/mp3",
        "audio/wav",
        "audio/x-wav",
        "audio/wave",
        "audio/ogg",
        "application/ogg"
    };

    private readonly SoftwareschmiedeDbContext _db;

    /// <inheritdoc cref="NotificationSettingsService"/>
    public NotificationSettingsService(SoftwareschmiedeDbContext db)
    {
        _db = db;
    }

    /// <summary>Gibt die gespeicherten Benachrichtigungseinstellungen des Benutzers zurück.</summary>
    public async Task<NotificationSettingsDto> GetAsync(string benutzerId, CancellationToken ct = default)
    {
        var setting = await _db.NotificationSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == benutzerId, ct);

        if (setting is null)
        {
            return new NotificationSettingsDto(
                NotificationMode.Banner,
                NotificationMode.Disabled);
        }

        return new NotificationSettingsDto(setting.BannerMode, setting.SoundMode);
    }

    /// <summary>Speichert die Benachrichtigungseinstellungen des Benutzers.</summary>
    public async Task SaveAsync(string benutzerId, NotificationSettingsDto dto, CancellationToken ct = default)
    {
        var setting = await _db.NotificationSettings
            .FirstOrDefaultAsync(s => s.UserId == benutzerId, ct);

        if (setting is null)
        {
            setting = new NotificationSetting
            {
                Id = Guid.NewGuid(),
                UserId = benutzerId
            };
            _db.NotificationSettings.Add(setting);
        }

        setting.BannerMode = dto.BannerMode;
        setting.SoundMode = dto.SoundMode;
        setting.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Gibt Metadaten der hochgeladenen Audio-Datei des Benutzers zurück.</summary>
    public async Task<NotificationAudioInfoDto> GetAudioInfoAsync(string benutzerId, CancellationToken ct = default)
    {
        var audio = await _db.NotificationAudioFiles
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == benutzerId, ct);

        if (audio is null)
        {
            return new NotificationAudioInfoDto(false, null, null, null);
        }

        return new NotificationAudioInfoDto(true, audio.OriginalFileName, audio.MimeType, audio.SizeBytes);
    }

    /// <summary>Gibt den Base64-kodierten Audio-Payload des Benutzers zurück.</summary>
    public async Task<NotificationAudioPayload?> GetAudioPayloadAsync(string benutzerId, CancellationToken ct = default)
    {
        var audio = await _db.NotificationAudioFiles
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == benutzerId, ct);

        if (audio is null || audio.Content.Length == 0)
        {
            return null;
        }

        return new NotificationAudioPayload(audio.MimeType, Convert.ToBase64String(audio.Content));
    }

    /// <summary>Speichert eine neue Audio-Datei für den Benutzer.</summary>
    public async Task UploadAudioAsync(
        string benutzerId,
        string fileName,
        string? mimeType,
        byte[] content,
        CancellationToken ct = default)
    {
        ValidateAudioFile(fileName, mimeType, content);

        var existing = await _db.NotificationAudioFiles
            .FirstOrDefaultAsync(s => s.UserId == benutzerId, ct);

        if (existing is null)
        {
            existing = new NotificationAudioFile
            {
                Id = Guid.NewGuid(),
                UserId = benutzerId
            };
            _db.NotificationAudioFiles.Add(existing);
        }

        existing.OriginalFileName = SanitizeFileName(fileName);
        existing.MimeType = NormalizeMimeType(mimeType, fileName);
        existing.SizeBytes = content.Length;
        existing.Content = content;
        existing.UploadedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Löscht die benutzerdefinierte Audio-Datei des Benutzers.</summary>
    public async Task RemoveAudioAsync(string benutzerId, CancellationToken ct = default)
    {
        var existing = await _db.NotificationAudioFiles
            .FirstOrDefaultAsync(s => s.UserId == benutzerId, ct);
        if (existing is null)
        {
            return;
        }

        _db.NotificationAudioFiles.Remove(existing);
        await _db.SaveChangesAsync(ct);
    }

    private static void ValidateAudioFile(string fileName, string? mimeType, byte[] content)
    {
        if (content.Length == 0)
        {
            throw new ArgumentException("Die Datei ist leer.");
        }

        if (content.Length > MaxAudioFileSizeBytes)
        {
            throw new ArgumentException("Die Datei überschreitet das Limit von 10 MB.");
        }

        var extension = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
        {
            throw new ArgumentException("Ungültiges Audioformat. Erlaubt sind mp3, wav und ogg.");
        }

        if (!string.IsNullOrWhiteSpace(mimeType) && !AllowedMimeTypes.Contains(mimeType.Trim()))
        {
            throw new ArgumentException("Ungültiger MIME-Typ für Audio-Datei.");
        }

        if (!HasValidFileSignature(extension, content))
        {
            throw new ArgumentException("Die Datei-Signatur passt nicht zum gewählten Audioformat.");
        }
    }

    private static string SanitizeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return $"audio-{Guid.NewGuid():N}.bin";
        }

        return name.Length > 240 ? name[..240] : name;
    }

    private static string NormalizeMimeType(string? mimeType, string fileName)
    {
        if (!string.IsNullOrWhiteSpace(mimeType))
        {
            return mimeType.Trim();
        }

        return Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".mp3" => "audio/mpeg",
            ".wav" => "audio/wav",
            ".ogg" => "audio/ogg",
            _ => "application/octet-stream"
        };
    }

    private static bool HasValidFileSignature(string extension, byte[] content)
    {
        return extension.ToLowerInvariant() switch
        {
            ".mp3" => IsMp3(content),
            ".wav" => IsWav(content),
            ".ogg" => IsOgg(content),
            _ => false
        };
    }

    private static bool IsMp3(IReadOnlyList<byte> bytes)
    {
        if (bytes.Count < 3)
        {
            return false;
        }

        if (bytes[0] == 0x49 && bytes[1] == 0x44 && bytes[2] == 0x33)
        {
            return true;
        }

        return bytes.Count >= 2 && bytes[0] == 0xFF && (bytes[1] & 0xE0) == 0xE0;
    }

    private static bool IsWav(IReadOnlyList<byte> bytes)
    {
        return bytes.Count >= 12
               && bytes[0] == 0x52
               && bytes[1] == 0x49
               && bytes[2] == 0x46
               && bytes[3] == 0x46
               && bytes[8] == 0x57
               && bytes[9] == 0x41
               && bytes[10] == 0x56
               && bytes[11] == 0x45;
    }

    private static bool IsOgg(IReadOnlyList<byte> bytes)
    {
        return bytes.Count >= 4
               && bytes[0] == 0x4F
               && bytes[1] == 0x67
               && bytes[2] == 0x67
               && bytes[3] == 0x53;
    }
}
