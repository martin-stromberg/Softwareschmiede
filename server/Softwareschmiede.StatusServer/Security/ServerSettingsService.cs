using Microsoft.EntityFrameworkCore;
using Softwareschmiede.StatusServer.Data;
using Softwareschmiede.StatusServer.Update;

namespace Softwareschmiede.StatusServer.Security;

/// <summary>Zugriff auf admin-konfigurierbare Server-Einstellungen
/// (persistiert in der <c>ServerSettings</c>-Tabelle).</summary>
public sealed class ServerSettingsService
{
    /// <summary>Schlüssel der Sitzungsdauer-Einstellung.</summary>
    public const string SessionDurationKey = "SessionDurationMinutes";

    /// <summary>Standard-Sitzungsdauer in Minuten, solange kein Wert persistiert ist.</summary>
    public const int DefaultSessionDurationMinutes = 480;

    /// <summary>Kleinste zulässige Sitzungsdauer in Minuten.</summary>
    public const int MinSessionDurationMinutes = 5;

    /// <summary>Größte zulässige Sitzungsdauer in Minuten (30 Tage).</summary>
    public const int MaxSessionDurationMinutes = 43200;

    /// <summary>Schlüssel des persistierten Update-Kanals.</summary>
    public const string UpdateChannelKey = "UpdateChannel";

    /// <summary>Schlüssel des persistierten Update-Automatisierungsmodus.</summary>
    public const string UpdateAutomationModeKey = "UpdateAutomationMode";

    /// <summary>Schlüssel der persistierten täglichen Installationsuhrzeit.</summary>
    public const string UpdateInstallTimeKey = "UpdateInstallTime";

    /// <summary>Standard-Update-Kanal, solange kein Wert persistiert ist.</summary>
    public const StatusServerUpdateChannel DefaultUpdateChannel = StatusServerUpdateChannel.Stable;

    /// <summary>Standard-Automatisierungsmodus, solange kein Wert persistiert ist.</summary>
    public const StatusServerAutomationMode DefaultUpdateMode = StatusServerAutomationMode.Off;

    /// <summary>Standard-Installationsuhrzeit (03:00), solange kein Wert persistiert ist.</summary>
    public static TimeOnly DefaultUpdateInstallTime { get; } = new(3, 0);

    /// <summary>Prozessweiter Schalter, der den Check-then-act-Upsert in
    /// <see cref="SetSessionDurationAsync"/> serialisiert: Zwei parallele
    /// Erst-Schreibvorgänge sähen sonst beide „kein Datensatz" und der zweite
    /// Insert liefe auf eine PK-Verletzung (<see cref="DbUpdateException"/> als
    /// HTTP 500). Der Status-Server läuft single-instanzig — ein In-Process-Lock
    /// genügt (gleiches Muster wie der Letzter-Admin-Lock im
    /// <see cref="UserAdminService"/>).</summary>
    private static readonly SemaphoreSlim s_writeLock = new(1, 1);

    private readonly StatusServerDbContext _db;

    /// <summary>Erstellt den Service.</summary>
    /// <param name="db">Der Datenbankkontext.</param>
    public ServerSettingsService(StatusServerDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    /// <summary>Liefert die konfigurierte Sitzungsdauer in Minuten
    /// (<see cref="DefaultSessionDurationMinutes"/>, solange kein Wert persistiert ist).</summary>
    /// <param name="cancellationToken">Abbruchtoken.</param>
    /// <returns>Die Sitzungsdauer in Minuten.</returns>
    public async Task<int> GetSessionDurationAsync(CancellationToken cancellationToken = default)
    {
        var value = await _db.ServerSettings
            .Where(s => s.Key == SessionDurationKey)
            .Select(s => s.Value)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return int.TryParse(value, out var minutes)
            ? minutes
            : DefaultSessionDurationMinutes;
    }

    /// <summary>Persistiert die Sitzungsdauer in Minuten.</summary>
    /// <param name="minutes">Die neue Sitzungsdauer
    /// (<see cref="MinSessionDurationMinutes"/>–<see cref="MaxSessionDurationMinutes"/>).</param>
    /// <param name="cancellationToken">Abbruchtoken.</param>
    /// <returns>Eine abgeschlossene Aufgabe.</returns>
    public async Task SetSessionDurationAsync(int minutes, CancellationToken cancellationToken = default)
    {
        if (minutes is < MinSessionDurationMinutes or > MaxSessionDurationMinutes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minutes), minutes,
                $"Sitzungsdauer muss zwischen {MinSessionDurationMinutes} und {MaxSessionDurationMinutes} Minuten liegen.");
        }

