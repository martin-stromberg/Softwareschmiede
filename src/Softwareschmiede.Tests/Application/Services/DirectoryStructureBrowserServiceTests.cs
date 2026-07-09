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
        mock.Setup(p => p.GetRepositoryStructureAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries);
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
            p => p.GetRepositoryStructureAsync("https://example.com/repo.git", 3, It.IsAny<CancellationToken>()),
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
            p => p.GetRepositoryStructureAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>Bei einem Fehler des Plugins wird eine leere Liste zurückgegeben, ohne dass eine Exception propagiert wird.</summary>
    [Fact]
    public async Task GetDirectoriesAsync_ShouldHandleErrors_Gracefully()
    {
        var pluginMock = new Mock<IGitPlugin>();
        pluginMock.Setup(p => p.GetRepositoryStructureAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Verbindungsfehler"));
        var sut = CreateSut();

        var act = () => sut.GetDirectoriesAsync(pluginMock.Object, "https://example.com/repo.git");

        (await act.Should().NotThrowAsync()).Which.Should().BeEmpty();
    }
}
