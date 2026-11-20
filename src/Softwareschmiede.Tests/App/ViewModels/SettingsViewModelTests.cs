using System.Diagnostics;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.App.Services;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.App.Views;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Application.Services.Updates;
using Softwareschmiede.Domain.Abstractions;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.App.ViewModels;

/// <summary>Unit-Tests für SettingsViewModel.</summary>
public sealed class SettingsViewModelTests : IDisposable
{
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiedeDbContext _db;
    private readonly AppSettingService _settingService;
    private readonly WorkingDirectorySettingsService _workingDirectoryService;
    private readonly DarkModeService _darkModeService;
    private readonly Mock<IPluginManager> _pluginManagerMock;
    private readonly PluginActivationService _pluginActivationService;
    private readonly InMemoryCredentialStoreForSettings _credentialStore;
    private readonly PluginSettingsService _pluginSettingsService;
    private readonly PromptTemplateService _promptTemplatesService;

    /// <summary>SettingsViewModelTests.</summary>
    public SettingsViewModelTests()
    {
        _db = TestDbContextFactory.Create();
        _settingService = new AppSettingService(_db, NullLogger<AppSettingService>.Instance);
        _workingDirectoryService = new WorkingDirectorySettingsService(_db, NullLogger<WorkingDirectorySettingsService>.Instance);

        var scopeMock = new Mock<IServiceScope>();
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        var serviceProviderMock = new Mock<IServiceProvider>();

        scopeFactoryMock.Setup(f => f.CreateScope()).Returns(scopeMock.Object);
        scopeMock.Setup(s => s.ServiceProvider).Returns(serviceProviderMock.Object);
        serviceProviderMock.Setup(p => p.GetService(typeof(AppSettingService))).Returns(_settingService);

        _darkModeService = new DarkModeService(scopeFactoryMock.Object, NullLogger<DarkModeService>.Instance);
        _pluginManagerMock = new Mock<IPluginManager>();
        _pluginManagerMock.Setup(m => m.GetIdePlugins()).Returns([]);
        _pluginActivationService = new PluginActivationService(_settingService, _pluginManagerMock.Object, NullLogger<PluginActivationService>.Instance);
        _credentialStore = new InMemoryCredentialStoreForSettings();
        _pluginSettingsService = new PluginSettingsService(_credentialStore, NullLogger<PluginSettingsService>.Instance);
        _promptTemplatesService = new PromptTemplateService(_db, NullLogger<PromptTemplateService>.Instance);
    }

    /// <summary>Dispose.</summary>
    public void Dispose() => _db.Dispose();

    private SettingsViewModel CreateSut() =>
        new(
            _settingService,
            _workingDirectoryService,
            _darkModeService,
            _pluginManagerMock.Object,
            _pluginActivationService,
            _pluginSettingsService,
            _promptTemplatesService,
            new ProjectService(_db, NullLogger<ProjectService>.Instance),
            NullLogger<SettingsViewModel>.Instance,
            new Mock<IDialogService>().Object,
            new Mock<IServiceProvider>().Object);

    private static Mock<IGitPlugin> CreateScmPluginMock(string pluginName, IReadOnlyList<PluginSettingGroup>? groups = null)
    {
        var mock = new Mock<IGitPlugin>();
        mock.Setup(p => p.PluginName).Returns(pluginName);
        mock.Setup(p => p.PluginPrefix).Returns(pluginName.ToLowerInvariant());
        mock.Setup(p => p.PluginType).Returns(PluginType.SourceCodeManagement);
        mock.Setup(p => p.GetSettingGroups()).Returns(groups ?? []);
        return mock;
    }

    private static Mock<IAiPlugin> CreateKiPluginMock(
        string pluginName,
        IReadOnlyList<PluginSettingGroup>? groups = null,
        string? pluginPrefix = null)
    {
        var mock = new Mock<IAiPlugin>();
        mock.Setup(p => p.PluginName).Returns(pluginName);
        mock.Setup(p => p.PluginPrefix).Returns(pluginPrefix ?? pluginName.ToLowerInvariant());
        mock.Setup(p => p.PluginType).Returns(PluginType.DevelopmentAutomation);
        mock.Setup(p => p.GetSettingGroups()).Returns(groups ?? []);
        return mock;
    }

    /// <summary>ScmPluginSelectedCommand lädt Setting-Groups des ausgewählten SCM-Plugins und füllt SelectedPluginSettings.</summary>
    [Fact]
    public async Task ScmPluginSelectedCommand_LaeadtSettingsGroups_FuerAusgewaehltesPlugin()
    {
        var field = new PluginSettingField("token", "Token", PluginSettingFieldType.Secret);
        var group = new PluginSettingGroup("Authentifizierung", [field]);
        var plugin = CreateScmPluginMock("GitHub", [group]).Object;
        _pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([plugin]);
        _pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([]);
        var sut = CreateSut();
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.ScmPluginSelectedCommand.Execute(plugin);

        sut.SelectedPluginSettings.Should().HaveCount(1);
        sut.SelectedPluginSettings[0].GroupName.Should().Be("Authentifizierung");
        sut.SelectedPluginSettings[0].Entries.Should().HaveCount(1);
        sut.SelectedPluginSettings[0].Entries[0].Field.Key.Should().Be("token");
    }

    /// <summary>ScmPluginSelectedCommand lädt alle Felder verschiedener Typen korrekt.</summary>
    [Fact]
    public async Task ScmPluginSelectedCommand_WithMultipleFields_LoadsAllValues()
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
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.ScmPluginSelectedCommand.Execute(plugin);

