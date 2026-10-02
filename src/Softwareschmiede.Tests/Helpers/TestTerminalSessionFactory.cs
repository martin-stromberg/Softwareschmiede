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

    /// <summary>Erstellt ein Launcher-Double, das die übergebene Output-Senke direkt beim Start mit
    /// den angegebenen Test-Chunks füttert (der deterministische Standard-Launcher erzeugt die Session
    /// auf leeren MemoryStreams und füttert keine Chunks — für Recorder-/Export-Tests).</summary>
    /// <param name="chunks">Die der Senke zu meldenden Roh-Chunks.</param>
    /// <returns>Ein Launcher-Double, das beim Start die Chunks an <c>outputSink.OnOutputChunk</c> meldet.</returns>
    public static IPseudoConsoleProcessLauncher CreateChunkEmittingLauncher(IReadOnlyList<byte[]> chunks)
        => new ChunkEmittingPseudoConsoleProcessLauncher(chunks);

    /// <summary>Launcher-Double, das <c>outputSink.OnOutputChunk</c> beim Start mit vorgegebenen
    /// Chunks aufruft (zusätzlich zum Anlegen der In-Memory-Session).</summary>
    private sealed class ChunkEmittingPseudoConsoleProcessLauncher : IPseudoConsoleProcessLauncher
    {
        private readonly IReadOnlyList<byte[]> _chunks;

        public ChunkEmittingPseudoConsoleProcessLauncher(IReadOnlyList<byte[]> chunks)
        {
            _chunks = chunks;
        }

        public bool IsPseudoTerminal => false;

        public TerminalSessionStartResult Start(Guid aufgabeId, TerminalSessionStartSpec spec, ITerminalOutputSink? outputSink = null)
        {
            if (outputSink is not null)
                foreach (var chunk in _chunks)
                    outputSink.OnOutputChunk(chunk);

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
