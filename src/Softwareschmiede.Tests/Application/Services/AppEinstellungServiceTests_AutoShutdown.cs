using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Infrastructure.Data;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>
/// Tests der Auto-Shutdown-Einstellungen in <see cref="AppSettingService"/> mit echter
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
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);

        (await service.GetAutoShutdownForceAsync(TestContext.Current.CancellationToken)).Should().BeFalse();
        (await service.GetAutoShutdownToleranceMinutesAsync(TestContext.Current.CancellationToken))
            .Should().Be(AppSettingService.AutoShutdownDefaultToleranceMinutes);
    }

    /// <summary>Ein gespeichertes Wertepaar bleibt über Context-Grenzen hinweg erhalten.</summary>
    [Fact]
    public async Task AutoShutdown_PersistsAcrossContexts()
    {
        await using (var db = CreateContext())
        {
            await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
            var service = CreateService(db);
            await service.SetAutoShutdownForceAsync(true, TestContext.Current.CancellationToken);
            await service.SetAutoShutdownToleranceMinutesAsync(42, TestContext.Current.CancellationToken);
        }

        await using (var db = CreateContext())
        {
            var service = CreateService(db);
            (await service.GetAutoShutdownForceAsync(TestContext.Current.CancellationToken)).Should().BeTrue();
            (await service.GetAutoShutdownToleranceMinutesAsync(TestContext.Current.CancellationToken)).Should().Be(42);
        }
    }

    /// <summary>Gespeicherte Toleranzwerte außerhalb 0–1440 werden beim Lesen auf den gültigen Bereich begrenzt.</summary>
    [Fact]
    public async Task GetAutoShutdownToleranzMinuten_ClampsStoredValues()
    {
        await using var db = CreateContext();
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);

        await service.SetAutoShutdownToleranceMinutesAsync(-10, TestContext.Current.CancellationToken);
        (await service.GetAutoShutdownToleranceMinutesAsync(TestContext.Current.CancellationToken)).Should().Be(0);

        await service.SetAutoShutdownToleranceMinutesAsync(AppSettingService.AutoShutdownMaxToleranceMinutes + 100, TestContext.Current.CancellationToken);
        (await service.GetAutoShutdownToleranceMinutesAsync(TestContext.Current.CancellationToken))
            .Should().Be(AppSettingService.AutoShutdownMaxToleranceMinutes);
    }

    /// <summary>Ein nicht ganzzahlig gespeicherter Toleranzwert fällt auf den Default zurück.</summary>
    [Fact]
    public async Task GetAutoShutdownToleranzMinuten_FaelltBeiUngueltigemWertAufDefaultZurueck()
    {
        await using var db = CreateContext();
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        db.AppSettings.Add(new AppSetting
        {
            Id = Guid.NewGuid(),
            Key = AppSettingService.AutoShutdownToleranceMinutesKey,
            Value = "keine-zahl",
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);

        (await service.GetAutoShutdownToleranceMinutesAsync(TestContext.Current.CancellationToken))
            .Should().Be(AppSettingService.AutoShutdownDefaultToleranceMinutes);
    }

    private SoftwareschmiedeDbContext CreateContext()
        => TestDbContextFactory.CreateSqlite($"Data Source={_dbPath}");

    private static AppSettingService CreateService(SoftwareschmiedeDbContext db)
        => new(db, NullLogger<AppSettingService>.Instance);
}
