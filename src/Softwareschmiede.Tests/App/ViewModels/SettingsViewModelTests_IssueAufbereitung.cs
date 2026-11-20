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
public sealed class SettingsViewModelTests_IssueAufbereitung : IDisposable
{
    private const string KiPrefix = "Softwareschmiede.TestKi";

    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiedeDbContext _db;
    private readonly AppSettingService _settingService;
    private readonly WorkingDirectorySettingsService _arbeitsverzeichnisService;
    private readonly DarkModeService _darkModeService;
    private readonly Mock<IPluginManager> _pluginManagerMock;
    private readonly PluginActivationService _pluginActivationService;
    private readonly PluginSettingsService _pluginSettingsService;
    private readonly PromptTemplateService _promptVorlagenService;

    /// <summary>SettingsViewModelTests_IssueAufbereitung.</summary>
    public SettingsViewModelTests_IssueAufbereitung()
    {
        _db = TestDbContextFactory.Create();
        _settingService = new AppSettingService(_db, NullLogger<AppSettingService>.Instance);
        _arbeitsverzeichnisService = new WorkingDirectorySettingsService(_db, NullLogger<WorkingDirectorySettingsService>.Instance);

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
        _promptVorlagenService = new PromptTemplateService(_db, NullLogger<PromptTemplateService>.Instance);
    }

    /// <summary>Dispose.</summary>
    public void Dispose() => _db.Dispose();

    private SettingsViewModel CreateSut() =>
        new(
            _settingService,
            _arbeitsverzeichnisService,
            _darkModeService,
            _pluginManagerMock.Object,
            _pluginActivationService,
            _pluginSettingsService,
            _promptVorlagenService,
            new ProjectService(_db, NullLogger<ProjectService>.Instance),
            NullLogger<SettingsViewModel>.Instance,
            new Mock<IDialogService>().Object,
            new Mock<IServiceProvider>().Object);

