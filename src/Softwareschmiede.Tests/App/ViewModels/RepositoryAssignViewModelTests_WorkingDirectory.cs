using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Tests.App.ViewModels;

/// <summary>Tests für die Arbeitsverzeichnis-Auswahl in <see cref="RepositoryAssignViewModel"/>.</summary>
public sealed class RepositoryAssignViewModelTests_WorkingDirectory : IDisposable
{
    private readonly Mock<IPluginManager> _pluginManagerMock = new();
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());

    /// <summary>Dispose.</summary>
    public void Dispose() => _cache.Dispose();

    private DirectoryStructureBrowserService CreateDirectoryStructureService() =>
        new(_cache, Options.Create(new DirectoryStructureOptions()), NullLogger<DirectoryStructureBrowserService>.Instance);

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

    private RepositoryAssignViewModel CreateSut(ILogger<RepositoryAssignViewModel>? logger = null) =>
        new(logger ?? NullLogger<RepositoryAssignViewModel>.Instance, _pluginManagerMock.Object, CreateDirectoryStructureService());

    /// <summary>Wählt das übergebene Plugin aus und wartet, bis der dadurch ausgelöste Repository-Reload abgeschlossen ist.</summary>
    /// <param name="sut">Das zu testende ViewModel.</param>
    /// <param name="plugin">Das auszuwählende Plugin.</param>
    private static async Task SelectPluginAndWaitAsync(RepositoryAssignViewModel sut, IGitPlugin plugin)
    {
        sut.SelectedScmPlugin = plugin;
        if (sut.CurrentReloadTask is not null)
            await sut.CurrentReloadTask;
    }

    /// <summary>Ändern von SelectedRepository löst das Laden der Verzeichnisstruktur aus.</summary>
    [Fact]
    public async Task SelectedRepositoryChanged_ShouldLoadDirectoryStructure()
    {
        var pluginMock = CreatePluginMock("GitHub");
        pluginMock.Setup(p => p.GetRepositoryStructureAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new RepositoryDirectoryEntry("backend", IsDirectory: true)]);
        var sut = CreateSut();
        await SelectPluginAndWaitAsync(sut, pluginMock.Object);

        sut.SelectedRepository = new AvailableRepository("repo", DateTime.UtcNow, "owner/repo", "https://example.com/repo.git");
        await sut.CurrentLoadDirectoryStructureTask!;

        sut.AvailableWorkingDirectories.Should().Contain("backend");
    }

    /// <summary>Beim Wechsel des Repositories wird SelectedWorkingDirectory auf null zurückgesetzt, bevor die neue Verzeichnisstruktur geladen ist.</summary>
    [Fact]
    public async Task SelectedRepositoryChanged_ShouldResetSelectedWorkingDirectory()
    {
        var firstLoadTcs = new TaskCompletionSource<IEnumerable<RepositoryDirectoryEntry>>();
        var pluginMock = CreatePluginMock("GitHub");
        pluginMock.Setup(p => p.GetRepositoryStructureAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(firstLoadTcs.Task);
        var sut = CreateSut();
        await SelectPluginAndWaitAsync(sut, pluginMock.Object);
        sut.SelectedRepository = new AvailableRepository("repo1", DateTime.UtcNow, "owner/repo1", "https://example.com/repo1.git");
        firstLoadTcs.SetResult([]);
        await sut.CurrentLoadDirectoryStructureTask!;
        sut.SelectedWorkingDirectory = "backend";

        var secondLoadTcs = new TaskCompletionSource<IEnumerable<RepositoryDirectoryEntry>>();
        pluginMock.Setup(p => p.GetRepositoryStructureAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(secondLoadTcs.Task);

        sut.SelectedRepository = new AvailableRepository("repo2", DateTime.UtcNow, "owner/repo2", "https://example.com/repo2.git");

        sut.SelectedWorkingDirectory.Should().BeNull();

        secondLoadTcs.SetResult([]);
        await sut.CurrentLoadDirectoryStructureTask!;
    }

    /// <summary>Beim erneuten Wechsel des Repositories wird der vorherige Ladevorgang der Verzeichnisstruktur abgebrochen.</summary>
    [Fact]
    public async Task SelectedRepositoryChanged_ShouldCancelPreviousLoad()
    {
        var callCount = 0;
        var pluginMock = CreatePluginMock("GitHub");
        pluginMock.Setup(p => p.GetRepositoryStructureAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns<string, int, CancellationToken>(async (_, _, ct) =>
            {
                if (Interlocked.Increment(ref callCount) == 1)
                {
                    await Task.Delay(Timeout.Infinite, ct);
                }

                return [];
            });
        var sut = CreateSut();
        await SelectPluginAndWaitAsync(sut, pluginMock.Object);

        sut.SelectedRepository = new AvailableRepository("repo1", DateTime.UtcNow, "owner/repo1", "https://example.com/repo1.git");
        var firstLoadTask = sut.CurrentLoadDirectoryStructureTask!;
        firstLoadTask.IsCompleted.Should().BeFalse();

        sut.SelectedRepository = new AvailableRepository("repo2", DateTime.UtcNow, "owner/repo2", "https://example.com/repo2.git");
        await sut.CurrentLoadDirectoryStructureTask!;

        var finished = await Task.WhenAny(firstLoadTask, Task.Delay(TimeSpan.FromSeconds(5)));
        finished.Should().Be(firstLoadTask, "der vorherige Ladevorgang muss durch den Repository-Wechsel abgebrochen werden");
    }

    /// <summary>IsLoadingDirectoryStructure wird während des Abrufs auf true gesetzt und danach zurückgesetzt.</summary>
    [Fact]
    public async Task LoadDirectoryStructureAsync_ShouldSetIsLoading_Flag()
    {
        var tcs = new TaskCompletionSource<IEnumerable<RepositoryDirectoryEntry>>();
        var pluginMock = CreatePluginMock("GitHub");
        pluginMock.Setup(p => p.GetRepositoryStructureAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(tcs.Task);
        var sut = CreateSut();
        await SelectPluginAndWaitAsync(sut, pluginMock.Object);

        var loadingWasTrue = false;
        sut.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(sut.IsLoadingDirectoryStructure) && sut.IsLoadingDirectoryStructure)
                loadingWasTrue = true;
        };

        sut.SelectedRepository = new AvailableRepository("repo", DateTime.UtcNow, "owner/repo", "https://example.com/repo.git");
        var loadTask = sut.CurrentLoadDirectoryStructureTask!;
        tcs.SetResult([]);
        await loadTask;

        loadingWasTrue.Should().BeTrue();
        sut.IsLoadingDirectoryStructure.Should().BeFalse();
    }

    /// <summary>AvailableWorkingDirectories wird mit "." (Root) gefolgt vom Abruf-Ergebnis befüllt.</summary>
    [Fact]
    public async Task LoadDirectoryStructureAsync_ShouldPopulateDirectories_WithDotRoot()
    {
        var pluginMock = CreatePluginMock("GitHub");
        pluginMock.Setup(p => p.GetRepositoryStructureAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new RepositoryDirectoryEntry("backend", IsDirectory: true),
                new RepositoryDirectoryEntry("frontend", IsDirectory: true),
            ]);
        var sut = CreateSut();
        await SelectPluginAndWaitAsync(sut, pluginMock.Object);

        sut.SelectedRepository = new AvailableRepository("repo", DateTime.UtcNow, "owner/repo", "https://example.com/repo.git");
        await sut.CurrentLoadDirectoryStructureTask!;

        sut.AvailableWorkingDirectories.Should().Equal(".", "backend", "frontend");
    }

    /// <summary>SelectedWorkingDirectory wird nach dem Laden auf "." (Root) als Default gesetzt.</summary>
    [Fact]
    public async Task LoadDirectoryStructureAsync_ShouldSetDefaultSelectedDirectory()
    {
        var pluginMock = CreatePluginMock("GitHub");
        pluginMock.Setup(p => p.GetRepositoryStructureAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var sut = CreateSut();
        await SelectPluginAndWaitAsync(sut, pluginMock.Object);

        sut.SelectedRepository = new AvailableRepository("repo", DateTime.UtcNow, "owner/repo", "https://example.com/repo.git");
        await sut.CurrentLoadDirectoryStructureTask!;

        sut.SelectedWorkingDirectory.Should().Be(".");
    }

    /// <summary>Wird SelectedRepository auf null gesetzt, wird AvailableWorkingDirectories geleert, ohne dass eine Exception geworfen wird.</summary>
    [Fact]
    public async Task LoadDirectoryStructureAsync_ShouldHandleNullRepository()
    {
        var pluginMock = CreatePluginMock("GitHub");
        pluginMock.Setup(p => p.GetRepositoryStructureAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new RepositoryDirectoryEntry("backend", IsDirectory: true)]);
        var sut = CreateSut();
        await SelectPluginAndWaitAsync(sut, pluginMock.Object);
        sut.SelectedRepository = new AvailableRepository("repo", DateTime.UtcNow, "owner/repo", "https://example.com/repo.git");
        await sut.CurrentLoadDirectoryStructureTask!;
        sut.AvailableWorkingDirectories.Should().NotBeEmpty();

        sut.SelectedRepository = null;
        await sut.CurrentLoadDirectoryStructureTask!;

        sut.AvailableWorkingDirectories.Should().BeEmpty();
    }

    /// <summary>Schlägt der Abruf der Verzeichnisstruktur fehl, wird auf die Root-Option zurückgefallen, ohne dass eine Exception propagiert wird.</summary>
    [Fact]
    public async Task LoadDirectoryStructureAsync_ShouldHandleErrors_WithLogging()
    {
        var pluginMock = CreatePluginMock("GitHub");
        pluginMock.Setup(p => p.GetRepositoryStructureAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Fehler beim Abruf der Verzeichnisstruktur"));
        var sut = CreateSut();
        await SelectPluginAndWaitAsync(sut, pluginMock.Object);

        sut.SelectedRepository = new AvailableRepository("repo", DateTime.UtcNow, "owner/repo", "https://example.com/repo.git");
        await sut.CurrentLoadDirectoryStructureTask!;

        sut.AvailableWorkingDirectories.Should().ContainSingle().Which.Should().Be(".");
        sut.SelectedWorkingDirectory.Should().Be(".");
    }
}
