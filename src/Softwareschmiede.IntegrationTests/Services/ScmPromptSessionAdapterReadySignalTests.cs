using System.Diagnostics;
using FluentAssertions;
using Moq;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Terminal;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Infrastructure.Terminal;

namespace Softwareschmiede.IntegrationTests.Services;

/// <summary>Das öffentliche Übergangssignal des Bereitschaftsstands: <see cref="IScmPromptSessionAdapter.ReadyStatusChanged"/>
/// feuert bei jedem tatsächlichen Wechsel mit dem neuen Wert — Grundlage der Platzfreigabe der
/// Ausführungsbegrenzung an einem verifizierten Eingabeprompt.</summary>
public sealed class ScmPromptSessionAdapterReadySignalTests
{
    private static readonly string Root = Path.GetTempPath();

    /// <summary>Der Übergang in den verifizierten Eingabeprompt meldet den neuen Status.</summary>
    [Fact]
    public void SetReady_TransitionToReady_RaisesReadyStatusChanged()
    {
        var session = Session("C:\\>");
        using var adapter = CreateAdapter(session);
        adapter.ReadyStatus.Should().Be(ScmPromptReadyStatus.Unknown);
        var statuses = new List<ScmPromptReadyStatus>();
        adapter.ReadyStatusChanged += status => statuses.Add(status);

        SetScreen(session, "SCM_READY_V1");

        statuses.Should().Equal(ScmPromptReadyStatus.Ready);
        adapter.ReadyStatus.Should().Be(ScmPromptReadyStatus.Ready);
    }

    /// <summary>Auch Rückfälle in den Arbeitszustand werden mit dem neuen Status gemeldet —
    /// unveränderte Bildschirme feuern kein weiteres Ereignis.</summary>
    [Fact]
    public void SetReady_BusyTransition_RaisesWithBusy()
    {
        var session = Session("SCM_BUSY_V1");
        using var adapter = CreateAdapter(session);
        var statuses = new List<ScmPromptReadyStatus>();
        adapter.ReadyStatusChanged += status => statuses.Add(status);

        SetScreen(session, "SCM_READY_V1");
        SetScreen(session, "SCM_BUSY_V1");
        // Ein unveränderter Bildschirminhalt ist kein neuer Übergang.
        session.Raise(x => x.BufferChanged += null, EventArgs.Empty);

        statuses.Should().Equal(ScmPromptReadyStatus.Ready, ScmPromptReadyStatus.Busy);
        adapter.ReadyStatus.Should().Be(ScmPromptReadyStatus.Busy);
    }

    /// <summary>Ein werfender Subscriber darf den Zustandswechsel und die Session-
    /// Ereignisverarbeitung nicht abbrechen — der Invoke ist gegen Subscriber-Fehler isoliert.</summary>
    [Fact]
    public void SetReady_HandlerThrows_TransitionStillCompletes()
    {
        var session = Session("C:\\>");
        using var adapter = CreateAdapter(session);
        var statuses = new List<ScmPromptReadyStatus>();
        adapter.ReadyStatusChanged += status => statuses.Add(status);
        adapter.ReadyStatusChanged += _ => throw new InvalidOperationException("Subscriber-Fehler");

        SetScreen(session, "SCM_READY_V1");

        statuses.Should().Equal(ScmPromptReadyStatus.Ready);
        adapter.ReadyStatus.Should().Be(ScmPromptReadyStatus.Ready);
    }

    private static ScmPromptSessionAdapter CreateAdapter(Mock<ITerminalSession> session)
        => new(Guid.NewGuid(), Guid.NewGuid(), "Softwareschmiede.KiSimulator", new TerminalSessionStartSpec
        {
            WorkingDirectory = Root,
            EnvironmentVariables = new Dictionary<string, string?>
            {
                ["SOFTWARESCHMIEDE_SCM_SIMULATOR"] = "1"
            }
        }, session.Object, [], () => true);

    private static Mock<ITerminalSession> Session(string prompt)
    {
        var session = new Mock<ITerminalSession>();
        session.SetupGet(x => x.Buffer).Returns(Screen(prompt));
        session.SetupGet(x => x.Process).Returns(Process.GetCurrentProcess());
        return session;
    }

    /// <summary>Ersetzt den sichtbaren Bildschirm wie der echte Puffer und meldet die Änderung.</summary>
    private static void SetScreen(Mock<ITerminalSession> session, string prompt)
    {
        session.SetupGet(x => x.Buffer).Returns(Screen(prompt));
        session.Raise(x => x.BufferChanged += null, EventArgs.Empty);
    }

    private static TerminalBuffer Screen(string prompt)
    {
        var buffer = new TerminalBuffer(120, 10);
        buffer.Apply(new TextWrittenEvent("SCM_SIMULATOR_PROTOCOL_V1"));
        buffer.Apply(new CursorMovedEvent(2, 0, true));
        buffer.Apply(new TextWrittenEvent(prompt));
        return buffer;
    }
}