        // Lesen-ändern-schreiben unter dem prozessweiten Lock serialisieren,
        // damit parallele Erst-Schreibvorgänge nicht beide einfügen.
        await s_writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var setting = await _db.ServerSettings
                .FirstOrDefaultAsync(s => s.Key == SessionDurationKey, cancellationToken)
                .ConfigureAwait(false);
            if (setting is null)
                _db.ServerSettings.Add(new ServerSetting { Key = SessionDurationKey, Value = minutes.ToString() });
            else
                setting.Value = minutes.ToString();
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            s_writeLock.Release();
        }
    }

    /// <summary>Liefert die persistierten Update-Einstellungen. Fehlende oder korrupte
    /// Werte fallen auf die jeweiligen Defaults zurück (<see cref="DefaultUpdateChannel"/>,
    /// <see cref="DefaultUpdateMode"/>, <see cref="DefaultUpdateInstallTime"/>).</summary>
    /// <param name="cancellationToken">Abbruchtoken.</param>
    /// <returns>Die Update-Einstellungen.</returns>
    public async Task<StatusServerUpdateSettings> GetUpdateSettingsAsync(CancellationToken cancellationToken = default)
    {
        var values = await _db.ServerSettings
            .Where(s => s.Key == UpdateChannelKey
                || s.Key == UpdateAutomationModeKey
                || s.Key == UpdateInstallTimeKey)
            .ToDictionaryAsync(s => s.Key, s => s.Value, cancellationToken)
            .ConfigureAwait(false);

        var channel = values.TryGetValue(UpdateChannelKey, out var channelValue)
            ? StatusServerUpdateSettingsCodec.TryParseChannel(channelValue) ?? DefaultUpdateChannel
            : DefaultUpdateChannel;
        var mode = values.TryGetValue(UpdateAutomationModeKey, out var modeValue)
            ? StatusServerUpdateSettingsCodec.TryParseMode(modeValue) ?? DefaultUpdateMode
            : DefaultUpdateMode;
        var installTime = values.TryGetValue(UpdateInstallTimeKey, out var timeValue)
            && StatusServerUpdateSettingsCodec.TryParseInstallTime(timeValue, out var parsed)
            ? parsed
            : DefaultUpdateInstallTime;

        return new StatusServerUpdateSettings(channel, mode, installTime);
    }

    /// <summary>Persistiert die Update-Einstellungen als Upsert aller drei Schlüssel.</summary>
    /// <param name="settings">Die zu speichernden Update-Einstellungen.</param>
    /// <param name="cancellationToken">Abbruchtoken.</param>
    /// <returns>Eine abgeschlossene Aufgabe.</returns>
    public async Task SetUpdateSettingsAsync(StatusServerUpdateSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        // Lesen-ändern-schreiben unter dem prozessweiten Lock serialisieren,
        // damit parallele Erst-Schreibvorgänge nicht beide einfügen.
        await s_writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var keys = new[] { UpdateChannelKey, UpdateAutomationModeKey, UpdateInstallTimeKey };
            var existing = await _db.ServerSettings
                .Where(s => keys.Contains(s.Key))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            Upsert(existing, UpdateChannelKey, StatusServerUpdateSettingsCodec.FormatChannel(settings.Channel));
            Upsert(existing, UpdateAutomationModeKey, StatusServerUpdateSettingsCodec.FormatMode(settings.Mode));
            Upsert(existing, UpdateInstallTimeKey, StatusServerUpdateSettingsCodec
                .FormatInstallTime(settings.ScheduledInstallTime ?? DefaultUpdateInstallTime));

            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            s_writeLock.Release();
        }
    }

    private void Upsert(List<ServerSetting> existing, string key, string value)
    {
        var setting = existing.FirstOrDefault(s => s.Key == key);
        if (setting is null)
            _db.ServerSettings.Add(new ServerSetting { Key = key, Value = value });
        else
            setting.Value = value;
    }
}
