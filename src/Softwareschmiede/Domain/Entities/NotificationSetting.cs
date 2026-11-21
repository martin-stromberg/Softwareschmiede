using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.Domain.Entities;

/// <summary>Benutzerbezogene Einstellungen für KI-Aufgaben-Benachrichtigungen.</summary>
public sealed class NotificationSetting
{
    /// <summary>Primärschlüssel.</summary>
    public Guid Id { get; set; }

    /// <summary>Benutzerkennung des Eigentümers dieser Einstellung.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>Modus für visuelle Banner-Benachrichtigungen.</summary>
    public NotificationMode BannerMode { get; set; } = NotificationMode.Banner;

    /// <summary>Modus für Ton-Benachrichtigungen.</summary>
    public NotificationMode SoundMode { get; set; } = NotificationMode.Disabled;

    /// <summary>Zeitstempel der letzten Aktualisierung.</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
