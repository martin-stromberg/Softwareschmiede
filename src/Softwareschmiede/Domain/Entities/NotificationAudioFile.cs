namespace Softwareschmiede.Domain.Entities;

/// <summary>Benutzerdefinierte Audio-Datei für Benachrichtigungstöne.</summary>
public sealed class NotificationAudioFile
{
    /// <summary>Primärschlüssel.</summary>
    public Guid Id { get; set; }

    /// <summary>Benutzerkennung des Eigentümers.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>Ursprünglicher Dateiname beim Upload.</summary>
    public string OriginalFileName { get; set; } = string.Empty;

    /// <summary>MIME-Typ der Audio-Datei.</summary>
    public string MimeType { get; set; } = string.Empty;

    /// <summary>Dateigröße in Bytes.</summary>
    public int SizeBytes { get; set; }

    /// <summary>Binärer Dateiinhalt.</summary>
    public byte[] Content { get; set; } = [];

    /// <summary>Zeitstempel des Uploads.</summary>
    public DateTimeOffset UploadedAt { get; set; }
}
