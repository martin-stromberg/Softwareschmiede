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

    [Fact]
    public async Task GetAvailableAgentsAsync_ShouldUseFirstContentLine_WhenDescriptionIsMissing()
    {
        // Arrange
        Directory.CreateDirectory(_testDirectory);
        const string content = """
            ---
            name: fallback-agent
            ---
            Weitere Details
            """;
        await File.WriteAllTextAsync(Path.Combine(_testDirectory, "fallback.agent.md"), content);

        // Act
        var result = (await _sut.GetAvailableAgentsAsync(_testDirectory)).Single(a => a.Name == "fallback");

        // Assert
        result.Beschreibung.Should().Be("name: fallback-agent");
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
        _sut.GetSettingGroups().Should().ContainSingle();
        _sut.GetSettingGroups().Single().Fields.Should().ContainSingle(f => f.Key == "Token");
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
    public async Task IsAgentPackageCompatibleAsync_ShouldReturnFalse_WhenPackageDirectoryDoesNotExist()
    {
        var missingDirectory = Path.Combine(_testDirectory, "missing");

        var result = await _sut.IsAgentPackageCompatibleAsync(missingDirectory);

        result.Should().BeFalse();
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
        IEnumerable<string>? capturedArgs = null;
        _credentialStoreMock.Setup(c => c.GetCredential("Softwareschmiede.GitHub.Token")).Returns("gh-token");
        _cliRunnerMock.Setup(c => c.StreamAsync(
                "copilot",
                It.IsAny<IEnumerable<string>>(),
                _testDirectory,
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, IEnumerable<string>, string?, IDictionary<string, string>?, CancellationToken>((_, args, _, _, _) => capturedArgs = args)
            .Returns(ToAsyncEnumerable("line 1", "line 2"));

        var agent = new AgentInfo("test-agent", "desc", "file");

        // Act
        var lines = new List<string>();
        await foreach (var line in _sut.StartDevelopmentAsync("Prompt-Inhalt", agent, _testDirectory, "gpt-5", null))
        {
            lines.Add(line);
        }

        // Assert
        lines.Should().Equal("line 1", "line 2");
        capturedArgs.Should().NotBeNull();
        var promptArgument = capturedArgs!.SkipWhile(a => a != "--prompt").Skip(1).First();
        promptArgument.Should().StartWith("@");
        var promptFile = promptArgument[1..];
        Path.GetFileName(promptFile).Should().MatchRegex("^[0-9a-f]{32}\\.copilot-task\\.md$");
        File.Exists(promptFile).Should().BeFalse();

        _cliRunnerMock.Verify(c => c.StreamAsync(
            "copilot",
            It.Is<IEnumerable<string>>(args =>
                args.Contains("--prompt") &&
                args.Contains($"@{promptFile}") &&
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
    public async Task StartDevelopmentAsync_ShouldNormalizeExecutionIdToNFormat_WhenDFormatIsProvided()
    {
        Directory.CreateDirectory(_testDirectory);
        IEnumerable<string>? capturedArgs = null;
        _cliRunnerMock.Setup(c => c.StreamAsync(
                "copilot",
                It.IsAny<IEnumerable<string>>(),
                _testDirectory,
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, IEnumerable<string>, string?, IDictionary<string, string>?, CancellationToken>((_, args, _, _, _) => capturedArgs = args)
            .Returns(ToAsyncEnumerable("ok"));

        var agent = new AgentInfo("test-agent", "desc", "file");
        const string executionId = "8934d257-5588-473e-9882-9b19d322851b";

        await foreach (var _ in _sut.StartDevelopmentAsync("Prompt", agent, _testDirectory, null, executionId))
        {
        }

        var promptArgument = capturedArgs!.SkipWhile(a => a != "--prompt").Skip(1).First();
        promptArgument.Should().Be("@"+Path.Combine(_testDirectory, "8934d2575588473e98829b19d322851b.copilot-task.md"));
    }

    [Fact]
    public async Task StartDevelopmentAsync_ShouldTrimAndNormalizeExecutionId_WhenWhitespaceWrappedGuidIsProvided()
    {
        Directory.CreateDirectory(_testDirectory);
        IEnumerable<string>? capturedArgs = null;
        _cliRunnerMock.Setup(c => c.StreamAsync(
                "copilot",
                It.IsAny<IEnumerable<string>>(),
                _testDirectory,
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, IEnumerable<string>, string?, IDictionary<string, string>?, CancellationToken>((_, args, _, _, _) => capturedArgs = args)
            .Returns(ToAsyncEnumerable("ok"));

        var agent = new AgentInfo("test-agent", "desc", "file");
        const string executionId = "  (8934d257-5588-473e-9882-9b19d322851b)  ";

        await foreach (var _ in _sut.StartDevelopmentAsync("Prompt", agent, _testDirectory, null, executionId))
        {
        }

        var promptArgument = capturedArgs!.SkipWhile(a => a != "--prompt").Skip(1).First();
        promptArgument.Should().Be("@" + Path.Combine(_testDirectory, "8934d2575588473e98829b19d322851b.copilot-task.md"));
    }

    [Fact]
    public async Task StartDevelopmentAsync_ShouldThrowArgumentException_WhenExecutionIdIsInvalid()
    {
        Directory.CreateDirectory(_testDirectory);
        var agent = new AgentInfo("test-agent", "desc", "file");

        var action = async () =>
        {
            await foreach (var _ in _sut.StartDevelopmentAsync("Prompt", agent, _testDirectory, null, "invalid-format"))
            {
            }
        };

        await action.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*executionId*");
        Directory.GetFiles(_testDirectory, "*.copilot-task.md").Should().BeEmpty();
        _cliRunnerMock.Verify(c => c.StreamAsync(
            "copilot",
            It.IsAny<IEnumerable<string>>(),
            It.IsAny<string?>(),
            It.IsAny<IDictionary<string, string>?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StartDevelopmentAsync_ShouldCleanupTaskFile_WhenCliThrows()
    {
        Directory.CreateDirectory(_testDirectory);
        IEnumerable<string>? capturedArgs = null;
        _cliRunnerMock.Setup(c => c.StreamAsync(
                "copilot",
                It.IsAny<IEnumerable<string>>(),
                _testDirectory,
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, IEnumerable<string>, string?, IDictionary<string, string>?, CancellationToken>((_, args, _, _, _) => capturedArgs = args)
            .Returns(ToFailingAsyncEnumerable(new InvalidOperationException("CLI failed")));

        var agent = new AgentInfo("test-agent", "desc", "file");
        var action = async () =>
        {
            await foreach (var _ in _sut.StartDevelopmentAsync("Prompt", agent, _testDirectory, null, null))
            {
            }
        };

        await action.Should().ThrowAsync<InvalidOperationException>();

        var promptArgument = capturedArgs!.SkipWhile(a => a != "--prompt").Skip(1).First();
        var promptFile = promptArgument[1..];
        File.Exists(promptFile).Should().BeFalse();
    }

    [Fact]
    public async Task StartDevelopmentAsync_ShouldThrowIOException_WhenPromptFileCannotBeWritten()
    {
        Directory.CreateDirectory(_testDirectory);
        var agent = new AgentInfo("test-agent", "desc", "file");
        const string executionId = "8934d2575588473e98829b19d322851b";
        var promptFilePath = Path.Combine(_testDirectory, $"{executionId}.copilot-task.md");
        await File.WriteAllTextAsync(promptFilePath, "locked");
        await using var lockStream = new FileStream(
            promptFilePath,
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.None);

        var action = async () =>
        {
            await foreach (var _ in _sut.StartDevelopmentAsync("Prompt", agent, _testDirectory, null, executionId))
            {
            }
        };

        await action.Should().ThrowAsync<IOException>();
        _cliRunnerMock.Verify(c => c.StreamAsync(
            "copilot",
            It.IsAny<IEnumerable<string>>(),
            It.IsAny<string?>(),
            It.IsAny<IDictionary<string, string>?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StartDevelopmentAsync_ShouldAppendGitIgnoreRule_WhenMissing()
    {
        Directory.CreateDirectory(_testDirectory);
        var gitIgnorePath = Path.Combine(_testDirectory, ".gitignore");
        await File.WriteAllTextAsync(gitIgnorePath, "bin/\nobj/\n");

        _cliRunnerMock.Setup(c => c.StreamAsync(
                "copilot",
                It.IsAny<IEnumerable<string>>(),
                _testDirectory,
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable());

        var agent = new AgentInfo("agent-a", null, "file");
        await foreach (var _ in _sut.StartDevelopmentAsync("Prompt", agent, _testDirectory, null, null))
        {
        }

        var content = await File.ReadAllTextAsync(gitIgnorePath);
        content.Should().Contain("*.copilot-task.md");
        content.Split('\n').Count(l => l.Trim() == "*.copilot-task.md").Should().Be(1);
    }

    [Fact]
    public async Task StartDevelopmentAsync_ShouldNotDuplicateGitIgnoreRule_WhenEquivalentRuleExists()
    {
        Directory.CreateDirectory(_testDirectory);
        var gitIgnorePath = Path.Combine(_testDirectory, ".gitignore");
        await File.WriteAllTextAsync(gitIgnorePath, ".copilot-task.md\n");

        _cliRunnerMock.Setup(c => c.StreamAsync(
                "copilot",
                It.IsAny<IEnumerable<string>>(),
                _testDirectory,
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable());

        var agent = new AgentInfo("agent-a", null, "file");
        await foreach (var _ in _sut.StartDevelopmentAsync("Prompt", agent, _testDirectory, null, null))
        {
        }

        var lines = (await File.ReadAllLinesAsync(gitIgnorePath))
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToList();
        lines.Should().ContainSingle(l => l == "*.copilot-task.md");
    }

    [Fact]
    public async Task StartDevelopmentAsync_ShouldConsolidateLegacyAndWildcardRulesToSingleTargetRule()
    {
        Directory.CreateDirectory(_testDirectory);
        var gitIgnorePath = Path.Combine(_testDirectory, ".gitignore");
        await File.WriteAllTextAsync(gitIgnorePath, "/.copilot-task.md\n*.copilot-task.md\n.copilot-task.md\n");

        _cliRunnerMock.Setup(c => c.StreamAsync(
                "copilot",
                It.IsAny<IEnumerable<string>>(),
                _testDirectory,
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable());

        var agent = new AgentInfo("agent-a", null, "file");
        await foreach (var _ in _sut.StartDevelopmentAsync("Prompt", agent, _testDirectory, null, null))
        {
        }

        var rules = (await File.ReadAllLinesAsync(gitIgnorePath))
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

        rules.Should().ContainSingle(l => l == "*.copilot-task.md");
    }

    [Fact]
    public async Task StartDevelopmentAsync_ShouldCreateGitIgnoreAndInsertRule_WhenGitIgnoreDoesNotExist()
    {
        Directory.CreateDirectory(_testDirectory);
        var gitIgnorePath = Path.Combine(_testDirectory, ".gitignore");

        _cliRunnerMock.Setup(c => c.StreamAsync(
                "copilot",
                It.IsAny<IEnumerable<string>>(),
                _testDirectory,
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable());

        var agent = new AgentInfo("agent-a", null, "file");
        await foreach (var _ in _sut.StartDevelopmentAsync("Prompt", agent, _testDirectory, null, null))
        {
        }

        File.Exists(gitIgnorePath).Should().BeTrue();
        (await File.ReadAllTextAsync(gitIgnorePath)).Should().Be("*.copilot-task.md\n");
    }

    [Fact]
    public async Task StartDevelopmentAsync_ShouldAppendRuleWithNewline_WhenGitIgnoreHasNoTrailingNewline()
    {
        Directory.CreateDirectory(_testDirectory);
        var gitIgnorePath = Path.Combine(_testDirectory, ".gitignore");
        await File.WriteAllTextAsync(gitIgnorePath, "bin/\nobj/");

        _cliRunnerMock.Setup(c => c.StreamAsync(
                "copilot",
                It.IsAny<IEnumerable<string>>(),
                _testDirectory,
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable());

        var agent = new AgentInfo("agent-a", null, "file");
        await foreach (var _ in _sut.StartDevelopmentAsync("Prompt", agent, _testDirectory, null, null))
        {
        }

        var content = await File.ReadAllTextAsync(gitIgnorePath);
        content.Should().Be("bin/\nobj/\n*.copilot-task.md\n");
        content.Split('\n').Count(l => l.Trim() == "*.copilot-task.md").Should().Be(1);
    }

    [Fact]
    public async Task StartDevelopmentAsync_ShouldAppendRule_WhenOnlyCommentedCopilotRuleExistsInGitIgnore()
    {
        Directory.CreateDirectory(_testDirectory);
        var gitIgnorePath = Path.Combine(_testDirectory, ".gitignore");
        await File.WriteAllTextAsync(gitIgnorePath, "# .copilot-task.md\nbin/\n");

        _cliRunnerMock.Setup(c => c.StreamAsync(
                "copilot",
                It.IsAny<IEnumerable<string>>(),
                _testDirectory,
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable());

        var agent = new AgentInfo("agent-a", null, "file");
        await foreach (var _ in _sut.StartDevelopmentAsync("Prompt", agent, _testDirectory, null, null))
        {
        }

        var content = await File.ReadAllTextAsync(gitIgnorePath);
        content.Should().Contain("# .copilot-task.md");
        content.Split('\n').Count(l => l.Trim() == "*.copilot-task.md").Should().Be(1);
    }

    [Fact]
    public async Task StartDevelopmentAsync_ShouldThrowIOException_WhenGitIgnoreWriteFailsAfterMaxRetries()
    {
        Directory.CreateDirectory(_testDirectory);
        var gitIgnorePath = Path.Combine(_testDirectory, ".gitignore");
        await File.WriteAllTextAsync(gitIgnorePath, "bin/\n");
        await using var lockStream = new FileStream(
            gitIgnorePath,
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.None);

        var agent = new AgentInfo("agent-a", null, "file");
        var action = async () =>
        {
            await foreach (var _ in _sut.StartDevelopmentAsync("Prompt", agent, _testDirectory, null, null))
            {
            }
        };

        await action.Should().ThrowAsync<IOException>();
        _cliRunnerMock.Verify(c => c.StreamAsync(
            "copilot",
            It.IsAny<IEnumerable<string>>(),
            It.IsAny<string?>(),
            It.IsAny<IDictionary<string, string>?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StartDevelopmentAsync_ShouldRetryGitIgnoreWriteAndSucceed_OnTransientIOException()
    {
        Directory.CreateDirectory(_testDirectory);
        var gitIgnorePath = Path.Combine(_testDirectory, ".gitignore");
        await File.WriteAllTextAsync(gitIgnorePath, "bin/\n");

        var lockStream = new FileStream(
            gitIgnorePath,
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.None);
        var releaseLockTask = Task.Run(async () =>
        {
            await Task.Delay(80);
            await lockStream.DisposeAsync();
        });

        _cliRunnerMock.Setup(c => c.StreamAsync(
                "copilot",
                It.IsAny<IEnumerable<string>>(),
                _testDirectory,
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable());

        var agent = new AgentInfo("agent-a", null, "file");
        await foreach (var _ in _sut.StartDevelopmentAsync("Prompt", agent, _testDirectory, null, null))
        {
        }

        await releaseLockTask;
        var content = await File.ReadAllTextAsync(gitIgnorePath);
        content.Split('\n').Count(l => l.Trim() == "*.copilot-task.md").Should().Be(1);
        _cliRunnerMock.Verify(c => c.StreamAsync(
            "copilot",
            It.IsAny<IEnumerable<string>>(),
            _testDirectory,
            It.IsAny<IDictionary<string, string>?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StartDevelopmentAsync_ShouldThrowOperationCanceledException_WhenCancelledDuringGitIgnoreRetryDelay()
    {
        Directory.CreateDirectory(_testDirectory);
        var gitIgnorePath = Path.Combine(_testDirectory, ".gitignore");
        await File.WriteAllTextAsync(gitIgnorePath, "bin/\n");
        await using var lockStream = new FileStream(
            gitIgnorePath,
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.None);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(10));
        var agent = new AgentInfo("agent-a", null, "file");

        var action = async () =>
        {
            await foreach (var _ in _sut.StartDevelopmentAsync("Prompt", agent, _testDirectory, null, null, cts.Token))
            {
            }
        };

        await action.Should().ThrowAsync<OperationCanceledException>();
        _cliRunnerMock.Verify(c => c.StreamAsync(
            "copilot",
            It.IsAny<IEnumerable<string>>(),
            It.IsAny<string?>(),
            It.IsAny<IDictionary<string, string>?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StartDevelopmentAsync_ShouldThrow_WhenRepositoryDirectoryDoesNotExist()
    {
        var missingDirectory = Path.Combine(_testDirectory, "missing");
        var agent = new AgentInfo("agent-a", null, "file");

        var action = async () =>
        {
            await foreach (var _ in _sut.StartDevelopmentAsync("Prompt", agent, missingDirectory, null, null))
            {
            }
        };

        await action.Should().ThrowAsync<DirectoryNotFoundException>();
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

        await foreach (var _ in _sut.StartDevelopmentAsync("Prompt", agent, _testDirectory, null, null))
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

        await foreach (var _ in _sut.StartDevelopmentAsync("Prompt", agent, _testDirectory, null, null))
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
        _credentialStoreMock.Setup(c => c.GetCredential(It.IsAny<string>())).Returns("token");
        _cliRunnerMock.Setup(c => c.RunAsync(
                "copilot", It.IsAny<IEnumerable<string>>(), null,
                It.IsAny<IDictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, "copilot 1.0.0", string.Empty));

        // Act
        var result = await _sut.CheckHealthAsync();

        // Assert
        result.Should().BeTrue();
    }

    private static async IAsyncEnumerable<string> ToAsyncEnumerable(params string[] lines)
    {
        foreach (var line in lines)
        {
            yield return line;
            await Task.Yield();
        }
    }

    private static async IAsyncEnumerable<string> ToFailingAsyncEnumerable(Exception exception)
    {
        await Task.Yield();
        throw exception;
#pragma warning disable CS0162
        yield break;
#pragma warning restore CS0162
    }
}

