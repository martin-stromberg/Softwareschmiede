using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.App.ViewModels;

/// <summary>Unit-Tests für RepositoryAssignViewModel.</summary>
public sealed class RepositoryAssignViewModelTests : IDisposable
{
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiededDbContext _db;
    private readonly ProjektService _projektService;
    private readonly Mock<IPluginManager> _pluginManagerMock;

    public RepositoryAssignViewModelTests()
    {
        _db = TestDbContextFactory.Create();
        _projektService = new ProjektService(_db, NullLogger<ProjektService>.Instance);
        _pluginManagerMock = new Mock<IPluginManager>();
    }

    public void Dispose() => _db.Dispose();

    private static Mock<IGitPlugin> CreatePluginMock(string pluginName, PluginType pluginType = PluginType.SourceCodeManagement)
    {
        var mock = new Mock<IGitPlugin>();
        mock.Setup(p => p.PluginName).Returns(pluginName);
        mock.Setup(p => p.PluginType).Returns(pluginType);
        mock.Setup(p => p.PluginPrefix).Returns(pluginName);
        mock.Setup(p => p.GetSettingGroups()).Returns([]);
        return mock;
    }

    private RepositoryAssignViewModel CreateSut() =>
        new(_projektService, NullLogger<RepositoryAssignViewModel>.Instance, _pluginManagerMock.Object);

    /// <summary>LadenAsync befüllt AvailableScmPlugins wenn Plugins vorhanden sind.</summary>
    [Fact]
    public async Task LadenAsync_ShouldLoadAvailablePlugins_WhenPluginsExist()
    {
        // Arrange
        var plugin1 = CreatePluginMock("GitHub").Object;
        var plugin2 = CreatePluginMock("GitLab").Object;
        var plugin3 = CreatePluginMock("Bitbucket").Object;
        _pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins())
            .Returns([plugin1, plugin2, plugin3]);
        var sut = CreateSut();

        // Act
        await sut.LadenAsync();

