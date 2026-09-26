using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Domain.Terminal;

namespace Softwareschmiede.Infrastructure.Terminal;

/// <summary><see cref="ITerminalSession"/>-Implementierung, die eine <see cref="CliOutputAufzeichnung"/>
/// zeitreal durch denselben Renderpfad wie <see cref="PseudoConsoleSession"/> abspielt
/// (<see cref="AnsiSequenceParser"/> → <see cref="TerminalBuffer"/> → <see cref="BufferChanged"/>).
/// Zusätzlich zur Schnittstelle steuert sie die Wiedergabe (Start, Pause/Fortsetzen, Zeitraffer).
/// Die im Header der Aufzeichnung gespeicherte Geometrie dient nur der initialen Buffer-Größe —
/// die Wiedergabe nutzt die aktuelle Control-Geometrie (<c>TerminalControl</c> resized den Buffer
/// beim Binden); Resize-Ereignisse werden nicht aufgezeichnet und nicht reproduziert.</summary>
public sealed class TerminalReplaySession : ITerminalSession
{
    private readonly CliOutputAufzeichnung _aufzeichnung;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<TerminalReplaySession> _logger;
    private readonly AnsiSequenceParser _parser = new();
    private readonly List<byte[]> _abgespielteChunks = [];
    private readonly object _renderLock = new();
    private readonly object _statusLock = new();
    private readonly CancellationTokenSource _playbackCts = new();
    private readonly object _pauseLock = new();
    // Async-Pause-Gate (kein synchrones Wait): Task.Delay-Fortsetzungen dürfen zeitprovider-bedingt
    // synchron auf fremden Threads laufen (z. B. FakeTimeProvider.Advance) — ein blockierendes Wait
    // würde dort den fremden Thread parken (Deadlock). RunContinuationsAsynchronously sorgt
    // zusätzlich dafür, dass Fortsetzen() keine Schleifen-Fortsetzung inline ausführt.
    private TaskCompletionSource _pauseGate = CreateCompletedPauseGate();
    private readonly Process _process = new();
    private Task? _playbackTask;
    private long _zeitrafferTicks = TimeSpan.MaxValue.Ticks;
    private volatile bool _istPausiert;
    private int _aktuellerChunkIndex;
    private int _wiedergabeGestartet;
    private int _exitedSignaled;
    private int _disposed;
    private CliRuntimeStatus _runtimeStatus = CliRuntimeStatus.Inaktiv;

    /// <summary>Erstellt eine neue Replay-Session über der geladenen Aufzeichnung.</summary>
    /// <param name="aufzeichnung">Die abzuspielende Aufzeichnung.</param>
    /// <param name="timeProvider">Zeitquelle für die Wiedergabe-Pausen (Test-Hook).</param>
    /// <param name="logger">Logger für Diagnosemeldungen (optional).</param>
    public TerminalReplaySession(
        CliOutputAufzeichnung aufzeichnung,
        TimeProvider timeProvider,
        ILogger<TerminalReplaySession>? logger = null)
    {
        _aufzeichnung = aufzeichnung ?? throw new ArgumentNullException(nameof(aufzeichnung));
        _timeProvider = timeProvider ?? TimeProvider.System;
        _logger = logger ?? NullLogger<TerminalReplaySession>.Instance;
        Buffer = new TerminalBuffer(Math.Max(1, aufzeichnung.Cols), Math.Max(1, aufzeichnung.Rows));
    }

    /// <inheritdoc/>
    public Stream InputStream => Stream.Null;

    /// <inheritdoc/>
    public Stream OutputStream => Stream.Null;

    /// <inheritdoc/>
    /// <remarks>Stub: ein <b>nicht gestartetes</b> <see cref="Process"/>-Objekt — nahezu jeder
    /// Zugriff (<see cref="Process.Id"/>, <see cref="Process.HasExited"/>, <see cref="Process.ExitCode"/>)
    /// wirft <see cref="InvalidOperationException"/>. Bestehende Konsumenten fangen genau diesen
    /// Fehlermodus ab (<c>TaskDetailView.TryGetProcessId</c>).</remarks>
    public Process Process => _process;

    /// <inheritdoc/>
    public TerminalBuffer Buffer { get; }

    /// <inheritdoc/>
    public CliRuntimeStatus RuntimeStatus
    {
        get { lock (_statusLock) return _runtimeStatus; }
    }

    /// <inheritdoc/>
    public bool IsPseudoTerminal => false;

    /// <inheritdoc/>
    public int? ExitCode { get; private set; }

    /// <inheritdoc/>
    public TerminalSessionFailedEventArgs? Failure => null;

