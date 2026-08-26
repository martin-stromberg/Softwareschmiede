using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Tests für <see cref="DirectoryStructureBrowserService"/>.</summary>
public sealed class DirectoryStructureBrowserServiceTests : IDisposable
{
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());

    private DirectoryStructureBrowserService CreateSut(DirectoryStructureOptions? options = null) =>
        new(_cache, Options.Create(options ?? new DirectoryStructureOptions()), NullLogger<DirectoryStructureBrowserService>.Instance);

    /// <summary>Dispose.</summary>
    public void Dispose() => _cache.Dispose();

    private static Mock<IGitPlugin> CreatePluginMock(IEnumerable<RepositoryDirectoryEntry> entries)
    {
        var mock = new Mock<IGitPlugin>();
        mock.Setup(p => p.PluginPrefix).Returns("Test");
        mock.Setup(p => p.GetRepositoryStructureAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries);
        mock.Setup(p => p.GetRepositoryStructureLoadResultAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RepositoryStructureLoadResult.Success(entries));
        return mock;
    }

    /// <summary>GetDirectoriesAsync gibt die vom Plugin gelieferten Verzeichnisse zurück (Dateien werden ausgefiltert).</summary>
    [Fact]
    public async Task GetDirectoriesAsync_ShouldReturnDirectories()
    {
        var pluginMock = CreatePluginMock(
        [
            new RepositoryDirectoryEntry("backend", IsDirectory: true),
            new RepositoryDirectoryEntry("README.md", IsDirectory: false),
            new RepositoryDirectoryEntry("frontend", IsDirectory: true),
        ]);
        var sut = CreateSut();

        var result = await sut.GetDirectoriesAsync(pluginMock.Object, "https://example.com/repo.git");

        result.Should().BeEquivalentTo(["backend", "frontend"]);
    }

    /// <summary>GetDirectoriesAsync ruft IGitPlugin.GetRepositoryStructureAsync mit der Repository-URL und der konfigurierten MaxDepth auf.</summary>
    [Fact]
    public async Task GetDirectoriesAsync_ShouldCallPluginMethod()
    {
        var pluginMock = CreatePluginMock([new RepositoryDirectoryEntry("src", IsDirectory: true)]);
        var sut = CreateSut(new DirectoryStructureOptions { MaxDepth = 3 });

        await sut.GetDirectoriesAsync(pluginMock.Object, "https://example.com/repo.git");

        pluginMock.Verify(
            p => p.GetRepositoryStructureLoadResultAsync("https://example.com/repo.git", 3, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>Ein zweiter Abruf innerhalb der TTL kommt aus dem Cache, ohne das Plugin erneut aufzurufen.</summary>
    [Fact]
    public async Task GetDirectoriesAsync_ShouldCache_WithTTL()
    {
        var pluginMock = CreatePluginMock([new RepositoryDirectoryEntry("src", IsDirectory: true)]);
        var sut = CreateSut();

        var first = await sut.GetDirectoriesAsync(pluginMock.Object, "https://example.com/repo.git");
        var second = await sut.GetDirectoriesAsync(pluginMock.Object, "https://example.com/repo.git");

        first.Should().BeEquivalentTo(second);
        pluginMock.Verify(
            p => p.GetRepositoryStructureLoadResultAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>Bei einem Fehler des Plugins wird eine leere Liste zurückgegeben, ohne dass eine Exception propagiert wird.</summary>
    [Fact]
    public async Task GetDirectoriesAsync_ShouldHandleErrors_Gracefully()
    {
        var pluginMock = new Mock<IGitPlugin>();
        pluginMock.Setup(p => p.PluginPrefix).Returns("Test");
        pluginMock.Setup(p => p.GetRepositoryStructureLoadResultAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RepositoryStructureLoadResult.Failed("Verbindungsfehler"));
        var sut = CreateSut();

        var act = () => sut.GetDirectoriesAsync(pluginMock.Object, "https://example.com/repo.git");

        (await act.Should().NotThrowAsync()).Which.Should().BeEmpty();
    }

    /// <summary>Ein leerer erfolgreicher Plugin-Result bleibt ein Erfolg.</summary>
    [Fact]
    public async Task GetDirectoryLoadResultAsync_ShouldReturnSuccess_ForEmptyRepository()
    {
        var pluginMock = CreatePluginMock([]);
        var sut = CreateSut();

        var result = await sut.GetDirectoryLoadResultAsync(pluginMock.Object, "https://example.com/repo.git");

        result.Status.Should().Be(RepositoryStructureLoadStatus.Success);
        result.Entries.Should().BeEmpty();
    }

    /// <summary>Plugin-Exceptions werden als Fehlerstatus gemeldet und nicht als leerer Erfolg.</summary>
    [Fact]
    public async Task GetDirectoryLoadResultAsync_ShouldReturnFailed_WhenPluginThrows()
    {
        var pluginMock = new Mock<IGitPlugin>();
        pluginMock.Setup(p => p.PluginPrefix).Returns("Test");
        pluginMock.Setup(p => p.GetRepositoryStructureLoadResultAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Verbindungsfehler"));
        var sut = CreateSut();

        var result = await sut.GetDirectoryLoadResultAsync(pluginMock.Object, "https://example.com/repo.git");

        result.Status.Should().Be(RepositoryStructureLoadStatus.Failed);
    }

    /// <summary>Cache-Keys berücksichtigen Plugin-Prefix und MaxDepth.</summary>
    [Fact]
    public async Task GetDirectoryLoadResultAsync_ShouldUsePluginPrefixAndMaxDepthInCacheKey()
    {
        var pluginA = CreatePluginMock([new RepositoryDirectoryEntry("a", IsDirectory: true)]);
        pluginA.Setup(p => p.PluginPrefix).Returns("A");
        var pluginB = CreatePluginMock([new RepositoryDirectoryEntry("b", IsDirectory: true)]);
        pluginB.Setup(p => p.PluginPrefix).Returns("B");
        var sut = CreateSut(new DirectoryStructureOptions { MaxDepth = 3 });

        var first = await sut.GetDirectoriesAsync(pluginA.Object, "https://example.com/repo.git");
        var second = await sut.GetDirectoriesAsync(pluginB.Object, "https://example.com/repo.git");

        first.Should().Equal("a");
        second.Should().Equal("b");
    }

    /// <summary>GetFileLoadResultAsync liefert nur Datei-Einträge; Verzeichnisse werden ausgefiltert.</summary>
    [Fact]
    public async Task GetFileLoadResultAsync_ShouldReturnOnlyFiles_ForMixedEntries()
    {
        var pluginMock = CreatePluginMock(
        [
            new RepositoryDirectoryEntry("backend", IsDirectory: true),
            new RepositoryDirectoryEntry("README.md", IsDirectory: false),
            new RepositoryDirectoryEntry("init.sh", IsDirectory: false),
        ]);
        var sut = CreateSut();

        var result = await sut.GetFileLoadResultAsync(pluginMock.Object, "https://example.com/repo.git");

        result.Status.Should().Be(RepositoryStructureLoadStatus.Success);
        result.Entries.Select(entry => entry.Path).Should().BeEquivalentTo(["README.md", "init.sh"]);
    }

    /// <summary>Plugin-Exceptions werden bei GetFileLoadResultAsync als Fehlerstatus gemeldet.</summary>
    [Fact]
    public async Task GetFileLoadResultAsync_ShouldReturnFailed_WhenPluginThrows()
    {
        var pluginMock = new Mock<IGitPlugin>();
        pluginMock.Setup(p => p.PluginPrefix).Returns("Test");
        pluginMock.Setup(p => p.GetRepositoryStructureLoadResultAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Verbindungsfehler"));
        var sut = CreateSut();

        var result = await sut.GetFileLoadResultAsync(pluginMock.Object, "https://example.com/repo.git");

        result.Status.Should().Be(RepositoryStructureLoadStatus.Failed);
    }

    /// <summary>GetDirectoryLoadResultAsync und GetFileLoadResultAsync verwenden für dieselbe Repository-URL unabhängige Cache-Einträge.</summary>
    [Fact]
    public async Task GetDirectoryLoadResultAsync_And_GetFileLoadResultAsync_ShouldUseIndependentCacheEntries()
    {
        var pluginMock = CreatePluginMock(
        [
            new RepositoryDirectoryEntry("backend", IsDirectory: true),
            new RepositoryDirectoryEntry("README.md", IsDirectory: false),
        ]);
        var sut = CreateSut();

        var dirsFirst = await sut.GetDirectoryLoadResultAsync(pluginMock.Object, "https://example.com/repo.git");
        var filesFirst = await sut.GetFileLoadResultAsync(pluginMock.Object, "https://example.com/repo.git");
        var dirsSecond = await sut.GetDirectoryLoadResultAsync(pluginMock.Object, "https://example.com/repo.git");
        var filesSecond = await sut.GetFileLoadResultAsync(pluginMock.Object, "https://example.com/repo.git");

        dirsFirst.Entries.Select(entry => entry.Path).Should().BeEquivalentTo(["backend"]);
        filesFirst.Entries.Select(entry => entry.Path).Should().BeEquivalentTo(["README.md"]);
        dirsSecond.Should().BeSameAs(dirsFirst);
        filesSecond.Should().BeSameAs(filesFirst);
        pluginMock.Verify(
            p => p.GetRepositoryStructureLoadResultAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    /// <summary>GetFileLoadResultAsync reicht einen übergebenen Branch-Namen an das Git-Plugin durch.</summary>
    [Fact]
    public async Task GetFileLoadResultAsync_ShouldPassBranchNameToPlugin()
    {
        var pluginMock = CreatePluginMock([new RepositoryDirectoryEntry("init.sh", IsDirectory: false)]);
        var sut = CreateSut();

        await sut.GetFileLoadResultAsync(pluginMock.Object, "https://example.com/repo.git", ct: default, branchName: "develop");

        pluginMock.Verify(
            p => p.GetRepositoryStructureLoadResultAsync("https://example.com/repo.git", It.IsAny<int>(), It.IsAny<CancellationToken>(), "develop"),
            Times.Once);
    }

    /// <summary>Abrufe mit unterschiedlichem Branch-Namen verwenden unabhängige Cache-Einträge und rufen das Plugin jeweils erneut auf.</summary>
    [Fact]
    public async Task GetFileLoadResultAsync_ShouldUseBranchNameInCacheKey()
    {
        var pluginMock = new Mock<IGitPlugin>();
        pluginMock.Setup(p => p.PluginPrefix).Returns("Test");
        pluginMock.Setup(p => p.GetRepositoryStructureLoadResultAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>(), "main"))
            .ReturnsAsync(RepositoryStructureLoadResult.Success([new RepositoryDirectoryEntry("main.sh", IsDirectory: false)]));
        pluginMock.Setup(p => p.GetRepositoryStructureLoadResultAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>(), "develop"))
            .ReturnsAsync(RepositoryStructureLoadResult.Success([new RepositoryDirectoryEntry("develop.sh", IsDirectory: false)]));
        var sut = CreateSut();

        var main = await sut.GetFileLoadResultAsync(pluginMock.Object, "https://example.com/repo.git", ct: default, branchName: "main");
        var develop = await sut.GetFileLoadResultAsync(pluginMock.Object, "https://example.com/repo.git", ct: default, branchName: "develop");

        main.Entries.Select(entry => entry.Path).Should().BeEquivalentTo(["main.sh"]);
        develop.Entries.Select(entry => entry.Path).Should().BeEquivalentTo(["develop.sh"]);
        pluginMock.Verify(
            p => p.GetRepositoryStructureLoadResultAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>(), It.IsAny<string>()),
            Times.Exactly(2));
    }
}
