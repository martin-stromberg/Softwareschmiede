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

    /// <summary>CheckHealthAsync gibt true zurück wenn gh copilot version erfolgreich ist.</summary>
    [Fact]
    public async Task CheckHealthAsync_ShouldReturnTrue_WhenGhCopilotVersionSucceeds()
    {
        // Arrange
        _credentialStoreMock.Setup(c => c.GetCredential(It.IsAny<string>())).Returns("token");
        _cliRunnerMock.Setup(c => c.RunAsync(
                "gh", It.IsAny<IEnumerable<string>>(), null,
                It.IsAny<IDictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, "copilot 1.0.0", string.Empty));

        // Act
        var result = await _sut.CheckHealthAsync();

        // Assert
        result.Should().BeTrue();
    }
}