        sut.SelectedPluginSettings[0].Entries.Should().HaveCount(3);
        sut.SelectedPluginSettings[0].Entries[0].FieldType.Should().Be(PluginSettingFieldType.Url);
        sut.SelectedPluginSettings[0].Entries[1].FieldType.Should().Be(PluginSettingFieldType.Integer);
        sut.SelectedPluginSettings[0].Entries[2].FieldType.Should().Be(PluginSettingFieldType.Boolean);
    }

    /// <summary>KiPluginSelectedCommand lädt Setting-Groups des ausgewählten KI-Plugins und füllt SelectedPluginSettings.</summary>
    [Fact]
    public async Task KiPluginSelectedCommand_LaeadtSettingsGroups_FuerAusgewaehltesPlugin()
    {
        var field = new PluginSettingField("model", "Modell", PluginSettingFieldType.Text);
        var group = new PluginSettingGroup("Modell-Konfiguration", [field]);
        var plugin = CreateKiPluginMock("Claude", [group]).Object;
        _pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([]);
        _pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([plugin]);
        var sut = CreateSut();
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.AiPluginSelectedCommand.Execute(plugin);

        // Neben den Plugin-Gruppen enthält die Auswahl die hostseitige Gruppe
        // "Ausführungsbegrenzung" mit dem ParallelLimit-Feld (Issue 410).
        sut.SelectedPluginSettings.Should().HaveCount(2);
        sut.SelectedPluginSettings[0].GroupName.Should().Be("Modell-Konfiguration");
        sut.SelectedPluginSettings[0].Entries[0].Field.Key.Should().Be("model");
        sut.SelectedPluginSettings[1].GroupName.Should().Be("Ausführungsbegrenzung");
        sut.SelectedPluginSettings[1].Entries[0].Field.Key.Should().Be("ParallelLimit");
    }

    /// <summary>LadenAsync lädt die gespeicherten Default-Plugins. Die Settings selbst werden erst nach Auswahl eines Eintrags im Plugins-Register geladen (SelectedPluginSettings).</summary>
    [Fact]
    public async Task LadenAsync_LaedtDefaultPlugine()
    {
        var field = new PluginSettingField("token", "Token", PluginSettingFieldType.Secret);
        var group = new PluginSettingGroup("Auth", [field]);
        var scmPlugin = CreateScmPluginMock("GitHub", [group]).Object;
        var aiPlugin = CreateKiPluginMock("Claude").Object;
        _pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([scmPlugin]);
        _pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([aiPlugin]);
        await _settingService.SetSettingAsync(AppSettingService.DefaultScmPluginKey, "GitHub", TestContext.Current.CancellationToken);
        await _settingService.SetSettingAsync(AppSettingService.DefaultAiPluginKey, "Claude", TestContext.Current.CancellationToken);
        var sut = CreateSut();

        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.DefaultScmPlugin.Should().Be(scmPlugin);
        sut.DefaultAiPlugin.Should().Be("Claude");
    }

    /// <summary>LadenAsync verwendet erstes Plugin als Fallback, wenn der gespeicherte Name nicht existiert.</summary>
    [Fact]
    public async Task LadenAsync_VerwendetErstesPlugin_WennGespeicherterNameNichtExistiert()
    {
        var scmPlugin = CreateScmPluginMock("GitHub").Object;
        _pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([scmPlugin]);
        _pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([]);
        await _settingService.SetSettingAsync(AppSettingService.DefaultScmPluginKey, "NichtExistierendesPlugin", TestContext.Current.CancellationToken);
        var sut = CreateSut();

        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

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
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();
        sut.ScmPluginSelectedCommand.Execute(scmPlugin);

        sut.SelectedPluginSettings[0].Entries[0].Value = "mein-token";
        await ((AsyncRelayCommand)sut.SaveCommand).ExecuteAsync();

        var gespeicherterWert = _pluginSettingsService.GetValue(scmPlugin, field);
        gespeicherterWert.Should().Be("mein-token");

        var gespeichertesPlugin = await _settingService.GetSettingAsync(AppSettingService.DefaultScmPluginKey, TestContext.Current.CancellationToken);
        gespeichertesPlugin.Should().Be("GitHub");
    }

    /// <summary>SpeichernAsync speichert DefaultAiPlugin Name und alle KI-Einstellungswerte.</summary>
    [Fact]
    public async Task SpeichernAsync_SpeichertDefaultPlugine_Und_EinstellungswerteFuerKi()
    {
        var field = new PluginSettingField("model", "Modell", PluginSettingFieldType.Text);
        var group = new PluginSettingGroup("Konfiguration", [field]);
        var aiPlugin = CreateKiPluginMock("Claude", [group]).Object;
        _pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([]);
        _pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([aiPlugin]);
        await _settingService.SetSettingAsync(AppSettingService.DefaultAiPluginKey, "Claude", TestContext.Current.CancellationToken);
        var sut = CreateSut();
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();
        sut.AiPluginSelectedCommand.Execute(aiPlugin);

        sut.SelectedPluginSettings[0].Entries[0].Value = "claude-opus";
        await ((AsyncRelayCommand)sut.SaveCommand).ExecuteAsync();

        var gespeicherterWert = _pluginSettingsService.GetValue(aiPlugin, field);
        gespeicherterWert.Should().Be("claude-opus");
    }

    /// <summary>Codex-CommandLineParameters übernehmen keinen automatischen Default als Anwenderwert.</summary>
    [Fact]
    public async Task SpeichernAsync_PersistiertKeinenCodexCommandLineDefault_WennKeinAnwenderwertExistiert()
    {
        var field = new PluginSettingField(
            "CommandLineParameters",
            "Kommandozeilenparameter",
            PluginSettingFieldType.CommandLineParameters,
            DefaultValue: "--auto-default");
        var group = new PluginSettingGroup("CLI-Konfiguration", [field]);
        var aiPlugin = CreateKiPluginMock("Codex CLI", [group], "Softwareschmiede.Codex").Object;
        _pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([]);
        _pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([aiPlugin]);
        await _settingService.SetSettingAsync(AppSettingService.DefaultAiPluginKey, "Codex CLI", TestContext.Current.CancellationToken);
        var sut = CreateSut();

        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();
        sut.AiPluginSelectedCommand.Execute(aiPlugin);

        sut.SelectedPluginSettings[0].Entries[0].Value.Should().BeEmpty();

        await ((AsyncRelayCommand)sut.SaveCommand).ExecuteAsync();

        _credentialStore.GetCredential("Softwareschmiede.Codex.CommandLineParameters").Should().BeEmpty();
    }

    /// <summary>Anwenderdefinierte Codex-CommandLineParameters bleiben nach Speichern und Neuladen unverändert.</summary>
    [Fact]
    public async Task SpeichernAsync_ErhaeltGeaenderteCodexCommandLineParameters()
    {
        var field = new PluginSettingField(
            "CommandLineParameters",
            "Kommandozeilenparameter",
            PluginSettingFieldType.CommandLineParameters,
            DefaultValue: "--auto-default");
        var group = new PluginSettingGroup("CLI-Konfiguration", [field]);
        var aiPlugin = CreateKiPluginMock("Codex CLI", [group], "Softwareschmiede.Codex").Object;
        _pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([]);
        _pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([aiPlugin]);
        await _settingService.SetSettingAsync(AppSettingService.DefaultAiPluginKey, "Codex CLI", TestContext.Current.CancellationToken);
        var sut = CreateSut();

        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();
        sut.AiPluginSelectedCommand.Execute(aiPlugin);
        sut.SelectedPluginSettings[0].Entries[0].Value = "--user-choice --model custom";
        await ((AsyncRelayCommand)sut.SaveCommand).ExecuteAsync();
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();
        sut.AiPluginSelectedCommand.Execute(aiPlugin);

        _credentialStore.GetCredential("Softwareschmiede.Codex.CommandLineParameters")
            .Should().Be("--user-choice --model custom");
        sut.SelectedPluginSettings[0].Entries[0].Value.Should().Be("--user-choice --model custom");
    }

    /// <summary>Entfernte Codex-CommandLineParameters bleiben leer und fallen nicht auf Defaults zurück.</summary>
    [Fact]
    public async Task SpeichernAsync_ErhaeltLeereCodexCommandLineParameters_NachEntfernung()
    {
        var field = new PluginSettingField(
            "CommandLineParameters",
            "Kommandozeilenparameter",
            PluginSettingFieldType.CommandLineParameters,
            DefaultValue: "--auto-default");
        var group = new PluginSettingGroup("CLI-Konfiguration", [field]);
        var aiPlugin = CreateKiPluginMock("Codex CLI", [group], "Softwareschmiede.Codex").Object;
        _credentialStore.SetCredential("Softwareschmiede.Codex.CommandLineParameters", "--auto-old");
        _pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([]);
        _pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([aiPlugin]);
        await _settingService.SetSettingAsync(AppSettingService.DefaultAiPluginKey, "Codex CLI", TestContext.Current.CancellationToken);
        var sut = CreateSut();

        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();
        sut.AiPluginSelectedCommand.Execute(aiPlugin);
        sut.SelectedPluginSettings[0].Entries[0].Value = string.Empty;
        await ((AsyncRelayCommand)sut.SaveCommand).ExecuteAsync();
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();
        sut.AiPluginSelectedCommand.Execute(aiPlugin);

        _credentialStore.GetCredential("Softwareschmiede.Codex.CommandLineParameters").Should().BeEmpty();
        sut.SelectedPluginSettings[0].Entries[0].Value.Should().BeEmpty();
    }

    /// <summary>Der Hilfebutton verwendet das aktuell ausgewählte CLI-Plugin und nicht das gespeicherte Default-KI-Plugin.</summary>
    [Fact]
    public async Task HilfeButtonResolver_VerwendetAusgewaehltesCliPlugin_StattDefaultKiPlugin()
    {
        var defaultPlugin = new TestCliPlugin("Codex CLI", "Softwareschmiede.Codex");
        var selectedPlugin = new TestCliPlugin("Devin CLI", "Softwareschmiede.Devin");
        _pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([]);
        _pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([defaultPlugin, selectedPlugin]);
        await _settingService.SetSettingAsync(AppSettingService.DefaultAiPluginKey, "Codex CLI", TestContext.Current.CancellationToken);
        var sut = CreateSut();
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.PluginSelectedCommand.Execute(sut.DevelopmentAutomationPlugins.Single(entry => entry.Plugin == selectedPlugin));

        SettingsView.ResolveCliPluginForHelp(sut).Should().BeSameAs(selectedPlugin);
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
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();
        sut.ScmPluginSelectedCommand.Execute(scmPlugin);

        sut.SelectedPluginSettings[0].Entries[0].Value = string.Empty;
        await ((AsyncRelayCommand)sut.SaveCommand).ExecuteAsync();

        sut.ErrorMessage.Should().NotBeNullOrEmpty();
        sut.SuccessMessage.Should().BeNull();
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
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();
        sut.ScmPluginSelectedCommand.Execute(scmPlugin);

        sut.SelectedPluginSettings[0].Entries[0].BoolValue = true;
        await ((AsyncRelayCommand)sut.SaveCommand).ExecuteAsync();

        var gespeicherterWert = _pluginSettingsService.GetValue(scmPlugin, field);
        gespeicherterWert.Should().Be("true");
    }

    /// <summary>LadenAsync lädt Promptvorlagen in die editierbare Collection.</summary>
    [Fact]
    public async Task LadenAsync_LaedtPromptVorlagen()
    {
        await _promptTemplatesService.CreateAsync("Vorlage", "Text", TestContext.Current.CancellationToken);
        _pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([]);
        _pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([]);
        var sut = CreateSut();

        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.PromptTemplates.Should().ContainSingle();
        sut.PromptTemplates[0].Name.Should().Be("Vorlage");
        sut.PromptTemplates[0].PromptText.Should().Be("Text");
    }

    /// <summary>SpeichernAsync persistiert neue und geänderte Promptvorlagen.</summary>
    [Fact]
    public async Task SpeichernAsync_SpeichertPromptVorlagen()
    {
        await _promptTemplatesService.CreateAsync("Alt", "Alter Text", TestContext.Current.CancellationToken);
        _pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([]);
        _pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([]);
        var sut = CreateSut();
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.PromptTemplates[0].Name = "Neu";
        sut.PromptTemplates[0].PromptText = "Neuer Text";
        sut.AddPromptTemplateCommand.Execute(null);
        sut.PromptTemplates[1].Name = "Zweite";
        sut.PromptTemplates[1].PromptText = "Zweiter Text";
        await ((AsyncRelayCommand)sut.SaveCommand).ExecuteAsync();

        var templates = await _promptTemplatesService.GetAllAsync(TestContext.Current.CancellationToken);
        templates.Should().HaveCount(2);
        templates.Should().Contain(v => v.Name == "Neu" && v.PromptText == "Neuer Text");
        templates.Should().Contain(v => v.Name == "Zweite" && v.PromptText == "Zweiter Text");
    }

    /// <summary>SpeichernAsync validiert leere Promptvorlagenfelder.</summary>
    [Fact]
    public async Task SpeichernAsync_ValidiertLeerenPrompttext()
    {
        _pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([]);
        _pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([]);
        var sut = CreateSut();
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.AddPromptTemplateCommand.Execute(null);
        sut.PromptTemplates[0].Name = "Vorlage";
        sut.PromptTemplates[0].PromptText = string.Empty;
        await ((AsyncRelayCommand)sut.SaveCommand).ExecuteAsync();

        sut.ErrorMessage.Should().Contain("Prompttext");
        (await _promptTemplatesService.GetAllAsync(TestContext.Current.CancellationToken)).Should().BeEmpty();
    }

    /// <summary>LadenAsync befüllt die Aktivierungs-Collections mit dem aktuellen Status je Plugin.</summary>
    [Fact]
    public async Task Laden_BefuelltAktivierungsCollections_MitStatus()
    {
        var scmPlugin = CreateScmPluginMock("GitHub").Object;
        var aiPlugin = CreateKiPluginMock("Claude").Object;
        _pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([scmPlugin]);
        _pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([aiPlugin]);
        await _pluginActivationService.SetPluginEnabledAsync("github", false, TestContext.Current.CancellationToken);
        var sut = CreateSut();

        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.SourceCodeManagementPlugins.Should().ContainSingle();
        sut.SourceCodeManagementPlugins[0].PluginPrefix.Should().Be("github");
        sut.SourceCodeManagementPlugins[0].IsEnabled.Should().BeFalse();
        sut.DevelopmentAutomationPlugins.Should().ContainSingle();
        sut.DevelopmentAutomationPlugins[0].PluginPrefix.Should().Be("claude");
        sut.DevelopmentAutomationPlugins[0].IsEnabled.Should().BeTrue();
    }

    /// <summary>Umschalten eines Aktivierungsstatus und Speichern persistiert den Status über PluginActivationService.</summary>
    [Fact]
    public async Task TogglePlugin_UndSpeichern_PersistiertStatus()
    {
        var scmPlugin1 = CreateScmPluginMock("GitHub").Object;
        var scmPlugin2 = CreateScmPluginMock("GitLab").Object;
        _pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([scmPlugin1, scmPlugin2]);
        _pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([]);
        var sut = CreateSut();
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.SourceCodeManagementPlugins.Single(p => p.PluginPrefix == "gitlab").IsEnabled = false;
        await ((AsyncRelayCommand)sut.SaveCommand).ExecuteAsync();

        var gespeichertesStatus = await _pluginActivationService.IsPluginEnabledAsync("gitlab", TestContext.Current.CancellationToken);
        gespeichertesStatus.Should().BeFalse();
    }

    /// <summary>SpeichernAsync verhindert das Deaktivieren des letzten aktiven Plugins einer Kategorie.</summary>
    [Fact]
    public async Task Speichern_VerhindertDeaktivierenDesLetztenPlugins()
    {
        var scmPlugin = CreateScmPluginMock("GitHub").Object;
        _pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([scmPlugin]);
        _pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([]);
        var sut = CreateSut();
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.SourceCodeManagementPlugins[0].IsEnabled = false;
        await ((AsyncRelayCommand)sut.SaveCommand).ExecuteAsync();

        sut.ErrorMessage.Should().Contain("Mindestens ein");
        var gespeichertesStatus = await _pluginActivationService.IsPluginEnabledAsync("github", TestContext.Current.CancellationToken);
        gespeichertesStatus.Should().BeTrue();
    }

    /// <summary>LadenCommand fällt bei fehlenden oder unbekannten gespeicherten Update-Werten auf die Defaults zurück.</summary>
    [Fact]
    public async Task UpdateSettings_DefaultsAndInvalidValues()
    {
        _pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([]);
        _pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([]);
        var sut = CreateSut();

        // Fehlende Werte -> Defaults (Nur prüfen / Produktivversionen)
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.SelectedUpdateMode.Should().Be(UpdateModeOption.ForMode(UpdateMode.CheckOnly));
        sut.SelectedUpdatePath.Should().Be(UpdatePathOption.ForPath(UpdatePathChannelResolver.StableVersionsPath));
        sut.DerivedUpdateChannel.Should().Be(UpdatePathText.ChannelStable);

        // Unbekannte gespeicherte Werte -> Defaults
        await _settingService.SetSettingAsync(AppSettingService.UpdateModeKey, "unbekannt", TestContext.Current.CancellationToken);
        await _settingService.SetSettingAsync(AppSettingService.UpdatePathKey, "kein-pfad", TestContext.Current.CancellationToken);
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.SelectedUpdateMode.Should().Be(UpdateModeOption.ForMode(UpdateMode.CheckOnly));
        sut.SelectedUpdatePath.Should().Be(UpdatePathOption.ForPath(UpdatePathChannelResolver.StableVersionsPath));

        // Eine fehlende Auswahl (kein Eintrag aus UpdateModusOptionen) wird nicht gespeichert.
        UpdateSettings? published = null;
        sut.UpdateSettingsSaved += (_, s) => published = s;
        sut.SelectedUpdateMode = null;
        await ((AsyncRelayCommand)sut.SaveCommand).ExecuteAsync();

        published.Should().BeNull("eine fehlende Modus-Auswahl darf nicht gespeichert werden");
        sut.ErrorMessage.Should().NotBeNullOrEmpty();

        // Auch eine fehlende Pfad-Auswahl wird nicht gespeichert.
        sut.SelectedUpdateMode = UpdateModeOption.ForMode(UpdateMode.CheckOnly);
        sut.SelectedUpdatePath = null;
        await ((AsyncRelayCommand)sut.SaveCommand).ExecuteAsync();

        published.Should().BeNull("eine fehlende Pfad-Auswahl darf nicht gespeichert werden");
        sut.ErrorMessage.Should().NotBeNullOrEmpty();
        (await _settingService.GetUpdateSettingsAsync(TestContext.Current.CancellationToken))
            .Should().Be(new UpdateSettings(UpdateMode.CheckOnly, UpdatePathChannelResolver.StableVersionsPath));
    }

    /// <summary>Alle drei Update-Modi in Kombination mit beiden Update-Pfaden werden gespeichert und nach erneutem Laden wiederhergestellt.</summary>
    [Theory]
    [InlineData(UpdateMode.Off, UpdatePathChannelResolver.StableVersionsPath)]
    [InlineData(UpdateMode.Off, UpdatePathChannelResolver.PrereleaseVersionsPath)]
    [InlineData(UpdateMode.CheckOnly, UpdatePathChannelResolver.StableVersionsPath)]
    [InlineData(UpdateMode.CheckOnly, UpdatePathChannelResolver.PrereleaseVersionsPath)]
    [InlineData(UpdateMode.CheckAndRunAtStartup, UpdatePathChannelResolver.StableVersionsPath)]
    [InlineData(UpdateMode.CheckAndRunAtStartup, UpdatePathChannelResolver.PrereleaseVersionsPath)]
    public async Task UpdateSettings_SaveLoadAllValues(UpdateMode mode, string updatePath)
    {
        _pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([]);
        _pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([]);
        var sut = CreateSut();
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        UpdateSettings? published = null;
        sut.UpdateSettingsSaved += (_, s) => published = s;

        sut.SelectedUpdateMode = UpdateModeOption.ForMode(mode);
        sut.SelectedUpdatePath = UpdatePathOption.ForPath(updatePath);
        var erwarteterKanal = updatePath == UpdatePathChannelResolver.PrereleaseVersionsPath
            ? UpdatePathText.ChannelReleaseCandidate
            : UpdatePathText.ChannelStable;
        sut.DerivedUpdateChannel.Should().Be(erwarteterKanal);
        await ((AsyncRelayCommand)sut.SaveCommand).ExecuteAsync();

        sut.ErrorMessage.Should().BeNull();
        published.Should().Be(new UpdateSettings(mode, updatePath),
            "das Event muss den tatsächlich gespeicherten Snapshot liefern");

        var gespeichert = await _settingService.GetUpdateSettingsAsync(TestContext.Current.CancellationToken);
        gespeichert.Should().Be(new UpdateSettings(mode, updatePath));

        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();
        sut.SelectedUpdatePath.Should().Be(UpdatePathOption.ForPath(updatePath));
        sut.SelectedUpdateMode.Should().Be(UpdateModeOption.ForMode(mode));
    }

    /// <summary>Das UpdateSettingsSaved-Event wird direkt nach dem Update-Speichern ausgelöst – auch wenn ein späterer Speicherschritt fehlschlägt.</summary>
    [Fact]
    public async Task UpdateSettings_EventFiresBeforeLaterSettingsFailure()
    {
        _pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([]);
        _pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([]);
        var sut = CreateSut();
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        var eventCount = 0;
        UpdateSettings? published = null;
        sut.UpdateSettingsSaved += (_, s) =>
        {
            eventCount++;
            published = s;
        };

        sut.SelectedUpdateMode = UpdateModeOption.ForMode(UpdateMode.Off);
        // Relativer Pfad -> der spätere Arbeitsverzeichnis-Speicherschritt schlägt fehl.
        sut.WorkingDirectory = "relativer-pfad-ohne-root";
        await ((AsyncRelayCommand)sut.SaveCommand).ExecuteAsync();

        eventCount.Should().Be(1, "das Event muss trotz des späteren Fehlers ausgelöst worden sein");
        published.Should().Be(new UpdateSettings(UpdateMode.Off, UpdatePathChannelResolver.StableVersionsPath));
        sut.ErrorMessage.Should().NotBeNullOrEmpty();
        (await _settingService.GetUpdateSettingsAsync(TestContext.Current.CancellationToken))
            .Should().Be(new UpdateSettings(UpdateMode.Off, UpdatePathChannelResolver.StableVersionsPath),
                "die Update--Einstellungen müssen trotz späterem Fehler persistiert sein");
    }

    /// <summary>VerwerfenAsync lädt neu, ohne das UpdateSettingsSaved-Event auszulösen oder Werte zu schreiben.</summary>
    [Fact]
    public async Task UpdateSettings_DiscardDoesNotPublish()
    {
        _pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([]);
        _pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([]);
        var sut = CreateSut();
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        var eventCount = 0;
        sut.UpdateSettingsSaved += (_, _) => eventCount++;

        // Ungespeicherte Änderungen -> Verwerfen lädt neu, ohne zu publizieren
        sut.SelectedUpdateMode = UpdateModeOption.ForMode(UpdateMode.Off);
        sut.SelectedUpdatePath = UpdatePathOption.ForPath(UpdatePathChannelResolver.PrereleaseVersionsPath);
        await ((AsyncRelayCommand)sut.DiscardCommand).ExecuteAsync();

        eventCount.Should().Be(0, "Verwerfen darf kein UpdateSettingsSaved-Ereignis auslösen");
        sut.SelectedUpdateMode.Should().Be(UpdateModeOption.ForMode(UpdateMode.CheckOnly));
        sut.SelectedUpdatePath.Should().Be(UpdatePathOption.ForPath(UpdatePathChannelResolver.StableVersionsPath));
        (await _settingService.GetUpdateSettingsAsync(TestContext.Current.CancellationToken))
            .Should().Be(new UpdateSettings(UpdateMode.CheckOnly, UpdatePathChannelResolver.StableVersionsPath),
                "Verwerfen darf keine Update-Werte schreiben");
    }

    /// <summary>LadenCommand füllt die SCM--Einstellungen mit den Defaults: Überwachung aktiv, Standard-Trigger.</summary>
    [Fact]
    public async Task LadenCommand_LoadsScmMonitoringDefaults()
    {
        _pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([]);
        _pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([]);
        var sut = CreateSut();

        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.IsScmMonitoringEnabled.Should().BeTrue();
        sut.ScmTriggerKeyword.Should().Be(ScmEventValidationService.DefaultTriggerKeyword);
    }

    /// <summary>SpeichernCommand persistiert den Überwachungs-Schalter und das getrimmte Trigger-Keyword.</summary>
    [Fact]
    public async Task SpeichernCommand_PersistsScmMonitoringSettings()
    {
        _pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([]);
        _pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([]);
        var sut = CreateSut();
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.IsScmMonitoringEnabled = false;
        sut.ScmTriggerKeyword = "  !Bearbeite  ";
        await ((AsyncRelayCommand)sut.SaveCommand).ExecuteAsync();

        sut.ErrorMessage.Should().BeNull();
        (await _settingService.GetBoolSettingAsync(AppSettingService.ScmMonitoringEnabledKey, TestContext.Current.CancellationToken))
            .Should().BeFalse();
        (await _settingService.GetSettingAsync(AppSettingService.ScmTriggerKeywordKey, TestContext.Current.CancellationToken))
            .Should().Be("!Bearbeite");

        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();
        sut.IsScmMonitoringEnabled.Should().BeFalse();
        sut.ScmTriggerKeyword.Should().Be("!Bearbeite");
    }

    /// <summary>Ein leeres Trigger-Keyword wird abgelehnt und nicht gespeichert.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SpeichernCommand_RejectsEmptyScmTriggerKeyword(string keyword)
    {
        _pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([]);
        _pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([]);
        var sut = CreateSut();
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.ScmTriggerKeyword = keyword;
        await ((AsyncRelayCommand)sut.SaveCommand).ExecuteAsync();

        sut.ErrorMessage.Should().Contain("Trigger-Keyword");
        sut.SuccessMessage.Should().BeNull();
        (await _settingService.GetSettingAsync(AppSettingService.ScmTriggerKeywordKey, TestContext.Current.CancellationToken))
            .Should().BeNull("ein ungültiges Keyword darf nicht persistiert werden");
    }

    /// <summary>Ein leeres Trigger-Keyword blockiert das Speichern nicht, solange das Feld nicht
    /// wirksam ist — bei deaktivierter Ereignisüberwachung ist der Bereich ausgeblendet.</summary>
    [Fact]
    public async Task SpeichernCommand_EmptyTriggerKeyword_MonitoringDisabled_Saves()
    {
        _pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([]);
        _pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([]);
        var sut = CreateSut();
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.IsScmMonitoringEnabled = false;
        sut.ScmTriggerKeyword = "   ";
        await ((AsyncRelayCommand)sut.SaveCommand).ExecuteAsync();

        sut.ErrorMessage.Should().BeNull();
        sut.SuccessMessage.Should().NotBeNull();
    }

    /// <summary>Im SeparatesKonto-Modus ist das Trigger-Keyword-Feld ausgeblendet; ein leerer
    /// Wert blockiert das Speichern daher nicht.</summary>
    [Fact]
    public async Task SpeichernCommand_EmptyTriggerKeyword_SeparatesKonto_Saves()
    {
        var plugin = CreateGitHubPluginMock().Object;
        _pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([plugin]);
        _pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([]);
        _credentialStore.SetCredential("Softwareschmiede.GitHub.AgentToken", "bot-token");
        var sut = CreateSut();
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.SelectedAccountMode = ScmAccountModeOption.ForMode(ScmAccountMode.SeparateAccount);
        sut.ScmTriggerKeyword = "   ";
        await ((AsyncRelayCommand)sut.SaveCommand).ExecuteAsync();

        sut.ErrorMessage.Should().BeNull();
        (await _settingService.GetScmAccountModeAsync(TestContext.Current.CancellationToken)).Should().Be(ScmAccountMode.SeparateAccount);
    }

    /// <summary>Eine ungültige Wartezeit blockiert das Speichern nicht, solange die
    /// Abschlussüberwachung deaktiviert ist — das Feld ist dann deaktiviert und nicht wirksam;
    /// der gespeicherte Wert bleibt unberührt.</summary>
    [Fact]
    public async Task SpeichernCommand_InvalidWait_TaskCompletionDisabled_Saves()
    {
        _pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([]);
        _pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([]);
        var sut = CreateSut();
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.IsScmTaskCompletionEnabled = false;
        sut.ScmTaskCompletionWaitMinutes = "keine Zahl";
        await ((AsyncRelayCommand)sut.SaveCommand).ExecuteAsync();

        sut.ErrorMessage.Should().BeNull();
        sut.SuccessMessage.Should().NotBeNull();
        (await _settingService.GetScmTaskCompletionWaitMinutesAsync(TestContext.Current.CancellationToken))
            .Should().Be(AppSettingService.ScmTaskCompletionDefaultWaitMinutes,
                "die nicht wirksame Wartezeit darf nicht überschrieben werden");
    }

    /// <summary>„Verwerfen“ stellt die gespeicherten Auswahlen wieder her und benachrichtigt dabei
    /// auch die abgeleiteten Anzeige-Properties (Sichtbarkeit, Enabled-Zustand, Panel-Status) —
    /// die UI bleibt nach dem Wiederherstellen konsistent.</summary>
    [Fact]
    public async Task VerwerfenCommand_NotifiesDependentAnzeigeProperties()
    {
        _pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([]);
        _pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([]);
        var sut = CreateSut();
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();
        // Probehalber vom gespeicherten Standard abweichende Werte setzen.
        sut.SelectedBackmergeMode = ScmBackmergeModeOption.ForMode(ScmBackmergeMode.Global);
        sut.SelectedAccountMode = ScmAccountModeOption.ForMode(ScmAccountMode.SeparateAccount);
        sut.IsScmMonitoringEnabled = false;
        sut.IsScmTaskCompletionEnabled = false;

        var changes = new List<string?>();
        sut.PropertyChanged += (_, e) => changes.Add(e.PropertyName);
        await ((AsyncRelayCommand)sut.DiscardCommand).ExecuteAsync();

        sut.SelectedBackmergeMode?.Mode.Should().Be(ScmBackmergeMode.Disabled);
        sut.SelectedAccountMode?.Mode.Should().Be(ScmAccountMode.UserAccount);
        sut.IsProcessRunsFieldEnabled.Should().BeFalse();
        sut.ProcessRunsStatusText.Should().Be("deaktiviert");
        sut.IsTriggerKeywordVisible.Should().BeTrue();
        changes.Should().Contain(nameof(sut.IsProcessRunsFieldEnabled));
        changes.Should().Contain(nameof(sut.ProcessRunsStatusText));
        changes.Should().Contain(nameof(sut.IsTriggerKeywordVisible));
        changes.Should().Contain(nameof(sut.IsPromptAppendVisible));
        changes.Should().Contain(nameof(sut.IsMonitoringModeEnabled));
        changes.Should().Contain(nameof(sut.WorkItemsExecuteStatusText));
        changes.Should().Contain(nameof(sut.IsTaskCompletionFieldEnabled));
        changes.Should().Contain(nameof(sut.WorkItemsExitStatusText));
    }

    /// <summary>LadenCommand lädt den Backmerge-Modus; ohne gespeicherten Eintrag gilt „Deaktiviert“.</summary>
    [Fact]
    public async Task LadenCommand_LoadsBackmergeModusDefaults()
    {
        _pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([]);
        _pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([]);
        var sut = CreateSut();

        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.SelectedBackmergeMode.Should().Be(ScmBackmergeModeOption.ForMode(ScmBackmergeMode.Disabled));
        sut.BackmergeModeOptions.Should().HaveCount(3);
    }

    /// <summary>SpeichernCommand persistiert den gewählten Backmerge-Modus und lädt ihn beim Neuladen zurück.</summary>
    [Fact]
    public async Task SpeichernCommand_PersistsBackmergeModus()
    {
        _pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([]);
        _pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([]);
        var sut = CreateSut();
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.SelectedBackmergeMode = ScmBackmergeModeOption.ForMode(ScmBackmergeMode.ProjectScoped);
        await ((AsyncRelayCommand)sut.SaveCommand).ExecuteAsync();

        sut.ErrorMessage.Should().BeNull();
        (await _settingService.GetScmBackmergeModeAsync(TestContext.Current.CancellationToken)).Should().Be(ScmBackmergeMode.ProjectScoped);

        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();
        sut.SelectedBackmergeMode.Should().Be(ScmBackmergeModeOption.ForMode(ScmBackmergeMode.ProjectScoped));
    }

    /// <summary>LadenCommand füllt die fünf SCM-Prompt-Anhänge mit leeren Feldern, wenn nichts gespeichert ist.</summary>
    [Fact]
    public async Task LadenCommand_LoadsScmPromptAppendixDefaults()
    {
        _pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([]);
        _pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([]);
        var sut = CreateSut();

        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.ScmPromptAppendixGeneral.Should().BeEmpty();
        sut.ScmPromptAppendixIssue.Should().BeEmpty();
        sut.ScmPromptAppendixIssueComment.Should().BeEmpty();
        sut.ScmPromptAppendixPullRequest.Should().BeEmpty();
        sut.ScmPromptAppendixPullRequestComment.Should().BeEmpty();
    }

    /// <summary>SpeichernCommand persistiert alle fünf SCM-Prompt-Anhänge (mehrzeilig möglich) und
    /// lädt sie nach einem Neuladen unverändert zurück.</summary>
    [Fact]
    public async Task SpeichernCommand_PersistsAndReloadsScmPromptAppendices()
    {
        _pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([]);
        _pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([]);
        var sut = CreateSut();
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.ScmPromptAppendixGeneral = "Immer auf Deutsch antworten.";
        sut.ScmPromptAppendixIssue = "Issue:\nReproduktion prüfen.";
        sut.ScmPromptAppendixIssueComment = "Issue-Kommentar-Anhang";
        sut.ScmPromptAppendixPullRequest = "PR-Anhang";
        sut.ScmPromptAppendixPullRequestComment = "PR-Kommentar-Anhang";
        await ((AsyncRelayCommand)sut.SaveCommand).ExecuteAsync();

        sut.ErrorMessage.Should().BeNull();
        (await _settingService.GetSettingAsync(AppSettingService.ScmPromptAppendGeneralKey, TestContext.Current.CancellationToken))
            .Should().Be("Immer auf Deutsch antworten.");
        (await _settingService.GetSettingAsync(AppSettingService.ScmPromptAppendIssueKey, TestContext.Current.CancellationToken))
            .Should().Be("Issue:\nReproduktion prüfen.");
        (await _settingService.GetSettingAsync(AppSettingService.ScmPromptAppendIssueCommentKey, TestContext.Current.CancellationToken))
            .Should().Be("Issue-Kommentar-Anhang");
        (await _settingService.GetSettingAsync(AppSettingService.ScmPromptAppendPullRequestKey, TestContext.Current.CancellationToken))
            .Should().Be("PR-Anhang");
        (await _settingService.GetSettingAsync(AppSettingService.ScmPromptAppendPullRequestCommentKey, TestContext.Current.CancellationToken))
            .Should().Be("PR-Kommentar-Anhang");

        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();
        sut.ScmPromptAppendixGeneral.Should().Be("Immer auf Deutsch antworten.");
        sut.ScmPromptAppendixIssue.Should().Be("Issue:\nReproduktion prüfen.");
        sut.ScmPromptAppendixIssueComment.Should().Be("Issue-Kommentar-Anhang");
        sut.ScmPromptAppendixPullRequest.Should().Be("PR-Anhang");
        sut.ScmPromptAppendixPullRequestComment.Should().Be("PR-Kommentar-Anhang");
    }

    /// <summary>Leere oder nur aus Leerraum bestehende Anhänge werden nicht gespeichert (leer = nichts anhängen);
    /// ein geleertes Feld entfernt den wirksamen Wert wieder.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SpeichernCommand_BlankScmPromptAppendix_StoresNoValue(string blank)
    {
        _pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([]);
        _pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([]);
        await _settingService.SetSettingAsync(AppSettingService.ScmPromptAppendIssueKey, "vorhanden", TestContext.Current.CancellationToken);
        var sut = CreateSut();
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();
        sut.ScmPromptAppendixIssue.Should().Be("vorhanden");

        sut.ScmPromptAppendixIssue = blank;
        await ((AsyncRelayCommand)sut.SaveCommand).ExecuteAsync();

        sut.ErrorMessage.Should().BeNull();
        sut.SuccessMessage.Should().NotBeNull();
        (await _settingService.GetSettingAsync(AppSettingService.ScmPromptAppendIssueKey, TestContext.Current.CancellationToken))
            .Should().BeNull("ein leerer Anhang hängt nichts an und wird als leerer Wert persistiert");
    }

    private static Mock<IGitPlugin> CreateGitHubPluginMock()
    {
        var mock = new Mock<IGitPlugin>();
        mock.Setup(p => p.PluginName).Returns("GitHub");
        mock.Setup(p => p.PluginPrefix).Returns("Softwareschmiede.GitHub");
        mock.Setup(p => p.PluginType).Returns(PluginType.SourceCodeManagement);
        mock.Setup(p => p.GetSettingGroups()).Returns(
        [
            new PluginSettingGroup("Authentifizierung",
            [
                new PluginSettingField("Token", "Personal Access Token", PluginSettingFieldType.Secret, IsRequired: true),
                new PluginSettingField("AgentToken", "Personal Access Token des separaten Kontos", PluginSettingFieldType.Secret)
            ])
        ]);
        return mock;
    }

    /// <summary>LadenCommand lädt den Konto-Modus; ohne Eintrag gilt „Benutzerkonto“.</summary>
    [Fact]
    public async Task LadenCommand_LoadsKontoModusDefaults()
    {
        _pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([]);
        _pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([]);
        var sut = CreateSut();

        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.SelectedAccountMode.Should().Be(ScmAccountModeOption.ForMode(ScmAccountMode.UserAccount));
        sut.AccountModeOptions.Should().HaveCount(2);
    }

    /// <summary>SpeichernCommand persistiert den gewählten Konto-Modus und lädt ihn beim Neuladen zurück.</summary>
    [Fact]
    public async Task SpeichernCommand_PersistsKontoModus()
    {
        var plugin = CreateGitHubPluginMock().Object;
        _pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([plugin]);
        _pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([]);
        _credentialStore.SetCredential("Softwareschmiede.GitHub.AgentToken", "bot-token");
        var sut = CreateSut();
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.SelectedAccountMode = ScmAccountModeOption.ForMode(ScmAccountMode.SeparateAccount);
        await ((AsyncRelayCommand)sut.SaveCommand).ExecuteAsync();

        sut.ErrorMessage.Should().BeNull();
        (await _settingService.GetScmAccountModeAsync(TestContext.Current.CancellationToken)).Should().Be(ScmAccountMode.SeparateAccount);

        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();
        sut.SelectedAccountMode.Should().Be(ScmAccountModeOption.ForMode(ScmAccountMode.SeparateAccount));
    }

    /// <summary>Eine fehlende Konto-Modus-Auswahl wird nicht gespeichert.</summary>
    [Fact]
    public async Task SpeichernCommand_RejectsMissingKontoModus()
    {
        _pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([]);
        _pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([]);
        var sut = CreateSut();
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.SelectedAccountMode = null;
        await ((AsyncRelayCommand)sut.SaveCommand).ExecuteAsync();

        sut.ErrorMessage.Should().NotBeNullOrEmpty();
        (await _settingService.GetScmAccountModeAsync(TestContext.Current.CancellationToken)).Should().Be(ScmAccountMode.UserAccount);
    }

    /// <summary>SeparatesKonto ohne AgentToken wird abgelehnt — der Modus bleibt unpersistiert.</summary>
    [Fact]
    public async Task SpeichernCommand_SeparatesKontoWithoutAgentToken_ShowsFehlerMeldung()
    {
        var plugin = CreateGitHubPluginMock().Object;
        _pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([plugin]);
        _pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([]);
        var sut = CreateSut();
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.SelectedAccountMode = ScmAccountModeOption.ForMode(ScmAccountMode.SeparateAccount);
        await ((AsyncRelayCommand)sut.SaveCommand).ExecuteAsync();

        sut.ErrorMessage.Should().Contain("separaten Kontos").And.Contain("Authentifizierung");
        sut.SuccessMessage.Should().BeNull();
        (await _settingService.GetScmAccountModeAsync(TestContext.Current.CancellationToken)).Should().Be(ScmAccountMode.UserAccount);
    }

    /// <summary>Ein gerade im Formular eingetragener AgentToken-Wert zählt bereits bei der Pflichtprüfung —
    /// Token eintragen und Modus wählen gelingt in einem Speicherschritt.</summary>
    [Fact]
    public async Task SpeichernCommand_SeparatesKonto_FormValueSatisfiesAgentTokenRequirement()
    {
        var plugin = CreateGitHubPluginMock().Object;
        _pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([plugin]);
        _pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([]);
        var sut = CreateSut();
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();
        sut.ScmPluginSelectedCommand.Execute(plugin);
        sut.SelectedPluginSettings.SelectMany(g => g.Entries).Single(e => e.Field.Key == "Token").Value = "user-token";
        sut.SelectedPluginSettings.SelectMany(g => g.Entries).Single(e => e.Field.Key == "AgentToken").Value = "bot-token";

        sut.SelectedAccountMode = ScmAccountModeOption.ForMode(ScmAccountMode.SeparateAccount);
        await ((AsyncRelayCommand)sut.SaveCommand).ExecuteAsync();

        sut.ErrorMessage.Should().BeNull();
        (await _settingService.GetScmAccountModeAsync(TestContext.Current.CancellationToken)).Should().Be(ScmAccountMode.SeparateAccount);
        _credentialStore.GetCredential("Softwareschmiede.GitHub.AgentToken").Should().Be("bot-token");
    }

    /// <summary>LadenCommand lädt den Prüfmodus der Abschlussüberwachung; ohne Eintrag gilt „Ereignisgesteuert“.</summary>
    [Fact]
    public async Task LadenCommand_LoadsPruefmodusDefaults()
    {
        _pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([]);
        _pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([]);
        var sut = CreateSut();

        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.SelectedScmTaskCompletionCheckmode.Should().Be(ScmTaskCompletionCheckModeOption.ForMode(ScmTaskCompletionCheckMode.EventDriven));
        sut.ScmTaskCompletionCheckmodeOptions.Should().HaveCount(2);
    }

    /// <summary>SpeichernCommand persistiert den Prüfmodus und lädt ihn beim Neuladen zurück.</summary>
    [Fact]
    public async Task SpeichernCommand_PersistsPruefmodus()
    {
        _pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([]);
        _pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([]);
        var sut = CreateSut();
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.SelectedScmTaskCompletionCheckmode = ScmTaskCompletionCheckModeOption.ForMode(ScmTaskCompletionCheckMode.AllWorkItems);
        await ((AsyncRelayCommand)sut.SaveCommand).ExecuteAsync();

        sut.ErrorMessage.Should().BeNull();
        (await _settingService.GetScmTaskCompletionCheckModeAsync(TestContext.Current.CancellationToken)).Should().Be(ScmTaskCompletionCheckMode.AllWorkItems);

        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();
        sut.SelectedScmTaskCompletionCheckmode.Should().Be(ScmTaskCompletionCheckModeOption.ForMode(ScmTaskCompletionCheckMode.AllWorkItems));
    }

    /// <summary>Eine fehlende Prüfmodus-Auswahl wird nicht gespeichert.</summary>
    [Fact]
    public async Task SpeichernCommand_RejectsMissingPruefmodus()
    {
        _pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([]);
        _pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([]);
        var sut = CreateSut();
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.SelectedScmTaskCompletionCheckmode = null;
        await ((AsyncRelayCommand)sut.SaveCommand).ExecuteAsync();

        sut.ErrorMessage.Should().NotBeNullOrEmpty();
        (await _settingService.GetScmTaskCompletionCheckModeAsync(TestContext.Current.CancellationToken)).Should().Be(ScmTaskCompletionCheckMode.EventDriven);
    }

    /// <summary>LadenCommand lädt die Backmerge-Optionen; ohne Eintrag sind beide deaktiviert.</summary>
    [Fact]
    public async Task LadenCommand_LoadsBackmergeOptionenDefaults()
    {
        _pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([]);
        _pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([]);
        var sut = CreateSut();

        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.IsScmBackmergeDraftActivation.Should().BeFalse();
        sut.IsScmBackmergeAdminMerge.Should().BeFalse();
    }

    /// <summary>SpeichernCommand persistiert beide Backmerge-Optionen unabhängig voneinander und lädt sie zurück.</summary>
    [Fact]
    public async Task SpeichernCommand_PersistsBackmergeOptionen()
    {
        _pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([]);
        _pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([]);
        var sut = CreateSut();
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.IsScmBackmergeDraftActivation = true;
        sut.IsScmBackmergeAdminMerge = true;
        await ((AsyncRelayCommand)sut.SaveCommand).ExecuteAsync();

        sut.ErrorMessage.Should().BeNull();
        (await _settingService.GetScmBackmergeDraftActivationAsync(TestContext.Current.CancellationToken)).Should().BeTrue();
        (await _settingService.GetScmBackmergeAdminMergeAsync(TestContext.Current.CancellationToken)).Should().BeTrue();

        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();
        sut.IsScmBackmergeDraftActivation.Should().BeTrue();
        sut.IsScmBackmergeAdminMerge.Should().BeTrue();
    }

    /// <summary>Der Überwachungsmodus ist nur bedienbar, Trigger-Keyword und Prompt-Anhänge nur sichtbar,
    /// solange die Ereignisüberwachung aktiviert ist.</summary>
    [Fact]
    public async Task AufgabenAusfuehren_AbleitungenFolgenDemSchalter()
    {
        _pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([]);
        _pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([]);
        var sut = CreateSut();
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.IsMonitoringModeEnabled.Should().BeTrue();
        sut.IsTriggerKeywordVisible.Should().BeTrue("Benutzerkonto ist der Standard-Modus");
        sut.IsPromptAppendVisible.Should().BeTrue();
        sut.WorkItemsExecuteStatusText.Should().Be("aktiviert");

        sut.SelectedAccountMode = ScmAccountModeOption.ForMode(ScmAccountMode.SeparateAccount);
        sut.IsTriggerKeywordVisible.Should().BeFalse("im SeparatesKonto gilt die Erwähnung statt des Keywords");
        sut.IsPromptAppendVisible.Should().BeTrue();

        sut.IsScmMonitoringEnabled = false;
        sut.IsMonitoringModeEnabled.Should().BeFalse();
        sut.IsTriggerKeywordVisible.Should().BeFalse();
        sut.IsPromptAppendVisible.Should().BeFalse();
        sut.WorkItemsExecuteStatusText.Should().Be("deaktiviert");
    }

    /// <summary>Prüfmodus und Wartezeit folgen dem Abschlussüberwachungs-Schalter; der Status zeigt den Schaltzustand.</summary>
    [Fact]
    public async Task AufgabenBeenden_AbleitungenFolgenDemSchalter()
    {
        _pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([]);
        _pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([]);
        var sut = CreateSut();
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.IsTaskCompletionFieldEnabled.Should().BeTrue();
        sut.WorkItemsExitStatusText.Should().Be("aktiviert");

        sut.IsScmTaskCompletionEnabled = false;
        sut.IsTaskCompletionFieldEnabled.Should().BeFalse();
        sut.WorkItemsExitStatusText.Should().Be("deaktiviert");
    }

    /// <summary>Die Backmerge-Optionen sind nur bedienbar, solange der Backmerge-Modus nicht „Deaktiviert“ ist.</summary>
    [Fact]
    public async Task Prozessablaeufe_AbleitungenFolgenDemBackmergeModus()
    {
        _pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([]);
        _pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([]);
        var sut = CreateSut();
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.IsProcessRunsFieldEnabled.Should().BeFalse("Standard ist Deaktiviert");
        sut.ProcessRunsStatusText.Should().Be("deaktiviert");

        sut.SelectedBackmergeMode = ScmBackmergeModeOption.ForMode(ScmBackmergeMode.Global);
        sut.IsProcessRunsFieldEnabled.Should().BeTrue();
        sut.ProcessRunsStatusText.Should().Be("aktiviert");

        sut.SelectedBackmergeMode = ScmBackmergeModeOption.ForMode(ScmBackmergeMode.ProjectScoped);
        sut.IsProcessRunsFieldEnabled.Should().BeTrue();
        sut.ProcessRunsStatusText.Should().Be("aktiviert");
    }
}

