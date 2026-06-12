using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.ServiceIntegration;

/// <summary>E2E-Test: Dark Mode aktivieren, persistieren, beim Neustart wiederherstellen.</summary>
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
        await _einstellungService.SetBoolSettingAsync(AppEinstellungService.DarkModeEnabledKey, true);

        var geladen = await _einstellungService.GetBoolSettingAsync(AppEinstellungService.DarkModeEnabledKey);
        geladen.Should().Be(true);
    }

    [Fact]
    public async Task DarkMode_KannDeaktiviert_Werden()
    {
        await _einstellungService.SetBoolSettingAsync(AppEinstellungService.DarkModeEnabledKey, true);
        await _einstellungService.SetBoolSettingAsync(AppEinstellungService.DarkModeEnabledKey, false);

        var geladen = await _einstellungService.GetBoolSettingAsync(AppEinstellungService.DarkModeEnabledKey);
        geladen.Should().Be(false);
    }

    [Fact]
    public async Task DarkMode_Standardwert_IstNull_OhneGespeicherteEinstellung()
    {
        var geladen = await _einstellungService.GetBoolSettingAsync(AppEinstellungService.DarkModeEnabledKey);
        geladen.Should().BeNull();
    }

    [Fact]
    public async Task DarkMode_WirdBeimNeustart_Wiederhergestellt()
    {
        await _einstellungService.SetBoolSettingAsync(AppEinstellungService.DarkModeEnabledKey, true);

        var neuerService = new AppEinstellungService(_db, NullLogger<AppEinstellungService>.Instance);
        var geladen = await neuerService.GetBoolSettingAsync(AppEinstellungService.DarkModeEnabledKey);
        geladen.Should().Be(true);
    }
}
