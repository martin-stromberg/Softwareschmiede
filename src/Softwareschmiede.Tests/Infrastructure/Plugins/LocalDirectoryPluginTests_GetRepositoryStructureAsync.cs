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

    private static LocalDirectoryPlugin CreateSutWithCancellingEnumerator(CancellationTokenSource cts) =>
        new(
            new Mock<ICliRunner>(MockBehavior.Strict).Object,
            new Mock<ICredentialStore>().Object,
            NullLogger<LocalDirectoryPlugin>.Instance,
            path =>
            {
                cts.Cancel();
                return Directory.EnumerateDirectories(path);
            });

    private static LocalDirectoryPlugin CreateSutWithThrowingFileEnumerator(string throwingDirectoryPath) =>
        new(
            new Mock<ICliRunner>(MockBehavior.Strict).Object,
            new Mock<ICredentialStore>().Object,
            NullLogger<LocalDirectoryPlugin>.Instance,
            Directory.EnumerateDirectories,
            path => path == throwingDirectoryPath
                ? throw new IOException("Simulierter Zugriffsfehler bei der Datei-Enumeration.")
                : Directory.EnumerateFiles(path));

    /// <summary>
    /// Liefert die Unterverzeichnisse und Dateien bis zur konfigurierten Tiefe als relative Pfade mit '/' als
    /// Trenner, mit korrekt gesetztem <see cref="RepositoryDirectoryEntry.IsDirectory"/>. Regressionstest: die
    /// ursprüngliche Implementierung rekursierte ausschließlich über Unterverzeichnisse und lieferte nie
    /// Datei-Einträge (z. B. Initialisierungsskripte tauchten dadurch nie in der Vorschlagsliste auf).
    /// </summary>
    [Fact]
    public async Task GetRepositoryStructureAsync_ShouldReturnFilesAndDirectories_UpToMaxDepth()
    {
        var root = Directory.CreateTempSubdirectory().FullName;
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "backend"));
            Directory.CreateDirectory(Path.Combine(root, "backend", "src"));
            Directory.CreateDirectory(Path.Combine(root, "backend", "src", "too-deep"));
            Directory.CreateDirectory(Path.Combine(root, "frontend"));
            File.WriteAllText(Path.Combine(root, "backend", "README.md"), "hello");
            File.WriteAllText(Path.Combine(root, "backend", "src", "too-deep", "ignored.txt"), "hello");
            var sut = CreateSut();

            var result = (await sut.GetRepositoryStructureAsync(root, maxDepth: 2)).ToList();

            var paths = result.Select(e => e.Path).ToList();
            paths.Should().Contain(["backend", "frontend", "backend/src", "backend/README.md"]);
            paths.Should().NotContain("backend/src/too-deep");
            paths.Should().NotContain("backend/src/too-deep/ignored.txt");
            result.Single(e => e.Path == "backend/README.md").IsDirectory.Should().BeFalse();
            result.Where(e => e.Path is "backend" or "frontend" or "backend/src").Should().OnlyContain(e => e.IsDirectory);
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
    /// (Code-Review-Befund: bislang war nur der Vorab-Abbruch getestet). Der Abbruch wird deterministisch
    /// über einen injizierten Verzeichnis-Enumerator ausgelöst statt über ein Wall-Clock-Zeitfenster.
    /// </summary>
    [Fact]
    public async Task GetRepositoryStructureAsync_ShouldThrow_WhenCancelledDuringTraversal()
    {
        var root = Directory.CreateTempSubdirectory().FullName;
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "dir-0"));
            Directory.CreateDirectory(Path.Combine(root, "dir-1"));

            using var cts = new CancellationTokenSource();
            var sut = CreateSutWithCancellingEnumerator(cts);

            var act = () => sut.GetRepositoryStructureAsync(root, maxDepth: 2, cts.Token);

            await act.Should().ThrowAsync<OperationCanceledException>();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>
    /// Schlägt die Datei-Enumeration eines Unterverzeichnisses mit <see cref="IOException"/> fehl (z. B. wegen
    /// verweigerter Zugriffsberechtigung), wird dieses Verzeichnis für Dateien übersprungen statt die gesamte
    /// Traversierung abzubrechen — analog zum bestehenden Verhalten bei einer fehlschlagenden
    /// Verzeichnis-Enumeration. Deckt den zuvor ungetesteten Fehlerpfad um <c>Directory.EnumerateFiles</c> ab
    /// (Code-Review-Befund), der erst durch den injizierbaren Datei-Enumerator-Seam testbar wurde.
    /// </summary>
    [Fact]
    public async Task GetRepositoryStructureAsync_ShouldSkipFilesOfDirectory_WhenFileEnumerationThrows()
    {
        var root = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var okDir = Path.Combine(root, "ok");
            var badDir = Path.Combine(root, "bad");
            Directory.CreateDirectory(okDir);
            Directory.CreateDirectory(badDir);
            File.WriteAllText(Path.Combine(okDir, "keep.txt"), "hello");
            File.WriteAllText(Path.Combine(root, "root-file.txt"), "hello");
            var sut = CreateSutWithThrowingFileEnumerator(badDir);

            var result = (await sut.GetRepositoryStructureAsync(root, maxDepth: 2)).ToList();

            var paths = result.Select(e => e.Path).ToList();
            paths.Should().Contain(["ok", "bad", "ok/keep.txt", "root-file.txt"]);
            paths.Should().NotContain(p => p.StartsWith("bad/", StringComparison.Ordinal));
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
