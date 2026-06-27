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

    /// <summary>DarkModeServiceIntegrationTests.</summary>
    public DarkModeServiceIntegrationTests()
    {
        _db = TestDbContextFactory.Create();
        _einstellungService = new AppEinstellungService(_db, NullLogger<AppEinstellungService>.Instance);
    }

    /// <summary>Dispose.</summary>
    public void Dispose() => _db.Dispose();

    /// <summary><summary>DarkMode_WirdGespeichert_UndBeimLadenWiederhergestellt.</summary>.</summary>
    [Fact]
    /// <summary>DarkMode_WirdGespeichert_UndBeimLadenWiederhergestellt.</summary>
    public async Task DarkMode_WirdGespeichert_UndBeimLadenWiederhergestellt()
    {
        await _einstellungService.SetSettingAsync(AppEinstellungService.DesignModeKey, "Dark");

        var geladen = await _einstellungService.GetSettingAsync(AppEinstellungService.DesignModeKey);
        geladen.Should().Be("Dark");
    }

    /// <summary><summary>DarkMode_KannAufLightUmgestellt_Werden.</summary>.</summary>
    [Fact]
    /// <summary>DarkMode_KannAufLightUmgestellt_Werden.</summary>
    public async Task DarkMode_KannAufLightUmgestellt_Werden()
    {
        await _einstellungService.SetSettingAsync(AppEinstellungService.DesignModeKey, "Dark");
        await _einstellungService.SetSettingAsync(AppEinstellungService.DesignModeKey, "Light");

        var geladen = await _einstellungService.GetSettingAsync(AppEinstellungService.DesignModeKey);
        geladen.Should().Be("Light");
    }

    /// <summary><summary>DarkMode_Standardwert_IstNull_OhneGespeicherteEinstellung.</summary>.</summary>
    [Fact]
    /// <summary>DarkMode_Standardwert_IstNull_OhneGespeicherteEinstellung.</summary>
    public async Task DarkMode_Standardwert_IstNull_OhneGespeicherteEinstellung()
    {
        var geladen = await _einstellungService.GetSettingAsync(AppEinstellungService.DesignModeKey);
        geladen.Should().BeNull();
    }

    /// <summary><summary>DarkMode_WirdBeimNeustart_Wiederhergestellt.</summary>.</summary>
    [Fact]
    /// <summary>DarkMode_WirdBeimNeustart_Wiederhergestellt.</summary>
    public async Task DarkMode_WirdBeimNeustart_Wiederhergestellt()
    {
        await _einstellungService.SetSettingAsync(AppEinstellungService.DesignModeKey, "Dark");

        var neuerService = new AppEinstellungService(_db, NullLogger<AppEinstellungService>.Instance);
        var geladen = await neuerService.GetSettingAsync(AppEinstellungService.DesignModeKey);
        geladen.Should().Be("Dark");
    }
}
