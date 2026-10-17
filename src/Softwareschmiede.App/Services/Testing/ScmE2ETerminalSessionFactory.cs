using System.Diagnostics;
using System.IO;
using Softwareschmiede.Domain.Terminal;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Infrastructure.Terminal;

namespace Softwareschmiede.App.Services.Testing;

// Installed only after validation of the isolated SCM scenario and database root.
internal sealed class ScmE2ETerminalSessionFactory(TerminalSessionService inner, ScmE2ETestConfiguration configuration) : ITerminalSessionFactory
{
    public async Task<TerminalSessionStartResult> StartAsync(Guid aufgabeId, TerminalSessionStartSpec spec,
        ITerminalOutputSink? outputSink, Func<CancellationToken, Task<bool>>? healthCheck, CancellationToken ct)
    {
        var result = await inner.StartAsync(aufgabeId, spec, outputSink, healthCheck, ct);
        return result with { Session = new FaultSession(result.Session, configuration) };
    }

    private sealed class FaultSession(ITerminalSession inner, ScmE2ETestConfiguration configuration) : ITerminalSession
    {
        public Stream InputStream => inner.InputStream;
        public Stream OutputStream => inner.OutputStream;
        public Process Process => inner.Process;
        public TerminalBuffer Buffer => inner.Buffer;
        public CliRuntimeStatus RuntimeStatus => inner.RuntimeStatus;
        public bool IsPseudoTerminal => inner.IsPseudoTerminal;
        public bool SupportsResize => inner.SupportsResize;
        public int? ExitCode => inner.ExitCode;
        public TerminalSessionFailedEventArgs? Failure => inner.Failure;
        public Task WriteInputAsync(ReadOnlyMemory<byte> bytes, CancellationToken ct = default) => inner.WriteInputAsync(bytes, ct);
        public async Task WritePromptAsync(string prompt, CancellationToken ct)
        {
            await inner.WritePromptAsync(prompt, ct);
            var fault = Path.Combine(configuration.SimulatorDirectory, "uncertain-write");
            if (File.Exists(fault))
            {
                File.Delete(fault);
                throw new IOException("Kontrollierter Transportfehler nach tatsächlich begonnenem Promptwrite.");
            }
        }
        public bool Resize(int cols, int rows) => inner.Resize(cols, rows);
        public void MarkInputActivity() => inner.MarkInputActivity();
        public void MarkOutputActivity() => inner.MarkOutputActivity();
        public Task<bool> DrainOutputAsync(TimeSpan timeout, CancellationToken ct = default) => inner.DrainOutputAsync(timeout, ct);
        public void RebuildBufferFromReplay() => inner.RebuildBufferFromReplay();
        public void Dispose() => inner.Dispose();
        public event EventHandler<TerminalOutputChunkEventArgs>? OutputChunk { add => inner.OutputChunk += value; remove => inner.OutputChunk -= value; }
        public event EventHandler<TerminalSessionExitedEventArgs>? Exited { add => inner.Exited += value; remove => inner.Exited -= value; }
        public event EventHandler<TerminalSessionFailedEventArgs>? Failed { add => inner.Failed += value; remove => inner.Failed -= value; }
        public event EventHandler? BufferChanged { add => inner.BufferChanged += value; remove => inner.BufferChanged -= value; }
        public event EventHandler<CliRuntimeStatusChangedEventArgs>? RuntimeStatusChanged { add => inner.RuntimeStatusChanged += value; remove => inner.RuntimeStatusChanged -= value; }
    }
}
