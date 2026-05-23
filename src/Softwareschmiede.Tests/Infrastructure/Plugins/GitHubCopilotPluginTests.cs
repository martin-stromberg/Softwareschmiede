using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Infrastructure.Plugins;

namespace Softwareschmiede.Tests.Infrastructure.Plugins;

/// <summary>Tests für das GitHubCopilotPlugin.</summary>
public sealed class GitHubCopilotPluginTests : IDisposable
{
    private readonly Mock<ICliRunner> _cliRunnerMock;
    private readonly Mock<ICredentialStore> _credentialStoreMock;
    private readonly GitHubCopilotPlugin _sut;
    private readonly string _testDirectory;

    public GitHubCopilotPluginTests()
    {
        _cliRunnerMock = new Mock<ICliRunner>();
        _credentialStoreMock = new Mock<ICredentialStore>();
        _sut = new GitHubCopilotPlugin(
            _cliRunnerMock.Object,
            _credentialStoreMock.Object,
            new Mock<ILogger<GitHubCopilotPlugin>>().Object);
        _testDirectory = Path.Combine(Path.GetTempPath(), "softwareschmiede-test-" + Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
            Directory.Delete(_testDirectory, recursive: true);
    }

    /// <summary>GetAvailableAgentsAsync gibt leere Liste zurück wenn Verzeichnis nicht existiert.</summary>
    [Fact]
    public async Task GetAvailableAgentsAsync_ShouldReturnEmpty_WhenDirectoryDoesNotExist()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_testDirectory, "nonexistent");

        // Act
        var result = await _sut.GetAvailableAgentsAsync(nonExistentPath);

