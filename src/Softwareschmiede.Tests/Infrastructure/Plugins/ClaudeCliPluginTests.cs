using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Softwareschmiede.Domain.Abstractions;
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
        var taskFile = Directory.GetFiles(_testDirectory, "*.claude.task.md").Single();
        var taskFileName = Path.GetFileName(taskFile);
        Directory.GetFiles(_testDirectory, "*.claude.task.md").Should().ContainSingle();
        _cliRunnerMock.Verify(c => c.StreamAsync(
            "claude",
            It.Is<IEnumerable<string>>(args =>
                args.Contains("-p")
                && args.Contains("-n")
                && args.Contains("--dangerously-skip-permissions")
                && args.Contains("--verbose")
                && args.Contains("--model")
                && args.Contains("claude-model")
                && args.Contains("--agent")
                && args.Contains("test-agent")
                && args.Any(a => a.Contains("Aktuelle Anfrage", StringComparison.Ordinal))
                && !args.Contains("--prompt")
                && !args.Contains("--allow-all-tools")
                && !args.Contains("--allow-all-paths")
                && !args.Contains("--no-ask-user")
                && !args.Contains("--silent")),
            _testDirectory,
            It.Is<IDictionary<string, string>?>(env => env != null && env["ANTHROPIC_API_KEY"] == "claude-token"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Normalizes stream-json assistant text messages into user-friendly protocol lines.</summary>
    [Fact]
    public async Task StartDevelopmentAsync_ShouldNormalizeAssistantTextMessage_WhenJsonStreamIsUsed()
    {
        Directory.CreateDirectory(_testDirectory);
        _cliRunnerMock.Setup(c => c.StreamAsync(
                "claude",
                It.IsAny<IEnumerable<string>>(),
                _testDirectory,
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable("{\"type\":\"assistant\",\"message\":{\"content\":[{\"type\":\"text\",\"text\":\"Ich lese die Task-Datei.\"}]}}"));
        var agent = new AgentInfo("agent-a", null, "file");

        var lines = new List<string>();
        await foreach (var line in _sut.StartDevelopmentAsync("Prompt", agent, _testDirectory, null))
        {
            lines.Add(line);
        }

        lines.Should().ContainSingle().Which.Should().Be("[Claude] Ich lese die Task-Datei.");
    }

    /// <summary>Emits a structured rate-limit suggestion marker with reset timestamp.</summary>
    [Fact]
    public async Task StartDevelopmentAsync_ShouldEmitRateLimitSuggestionMarker_WhenRateLimitEventContainsResetUnixTime()
    {
        Directory.CreateDirectory(_testDirectory);
        _cliRunnerMock.Setup(c => c.StreamAsync(
                "claude",
                It.IsAny<IEnumerable<string>>(),
                _testDirectory,
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable("{\"type\":\"rate_limit_event\",\"rate_limit_info\":{\"resetsAt\":1780228200}}"));
        var agent = new AgentInfo("agent-a", null, "file");

        var lines = new List<string>();
        await foreach (var line in _sut.StartDevelopmentAsync("Prompt", agent, _testDirectory, null))
        {
            lines.Add(line);
        }

        lines.Should().ContainSingle();
        lines[0].Should().StartWith(ClaudeCliPlugin.RateLimitSuggestionMarker + ";resetUtc=");
        lines[0].Should().Contain(";prompt=Mach nun bitte weiter.");
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
        var capturedArgs = new List<IReadOnlyList<string>>();
        _cliRunnerMock.Setup(c => c.StreamAsync(
                "claude",
                It.IsAny<IEnumerable<string>>(),
                _testDirectory,
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                var call = capturedArgs.Count + 1;
                return call switch
                {
                    1 => ToAsyncEnumerable("{\"session_id\":\"aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee\"}"),
                    2 => ToAsyncEnumerable("{\"session_id\":\"aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee\"}"),
                    _ => ToAsyncEnumerable("ok")
                };
            })
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
        var taskId = firstRunArgs.SkipWhile(a => a != "-n").Skip(1).FirstOrDefault();
        taskId.Should().NotBeNullOrWhiteSpace();

        _cliRunnerMock.Verify(c => c.StreamAsync(
            "claude",
            It.Is<IEnumerable<string>>(args =>
                args.Contains("-n")
                && args.Contains(taskId!)
                && args.Contains("-p")
                && !args.Contains("-r")),
            _testDirectory,
            It.IsAny<IDictionary<string, string>?>(),
            It.IsAny<CancellationToken>()), Times.Once);

        _cliRunnerMock.Verify(c => c.StreamAsync(
            "claude",
            It.Is<IEnumerable<string>>(args =>
                args.Contains("-r")
                && args.Contains("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee")
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
                    1 => ToAsyncEnumerable("{\"session_id\":\"11111111-2222-3333-4444-555555555555\"}"),
                    2 => ToAsyncEnumerable("[Fehler] No conversation found with session ID: 11111111-2222-3333-4444-555555555555"),
                    3 => ToAsyncEnumerable("{\"session_id\":\"66666666-7777-8888-9999-aaaaaaaaaaaa\"}", "recovered"),
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
            It.Is<IEnumerable<string>>(args => args.Contains("-r") && args.Contains("11111111-2222-3333-4444-555555555555") && args.Contains("-p")),
            _testDirectory,
            It.IsAny<IDictionary<string, string>?>(),
            It.IsAny<CancellationToken>()), Times.Once);
        _cliRunnerMock.Verify(c => c.StreamAsync(
            "claude",
            It.Is<IEnumerable<string>>(args => args.Contains("-n") && args.Contains("11111111-2222-3333-4444-555555555555") && args.Contains("-p")),
            _testDirectory,
            It.IsAny<IDictionary<string, string>?>(),
            It.IsAny<CancellationToken>()), Times.Once);
        _cliRunnerMock.Verify(c => c.StreamAsync(
            "claude",
            It.Is<IEnumerable<string>>(args =>
                args.Contains("-p")
                && args.Contains("-n")
                && args.Any(a => a.Contains("11111111-2222-3333-4444-555555555555", StringComparison.Ordinal))),
            _testDirectory,
            It.IsAny<IDictionary<string, string>?>(),
            It.IsAny<CancellationToken>()), Times.Once);
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

    /// <summary>Uses resume arguments on large follow-up prompts while keeping CLI command stable.</summary>
    [Fact]
    public async Task StartDevelopmentAsync_ShouldUseResumeArgs_WhenLargePromptOnFollowUp()
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
        commands.Should().OnlyContain(c => c == "claude");

        var firstArgs = string.Join(" ", allArgs[0]);
        var secondArgs = string.Join(" ", allArgs[1]);
        firstArgs.Should().Contain("-n");
        firstArgs.Should().NotContain("-r");
        secondArgs.Should().Contain("-r");
        secondArgs.Should().NotContain("-n");
        firstArgs.Should().NotContain(largePrompt[..100]);
        secondArgs.Should().NotContain(largePrompt[..100]);
    }

    /// <summary>Uses direct claude command with compact file-reference prompt even for large instruction files.</summary>
    [Fact]
    public async Task StartDevelopmentAsync_ShouldUseClaudeCommand_WhenPromptIsLarge()
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
            "claude",
            It.Is<IEnumerable<string>>(args =>
                args.Contains("-p")
                && args.Any(a => a.Contains("Aktuelle Anfrage", StringComparison.Ordinal))
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

    /// <summary>Handles task_notification JSON structure without crashing.</summary>
    [Fact]
    public async Task StartDevelopmentAsync_ShouldHandleTaskNotificationJson_WhenOutputContains()
    {
        Directory.CreateDirectory(_testDirectory);
        var agent = new AgentInfo("test-agent", "description", "file");
        var lines = new[]
        {
            """{"type":"system","subtype":"task_notification","task_id":"b42i2ialp","status":"completed","summary":"Test summary"}"""
        };

        _cliRunnerMock
            .Setup(c => c.StreamAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), 
                It.IsAny<string>(), It.IsAny<IDictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable(lines));

        var result = new List<string>();
        await foreach (var line in _sut.StartDevelopmentAsync("test", agent, _testDirectory))
        {
            result.Add(line);
        }

        result.Should().HaveCountGreaterThanOrEqualTo(1);
        // Either contains task_notification or has been processed to "Task b42i2ialp"
        var firstLine = result[0];
        (firstLine.Contains("Task b42i2ialp") || firstLine.Contains("task_notification")).Should().BeTrue();
    }

    /// <summary>Gracefully handles JSON parsing errors without crashing.</summary>
    [Fact]
    public async Task StartDevelopmentAsync_ShouldNotCrash_WhenJsonParsingFails()
    {
        Directory.CreateDirectory(_testDirectory);
        var agent = new AgentInfo("test-agent", "description", "file");
        var lines = new[]
        {
            // Malformed JSON mixed with valid JSON
            """{"type":"system","subtype":"init","model":"claude-3-5-sonnet-20241022","session_id":"test-session","cwd":"/path"}""",
            """{"type":"assistant","message":{"content":[{"type":"text","text":"Hello"}]}}""",
            // This JSON has a property that is an object instead of string - would cause original code to crash
            """{"type":"system","subtype":"task_notification","task_id":"b42i2ialp","status":"completed","output_file":{"nested":"value"},"summary":"Test"}""",
            "[invalid json that is not an object",
            "plain text line"
        };

        _cliRunnerMock
            .Setup(c => c.StreamAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), 
                It.IsAny<string>(), It.IsAny<IDictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable(lines));

        // This should not throw an exception
        var result = new List<string>();
        await foreach (var line in _sut.StartDevelopmentAsync("test", agent, _testDirectory))
        {
            result.Add(line);
        }

        // We should get all lines, including error summaries for malformed JSON
        result.Should().NotBeEmpty();
        // Should contain a line indicating JSON error for the malformed output_file
        result.Should().Contain(l => l.Contains("JSON-Parsing-Fehler") || l.Contains("task_notification") || l.Contains("Task"));
        // Plain text should pass through
        result.Should().Contain("plain text line");
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