/// <summary>In-Memory Credential Store für SettingsViewModel-Tests.</summary>
internal sealed class InMemoryCredentialStoreForSettings : ICredentialStore
{
    private readonly Dictionary<string, string> _store = new();

    public string? GetCredential(string key) => _store.TryGetValue(key, out var v) ? v : null;
    /// <summary>SetCredential.</summary>
    public void SetCredential(string key, string value) => _store[key] = value;
    /// <summary>DeleteCredential.</summary>
    public void DeleteCredential(string key) => _store.Remove(key);
}

internal sealed class TestCliPlugin : CliAiPluginBase
{
    public TestCliPlugin(string pluginName, string pluginPrefix)
    {
        PluginName = pluginName;
        PluginPrefix = pluginPrefix;
        ProviderFilePrefix = pluginPrefix;
    }

    public override string ProviderFilePrefix { get; }
    public override string PluginName { get; }
    public override string PluginPrefix { get; }
    public override PluginType PluginType => PluginType.DevelopmentAutomation;
    public override IReadOnlyList<PluginSettingGroup> GetSettingGroups() => [];
    public override bool SupportsSessionContinuation() => false;
    public override Task<bool> CheckHealthAsync(CancellationToken ct = default) => Task.FromResult(true);

    protected override ProcessStartInfo BuildProcessStartInfo(string localRepoPath, string? parameters)
        => new("test") { WorkingDirectory = localRepoPath };
}