    private static IAiPlugin CreateKiPlugin()
    {
        var mock = new Mock<IAiPlugin>();
        mock.Setup(p => p.PluginName).Returns("TestKI");
        mock.Setup(p => p.PluginPrefix).Returns(KiPrefix);
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
    public async Task LadenAsync_Standardwerte()
    {
        var sut = CreateSut();

        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.IsIssueAufbereitungEnabled.Should().BeFalse();
        sut.IsIssueAufbereitungKommentarEnabled.Should().BeTrue();
        sut.SelectedIssueAufbereitungKiPlugin.Should().Be(IssueAufbereitungKiPluginOption.Default);
        sut.IssueAufbereitungKiPluginOptionen.Should().ContainSingle(x => x.PluginPrefix == null);
        sut.IssueAufbereitungStatusText.Should().Be("deaktiviert");
        sut.IsIssueAufbereitungFelderEnabled.Should().BeFalse();
    }

    /// <summary>Die Plugin-Auswahl enthält den Standard-Eintrag plus jedes KI-Plugin mit Namen.</summary>
    [Fact]
    public async Task LadenAsync_PluginOptionen_EnthaltenStandardUndPlugins()
    {
        _pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([CreateKiPlugin()]);
        var sut = CreateSut();

        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.IssueAufbereitungKiPluginOptionen.Should().HaveCount(2);
        sut.IssueAufbereitungKiPluginOptionen[0].PluginPrefix.Should().BeNull();
        sut.IssueAufbereitungKiPluginOptionen[1].PluginPrefix.Should().Be(KiPrefix);
        sut.IssueAufbereitungKiPluginOptionen[1].Label.Should().Be("TestKI");
    }

    /// <summary>Gespeichertes Plugin-Prefix wird in der Auswahl vorgewählt.</summary>
    [Fact]
    public async Task LadenAsync_GespeichertesPlugin_Vorausgewählt()
    {
        _pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([CreateKiPlugin()]);
        await _settingService.SetSettingAsync(AppSettingService.ScmIssuePreparationPluginKey, KiPrefix, TestContext.Current.CancellationToken);
        var sut = CreateSut();

        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.SelectedIssueAufbereitungKiPlugin.Should().Be(sut.IssueAufbereitungKiPluginOptionen[1]);
    }

    /// <summary>Speichern persistiert alle vier Einstellungen (Schalter, Plugin, Repositories, Kommentar).</summary>
    [Fact]
    public async Task SpeichernAsync_PersistiertAlleEinstellungen()
    {
        _pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([CreateKiPlugin()]);
        var repository = await AddRepositoryAsync();
        var sut = CreateSut();
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();
        sut.IsIssueAufbereitungEnabled = true;
        sut.IsIssueAufbereitungKommentarEnabled = false;
        sut.SelectedIssueAufbereitungKiPlugin = sut.IssueAufbereitungKiPluginOptionen[1];
        sut.IssueAufbereitungRepositories.Single().IsSelected = true;

        await ((AsyncRelayCommand)sut.SpeichernCommand).ExecuteAsync();

        sut.FehlerMeldung.Should().BeNull();
        (await _settingService.GetSettingAsync(AppSettingService.ScmIssuePreparationEnabledKey, TestContext.Current.CancellationToken)).Should().Be("True");
        (await _settingService.GetSettingAsync(AppSettingService.ScmIssuePreparationCommentEnabledKey, TestContext.Current.CancellationToken)).Should().Be("False");
        (await _settingService.GetSettingAsync(AppSettingService.ScmIssuePreparationPluginKey, TestContext.Current.CancellationToken)).Should().Be(KiPrefix);
        var stored = await _db.GitRepositories.FindAsync(new object?[] { repository.Id }, TestContext.Current.CancellationToken);
        stored!.IssuePreparationEnabled.Should().BeTrue();
    }

    /// <summary>Der Standard-Eintrag persistiert null als Plugin-Prefix (Fallback-Kette).</summary>
    [Fact]
    public async Task SpeichernAsync_StandardEintrag_PersistiertNull()
    {
        await _settingService.SetSettingAsync(AppSettingService.ScmIssuePreparationPluginKey, "alt.wert", TestContext.Current.CancellationToken);
        var sut = CreateSut();
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();
        // „alt.wert" ist kein installiertes Plugin → Standard-Auswahl.
        sut.SelectedIssueAufbereitungKiPlugin.Should().Be(IssueAufbereitungKiPluginOption.Default);

        await ((AsyncRelayCommand)sut.SpeichernCommand).ExecuteAsync();

        sut.FehlerMeldung.Should().BeNull();
        (await _settingService.GetSettingAsync(AppSettingService.ScmIssuePreparationPluginKey, TestContext.Current.CancellationToken)).Should().BeNull();
    }

    /// <summary>Der Funktionsschalter steuert die Bedienbarkeit der Detail-Felder und den Header-Status.</summary>
    [Fact]
    public async Task Schalter_SteuertFelderEnabledUndStatus()
    {
        var sut = CreateSut();
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.IsIssueAufbereitungFelderEnabled.Should().BeFalse();
        sut.IssueAufbereitungStatusText.Should().Be("deaktiviert");

        sut.IsIssueAufbereitungEnabled = true;

        sut.IsIssueAufbereitungFelderEnabled.Should().BeTrue();
        sut.IssueAufbereitungStatusText.Should().Be("aktiviert");
    }

    /// <summary>Überwachte Repositories erscheinen benannt in der Auswahl; ihre Flags laden/speichern.</summary>
    [Fact]
    public async Task LadenAsync_Repositories_MitAnzeigenameUndFlag()
    {
        var repository = await AddRepositoryAsync("mein-repo");
        await new ProjectService(_db, NullLogger<ProjectService>.Instance).SetIssuePreparationEnabledAsync(repository.Id, true, TestContext.Current.CancellationToken);
        var sut = CreateSut();

        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        var entry = Assert.Single(sut.IssueAufbereitungRepositories);
        entry.RepositoryName.Should().Be("mein-repo");
        entry.IsSelected.Should().BeTrue();
    }
}
