using System.Diagnostics;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.Terminal;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Infrastructure.Plugins;
using Softwareschmiede.Infrastructure.Terminal;

namespace Softwareschmiede.IntegrationTests.Services;

/// <summary>Real Codex/Claude plugin StartSpecs and adapters, without credentials or live AI calls.</summary>
public sealed partial class ScmEventCliExchangeAccessTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "scm-access-tests-" + Guid.NewGuid().ToString("N"));

    /// <summary>Creates an isolated effective CLI workspace.</summary>
    public ScmEventCliExchangeAccessTests() => Directory.CreateDirectory(_root);

    /// <summary>A read-only simulator remains identifiable and ready while its independent channel contract denies dispatch.</summary>
    [Fact]
    public async Task ReadOnlySimulator_ReportsChannelDenialWithoutLosingPromptRecognition()
    {
        var spec = new TerminalSessionStartSpec
        {
            WorkingDirectory = _root,
            EnvironmentVariables = new Dictionary<string, string?>
            {
                ["SOFTWARESCHMIEDE_SCM_SIMULATOR"] = "1",
                ["SOFTWARESCHMIEDE_SCM_SIMULATOR_READONLY"] = "1"
            }
        };
        var session = Session("SCM_SIMULATOR_PROTOCOL_V1", "SCM_READY_V1");
        var run = Guid.NewGuid();
        using var adapter = new ScmPromptSessionAdapter(Guid.NewGuid(), run, "Softwareschmiede.KiSimulator", spec, session.Object, [], () => true);
        adapter.ReadyStatus.Should().Be(ScmPromptReadyStatus.Ready);
        adapter.ConfirmReady(run).Should().BeTrue();
        var access = await adapter.CheckExchangeAccessAsync(Path.Combine(_root, ScmEventExchangeProtocol.DirectoryName));
        access.Status.Should().Be(ScmExchangeAccessStatus.Denied);
        access.Reason.Should().Contain("schreibgeschützt");
        await FluentActions.Awaiting(() => adapter.WritePromptAsync("event")).Should().ThrowAsync<InvalidOperationException>();
        session.Verify(value => value.WritePromptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>A normal parent/start specification never proves access without an actual file-tool challenge.</summary>
    [Theory]
    [InlineData("Codex", "--sandbox workspace-write", "OpenAI Codex (v0.114.0)", "›")]
    [InlineData("ClaudeCli", "--permission-mode acceptEdits", "Claude Code v2.1.0", "❯")]
    [InlineData("Devin", "--permission-mode accept-edits --respect-workspace-trust false", "Devin CLI\nv3000.11.92", "❭")]
    public async Task ActualStartSpec_AllowsWorkspaceChannelWithoutAddingBypass(string provider, string arguments, string banner, string prompt)
    {
        var (prefix, spec) = await Spec(provider, arguments);
        spec.Arguments.Should().Be(arguments);
        spec.WorkingDirectory.Should().Be(_root);
        var mock = Session(banner, prompt);
        await GitInit(_root);
        var run = Guid.NewGuid();
        using var adapter = new ScmPromptSessionAdapter(Guid.NewGuid(), run, prefix, spec, mock.Object, [], () => true);
        var access = await adapter.CheckExchangeAccessAsync(Path.Combine(_root, ScmEventExchangeProtocol.DirectoryName));
        access.Status.Should().Be(ScmExchangeAccessStatus.Unknown, "an unrestricted parent is not the file-tool execution context");
        access.RestrictedWriteSids.Should().BeEmpty();
        access.CliRunId.Should().Be(run);
        access.WorkingDirectory.Should().Be(_root);
        adapter.ReadyStatus.Should().Be(ScmPromptReadyStatus.Busy, "only the separate access probe was sent");
        var outside = await adapter.CheckExchangeAccessAsync(Path.Combine(_root, "neighbor-app-data"));
        outside.Status.Should().Be(ScmExchangeAccessStatus.Denied);
        spec.Arguments.Should().NotContain("bypass").And.NotContain("dangerously");
        await FluentActions.Awaiting(() => adapter.WritePromptAsync("{\"body\":\"requirement\"}")).Should().ThrowAsync<InvalidOperationException>();
        mock.Verify(session => session.WritePromptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        var sent = (string)mock.Invocations.Single(call => call.Method.Name == nameof(ITerminalSession.WritePromptAsync)).Arguments[0]!;
        using var document = JsonDocument.Parse(sent);
        document.RootElement.TryGetProperty("challengePath", out _).Should().BeTrue();
        document.RootElement.TryGetProperty("request", out _).Should().BeFalse("no SCM content may be processed before access proof");
    }

    /// <summary>Arguments that can change effective rights are never accepted via a generic positive default.</summary>
    [Theory]
    [InlineData("Codex", "--sandbox read-only", ScmExchangeAccessStatus.Denied)]
    [InlineData("Codex", "--sandbox workspace-write --cd elsewhere", ScmExchangeAccessStatus.Unknown)]
    [InlineData("Codex", "--sandbox workspace-write --profile unknown", ScmExchangeAccessStatus.Unknown)]
    [InlineData("Codex", "--sandbox workspace-write -c features.shell_tool=false", ScmExchangeAccessStatus.Unknown)]
    [InlineData("ClaudeCli", "--permission-mode default", ScmExchangeAccessStatus.Unknown)]
    [InlineData("ClaudeCli", "--permission-mode plan", ScmExchangeAccessStatus.Unknown)]
    [InlineData("ClaudeCli", "--permission-mode acceptEdits --tools Read", ScmExchangeAccessStatus.Unknown)]
    [InlineData("ClaudeCli", "--permission-mode acceptEdits --settings unknown.json", ScmExchangeAccessStatus.Unknown)]
    [InlineData("ClaudeCli", "--permission-mode bypassPermissions", ScmExchangeAccessStatus.Unknown)]
    [InlineData("Devin", "--permission-mode accept-edits --respect-workspace-trust false", ScmExchangeAccessStatus.Supported)]
    [InlineData("Devin", "--permission-mode dangerous", ScmExchangeAccessStatus.Supported)]
    [InlineData("Devin", "--permission-mode bypass", ScmExchangeAccessStatus.Supported)]
    [InlineData("Devin", "--permission-mode=dangerous", ScmExchangeAccessStatus.Supported)]
    [InlineData("Devin", "--continue --permission-mode accept-edits", ScmExchangeAccessStatus.Supported)]
    [InlineData("Devin", "--resume=session-123 --permission-mode accept-edits", ScmExchangeAccessStatus.Supported)]
    [InlineData("Devin", "--sandbox --permission-mode accept-edits", ScmExchangeAccessStatus.Supported)]
    [InlineData("Devin", "--model opus --permission-mode accept-edits", ScmExchangeAccessStatus.Supported)]
    [InlineData("Devin", "--permission-mode auto", ScmExchangeAccessStatus.Unknown)]
    [InlineData("Devin", "--permission-mode smart", ScmExchangeAccessStatus.Unknown)]
    [InlineData("Devin", "--permission-mode unknown-mode", ScmExchangeAccessStatus.Unknown)]
    [InlineData("Devin", "", ScmExchangeAccessStatus.Unknown)]
    [InlineData("Devin", "--permission-mode accept-edits --config other.json", ScmExchangeAccessStatus.Unknown)]
    [InlineData("Devin", "--permission-mode accept-edits --cloud", ScmExchangeAccessStatus.Unknown)]
    [InlineData("Devin", "--permission-mode accept-edits --print", ScmExchangeAccessStatus.Unknown)]
    [InlineData("Devin", "--permission-mode accept-edits --export out.json", ScmExchangeAccessStatus.Unknown)]
    [InlineData("Devin", "--prompt-file prompt.md --permission-mode accept-edits", ScmExchangeAccessStatus.Unknown)]
    [InlineData("Devin", "--permission-mode accept-edits --respect-workspace-trust", ScmExchangeAccessStatus.Unknown)]
    [InlineData("Devin", "--permission-mode accept-edits --respect-workspace-trust maybe", ScmExchangeAccessStatus.Unknown)]
    [InlineData("Devin", "--permission-mode accept-edits --resume session-123", ScmExchangeAccessStatus.Unknown)]
    [InlineData("Devin", "--permission-mode accept-edits freeform-path", ScmExchangeAccessStatus.Unknown)]
    public async Task ActualStartSpec_RejectsOverridesWithoutMutatingThem(string provider, string arguments, ScmExchangeAccessStatus expected)
    {
        var (prefix, spec) = await Spec(provider, arguments);
        ScmCliAccessEvaluator.Evaluate(prefix, spec, Guid.NewGuid(), []).Status.Should().Be(expected);
        spec.Arguments.Should().Be(arguments);
    }

    /// <summary>Effective user/project rules survive evaluation byte-for-byte; restrictions beat CLI mode flags.</summary>
    [Theory]
    [InlineData("Codex", "config.toml", "sandbox_mode = \"workspace-write\"", ScmExchangeAccessStatus.Supported)]
    [InlineData("Codex", "config.toml", "sandbox_mode = \"read-only\"", ScmExchangeAccessStatus.Denied)]
    [InlineData("Codex", "config.toml", "sandbox_mode = \"workspace-write\"\n[features]\nshell_tool = false", ScmExchangeAccessStatus.Denied)]
    [InlineData("Codex", "config.toml", "sandbox_mode = \"workspace-write\"\n[profiles.unknown]\nsandbox_mode = \"workspace-write\"", ScmExchangeAccessStatus.Unknown)]
    [InlineData("Codex", "config.toml", "sandbox_mode = \"workspace-write\"\n[projects.\"C:/work\"]\ntrust_level = \"trusted\"", ScmExchangeAccessStatus.Supported)]
    [InlineData("Codex", "config.toml", "sandbox_mode = \"workspace-write\"\n[projects.'C:\\work']\ntrust_level = \"untrusted\"", ScmExchangeAccessStatus.Supported)]
    [InlineData("Codex", "config.toml", "sandbox_mode = \"workspace-write\"\n[projects.\"C:/work\"]\nallow = true", ScmExchangeAccessStatus.Unknown)]
    [InlineData("Codex", "config.toml", "sandbox_mode = \"workspace-write\"\n[projects.\"C:/work\"]\ntrust_level = \"maybe\"", ScmExchangeAccessStatus.Unknown)]
    [InlineData("Codex", "requirements.toml", "sandbox_mode = \"workspace-write\"", ScmExchangeAccessStatus.Unknown)]
    [InlineData("ClaudeCli", "settings.json", "{\"permissions\":{\"defaultMode\":\"acceptEdits\"}}", ScmExchangeAccessStatus.Supported)]
    [InlineData("ClaudeCli", "settings.json", "{\"permissions\":{\"defaultMode\":\"acceptEdits\",\"deny\":[\"Write\"]}}", ScmExchangeAccessStatus.Denied)]
    [InlineData("ClaudeCli", "settings.json", "{\"permissions\":{\"defaultMode\":\"acceptEdits\",\"ask\":[\"Edit\"]}}", ScmExchangeAccessStatus.Denied)]
    [InlineData("ClaudeCli", "managed-settings.json", "{\"permissions\":{\"defaultMode\":\"acceptEdits\"}}", ScmExchangeAccessStatus.Unknown)]
    [InlineData("Devin", "config.json", "{\"permissions\":{\"defaultMode\":\"accept-edits\"}}", ScmExchangeAccessStatus.Supported)]
    [InlineData("Devin", "config.json", "{\"permissionMode\":\"dangerous\"}", ScmExchangeAccessStatus.Supported)]
    [InlineData("Devin", "config.json", "{\"permissions\":{\"defaultMode\":\"accept-edits\",\"deny\":[\"Write\"]}}", ScmExchangeAccessStatus.Denied)]
    [InlineData("Devin", "config.json", "{\"permissions\":{\"defaultMode\":\"accept-edits\",\"ask\":[\"Edit\"]}}", ScmExchangeAccessStatus.Denied)]
    [InlineData("Devin", "config.json", "{\"permissions\":{\"defaultMode\":\"auto\"}}", ScmExchangeAccessStatus.Unknown)]
    [InlineData("Devin", "config.json", "{\"hooks\":{\"pre-commit\":\"x\"},\"encoding\":{\"enforced\":\"UTF-8\"}}", ScmExchangeAccessStatus.Unknown)]
    [InlineData("Devin", "config.json", "not json", ScmExchangeAccessStatus.Unknown)]
    [InlineData("Devin", "config.json", "[1,2]", ScmExchangeAccessStatus.Unknown)]
    [InlineData("Devin", "managed-config.json", "{\"permissionMode\":\"dangerous\"}", ScmExchangeAccessStatus.Unknown)]
    public async Task ConfigurationFixtures_EnforceRestrictionsAndPreserveFiles(string provider, string fileName, string content, ScmExchangeAccessStatus expected)
    {
        var (prefix, spec) = await Spec(provider, "");
        var path = Path.Combine(_root, fileName);
        await File.WriteAllTextAsync(path, content);
        var bytes = await File.ReadAllBytesAsync(path);
        ScmCliConfigurationFile[] files = [new(path, content)];
        ScmCliAccessEvaluator.Evaluate(prefix, spec, Guid.NewGuid(), files).Status.Should().Be(expected);
        (await File.ReadAllBytesAsync(path)).Should().Equal(bytes);
    }

    /// <summary>Changed files or a new run invalidate previous access; readiness confirmation cannot restore access.</summary>
    [Theory]
    [InlineData("Codex", "sandbox_mode = \"workspace-write\"", "OpenAI Codex (v0.114.0)", "›")]
    [InlineData("ClaudeCli", "{\"permissions\":{\"defaultMode\":\"acceptEdits\"}}", "Claude Code v2.1.0", "❯")]
    [InlineData("Devin", "{\"permissions\":{\"defaultMode\":\"accept-edits\"}}", "Devin CLI\nv3000.11.92", "❭")]
    public async Task LivingRun_RequiresUnchangedCapturedConfiguration(string provider, string content, string banner, string prompt)
    {
        var (prefix, spec) = await Spec(provider, "");
        var path = Path.Combine(_root, provider == "Codex" ? "config.toml" : provider == "Devin" ? "config.json" : "settings.json");
        await File.WriteAllTextAsync(path, content);
        await GitInit(_root);
        var current = true;
        var run = Guid.NewGuid();
        var session = Session(banner, prompt);
        // This test checks configuration invalidation; the separate restricted-child test proves
        // the real security boundary. Here a deterministic regular tool executes the same probe.
        session.Setup(value => value.WritePromptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string text, CancellationToken token) => CompleteProbeAsync(text, token));
        using var adapter = new ScmPromptSessionAdapter(Guid.NewGuid(), run, prefix, spec, session.Object, [new(path, content)], () => current);
        var channel = Path.Combine(_root, ScmEventExchangeProtocol.DirectoryName);
        (await adapter.CheckExchangeAccessAsync(channel)).Status.Should().Be(ScmExchangeAccessStatus.Unknown);
        session.Raise(value => value.BufferChanged += null, EventArgs.Empty);
        (await adapter.CheckExchangeAccessAsync(channel)).Status.Should().Be(ScmExchangeAccessStatus.Supported);
        await File.AppendAllTextAsync(path, " ");
        adapter.ConfirmReady(run).Should().BeTrue();
        (await adapter.CheckExchangeAccessAsync(channel)).Status.Should().Be(ScmExchangeAccessStatus.Unknown);
        await File.WriteAllTextAsync(path, content);
        (await adapter.CheckExchangeAccessAsync(channel)).Status.Should().Be(ScmExchangeAccessStatus.Supported);
        current = false;
        adapter.ConfirmReady(run).Should().BeFalse();
        (await adapter.CheckExchangeAccessAsync(channel)).Status.Should().Be(ScmExchangeAccessStatus.Unknown);
    }

    private static async Task CompleteProbeAsync(string prompt, CancellationToken ct = default)
    {
        using var document = JsonDocument.Parse(prompt);
        string PathValue(string name) => document.RootElement.GetProperty(name).GetString()!;
        var content = await File.ReadAllBytesAsync(PathValue("challengePath"), ct);
        await File.WriteAllBytesAsync(PathValue("temporaryProofPath"), content, ct);
        File.Move(PathValue("temporaryProofPath"), PathValue("proofPath"));
        File.Move(PathValue("challengePath"), PathValue("readChallengePath"));
    }

    private async Task<(string Prefix, TerminalSessionStartSpec Spec)> Spec(string provider, string arguments)
    {
        var credentials = new Mock<ICredentialStore>(MockBehavior.Strict);
        credentials.Setup(value => value.GetCredential(It.IsAny<string>())).Returns((string?)null);
        credentials.Setup(value => value.GetCredential("Softwareschmiede." + provider + ".CommandLineParameters")).Returns(arguments);
        IKiPlugin plugin = provider switch
        {
            "Codex" => new CodexPlugin(credentials.Object, NullLogger<CodexPlugin>.Instance),
            "Devin" => new DevinPlugin(credentials.Object, NullLogger<DevinPlugin>.Instance),
            _ => new ClaudeCliPlugin(credentials.Object, NullLogger<ClaudeCliPlugin>.Instance),
        };
        return (plugin.PluginPrefix, await plugin.GetTerminalStartSpecAsync(_root));
    }

    private static Mock<ITerminalSession> Session(string banner, string prompt)
    {
        var buffer = new TerminalBuffer(120, 10);
        buffer.Apply(new TextWrittenEvent(banner));
        buffer.Apply(new CursorMovedEvent(2, 0, true));
        buffer.Apply(new TextWrittenEvent(prompt));
        var session = new Mock<ITerminalSession>();
        session.SetupGet(value => value.Buffer).Returns(buffer);
        session.SetupGet(value => value.Process).Returns(Process.GetCurrentProcess());
        session.Setup(value => value.WritePromptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        return session;
    }

    /// <summary>Removes only the isolated fixture workspace.</summary>
    public void Dispose() => Directory.Delete(_root, recursive: true);
}
