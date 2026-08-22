using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Fokussierte Tests für die Git-/Verzeichnis-Provisionierung eines Unteragenten durch <see cref="UnteragentGitProvisioningService"/>, isoliert vom <see cref="ProjektleiterAgentService"/>.</summary>
public sealed class UnteragentGitProvisioningServiceTests : IDisposable
{
    private readonly Mock<ICliRunner> _cliRunnerMock;
    private readonly UnteragentGitProvisioningService _sut;
    private readonly string _testRoot;
    private readonly string _repoMainPfad;

    /// <summary>UnteragentGitProvisioningServiceTests.</summary>
    public UnteragentGitProvisioningServiceTests()
    {
        _cliRunnerMock = new Mock<ICliRunner>();
        _sut = new UnteragentGitProvisioningService(_cliRunnerMock.Object, NullLogger<UnteragentGitProvisioningService>.Instance);

        _testRoot = Path.Combine(Path.GetTempPath(), "SoftwareschmiedeTests", "UnteragentGitProvisioning", Guid.NewGuid().ToString("N"));
        _repoMainPfad = Path.Combine(_testRoot, "clones", "repo_main");
        Directory.CreateDirectory(_repoMainPfad);
    }

    /// <summary>Dispose.</summary>
    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
        {
            Directory.Delete(_testRoot, recursive: true);
        }
    }

    /// <summary>ProvisioniereAsync erstellt das Arbeitsverzeichnis, legt den Branch an und klont den Branch in den Zielpfad.</summary>
    [Fact]
    public async Task ProvisioniereAsync_ErstelltVerzeichnisBranchUndKlon()
    {
        _cliRunnerMock
            .Setup(r => r.RunAsync("git", It.Is<IEnumerable<string>>(a => a.Contains("branch")), _repoMainPfad, It.IsAny<IDictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, string.Empty, string.Empty));
        _cliRunnerMock
            .Setup(r => r.RunAsync("git", It.Is<IEnumerable<string>>(a => a.Contains("clone")), It.IsAny<string?>(), It.IsAny<IDictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .Callback<string, IEnumerable<string>, string?, IDictionary<string, string>?, CancellationToken>((_, args, _, _, _) =>
            {
                Directory.CreateDirectory(args.Last());
            })
            .ReturnsAsync(new CliResult(0, string.Empty, string.Empty));

        var unteragent = ProjektleiterAgentServiceTestDatenFactory.ErstelleUnteragent(_testRoot, Guid.NewGuid());

        await _sut.ProvisioniereAsync(unteragent, _repoMainPfad);

        Directory.Exists(unteragent.VerzeichnisPfad).Should().BeTrue();
        Directory.Exists(unteragent.GitArbeitsbereich.ClonePfad).Should().BeTrue();
        _cliRunnerMock.Verify(r => r.RunAsync("git", It.Is<IEnumerable<string>>(a => a.Contains("branch") && a.Contains(unteragent.GitArbeitsbereich.BranchName)), _repoMainPfad, It.IsAny<IDictionary<string, string>?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>ProvisioniereAsync wirft eine InvalidOperationException, wenn die Branch-Erstellung fehlschlägt, und klont nicht.</summary>
    [Fact]
    public async Task ProvisioniereAsync_WirftBeiFehlgeschlagenerBranchErstellung()
    {
        _cliRunnerMock
            .Setup(r => r.RunAsync("git", It.Is<IEnumerable<string>>(a => a.Contains("branch")), It.IsAny<string?>(), It.IsAny<IDictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(1, string.Empty, "fatal: Branch existiert bereits"));

        var unteragent = ProjektleiterAgentServiceTestDatenFactory.ErstelleUnteragent(_testRoot, Guid.NewGuid());

        var akt = () => _sut.ProvisioniereAsync(unteragent, _repoMainPfad);

        (await akt.Should().ThrowAsync<InvalidOperationException>()).WithMessage($"*{unteragent.GitArbeitsbereich.BranchName}*");
        _cliRunnerMock.Verify(r => r.RunAsync("git", It.Is<IEnumerable<string>>(a => a.Contains("clone")), It.IsAny<string?>(), It.IsAny<IDictionary<string, string>?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>ProvisioniereAsync wirft eine InvalidOperationException, wenn der Git-Klon fehlschlägt.</summary>
    [Fact]
    public async Task ProvisioniereAsync_WirftBeiFehlgeschlagenemKlon()
    {
        _cliRunnerMock
            .Setup(r => r.RunAsync("git", It.Is<IEnumerable<string>>(a => a.Contains("branch")), It.IsAny<string?>(), It.IsAny<IDictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, string.Empty, string.Empty));
        _cliRunnerMock
            .Setup(r => r.RunAsync("git", It.Is<IEnumerable<string>>(a => a.Contains("clone")), It.IsAny<string?>(), It.IsAny<IDictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(1, string.Empty, "fatal: Klon fehlgeschlagen"));

        var unteragent = ProjektleiterAgentServiceTestDatenFactory.ErstelleUnteragent(_testRoot, Guid.NewGuid());

        var akt = () => _sut.ProvisioniereAsync(unteragent, _repoMainPfad);

        await akt.Should().ThrowAsync<InvalidOperationException>();
    }
}
