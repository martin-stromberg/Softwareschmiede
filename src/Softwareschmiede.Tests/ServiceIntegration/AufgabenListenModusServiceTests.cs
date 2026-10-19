using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Softwareschmiede.App.Services;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.ServiceIntegration;

/// <summary>Integrationstests für die Persistenz des Aufgabenlisten-Anzeigemodus (je Ansicht eigener Schlüssel).</summary>
public sealed class AufgabenListenModusServiceTests : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly AufgabenListenModusService _sut;

    /// <summary>AufgabenListenModusServiceTests.</summary>
    public AufgabenListenModusServiceTests()
    {
        (_sut, _provider) = AufgabenListenModusServiceTestFactory.ErstelleModusService();
    }

    /// <summary>Dispose.</summary>
    public void Dispose() => _provider.Dispose();

    /// <summary>Der gespeicherte Menü-Modus wird über einen neuen Scope (Neustart-Semantik) wiederhergestellt.</summary>
    [Fact]
    public async Task SpeichernUndLaden_MenueModus_RoundtripUeberGetrennteScopes()
    {
        await _sut.SpeichereMenueModusAsync(AufgabenListenModus.Repository);

        var geladen = await _sut.LadeMenueModusAsync();
        geladen.Should().Be(AufgabenListenModus.Repository);
    }

    /// <summary>Der gespeicherte Dashboard-Modus wird über einen neuen Scope wiederhergestellt.</summary>
    [Fact]
    public async Task SpeichernUndLaden_DashboardModus_RoundtripUeberGetrennteScopes()
    {
        await _sut.SpeichereDashboardModusAsync(AufgabenListenModus.Ki);

        var geladen = await _sut.LadeDashboardModusAsync();
        geladen.Should().Be(AufgabenListenModus.Ki);
    }

    /// <summary>Ohne gespeicherten Wert liefert das Laden den Default Datum.</summary>
    [Fact]
    public async Task Lade_MenueModus_ReturnsDatum_WhenMissing()
    {
        var geladen = await _sut.LadeMenueModusAsync();
        geladen.Should().Be(AufgabenListenModus.Datum);
    }

    /// <summary>Ein ungültiger gespeicherter Wert fällt auf den Default Datum zurück.</summary>
    [Fact]
    public async Task Lade_MenueModus_ReturnsDatum_WhenInvalid()
    {
        using (var scope = _provider.CreateScope())
        {
            var einstellungService = scope.ServiceProvider.GetRequiredService<AppEinstellungService>();
            await einstellungService.SetSettingAsync(AppEinstellungService.AktiveAufgabenListenModusKey, "UnbekannterModus");
        }

        var geladen = await _sut.LadeMenueModusAsync();
        geladen.Should().Be(AufgabenListenModus.Datum);
    }

    /// <summary>Ein leerer gespeicherter Wert fällt auf den Default Datum zurück.</summary>
    [Fact]
    public async Task Lade_DashboardModus_ReturnsDatum_WhenEmpty()
    {
        using (var scope = _provider.CreateScope())
        {
            var einstellungService = scope.ServiceProvider.GetRequiredService<AppEinstellungService>();
            await einstellungService.SetSettingAsync(AppEinstellungService.AktiveAufgabenListenModusDashboardKey, string.Empty);
        }

        var geladen = await _sut.LadeDashboardModusAsync();
        geladen.Should().Be(AufgabenListenModus.Datum);
    }

    /// <summary>Menü- und Dashboard-Schlüssel sind unabhängig voneinander.</summary>
    [Fact]
    public async Task SchluesselUnabhaengig_MenueUndDashboardBeeinflussenSichNicht()
    {
        await _sut.SpeichereMenueModusAsync(AufgabenListenModus.Repository);

        (await _sut.LadeMenueModusAsync()).Should().Be(AufgabenListenModus.Repository);
        (await _sut.LadeDashboardModusAsync()).Should().Be(AufgabenListenModus.Datum);
    }
}
