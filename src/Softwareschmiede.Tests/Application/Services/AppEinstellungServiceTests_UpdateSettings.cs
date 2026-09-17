using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.App.Services.Testing;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Application.Services.Updates;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Infrastructure.Data;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Tests für die Update-Einstellungen in <see cref="AppEinstellungService"/> mit echter SQLite-Datei-Datenbank.</summary>
public sealed class AppEinstellungServiceTests_UpdateSettings : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"sw-updatesettings-{Guid.NewGuid():N}.db");

    /// <summary>Räumt die temporäre SQLite-Datenbankdatei auf.</summary>
    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }

    /// <summary>Ein gespeichertes Wertepaar bleibt über Scope-/Context-Grenzen hinweg unverändert erhalten.</summary>
    [Fact]
    public async Task UpdateSettings_PersistsAcrossScopes()
    {
        await using (var db = CreateContext())
        {
            await db.Database.EnsureCreatedAsync();
            await CreateService(db).SetUpdateSettingsAsync(
                new UpdateSettings(UpdateMode.BeiProgrammstartPruefenUndAusfuehren, true));
        }

        await using (var db = CreateContext())
        {
            var settings = await CreateService(db).GetUpdateSettingsAsync();

            settings.Should().Be(new UpdateSettings(UpdateMode.BeiProgrammstartPruefenUndAusfuehren, true));
        }
    }

    /// <summary>Ein fehlschlagender gemeinsamer Speichervorgang hinterlässt kein gemischtes Wertepaar.</summary>
    [Fact]
    public async Task UpdateSettings_SaveFailureLeavesNoMixedPair()
    {
        await using (var db = CreateContext())
        {
            await db.Database.EnsureCreatedAsync();
            await CreateService(db).SetUpdateSettingsAsync(new UpdateSettings(UpdateMode.Aus, true));
        }

        // Eine zweite Verbindung hält eine offene Schreibtransaktion und damit den SQLite-Write-Lock;
        // der anschließende SaveChangesAsync des Services schlägt mit SQLITE_BUSY fehl.
        await using var lockContext = CreateContext();
        await using var lockTransaction = await lockContext.Database.BeginTransactionAsync();
        lockContext.AppEinstellungen.Add(new AppEinstellung
        {
            Id = Guid.NewGuid(),
            Schluessel = "test.lock",
            Wert = "1",
            AktualisiertAm = DateTimeOffset.UtcNow
        });
        await lockContext.SaveChangesAsync();

        await using (var db = CreateContext(defaultTimeoutSeconds: 1))
        {
            var act = () => CreateService(db).SetUpdateSettingsAsync(new UpdateSettings(UpdateMode.NurPruefen, false));

            await act.Should().ThrowAsync<SqliteException>();
        }

        await using (var db = CreateContext())
        {
            var settings = await CreateService(db).GetUpdateSettingsAsync();

            settings.Should().Be(new UpdateSettings(UpdateMode.Aus, true));
        }
    }

    /// <summary>Ein gezielt fehlschlagender markierter Read liefert eine Exception statt Defaults oder alter Werte; nach Deaktivierung ist das Wertepaar unverändert lesbar.</summary>
    [Fact]
    public async Task GetUpdateSettingsAsync_ReadFailureDoesNotReturnDefaults()
    {
        var interceptor = new UpdateSettingsReadFailureInterceptor();

        await using (var db = CreateContext())
        {
            await db.Database.EnsureCreatedAsync();
            await CreateService(db).SetUpdateSettingsAsync(
                new UpdateSettings(UpdateMode.BeiProgrammstartPruefenUndAusfuehren, true));
        }

        var reachedCount = 0;
        interceptor.SettingsReadReached += (_, _) => reachedCount++;

        await using (var db = CreateContext(interceptor))
        {
            var settings = await CreateService(db).GetUpdateSettingsAsync();

            settings.Should().Be(new UpdateSettings(UpdateMode.BeiProgrammstartPruefenUndAusfuehren, true));
            interceptor.SettingsReadCount.Should().Be(1);
            reachedCount.Should().Be(1);
        }

        interceptor.ActivateFailure();

        await using (var db = CreateContext(interceptor))
        {
            var act = () => CreateService(db).GetUpdateSettingsAsync();

            await act.Should().ThrowAsync<InvalidOperationException>();
            interceptor.SettingsReadCount.Should().Be(2);
        }

        interceptor.DeactivateFailure();

        await using (var db = CreateContext(interceptor))
        {
            var settings = await CreateService(db).GetUpdateSettingsAsync();

            settings.Should().Be(new UpdateSettings(UpdateMode.BeiProgrammstartPruefenUndAusfuehren, true));
        }
    }

    /// <summary>Fehlende oder ungültige gespeicherte Werte liefern die Defaults NurPruefen/false.</summary>
    [Fact]
    public async Task GetUpdateSettingsAsync_ReturnsDefaults_WhenStoredValuesAreMissingOrInvalid()
    {
        await using var db = CreateContext();
        await db.Database.EnsureCreatedAsync();
        var service = CreateService(db);

        (await service.GetUpdateSettingsAsync())
            .Should().Be(new UpdateSettings(UpdateMode.NurPruefen, false));

        await service.SetSettingAsync(AppEinstellungService.UpdateModeKey, "kein-modus");
        await service.SetSettingAsync(AppEinstellungService.IncludePrereleasesKey, "vielleicht");

        (await service.GetUpdateSettingsAsync())
            .Should().Be(new UpdateSettings(UpdateMode.NurPruefen, false));

        await service.SetSettingAsync(AppEinstellungService.UpdateModeKey, "7");
        await service.SetSettingAsync(AppEinstellungService.IncludePrereleasesKey, "True");

        (await service.GetUpdateSettingsAsync())
            .Should().Be(new UpdateSettings(UpdateMode.NurPruefen, true));
    }

    /// <summary>SetUpdateSettingsAsync lehnt nicht definierte UpdateMode-Werte ab, ohne etwas zu schreiben.</summary>
    [Fact]
    public async Task SetUpdateSettingsAsync_RejectsInvalidUpdateMode()
    {
        await using var db = CreateContext();
        await db.Database.EnsureCreatedAsync();
        var service = CreateService(db);

        var act = () => service.SetUpdateSettingsAsync(new UpdateSettings((UpdateMode)7, true));

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
        (await service.GetUpdateSettingsAsync())
            .Should().Be(new UpdateSettings(UpdateMode.NurPruefen, false));
    }

    private SoftwareschmiededDbContext CreateContext(
        UpdateSettingsReadFailureInterceptor? interceptor = null,
        int? defaultTimeoutSeconds = null)
    {
        var connectionString = defaultTimeoutSeconds is int timeout
            ? $"Data Source={_dbPath};Default Timeout={timeout}"
            : $"Data Source={_dbPath}";

        return TestDbContextFactory.CreateSqlite(
            connectionString,
            interceptor is not null ? [interceptor] : []);
    }

    private static AppEinstellungService CreateService(SoftwareschmiededDbContext db)
        => new(db, NullLogger<AppEinstellungService>.Instance);
}
