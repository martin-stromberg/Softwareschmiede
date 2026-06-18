using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.App.Services;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.App.ViewModels;

/// <summary>Unit-Tests für SettingsViewModel.</summary>
public sealed class SettingsViewModelTests : IDisposable
{
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiededDbContext _db;
    private readonly AppEinstellungService _einstellungService;
    private readonly ArbeitsverzeichnisSettingsService _arbeitsverzeichnisService;
    private readonly DarkModeService _darkModeService;
    private readonly Mock<IPluginManager> _pluginManagerMock;
    private readonly InMemoryCredentialStoreForSettings _credentialStore;
    private readonly PluginSettingsService _pluginSettingsService;

    public SettingsViewModelTests()
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
        _credentialStore = new InMemoryCredentialStoreForSettings();
        _pluginSettingsService = new PluginSettingsService(_credentialStore, NullLogger<PluginSettingsService>.Instance);
    }

    public void Dispose() => _db.Dispose();

    private SettingsViewModel CreateSut() =>
        new(
            _einstellungService,
            _arbeitsverzeichnisService,
            _darkModeService,
            _pluginManagerMock.Object,
            _pluginSettingsService,
            NullLogger<SettingsViewModel>.Instance);

    private static Mock<IGitPlugin> CreateScmPluginMock(string pluginName, IReadOnlyList<PluginSettingGroup>? groups = null)
    {
        var mock = new Mock<IGitPlugin>();
        mock.Setup(p => p.PluginName).Returns(pluginName);
        mock.Setup(p => p.PluginPrefix).Returns(pluginName.ToLowerInvariant());
        mock.Setup(p => p.PluginType).Returns(PluginType.SourceCodeManagement);
        mock.Setup(p => p.GetSettingGroups()).Returns(groups ?? []);
        return mock;
    }

    private static Mock<IKiPlugin> CreateKiPluginMock(string pluginName, IReadOnlyList<PluginSettingGroup>? groups = null)
    {
        var mock = new Mock<IKiPlugin>();
        mock.Setup(p => p.PluginName).Returns(pluginName);
        mock.Setup(p => p.PluginPrefix).Returns(pluginName.ToLowerInvariant());
        mock.Setup(p => p.PluginType).Returns(PluginType.DevelopmentAutomation);
        mock.Setup(p => p.GetSettingGroups()).Returns(groups ?? []);
        return mock;
    }

    /// <summary>ScmPluginSelectedCommand lädt Setting-Groups des ausgewählten SCM-Plugins und füllt SelectedScmPluginSettings.</summary>
    [Fact]
    public void ScmPluginSelectedCommand_LaeadtSettingsGroups_FuerAusgewaehltesPlugin()
    {
        var field = new PluginSettingField("token", "Token", PluginSettingFieldType.Secret);
        var group = new PluginSettingGroup("Authentifizierung", [field]);
        var plugin = CreateScmPluginMock("GitHub", [group]).Object;
        _pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([plugin]);
        _pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([]);
        var sut = CreateSut();

        sut.ScmPluginSelectedCommand.Execute(plugin);

        sut.SelectedScmPluginSettings.Should().HaveCount(1);
        sut.SelectedScmPluginSettings[0].GroupName.Should().Be("Authentifizierung");
        sut.SelectedScmPluginSettings[0].Entries.Should().HaveCount(1);
        sut.SelectedScmPluginSettings[0].Entries[0].Field.Key.Should().Be("token");
    }

    /// <summary>ScmPluginSelectedCommand lädt alle Felder verschiedener Typen korrekt.</summary>
    [Fact]
    public void ScmPluginSelectedCommand_WithMultipleFields_LoadsAllValues()
    {
        var fields = new PluginSettingField[]
        {
            new("url", "URL", PluginSettingFieldType.Url),
            new("port", "Port", PluginSettingFieldType.Integer),
            new("aktiv", "Aktiv", PluginSettingFieldType.Boolean),
        };
        var group = new PluginSettingGroup("Verbindung", fields);
        var plugin = CreateScmPluginMock("GitLab", [group]).Object;
        _pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([plugin]);
        _pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([]);
        var sut = CreateSut();

        sut.ScmPluginSelectedCommand.Execute(plugin);

        sut.SelectedScmPluginSettings[0].Entries.Should().HaveCount(3);
        sut.SelectedScmPluginSettings[0].Entries[0].FieldType.Should().Be(PluginSettingFieldType.Url);
        sut.SelectedScmPluginSettings[0].Entries[1].FieldType.Should().Be(PluginSettingFieldType.Integer);
        sut.SelectedScmPluginSettings[0].Entries[2].FieldType.Should().Be(PluginSettingFieldType.Boolean);
    }

    /// <summary>KiPluginSelectedCommand lädt Setting-Groups des ausgewählten KI-Plugins und füllt SelectedKiPluginSettings.</summary>
    [Fact]
    public void KiPluginSelectedCommand_LaeadtSettingsGroups_FuerAusgewaehltesPlugin()
    {
        var field = new PluginSettingField("model", "Modell", PluginSettingFieldType.Text);
        var group = new PluginSettingGroup("Modell-Konfiguration", [field]);
        var plugin = CreateKiPluginMock("Claude", [group]).Object;
        _pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([]);
        _pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([plugin]);
        var sut = CreateSut();

        sut.KiPluginSelectedCommand.Execute(plugin);

        sut.SelectedKiPluginSettings.Should().HaveCount(1);
        sut.SelectedKiPluginSettings[0].GroupName.Should().Be("Modell-Konfiguration");
        sut.SelectedKiPluginSettings[0].Entries[0].Field.Key.Should().Be("model");
    }

    /// <summary>LadenAsync lädt Default-Plugins und initialisiert Settings für das Default-SCM-Plugin.</summary>
    [Fact]
    public async Task LadenAsync_LaedtDefaultPlugine_UndInitialeSettings()
    {
        var field = new PluginSettingField("token", "Token", PluginSettingFieldType.Secret);
        var group = new PluginSettingGroup("Auth", [field]);
        var scmPlugin = CreateScmPluginMock("GitHub", [group]).Object;
        var kiPlugin = CreateKiPluginMock("Claude").Object;
        _pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([scmPlugin]);
        _pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([kiPlugin]);
        await _einstellungService.SetSettingAsync(AppEinstellungService.DefaultScmPluginKey, "GitHub");
        await _einstellungService.SetSettingAsync(AppEinstellungService.DefaultKiPluginKey, "Claude");
        var sut = CreateSut();

        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.DefaultScmPlugin.Should().Be(scmPlugin);
        sut.DefaultKiPlugin.Should().Be("Claude");
        sut.SelectedScmPluginSettings.Should().HaveCount(1);
        sut.SelectedScmPluginSettings[0].GroupName.Should().Be("Auth");
    }

    /// <summary>LadenAsync verwendet erstes Plugin als Fallback, wenn der gespeicherte Name nicht existiert.</summary>
    [Fact]
    public async Task LadenAsync_VerwendetErstesPlugin_WennGespeicherterNameNichtExistiert()
    {
        var scmPlugin = CreateScmPluginMock("GitHub").Object;
        _pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([scmPlugin]);
        _pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([]);
        await _einstellungService.SetSettingAsync(AppEinstellungService.DefaultScmPluginKey, "NichtExistierendesPlugin");
        var sut = CreateSut();

        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.DefaultScmPlugin.Should().Be(scmPlugin);
    }

    /// <summary>SpeichernAsync speichert DefaultScmPlugin Name und alle SCM-Einstellungswerte.</summary>
    [Fact]
    public async Task SpeichernAsync_SpeichertDefaultPlugine_Und_EinstellungswerteFuerScm()
    {
        var field = new PluginSettingField("api_key", "API Key", PluginSettingFieldType.Secret);
        var group = new PluginSettingGroup("Auth", [field]);
        var scmPlugin = CreateScmPluginMock("GitHub", [group]).Object;
        _pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([scmPlugin]);
        _pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([]);
        var sut = CreateSut();
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.SelectedScmPluginSettings[0].Entries[0].Value = "mein-token";
        await ((AsyncRelayCommand)sut.SpeichernCommand).ExecuteAsync();

        var gespeicherterWert = _pluginSettingsService.GetValue(scmPlugin, field);
        gespeicherterWert.Should().Be("mein-token");

        var gespeichertesPlugin = await _einstellungService.GetSettingAsync(AppEinstellungService.DefaultScmPluginKey);
        gespeichertesPlugin.Should().Be("GitHub");
    }

    /// <summary>SpeichernAsync speichert DefaultKiPlugin Name und alle KI-Einstellungswerte.</summary>
    [Fact]
    public async Task SpeichernAsync_SpeichertDefaultPlugine_Und_EinstellungswerteFuerKi()
    {
        var field = new PluginSettingField("model", "Modell", PluginSettingFieldType.Text);
        var group = new PluginSettingGroup("Konfiguration", [field]);
        var kiPlugin = CreateKiPluginMock("Claude", [group]).Object;
        _pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([]);
        _pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([kiPlugin]);
        await _einstellungService.SetSettingAsync(AppEinstellungService.DefaultKiPluginKey, "Claude");
        var sut = CreateSut();
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.SelectedKiPluginSettings[0].Entries[0].Value = "claude-opus";
        await ((AsyncRelayCommand)sut.SpeichernCommand).ExecuteAsync();

        var gespeicherterWert = _pluginSettingsService.GetValue(kiPlugin, field);
        gespeicherterWert.Should().Be("claude-opus");
    }

    /// <summary>SpeichernAsync zeigt Fehlermeldung wenn Pflichtfeld leer ist und speichert nicht.</summary>
    [Fact]
    public async Task SpeichernAsync_ValidierungFehlgeschlagen_ZeigtFehlerMeldung()
    {
        var field = new PluginSettingField("token", "Token", PluginSettingFieldType.Secret, IsRequired: true);
        var group = new PluginSettingGroup("Auth", [field]);
        var scmPlugin = CreateScmPluginMock("GitHub", [group]).Object;
        _pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([scmPlugin]);
        _pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([]);
        var sut = CreateSut();
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.SelectedScmPluginSettings[0].Entries[0].Value = string.Empty;
        await ((AsyncRelayCommand)sut.SpeichernCommand).ExecuteAsync();

        sut.FehlerMeldung.Should().NotBeNullOrEmpty();
        sut.ErfolgsMeldung.Should().BeNull();
        var gespeicherterWert = _pluginSettingsService.GetValue(scmPlugin, field);
        gespeicherterWert.Should().BeNull();
    }

    /// <summary>SpeichernAsync konvertiert Boolean-Werte korrekt zu "true"/"false" Strings.</summary>
    [Fact]
    public async Task SpeichernAsync_BooleanFelder_KonvertiertCorrect()
    {
        var field = new PluginSettingField("debug", "Debug", PluginSettingFieldType.Boolean);
        var group = new PluginSettingGroup("Entwicklung", [field]);
        var scmPlugin = CreateScmPluginMock("GitHub", [group]).Object;
        _pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([scmPlugin]);
        _pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([]);
        var sut = CreateSut();
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.SelectedScmPluginSettings[0].Entries[0].BoolValue = true;
        await ((AsyncRelayCommand)sut.SpeichernCommand).ExecuteAsync();

        var gespeicherterWert = _pluginSettingsService.GetValue(scmPlugin, field);
        gespeicherterWert.Should().Be("true");
    }
}

/// <summary>In-Memory Credential Store für SettingsViewModel-Tests.</summary>
internal sealed class InMemoryCredentialStoreForSettings : ICredentialStore
{
    private readonly Dictionary<string, string> _store = new();

    public string? GetCredential(string key) => _store.TryGetValue(key, out var v) ? v : null;
    public void SetCredential(string key, string value) => _store[key] = value;
    public void DeleteCredential(string key) => _store.Remove(key);
}
