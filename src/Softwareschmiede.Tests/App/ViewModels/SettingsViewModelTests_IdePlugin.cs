using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Softwareschmiede.App.Services;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Abstractions;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.App.ViewModels;

/// <summary>Unit-Tests für die IDE-Plugin-Logik in SettingsViewModel: Reihenfolge-Parsing (plugins.ide.order), Verschiebe-Logik sowie Aktivierungs-Validierung und -Persistierung.</summary>
public sealed class SettingsViewModelTests_IdePlugin : IDisposable
{
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiededDbContext _db;
    private readonly AppEinstellungService _einstellungService;
    private readonly ArbeitsverzeichnisSettingsService _arbeitsverzeichnisService;
    private readonly DarkModeService _darkModeService;
    private readonly Mock<IPluginManager> _pluginManagerMock;
    private readonly PluginActivationService _pluginActivationService;
    private readonly PluginSettingsService _pluginSettingsService;
    private readonly PromptVorlagenService _promptVorlagenService;

    /// <summary>SettingsViewModelTests_IdePlugin.</summary>
    public SettingsViewModelTests_IdePlugin()
    {
        _db = TestDbContextFactory.Create();
        _einstellungService = new AppEinstellungService(_db, NullLogger<AppEinstellungService>.Instance);
        _arbeitsverzeichnisService = new ArbeitsverzeichnisSettingsService(_db, NullLogger<ArbeitsverzeichnisSettingsService>.Instance);

        var scopeMock = new Mock<IServiceScope>();
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        var serviceProviderMock = new Mock<IServiceProvider>();

        scopeFactoryMock.Setup(f => f.CreateScope()).Returns(scopeMock.Object);
        scopeMock.Setup(s => s.ServiceProvider).Returns(serviceProviderMock.Object);
        serviceProviderMock.Setup(p => p.GetService(typeof(AppEinstellungService))).Returns(_einstellungService);

        _darkModeService = new DarkModeService(scopeFactoryMock.Object, NullLogger<DarkModeService>.Instance);
        _pluginManagerMock = new Mock<IPluginManager>();
        _pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([]);
        _pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([]);
        _pluginActivationService = new PluginActivationService(_einstellungService, _pluginManagerMock.Object, NullLogger<PluginActivationService>.Instance);
        _pluginSettingsService = new PluginSettingsService(new Mock<ICredentialStore>().Object, NullLogger<PluginSettingsService>.Instance);
        _promptVorlagenService = new PromptVorlagenService(_db, NullLogger<PromptVorlagenService>.Instance);
    }

    /// <summary>Dispose.</summary>
    public void Dispose() => _db.Dispose();

    private SettingsViewModel CreateSut() =>
        new(
            _einstellungService,
            _arbeitsverzeichnisService,
            _darkModeService,
            _pluginManagerMock.Object,
            _pluginActivationService,
            _pluginSettingsService,
            _promptVorlagenService,
            NullLogger<SettingsViewModel>.Instance,
            Options.Create(new AutonomAufgabenOptions()));

    private static IIdePlugin CreateIdePlugin(string pluginName, string pluginPrefix)
    {
        var mock = new Mock<IIdePlugin>();
        mock.Setup(p => p.PluginName).Returns(pluginName);
        mock.Setup(p => p.PluginPrefix).Returns(pluginPrefix);
        mock.Setup(p => p.PluginType).Returns(PluginType.Ide);
        mock.Setup(p => p.GetSettingGroups()).Returns([]);
        return mock.Object;
    }

    private static IGitPlugin CreateScmPlugin(string pluginName, string pluginPrefix)
    {
        var mock = new Mock<IGitPlugin>();
        mock.Setup(p => p.PluginName).Returns(pluginName);
        mock.Setup(p => p.PluginPrefix).Returns(pluginPrefix);
        mock.Setup(p => p.PluginType).Returns(PluginType.SourceCodeManagement);
        mock.Setup(p => p.GetSettingGroups()).Returns([]);
        return mock.Object;
    }

    /// <summary>
    /// Die Sichtbarkeit der beiden Inhaltsbereiche (SCM/KI-Plugin-Einstellungen vs. IDE-Plugin-Einstellungen)
    /// ist exklusiv: Nur der zuletzt ausgewählte Bereich ist sichtbar. Die jeweils andere Auswahl
    /// (SelectedPlugin bzw. SelectedIdePlugin) bleibt dabei erhalten und wird nicht auf null zurückgesetzt.
    /// </summary>
    [Fact]
    public async Task PluginUndIdePluginAuswahl_SchaltenInhaltsbereicheExklusivSichtbar()
    {
        var scmPlugin = CreateScmPlugin("GitHub", "Softwareschmiede.GitHub");
        _pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([scmPlugin]);
        var visualStudio = CreateIdePlugin("Visual Studio", "Softwareschmiede.VisualStudio");
        _pluginManagerMock.Setup(m => m.GetIdePlugins()).Returns([visualStudio]);
        var sut = CreateSut();
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();
        var scmEintrag = sut.SourceCodeManagementPlugins.Single();
        var idePluginEintrag = sut.DevelopmentEnvironmentPlugins.Single();

        sut.PluginSelectedCommand.Execute(scmEintrag);

        sut.IsScmKiPluginContentVisible.Should().BeTrue();
        sut.IsIdePluginContentVisible.Should().BeFalse();

        sut.IdePluginSelectedCommand.Execute(idePluginEintrag);

        sut.IsIdePluginContentVisible.Should().BeTrue();
        sut.IsScmKiPluginContentVisible.Should().BeFalse();
        sut.SelectedPlugin.Should().NotBeNull();

        sut.PluginSelectedCommand.Execute(scmEintrag);

        sut.IsScmKiPluginContentVisible.Should().BeTrue();
        sut.IsIdePluginContentVisible.Should().BeFalse();
        sut.SelectedIdePlugin.Should().NotBeNull();
    }

