namespace Softwareschmiede.StatusServer.Data;

/// <summary>Die pro Benutzer persistierte Theme-Einstellung der Status-Webseite
/// (Ordinal-Wert in der <c>AspNetUsers</c>-Spalte <c>Theme</c>).</summary>
public enum ThemePreference
{
    /// <summary>Folgt dem System-Schema des Betrachters
    /// (<c>prefers-color-scheme</c>, Fallback Light).</summary>
    Auto = 0,

    /// <summary>Immer das Light-Schema — auch bei dunkler Systemeinstellung.</summary>
    Light,

    /// <summary>Immer das Dark-Schema — auch bei heller Systemeinstellung.</summary>
    Dark,
}
