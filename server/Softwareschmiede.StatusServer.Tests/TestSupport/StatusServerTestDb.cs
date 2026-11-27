using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Softwareschmiede.StatusServer.Data;

namespace Softwareschmiede.StatusServer.Tests.TestSupport;

/// <summary>In-Memory-SQLite-<see cref="StatusServerDbContext"/> für Unit-Tests
/// (offene Connection hält die Datenbank am Leben).</summary>
public sealed class StatusServerTestDb : IDisposable
{
    private readonly SqliteConnection _connection;

    /// <summary>Erstellt die In-Memory-Datenbank inkl. Schema.</summary>
    public StatusServerTestDb()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<StatusServerDbContext>()
            .UseSqlite(_connection)
            .Options;
        Context = new StatusServerDbContext(options);
        Context.Database.EnsureCreated();
    }

    /// <summary>Der Datenbankkontext.</summary>
    public StatusServerDbContext Context { get; }

    /// <inheritdoc/>
    public void Dispose()
    {
        Context.Dispose();
        _connection.Dispose();
    }
}