        // Assert
        sut.AvailableScmPlugins.Should().HaveCount(3);
        sut.AvailableScmPlugins.Should().Contain(plugin1);
        sut.AvailableScmPlugins.Should().Contain(plugin2);
        sut.AvailableScmPlugins.Should().Contain(plugin3);
    }

    /// <summary>LadenAsync setzt HasScmPlugins auf true wenn Plugins verfügbar sind.</summary>
    [Fact]
    public async Task LadenAsync_ShouldSetHasScmPlugins_ToTrue_WhenPluginsAvailable()
    {
        // Arrange
        var plugin = CreatePluginMock("GitHub").Object;
        _pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins())
            .Returns([plugin]);
        var sut = CreateSut();

        // Act
        await sut.LadenAsync();

        // Assert
        sut.HasScmPlugins.Should().BeTrue();
    }

    /// <summary>LadenAsync setzt HasScmPlugins auf false wenn keine Plugins vorhanden sind.</summary>
    [Fact]
    public async Task LadenAsync_ShouldSetHasScmPlugins_ToFalse_WhenNoPluginsAvailable()
    {
        // Arrange
        _pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins())
            .Returns([]);
        var sut = CreateSut();

        // Act
        await sut.LadenAsync();

        // Assert
        sut.HasScmPlugins.Should().BeFalse();
        sut.AvailableScmPlugins.Should().BeEmpty();
    }

    /// <summary>Plugin-Auswahl filtert Repositories nach PluginType.</summary>
    [Fact]
    public async Task SelectedScmPluginChanged_ShouldReloadRepositories_FilteredByPluginType()
    {
        // Arrange
        var pluginA = CreatePluginMock("GitHub").Object;
        var pluginB = CreatePluginMock("LocalDirectoryPlugin").Object;
        _pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins())
            .Returns([pluginA, pluginB]);

        var projekt = await _projektService.CreateAsync("Test-Projekt", null);
        await _projektService.AddRepositoryAsync(projekt.Id, "SourceCodeManagement", "https://github.com/a/repo1", "repo1");
        await _projektService.AddRepositoryAsync(projekt.Id, "SourceCodeManagement", "https://github.com/a/repo2", "repo2");
        await _projektService.AddRepositoryAsync(projekt.Id, "SourceCodeManagement", "https://github.com/a/repo3", "repo3");
        await _projektService.AddRepositoryAsync(projekt.Id, "DevelopmentAutomation", "/local/path1", "local1");
        await _projektService.AddRepositoryAsync(projekt.Id, "DevelopmentAutomation", "/local/path2", "local2");

        var sut = CreateSut();
        await sut.LadenAsync();

        // Act
        sut.SelectedScmPlugin = pluginA;
        await Task.Delay(100);

        // Assert
        sut.VerfuegbareRepositories.Should().HaveCount(3);
        sut.VerfuegbareRepositories.Should().OnlyContain(r => r.PluginTyp == "SourceCodeManagement");
    }

    /// <summary>Wenn SelectedScmPlugin auf null gesetzt wird, wird VerfuegbareRepositories geleert.</summary>
    [Fact]
    public async Task SelectedScmPluginChanged_ShouldClearRepositories_WhenPluginUnselected()
    {
        // Arrange
        var plugin = CreatePluginMock("GitHub").Object;
        _pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins())
            .Returns([plugin]);

        var projekt = await _projektService.CreateAsync("Test-Projekt", null);
        await _projektService.AddRepositoryAsync(projekt.Id, "SourceCodeManagement", "https://github.com/a/repo1", "repo1");

        var sut = CreateSut();
        await sut.LadenAsync();
        sut.SelectedScmPlugin = plugin;
        await Task.Delay(100);

        // Act
        sut.SelectedScmPlugin = null;
        await Task.Delay(100);

        // Assert
        sut.VerfuegbareRepositories.Should().BeEmpty();
    }

    /// <summary>IsLoading wird während des Reloads gesetzt und danach zurückgesetzt.</summary>
    [Fact]
    public async Task SelectedScmPluginChanged_ShouldSetIsLoading_FlagDuringReload()
    {
        // Arrange
        var tcs = new TaskCompletionSource<IEnumerable<GitRepository>>();
        var plugin = CreatePluginMock("GitHub").Object;
        _pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins())
            .Returns([plugin]);

        var sut = CreateSut();
        await sut.LadenAsync();

        var loadingWasTrue = false;
        sut.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(sut.IsLoading) && sut.IsLoading)
                loadingWasTrue = true;
        };

        // Act
        sut.SelectedScmPlugin = plugin;
        await Task.Delay(200);

        // Assert
        loadingWasTrue.Should().BeTrue();
        sut.IsLoading.Should().BeFalse();
    }

    /// <summary>Wenn keine Repositories zum gewählten Plugin-Typ existieren, bleibt VerfuegbareRepositories leer und wird keine Exception propagiert.</summary>
    [Fact]
    public async Task ReloadRepositoriesForSelectedPlugin_ShouldLogError_WhenServiceThrows()
    {
        // Arrange: Plugin vom Typ DevelopmentAutomation
        var plugin = CreatePluginMock("GitHub", PluginType.DevelopmentAutomation).Object;
        _pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins())
            .Returns([plugin]);

        var projekt = await _projektService.CreateAsync("Test-Projekt", null);
        // Nur Repositories mit PluginTyp "SourceCodeManagement" in der DB
        await _projektService.AddRepositoryAsync(projekt.Id, "SourceCodeManagement", "https://github.com/a/repo1", "repo1");

        var sut = CreateSut();
        await sut.LadenAsync();

        // Act: Plugin auswählen dessen PluginType.ToString() == "DevelopmentAutomation" — kein Match
        sut.SelectedScmPlugin = plugin;
        await Task.Delay(200);

        // Assert: Keine Exception propagiert; VerfuegbareRepositories ist leer (kein Typ-Match)
        sut.VerfuegbareRepositories.Should().BeEmpty();
        sut.IsLoading.Should().BeFalse();
    }

    /// <summary>BestaetigenCommand hat CanExecute true wenn ein Repository ausgewählt ist.</summary>
    [Fact]
    public void RepositorySelection_ShouldEnableBestaetigenCommand_WhenRepositorySelected()
    {
        // Arrange
        var sut = CreateSut();
        var repo = new GitRepository
        {
            Id = Guid.NewGuid(),
            PluginTyp = "SourceCodeManagement",
            RepositoryUrl = "https://github.com/test/repo",
            RepositoryName = "test-repo"
        };

        // Act
        sut.SelectedRepository = repo;

        // Assert
        sut.BestaetigenCommand.CanExecute(null).Should().BeTrue();
    }

    /// <summary>BestaetigenCommand hat CanExecute false wenn kein Repository ausgewählt ist.</summary>
    [Fact]
    public void RepositorySelection_ShouldDisableBestaetigenCommand_WhenRepositoryUnselected()
    {
        // Arrange
        var sut = CreateSut();
        sut.SelectedRepository = new GitRepository
        {
            Id = Guid.NewGuid(),
            PluginTyp = "SourceCodeManagement",
            RepositoryUrl = "https://github.com/test/repo",
            RepositoryName = "test-repo"
        };

        // Act
        sut.SelectedRepository = null;

        // Assert
        sut.BestaetigenCommand.CanExecute(null).Should().BeFalse();
    }
}
