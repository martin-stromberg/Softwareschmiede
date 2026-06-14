using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.ServiceIntegration;

/// <summary>Integrationstests für die Dark-Mode-Persistierung.</summary>
public sealed class DarkModeServiceIntegrationTests : IDisposable
{
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiededDbContext _db;
    private readonly AppEinstellungService _einstellungService;

    public DarkModeServiceIntegrationTests()
    {
        _db = TestDbContextFactory.Create();
        _einstellungService = new AppEinstellungService(_db, NullLogger<AppEinstellungService>.Instance);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task DarkMode_WirdGespeichert_UndBeimLadenWiederhergestellt()
    {
        await _einstellungService.SetSettingAsync(AppEinstellungService.DesignModeKey, "Dark");

        var geladen = await _einstellungService.GetSettingAsync(AppEinstellungService.DesignModeKey);
        geladen.Should().Be("Dark");
    }

    [Fact]
    public async Task DarkMode_KannAufLightUmgestellt_Werden()
    {
        await _einstellungService.SetSettingAsync(AppEinstellungService.DesignModeKey, "Dark");
        await _einstellungService.SetSettingAsync(AppEinstellungService.DesignModeKey, "Light");

        var geladen = await _einstellungService.GetSettingAsync(AppEinstellungService.DesignModeKey);
        geladen.Should().Be("Light");
    }

    [Fact]
    public async Task DarkMode_Standardwert_IstNull_OhneGespeicherteEinstellung()
    {
        var geladen = await _einstellungService.GetSettingAsync(AppEinstellungService.DesignModeKey);
        geladen.Should().BeNull();
    }

    [Fact]
    public async Task DarkMode_WirdBeimNeustart_Wiederhergestellt()
    {
        await _einstellungService.SetSettingAsync(AppEinstellungService.DesignModeKey, "Dark");

        var neuerService = new AppEinstellungService(_db, NullLogger<AppEinstellungService>.Instance);
        var geladen = await neuerService.GetSettingAsync(AppEinstellungService.DesignModeKey);
        geladen.Should().Be("Dark");
    }
}
