namespace Softwareschmiede.StatusServer.Update;

/// <summary>Der Automatisierungsmodus der Update-Funktion (persistiert als
/// <c>off</c>/<c>check</c>/<c>install</c> in der <c>ServerSettings</c>-Tabelle).</summary>
public enum StatusServerAutomationMode
{
    /// <summary>Automatisierung aus — nur manuelle Prüfung/Installation aus der Admin-Oberfläche.</summary>
    Off,

    /// <summary>Periodisch im Prüfzeitfenster nach Updates suchen und sie melden, aber nichts laden oder installieren.</summary>
    CheckOnly,

    /// <summary>Prüfen, gefundene Updates automatisch herunterladen und zur konfigurierten Uhrzeit installieren.</summary>
    CheckAndInstall,
}
