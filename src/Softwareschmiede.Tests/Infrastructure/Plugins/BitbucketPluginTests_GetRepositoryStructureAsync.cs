using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Infrastructure.Plugins;

namespace Softwareschmiede.Tests.Infrastructure.Plugins;

/// <summary>
/// Tests für <see cref="BitbucketPlugin.GetRepositoryStructureAsync"/> — ruft die Verzeichnisstruktur rein
/// remote über die Bitbucket-Source-API ab (kein lokaler Klon erforderlich), damit eine Unterverzeichnis-Auswahl
/// bereits vor dem Klon möglich ist (Issue #98, zweiter Nacharbeits-Zyklus).
/// </summary>
public sealed class BitbucketPluginTests_GetRepositoryStructureAsync
{
    private readonly Mock<ICliRunner> _cliRunnerMock;
    private readonly Mock<ICredentialStore> _credentialStoreMock;
    private readonly BitbucketPlugin _sut;

    /// <summary>BitbucketPluginTests_GetRepositoryStructureAsync.</summary>
    public BitbucketPluginTests_GetRepositoryStructureAsync()
    {
        _cliRunnerMock = new Mock<ICliRunner>();
        _credentialStoreMock = new Mock<ICredentialStore>();
        _sut = new BitbucketPlugin(
            _cliRunnerMock.Object,
            _credentialStoreMock.Object,
            new Mock<ILogger<BitbucketPlugin>>().Object);

        _credentialStoreMock.Setup(c => c.GetCredential("Softwareschmiede.Bitbucket.HostingMode")).Returns("Cloud");
        _credentialStoreMock.Setup(c => c.GetCredential(It.IsAny<string>())).Returns("test");
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
    /// Ruft die Verzeichnisstruktur über die Bitbucket-Cloud-Source-API ab und filtert Verzeichniseinträge
    /// (<c>type == "commit_directory"</c>) bis zur konfigurierten Tiefe, ohne Dateien einzubeziehen.
    /// </summary>
    [Fact]
    public async Task GetRepositoryStructureAsync_ShouldReturnDirectories_UpToMaxDepth()
    {
        SetupDefaultBranch();

        const string sourceJson = """
            {
                "values": [
                    { "path": "backend", "type": "commit_directory" },
                    { "path": "backend/src", "type": "commit_directory" },
                    { "path": "backend/src/too-deep", "type": "commit_directory" },
                    { "path": "backend/README.md", "type": "commit_file" },
                    { "path": "frontend", "type": "commit_directory" }
                ]
            }
            """;
        _cliRunnerMock.Setup(c => c.RunAsync(
                "curl",
                It.Is<IEnumerable<string>>(args => args.Any(a => a.Contains("/src/main/"))),
                null,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, sourceJson, string.Empty));

        var result = (await _sut.GetRepositoryStructureAsync("https://bitbucket.org/workspace/repo", maxDepth: 2)).ToList();

        var paths = result.Select(e => e.Path).ToList();
        paths.Should().Contain(["backend", "frontend", "backend/src"]);
        paths.Should().NotContain("backend/src/too-deep");
        paths.Should().NotContain("backend/README.md");
        result.Should().OnlyContain(e => e.IsDirectory);
    }

    /// <summary>Folgt dem <c>next</c>-Link über mehrere Seiten und aggregiert die Verzeichniseinträge aller Seiten.</summary>
    [Fact]
    public async Task GetRepositoryStructureAsync_ShouldFollowPagination_ViaNextLink()
    {
        SetupDefaultBranch();

        const string page1Json = """
            {
                "values": [ { "path": "backend", "type": "commit_directory" } ],
                "next": "https://api.bitbucket.org/2.0/repositories/workspace/repo/src/main/?page=2"
            }
            """;
        const string page2Json = """
            {
                "values": [ { "path": "frontend", "type": "commit_directory" } ]
            }
            """;

        _cliRunnerMock.Setup(c => c.RunAsync(
                "curl",
                It.Is<IEnumerable<string>>(args => args.Any(a => a.Contains("/src/main/") && !a.Contains("page="))),
                null,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, page1Json, string.Empty));

        _cliRunnerMock.Setup(c => c.RunAsync(
                "curl",
                It.Is<IEnumerable<string>>(args => args.Any(a => a.Contains("page=2"))),
                null,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, page2Json, string.Empty));

        var result = (await _sut.GetRepositoryStructureAsync("https://bitbucket.org/workspace/repo", maxDepth: 2)).ToList();

        result.Select(e => e.Path).Should().BeEquivalentTo(["backend", "frontend"]);
    }

    /// <summary>Wenn der curl-Aufruf fehlschlägt, wird eine leere Liste zurückgegeben statt einer Exception.</summary>
    [Fact]
    public async Task GetRepositoryStructureAsync_ShouldReturnEmpty_WhenApiCallFails()
    {
        SetupDefaultBranch();
        _cliRunnerMock.Setup(c => c.RunAsync(
                "curl",
                It.Is<IEnumerable<string>>(args => args.Any(a => a.Contains("/src/main/"))),
                null,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(1, string.Empty, "connection failed"));

        var result = await _sut.GetRepositoryStructureAsync("https://bitbucket.org/workspace/repo", maxDepth: 2);

        result.Should().BeEmpty();
    }

    /// <summary>Ein Bitbucket-API-Fehlerobjekt (<c>errors: [...]</c>) führt zu einer leeren Liste, nicht zu einer Exception.</summary>
    [Fact]
    public async Task GetRepositoryStructureAsync_ShouldReturnEmpty_WhenApiReturnsErrorPayload()
    {
        SetupDefaultBranch();
        const string errorJson = """{ "errors": [ { "message": "Repository not found" } ] }""";
        _cliRunnerMock.Setup(c => c.RunAsync(
                "curl",
                It.Is<IEnumerable<string>>(args => args.Any(a => a.Contains("/src/main/"))),
                null,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, errorJson, string.Empty));

        var result = await _sut.GetRepositoryStructureAsync("https://bitbucket.org/workspace/repo", maxDepth: 2);

        result.Should().BeEmpty();
    }

    /// <summary>Eine leere Repository-URL, aus der keine Repository-ID extrahiert werden kann, liefert eine leere Liste, ohne die API aufzurufen.</summary>
    [Fact]
    public async Task GetRepositoryStructureAsync_ShouldReturnEmpty_ForUnparsableUrl()
    {
        var result = await _sut.GetRepositoryStructureAsync(string.Empty, maxDepth: 2);

        result.Should().BeEmpty();
        _cliRunnerMock.Verify(c => c.RunAsync(
            "curl",
            It.IsAny<IEnumerable<string>>(),
            It.IsAny<string?>(),
            It.IsAny<IDictionary<string, string>?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Im Self-Hosted-Modus wird die Verzeichnisstruktur über die Bitbucket-Server-Browse-API level-by-level
    /// abgerufen (verschachteltes <c>children.values</c>-Schema mit <c>type == "DIRECTORY"</c>, abweichend vom
    /// Cloud-Schema), bis zur konfigurierten Tiefe: der Wurzel-Aufruf liefert "backend" (Verzeichnis) und
    /// "README.md" (Datei, wird ignoriert), ein zweiter Aufruf für "backend" liefert dessen Unterverzeichnis
    /// "src". Regressionstest für einen im Code-Review gefundenen Bug: die ursprüngliche Implementierung
    /// nutzte fälschlich das Cloud-JSON-Schema für Self-Hosted-Antworten und lieferte dadurch in der Praxis
    /// immer eine leere Liste.
    /// </summary>
    [Fact]
    public async Task GetRepositoryStructureAsync_ShouldWalkDirectoryLevels_WhenHostingModeIsSelfHosted()
    {
        _credentialStoreMock.Setup(c => c.GetCredential("Softwareschmiede.Bitbucket.HostingMode")).Returns("SelfHosted");
        _credentialStoreMock.Setup(c => c.GetCredential("Softwareschmiede.Bitbucket.SelfHostedUrl")).Returns("https://bitbucket.example.com");
        SetupDefaultBranch();

        const string rootJson = """
            {
                "children": {
                    "isLastPage": true,
                    "values": [
                        { "path": { "name": "backend" }, "type": "DIRECTORY" },
                        { "path": { "name": "README.md" }, "type": "FILE" }
                    ]
                }
            }
            """;
        const string backendJson = """
            {
                "children": {
                    "isLastPage": true,
                    "values": [
                        { "path": { "name": "src" }, "type": "DIRECTORY" }
                    ]
                }
            }
            """;

        _cliRunnerMock.Setup(c => c.RunAsync(
                "curl",
                It.Is<IEnumerable<string>>(args => args.Any(a =>
                    a.StartsWith("https://bitbucket.example.com/rest/api/1.0/projects/PROJ/repos/repo/browse?at=", StringComparison.Ordinal))),
                null,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, rootJson, string.Empty));

        _cliRunnerMock.Setup(c => c.RunAsync(
                "curl",
                It.Is<IEnumerable<string>>(args => args.Any(a =>
                    a.StartsWith("https://bitbucket.example.com/rest/api/1.0/projects/PROJ/repos/repo/browse/backend?at=", StringComparison.Ordinal))),
                null,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, backendJson, string.Empty));

        var result = (await _sut.GetRepositoryStructureAsync("https://bitbucket.example.com/projects/PROJ/repos/repo/browse", maxDepth: 2)).ToList();

        var paths = result.Select(e => e.Path).ToList();
        paths.Should().Contain(["backend", "backend/src"]);
        paths.Should().NotContain("README.md");
        result.Should().OnlyContain(e => e.IsDirectory);
    }

    /// <summary>Ein fehlgeschlagener Browse-Aufruf im Self-Hosted-Modus liefert eine leere Liste statt einer Exception.</summary>
    [Fact]
    public async Task GetRepositoryStructureAsync_ShouldReturnEmpty_WhenSelfHostedBrowseFails()
    {
        _credentialStoreMock.Setup(c => c.GetCredential("Softwareschmiede.Bitbucket.HostingMode")).Returns("SelfHosted");
        _credentialStoreMock.Setup(c => c.GetCredential("Softwareschmiede.Bitbucket.SelfHostedUrl")).Returns("https://bitbucket.example.com");
        SetupDefaultBranch();

        _cliRunnerMock.Setup(c => c.RunAsync(
                "curl",
                It.Is<IEnumerable<string>>(args => args.Any(a => a.Contains("/browse"))),
                null,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(1, string.Empty, "connection refused"));

        var result = await _sut.GetRepositoryStructureAsync("https://bitbucket.example.com/projects/PROJ/repos/repo/browse", maxDepth: 2);

        result.Should().BeEmpty();
    }
}
