using FluentAssertions;
using Softwareschmiede.StatusServer.Data;
using Softwareschmiede.StatusServer.Security;
using Softwareschmiede.StatusServer.Tests.TestSupport;
using Softwareschmiede.StatusServer.Update;

namespace Softwareschmiede.StatusServer.Tests.Security;

/// <summary>Tests für <see cref="ServerSettingsService"/> (Sitzungsdauer-Einstellung).</summary>
public sealed class ServerSettingsServiceTests
{
    /// <summary>Ohne gespeicherten Wert gilt der Default von 480 Minuten.</summary>
    [Fact]
    public async Task GetSessionDuration_DefaultsTo480()
    {
        using var db = new StatusServerTestDb();
        var sut = new ServerSettingsService(db.Context);
        (await sut.GetSessionDurationAsync(TestContext.Current.CancellationToken)).Should().Be(480);
    }

    /// <summary>Ein gesetzter Wert wird persistent gespeichert und zurückgegeben.</summary>
    [Fact]
    public async Task SetSessionDuration_Persists()
    {
        var ct = TestContext.Current.CancellationToken;
        using var db = new StatusServerTestDb();
        var sut = new ServerSettingsService(db.Context);

        await sut.SetSessionDurationAsync(120, ct);
        (await sut.GetSessionDurationAsync(ct)).Should().Be(120);

        // Neuer Service auf derselben DB sieht den persistierten Wert.
        var sut2 = new ServerSettingsService(db.Context);
        (await sut2.GetSessionDurationAsync(ct)).Should().Be(120);
    }

    /// <summary>Zwei parallele Erst-Schreibvorgänge (der Datensatz existiert noch
    /// nicht) werden vom prozessweiten Lock serialisiert: Beide Aufrufe gelingen
    /// — keine <c>DbUpdateException</c> durch doppelten Insert, einer der Werte
    /// bleibt als Last-Writer-Wins persistiert.</summary>
    [Fact]
    public async Task SetSessionDuration_ParallelFirstWrites_BothSucceed()
    {
        await using var factory = new StatusWebApplicationFactory();
        var seeder = new StatusServerDbSeeder(factory);

        await Task.WhenAll(
            seeder.SetSessionDurationAsync(60),
            seeder.SetSessionDurationAsync(120));

        var persisted = await seeder.GetSessionDurationAsync();
        (persisted is 60 or 120).Should().BeTrue();
    }

    /// <summary>Werte außerhalb des erlaubten Bereichs (5–43200 Minuten) werden abgelehnt.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(43201)]
    [InlineData(-10)]
    public async Task SetSessionDuration_OutOfRange_Throws(int minutes)
    {
        using var db = new StatusServerTestDb();
        var sut = new ServerSettingsService(db.Context);
        var act = () => sut.SetSessionDurationAsync(minutes, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    /// <summary>Ohne persistierte Werte gelten die Update-Defaults:
    /// Kanal Stable, Modus Off, Installationszeit 03:00.</summary>
    [Fact]
    public async Task GetUpdateSettings_WithoutPersistedValues_ReturnsDefaults()
    {
        using var db = new StatusServerTestDb();
        var sut = new ServerSettingsService(db.Context);

        var settings = await sut.GetUpdateSettingsAsync(TestContext.Current.CancellationToken);

        settings.Channel.Should().Be(StatusServerUpdateChannel.Stable);
        settings.Mode.Should().Be(StatusServerAutomationMode.Off);
        settings.ScheduledInstallTime.Should().Be(new TimeOnly(3, 0));
    }

    /// <summary>Korrupte persistierte Werte fallen pro Schlüssel auf die Defaults
    /// zurück statt zu fehlschlagen.</summary>
    [Fact]
    public async Task GetUpdateSettings_CorruptValues_FallBackToDefaults()
    {
        using var db = new StatusServerTestDb();
        db.Context.ServerSettings.AddRange(
            new ServerSetting { Key = ServerSettingsService.UpdateChannelKey, Value = "bogus" },
            new ServerSetting { Key = ServerSettingsService.UpdateAutomationModeKey, Value = "xyz" },
            new ServerSetting { Key = ServerSettingsService.UpdateInstallTimeKey, Value = "keine-uhrzeit" });
        await db.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var sut = new ServerSettingsService(db.Context);

        var settings = await sut.GetUpdateSettingsAsync(TestContext.Current.CancellationToken);

        settings.Channel.Should().Be(StatusServerUpdateChannel.Stable);
        settings.Mode.Should().Be(StatusServerAutomationMode.Off);
        settings.ScheduledInstallTime.Should().Be(new TimeOnly(3, 0));
    }

    /// <summary>Ein gesetzter Einstellungssatz wird unter allen drei Schlüsseln
    /// persistiert und vollständig zurückgegeben; ein erneutes Setzen aktualisiert
    /// die vorhandenen Datensätze statt Duplikate anzulegen.</summary>
    [Fact]
    public async Task SetUpdateSettings_PersistsAllKeys_AndRoundtrips()
    {
        var ct = TestContext.Current.CancellationToken;
        using var db = new StatusServerTestDb();
        var sut = new ServerSettingsService(db.Context);
        var settings = new StatusServerUpdateSettings(
            StatusServerUpdateChannel.ReleaseCandidate,
            StatusServerAutomationMode.CheckAndInstall,
            new TimeOnly(4, 30));

        await sut.SetUpdateSettingsAsync(settings, ct);

        var keys = db.Context.ServerSettings.Select(s => s.Key).ToList();
        keys.Should().BeEquivalentTo(
            ServerSettingsService.UpdateChannelKey,
            ServerSettingsService.UpdateAutomationModeKey,
            ServerSettingsService.UpdateInstallTimeKey);

        var roundtrip = await sut.GetUpdateSettingsAsync(ct);
        roundtrip.Should().Be(settings);

        // Zweites Setzen aktualisiert die bestehenden Datensätze (Upsert).
        var updated = settings with { Mode = StatusServerAutomationMode.Off };
        await sut.SetUpdateSettingsAsync(updated, ct);
        db.Context.ServerSettings.Count().Should().Be(3);
        (await sut.GetUpdateSettingsAsync(ct)).Mode.Should().Be(StatusServerAutomationMode.Off);
    }
}
