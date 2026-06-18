namespace Softwareschmiede.Domain.Entities;

/// <summary>Benutzerdefinierte Audio-Datei für Benachrichtigungstöne.</summary>
public sealed class BenachrichtigungsAudioDatei
{
    /// <summary>Primärschlüssel.</summary>
    public Guid Id { get; set; }

    /// <summary>Benutzerkennung des Eigentümers.</summary>
    public string BenutzerId { get; set; } = string.Empty;

    /// <summary>Ursprünglicher Dateiname beim Upload.</summary>
    public string OriginalDateiname { get; set; } = string.Empty;

    /// <summary>MIME-Typ der Audio-Datei.</summary>
    public string MimeType { get; set; } = string.Empty;

    /// <summary>Dateigröße in Bytes.</summary>
    public int GroesseBytes { get; set; }

    /// <summary>Binärer Dateiinhalt.</summary>
    public byte[] Inhalt { get; set; } = [];

    /// <summary>Zeitstempel des Uploads.</summary>
    public DateTimeOffset HochgeladenAm { get; set; }
}
