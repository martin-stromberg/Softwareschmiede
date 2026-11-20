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

    /// <summary>Fenstergeometrie_WirdGespeichert_UndWiederhergestellt.</summary>
    [Fact]
    public async Task Windowgeometry_WillSaved_AndRestored()
    {
        var geometry = new WindowGeometrySettings(X: 200, Y: 150, Width: 1400, Height: 900);

        await _settingService.SetWindowGeometryAsync(geometry, TestContext.Current.CancellationToken);
        var loaded = await _settingService.GetWindowGeometryAsync(TestContext.Current.CancellationToken);

        loaded.X.Should().Be(200);
        loaded.Y.Should().Be(150);
        loaded.Width.Should().Be(1400);
        loaded.Height.Should().Be(900);
    }

    /// <summary>Fenstergeometrie_OhneGespeicherteWerte_GibtNull_Zurueck.</summary>
    [Fact]
    public async Task Windowgeometry_WithoutSavedValues_ReturnsNull_Back()
    {
        var loaded = await _settingService.GetWindowGeometryAsync(TestContext.Current.CancellationToken);

        loaded.X.Should().BeNull();
        loaded.Y.Should().BeNull();
        loaded.Width.Should().BeNull();
        loaded.Height.Should().BeNull();
    }

    /// <summary>Fenstergeometrie_KannMehrmalsAktualisiert_Werden.</summary>
    [Fact]
    public async Task Windowgeometry_UpdatesCanRepeatedly_Be()
    {
        await _settingService.SetWindowGeometryAsync(new WindowGeometrySettings(100, 100, 1200, 800), TestContext.Current.CancellationToken);
        await _settingService.SetWindowGeometryAsync(new WindowGeometrySettings(300, 250, 1600, 1000), TestContext.Current.CancellationToken);

        var loaded = await _settingService.GetWindowGeometryAsync(TestContext.Current.CancellationToken);

        loaded.X.Should().Be(300);
        loaded.Y.Should().Be(250);
        loaded.Width.Should().Be(1600);
        loaded.Height.Should().Be(1000);
    }

    /// <summary>Fenstergeometrie_BeimNeustart_WirdWiederhergestellt.</summary>
    [Fact]
    public async Task Windowgeometry_AtRestart_WillRestored()
    {
        await _settingService.SetWindowGeometryAsync(new WindowGeometrySettings(50, 75, 1280, 720), TestContext.Current.CancellationToken);

        var newService = new AppSettingService(_db, NullLogger<AppSettingService>.Instance);
        var loaded = await newService.GetWindowGeometryAsync(TestContext.Current.CancellationToken);

        loaded.X.Should().Be(50);
        loaded.Y.Should().Be(75);
        loaded.Width.Should().Be(1280);
        loaded.Height.Should().Be(720);
    }
}
