using System.Diagnostics;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Infrastructure.Terminal;

namespace Softwareschmiede.Tests.Helpers;

/// <summary>Test-Double für <see cref="ITerminalSessionFactory"/>: Delegiert <see cref="StartAsync"/> an
/// einen übergebenen <see cref="IPseudoConsoleProcessLauncher"/> (bestehende Launcher-Doubles bleiben
/// wiederverwendbar) bzw. erzeugt ohne Launcher die deterministische In-Memory-Session.
/// Auflösung, Preflight und Backend-Wahl laufen in Service-Tests bewusst nicht mit — dafür gibt es
/// <c>TerminalSessionServiceTests</c>.</summary>
public sealed class TestTerminalSessionFactory : ITerminalSessionFactory
{
    private readonly IPseudoConsoleProcessLauncher _launcher;

    /// <summary>Erstellt eine neue Instanz von <see cref="TestTerminalSessionFactory"/>.</summary>
    /// <param name="launcher">Optionaler Launcher; ohne Angabe wird die deterministische
    /// In-Memory-Session (Prozess = CurrentProcess, MemoryStreams) erzeugt.</param>
    public TestTerminalSessionFactory(IPseudoConsoleProcessLauncher? launcher = null)
    {
        _launcher = launcher ?? new DeterministicPseudoConsoleProcessLauncher();
    }

    /// <inheritdoc/>
    public Task<TerminalSessionStartResult> StartAsync(
        Guid aufgabeId,
        TerminalSessionStartSpec spec,
        ITerminalOutputSink? outputSink,
        Func<CancellationToken, Task<bool>>? healthCheck,
        CancellationToken ct)
        => Task.FromResult(_launcher.Start(aufgabeId, spec, outputSink));

    /// <summary>Deterministischer In-Memory-Launcher: kein echter Prozessstart, Session auf
    /// MemoryStreams mit <c>TerminalSessionOptions</c>-Defaults.</summary>
    private sealed class DeterministicPseudoConsoleProcessLauncher : IPseudoConsoleProcessLauncher
    {
        public bool IsPseudoTerminal => false;

        public TerminalSessionStartResult Start(Guid aufgabeId, TerminalSessionStartSpec spec, ITerminalOutputSink? outputSink = null)
        {
            var process = Process.GetCurrentProcess();
            var session = new PseudoConsoleSession(
                NullPseudoConsoleHandle.Instance,
                process,
                new MemoryStream(),
                new MemoryStream(),
                new PseudoConsoleSessionContext
                {
                    OutputSink = outputSink,
                    Options = new TerminalSessionOptions(),
                });
            return new TerminalSessionStartResult(process, session, IsPseudoTerminal: false);
        }
    }
}
