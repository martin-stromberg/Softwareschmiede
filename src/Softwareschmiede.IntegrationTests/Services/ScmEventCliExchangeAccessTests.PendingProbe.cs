using FluentAssertions;
using Moq;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.Terminal;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Infrastructure.Terminal;
using Softwareschmiede.TestSupport;

namespace Softwareschmiede.IntegrationTests.Services;

/// <summary>Pending channel probes: bounded re-delivery, trust-dialog state and recovery.</summary>
public sealed partial class ScmEventCliExchangeAccessTests
{
    private sealed class SettableTimeProvider : TimeProvider
    {
        internal DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    /// <summary>An unanswered probe is re-sent only after the resend delay at a visible prompt, up to the cap.</summary>
    [Theory]
    [InlineData("Codex", "--sandbox workspace-write", "OpenAI Codex (v0.114.0)", "›")]
    [InlineData("ClaudeCli", "--permission-mode acceptEdits", "Claude Code v2.1.0", "❯")]
    [InlineData("Devin", "--permission-mode accept-edits --respect-workspace-trust false", "Devin CLI\nv3000.11.92", "❭")]
    [InlineData("Devin", "--permission-mode accept-edits --respect-workspace-trust false", "Devin CLI\nv3000.11.205", "❭")]
    public async Task PendingProbe_ResendsAfterDelay_UpToCap(string provider, string arguments, string banner, string prompt)
    {
        var (prefix, spec) = await Spec(provider, arguments);
        await GitInit(_root);
        var time = new SettableTimeProvider();
        var session = Session(banner, prompt);
        var run = Guid.NewGuid();
        using var adapter = new ScmPromptSessionAdapter(Guid.NewGuid(), run, prefix, spec, session.Object, [], () => true, time);
        var channel = Path.Combine(_root, ScmEventExchangeProtocol.DirectoryName);
        (await adapter.CheckExchangeAccessAsync(channel)).Status.Should().Be(ScmExchangeAccessStatus.Unknown);
        // The resend delay has not elapsed: re-checks only re-read the pending probe.
        (await adapter.CheckExchangeAccessAsync(channel)).Status.Should().Be(ScmExchangeAccessStatus.Unknown);
        session.Verify(value => value.WritePromptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        for (var sends = 1; sends < 3; sends++)
        {
            time.Now = time.Now.AddSeconds(61);
            session.Raise(value => value.BufferChanged += null, EventArgs.Empty);
            (await adapter.CheckExchangeAccessAsync(channel)).Status.Should().Be(ScmExchangeAccessStatus.Unknown);
            session.Verify(value => value.WritePromptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(sends + 1));
        }
        // The cap prevents endless prompts into a dead session; the remaining path is a regular restart.
        time.Now = time.Now.AddSeconds(61);
        session.Raise(value => value.BufferChanged += null, EventArgs.Empty);
        var exhausted = await adapter.CheckExchangeAccessAsync(channel);
        exhausted.Status.Should().Be(ScmExchangeAccessStatus.Unknown);
        exhausted.Reason.Should().Contain("neu starten");
        session.Verify(value => value.WritePromptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    /// <summary>A probe swallowed by the trust dialog is re-delivered after the user answers and the prompt is ready.</summary>
    [Theory]
    [InlineData("Codex", "--sandbox workspace-write", "OpenAI Codex (v0.114.0)", "›", "Do you trust the contents of this directory?")]
    [InlineData("ClaudeCli", "--permission-mode acceptEdits", "Claude Code v2.1.0", "❯", "Do you trust the files in this folder?")]
    [InlineData("Devin", "--permission-mode accept-edits --respect-workspace-trust true", "Devin CLI\nv3000.11.92", "❭", "Do you trust the authors of this directory?")]
    public async Task ProbeLostToTrustDialog_ResumesAfterAnswer(string provider, string arguments, string banner, string prompt, string trust)
    {
        var (prefix, spec) = await Spec(provider, arguments);
        await GitInit(_root);
        var time = new SettableTimeProvider();
        var session = Session(banner, prompt);
        var run = Guid.NewGuid();
        using var adapter = new ScmPromptSessionAdapter(Guid.NewGuid(), run, prefix, spec, session.Object, [], () => true, time);
        var channel = Path.Combine(_root, ScmEventExchangeProtocol.DirectoryName);
        (await adapter.CheckExchangeAccessAsync(channel)).Status.Should().Be(ScmExchangeAccessStatus.Unknown);
        session.Verify(value => value.WritePromptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        // The dialog swallows the already sent probe and is a distinct, non-confirmable wait state.
        SetScreen(session, banner, trust);
        adapter.ReadyStatus.Should().Be(ScmPromptReadyStatus.TrustPrompt);
        adapter.ConfirmReady(run).Should().BeFalse();
        (await adapter.CheckExchangeAccessAsync(channel)).Status.Should().Be(ScmExchangeAccessStatus.Unknown);
        session.Verify(value => value.WritePromptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        // After the user answers, the pending probe still waits for the resend delay.
        SetScreen(session, banner, prompt);
        adapter.ReadyStatus.Should().Be(ScmPromptReadyStatus.Ready);
        (await adapter.CheckExchangeAccessAsync(channel)).Status.Should().Be(ScmExchangeAccessStatus.Unknown);
        session.Verify(value => value.WritePromptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        time.Now = time.Now.AddSeconds(61);
        session.Raise(value => value.BufferChanged += null, EventArgs.Empty);
        (await adapter.CheckExchangeAccessAsync(channel)).Status.Should().Be(ScmExchangeAccessStatus.Unknown);
        session.Verify(value => value.WritePromptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        // The file tool completes the re-delivered challenge on the second attempt.
        var sent = (string)session.Invocations.Last(call => call.Method.Name == nameof(ITerminalSession.WritePromptAsync)).Arguments[0]!;
        await ScmDispatchFixture.CompleteProbeAsync(sent);
        session.Raise(value => value.BufferChanged += null, EventArgs.Empty);
        (await adapter.CheckExchangeAccessAsync(channel)).Status.Should().Be(ScmExchangeAccessStatus.Supported);
    }

    /// <summary>A trust screen while a probe is pending never receives input and never resets the probe.</summary>
    [Theory]
    [InlineData("Codex", "--sandbox workspace-write", "OpenAI Codex (v0.114.0)", "Do you trust the contents of this directory?")]
    [InlineData("ClaudeCli", "--permission-mode acceptEdits", "Claude Code v2.1.0", "Is this a project you created or one you trust?")]
    [InlineData("Devin", "--permission-mode accept-edits --respect-workspace-trust true", "Devin CLI\nv3000.11.92", "Do you trust the authors of this directory?")]
    public async Task TrustPrompt_NeverReceivesProbeOrConfirmation(string provider, string arguments, string banner, string trust)
    {
        var (prefix, spec) = await Spec(provider, arguments);
        await GitInit(_root);
        var session = Session(banner, trust);
        var run = Guid.NewGuid();
        using var adapter = new ScmPromptSessionAdapter(Guid.NewGuid(), run, prefix, spec, session.Object, [], () => true);
        adapter.ReadyStatus.Should().Be(ScmPromptReadyStatus.TrustPrompt);
        adapter.IsBusy.Should().BeFalse();
        adapter.ConfirmReady(run).Should().BeFalse();
        var channel = Path.Combine(_root, ScmEventExchangeProtocol.DirectoryName);
        (await adapter.CheckExchangeAccessAsync(channel)).Status.Should().Be(ScmExchangeAccessStatus.Unknown);
        session.Verify(value => value.WritePromptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Replaces the visible screen like the real buffer would and reports the change.</summary>
    private static void SetScreen(Mock<ITerminalSession> session, string banner, string prompt)
    {
        var buffer = new TerminalBuffer(120, 10);
        buffer.Apply(new TextWrittenEvent(banner));
        buffer.Apply(new CursorMovedEvent(2, 0, true));
        buffer.Apply(new TextWrittenEvent(prompt));
        session.SetupGet(value => value.Buffer).Returns(buffer);
        session.Raise(value => value.BufferChanged += null, EventArgs.Empty);
    }
}
