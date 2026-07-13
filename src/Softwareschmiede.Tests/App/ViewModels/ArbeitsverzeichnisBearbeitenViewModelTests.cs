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

/// <summary>Tests für <see cref="ArbeitsverzeichnisBearbeitenViewModel"/>.</summary>
public sealed class ArbeitsverzeichnisBearbeitenViewModelTests : IDisposable
{
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());

    /// <summary>Dispose.</summary>
    public void Dispose() => _cache.Dispose();

    private DirectoryStructureBrowserService CreateDirectoryStructureService() =>
        new(_cache, Options.Create(new DirectoryStructureOptions()), NullLogger<DirectoryStructureBrowserService>.Instance);

    private ArbeitsverzeichnisBearbeitenViewModel CreateSut(
        DirectoryStructureBrowserService? directoryStructureService = null,
        ILogger<ArbeitsverzeichnisBearbeitenViewModel>? logger = null) =>
        new(logger ?? NullLogger<ArbeitsverzeichnisBearbeitenViewModel>.Instance, directoryStructureService);

    private static Mock<IGitPlugin> CreatePluginMock(IEnumerable<RepositoryDirectoryEntry> entries)
    {
        var mock = new Mock<IGitPlugin>();
        mock.SetupGet(p => p.PluginType).Returns(PluginType.SourceCodeManagement);
        mock.SetupGet(p => p.PluginPrefix).Returns("Test");
        mock.Setup(p => p.GetRepositoryStructureAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries);
        mock.Setup(p => p.GetRepositoryStructureLoadResultAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RepositoryStructureLoadResult.Success(entries));
        return mock;
    }

    /// <summary>LadenAsync befüllt AvailableWorkingDirectories mit "." gefolgt von den geladenen Verzeichnissen.</summary>
    [Fact]
    public async Task LadenAsync_ShouldPopulateDirectories_WithDotRoot()
    {
        var pluginMock = CreatePluginMock([
            new RepositoryDirectoryEntry("backend", IsDirectory: true),
            new RepositoryDirectoryEntry("frontend", IsDirectory: true),
        ]);
        var sut = CreateSut(CreateDirectoryStructureService());

        await sut.LadenAsync(pluginMock.Object, "https://example.com/repo.git", currentWorkingDirectory: null);

        sut.AvailableWorkingDirectories.Should().Equal(".", "backend", "frontend");
    }

    /// <summary>LadenAsync wählt das übergebene aktuelle Arbeitsverzeichnis vor, wenn es in der geladenen Struktur enthalten ist.</summary>
    [Fact]
    public async Task LadenAsync_ShouldPreselectCurrentWorkingDirectory_WhenPresent()
    {
        var pluginMock = CreatePluginMock([
            new RepositoryDirectoryEntry("backend", IsDirectory: true),
            new RepositoryDirectoryEntry("frontend", IsDirectory: true),
        ]);
        var sut = CreateSut(CreateDirectoryStructureService());

        await sut.LadenAsync(pluginMock.Object, "https://example.com/repo.git", currentWorkingDirectory: "backend");

        sut.SelectedWorkingDirectory.Should().Be("backend");
    }

    /// <summary>LadenAsync fügt das aktuelle Arbeitsverzeichnis hinzu, wenn es nicht Teil der geladenen Struktur ist, damit die bestehende Auswahl nicht verloren geht.</summary>
    [Fact]
    public async Task LadenAsync_ShouldAddCurrentWorkingDirectory_WhenNotInLoadedStructure()
    {
        var pluginMock = CreatePluginMock([
            new RepositoryDirectoryEntry("frontend", IsDirectory: true),
        ]);
        var sut = CreateSut(CreateDirectoryStructureService());

        await sut.LadenAsync(pluginMock.Object, "https://example.com/repo.git", currentWorkingDirectory: "legacy/backend");

        sut.AvailableWorkingDirectories.Should().Contain("legacy/backend");
        sut.SelectedWorkingDirectory.Should().Be("legacy/backend");
    }

    /// <summary>LadenAsync wählt "." wenn kein aktuelles Arbeitsverzeichnis übergeben wurde.</summary>
    [Fact]
    public async Task LadenAsync_ShouldSelectRoot_WhenCurrentWorkingDirectoryIsNull()
    {
        var pluginMock = CreatePluginMock([new RepositoryDirectoryEntry("backend", IsDirectory: true)]);
        var sut = CreateSut(CreateDirectoryStructureService());

        await sut.LadenAsync(pluginMock.Object, "https://example.com/repo.git", currentWorkingDirectory: null);

        sut.SelectedWorkingDirectory.Should().Be(".");
    }

    /// <summary>LadenAsync wechselt auf manuelle Eingabe, wenn kein Git-Plugin übergeben wurde (z. B. Plugin nicht mehr installiert).</summary>
    [Fact]
    public async Task LadenAsync_ShouldEnableManualInput_WhenGitPluginIsNull()
    {
        var sut = CreateSut(CreateDirectoryStructureService());

        await sut.LadenAsync(gitPlugin: null, repositoryUrl: "https://example.com/repo.git", currentWorkingDirectory: null);

        sut.IsWorkingDirectoryManualInput.Should().BeTrue();
        sut.WorkingDirectoryInputText.Should().Be(".");
        sut.AvailableWorkingDirectories.Should().BeEmpty();
        sut.SelectedWorkingDirectory.Should().Be(".");
    }

    /// <summary>Schlägt der Abruf der Verzeichnisstruktur fehl, bleibt der Dialog über manuelle Eingabe benutzbar.</summary>
    [Fact]
    public async Task LadenAsync_ShouldShowCurrentWorkingDirectoryInManualInput_WhenLoadFails()
    {
        var pluginMock = new Mock<IGitPlugin>();
        pluginMock.SetupGet(p => p.PluginPrefix).Returns("Test");
        pluginMock.Setup(p => p.GetRepositoryStructureLoadResultAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RepositoryStructureLoadResult.Failed("Verbindungsfehler"));
        var sut = CreateSut(CreateDirectoryStructureService());

        var act = () => sut.LadenAsync(pluginMock.Object, "https://example.com/repo.git", currentWorkingDirectory: "backend");

        await act.Should().NotThrowAsync();
        sut.IsWorkingDirectoryManualInput.Should().BeTrue();
        sut.WorkingDirectoryInputText.Should().Be("backend");
        sut.AvailableWorkingDirectories.Should().BeEmpty();
        sut.SelectedWorkingDirectory.Should().Be("backend");
    }

    /// <summary>IsLoadingDirectoryStructure wird während des Abrufs auf true gesetzt und danach zurückgesetzt.</summary>
    [Fact]
    public async Task LadenAsync_ShouldToggleIsLoadingDirectoryStructure()
    {
        var tcs = new TaskCompletionSource<IEnumerable<RepositoryDirectoryEntry>>();
        var pluginMock = new Mock<IGitPlugin>();
        pluginMock.SetupGet(p => p.PluginPrefix).Returns("Test");
        pluginMock.Setup(p => p.GetRepositoryStructureLoadResultAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(async (string _, int _, CancellationToken _) => RepositoryStructureLoadResult.Success(await tcs.Task));
        var sut = CreateSut(CreateDirectoryStructureService());

        var loadingWasTrue = false;
        sut.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(sut.IsLoadingDirectoryStructure) && sut.IsLoadingDirectoryStructure)
                loadingWasTrue = true;
        };

        var loadTask = sut.LadenAsync(pluginMock.Object, "https://example.com/repo.git", currentWorkingDirectory: null);
        tcs.SetResult([]);
        await loadTask;

        loadingWasTrue.Should().BeTrue();
        sut.IsLoadingDirectoryStructure.Should().BeFalse();
    }

    /// <summary>
    /// Wird der Ladevorgang abgebrochen (Cancellation), bleibt die bisherige Auswahl unverändert (statt sie
    /// mit dem Root-Fallback zu überschreiben) und der Lade-Status wird zurückgesetzt, statt dauerhaft auf
    /// <c>true</c> hängen zu bleiben (Code-Review-Befund: `LadenAsync` wird pro Dialogaufruf nur einmal
    /// aufgerufen, es gibt keinen Folgeaufruf, der den Status sonst zurücksetzen würde).
    /// </summary>
    [Fact]
    public async Task LadenAsync_ShouldResetLoadingState_WithoutOverwritingSelection_WhenCancelled()
    {
        var pluginMock = new Mock<IGitPlugin>();
        pluginMock.SetupGet(p => p.PluginPrefix).Returns("Test");
        pluginMock.Setup(p => p.GetRepositoryStructureLoadResultAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns<string, int, CancellationToken>(async (_, _, ct) =>
            {
                await Task.Delay(Timeout.Infinite, ct);
                return RepositoryStructureLoadResult.Success([]);
            });
        var sut = CreateSut(CreateDirectoryStructureService());
        using var cts = new CancellationTokenSource();

        var loadTask = sut.LadenAsync(pluginMock.Object, "https://example.com/repo.git", currentWorkingDirectory: "backend", cts.Token);
        cts.Cancel();

        await loadTask;

        sut.IsLoadingDirectoryStructure.Should().BeFalse();
        sut.AvailableWorkingDirectories.Should().BeEmpty();
        sut.SelectedWorkingDirectory.Should().BeNull();
    }

    /// <summary>BestaetigenCommand löst CloseRequested mit true aus.</summary>
    [Fact]
    public void BestaetigenCommand_ShouldRaiseCloseRequested_WithTrue()
    {
        var sut = CreateSut();
        bool? result = null;
        sut.CloseRequested += (_, confirmed) => result = confirmed;

        sut.BestaetigenCommand.Execute(null);

        result.Should().BeTrue();
    }

    /// <summary>Absolute manuelle Pfade werden abgelehnt.</summary>
    [Fact]
    public async Task BestaetigenCommand_ShouldRejectAbsoluteManualWorkingDirectory()
    {
        var sut = CreateSut(CreateDirectoryStructureService());
        await sut.LadenAsync(gitPlugin: null, repositoryUrl: "https://example.com/repo.git", currentWorkingDirectory: null);
        sut.WorkingDirectoryInputText = "C:\\temp";
        bool? result = null;
        sut.CloseRequested += (_, confirmed) => result = confirmed;

        sut.BestaetigenCommand.CanExecute(null).Should().BeFalse();
        sut.BestaetigenCommand.Execute(null);

        result.Should().BeNull();
        sut.WorkingDirectoryInputError.Should().NotBeNullOrWhiteSpace();
    }

    /// <summary>Traversal-Segmente werden in manuellen Pfaden abgelehnt.</summary>
    [Fact]
    public async Task BestaetigenCommand_ShouldRejectTraversalManualWorkingDirectory()
    {
        var sut = CreateSut(CreateDirectoryStructureService());
        await sut.LadenAsync(gitPlugin: null, repositoryUrl: "https://example.com/repo.git", currentWorkingDirectory: null);
        sut.WorkingDirectoryInputText = "../backend";
        bool? result = null;
        sut.CloseRequested += (_, confirmed) => result = confirmed;

        sut.BestaetigenCommand.CanExecute(null).Should().BeFalse();
        sut.BestaetigenCommand.Execute(null);

        result.Should().BeNull();
        sut.WorkingDirectoryInputError.Should().Contain("..");
    }

    /// <summary>AbbrechenCommand löst CloseRequested mit false aus.</summary>
    [Fact]
    public void AbbrechenCommand_ShouldRaiseCloseRequested_WithFalse()
    {
        var sut = CreateSut();
        bool? result = null;
        sut.CloseRequested += (_, confirmed) => result = confirmed;

        sut.AbbrechenCommand.Execute(null);

        result.Should().BeFalse();
    }
}
