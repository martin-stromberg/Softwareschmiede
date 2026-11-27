namespace Softwareschmiede.StatusServer.Data;

/// <summary>Schlüssel-Wert-Eintrag für admin-konfigurierbare Server-Einstellungen
/// (z. B. die Sitzungsdauer der Anmeldesession).</summary>
public sealed class ServerSetting
{
    /// <summary>Schlüssel der Einstellung (Primärschlüssel).</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Wert der Einstellung.</summary>
    public string Value { get; set; } = string.Empty;
}
