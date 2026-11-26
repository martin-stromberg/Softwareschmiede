using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.App.Services;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.App.Views;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.App.ViewModels;

/// <summary>Unit-Tests für die Issue-Aufbereitung in SettingsViewModel (Issue 412, Schritt 6):
/// Funktionsschalter, Plugin-Auswahl mit Standard-Fallback, Repository-Auswahl und Kommentar-Schalter.</summary>
public sealed class SettingsViewModelTests_IssuePreparation : IDisposable
{
    private const string AiPrefix = "Softwareschmiede.TestKi";

    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiedeDbContext _db;
    private readonly AppSettingService _settingService;
    private readonly WorkingDirectorySettingsService _workingDirectoryService;
    private readonly DarkModeService _darkModeService;
    private readonly Mock<IPluginManager> _pluginManagerMock;
    private readonly PluginActivationService _pluginActivationService;
    private readonly PluginSettingsService _pluginSettingsService;
    private readonly PromptTemplateService _promptTemplatesService;

    /// <summary>SettingsViewModelTests_IssuePreparation.</summary>
    public SettingsViewModelTests_IssuePreparation()
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
        _pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([]);
        _pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([]);
        _pluginManagerMock.Setup(m => m.GetIdePlugins()).Returns([]);
        _pluginActivationService = new PluginActivationService(_settingService, _pluginManagerMock.Object, NullLogger<PluginActivationService>.Instance);
        _pluginSettingsService = new PluginSettingsService(new InMemoryCredentialStoreForSettings(), NullLogger<PluginSettingsService>.Instance);
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
            new Mock<IServiceProvider>().Object,
            new RemoteStatusConnectionInfo(),
            new InMemoryCredentialStoreForSettings());

    private static IAiPlugin CreateAiPlugin()
    {
        var mock = new Mock<IAiPlugin>();
        mock.Setup(p => p.PluginName).Returns("TestKI");
        mock.Setup(p => p.PluginPrefix).Returns(AiPrefix);
        mock.Setup(p => p.PluginType).Returns(PluginType.DevelopmentAutomation);
        mock.Setup(p => p.GetSettingGroups()).Returns([]);
        return mock.Object;
    }

    private async Task<GitRepository> AddRepositoryAsync(string name = "repo")
    {
        var project = new Project { Id = Guid.NewGuid(), Name = "P" };
        var repository = new GitRepository
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            RepositoryUrl = "https://github.com/owner/" + name,
            RepositoryName = name,
            PluginType = "test-scm",
            Active = true
        };
        _db.AddRange(project, repository);
        await _db.SaveChangesAsync();
        return repository;
    }

    /// <summary>Standardwerte: Funktion aus, Kommentar an, Standard-CLI als Plugin-Auswahl.</summary>
    [Fact]
    public async Task LoadAsync_Defaultvalue()
    {
        var sut = CreateSut();

        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.IsIssuePreparationEnabled.Should().BeFalse();
        sut.IsIssuePreparationCommentEnabled.Should().BeTrue();
        sut.SelectedIssuePreparationAiPlugin.Should().Be(IssuePreparationAiPluginOption.Default);
        sut.IssuePreparationAiPluginOptions.Should().ContainSingle(x => x.PluginPrefix == null);
        sut.IssuePreparationStatusText.Should().Be("deaktiviert");
        sut.IsIssuePreparationFieldEnabled.Should().BeFalse();
    }

    /// <summary>Die Plugin-Auswahl enthält den Standard-Eintrag plus jedes KI-Plugin mit Namen.</summary>
    [Fact]
    public async Task LoadAsync_PluginOptions_ContainsDefaultAndPlugins()
    {
        _pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([CreateAiPlugin()]);
        var sut = CreateSut();

        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.IssuePreparationAiPluginOptions.Should().HaveCount(2);
        sut.IssuePreparationAiPluginOptions[0].PluginPrefix.Should().BeNull();
        sut.IssuePreparationAiPluginOptions[1].PluginPrefix.Should().Be(AiPrefix);
        sut.IssuePreparationAiPluginOptions[1].Label.Should().Be("TestKI");
    }

    /// <summary>Gespeichertes Plugin-Prefix wird in der Auswahl vorgewählt.</summary>
    [Fact]
    public async Task LadenAsync_GespeichertesPlugin_Vorausgewählt()
    {
        _pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([CreateAiPlugin()]);
        await _settingService.SetSettingAsync(AppSettingService.ScmIssuePreparationPluginKey, AiPrefix, TestContext.Current.CancellationToken);
        var sut = CreateSut();

        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.SelectedIssuePreparationAiPlugin.Should().Be(sut.IssuePreparationAiPluginOptions[1]);
    }

    /// <summary>Speichern persistiert alle vier Settings (Schalter, Plugin, Repositories, Kommentar).</summary>
    [Fact]
    public async Task SaveAsync_PersistedAllSettings()
    {
        _pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([CreateAiPlugin()]);
        var repository = await AddRepositoryAsync();
        var sut = CreateSut();
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();
        sut.IsIssuePreparationEnabled = true;
        sut.IsIssuePreparationCommentEnabled = false;
        sut.SelectedIssuePreparationAiPlugin = sut.IssuePreparationAiPluginOptions[1];
        sut.IssuePreparationRepositories.Single().IsSelected = true;

        await ((AsyncRelayCommand)sut.SaveCommand).ExecuteAsync();

        sut.ErrorMessage.Should().BeNull();
        (await _settingService.GetSettingAsync(AppSettingService.ScmIssuePreparationEnabledKey, TestContext.Current.CancellationToken)).Should().Be("True");
        (await _settingService.GetSettingAsync(AppSettingService.ScmIssuePreparationCommentEnabledKey, TestContext.Current.CancellationToken)).Should().Be("False");
        (await _settingService.GetSettingAsync(AppSettingService.ScmIssuePreparationPluginKey, TestContext.Current.CancellationToken)).Should().Be(AiPrefix);
        var stored = await _db.GitRepositories.FindAsync(new object?[] { repository.Id }, TestContext.Current.CancellationToken);
        stored!.IssuePreparationEnabled.Should().BeTrue();
    }

    /// <summary>Der Standard-Eintrag persistiert null als Plugin-Prefix (Fallback-Kette).</summary>
    [Fact]
    public async Task SaveAsync_DefaultEntry_PersistedNull()
    {
        await _settingService.SetSettingAsync(AppSettingService.ScmIssuePreparationPluginKey, "alt.wert", TestContext.Current.CancellationToken);
        var sut = CreateSut();
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();
        // „alt.wert" ist kein installiertes Plugin → Standard-Auswahl.
        sut.SelectedIssuePreparationAiPlugin.Should().Be(IssuePreparationAiPluginOption.Default);

        await ((AsyncRelayCommand)sut.SaveCommand).ExecuteAsync();

        sut.ErrorMessage.Should().BeNull();
        (await _settingService.GetSettingAsync(AppSettingService.ScmIssuePreparationPluginKey, TestContext.Current.CancellationToken)).Should().BeNull();
    }

    /// <summary>Der Funktionsschalter steuert die Bedienbarkeit der Detail-Felder und den Header-Status.</summary>
    [Fact]
    public async Task Toggle_ControlsFieldEnabledAndStatus()
    {
        var sut = CreateSut();
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.IsIssuePreparationFieldEnabled.Should().BeFalse();
        sut.IssuePreparationStatusText.Should().Be("deaktiviert");

        sut.IsIssuePreparationEnabled = true;

        sut.IsIssuePreparationFieldEnabled.Should().BeTrue();
        sut.IssuePreparationStatusText.Should().Be("aktiviert");
    }

    /// <summary>Überwachte Repositories erscheinen benannt in der Auswahl; ihre Flags laden/speichern.</summary>
    [Fact]
    public async Task LoadAsync_Repositories_WithDisplaynameAndFlag()
    {
        var repository = await AddRepositoryAsync("mein-repo");
        await new ProjectService(_db, NullLogger<ProjectService>.Instance).SetIssuePreparationEnabledAsync(repository.Id, true, TestContext.Current.CancellationToken);
        var sut = CreateSut();

        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        var entry = Assert.Single(sut.IssuePreparationRepositories);
        entry.RepositoryName.Should().Be("mein-repo");
        entry.IsSelected.Should().BeTrue();
    }
}
