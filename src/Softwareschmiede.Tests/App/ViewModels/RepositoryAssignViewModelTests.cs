using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Tests.App.ViewModels;

/// <summary>Unit-Tests für RepositoryAssignViewModel.</summary>
public sealed class RepositoryAssignViewModelTests
{
    private readonly Mock<IPluginManager> _pluginManagerMock = new();

    private static Mock<IGitPlugin> CreatePluginMock(string pluginName, PluginType pluginType = PluginType.SourceCodeManagement)
    {
        var mock = new Mock<IGitPlugin>();
        mock.Setup(p => p.PluginName).Returns(pluginName);
        mock.Setup(p => p.PluginType).Returns(pluginType);
        mock.Setup(p => p.PluginPrefix).Returns(pluginName);
        mock.Setup(p => p.GetSettingGroups()).Returns([]);
        mock.Setup(p => p.GetAvailableRepositoriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        return mock;
    }

    private RepositoryAssignViewModel CreateSut() =>
        new(NullLogger<RepositoryAssignViewModel>.Instance, _pluginManagerMock.Object);

    /// <summary>LadenAsync befüllt AvailableScmPlugins wenn Plugins vorhanden sind.</summary>
    [Fact]
    public async Task LadenAsync_ShouldLoadAvailablePlugins_WhenPluginsExist()
    {
        var plugin1 = CreatePluginMock("GitHub").Object;
        var plugin2 = CreatePluginMock("GitLab").Object;
        var plugin3 = CreatePluginMock("Bitbucket").Object;
        _pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins())
            .Returns([plugin1, plugin2, plugin3]);
        var sut = CreateSut();

        await sut.LadenAsync();

        sut.AvailableScmPlugins.Should().HaveCount(3);
        sut.AvailableScmPlugins.Should().Contain(plugin1);
        sut.AvailableScmPlugins.Should().Contain(plugin2);
        sut.AvailableScmPlugins.Should().Contain(plugin3);
    }

    /// <summary>LadenAsync setzt HasScmPlugins auf true wenn Plugins verfügbar sind.</summary>
    [Fact]
    public async Task LadenAsync_ShouldSetHasScmPlugins_ToTrue_WhenPluginsAvailable()
    {
        var plugin = CreatePluginMock("GitHub").Object;
        _pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([plugin]);
        var sut = CreateSut();

        await sut.LadenAsync();

        sut.HasScmPlugins.Should().BeTrue();
    }

    /// <summary>LadenAsync setzt HasScmPlugins auf false wenn keine Plugins vorhanden sind.</summary>
    [Fact]
    public async Task LadenAsync_ShouldSetHasScmPlugins_ToFalse_WhenNoPluginsAvailable()
    {
        _pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([]);
        var sut = CreateSut();

        await sut.LadenAsync();

        sut.HasScmPlugins.Should().BeFalse();
        sut.AvailableScmPlugins.Should().BeEmpty();
    }

    /// <summary>LadenAsync setzt SelectedScmPlugin auf das erste Plugin.</summary>
    [Fact]
    public async Task LadenAsync_ShouldSetSelectedScmPlugin_ToFirstPlugin()
    {
        var plugin1 = CreatePluginMock("GitHub").Object;
        var plugin2 = CreatePluginMock("LocalDirectory").Object;
        _pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([plugin1, plugin2]);
        var sut = CreateSut();

        await sut.LadenAsync();

        sut.SelectedScmPlugin.Should().Be(plugin1);
    }

    /// <summary>Plugin-Auswahl lädt Repositories aus der externen Plugin-Quelle.</summary>
    [Fact]
    public async Task SelectedScmPluginChanged_ShouldReloadRepositories_FromPlugin()
    {
        var pluginMock = CreatePluginMock("GitHub");
        pluginMock.Setup(p => p.GetAvailableRepositoriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new AvailableRepository("owner/repo1", "https://github.com/owner/repo1"),
                new AvailableRepository("owner/repo2", "https://github.com/owner/repo2"),
                new AvailableRepository("owner/repo3", "https://github.com/owner/repo3"),
            ]);
        _pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([pluginMock.Object]);
        var sut = CreateSut();
        await sut.LadenAsync();
        await sut.CurrentReloadTask!;

