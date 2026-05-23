namespace Softwareschmiede.Domain.Entities;

/// <summary>Benutzerdefinierte Audio-Datei für Benachrichtigungstöne.</summary>
public sealed class BenachrichtigungsAudioDatei
{
    public Guid Id { get; set; }

    public string BenutzerId { get; set; } = string.Empty;

    public string OriginalDateiname { get; set; } = string.Empty;

    public string MimeType { get; set; } = string.Empty;

    public int GroesseBytes { get; set; }

    public byte[] Inhalt { get; set; } = [];

    public DateTimeOffset HochgeladenAm { get; set; }
}
