using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Softwareschmiede.App.Services;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.ServiceIntegration;

/// <summary>Integrationstests für die Persistenz des Aufgabenlisten-Anzeigemodus (je Ansicht eigener Schlüssel).</summary>
public sealed class WorkItemsListsModeServiceTests : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly WorkItemListModeService _sut;

    /// <summary>AufgabenListenModusServiceTests.</summary>
    public WorkItemsListsModeServiceTests()
    {
        (_sut, _provider) = WorkItemsListsModeServiceTestFactory.CreateModeService();
    }

    /// <summary>Dispose.</summary>
    public void Dispose() => _provider.Dispose();

    /// <summary>Der gespeicherte Menü-Modus wird über einen neuen Scope (Neustart-Semantik) wiederhergestellt.</summary>
    [Fact]
    public async Task SaveAndLoad_MenuMode_RoundtripOverDisconnectedScopes()
    {
        await _sut.SaveMenuModeAsync(WorkItemListMode.Repository, TestContext.Current.CancellationToken);

        var loaded = await _sut.LoadMenuModeAsync(TestContext.Current.CancellationToken);
        loaded.Should().Be(WorkItemListMode.Repository);
    }

    /// <summary>Der gespeicherte Dashboard-Modus wird über einen neuen Scope wiederhergestellt.</summary>
    [Fact]
    public async Task SaveAndLoad_DashboardMode_RoundtripOverDisconnectedScopes()
    {
        await _sut.SaveDashboardModeAsync(WorkItemListMode.Ai, TestContext.Current.CancellationToken);

        var loaded = await _sut.LoadDashboardModeAsync(TestContext.Current.CancellationToken);
        loaded.Should().Be(WorkItemListMode.Ai);
    }

    /// <summary>Ohne gespeicherten Wert liefert das Laden den Default Datum.</summary>
    [Fact]
    public async Task Load_MenuMode_ReturnsDate_WhenMissing()
    {
        var loaded = await _sut.LoadMenuModeAsync(TestContext.Current.CancellationToken);
        loaded.Should().Be(WorkItemListMode.Date);
    }

    /// <summary>Ein ungültiger gespeicherter Wert fällt auf den Default Datum zurück.</summary>
    [Fact]
    public async Task Load_MenuMode_ReturnsDate_WhenInvalid()
    {
        using (var scope = _provider.CreateScope())
        {
            var settingService = scope.ServiceProvider.GetRequiredService<AppSettingService>();
            await settingService.SetSettingAsync(AppSettingService.ActiveWorkItemsListModeKey, "UnbekannterModus", TestContext.Current.CancellationToken);
        }

        var loaded = await _sut.LoadMenuModeAsync(TestContext.Current.CancellationToken);
        loaded.Should().Be(WorkItemListMode.Date);
    }

    /// <summary>Ein leerer gespeicherter Wert fällt auf den Default Datum zurück.</summary>
    [Fact]
    public async Task Load_DashboardMode_ReturnsDate_WhenEmpty()
    {
        using (var scope = _provider.CreateScope())
        {
            var settingService = scope.ServiceProvider.GetRequiredService<AppSettingService>();
            await settingService.SetSettingAsync(AppSettingService.ActiveWorkItemsListModeDashboardKey, string.Empty, TestContext.Current.CancellationToken);
        }

        var loaded = await _sut.LoadDashboardModeAsync(TestContext.Current.CancellationToken);
        loaded.Should().Be(WorkItemListMode.Date);
    }

    /// <summary>Menü- und Dashboard-Schlüssel sind unabhängig voneinander.</summary>
    [Fact]
    public async Task KeyIndependent_MenuAndDashboardInfluenceSelfNot()
    {
        await _sut.SaveMenuModeAsync(WorkItemListMode.Repository, TestContext.Current.CancellationToken);

        (await _sut.LoadMenuModeAsync(TestContext.Current.CancellationToken)).Should().Be(WorkItemListMode.Repository);
        (await _sut.LoadDashboardModeAsync(TestContext.Current.CancellationToken)).Should().Be(WorkItemListMode.Date);
    }
}
