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
    public async Task DarkMode_WillSaved_AndAtLoadRestored()
    {
        await _settingService.SetSettingAsync(AppSettingService.DesignModeKey, "Dark", TestContext.Current.CancellationToken);

        var loaded = await _settingService.GetSettingAsync(AppSettingService.DesignModeKey, TestContext.Current.CancellationToken);
        loaded.Should().Be("Dark");
    }

    /// <summary>DarkMode_KannAufLightUmgestellt_Werden.</summary>
    [Fact]
    public async Task DarkMode_CanOnLightSwitched_Be()
    {
        await _settingService.SetSettingAsync(AppSettingService.DesignModeKey, "Dark", TestContext.Current.CancellationToken);
        await _settingService.SetSettingAsync(AppSettingService.DesignModeKey, "Light", TestContext.Current.CancellationToken);

        var loaded = await _settingService.GetSettingAsync(AppSettingService.DesignModeKey, TestContext.Current.CancellationToken);
        loaded.Should().Be("Light");
    }

    /// <summary>DarkMode_Standardwert_IstNull_OhneGespeicherteEinstellung.</summary>
    [Fact]
    public async Task DarkMode_Defaultvalue_IsNull_WithoutSavedSetting()
    {
        var loaded = await _settingService.GetSettingAsync(AppSettingService.DesignModeKey, TestContext.Current.CancellationToken);
        loaded.Should().BeNull();
    }

    /// <summary>DarkMode_WirdBeimNeustart_Wiederhergestellt.</summary>
    [Fact]
    public async Task DarkMode_WillAtRestart_Restored()
    {
        await _settingService.SetSettingAsync(AppSettingService.DesignModeKey, "Dark", TestContext.Current.CancellationToken);

        var newService = new AppSettingService(_db, NullLogger<AppSettingService>.Instance);
        var loaded = await newService.GetSettingAsync(AppSettingService.DesignModeKey, TestContext.Current.CancellationToken);
        loaded.Should().Be("Dark");
    }
}
