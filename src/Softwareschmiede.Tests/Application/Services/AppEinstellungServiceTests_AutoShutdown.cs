using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Infrastructure.Data;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>
/// Tests der Auto-Shutdown-Einstellungen in <see cref="AppEinstellungService"/> mit echter
/// SQLite-Datei-Datenbank: Defaults, Bereichsbegrenzung (0–1440 Minuten) und Persistenz
/// über Scope-/Context-Grenzen.
/// </summary>
public sealed class AppEinstellungServiceTests_AutoShutdown : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"sw-autoshutdown-{Guid.NewGuid():N}.db");

    /// <summary>Räumt die temporäre SQLite-Datenbankdatei auf.</summary>
    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }

    /// <summary>Ohne gespeicherte Werte liefern die Getter die Defaults (Erzwingen aus, Toleranz 5 Minuten).</summary>
    [Fact]
    public async Task AutoShutdown_LiefertDefaults_OhneGespeicherteWerte()
    {
        await using var db = CreateContext();
        await db.Database.EnsureCreatedAsync();
        var service = CreateService(db);

        (await service.GetAutoShutdownErzwingenAsync()).Should().BeFalse();
        (await service.GetAutoShutdownToleranzMinutenAsync())
            .Should().Be(AppEinstellungService.AutoShutdownDefaultToleranzMinuten);
    }

    /// <summary>Ein gespeichertes Wertepaar bleibt über Context-Grenzen hinweg erhalten.</summary>
    [Fact]
    public async Task AutoShutdown_PersistsAcrossContexts()
    {
        await using (var db = CreateContext())
        {
            await db.Database.EnsureCreatedAsync();
            var service = CreateService(db);
            await service.SetAutoShutdownErzwingenAsync(true);
            await service.SetAutoShutdownToleranzMinutenAsync(42);
        }

        await using (var db = CreateContext())
        {
            var service = CreateService(db);
            (await service.GetAutoShutdownErzwingenAsync()).Should().BeTrue();
            (await service.GetAutoShutdownToleranzMinutenAsync()).Should().Be(42);
        }
    }

    /// <summary>Gespeicherte Toleranzwerte außerhalb 0–1440 werden beim Lesen auf den gültigen Bereich begrenzt.</summary>
    [Fact]
    public async Task GetAutoShutdownToleranzMinuten_ClampsStoredValues()
    {
        await using var db = CreateContext();
        await db.Database.EnsureCreatedAsync();
        var service = CreateService(db);

        await service.SetAutoShutdownToleranzMinutenAsync(-10);
        (await service.GetAutoShutdownToleranzMinutenAsync()).Should().Be(0);

        await service.SetAutoShutdownToleranzMinutenAsync(AppEinstellungService.AutoShutdownMaxToleranzMinuten + 100);
        (await service.GetAutoShutdownToleranzMinutenAsync())
            .Should().Be(AppEinstellungService.AutoShutdownMaxToleranzMinuten);
    }

    /// <summary>Ein nicht ganzzahlig gespeicherter Toleranzwert fällt auf den Default zurück.</summary>
    [Fact]
    public async Task GetAutoShutdownToleranzMinuten_FaelltBeiUngueltigemWertAufDefaultZurueck()
    {
        await using var db = CreateContext();
        await db.Database.EnsureCreatedAsync();
        db.AppEinstellungen.Add(new AppEinstellung
        {
            Id = Guid.NewGuid(),
            Schluessel = AppEinstellungService.AutoShutdownToleranzMinutenKey,
            Wert = "keine-zahl",
            AktualisiertAm = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
        var service = CreateService(db);

        (await service.GetAutoShutdownToleranzMinutenAsync())
            .Should().Be(AppEinstellungService.AutoShutdownDefaultToleranzMinuten);
    }

    private SoftwareschmiededDbContext CreateContext()
        => TestDbContextFactory.CreateSqlite($"Data Source={_dbPath}");

    private static AppEinstellungService CreateService(SoftwareschmiededDbContext db)
        => new(db, NullLogger<AppEinstellungService>.Instance);
}
