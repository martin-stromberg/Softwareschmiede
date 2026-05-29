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

    /// <summary>Reads agents only from .claude/commands when folder exists.</summary>
    [Fact]
    public async Task GetAvailableAgentsAsync_ShouldUseCommandsFolder_WhenPresent()
    {
        var commandsDir = Path.Combine(_testDirectory, ".claude", "commands");
        Directory.CreateDirectory(commandsDir);
        await File.WriteAllTextAsync(Path.Combine(_testDirectory, "root.agent.md"), "description: root");
        await File.WriteAllTextAsync(Path.Combine(commandsDir, "inside.agent.md"), "description: inside");

        var result = (await _sut.GetAvailableAgentsAsync(_testDirectory)).ToList();

        result.Should().ContainSingle(a => a.Name == "inside");
        result.Should().NotContain(a => a.Name == "root");
    }

    /// <summary>Ignores agents outside the .claude/commands folder.</summary>
    [Fact]
    public async Task GetAvailableAgentsAsync_ShouldIgnoreAgentsOutsideCommandsFolder()
    {
        Directory.CreateDirectory(_testDirectory);
        var commandsDir = Path.Combine(_testDirectory, ".claude", "commands");
        Directory.CreateDirectory(commandsDir);
        await File.WriteAllTextAsync(Path.Combine(_testDirectory, "root.agent.md"), "description: root");
        await File.WriteAllTextAsync(Path.Combine(commandsDir, "inside.agent.md"), "description: inside");

        var result = (await _sut.GetAvailableAgentsAsync(_testDirectory)).ToList();

        result.Should().ContainSingle(a => a.Name == "inside");
        result.Should().NotContain(a => a.Name == "root");
    }

    /// <summary>Returns an empty agent list when package path does not exist.</summary>
    [Fact]
    public async Task GetAvailableAgentsAsync_ShouldReturnEmpty_WhenPackagePathDoesNotExist()
    {
        var nonExistentPath = Path.Combine(_testDirectory, "missing-package");

        var result = await _sut.GetAvailableAgentsAsync(nonExistentPath);

        result.Should().BeEmpty();
    }

    /// <summary>Uses a content fallback if no explicit description exists.</summary>
    [Fact]
    public async Task GetAvailableAgentsAsync_ShouldUseFallbackDescription_WhenFrontmatterHasNoDescription()
    {
        var commandsDir = Path.Combine(_testDirectory, ".claude", "commands");
        Directory.CreateDirectory(commandsDir);
        const string content = """
            ---
            name: fallback-agent
            ---
            Fallback description line
            """;
        await File.WriteAllTextAsync(Path.Combine(commandsDir, "fallback-agent.agent.md"), content);

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
            It.Is<IEnumerable<string>>(args =>
                args.Contains("-p")
                && args.Contains("-n")
                && args.Contains("--dangerously-skip-permissions")
                && args.Contains("--model")
                && args.Contains("claude-model")
                && args.Contains("--agent")
                && args.Contains("test-agent")
                && !args.Contains("--prompt")
                && !args.Contains("--allow-all-tools")
                && !args.Contains("--allow-all-paths")
                && !args.Contains("--no-ask-user")
                && !args.Contains("--silent")),
            _testDirectory,
            It.Is<IDictionary<string, string>?>(env => env != null && env["ANTHROPIC_API_KEY"] == "claude-token"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Uses sonnet-model alias when no explicit model was provided.</summary>
    [Fact]
    public async Task StartDevelopmentAsync_ShouldUseSonnetModel_WhenModelIsNull()
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
            It.Is<IEnumerable<string>>(args => args.Contains("--model") && args.Contains("sonnet")),
            _testDirectory,
            It.IsAny<IDictionary<string, string>?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Maps "auto" model alias to sonnet for Claude CLI compatibility.</summary>
    [Fact]
    public async Task StartDevelopmentAsync_ShouldMapAutoModelToSonnet()
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

        await foreach (var _ in _sut.StartDevelopmentAsync("Prompt", agent, _testDirectory, "auto"))
        {
        }

        _cliRunnerMock.Verify(c => c.StreamAsync(
            "claude",
            It.Is<IEnumerable<string>>(args => args.Contains("--model") && args.Contains("sonnet") && !args.Contains("auto")),
            _testDirectory,
            It.IsAny<IDictionary<string, string>?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Uses task-based session resume on follow-up runs in the same repository.</summary>
    [Fact]
    public async Task StartDevelopmentAsync_ShouldUseResumeArguments_OnFollowUpRun()
    {
        Directory.CreateDirectory(_testDirectory);
        var taskId = Guid.NewGuid();
        await File.WriteAllTextAsync(Path.Combine(_testDirectory, $"{taskId}.claude.context.md"), "context");
        _cliRunnerMock.Setup(c => c.StreamAsync(
                "claude",
                It.IsAny<IEnumerable<string>>(),
                _testDirectory,
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable("ok"));

        var agent = new AgentInfo("agent-a", null, "file");
        await foreach (var _ in _sut.StartDevelopmentAsync("Prompt 1", agent, _testDirectory, null))
        {
        }

        await foreach (var _ in _sut.StartDevelopmentAsync("Prompt 2", agent, _testDirectory, null))
        {
        }

        _cliRunnerMock.Verify(c => c.StreamAsync(
            "claude",
            It.Is<IEnumerable<string>>(args =>
                args.Contains("-n")
                && args.Contains(taskId.ToString())
                && args.Contains("-p")
                && !args.Contains("-r")),
            _testDirectory,
            It.IsAny<IDictionary<string, string>?>(),
            It.IsAny<CancellationToken>()), Times.Once);

        _cliRunnerMock.Verify(c => c.StreamAsync(
            "claude",
            It.Is<IEnumerable<string>>(args =>
                args.Contains("-r")
                && args.Contains(taskId.ToString())
                && args.Contains("-p")
                && !args.Contains("-n")),
            _testDirectory,
            It.IsAny<IDictionary<string, string>?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Retries as first run when resume reports that the session was not found.</summary>
    [Fact]
    public async Task StartDevelopmentAsync_ShouldFallbackToFirstRun_WhenSessionIsMissing()
    {
        Directory.CreateDirectory(_testDirectory);
        var taskId = Guid.NewGuid();
        await File.WriteAllTextAsync(Path.Combine(_testDirectory, $"{taskId}.claude.context.md"), "context");
        var callCount = 0;
        _cliRunnerMock.Setup(c => c.StreamAsync(
                It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>(),
                _testDirectory,
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                callCount++;
                return callCount switch
                {
                    1 => ToAsyncEnumerable("ok"),
                    2 => ToAsyncEnumerable("[Fehler] session not found"),
                    3 => ToAsyncEnumerable("recovered"),
                    _ => ToAsyncEnumerable()
                };
            });

        var agent = new AgentInfo("agent-a", null, "file");
        await foreach (var _ in _sut.StartDevelopmentAsync("Prompt 1", agent, _testDirectory, null))
        {
        }

        var lines = new List<string>();
        await foreach (var line in _sut.StartDevelopmentAsync("Prompt 2", agent, _testDirectory, null))
        {
            lines.Add(line);
        }

        lines.Should().Contain("recovered");
        _cliRunnerMock.Verify(c => c.StreamAsync(
            "claude",
            It.Is<IEnumerable<string>>(args => args.Contains("-r") && args.Contains(taskId.ToString())),
            _testDirectory,
            It.IsAny<IDictionary<string, string>?>(),
            It.IsAny<CancellationToken>()), Times.Once);
        _cliRunnerMock.Verify(c => c.StreamAsync(
            "claude",
            It.Is<IEnumerable<string>>(args => args.Contains("-n") && args.Contains(taskId.ToString())),
            _testDirectory,
            It.IsAny<IDictionary<string, string>?>(),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    /// <summary>Reuses generated session id across runs in the same repository even without context files.</summary>
    [Fact]
    public async Task StartDevelopmentAsync_ShouldReuseGeneratedSessionId_WhenNoContextFileExists()
    {
        Directory.CreateDirectory(_testDirectory);
        var capturedArgs = new List<IReadOnlyList<string>>();
        _cliRunnerMock.Setup(c => c.StreamAsync(
                "claude",
                It.IsAny<IEnumerable<string>>(),
                _testDirectory,
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable("ok"))
            .Callback<string, IEnumerable<string>, string?, IDictionary<string, string>?, CancellationToken>(
                (_, args, _, _, _) => capturedArgs.Add(args.ToList()));

        var agent = new AgentInfo("agent-a", null, "file");

        await foreach (var _ in _sut.StartDevelopmentAsync("Prompt 1", agent, _testDirectory, null))
        {
        }

        await foreach (var _ in _sut.StartDevelopmentAsync("Prompt 2", agent, _testDirectory, null))
        {
        }

        capturedArgs.Should().HaveCount(2);
        var firstRunArgs = capturedArgs[0];
        var firstTaskId = firstRunArgs.SkipWhile(a => a != "-n").Skip(1).FirstOrDefault();
        firstTaskId.Should().NotBeNullOrWhiteSpace();

        firstRunArgs.Should().Contain("-n");
        firstRunArgs.Should().NotContain("-r");
        capturedArgs[1].Should().Contain("-r");
        capturedArgs[1].Should().Contain(firstTaskId!);
        capturedArgs[1].Should().NotContain("-n");
    }

    /// <summary>Uses stdin command wrapper and resume arguments on large follow-up prompts.</summary>
    [Fact]
    public async Task StartDevelopmentAsync_ShouldUseResumeArgs_WithCommandWrapper_WhenLargePromptOnFollowUp()
    {
        Directory.CreateDirectory(_testDirectory);
        var largePrompt = new string('x', 9000);
        var commands = new List<string>();
        var allArgs = new List<IReadOnlyList<string>>();
        _cliRunnerMock.Setup(c => c.StreamAsync(
                It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>(),
                _testDirectory,
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable("ok"))
            .Callback<string, IEnumerable<string>, string?, IDictionary<string, string>?, CancellationToken>(
                (command, args, _, _, _) =>
                {
                    commands.Add(command);
                    allArgs.Add(args.ToList());
                });
        var agent = new AgentInfo("agent-a", null, "file");

        await foreach (var _ in _sut.StartDevelopmentAsync(largePrompt, agent, _testDirectory, null))
        {
        }

        await foreach (var _ in _sut.StartDevelopmentAsync(largePrompt, agent, _testDirectory, null))
        {
        }

        commands.Should().HaveCount(2);
        commands.Should().OnlyContain(c => c == (OperatingSystem.IsWindows() ? "powershell" : "sh"));

        var firstArgs = string.Join(" ", allArgs[0]);
        var secondArgs = string.Join(" ", allArgs[1]);
        firstArgs.Should().Contain("'-n'");
        firstArgs.Should().NotContain("'-r'");
        secondArgs.Should().Contain("'-r'");
        secondArgs.Should().NotContain("'-n'");
        firstArgs.Should().NotContain(largePrompt[..100]);
        secondArgs.Should().NotContain(largePrompt[..100]);
    }

    /// <summary>Uses stdin piping through PowerShell when prompt text is larger than 8 KB.</summary>
    [Fact]
    public async Task StartDevelopmentAsync_ShouldUsePowerShellPipe_WhenPromptIsLarge()
    {
        Directory.CreateDirectory(_testDirectory);
        var largePrompt = new string('x', 9000);
        _cliRunnerMock.Setup(c => c.StreamAsync(
                It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>(),
                _testDirectory,
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable("ok"));
        var agent = new AgentInfo("agent-a", null, "file");

        await foreach (var _ in _sut.StartDevelopmentAsync(largePrompt, agent, _testDirectory, null))
        {
        }

        _cliRunnerMock.Verify(c => c.StreamAsync(
            OperatingSystem.IsWindows() ? "powershell" : "sh",
            It.Is<IEnumerable<string>>(args =>
                args.Any(a => a.Contains("Get-Content -Raw -LiteralPath", StringComparison.Ordinal) || a.StartsWith("cat ", StringComparison.Ordinal))
                && args.Any(a => a.Contains("--dangerously-skip-permissions", StringComparison.Ordinal))),
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

    /// <summary>Returns false if package has no .claude/commands folder.</summary>
    [Fact]
    public async Task IsAgentPackageCompatibleAsync_ShouldReturnFalse_WhenCommandsFolderMissing()
    {
        Directory.CreateDirectory(_testDirectory);

        var result = await _sut.IsAgentPackageCompatibleAsync(_testDirectory);

        result.Should().BeFalse();
    }

    /// <summary>Returns false when package path does not exist.</summary>
    [Fact]
    public async Task IsAgentPackageCompatibleAsync_ShouldReturnFalse_WhenPackagePathDoesNotExist()
    {
        var nonExistentPath = Path.Combine(_testDirectory, "missing-package");

        var result = await _sut.IsAgentPackageCompatibleAsync(nonExistentPath);

        result.Should().BeFalse();
    }

    /// <summary>Returns true when package has .claude/commands folder.</summary>
    [Fact]
    public async Task IsAgentPackageCompatibleAsync_ShouldReturnTrue_WhenCommandsFolderExists()
    {
        Directory.CreateDirectory(Path.Combine(_testDirectory, ".claude", "commands"));

        var result = await _sut.IsAgentPackageCompatibleAsync(_testDirectory);

        result.Should().BeTrue();
    }

    /// <summary>Copies .claude files recursively during deploy.</summary>
    [Fact]
    public async Task DeployAgentPackageAsync_ShouldCopyClaudeFilesRecursively()
    {
        var packagePath = Path.Combine(_testDirectory, "package");
        var repoPath = Path.Combine(_testDirectory, "repo");
        var sourceWorkflows = Path.Combine(packagePath, ".claude", "commands");
        Directory.CreateDirectory(sourceWorkflows);
        Directory.CreateDirectory(repoPath);
        await File.WriteAllTextAsync(Path.Combine(sourceWorkflows, "agent.agent.md"), "name: CI");

        await _sut.DeployAgentPackageAsync(packagePath, repoPath);

        var targetFile = Path.Combine(repoPath, ".claude", "commands", "agent.agent.md");
        File.Exists(targetFile).Should().BeTrue();
        (await File.ReadAllTextAsync(targetFile)).Should().Be("name: CI");
    }

    /// <summary>Overwrites existing target files during deploy.</summary>
    [Fact]
    public async Task DeployAgentPackageAsync_ShouldOverwriteExistingFiles_WhenTargetExists()
    {
        var packagePath = Path.Combine(_testDirectory, "package");
        var repoPath = Path.Combine(_testDirectory, "repo");
        var sourceWorkflows = Path.Combine(packagePath, ".claude", "commands");
        var targetWorkflows = Path.Combine(repoPath, ".claude", "commands");
        Directory.CreateDirectory(sourceWorkflows);
        Directory.CreateDirectory(targetWorkflows);
        await File.WriteAllTextAsync(Path.Combine(sourceWorkflows, "agent.agent.md"), "new");
        await File.WriteAllTextAsync(Path.Combine(targetWorkflows, "agent.agent.md"), "old");

        await _sut.DeployAgentPackageAsync(packagePath, repoPath);

        (await File.ReadAllTextAsync(Path.Combine(targetWorkflows, "agent.agent.md"))).Should().Be("new");
    }

    /// <summary>Skips deploy when .claude does not exist in package.</summary>
    [Fact]
    public async Task DeployAgentPackageAsync_ShouldDoNothing_WhenClaudeFolderMissing()
    {
        var packagePath = Path.Combine(_testDirectory, "package");
        var repoPath = Path.Combine(_testDirectory, "repo");
        Directory.CreateDirectory(packagePath);
        Directory.CreateDirectory(repoPath);

        await _sut.DeployAgentPackageAsync(packagePath, repoPath);

        Directory.Exists(Path.Combine(repoPath, ".claude")).Should().BeFalse();
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
