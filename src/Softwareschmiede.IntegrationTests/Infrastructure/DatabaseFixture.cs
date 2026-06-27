using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Softwareschmiede.Infrastructure.Data;

namespace Softwareschmiede.IntegrationTests.Infrastructure;

/// <summary>
/// Stellt für jeden Test eine frisch migrierte SQLite-Datenbankdatei bereit.
/// Die Datenbankdatei wird nach dem Test automatisch gelöscht.
/// WAL-Modus ist deaktiviert (PRAGMA journal_mode=DELETE), damit Dateisperren nach Dispose sicher aufgehoben werden.
/// </summary>
public sealed class DatabaseFixture : IAsyncDisposable
{
    private readonly string _dbFilePath;
    private readonly SqliteConnection _connection;
    private bool _disposed;

    /// <summary>Der DbContext, der auf die temporäre SQLite-Datenbankdatei zeigt.</summary>
    public SoftwareschmiededDbContext Context { get; }

    private DatabaseFixture(string dbFilePath, SqliteConnection connection, SoftwareschmiededDbContext context)
    {
        _dbFilePath = dbFilePath;
        _connection = connection;
        Context = context;
    }

    /// <summary>
    /// Erstellt eine neue <see cref="DatabaseFixture"/> mit frisch migrierter Datenbank.
    /// Deaktiviert den WAL-Modus, damit die Datenbankdatei nach dem Test zuverlässig gelöscht werden kann.
    /// </summary>
    public static async Task<DatabaseFixture> CreateAsync(CancellationToken ct = default)
    {
        var dbFilePath = Path.Combine(Path.GetTempPath(), $"softwareschmiede_test_{Guid.NewGuid():N}.db");

        // Gemeinsame Verbindung verwenden, damit journal_mode=DELETE sofort gilt
        var connection = new SqliteConnection($"Data Source={dbFilePath}");
        await connection.OpenAsync(ct);

        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "PRAGMA journal_mode=DELETE;";
            await cmd.ExecuteNonQueryAsync(ct);
        }

        var options = new DbContextOptionsBuilder<SoftwareschmiededDbContext>()
            .UseSqlite(connection)
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;

        var context = new SoftwareschmiededDbContext(options);
        await context.Database.MigrateAsync(ct);

        return new DatabaseFixture(dbFilePath, connection, context);
    }

    /// <summary>
    /// Erstellt einen neuen, unabhängigen DbContext auf derselben Datenbankdatei.
    /// Nützlich, um Persistenz zu prüfen (zweiter Context liest geschriebene Daten).
    /// Der Aufrufer ist für den Dispose des zurückgegebenen Contexts verantwortlich.
    /// </summary>
    public SoftwareschmiededDbContext CreateNewContext()
    {
        var options = new DbContextOptionsBuilder<SoftwareschmiededDbContext>()
            .UseSqlite($"Data Source={_dbFilePath}")
            .AddInterceptors(new SqliteBusyTimeoutInterceptor())
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;

        return new SoftwareschmiededDbContext(options);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        await Context.DisposeAsync();
        await _connection.DisposeAsync();

        // Verbindungspool leeren, damit alle gepoolten Verbindungen geschlossen werden
        // bevor wir die Datei löschen (SQLite-Pooling hält Datei-Handles offen).
        SqliteConnection.ClearAllPools();

        // Datenbankdatei und eventuelle Hilfsdateien entfernen
        foreach (var suffix in new[] { string.Empty, "-shm", "-wal" })
        {
            var path = _dbFilePath + suffix;
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