        sut.VerfuegbareRepositories.Should().HaveCount(3);
        sut.VerfuegbareRepositories.Should().Contain(r => r.Name == "owner/repo1");
    }

    /// <summary>Wenn SelectedScmPlugin auf null gesetzt wird, wird VerfuegbareRepositories geleert.</summary>
    [Fact]
    public async Task SelectedScmPluginChanged_ShouldClearRepositories_WhenPluginUnselected()
    {
        var pluginMock = CreatePluginMock("GitHub");
        pluginMock.Setup(p => p.GetAvailableRepositoriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new AvailableRepository("owner/repo1", "https://github.com/owner/repo1")]);
        _pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([pluginMock.Object]);
        var sut = CreateSut();
        await sut.LadenAsync();
        await sut.CurrentReloadTask!;

        sut.SelectedScmPlugin = null;
        await sut.CurrentReloadTask!;

        sut.VerfuegbareRepositories.Should().BeEmpty();
    }

    /// <summary>IsLoading wird während des Reloads gesetzt und danach zurückgesetzt.</summary>
    [Fact]
    public async Task SelectedScmPluginChanged_ShouldSetIsLoading_FlagDuringReload()
    {
        var tcs = new TaskCompletionSource<IEnumerable<AvailableRepository>>();
        var pluginMock = CreatePluginMock("GitHub");
        pluginMock.Setup(p => p.GetAvailableRepositoriesAsync(It.IsAny<CancellationToken>()))
            .Returns(tcs.Task);
        _pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([pluginMock.Object]);
        var sut = CreateSut();

        var loadingWasTrue = false;
        sut.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(sut.IsLoading) && sut.IsLoading)
                loadingWasTrue = true;
        };

        await sut.LadenAsync();
        tcs.SetResult([]);
        await sut.CurrentReloadTask!;

        loadingWasTrue.Should().BeTrue();
        sut.IsLoading.Should().BeFalse();
    }

    /// <summary>Bei Fehler in GetAvailableRepositoriesAsync bleibt VerfuegbareRepositories leer ohne Exception-Propagation.</summary>
    [Fact]
    public async Task ReloadRepositoriesForSelectedPlugin_ShouldHandleError_Gracefully()
    {
        var pluginMock = CreatePluginMock("GitHub");
        pluginMock.Setup(p => p.GetAvailableRepositoriesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Verbindungsfehler"));
        _pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([pluginMock.Object]);
        var sut = CreateSut();
        await sut.LadenAsync();
        await sut.CurrentReloadTask!;

        sut.VerfuegbareRepositories.Should().BeEmpty();
        sut.IsLoading.Should().BeFalse();
    }

    /// <summary>BestaetigenCommand hat CanExecute true wenn ein Repository ausgewählt ist.</summary>
    [Fact]
    public void RepositorySelection_ShouldEnableBestaetigenCommand_WhenRepositorySelected()
    {
        var sut = CreateSut();

        sut.SelectedRepository = new AvailableRepository("owner/repo", "https://github.com/owner/repo");

        sut.BestaetigenCommand.CanExecute(null).Should().BeTrue();
    }

    /// <summary>BestaetigenCommand hat CanExecute false wenn kein Repository ausgewählt ist.</summary>
    [Fact]
    public void RepositorySelection_ShouldDisableBestaetigenCommand_WhenRepositoryUnselected()
    {
        var sut = CreateSut();
        sut.SelectedRepository = new AvailableRepository("owner/repo", "https://github.com/owner/repo");

        sut.SelectedRepository = null;

        sut.BestaetigenCommand.CanExecute(null).Should().BeFalse();
    }
}
