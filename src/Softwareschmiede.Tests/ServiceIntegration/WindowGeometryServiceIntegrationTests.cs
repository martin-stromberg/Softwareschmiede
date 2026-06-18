using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.ServiceIntegration;

/// <summary>E2E-Test: Fensterposition verschieben/skalieren und beim Neustart wiederherstellen.</summary>
public sealed class WindowGeometryServiceIntegrationTests : IDisposable
{
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiededDbContext _db;
    private readonly AppEinstellungService _einstellungService;

    public WindowGeometryServiceIntegrationTests()
    {
        _db = TestDbContextFactory.Create();
        _einstellungService = new AppEinstellungService(_db, NullLogger<AppEinstellungService>.Instance);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Fenstergeometrie_WirdGespeichert_UndWiederhergestellt()
    {
        var geometry = new WindowGeometrySettings(X: 200, Y: 150, Width: 1400, Height: 900);

        await _einstellungService.SetWindowGeometryAsync(geometry);
        var geladen = await _einstellungService.GetWindowGeometryAsync();

        geladen.X.Should().Be(200);
        geladen.Y.Should().Be(150);
        geladen.Width.Should().Be(1400);
        geladen.Height.Should().Be(900);
    }

    [Fact]
    public async Task Fenstergeometrie_OhneGespeicherteWerte_GibtNull_Zurueck()
    {
        var geladen = await _einstellungService.GetWindowGeometryAsync();

        geladen.X.Should().BeNull();
        geladen.Y.Should().BeNull();
        geladen.Width.Should().BeNull();
        geladen.Height.Should().BeNull();
    }

    [Fact]
    public async Task Fenstergeometrie_KannMehrmalsAktualisiert_Werden()
    {
        await _einstellungService.SetWindowGeometryAsync(new WindowGeometrySettings(100, 100, 1200, 800));
        await _einstellungService.SetWindowGeometryAsync(new WindowGeometrySettings(300, 250, 1600, 1000));

        var geladen = await _einstellungService.GetWindowGeometryAsync();

        geladen.X.Should().Be(300);
        geladen.Y.Should().Be(250);
        geladen.Width.Should().Be(1600);
        geladen.Height.Should().Be(1000);
    }

    [Fact]
    public async Task Fenstergeometrie_BeimNeustart_WirdWiederhergestellt()
    {
        await _einstellungService.SetWindowGeometryAsync(new WindowGeometrySettings(50, 75, 1280, 720));

        var neuerService = new AppEinstellungService(_db, NullLogger<AppEinstellungService>.Instance);
        var geladen = await neuerService.GetWindowGeometryAsync();

        geladen.X.Should().Be(50);
        geladen.Y.Should().Be(75);
        geladen.Width.Should().Be(1280);
        geladen.Height.Should().Be(720);
    }
}
