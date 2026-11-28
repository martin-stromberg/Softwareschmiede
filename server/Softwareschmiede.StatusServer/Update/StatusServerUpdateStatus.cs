using msTools.Updater;

namespace Softwareschmiede.StatusServer.Update;

/// <summary>Flaches Status-DTO für die Update-Sektion der Admin-Einstellungsseite —
/// schirmt den Seiten-Composer von den Bibliothekstypen ab.</summary>
/// <param name="InstalledVersion">Die installierte Version aus
/// <c>release-metadata.json</c> (<see langword="null"/> = unbekannt).</param>
/// <param name="AvailableVersion">Die zuletzt gefundene verfügbare Version, sofern vorhanden.</param>
/// <param name="State">Der aktuelle Lebenszyklus-Zustand.</param>
/// <param name="LastCheckedAt">Zeitpunkt der letzten Quellprüfung, sofern erfolgt.</param>
/// <param name="LastError">Die zuletzt gemeldete Fehlermeldung, sofern vorhanden.</param>
/// <param name="IsLocked">Ob aktuell ein Installations-Lock aktiv ist.</param>
/// <param name="IsLockStale">Ob der aktive Lock als verwaist gilt (Reset möglich).</param>
/// <param name="UpdateLogPath">Pfad des Skript-Logs (<c>updates/update.log</c>), sofern vorhanden.</param>
public sealed record StatusServerUpdateStatus(
    string? InstalledVersion,
    string? AvailableVersion,
    AutoUpdateState State,
    DateTimeOffset? LastCheckedAt,
    string? LastError,
    bool IsLocked,
    bool IsLockStale,
    string? UpdateLogPath);
