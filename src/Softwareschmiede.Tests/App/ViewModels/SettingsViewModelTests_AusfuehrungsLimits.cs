using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.App.Services;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.App.Views;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Abstractions;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.App.ViewModels;

/// <summary>Unit-Tests für die Ausführungsbegrenzung in SettingsViewModel (Issue 410, Schritt 2):
/// globales Limit-Feld und das hostseitige Parallel-Limit-Feld der KI-Plugins.</summary>
public sealed class SettingsViewModelTests_AusfuehrungsLimits : IDisposable
{
    private const string KiPrefix = "Softwareschmiede.TestKi";

    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiededDbContext _db;
    private readonly AppEinstellungService _einstellungService;
    private readonly ArbeitsverzeichnisSettingsService _arbeitsverzeichnisService;
    private readonly DarkModeService _darkModeService;
    private readonly Mock<IPluginManager> _pluginManagerMock;
    private readonly PluginActivationService _pluginActivationService;
    private readonly InMemoryCredentialStoreForSettings _credentialStore;
    private readonly PluginSettingsService _pluginSettingsService;
    private readonly PromptVorlagenService _promptVorlagenService;

    /// <summary>SettingsViewModelTests_AusfuehrungsLimits.</summary>
    public SettingsViewModelTests_AusfuehrungsLimits()
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
        _pluginManagerMock.Setup(m => m.GetIdePlugins()).Returns([]);
        _pluginActivationService = new PluginActivationService(_einstellungService, _pluginManagerMock.Object, NullLogger<PluginActivationService>.Instance);
        _credentialStore = new InMemoryCredentialStoreForSettings();
        _pluginSettingsService = new PluginSettingsService(_credentialStore, NullLogger<PluginSettingsService>.Instance);
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
            new ProjektService(_db, NullLogger<ProjektService>.Instance),
            NullLogger<SettingsViewModel>.Instance,
            new Mock<IDialogService>().Object,
            new Mock<IServiceProvider>().Object);

    private static IKiPlugin CreateKiPlugin(IReadOnlyList<PluginSettingGroup>? groups = null)
    {
        var mock = new Mock<IKiPlugin>();
        mock.Setup(p => p.PluginName).Returns("TestKI");
        mock.Setup(p => p.PluginPrefix).Returns(KiPrefix);
        mock.Setup(p => p.PluginType).Returns(PluginType.DevelopmentAutomation);
        mock.Setup(p => p.GetSettingGroups()).Returns(groups ?? []);
        return mock.Object;
    }

    /// <summary>Ohne gespeicherten Wert ist das globale Limit-Feld leer (unbegrenzt).</summary>
    [Fact]
    public async Task LadenAsync_GlobalesLimitUngesetzt_FeldLeer()
    {
        var sut = CreateSut();

        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.MaxParalleleAusfuehrungen.Should().BeEmpty();
    }

    /// <summary>Gespeicherter gültiger Wert wird ins globale Feld geladen.</summary>
    [Fact]
    public async Task LadenAsync_GlobalesLimitGespeichert_FeldGefuellt()
    {
        await _einstellungService.SetIntSettingAsync(ScmEventExecutionLimitService.GlobalLimitKey, 3, TestContext.Current.CancellationToken);
        var sut = CreateSut();

        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.MaxParalleleAusfuehrungen.Should().Be("3");
    }

    /// <summary>Ungültige gespeicherte Werte werden als unbegrenzt (leer) dargestellt.</summary>
    [Theory]
    [InlineData("0")]
    [InlineData("-2")]
    [InlineData("abc")]
    public async Task LadenAsync_GlobalesLimitUngueltig_FeldLeer(string wert)
    {
        await _einstellungService.SetSettingAsync(ScmEventExecutionLimitService.GlobalLimitKey, wert, TestContext.Current.CancellationToken);
        var sut = CreateSut();

        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.MaxParalleleAusfuehrungen.Should().BeEmpty();
    }

    /// <summary>Speichern legt das globale Limit als AppEinstellung ab.</summary>
    [Fact]
    public async Task SpeichernAsync_GlobalesLimit_Persistiert()
    {
        var sut = CreateSut();
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();
        sut.MaxParalleleAusfuehrungen = "2";

        await ((AsyncRelayCommand)sut.SpeichernCommand).ExecuteAsync();

        sut.FehlerMeldung.Should().BeNull();
        (await _einstellungService.GetSettingAsync(ScmEventExecutionLimitService.GlobalLimitKey, TestContext.Current.CancellationToken))
            .Should().Be("2");
    }

    /// <summary>Leeres globales Feld speichert null (unbegrenzt).</summary>
    [Fact]
    public async Task SpeichernAsync_GlobalesLimitLeer_PersistiertNull()
    {
        await _einstellungService.SetIntSettingAsync(ScmEventExecutionLimitService.GlobalLimitKey, 4, TestContext.Current.CancellationToken);
        var sut = CreateSut();
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();
        sut.MaxParalleleAusfuehrungen = "  ";

        await ((AsyncRelayCommand)sut.SpeichernCommand).ExecuteAsync();

        sut.FehlerMeldung.Should().BeNull();
        (await _einstellungService.GetSettingAsync(ScmEventExecutionLimitService.GlobalLimitKey, TestContext.Current.CancellationToken))
            .Should().BeNull();
    }

    /// <summary>Ungültige globale Werte werden mit deutscher Fehlermeldung abgelehnt und nicht persistiert.</summary>
    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("abc")]
    [InlineData("1,5")]
    public async Task SpeichernAsync_GlobalesLimitUngueltig_Validierungsfehler(string wert)
    {
        var sut = CreateSut();
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();
        sut.MaxParalleleAusfuehrungen = wert;

        await ((AsyncRelayCommand)sut.SpeichernCommand).ExecuteAsync();

        sut.FehlerMeldung.Should().Contain("Limit paralleler CLI-Ausführungen");
        (await _einstellungService.GetSettingAsync(ScmEventExecutionLimitService.GlobalLimitKey, TestContext.Current.CancellationToken))
            .Should().BeNull();
    }

    /// <summary>Für KI-Plugins enthält das Einstellungspanel die Host-Gruppe „Ausführungsbegrenzung“.</summary>
    [Fact]
    public async Task KiPluginSelected_ZeigtParallelLimitFeld()
    {
        var kiPlugin = CreateKiPlugin();
        _pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([kiPlugin]);
        var sut = CreateSut();
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.KiPluginSelectedCommand.Execute(kiPlugin);

        var gruppe = sut.SelectedPluginSettings.SingleOrDefault(g => g.GroupName == "Ausführungsbegrenzung");
        gruppe.Should().NotBeNull();
        gruppe!.Entries.Should().ContainSingle();
        gruppe.Entries[0].Field.Key.Should().Be("ParallelLimit");
        gruppe.Entries[0].Field.FieldType.Should().Be(PluginSettingFieldType.Integer);
        gruppe.Entries[0].Value.Should().BeEmpty();
    }

    /// <summary>Für SCM-Plugins gibt es keine Limit-Gruppe.</summary>
    [Fact]
    public async Task ScmPluginSelected_KeineLimitGruppe()
    {
        var scmPlugin = new Mock<IGitPlugin>();
        scmPlugin.Setup(p => p.PluginName).Returns("GitHub");
        scmPlugin.Setup(p => p.PluginPrefix).Returns("Softwareschmiede.GitHub");
        scmPlugin.Setup(p => p.PluginType).Returns(PluginType.SourceCodeManagement);
        scmPlugin.Setup(p => p.GetSettingGroups()).Returns([]);
        _pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([scmPlugin.Object]);
        var sut = CreateSut();
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.ScmPluginSelectedCommand.Execute(scmPlugin.Object);

        sut.SelectedPluginSettings.Should().NotContain(g => g.GroupName == "Ausführungsbegrenzung");
    }

    /// <summary>Gespeicherter Plugin-Limit-Wert erscheint im Feld und wird per AppEinstellung persistiert (nicht Credential Store).</summary>
    [Fact]
    public async Task SpeichernAsync_PluginLimit_PersistiertInAppEinstellung()
    {
        var kiPlugin = CreateKiPlugin();
        _pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([kiPlugin]);
        var sut = CreateSut();
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();
        sut.KiPluginSelectedCommand.Execute(kiPlugin);
        var eintrag = sut.SelectedPluginSettings.Single(g => g.GroupName == "Ausführungsbegrenzung").Entries[0];

        eintrag.Value = "2";
        await ((AsyncRelayCommand)sut.SpeichernCommand).ExecuteAsync();

        sut.FehlerMeldung.Should().BeNull();
        (await _einstellungService.GetSettingAsync(ScmEventExecutionLimitService.PluginLimitKey(KiPrefix), TestContext.Current.CancellationToken))
            .Should().Be("2");
        _credentialStore.GetCredential($"{KiPrefix}.ParallelLimit").Should().BeNull();

        var neu = CreateSut();
        await ((AsyncRelayCommand)neu.LadenCommand).ExecuteAsync();
        neu.KiPluginSelectedCommand.Execute(kiPlugin);
        neu.SelectedPluginSettings.Single(g => g.GroupName == "Ausführungsbegrenzung").Entries[0].Value
            .Should().Be("2");
    }

    /// <summary>Leeres Plugin-Limit-Feld entfernt den Grenzwert (null persistiert).</summary>
    [Fact]
    public async Task SpeichernAsync_PluginLimitLeer_PersistiertNull()
    {
        var kiPlugin = CreateKiPlugin();
        _pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([kiPlugin]);
        await _einstellungService.SetIntSettingAsync(ScmEventExecutionLimitService.PluginLimitKey(KiPrefix), 3, TestContext.Current.CancellationToken);
        var sut = CreateSut();
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();
        sut.KiPluginSelectedCommand.Execute(kiPlugin);
        var eintrag = sut.SelectedPluginSettings.Single(g => g.GroupName == "Ausführungsbegrenzung").Entries[0];
        eintrag.Value.Should().Be("3");

        eintrag.Value = "";
        await ((AsyncRelayCommand)sut.SpeichernCommand).ExecuteAsync();

        sut.FehlerMeldung.Should().BeNull();
        (await _einstellungService.GetSettingAsync(ScmEventExecutionLimitService.PluginLimitKey(KiPrefix), TestContext.Current.CancellationToken))
            .Should().BeNull();
    }

    /// <summary>Ungültige Plugin-Limit-Werte (unter 1) werden abgelehnt.</summary>
    [Theory]
    [InlineData("0")]
    [InlineData("-4")]
    [InlineData("text")]
    public async Task SpeichernAsync_PluginLimitUngueltig_Validierungsfehler(string wert)
    {
        var kiPlugin = CreateKiPlugin();
        _pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([kiPlugin]);
        var sut = CreateSut();
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();
        sut.KiPluginSelectedCommand.Execute(kiPlugin);
        var eintrag = sut.SelectedPluginSettings.Single(g => g.GroupName == "Ausführungsbegrenzung").Entries[0];

        eintrag.Value = wert;
        await ((AsyncRelayCommand)sut.SpeichernCommand).ExecuteAsync();

        sut.FehlerMeldung.Should().NotBeNull();
        (await _einstellungService.GetSettingAsync(ScmEventExecutionLimitService.PluginLimitKey(KiPrefix), TestContext.Current.CancellationToken))
            .Should().BeNull();
    }
}
