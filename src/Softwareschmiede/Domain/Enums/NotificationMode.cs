namespace Softwareschmiede.Domain.Enums;

/// <summary>Modus für Benachrichtigungen.</summary>
public enum NotificationMode
{
    /// <summary>Benachrichtigungen deaktiviert.</summary>
    Disabled = 0,
    /// <summary>Benachrichtigung als visuelles Banner anzeigen.</summary>
    Banner = 1,
    /// <summary>Benachrichtigung per Ton ausgeben.</summary>
    Sound = 2
}
