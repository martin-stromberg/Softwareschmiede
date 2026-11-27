using Microsoft.EntityFrameworkCore;
using Softwareschmiede.StatusServer.Data;

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
}