    /// <summary>Ohne gespeichertes plugins.ide.order-Setting entspricht die Reihenfolge der Entdeckungsreihenfolge.</summary>
    [Fact]
    public async Task LadenAsync_OhneOrderSetting_VerwendetEntdeckungsreihenfolge()
    {
        var visualStudio = CreateIdePlugin("Visual Studio", "Softwareschmiede.VisualStudio");
        var vsCode = CreateIdePlugin("Visual Studio Code", "Softwareschmiede.VisualStudioCode");
        _pluginManagerMock.Setup(m => m.GetIdePlugins()).Returns([visualStudio, vsCode]);
        var sut = CreateSut();

        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.DevelopmentEnvironmentPlugins.Select(e => e.PluginPrefix).Should().Equal(
            "Softwareschmiede.VisualStudio",
            "Softwareschmiede.VisualStudioCode");
        sut.IdePluginOrder.Should().Equal("Softwareschmiede.VisualStudio", "Softwareschmiede.VisualStudioCode");
        sut.DefaultIdePlugin.Should().Be("Softwareschmiede.VisualStudio");
    }

    /// <summary>Ein unbekannter Prefix im gespeicherten Setting wird beim Parsen ignoriert.</summary>
    [Fact]
    public async Task LadenAsync_OrderSettingMitUnbekanntemPrefix_IgnoriertUnbekanntenPrefix()
    {
        var visualStudio = CreateIdePlugin("Visual Studio", "Softwareschmiede.VisualStudio");
        var vsCode = CreateIdePlugin("Visual Studio Code", "Softwareschmiede.VisualStudioCode");
        _pluginManagerMock.Setup(m => m.GetIdePlugins()).Returns([visualStudio, vsCode]);
        await _einstellungService.SetSettingAsync("plugins.ide.order", "Softwareschmiede.Unbekannt,Softwareschmiede.VisualStudioCode");
        var sut = CreateSut();

        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.IdePluginOrder.Should().Equal("Softwareschmiede.VisualStudioCode", "Softwareschmiede.VisualStudio");
    }

    /// <summary>Ein im gespeicherten Setting fehlender Prefix wird in Entdeckungsreihenfolge angehängt.</summary>
    [Fact]
    public async Task LadenAsync_OrderSettingMitFehlendemPrefix_HaengtFehlendenPrefixAn()
    {
        var visualStudio = CreateIdePlugin("Visual Studio", "Softwareschmiede.VisualStudio");
        var vsCode = CreateIdePlugin("Visual Studio Code", "Softwareschmiede.VisualStudioCode");
        _pluginManagerMock.Setup(m => m.GetIdePlugins()).Returns([visualStudio, vsCode]);
        await _einstellungService.SetSettingAsync("plugins.ide.order", "Softwareschmiede.VisualStudioCode");
        var sut = CreateSut();

        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.IdePluginOrder.Should().Equal("Softwareschmiede.VisualStudioCode", "Softwareschmiede.VisualStudio");
    }

    /// <summary>Der erste Eintrag kann nicht weiter nach oben, der letzte nicht weiter nach unten verschoben werden.</summary>
    [Fact]
    public async Task IdePluginMoveCommands_AnDenGrenzen_SindNichtAusfuehrbar()
    {
        var visualStudio = CreateIdePlugin("Visual Studio", "Softwareschmiede.VisualStudio");
        var vsCode = CreateIdePlugin("Visual Studio Code", "Softwareschmiede.VisualStudioCode");
        _pluginManagerMock.Setup(m => m.GetIdePlugins()).Returns([visualStudio, vsCode]);
        var sut = CreateSut();
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();
        var ersterEintrag = sut.DevelopmentEnvironmentPlugins[0];
        var letzterEintrag = sut.DevelopmentEnvironmentPlugins[1];

        sut.IdePluginMoveUpCommand.CanExecute(ersterEintrag).Should().BeFalse();
        sut.IdePluginMoveDownCommand.CanExecute(ersterEintrag).Should().BeTrue();
        sut.IdePluginMoveUpCommand.CanExecute(letzterEintrag).Should().BeTrue();
        sut.IdePluginMoveDownCommand.CanExecute(letzterEintrag).Should().BeFalse();
    }