    /// <summary>Anzahl der bereits abgespielten Chunks (0 bis <see cref="CliOutputAufzeichnung.Chunks"/>.Count).</summary>
    public int AktuellerChunkIndex => Volatile.Read(ref _aktuellerChunkIndex);

    /// <summary><c>true</c>, solange die Wiedergabe pausiert ist.</summary>
    public bool IstPausiert => _istPausiert;

    /// <summary>Obere Grenze der Wartezeit vor jedem Chunk: reale Pausen werden auf diesen Wert
    /// verkürzt (Zeitraffer über Leerzeiten); <see cref="TimeSpan.Zero"/> = maximale Geschwindigkeit.
    /// Jederzeit änderbar, wirkt auf alle folgenden Pausen.</summary>
    public TimeSpan ZeitrafferSchwelle
    {
        get => TimeSpan.FromTicks(Interlocked.Read(ref _zeitrafferTicks));
        set => Interlocked.Exchange(ref _zeitrafferTicks, Math.Max(0, value.Ticks));
    }

    /// <inheritdoc/>
    public event EventHandler<TerminalOutputChunkEventArgs>? OutputChunk;

    /// <inheritdoc/>
    public event EventHandler<TerminalSessionExitedEventArgs>? Exited;

    /// <inheritdoc/>
#pragma warning disable CS0067 // Wird bewusst nie ausgelöst — Replay-Sessions haben keinen Prozess-Fehlerpfad.
    public event EventHandler<TerminalSessionFailedEventArgs>? Failed;
#pragma warning restore CS0067

    /// <inheritdoc/>
    public event EventHandler? BufferChanged;

    /// <inheritdoc/>
    public event EventHandler<CliRuntimeStatusChangedEventArgs>? RuntimeStatusChanged;

    /// <summary>Startet die Wiedergabe der Aufzeichnung (idempotent — ein bereits gestarteter oder
    /// beendeter Durchlauf wird nicht erneut gestartet).</summary>
    public void WiedergabeStarten()
    {
        if (Volatile.Read(ref _disposed) != 0)
            return;
        if (Interlocked.CompareExchange(ref _wiedergabeGestartet, 1, 0) != 0)
            return;

        SetRuntimeStatus(CliRuntimeStatus.Laeuft);
        _playbackTask = Task.Run(() => WiedergabeLoopAsync(_playbackCts.Token));
    }

