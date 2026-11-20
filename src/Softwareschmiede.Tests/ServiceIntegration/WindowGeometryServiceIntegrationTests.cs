using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.ServiceIntegration;

/// <summary>E2E-Test: Fensterposition verschieben/skalieren und beim Neustart wiederherstellen.</summary>
public sealed class WindowGeometryServiceIntegrationTests : IDisposable
{
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiedeDbContext _db;
    private readonly AppSettingService _settingService;

    /// <summary>WindowGeometryServiceIntegrationTests.</summary>
    public WindowGeometryServiceIntegrationTests()
    {
        _db = TestDbContextFactory.Create();
        _settingService = new AppSettingService(_db, NullLogger<AppSettingService>.Instance);
    }

    /// <summary>Dispose.</summary>
    public void Dispose() => _db.Dispose();

    /// <summary><summary>Fenstergeometrie_WirdGespeichert_UndWiederhergestellt.</summary>.</summary>
    [Fact]
    public async Task Fenstergeometrie_WirdGespeichert_UndWiederhergestellt()
    {
        var geometry = new WindowGeometrySettings(X: 200, Y: 150, Width: 1400, Height: 900);

        await _settingService.SetWindowGeometryAsync(geometry, TestContext.Current.CancellationToken);
        var geladen = await _settingService.GetWindowGeometryAsync(TestContext.Current.CancellationToken);

        geladen.X.Should().Be(200);
        geladen.Y.Should().Be(150);
        geladen.Width.Should().Be(1400);
        geladen.Height.Should().Be(900);
    }

    /// <summary><summary>Fenstergeometrie_OhneGespeicherteWerte_GibtNull_Zurueck.</summary>.</summary>
    [Fact]
    public async Task Fenstergeometrie_OhneGespeicherteWerte_GibtNull_Zurueck()
    {
        var geladen = await _settingService.GetWindowGeometryAsync(TestContext.Current.CancellationToken);

        geladen.X.Should().BeNull();
        geladen.Y.Should().BeNull();
        geladen.Width.Should().BeNull();
        geladen.Height.Should().BeNull();
    }

    /// <summary><summary>Fenstergeometrie_KannMehrmalsAktualisiert_Werden.</summary>.</summary>
    [Fact]
    public async Task Fenstergeometrie_KannMehrmalsAktualisiert_Werden()
    {
        await _settingService.SetWindowGeometryAsync(new WindowGeometrySettings(100, 100, 1200, 800), TestContext.Current.CancellationToken);
        await _settingService.SetWindowGeometryAsync(new WindowGeometrySettings(300, 250, 1600, 1000), TestContext.Current.CancellationToken);

        var geladen = await _settingService.GetWindowGeometryAsync(TestContext.Current.CancellationToken);

        geladen.X.Should().Be(300);
        geladen.Y.Should().Be(250);
        geladen.Width.Should().Be(1600);
        geladen.Height.Should().Be(1000);
    }

    /// <summary><summary>Fenstergeometrie_BeimNeustart_WirdWiederhergestellt.</summary>.</summary>
    [Fact]
    public async Task Fenstergeometrie_BeimNeustart_WirdWiederhergestellt()
    {
        await _settingService.SetWindowGeometryAsync(new WindowGeometrySettings(50, 75, 1280, 720), TestContext.Current.CancellationToken);

        var neuerService = new AppSettingService(_db, NullLogger<AppSettingService>.Instance);
        var geladen = await neuerService.GetWindowGeometryAsync(TestContext.Current.CancellationToken);

        geladen.X.Should().Be(50);
        geladen.Y.Should().Be(75);
        geladen.Width.Should().Be(1280);
        geladen.Height.Should().Be(720);
    }
}
