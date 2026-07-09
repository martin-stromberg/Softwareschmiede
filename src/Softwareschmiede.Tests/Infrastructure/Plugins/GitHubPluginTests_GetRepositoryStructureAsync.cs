using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Infrastructure.Plugins;

namespace Softwareschmiede.Tests.Infrastructure.Plugins;

/// <summary>
/// Tests für <see cref="GitHubPlugin.GetRepositoryStructureAsync"/> — ruft die Verzeichnisstruktur rein remote
/// über die GitHub Git-Trees-API ab (kein lokaler Klon erforderlich), damit eine Unterverzeichnis-Auswahl bereits
/// vor dem Klon möglich ist (Issue #98, zweiter Nacharbeits-Zyklus).
/// </summary>
public sealed class GitHubPluginTests_GetRepositoryStructureAsync
{
    private readonly Mock<ICliRunner> _cliRunnerMock;
    private readonly Mock<ICredentialStore> _credentialStoreMock;
    private readonly GitHubPlugin _sut;

    /// <summary>GitHubPluginTests_GetRepositoryStructureAsync.</summary>
    public GitHubPluginTests_GetRepositoryStructureAsync()
    {
        _cliRunnerMock = new Mock<ICliRunner>();
        _credentialStoreMock = new Mock<ICredentialStore>();
        _sut = new GitHubPlugin(
            _cliRunnerMock.Object,
            _credentialStoreMock.Object,
            new Mock<ILogger<GitHubPlugin>>().Object);
    }

