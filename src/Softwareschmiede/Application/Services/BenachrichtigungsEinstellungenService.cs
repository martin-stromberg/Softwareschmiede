using Microsoft.EntityFrameworkCore;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Infrastructure.Data;

namespace Softwareschmiede.Application.Services;

/// <summary>Verwaltet benutzerbezogene Einstellungen und Audio-Dateien für KI-Benachrichtigungen.</summary>
public sealed class BenachrichtigungsEinstellungenService
{
    /// <summary>Maximale erlaubte Audio-Dateigröße in Bytes (10 MB).</summary>
    public const int MaxAudioDateigroesseBytes = 10 * 1024 * 1024;

    private static readonly HashSet<string> ErlaubteErweiterungen = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3",
        ".wav",
        ".ogg"
    };

    private static readonly HashSet<string> ErlaubteMimeTypes = new(StringComparer.OrdinalIgnoreCase)
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

    /// <inheritdoc cref="BenachrichtigungsEinstellungenService"/>
    public BenachrichtigungsEinstellungenService(SoftwareschmiedeDbContext db)
    {
        _db = db;
    }

    /// <summary>Gibt die gespeicherten Benachrichtigungseinstellungen des Benutzers zurück.</summary>
    public async Task<BenachrichtigungsEinstellungenDto> GetAsync(string benutzerId, CancellationToken ct = default)
    {
        var einstellung = await _db.NotificationSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == benutzerId, ct);

        if (einstellung is null)
        {
            return new BenachrichtigungsEinstellungenDto(
                NotificationMode.Banner,
                NotificationMode.Disabled);
        }

        return new BenachrichtigungsEinstellungenDto(einstellung.BannerMode, einstellung.SoundMode);
    }

    /// <summary>Speichert die Benachrichtigungseinstellungen des Benutzers.</summary>
    public async Task SaveAsync(string benutzerId, BenachrichtigungsEinstellungenDto dto, CancellationToken ct = default)
    {
        var einstellung = await _db.NotificationSettings
            .FirstOrDefaultAsync(s => s.UserId == benutzerId, ct);

        if (einstellung is null)
        {
            einstellung = new NotificationSetting
            {
                Id = Guid.NewGuid(),
                UserId = benutzerId
            };
            _db.NotificationSettings.Add(einstellung);
        }

        einstellung.BannerMode = dto.BannerMode;
        einstellung.SoundMode = dto.SoundMode;
        einstellung.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Gibt Metadaten der hochgeladenen Audio-Datei des Benutzers zurück.</summary>
    public async Task<BenachrichtigungsAudioInfoDto> GetAudioInfoAsync(string benutzerId, CancellationToken ct = default)
    {
        var audio = await _db.NotificationAudioFiles
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == benutzerId, ct);

        if (audio is null)
        {
            return new BenachrichtigungsAudioInfoDto(false, null, null, null);
        }

        return new BenachrichtigungsAudioInfoDto(true, audio.OriginalFileName, audio.MimeType, audio.SizeBytes);
    }

    /// <summary>Gibt den Base64-kodierten Audio-Payload des Benutzers zurück.</summary>
    public async Task<BenachrichtigungsAudioPayload?> GetAudioPayloadAsync(string benutzerId, CancellationToken ct = default)
    {
        var audio = await _db.NotificationAudioFiles
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == benutzerId, ct);

        if (audio is null || audio.Content.Length == 0)
        {
            return null;
        }

        return new BenachrichtigungsAudioPayload(audio.MimeType, Convert.ToBase64String(audio.Content));
    }

    /// <summary>Speichert eine neue Audio-Datei für den Benutzer.</summary>
    public async Task UploadAudioAsync(
        string benutzerId,
        string dateiname,
        string? mimeType,
        byte[] inhalt,
        CancellationToken ct = default)
    {
        ValidateAudioDatei(dateiname, mimeType, inhalt);

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

        existing.OriginalFileName = SanitizeDateiname(dateiname);
        existing.MimeType = NormalizeMimeType(mimeType, dateiname);
        existing.SizeBytes = inhalt.Length;
        existing.Content = inhalt;
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

    private static void ValidateAudioDatei(string dateiname, string? mimeType, byte[] inhalt)
    {
        if (inhalt.Length == 0)
        {
            throw new ArgumentException("Die Datei ist leer.");
        }

        if (inhalt.Length > MaxAudioDateigroesseBytes)
        {
            throw new ArgumentException("Die Datei überschreitet das Limit von 10 MB.");
        }

        var extension = Path.GetExtension(dateiname);
        if (string.IsNullOrWhiteSpace(extension) || !ErlaubteErweiterungen.Contains(extension))
        {
            throw new ArgumentException("Ungültiges Audioformat. Erlaubt sind mp3, wav und ogg.");
        }

        if (!string.IsNullOrWhiteSpace(mimeType) && !ErlaubteMimeTypes.Contains(mimeType.Trim()))
        {
            throw new ArgumentException("Ungültiger MIME-Typ für Audio-Datei.");
        }

        if (!HatGueltigeDateisignatur(extension, inhalt))
        {
            throw new ArgumentException("Die Datei-Signatur passt nicht zum gewählten Audioformat.");
        }
    }

    private static string SanitizeDateiname(string dateiname)
    {
        var name = Path.GetFileName(dateiname).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return $"audio-{Guid.NewGuid():N}.bin";
        }

        return name.Length > 240 ? name[..240] : name;
    }

    private static string NormalizeMimeType(string? mimeType, string dateiname)
    {
        if (!string.IsNullOrWhiteSpace(mimeType))
        {
            return mimeType.Trim();
        }

        return Path.GetExtension(dateiname).ToLowerInvariant() switch
        {
            ".mp3" => "audio/mpeg",
            ".wav" => "audio/wav",
            ".ogg" => "audio/ogg",
            _ => "application/octet-stream"
        };
    }

    private static bool HatGueltigeDateisignatur(string extension, byte[] inhalt)
    {
        return extension.ToLowerInvariant() switch
        {
            ".mp3" => IsMp3(inhalt),
            ".wav" => IsWav(inhalt),
            ".ogg" => IsOgg(inhalt),
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
