using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Infrastructure.Plugins;

namespace Softwareschmiede.Tests.Infrastructure.Plugins;

/// <summary>Tests for the Claude CLI plugin.</summary>
public sealed class ClaudeCliPluginTests : IDisposable
{
    private readonly Mock<ICliRunner> _cliRunnerMock;
    private readonly Mock<ICredentialStore> _credentialStoreMock;
    private readonly ClaudeCliPlugin _sut;
    private readonly string _testDirectory;

    public ClaudeCliPluginTests()
    {
        _cliRunnerMock = new Mock<ICliRunner>();
        _credentialStoreMock = new Mock<ICredentialStore>();
        _sut = new ClaudeCliPlugin(
            _cliRunnerMock.Object,
            _credentialStoreMock.Object,
            new Mock<ILogger<ClaudeCliPlugin>>().Object);
        _testDirectory = Path.Combine(Path.GetTempPath(), "softwareschmiede-test-" + Guid.NewGuid().ToString("N"));
    }

    /// <summary>Exposes static plugin metadata.</summary>
    [Fact]
    public void PluginMetadata_ShouldExposeExpectedValues()
    {
        _sut.PluginName.Should().Be("Claude CLI");
        _sut.PluginPrefix.Should().Be("Softwareschmiede.ClaudeCli");
        _sut.ProviderDateiPraefix.Should().Be("claude");
        _sut.GetSettingGroups().Single().Fields.Should().ContainSingle(f => f.Key == "Token");
    }

    /// <summary>Reads agents from .github when folder exists.</summary>
    [Fact]
    public async Task GetAvailableAgentsAsync_ShouldUseGithubFolder_WhenPresent()
    {
        var githubDir = Path.Combine(_testDirectory, ".github");
        Directory.CreateDirectory(githubDir);
        await File.WriteAllTextAsync(Path.Combine(_testDirectory, "root.agent.md"), "description: root");
        await File.WriteAllTextAsync(Path.Combine(githubDir, "inside.agent.md"), "description: inside");

        var result = (await _sut.GetAvailableAgentsAsync(_testDirectory)).ToList();

        result.Should().ContainSingle(a => a.Name == "inside");
        result.Should().NotContain(a => a.Name == "root");
    }

    /// <summary>Uses a content fallback if no explicit description exists.</summary>
    [Fact]
    public async Task GetAvailableAgentsAsync_ShouldUseFallbackDescription_WhenFrontmatterHasNoDescription()
    {
        Directory.CreateDirectory(_testDirectory);
        const string content = """
            ---
            name: fallback-agent
            ---
            Fallback description line
            """;
        await File.WriteAllTextAsync(Path.Combine(_testDirectory, "fallback-agent.agent.md"), content);

        var result = (await _sut.GetAvailableAgentsAsync(_testDirectory)).Single();

        result.Beschreibung.Should().Be("name: fallback-agent");
    }

