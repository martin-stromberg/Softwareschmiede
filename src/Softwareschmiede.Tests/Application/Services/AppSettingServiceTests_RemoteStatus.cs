using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Infrastructure.Data;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>
/// Tests der Remote-Status--Einstellungen in <see cref="AppSettingService"/> (Issue 478)
/// mit echter SQLite-Datei-Datenbank: Defaults, Intervall-Begrenzung, Persistenz über
/// Context-Grenzen und die vergebene Instanz-ID.
/// </summary>
public sealed class AppSettingServiceTests_RemoteStatus : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"sw-remotestatus-{Guid.NewGuid():N}.db");

    /// <summary>Räumt die temporäre SQLite-Datenbankdatei auf.</summary>
    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }

    /// <summary>Ohne gespeicherte Werte liefern die Getter die Defaults (deaktiviert, leere Felder, 60 s).</summary>
    [Fact]
    public async Task RemoteStatus_ReturnsDefaults_WithoutSavedValues()
    {
        await using var db = CreateContext();
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);

        (await service.GetRemoteStatusEnabledAsync(TestContext.Current.CancellationToken)).Should().BeFalse();
        (await service.GetRemoteStatusUrlAsync(TestContext.Current.CancellationToken)).Should().BeNull();
        (await service.GetRemoteStatusInstanceNameAsync(TestContext.Current.CancellationToken)).Should().BeNull();
        (await service.GetRemoteStatusSnapshotIntervalSecondsAsync(TestContext.Current.CancellationToken))
            .Should().Be(AppSettingService.RemoteStatusDefaultSnapshotIntervalSeconds);
    }

    /// <summary>Alle gespeicherten Remote-Status-Werte bleiben über Context-Grenzen hinweg erhalten.</summary>
    [Fact]
    public async Task RemoteStatus_PersistsAcrossContexts()
    {
        await using (var db = CreateContext())
        {
            await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
            var service = CreateService(db);
            await service.SetRemoteStatusEnabledAsync(true, TestContext.Current.CancellationToken);
            await service.SetRemoteStatusUrlAsync("https://status.example.org", TestContext.Current.CancellationToken);
            await service.SetRemoteStatusInstanceNameAsync("Martin-PC", TestContext.Current.CancellationToken);
            await service.SetRemoteStatusSnapshotIntervalSecondsAsync(120, TestContext.Current.CancellationToken);
        }

        await using (var db = CreateContext())
        {
            var service = CreateService(db);
            (await service.GetRemoteStatusEnabledAsync(TestContext.Current.CancellationToken)).Should().BeTrue();
            (await service.GetRemoteStatusUrlAsync(TestContext.Current.CancellationToken))
                .Should().Be("https://status.example.org");
            (await service.GetRemoteStatusInstanceNameAsync(TestContext.Current.CancellationToken))
                .Should().Be("Martin-PC");
            (await service.GetRemoteStatusSnapshotIntervalSecondsAsync(TestContext.Current.CancellationToken))
                .Should().Be(120);
        }
    }

    /// <summary>Gespeicherte Intervall-Werte außerhalb des zulässigen Bereichs werden beim Lesen begrenzt.</summary>
    [Theory]
    [InlineData(1, AppSettingService.RemoteStatusMinSnapshotIntervalSeconds)]
    [InlineData(7200, AppSettingService.RemoteStatusMaxSnapshotIntervalSeconds)]
    public async Task RemoteStatusSnapshotInterval_ClampsOutOfRangeValues(int stored, int expected)
    {
        await using var db = CreateContext();
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);
        await service.SetRemoteStatusSnapshotIntervalSecondsAsync(stored, TestContext.Current.CancellationToken);

        (await service.GetRemoteStatusSnapshotIntervalSecondsAsync(TestContext.Current.CancellationToken))
            .Should().Be(expected);
    }

    /// <summary>Ein nicht-numerischer gespeicherter Intervall-Wert fällt auf den Default zurück.</summary>
    [Fact]
    public async Task RemoteStatusSnapshotInterval_InvalidStoredValue_ReturnsDefault()
    {
        await using var db = CreateContext();
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        db.AppSettings.Add(new AppSetting
        {
            Id = Guid.NewGuid(),
            Key = AppSettingService.RemoteStatusSnapshotIntervalSecondsKey,
            Value = "keine-zahl",
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);

        (await service.GetRemoteStatusSnapshotIntervalSecondsAsync(TestContext.Current.CancellationToken))
            .Should().Be(AppSettingService.RemoteStatusDefaultSnapshotIntervalSeconds);
    }

    /// <summary>GetOrCreate vergibt einmalig eine GUID-förmige Instanz-ID, persistiert sie und liefert sie stabil.</summary>
    [Fact]
    public async Task GetOrCreateRemoteStatusInstanceId_CreatesPersistsAndStaysStable()
    {
        string first;
        await using (var db = CreateContext())
        {
            await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
            var service = CreateService(db);
            first = await service.GetOrCreateRemoteStatusInstanceIdAsync(TestContext.Current.CancellationToken);

            first.Should().NotBeNullOrWhiteSpace();
            Guid.TryParseExact(first, "N", out _).Should().BeTrue();
            (await service.GetOrCreateRemoteStatusInstanceIdAsync(TestContext.Current.CancellationToken))
                .Should().Be(first);
        }

        await using (var db = CreateContext())
        {
            var service = CreateService(db);
            (await service.GetOrCreateRemoteStatusInstanceIdAsync(TestContext.Current.CancellationToken))
                .Should().Be(first);
        }
    }

    private SoftwareschmiedeDbContext CreateContext()
        => TestDbContextFactory.CreateSqlite($"Data Source={_dbPath}");

    private static AppSettingService CreateService(SoftwareschmiedeDbContext db)
        => new(db, NullLogger<AppSettingService>.Instance);
}
