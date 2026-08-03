using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Tests.App.ViewModels;

/// <summary>Tests für die Basis-Branch-Auswahl in <see cref="RepositoryAssignViewModel"/>.</summary>
public sealed class RepositoryAssignViewModelTests_BasisBranch
{
    private readonly Mock<IPluginManager> _pluginManagerMock = new();

    private static Mock<IGitPlugin> CreatePluginMock(string pluginName)
    {
        var mock = new Mock<IGitPlugin>();
        mock.Setup(p => p.PluginName).Returns(pluginName);
        mock.Setup(p => p.PluginType).Returns(PluginType.SourceCodeManagement);
        mock.Setup(p => p.PluginPrefix).Returns(pluginName);
        mock.Setup(p => p.GetSettingGroups()).Returns([]);
        mock.Setup(p => p.GetAvailableRepositoriesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        return mock;
    }

    private RepositoryAssignViewModel CreateSut() =>
        new(NullLogger<RepositoryAssignViewModel>.Instance, _pluginManagerMock.Object);

    /// <summary>Wählt das übergebene Plugin aus und wartet, bis der dadurch ausgelöste Repository-Reload abgeschlossen ist.</summary>
    private static async Task SelectPluginAndWaitAsync(RepositoryAssignViewModel sut, IGitPlugin plugin)
    {
        sut.SelectedScmPlugin = plugin;
        if (sut.CurrentReloadTask is not null)
            await sut.CurrentReloadTask;
    }

    /// <summary>Ändern von SelectedRepository lädt die verfügbaren Branches aus dem Plugin.</summary>
    [Fact]
    public async Task RepositoryChanged_ShouldLoadAvailableBranches()
    {
        var pluginMock = CreatePluginMock("GitHub");
        pluginMock.Setup(p => p.GetRemoteBranchesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(["main", "develop"]);
        pluginMock.Setup(p => p.GetDefaultBranchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("main");
        var sut = CreateSut();
        await SelectPluginAndWaitAsync(sut, pluginMock.Object);

        sut.SelectedRepository = new AvailableRepository("repo", DateTime.UtcNow, "owner/repo", "https://example.com/repo.git");
        await sut.CurrentLoadSourceBranchesTask!;

        sut.AvailableSourceBranches.Should().Contain(["main", "develop"]);
    }

    /// <summary>Wählt der Benutzer einen Default-Branch vor, der aus dem Plugin ermittelt wird.</summary>
    [Fact]
    public async Task SelectedRepository_ShouldLoadAndSuggestDefaultBranch()
    {
        var pluginMock = CreatePluginMock("GitHub");
        pluginMock.Setup(p => p.GetRemoteBranchesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(["main", "develop"]);
        pluginMock.Setup(p => p.GetDefaultBranchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("main");
        var sut = CreateSut();
        await SelectPluginAndWaitAsync(sut, pluginMock.Object);

        sut.SelectedRepository = new AvailableRepository("repo", DateTime.UtcNow, "owner/repo", "https://example.com/repo.git");
        await sut.CurrentLoadSourceBranchesTask!;

        sut.DefaultSourceBranchName.Should().Be("main");
    }

    /// <summary>Die Validierung schlägt fehl, wenn der eingegebene Branch nicht in der geladenen Liste enthalten ist.</summary>
    [Fact]
    public async Task SourceBranchValidation_ShouldFail_WhenBranchDoesNotExist()
    {
        var pluginMock = CreatePluginMock("GitHub");
        pluginMock.Setup(p => p.GetRemoteBranchesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(["main", "develop"]);
        pluginMock.Setup(p => p.GetDefaultBranchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("main");
        var sut = CreateSut();
        await SelectPluginAndWaitAsync(sut, pluginMock.Object);
        sut.SelectedRepository = new AvailableRepository("repo", DateTime.UtcNow, "owner/repo", "https://example.com/repo.git");
        await sut.CurrentLoadSourceBranchesTask!;

        sut.DefaultSourceBranchName = "staging";

        sut.SourceBranchInputError.Should().NotBeNullOrEmpty();
        sut.BestaetigenCommand.CanExecute(null).Should().BeFalse();
    }

    /// <summary>Die Validierung ist erfolgreich, wenn der eingegebene Branch in der geladenen Liste enthalten ist.</summary>
    [Fact]
    public async Task SourceBranchValidation_ShouldSucceed_WhenBranchExists()
    {
        var pluginMock = CreatePluginMock("GitHub");
        pluginMock.Setup(p => p.GetRemoteBranchesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(["main", "develop"]);
        pluginMock.Setup(p => p.GetDefaultBranchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("main");
        var sut = CreateSut();
        await SelectPluginAndWaitAsync(sut, pluginMock.Object);
        sut.SelectedRepository = new AvailableRepository("repo", DateTime.UtcNow, "owner/repo", "https://example.com/repo.git");
        await sut.CurrentLoadSourceBranchesTask!;

        sut.DefaultSourceBranchName = "develop";

        sut.SourceBranchInputError.Should().BeNullOrEmpty();
        sut.BestaetigenCommand.CanExecute(null).Should().BeTrue();
    }

    /// <summary>Bei Bestätigung des Dialogs bleibt der gewählte Basis-Branch über die Eigenschaft abrufbar (an die View gebunden).</summary>
    [Fact]
    public async Task Confirm_ShouldReturnDefaultSourceBranchName()
    {
        var pluginMock = CreatePluginMock("GitHub");
        pluginMock.Setup(p => p.GetRemoteBranchesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(["main", "develop"]);
        pluginMock.Setup(p => p.GetDefaultBranchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("main");
        var sut = CreateSut();
        await SelectPluginAndWaitAsync(sut, pluginMock.Object);
        sut.SelectedRepository = new AvailableRepository("repo", DateTime.UtcNow, "owner/repo", "https://example.com/repo.git");
        await sut.CurrentLoadSourceBranchesTask!;
        sut.DefaultSourceBranchName = "develop";

        var confirmed = false;
        sut.CloseRequested += (_, result) => confirmed = result;
        sut.BestaetigenCommand.Execute(null);

        confirmed.Should().BeTrue();
        sut.DefaultSourceBranchName.Should().Be("develop");
    }
}
