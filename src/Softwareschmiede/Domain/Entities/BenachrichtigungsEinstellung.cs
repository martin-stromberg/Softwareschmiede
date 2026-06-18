using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.Domain.Entities;

/// <summary>Benutzerbezogene Einstellungen für KI-Aufgaben-Benachrichtigungen.</summary>
public sealed class BenachrichtigungsEinstellung
{
    /// <summary>Primärschlüssel.</summary>
    public Guid Id { get; set; }

    /// <summary>Benutzerkennung des Eigentümers dieser Einstellung.</summary>
    public string BenutzerId { get; set; } = string.Empty;

    /// <summary>Modus für visuelle Banner-Benachrichtigungen.</summary>
    public BenachrichtigungsModus BannerModus { get; set; } = BenachrichtigungsModus.Banner;

    /// <summary>Modus für Ton-Benachrichtigungen.</summary>
    public BenachrichtigungsModus TonModus { get; set; } = BenachrichtigungsModus.Deaktiviert;

    /// <summary>Zeitstempel der letzten Aktualisierung.</summary>
    public DateTimeOffset AktualisiertAm { get; set; }
}