    /// <summary>IdePluginMoveDownCommand verschiebt den Eintrag und aktualisiert IdePluginOrder sowie DefaultIdePlugin.</summary>
    [Fact]
    public async Task IdePluginMoveDownCommand_VerschiebtEintragUndAktualisiertReihenfolge()
    {
        var visualStudio = CreateIdePlugin("Visual Studio", "Softwareschmiede.VisualStudio");
        var vsCode = CreateIdePlugin("Visual Studio Code", "Softwareschmiede.VisualStudioCode");
        _pluginManagerMock.Setup(m => m.GetIdePlugins()).Returns([visualStudio, vsCode]);
        var sut = CreateSut();
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();
        var ersterEintrag = sut.DevelopmentEnvironmentPlugins[0];

        sut.IdePluginMoveDownCommand.Execute(ersterEintrag);

        sut.DevelopmentEnvironmentPlugins.Select(e => e.PluginPrefix).Should().Equal(
            "Softwareschmiede.VisualStudioCode",
            "Softwareschmiede.VisualStudio");
        sut.IdePluginOrder.Should().Equal("Softwareschmiede.VisualStudioCode", "Softwareschmiede.VisualStudio");
        sut.DefaultIdePlugin.Should().Be("Softwareschmiede.VisualStudioCode");
    }

    /// <summary>Der Versuch, das letzte aktive IDE-Plugin zu deaktivieren, wird sofort rückgängig gemacht und zeigt eine Fehlermeldung, ohne dass Speichern nötig ist.</summary>
    [Fact]
    public async Task DeaktivierenDesLetztenAktivenIdePlugins_WirdSofortRueckgaengigGemacht()
    {
        var visualStudio = CreateIdePlugin("Visual Studio", "Softwareschmiede.VisualStudio");
        _pluginManagerMock.Setup(m => m.GetIdePlugins()).Returns([visualStudio]);
        var sut = CreateSut();
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();
        var eintrag = sut.DevelopmentEnvironmentPlugins.Single();
        eintrag.IsEnabled.Should().BeTrue();

        eintrag.IsEnabled = false;

        eintrag.IsEnabled.Should().BeTrue();
        sut.FehlerMeldung.Should().Contain("Mindestens ein IDE-Plugin");
    }

    /// <summary>Eine noch nicht gespeicherte IDE-Plugin-Aktivierungsänderung wird durch VerwerfenAsync rückgängig gemacht und bleibt in der Datenbank unverändert (Konsistenz mit SCM-/KI-Plugins).</summary>
    [Fact]
    public async Task VerwerfenAsync_MachtNichtGespeicherteIdePluginAktivierungRueckgaengig()
    {
        var visualStudio = CreateIdePlugin("Visual Studio", "Softwareschmiede.VisualStudio");
        var vsCode = CreateIdePlugin("Visual Studio Code", "Softwareschmiede.VisualStudioCode");
        _pluginManagerMock.Setup(m => m.GetIdePlugins()).Returns([visualStudio, vsCode]);
        var sut = CreateSut();
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.DevelopmentEnvironmentPlugins.Single(e => e.PluginPrefix == "Softwareschmiede.VisualStudioCode").IsEnabled = false;
        await ((AsyncRelayCommand)sut.VerwerfenCommand).ExecuteAsync();

        sut.DevelopmentEnvironmentPlugins.Single(e => e.PluginPrefix == "Softwareschmiede.VisualStudioCode").IsEnabled.Should().BeTrue();
        var gespeicherterStatus = await _pluginActivationService.IsPluginEnabledAsync("Softwareschmiede.VisualStudioCode");
        gespeicherterStatus.Should().BeTrue();
    }

    /// <summary>SpeichernAsync persistiert geänderte IDE-Plugin-Aktivierungen zusammen mit SCM-/KI-Plugins in derselben Speicher-Operation.</summary>
    [Fact]
    public async Task SpeichernAsync_PersistiertIdePluginAktivierung()
    {
        var visualStudio = CreateIdePlugin("Visual Studio", "Softwareschmiede.VisualStudio");
        var vsCode = CreateIdePlugin("Visual Studio Code", "Softwareschmiede.VisualStudioCode");
        _pluginManagerMock.Setup(m => m.GetIdePlugins()).Returns([visualStudio, vsCode]);
        var sut = CreateSut();
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.DevelopmentEnvironmentPlugins.Single(e => e.PluginPrefix == "Softwareschmiede.VisualStudioCode").IsEnabled = false;
        await ((AsyncRelayCommand)sut.SpeichernCommand).ExecuteAsync();

        var gespeicherterStatus = await _pluginActivationService.IsPluginEnabledAsync("Softwareschmiede.VisualStudioCode");
        gespeicherterStatus.Should().BeFalse();
    }
}