    /// <summary>Hält die Wiedergabe an (kein weiterer Chunk wird angewendet, bis <see cref="Fortsetzen"/>).</summary>
    public void Pausieren()
    {
        // Flag und Gate unter demselben Lock ändern wie Fortsetzen: nur so gilt die Invariante
        // „_istPausiert ⇒ Gate geschlossen" auch bei nebenläufigem Fortsetzen atomar.
        lock (_pauseLock)
        {
            _istPausiert = true;
            if (_pauseGate.Task.IsCompleted)
                _pauseGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        // Kurzer Render-Lock als Fence: ein in-flight Chunk (Gate bereits passiert, Apply noch
        // ausstehend) wird so zuverlässig abgewartet bzw. durch das gesetzte Flag zurückgehalten —
        // nach Rückkehr dieser Methode wird garantiert kein Chunk mehr angewendet.
        lock (_renderLock)
        {
        }
    }

    /// <summary>Setzt eine pausierte Wiedergabe exakt an der angehaltenen Position fort.</summary>
    public void Fortsetzen()
    {
        lock (_pauseLock)
        {
            _istPausiert = false;
            _pauseGate.TrySetResult();
        }
    }

    /// <inheritdoc/>
    public bool Resize(int cols, int rows) => true;

    /// <inheritdoc/>
    public Task WriteInputAsync(ReadOnlyMemory<byte> bytes, CancellationToken ct = default) => Task.CompletedTask;

    /// <inheritdoc/>
    public Task WritePromptAsync(string prompt, CancellationToken ct) => Task.CompletedTask;

    /// <inheritdoc/>
    public void MarkInputActivity()
    {
    }

    /// <inheritdoc/>
    public void MarkOutputActivity()
    {
    }

    /// <inheritdoc/>
    public Task<bool> DrainOutputAsync(TimeSpan timeout, CancellationToken ct = default)
        => Task.FromResult(true);

    /// <summary>Baut <see cref="Buffer"/> synchron aus den bis dahin abgespielten Chunks neu auf
    /// (unter demselben Render-Lock wie die Wiedergabe-Schleife — kein Vermischen mit Live-Chunks).</summary>
    public void RebuildBufferFromReplay()
    {
        lock (_renderLock)
        {
            Buffer.Reset();
            var parser = new AnsiSequenceParser();
            foreach (var chunk in _abgespielteChunks)
                foreach (var evt in parser.Parse(chunk))
                    Buffer.Apply(evt);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
            return;

        try { _playbackCts.Cancel(); } catch { }
        Fortsetzen();

        // Analog PseudoConsoleSession: kein synchrones Warten auf die Wiedergabe-Schleife;
        // das CTS wird erst entsorgt, wenn der Task tatsächlich beendet ist.
        _playbackTask?.ContinueWith(
            _ => { try { _playbackCts.Dispose(); } catch { } },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
        if (_playbackTask is null)
        {
            try { _playbackCts.Dispose(); } catch { }
        }

        try { _process.Dispose(); } catch { }
        SetRuntimeStatus(CliRuntimeStatus.Inaktiv);
    }

    /// <summary>Wiedergabe-Schleife: wartet vor jedem Chunk die reale Pause (gedeckelt auf
    /// <see cref="ZeitrafferSchwelle"/>), respektiert das Pause-Gate und wendet den Chunk unter
    /// dem Render-Lock an — dieselbe Reihenfolge wie <see cref="PseudoConsoleSession.ReadLoopAsync"/>.</summary>
    private async Task WiedergabeLoopAsync(CancellationToken ct)
    {
        var vorherigerOffset = TimeSpan.Zero;
        try
        {
            var chunks = _aufzeichnung.Chunks;
            for (var i = 0; i < chunks.Count; i++)
            {
                await WartePauseGateAsync(ct).ConfigureAwait(false);

                var chunk = chunks[i];
                var realePause = chunk.Offset - vorherigerOffset;
                vorherigerOffset = chunk.Offset;
                var delay = realePause <= TimeSpan.Zero
                    ? TimeSpan.Zero
                    : realePause < ZeitrafferSchwelle ? realePause : ZeitrafferSchwelle;

                if (delay > TimeSpan.Zero)
                    await Task.Delay(delay, _timeProvider, ct).ConfigureAwait(false);

                // Das Pausiert-Flag wird hier zusätzlich unter dem Render-Lock geprüft:
                // Pausieren() setzt es unter _pauseLock und nimmt danach den Render-Lock als
                // Fence — trifft das Pausieren zwischen Gate-Prüfung und Apply ein (relevant bei
                // ZeitrafferSchwelle = 0, wo alle Delays 0 sind), wird der in-flight Chunk
                // zurückgehalten statt trotz Pausierung noch angewendet.
                var angewendet = false;
                while (!angewendet)
                {
                    await WartePauseGateAsync(ct).ConfigureAwait(false);
                    lock (_renderLock)
                    {
                        if (_istPausiert)
                            continue;

                        OutputChunk?.Invoke(this, new TerminalOutputChunkEventArgs(chunk.Data));
                        _abgespielteChunks.Add(chunk.Data);
                        foreach (var evt in _parser.Parse(chunk.Data))
                            Buffer.Apply(evt);
                        angewendet = true;
                    }
                }

                Interlocked.Exchange(ref _aktuellerChunkIndex, i + 1);
                BufferChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fehler in der Wiedergabe-Schleife der Terminal-Replay-Session.");
        }
        finally
        {
            if (!ct.IsCancellationRequested)
                RaiseExited();
        }
    }

    /// <summary>Wartet asynchron auf das Ende einer Pausierung (sofort erfüllt, wenn nicht pausiert).</summary>
    private Task WartePauseGateAsync(CancellationToken ct)
    {
        Task warteTask;
        lock (_pauseLock)
            warteTask = _pauseGate.Task;
        return warteTask.WaitAsync(ct);
    }

    private static TaskCompletionSource CreateCompletedPauseGate()
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        tcs.SetResult();
        return tcs;
    }

    private void RaiseExited()
    {
        if (Volatile.Read(ref _disposed) != 0)
            return;
        if (Interlocked.CompareExchange(ref _exitedSignaled, 1, 0) != 0)
            return;

        SetRuntimeStatus(CliRuntimeStatus.Inaktiv);
        try
        {
            Exited?.Invoke(this, new TerminalSessionExitedEventArgs(null));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fehler im Exited-Eventhandler der Terminal-Replay-Session.");
        }
    }

    private void SetRuntimeStatus(CliRuntimeStatus status)
    {
        var changed = false;
        lock (_statusLock)
        {
            if (_runtimeStatus != status)
            {
                _runtimeStatus = status;
                changed = true;
            }
        }

        if (changed)
            RuntimeStatusChanged?.Invoke(this, new CliRuntimeStatusChangedEventArgs(status));
    }
}