    /// <summary>Writes provider-specific task file and passes expected CLI args.</summary>
    [Fact]
    public async Task StartDevelopmentAsync_ShouldUseClaudeCommandAndProviderTaskFile()
    {
        Directory.CreateDirectory(_testDirectory);
        _credentialStoreMock.Setup(c => c.GetCredential("Softwareschmiede.ClaudeCli.Token")).Returns("claude-token");
        _cliRunnerMock.Setup(c => c.StreamAsync(
                "claude",
                It.IsAny<IEnumerable<string>>(),
                _testDirectory,
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable("line 1"));
        var agent = new AgentInfo("test-agent", "desc", "file");

        var lines = new List<string>();
        await foreach (var line in _sut.StartDevelopmentAsync("Prompt", agent, _testDirectory, "claude-model"))
        {
            lines.Add(line);
        }

        lines.Should().Equal("line 1");
        Directory.GetFiles(_testDirectory, "*.claude-task.md").Should().ContainSingle();
        _cliRunnerMock.Verify(c => c.StreamAsync(
            "claude",
            It.Is<IEnumerable<string>>(args => args.Contains("--model") && args.Contains("claude-model") && args.Contains("--agent") && args.Contains("test-agent")),
            _testDirectory,
            It.Is<IDictionary<string, string>?>(env => env != null && env["ANTHROPIC_API_KEY"] == "claude-token"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Uses auto-model when no explicit model was provided.</summary>
    [Fact]
    public async Task StartDevelopmentAsync_ShouldUseAutoModel_WhenModelIsNull()
    {
        Directory.CreateDirectory(_testDirectory);
        _cliRunnerMock.Setup(c => c.StreamAsync(
                "claude",
                It.IsAny<IEnumerable<string>>(),
                _testDirectory,
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable());
        var agent = new AgentInfo("agent-a", null, "file");

        await foreach (var _ in _sut.StartDevelopmentAsync("Prompt", agent, _testDirectory, null))
        {
        }

        _cliRunnerMock.Verify(c => c.StreamAsync(
            "claude",
            It.Is<IEnumerable<string>>(args => args.Contains("--model") && args.Contains("auto")),
            _testDirectory,
            It.IsAny<IDictionary<string, string>?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Skips --agent argument when agent name is empty.</summary>
    [Fact]
    public async Task StartDevelopmentAsync_ShouldSkipAgentArgument_WhenAgentNameIsEmpty()
    {
        Directory.CreateDirectory(_testDirectory);
        _cliRunnerMock.Setup(c => c.StreamAsync(
                "claude",
                It.IsAny<IEnumerable<string>>(),
                _testDirectory,
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable());
        var agent = new AgentInfo(string.Empty, null, "file");

        await foreach (var _ in _sut.StartDevelopmentAsync("Prompt", agent, _testDirectory, null))
        {
        }

        _cliRunnerMock.Verify(c => c.StreamAsync(
            "claude",
            It.Is<IEnumerable<string>>(args => !args.Contains("--agent")),
            _testDirectory,
            It.IsAny<IDictionary<string, string>?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Returns false if package has no .github folder.</summary>
    [Fact]
    public async Task IsAgentPackageCompatibleAsync_ShouldReturnFalse_WhenGithubFolderMissing()
    {
        Directory.CreateDirectory(_testDirectory);

        var result = await _sut.IsAgentPackageCompatibleAsync(_testDirectory);

        result.Should().BeFalse();
    }

    /// <summary>Returns true when package has .github folder.</summary>
    [Fact]
    public async Task IsAgentPackageCompatibleAsync_ShouldReturnTrue_WhenGithubFolderExists()
    {
        Directory.CreateDirectory(Path.Combine(_testDirectory, ".github"));

        var result = await _sut.IsAgentPackageCompatibleAsync(_testDirectory);

        result.Should().BeTrue();
    }

    /// <summary>Copies .github files recursively during deploy.</summary>
    [Fact]
    public async Task DeployAgentPackageAsync_ShouldCopyGithubFilesRecursively()
    {
        var packagePath = Path.Combine(_testDirectory, "package");
        var repoPath = Path.Combine(_testDirectory, "repo");
        var sourceWorkflows = Path.Combine(packagePath, ".github", "workflows");
        Directory.CreateDirectory(sourceWorkflows);
        Directory.CreateDirectory(repoPath);
        await File.WriteAllTextAsync(Path.Combine(sourceWorkflows, "ci.yml"), "name: CI");

        await _sut.DeployAgentPackageAsync(packagePath, repoPath);

        var targetFile = Path.Combine(repoPath, ".github", "workflows", "ci.yml");
        File.Exists(targetFile).Should().BeTrue();
        (await File.ReadAllTextAsync(targetFile)).Should().Be("name: CI");
    }

    /// <summary>Overwrites existing target files during deploy.</summary>
    [Fact]
    public async Task DeployAgentPackageAsync_ShouldOverwriteExistingFiles_WhenTargetExists()
    {
        var packagePath = Path.Combine(_testDirectory, "package");
        var repoPath = Path.Combine(_testDirectory, "repo");
        var sourceWorkflows = Path.Combine(packagePath, ".github", "workflows");
        var targetWorkflows = Path.Combine(repoPath, ".github", "workflows");
        Directory.CreateDirectory(sourceWorkflows);
        Directory.CreateDirectory(targetWorkflows);
        await File.WriteAllTextAsync(Path.Combine(sourceWorkflows, "ci.yml"), "new");
        await File.WriteAllTextAsync(Path.Combine(targetWorkflows, "ci.yml"), "old");

        await _sut.DeployAgentPackageAsync(packagePath, repoPath);

        (await File.ReadAllTextAsync(Path.Combine(targetWorkflows, "ci.yml"))).Should().Be("new");
    }

    /// <summary>Skips deploy when .github does not exist in package.</summary>
    [Fact]
    public async Task DeployAgentPackageAsync_ShouldDoNothing_WhenGithubFolderMissing()
    {
        var packagePath = Path.Combine(_testDirectory, "package");
        var repoPath = Path.Combine(_testDirectory, "repo");
        Directory.CreateDirectory(packagePath);
        Directory.CreateDirectory(repoPath);

        await _sut.DeployAgentPackageAsync(packagePath, repoPath);

        Directory.Exists(Path.Combine(repoPath, ".github")).Should().BeFalse();
    }

    /// <summary>Parses passed, failed and skipped entries from combined CLI output.</summary>
    [Fact]
    public async Task RunTestsAsync_ShouldParseStdOutAndStdErrEntries()
    {
        _cliRunnerMock.Setup(c => c.RunAsync(
                "dotnet",
                It.IsAny<IEnumerable<string>>(),
                _testDirectory,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(1, "Passed A\nSkipped B", "Failed C"));

        var result = await _sut.RunTestsAsync(_testDirectory);

        result.Bestanden.Should().BeFalse();
        result.Ergebnisse.Should().HaveCount(3);
        result.Ergebnisse.Should().Contain(e => e.Status == Softwareschmiede.Domain.Enums.TestStatus.Bestanden);
        result.Ergebnisse.Should().Contain(e => e.Status == Softwareschmiede.Domain.Enums.TestStatus.Uebersprungen);
        result.Ergebnisse.Should().Contain(e => e.Status == Softwareschmiede.Domain.Enums.TestStatus.Fehlgeschlagen);
    }

    /// <summary>Returns true for successful health check.</summary>
    [Fact]
    public async Task CheckHealthAsync_ShouldReturnTrue_WhenClaudeVersionSucceeds()
    {
        _cliRunnerMock.Setup(c => c.RunAsync(
                "claude", It.IsAny<IEnumerable<string>>(), null,
                It.IsAny<IDictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, "claude 1.0.0", string.Empty));

        var result = await _sut.CheckHealthAsync();

        result.Should().BeTrue();
    }

    /// <summary>Returns false for failing health check.</summary>
    [Fact]
    public async Task CheckHealthAsync_ShouldReturnFalse_WhenClaudeVersionFails()
    {
        _cliRunnerMock.Setup(c => c.RunAsync(
                "claude", It.IsAny<IEnumerable<string>>(), null,
                It.IsAny<IDictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(1, string.Empty, "error"));

        var result = await _sut.CheckHealthAsync();

        result.Should().BeFalse();
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    private static async IAsyncEnumerable<string> ToAsyncEnumerable(params string[] lines)
    {
        foreach (var line in lines)
        {
            yield return line;
            await Task.Yield();
        }
    }
}
