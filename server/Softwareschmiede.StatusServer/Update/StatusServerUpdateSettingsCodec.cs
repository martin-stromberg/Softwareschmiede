using System.Globalization;

namespace Softwareschmiede.StatusServer.Update;

/// <summary>Zentrales Mapping zwischen den String-Werten der Update-Einstellungen
/// und den Enums bzw. <see cref="TimeOnly"/>: Formular-Posts
/// (<c>AdminEndpoints</c>), die <c>ServerSettings</c>-Persistenz
/// (<c>ServerSettingsService</c>) und die gerenderten Optionswerte
/// (<c>AdminPagesComposer</c>) beziehen die Literale ausschließlich von hier —
/// ein neues Enum-Mitglied muss nur an einer Stelle gepflegt werden.
/// Alle Zeitformate arbeiten kulturinvariant, damit der Browser-Wert eines
/// <c>input[type=time]</c> (<c>HH:mm</c>) unter jeder Kultur parst und rendert
/// (unter Kulturen mit Zeitseparator ≠ <c>:</c>, z. B. fi-FI, würde
/// <c>ToString("HH:mm")</c> sonst einen Punkt liefern).</summary>
public static class StatusServerUpdateSettingsCodec
{
    /// <summary>Bildet den übertragenen Kanal-String (<c>stable</c>/<c>prerelease</c>)
    /// auf den Enum ab.</summary>
    /// <param name="value">Der zu interpretierende String-Wert.</param>
    /// <returns>Der Kanal oder <see langword="null"/> bei unbekanntem Wert.</returns>
    public static StatusServerUpdateChannel? TryParseChannel(string? value)
        => value switch
        {
            "stable" => StatusServerUpdateChannel.Stable,
            "prerelease" => StatusServerUpdateChannel.ReleaseCandidate,
            _ => null,
        };

    /// <summary>Liefert den String-Wert (<c>stable</c>/<c>prerelease</c>) des Kanals —
    /// identisch für Persistenz und Formular-Optionen.</summary>
    /// <param name="channel">Der zu formatierende Kanal.</param>
    /// <returns>Der String-Wert.</returns>
    public static string FormatChannel(StatusServerUpdateChannel channel)
        => channel switch
        {
            StatusServerUpdateChannel.Stable => "stable",
            StatusServerUpdateChannel.ReleaseCandidate => "prerelease",
            _ => throw new ArgumentOutOfRangeException(nameof(channel), channel, "Unbekannter Update-Kanal."),
        };

    /// <summary>Bildet den übertragenen Modus-String
    /// (<c>off</c>/<c>check</c>/<c>install</c>) auf den Enum ab.</summary>
    /// <param name="value">Der zu interpretierende String-Wert.</param>
    /// <returns>Der Modus oder <see langword="null"/> bei unbekanntem Wert.</returns>
    public static StatusServerAutomationMode? TryParseMode(string? value)
        => value switch
        {
            "off" => StatusServerAutomationMode.Off,
            "check" => StatusServerAutomationMode.CheckOnly,
            "install" => StatusServerAutomationMode.CheckAndInstall,
            _ => null,
        };

    /// <summary>Liefert den String-Wert (<c>off</c>/<c>check</c>/<c>install</c>) des
    /// Modus — identisch für Persistenz und Formular-Optionen.</summary>
    /// <param name="mode">Der zu formatierende Modus.</param>
    /// <returns>Der String-Wert.</returns>
    public static string FormatMode(StatusServerAutomationMode mode)
        => mode switch
        {
            StatusServerAutomationMode.Off => "off",
            StatusServerAutomationMode.CheckOnly => "check",
            StatusServerAutomationMode.CheckAndInstall => "install",
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unbekannter Update-Automatisierungsmodus."),
        };

    /// <summary>Parst eine Uhrzeit kulturinvariant (<c>HH:mm</c>, das Format eines
    /// Browser-<c>input[type=time]</c>-Werts).</summary>
    /// <param name="value">Der zu interpretierende String-Wert.</param>
    /// <param name="time">Die geparste Uhrzeit, sofern erfolgreich.</param>
    /// <returns><see langword="true"/>, wenn der Wert geparst werden konnte.</returns>
    public static bool TryParseInstallTime(string? value, out TimeOnly time)
        => TimeOnly.TryParse(value, CultureInfo.InvariantCulture, out time);

    /// <summary>Formatiert eine Uhrzeit kulturinvariant als <c>HH:mm</c> —
    /// kompatibel mit <c>input[type=time]</c> und der
    /// <c>ServerSettings</c>-Persistenz.</summary>
    /// <param name="time">Die zu formatierende Uhrzeit.</param>
    /// <returns>Der formatierte Wert.</returns>
    public static string FormatInstallTime(TimeOnly time)
        => time.ToString("HH':'mm", CultureInfo.InvariantCulture);
}
