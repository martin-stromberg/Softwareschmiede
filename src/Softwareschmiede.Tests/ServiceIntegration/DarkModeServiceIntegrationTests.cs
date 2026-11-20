using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.ServiceIntegration;

/// <summary>Integrationstests für die Dark-Mode-Persistierung.</summary>
public sealed class DarkModeServiceIntegrationTests : IDisposable
{
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiedeDbContext _db;
    private readonly AppSettingService _settingService;

    /// <summary>DarkModeServiceIntegrationTests.</summary>
    public DarkModeServiceIntegrationTests()
    {
        _db = TestDbContextFactory.Create();
        _settingService = new AppSettingService(_db, NullLogger<AppSettingService>.Instance);
    }

    /// <summary>Dispose.</summary>
    public void Dispose() => _db.Dispose();

    /// <summary>DarkMode_WirdGespeichert_UndBeimLadenWiederhergestellt.</summary>
    [Fact]
    public async Task DarkMode_WirdGespeichert_UndBeimLadenWiederhergestellt()
    {
        await _settingService.SetSettingAsync(AppSettingService.DesignModeKey, "Dark", TestContext.Current.CancellationToken);

        var geladen = await _settingService.GetSettingAsync(AppSettingService.DesignModeKey, TestContext.Current.CancellationToken);
        geladen.Should().Be("Dark");
    }

    /// <summary>DarkMode_KannAufLightUmgestellt_Werden.</summary>
    [Fact]
    public async Task DarkMode_KannAufLightUmgestellt_Werden()
    {
        await _settingService.SetSettingAsync(AppSettingService.DesignModeKey, "Dark", TestContext.Current.CancellationToken);
        await _settingService.SetSettingAsync(AppSettingService.DesignModeKey, "Light", TestContext.Current.CancellationToken);

        var geladen = await _settingService.GetSettingAsync(AppSettingService.DesignModeKey, TestContext.Current.CancellationToken);
        geladen.Should().Be("Light");
    }

    /// <summary>DarkMode_Standardwert_IstNull_OhneGespeicherteEinstellung.</summary>
    [Fact]
    public async Task DarkMode_Standardwert_IstNull_OhneGespeicherteEinstellung()
    {
        var geladen = await _settingService.GetSettingAsync(AppSettingService.DesignModeKey, TestContext.Current.CancellationToken);
        geladen.Should().BeNull();
    }

    /// <summary>DarkMode_WirdBeimNeustart_Wiederhergestellt.</summary>
    [Fact]
    public async Task DarkMode_WirdBeimNeustart_Wiederhergestellt()
    {
        await _settingService.SetSettingAsync(AppSettingService.DesignModeKey, "Dark", TestContext.Current.CancellationToken);

        var neuerService = new AppSettingService(_db, NullLogger<AppSettingService>.Instance);
        var geladen = await neuerService.GetSettingAsync(AppSettingService.DesignModeKey, TestContext.Current.CancellationToken);
        geladen.Should().Be("Dark");
    }
}