    private void SetupDefaultBranch(string branch = "main")
    {
        _cliRunnerMock.Setup(c => c.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(args => args.Contains("ls-remote")),
                null,
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, $"ref: refs/heads/{branch}\tHEAD\n", string.Empty));
    }

    /// <summary>
    /// Ruft die Verzeichnisstruktur über die gh-Trees-API ab und filtert Verzeichniseinträge bis zur
    /// konfigurierten Tiefe, ohne Dateien einzubeziehen.
    /// </summary>
    [Fact]
    public async Task GetRepositoryStructureAsync_ShouldReturnDirectories_UpToMaxDepth()
    {
        SetupDefaultBranch();

        const string treeJson = """
            {
                "sha": "abc123",
                "truncated": false,
                "tree": [
                    { "path": "backend", "type": "tree" },
                    { "path": "backend/src", "type": "tree" },
                    { "path": "backend/src/too-deep", "type": "tree" },
                    { "path": "backend/README.md", "type": "blob" },
                    { "path": "frontend", "type": "tree" }
                ]
            }
            """;
        _cliRunnerMock.Setup(c => c.RunAsync(
                "gh",
                It.Is<IEnumerable<string>>(args => args.Any(a => a.Contains("git/trees/main"))),
                null,
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, treeJson, string.Empty));

        var result = (await _sut.GetRepositoryStructureAsync("https://github.com/owner/repo", maxDepth: 2)).ToList();

        var paths = result.Select(e => e.Path).ToList();
        paths.Should().Contain(["backend", "frontend", "backend/src"]);
        paths.Should().NotContain("backend/src/too-deep");
        paths.Should().NotContain("backend/README.md");
        result.Should().OnlyContain(e => e.IsDirectory);
    }

    /// <summary>Wenn der gh-Aufruf fehlschlägt, wird eine leere Liste zurückgegeben statt einer Exception.</summary>
    [Fact]
    public async Task GetRepositoryStructureAsync_ShouldReturnEmpty_WhenGhApiFails()
    {
        SetupDefaultBranch();
        _cliRunnerMock.Setup(c => c.RunAsync(
                "gh",
                It.Is<IEnumerable<string>>(args => args.Any(a => a.Contains("git/trees/"))),
                null,
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(1, string.Empty, "HTTP 404: Not Found"));

        var result = await _sut.GetRepositoryStructureAsync("https://github.com/owner/repo", maxDepth: 2);

        result.Should().BeEmpty();
    }

    /// <summary>Eine leere/ungültige Repository-URL, aus der keine Repository-ID extrahiert werden kann, liefert eine leere Liste.</summary>
    [Fact]
    public async Task GetRepositoryStructureAsync_ShouldReturnEmpty_ForUnparsableUrl()
    {
        var result = await _sut.GetRepositoryStructureAsync(string.Empty, maxDepth: 2);

        result.Should().BeEmpty();
        _cliRunnerMock.Verify(c => c.RunAsync(
            "gh",
            It.IsAny<IEnumerable<string>>(),
            It.IsAny<string?>(),
            It.IsAny<IDictionary<string, string>?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Eine SSH-Repository-URL (<c>git@github.com:owner/repo.git</c>) wird korrekt in die Repository-ID
    /// <c>owner/repo</c> aufgelöst.
    /// </summary>
    [Fact]
    public async Task GetRepositoryStructureAsync_ShouldResolveRepositoryId_FromSshUrl()
    {
        SetupDefaultBranch();
        const string treeJson = """{ "tree": [ { "path": "src", "type": "tree" } ] }""";
        _cliRunnerMock.Setup(c => c.RunAsync(
                "gh",
                It.Is<IEnumerable<string>>(args => args.Any(a => a == "repos/owner/repo/git/trees/main?recursive=1")),
                null,
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, treeJson, string.Empty));

        var result = await _sut.GetRepositoryStructureAsync("git@github.com:owner/repo.git", maxDepth: 2);

        result.Select(e => e.Path).Should().Contain("src");
    }

    /// <summary>
    /// Eine HTTPS-URL mit abschließendem Slash (z. B. aus dem Browser kopiert) wird korrekt in die
    /// Repository-ID aufgelöst, statt fälschlich als unparsbar behandelt zu werden (Regressionstest für einen
    /// im Code-Review gefundenen Bug: die ursprüngliche String-Slicing-Logik interpretierte bei einem
    /// abschließenden Slash "repo" als leeren String und "owner" fälschlich als "repo").
    /// </summary>
    [Fact]
    public async Task GetRepositoryStructureAsync_ShouldResolveRepositoryId_FromUrlWithTrailingSlash()
    {
        SetupDefaultBranch();
        const string treeJson = """{ "tree": [ { "path": "src", "type": "tree" } ] }""";
        _cliRunnerMock.Setup(c => c.RunAsync(
                "gh",
                It.Is<IEnumerable<string>>(args => args.Any(a => a == "repos/owner/repo/git/trees/main?recursive=1")),
                null,
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, treeJson, string.Empty));

        var result = await _sut.GetRepositoryStructureAsync("https://github.com/owner/repo/", maxDepth: 2);

        result.Select(e => e.Path).Should().Contain("src");
    }

    /// <summary>Ein <c>truncated: true</c>-Flag in der API-Antwort führt nicht zu einer Exception; die (unvollständige) Struktur wird trotzdem zurückgegeben.</summary>
    [Fact]
    public async Task GetRepositoryStructureAsync_ShouldNotThrow_WhenResponseIsTruncated()
    {
        SetupDefaultBranch();
        const string treeJson = """
            {
                "truncated": true,
                "tree": [ { "path": "backend", "type": "tree" } ]
            }
            """;
        _cliRunnerMock.Setup(c => c.RunAsync(
                "gh",
                It.Is<IEnumerable<string>>(args => args.Any(a => a.Contains("git/trees/"))),
                null,
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, treeJson, string.Empty));

        var result = await _sut.GetRepositoryStructureAsync("https://github.com/owner/repo", maxDepth: 2);

        result.Select(e => e.Path).Should().Contain("backend");
    }

    /// <summary>Ungültiges JSON in der API-Antwort führt zu einer leeren Liste statt einer unbehandelten Exception.</summary>
    [Fact]
    public async Task GetRepositoryStructureAsync_ShouldReturnEmpty_ForMalformedJson()
    {
        SetupDefaultBranch();
        _cliRunnerMock.Setup(c => c.RunAsync(
                "gh",
                It.Is<IEnumerable<string>>(args => args.Any(a => a.Contains("git/trees/"))),
                null,
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, "not-json", string.Empty));

        var result = await _sut.GetRepositoryStructureAsync("https://github.com/owner/repo", maxDepth: 2);

        result.Should().BeEmpty();
    }
}
