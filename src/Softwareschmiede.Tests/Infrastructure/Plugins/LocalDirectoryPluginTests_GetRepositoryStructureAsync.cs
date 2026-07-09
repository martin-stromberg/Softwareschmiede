using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Infrastructure.Plugins;

namespace Softwareschmiede.Tests.Infrastructure.Plugins;

/// <summary>Tests für <see cref="LocalDirectoryPlugin.GetRepositoryStructureAsync"/>.</summary>
public sealed class LocalDirectoryPluginTests_GetRepositoryStructureAsync
{
    private static LocalDirectoryPlugin CreateSut() =>
        new(
            new Mock<ICliRunner>(MockBehavior.Strict).Object,
            new Mock<ICredentialStore>().Object,
            NullLogger<LocalDirectoryPlugin>.Instance);

    /// <summary>Liefert die Unterverzeichnisse bis zur konfigurierten Tiefe als relative Pfade mit '/' als Trenner.</summary>
    [Fact]
    public async Task GetRepositoryStructureAsync_ShouldReturnDirectories_UpToMaxDepth()
    {
        var root = Directory.CreateTempSubdirectory().FullName;
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "backend"));
            Directory.CreateDirectory(Path.Combine(root, "backend", "src"));
            Directory.CreateDirectory(Path.Combine(root, "backend", "src", "too-deep"));
            Directory.CreateDirectory(Path.Combine(root, "frontend"));
            var sut = CreateSut();

            var result = await sut.GetRepositoryStructureAsync(root, maxDepth: 2);

            var paths = result.Select(e => e.Path).ToList();
            paths.Should().Contain(["backend", "frontend", "backend/src"]);
            paths.Should().NotContain("backend/src/too-deep");
            result.Should().OnlyContain(e => e.IsDirectory);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>Das .git-Verzeichnis wird von der Verzeichnisstruktur ausgeschlossen.</summary>
    [Fact]
    public async Task GetRepositoryStructureAsync_ShouldExcludeGitDirectory()
    {
        var root = Directory.CreateTempSubdirectory().FullName;
        try
        {
            Directory.CreateDirectory(Path.Combine(root, ".git"));
            Directory.CreateDirectory(Path.Combine(root, ".git", "hooks"));
            Directory.CreateDirectory(Path.Combine(root, "src"));
            var sut = CreateSut();

            var result = await sut.GetRepositoryStructureAsync(root, maxDepth: 2);

            result.Select(e => e.Path).Should().NotContain(p => p == ".git" || p.StartsWith(".git/", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>Für einen nicht existierenden Pfad wird eine leere Liste zurückgegeben, ohne eine Exception zu werfen.</summary>
    [Fact]
    public async Task GetRepositoryStructureAsync_ShouldReturnEmpty_ForNonExistentPath()
    {
        var sut = CreateSut();

        var result = await sut.GetRepositoryStructureAsync(@"C:\this-path-does-not-exist-12345", maxDepth: 2);

        result.Should().BeEmpty();
    }

    /// <summary>Für eine leere Repository-URL wird eine leere Liste zurückgegeben.</summary>
    [Fact]
    public async Task GetRepositoryStructureAsync_ShouldReturnEmpty_ForEmptyUrl()
    {
        var sut = CreateSut();

        var result = await sut.GetRepositoryStructureAsync(string.Empty, maxDepth: 2);

        result.Should().BeEmpty();
    }

    /// <summary>Ein bereits abgebrochenes CancellationToken führt zu einer OperationCanceledException.</summary>
    [Fact]
    public async Task GetRepositoryStructureAsync_ShouldThrow_WhenCancelledUpFront()
    {
        var root = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var sut = CreateSut();
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            var act = () => sut.GetRepositoryStructureAsync(root, maxDepth: 2, cts.Token);

            await act.Should().ThrowAsync<OperationCanceledException>();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>
    /// Ein CancellationToken, das erst während der laufenden Traversierung abgebrochen wird (nicht bereits
    /// vor dem Start), muss ebenfalls zu einer OperationCanceledException führen. Deckt die
    /// <c>ct.ThrowIfCancellationRequested()</c>-Prüfung innerhalb der Verzeichnis-Schleife ab
    /// (Code-Review-Befund: bislang war nur der Vorab-Abbruch getestet).
    /// </summary>
    [Fact]
    public async Task GetRepositoryStructureAsync_ShouldThrow_WhenCancelledDuringTraversal()
    {
        var root = Directory.CreateTempSubdirectory().FullName;
        try
        {
            // Großer, flacher Verzeichnisbaum, damit die Traversierung lange genug dauert, um den
            // Abbruch zuverlässig mitten in der Verarbeitung (statt vor dem Start) auszulösen.
            for (var i = 0; i < 3000; i++)
            {
                Directory.CreateDirectory(Path.Combine(root, $"dir-{i:D5}"));
            }

            var sut = CreateSut();
            using var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromMilliseconds(5));

            var act = () => sut.GetRepositoryStructureAsync(root, maxDepth: 2, cts.Token);

            await act.Should().ThrowAsync<OperationCanceledException>();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>Eine einzelne Ebene ohne Unterverzeichnisse liefert eine leere Struktur, wenn das Verzeichnis leer ist.</summary>
    [Fact]
    public async Task GetRepositoryStructureAsync_ShouldReturnEmpty_ForEmptyDirectory()
    {
        var root = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var sut = CreateSut();

            var result = await sut.GetRepositoryStructureAsync(root, maxDepth: 2);

            result.Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