        // Assert
        result.Should().BeEmpty();
    }

    /// <summary>GetAvailableAgentsAsync erkennt .agent.md Dateien als Agenten.</summary>
    [Fact]
    public async Task GetAvailableAgentsAsync_ShouldReturnAgents_WhenAgentMdFilesExist()
    {
        // Arrange
        Directory.CreateDirectory(_testDirectory);
        var agentContent = """
            ---
            name: my-agent
            description: Ein Test-Agent
            ---
            """;
        await File.WriteAllTextAsync(Path.Combine(_testDirectory, "my-agent.agent.md"), agentContent);
        await File.WriteAllTextAsync(Path.Combine(_testDirectory, "other-agent.agent.md"), "description: Anderer Agent");

        // Act
        var result = (await _sut.GetAvailableAgentsAsync(_testDirectory)).ToList();

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(a => a.Name == "my-agent");
        result.Should().Contain(a => a.Name == "other-agent");
    }

    /// <summary>GetAvailableAgentsAsync liest Beschreibung aus der description-Zeile.</summary>
    [Fact]
    public async Task GetAvailableAgentsAsync_ShouldReadDescriptionFromFrontmatter_WhenDescriptionLineExists()
    {
        // Arrange
        Directory.CreateDirectory(_testDirectory);
        const string content = """
            ---
            name: test
            description: Meine Agenten-Beschreibung
            ---
            """;
        await File.WriteAllTextAsync(Path.Combine(_testDirectory, "test.agent.md"), content);

        // Act
        var result = (await _sut.GetAvailableAgentsAsync(_testDirectory)).ToList();

        // Assert
        result.Should().HaveCount(1);
        result[0].Beschreibung.Should().Be("Meine Agenten-Beschreibung");
    }

    /// <summary>PluginName gibt "GitHub Copilot" zurück.</summary>
    [Fact]
    public void PluginName_ShouldBeGitHubCopilot()
    {
        // Act & Assert
        _sut.PluginName.Should().Be("GitHub Copilot");
    }

    [Fact]
    public void PluginMetadata_ShouldExposeExpectedValues()
    {
        _sut.PluginPrefix.Should().Be("Softwareschmiede.GitHubCopilot");
        _sut.ProviderDateiPraefix.Should().Be("copilot");

        var settingGroups = _sut.GetSettingGroups().ToList();
        settingGroups.Should().HaveCount(2);
        settingGroups.Select(g => g.GroupName).Should().Contain(["Authentifizierung", "Ausführung"]);

        settingGroups
            .SelectMany(g => g.Fields)
            .Should()
            .Contain(f => f.Key == "Token")
            .And.Contain(f => f.Key == "ExecutablePath");
    }

    [Fact]
    public async Task IsAgentPackageCompatibleAsync_ShouldReturnFalse_WhenGithubFolderIsMissing()
    {
        Directory.CreateDirectory(_testDirectory);

        var result = await _sut.IsAgentPackageCompatibleAsync(_testDirectory);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsAgentPackageCompatibleAsync_ShouldReturnTrue_WhenGithubFolderExists()
    {
        Directory.CreateDirectory(Path.Combine(_testDirectory, ".github"));

        var result = await _sut.IsAgentPackageCompatibleAsync(_testDirectory);

        result.Should().BeTrue();
    }

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

    [Fact]
    public async Task StartDevelopmentAsync_ShouldWritePromptFile_AndPassAgentAndModelToCli()
    {
        // Arrange
        Directory.CreateDirectory(_testDirectory);
        _credentialStoreMock.Setup(c => c.GetCredential("Softwareschmiede.GitHub.Token")).Returns("gh-token");
        _cliRunnerMock.Setup(c => c.StreamAsync(
                "copilot",
                It.IsAny<IEnumerable<string>>(),
                _testDirectory,
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable("line 1", "line 2"));

        var agent = new AgentInfo("test-agent", "desc", "file");

        // Act
        var lines = new List<string>();
        await foreach (var line in _sut.StartDevelopmentAsync("Prompt-Inhalt", agent, _testDirectory, "gpt-5"))
        {
            lines.Add(line);
        }

        // Assert
        lines.Should().Equal("line 1", "line 2");
        var promptFile = Directory.GetFiles(_testDirectory, "*.copilot-task.md").Single();
        File.Exists(promptFile).Should().BeTrue();
        (await File.ReadAllTextAsync(promptFile)).Should().Be("Prompt-Inhalt");

        _cliRunnerMock.Verify(c => c.StreamAsync(
            "copilot",
            It.Is<IEnumerable<string>>(args =>
                args.Contains("--prompt") &&
                args.Any(a => a.StartsWith("@", StringComparison.Ordinal)) &&
                args.Contains("--allow-all-tools") &&
                args.Contains("--allow-all-paths") &&
                args.Contains("--no-ask-user") &&
                args.Contains("--silent") &&
                args.Contains("--agent") &&
                args.Contains("test-agent") &&
                args.Contains("--model") &&
                args.Contains("gpt-5")),
            _testDirectory,
            It.Is<IDictionary<string, string>?>(env => env != null && env.ContainsKey("GH_TOKEN") && env["GH_TOKEN"] == "gh-token"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StartDevelopmentAsync_ShouldUseAutoModel_WhenModelIsNull()
    {
        Directory.CreateDirectory(_testDirectory);
        _cliRunnerMock.Setup(c => c.StreamAsync(
                "copilot",
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
            "copilot",
            It.Is<IEnumerable<string>>(args => args.Contains("--model") && args.Contains("auto")),
            _testDirectory,
            It.IsAny<IDictionary<string, string>?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StartDevelopmentAsync_ShouldSkipAgentArgument_WhenAgentNameIsEmpty()
    {
        Directory.CreateDirectory(_testDirectory);
        _cliRunnerMock.Setup(c => c.StreamAsync(
                "copilot",
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
            "copilot",
            It.Is<IEnumerable<string>>(args => !args.Contains("--agent")),
            _testDirectory,
            It.IsAny<IDictionary<string, string>?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunTestsAsync_ShouldReturnBestandenTrueAndParsedResults_WhenDotnetTestSucceeds()
    {
        _cliRunnerMock.Setup(c => c.RunAsync(
                "dotnet",
                It.Is<IEnumerable<string>>(a => a.Contains("test")),
                _testDirectory,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, "Passed MyTest\nSkipped Another", string.Empty));

        var result = await _sut.RunTestsAsync(_testDirectory);

        result.Bestanden.Should().BeTrue();
        result.Ergebnisse.Should().HaveCount(2);
        result.Ergebnisse.Should().Contain(e => e.Status == Softwareschmiede.Domain.Enums.TestStatus.Bestanden);
        result.Ergebnisse.Should().Contain(e => e.Status == Softwareschmiede.Domain.Enums.TestStatus.Uebersprungen);
    }

    [Fact]
    public async Task RunTestsAsync_ShouldReturnBestandenFalse_WhenDotnetTestFails()
    {
        _cliRunnerMock.Setup(c => c.RunAsync(
                "dotnet",
                It.IsAny<IEnumerable<string>>(),
                _testDirectory,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(1, string.Empty, "Failed MyTest"));

        var result = await _sut.RunTestsAsync(_testDirectory);

        result.Bestanden.Should().BeFalse();
        result.Ergebnisse.Should().ContainSingle();
        result.Ergebnisse[0].Status.Should().Be(Softwareschmiede.Domain.Enums.TestStatus.Fehlgeschlagen);
    }

    /// <summary>CheckHealthAsync gibt true zurück wenn gh copilot version erfolgreich ist.</summary>
    [Fact]
    public async Task CheckHealthAsync_ShouldReturnTrue_WhenGhCopilotVersionSucceeds()
    {
        // Arrange
        _credentialStoreMock.Setup(c => c.GetCredential("Softwareschmiede.GitHub.Token")).Returns("token");
        _credentialStoreMock.Setup(c => c.GetCredential("Softwareschmiede.GitHubCopilot.ExecutablePath")).Returns((string?)null);
        _cliRunnerMock.Setup(c => c.RunAsync(
                "copilot", It.IsAny<IEnumerable<string>>(), null,
                It.IsAny<IDictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, "copilot 1.0.0", string.Empty));

        // Act
        var result = await _sut.CheckHealthAsync();

        // Assert
        result.Should().BeTrue();
    }

    /// <summary>CheckHealthAsync gibt false zurück wenn gh copilot version fehlschlägt.</summary>
    [Fact]
    public async Task CheckHealthAsync_ShouldReturnFalse_WhenGhCopilotVersionFails()
    {
        // Arrange
        _cliRunnerMock.Setup(c => c.RunAsync(
                "copilot", It.IsAny<IEnumerable<string>>(), null,
                It.IsAny<IDictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(1, string.Empty, "error"));

        // Act
        var result = await _sut.CheckHealthAsync();

        // Assert
        result.Should().BeFalse();
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
