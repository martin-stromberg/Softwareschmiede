using System.Diagnostics;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Terminal;

namespace Softwareschmiede.Infrastructure.Terminal;

/// <summary>Interne Sessionhülle: auch manuelle Eingaben verwenden die gemeinsame Aufgabenserialisierung.</summary>
internal sealed class ScmCoordinatedTerminalSession(Guid taskId, ITerminalSession inner) : ITerminalSession
{
    public Action? InputStarted { get; set; }
    public Stream InputStream => inner.InputStream;
    public Stream OutputStream => inner.OutputStream;
    public Process Process => inner.Process;
    public TerminalBuffer Buffer => inner.Buffer;
    public CliRuntimeStatus RuntimeStatus => inner.RuntimeStatus;
    public bool IsPseudoTerminal => inner.IsPseudoTerminal;
    public bool SupportsResize => inner.SupportsResize;
    public int? ExitCode => inner.ExitCode;
    public TerminalSessionFailedEventArgs? Failure => inner.Failure;

    public Task WriteInputAsync(ReadOnlyMemory<byte> bytes, CancellationToken ct = default)
        => ScmTaskCoordinator.Shared.RunAsync(taskId, async () =>
        {
            InputStarted?.Invoke();
            await inner.WriteInputAsync(bytes, ct).ConfigureAwait(false);
        }, ct);

    public Task WritePromptAsync(string prompt, CancellationToken ct)
        => ScmTaskCoordinator.Shared.RunAsync(taskId, async () =>
        {
            InputStarted?.Invoke();
            await inner.WritePromptAsync(prompt, ct).ConfigureAwait(false);
        }, ct);

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
