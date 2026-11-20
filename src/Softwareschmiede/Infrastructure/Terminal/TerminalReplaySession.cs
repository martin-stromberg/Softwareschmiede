using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Domain.Terminal;

namespace Softwareschmiede.Infrastructure.Terminal;

/// <summary><see cref="ITerminalSession"/>-Implementierung, die eine <see cref="CliOutputRecording"/>
/// zeitreal durch denselben Renderpfad wie <see cref="PseudoConsoleSession"/> abspielt
/// (<see cref="AnsiSequenceParser"/> → <see cref="TerminalBuffer"/> → <see cref="BufferChanged"/>).
/// Zusätzlich zur Schnittstelle steuert sie die Wiedergabe (Start, Pause/Resume, Zeitraffer).
/// Die im Header der Aufzeichnung gespeicherte Geometrie ist die anfängliche Buffer-Geometrie.
/// Während der Wiedergabe werden aufgezeichnete xterm-Textflächen-Resizes (CSI 8;Zeilen;Spalten t)
/// auf den Buffer angewendet. <see cref="SupportsResize"/> bleibt dennoch <c>false</c>: Das
/// anzeigende <c>TerminalControl</c> darf Replay-Geometrien nicht aus seiner WPF-Größe ableiten
/// und ändert dabei niemals das Anwendungsfenster.</summary>
public sealed class TerminalReplaySession : ITerminalSession
{
    private readonly CliOutputRecording _recording;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<TerminalReplaySession> _logger;
    private readonly AnsiSequenceParser _parser = new();
    private readonly List<byte[]> _playedChunks = [];
    private readonly object _renderLock = new();
    private readonly object _statusLock = new();
    private readonly CancellationTokenSource _playbackCts = new();
    private readonly object _pauseLock = new();
    // Async-Pause-Gate (kein synchrones Wait): Task.Delay-Fortsetzungen dürfen zeitprovider-bedingt
    // synchron auf fremden Threads laufen (z. B. FakeTimeProvider.Advance) — ein blockierendes Wait
    // würde dort den fremden Thread parken (Deadlock). RunContinuationsAsynchronously sorgt
    // zusätzlich dafür, dass Resume() keine Schleifen-Fortsetzung inline ausführt.
    private TaskCompletionSource _pauseGate = CreateCompletedPauseGate();
    private readonly Process _process = new();
    private Task? _playbackTask;
    private long _timeLapseTicks = TimeSpan.MaxValue.Ticks;
    private volatile bool _isPaused;
    // Terminations-Flag für die Wiedergabe-Schleife: StepForward setzt es beim Erreichen des
    // Endes, damit eine aufgeweckte parkende Schleife deterministisch endet — auch wenn ein
    // StepBackward die Position zwischen Gate-Öffnung und Positions-Lesen wieder senkt
    // (rein positionsbasierte Termination würde dann eine zeitgesteuerte „Geister-Wiedergabe"
    // starten, obwohl Exited bereits gefeuert hat). Zugriff nur unter _renderLock; Reset in
    // StartPlayback, bewusst NICHT in StepBackward — das Resume ist Aufgabe eines
    // neuen Durchlaufs, nicht des Rückwärtsschritts.
    private bool _loopExitRequested;
    // Konsum-Merkmal zu _loopExitRequested: die Schleife setzt es unter _renderLock
    // in dem Moment, in dem sie das Flag gelesen und sich zum Abbruch entschieden hat (sie
    // liest das Flag danach nicht erneut). Ein CAS-fehlschlagendes StartPlayback kann so
    // „Flag noch ungelesen — Termination widerrufbar" von „Schleife rettungslos im Sterben —
    // Neustart an ihren Task-Abschluss hängen" unterscheiden. Zugriff nur unter _renderLock.
    private bool _loopExitConsumed;
    private int _currentChunkIndex;
    private int _playbackLoopActive;
    private int _exitedSignaled;
    private int _disposed;
    private CliRuntimeStatus _runtimeStatus = CliRuntimeStatus.Inactive;

    /// <summary>Erstellt eine neue Replay-Session über der geladenen Aufzeichnung.</summary>
    /// <param name="recording">Die abzuspielende Aufzeichnung.</param>
    /// <param name="timeProvider">Zeitquelle für die Wiedergabe-Pausen (Test-Hook).</param>
    /// <param name="logger">Logger für Diagnosemeldungen (optional).</param>
    public TerminalReplaySession(
        CliOutputRecording recording,
        TimeProvider timeProvider,
        ILogger<TerminalReplaySession>? logger = null)
    {
        _recording = recording ?? throw new ArgumentNullException(nameof(recording));
        _timeProvider = timeProvider ?? TimeProvider.System;
        _logger = logger ?? NullLogger<TerminalReplaySession>.Instance;
        Buffer = new TerminalBuffer(Math.Max(1, recording.Cols), Math.Max(1, recording.Rows));
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
    public bool SupportsResize => false;

    /// <inheritdoc/>
    public int? ExitCode { get; private set; }

    /// <inheritdoc/>
    public TerminalSessionFailedEventArgs? Failure => null;

    /// <summary>Anzahl der bereits abgespielten Chunks (0 bis <see cref="CliOutputRecording.Chunks"/>.Count).</summary>
    public int CurrentChunkIndex => Volatile.Read(ref _currentChunkIndex);

    /// <summary><c>true</c>, solange die Wiedergabe pausiert ist.</summary>
    public bool IsPaused => _isPaused;

    /// <summary>Obere Grenze der Wartezeit vor jedem Chunk: reale Pausen werden auf diesen Wert
    /// verkürzt (Zeitraffer über Leerzeiten); <see cref="TimeSpan.Zero"/> = maximale Geschwindigkeit.
    /// Jederzeit änderbar, wirkt auf alle folgenden Pausen.</summary>
    public TimeSpan TimeLapseThreshold
    {
        get => TimeSpan.FromTicks(Interlocked.Read(ref _timeLapseTicks));
        set => Interlocked.Exchange(ref _timeLapseTicks, Math.Max(0, value.Ticks));
    }

    /// <inheritdoc/>
    public event EventHandler<TerminalOutputChunkEventArgs>? OutputChunk;

    /// <inheritdoc/>
    /// <remarks>Flanken-Ereignis: signalisiert, dass ein Durchlauf das Ende der Aufzeichnung
    /// <b>erreicht hat</b> — nicht, dass die Position zum Zeitpunkt des Handlers noch am Ende
    /// steht (ein nebenläufiger <see cref="StepBackward"/> kann sie bereits wieder verlassen
    /// haben; Handler dürfen daher keinen Positions-Snapshot erwarten).</remarks>
    public event EventHandler<TerminalSessionExitedEventArgs>? Exited;

    /// <inheritdoc/>
#pragma warning disable CS0067 // Wird bewusst nie ausgelöst — Replay-Sessions haben keinen Prozess-Fehlerpfad.
    public event EventHandler<TerminalSessionFailedEventArgs>? Failed;
#pragma warning restore CS0067

    /// <inheritdoc/>
    public event EventHandler? BufferChanged;

    /// <inheritdoc/>
    public event EventHandler<CliRuntimeStatusChangedEventArgs>? RuntimeStatusChanged;

    /// <summary>Startet die zeitgesteuerte Wiedergabe ab der aktuellen Position (idempotent, solange
    /// eine Wiedergabe-Schleife lebt — nach einem beendeten Durchlauf re-armierbar, z. B. für
    /// „beendet → <see cref="StepBackward"/> → ab Position fortsetzen").</summary>
    public void StartPlayback()
    {
        if (Volatile.Read(ref _disposed) != 0)
            return;
        if (Interlocked.CompareExchange(ref _playbackLoopActive, 1, 0) != 0)
        {
            // CAS-Fail: eine Schleife lebt noch — sie kann aber bereits zum Abbruch
            // verurteilt sein (_loopExitRequested aus dem StepForward-Endpfad).
            // Ein blinder Return würde die angeforderte Wiedergabe dann lautlos verwerfen:
            // die sterbende Schleife feuert bei Position < Count kein Exited mehr und der
            // Aufrufer bliebe im Glauben einer laufenden Wiedergabe. Da Flag-Setzen
            // (StepForward), Flag-Konsum (Schleife) und dieser Pfad unter _renderLock
            // serialisiert sind, ist dort eine atomare Drei-Wege-Entscheidung möglich.
            Task? dyingLoop = null;
            var terminationCancelled = false;
            lock (_renderLock)
            {
                if (_loopExitConsumed)
                {
                    // Die Schleife hat das Flag bereits gelesen und sich zum Abbruch
                    // entschieden — sie liest es nicht erneut und ist unrettbar im
                    // Sterben: den Start an ihren Task-Abschluss hängen. Der Retry-CAS
                    // greift garantiert erst nach dem finally der alten Schleife. Ein
                    // zwischenzeitlich erneut gesetztes Flag wird mitgeräumt — es gehört
                    // zur sterbenden Schleife, nicht zum neuen Durchlauf.
                    _loopExitConsumed = false;
                    _loopExitRequested = false;
                    dyingLoop = _playbackTask;
                }
                else if (_loopExitRequested)
                {
                    // Das Flag wurde noch nicht konsumiert: Termination widerrufen —
                    // die Schleife setzt die Wiedergabe an der aktuellen Position fort.
                    // Das Ende dieses fortgesetzten Durchlaufs darf erneut Exited feuern.
                    _loopExitRequested = false;
                    Interlocked.Exchange(ref _exitedSignaled, 0);
                    terminationCancelled = true;
                }
                // Sonst: gesunde laufende Wiedergabe — der Aufruf bleibt idempotent.
            }

            if (terminationCancelled)
                SetRuntimeStatus(CliRuntimeStatus.Running);
            dyingLoop?.ContinueWith(
                // Der Retry prüft den Disposed-Zustand erneut; ein zwischenzeitliches
                // Dispose() kann dennoch das CTS schon entsorgt haben — dann ist der
                // Start obsolet und die Continuation darf nicht fehlschlagen.
                _ =>
                {
                    try
                    {
                        StartPlayback();
                    }
                    catch (ObjectDisposedException)
                    {
                    }
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            return;
        }

        // Neuer Durchlauf: das Ende-Signal darf am Schleifenende erneut feuern. Der Reset
        // läuft unter _renderLock, damit er gegen die Positions-Prüfung + das CAS in
        // RaiseExited(onlyAtEnd: true) und den Reset in StepBackward serialisiert ist —
        // lock-frei könnte er ein gerade gesetztes Signal eines nebenläufigen
        // StepForward-End-RaiseExited wieder löschen (doppeltes Exited).
        lock (_renderLock)
        {
            Interlocked.Exchange(ref _exitedSignaled, 0);
            _loopExitRequested = false;
            _loopExitConsumed = false;
        }
        SetRuntimeStatus(CliRuntimeStatus.Running);
        _playbackTask = Task.Run(() => PlaybackLoopAsync(_playbackCts.Token));
    }

    /// <summary>Hält die Wiedergabe an (kein weiterer Chunk wird angewendet, bis <see cref="Resume"/>).</summary>
    public void Pause()
    {
        // Flag und Gate unter demselben Lock ändern wie Resume: nur so gilt die Invariante
        // „_isPaused ⇒ Gate geschlossen" auch bei nebenläufigem Resume atomar.
        lock (_pauseLock)
        {
            _isPaused = true;
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
    public void Resume()
    {
        lock (_pauseLock)
        {
            _isPaused = false;
            _pauseGate.TrySetResult();
        }
    }

    /// <summary>Wendet genau den nächsten aufgezeichneten Chunk an — zeitstempel-unabhängig
    /// (keine Inter-Chunk-Pause, keine <see cref="TimeLapseThreshold"/>-Wirkung). Am Ende der
    /// Aufzeichnung feuert die Session <see cref="Exited"/> wie am Schleifenende.</summary>
    /// <returns><c>true</c>, wenn ein Chunk angewendet wurde; <c>false</c> am Ende der
    /// Aufzeichnung oder nach <see cref="Dispose"/>.</returns>
    public bool StepForward()
    {
        if (Volatile.Read(ref _disposed) != 0)
            return false;

        bool endReached;
        lock (_renderLock)
        {
            var position = _playedChunks.Count;
            if (position >= _recording.Chunks.Count)
                return false;

            ApplyChunkUnderLock(position);
            endReached = _playedChunks.Count >= _recording.Chunks.Count;
            if (endReached)
                _loopExitRequested = true;
        }

        BufferChanged?.Invoke(this, EventArgs.Empty);

        if (endReached)
        {
            RaiseExited(onlyAtEnd: true);
            // Volle Resume-Semantik (Flag + Gate): eine evtl. pausiert parkende
            // Wiedergabe-Schleife wacht auf und terminiert am gesetzten
            // _loopExitRequested deterministisch — selbst wenn ein StepBackward
            // die Position zwischen Gate-Öffnung und Positions-Lesen wieder senkt —
            // statt bis zum Dispose geparkt zu bleiben oder zeitgesteuert weiterzuspielen.
            Resume();
        }

        return true;
    }

    /// <summary>Stellt den gerenderten Zustand vor dem zuletzt angewendeten Chunk wieder her —
    /// deterministischer Neuaufbau des Buffers aus dem verbleibenden Präfix der abgespielten
    /// Chunks (das Rendering ist eine reine Funktion der Chunk-Sequenz).</summary>
    /// <returns><c>true</c>, wenn ein Schritt ausgeführt wurde; <c>false</c> an Position 0
    /// oder nach <see cref="Dispose"/>.</returns>
    public bool StepBackward()
    {
        if (Volatile.Read(ref _disposed) != 0)
            return false;

        lock (_renderLock)
        {
            if (_playedChunks.Count == 0)
                return false;

            _playedChunks.RemoveAt(_playedChunks.Count - 1);
            Volatile.Write(ref _currentChunkIndex, _playedChunks.Count);
            // Das Ende wurde verlassen — ein späterer Durchlauf darf wieder Exited feuern.
            Interlocked.Exchange(ref _exitedSignaled, 0);
            RebuildBufferAndParserFromPrefix();
        }

        BufferChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <inheritdoc/>
    /// <remarks>No-Op, der immer <c>true</c> meldet („nichts zu tun") — ab
    /// <see cref="SupportsResize"/> <c>== false</c> wird der Aufruf vom <c>TerminalControl</c>
    /// nicht mehr aufgerufen; das <c>true</c> dokumentiert die harmlose Stub-Semantik.</remarks>
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
            RebuildBufferAndParserFromPrefix();
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
            return;

        try { _playbackCts.Cancel(); } catch { }
        Resume();

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
        SetRuntimeStatus(CliRuntimeStatus.Inactive);
    }

    /// <summary>Wiedergabe-Schleife: wartet vor jedem Chunk die reale Pause (gedeckelt auf
    /// <see cref="TimeLapseThreshold"/>), respektiert das Pause-Gate und wendet den Chunk unter
    /// dem Render-Lock an — dieselbe Reihenfolge wie <see cref="PseudoConsoleSession.ReadLoopAsync"/>.</summary>
    private async Task PlaybackLoopAsync(CancellationToken ct)
    {
        var error = false;
        try
        {
            var chunks = _recording.Chunks;
            // Die Position ist _playedChunks.Count — dieselbe Quelle, die StepForward/
            // StepBackward mutieren: Einzelschritte verändern damit auch die Fortsetzposition
            // dieser Schleife.
            while (true)
            {
                // Expliziter Abbruch-Check am Schleifenanfang: WartePauseGateAsync liefert für ein
                // bereits offenes Gate einen erfüllten Task (WaitAsync wertet den Token dann nicht
                // aus) und bei delay == 0 wird Task.Delay übersprungen — ohne diese Prüfung würde
                // die Schleife nach Dispose() alle restlichen Chunks drain-en.
                ct.ThrowIfCancellationRequested();
                await WaitPauseGateAsync(ct).ConfigureAwait(false);

                int i;
                bool exitRequested;
                lock (_renderLock)
                {
                    // Flag atomar mit der Position prüfen: ein StepForward-Endpfad hat diese
                    // Schleife aufgeweckt — die Position kann seitdem wieder unter Count
                    // liegen (StepBackward), trotzdem muss die Schleife enden.
                    i = _playedChunks.Count;
                    exitRequested = _loopExitRequested;
                    if (exitRequested)
                        // Konsum unter demselben Lock vermerken: ab hier ist die
                        // Termination unwiderruflich beschlossen — ein CAS-fehlschlagendes
                        // StartPlayback erkennt das und hängt einen Neustart an den
                        // Task-Abschluss statt wirkungslos zu returnen.
                        _loopExitConsumed = true;
                }
                if (exitRequested || i >= chunks.Count)
                    break;

                var realPause = chunks[i].Offset - (i > 0 ? chunks[i - 1].Offset : TimeSpan.Zero);
                var delay = realPause <= TimeSpan.Zero
                    ? TimeSpan.Zero
                    : realPause < TimeLapseThreshold ? realPause : TimeLapseThreshold;

                if (delay > TimeSpan.Zero)
                    await Task.Delay(delay, _timeProvider, ct).ConfigureAwait(false);

                // Das Pausiert-Flag wird hier zusätzlich unter dem Render-Lock geprüft:
                // Pause() setzt es unter _pauseLock und nimmt danach den Render-Lock als
                // Fence — trifft das Pausieren zwischen Gate-Prüfung und Apply ein (relevant bei
                // FastForwardThreshold = 0, wo alle Delays 0 sind), wird der in-flight Chunk
                // zurückgehalten statt trotz Pausierung noch angewendet.
                var applied = false;
                while (!applied)
                {
                    await WaitPauseGateAsync(ct).ConfigureAwait(false);
                    // Öffnet Dispose() das Gate (Resume) während hier gewartet wird, würde der
                    // in-flight Chunk sonst noch auf der disposed Session angewendet.
                    ct.ThrowIfCancellationRequested();
                    lock (_renderLock)
                    {
                        if (_isPaused)
                            continue;

                        // Positions-Re-Check: hat ein Einzelschritt die Position seit
                        // Iterationsbeginn verändert, wird nichts angewendet und die Iteration
                        // neu begonnen — das Delay für die neue Position wird frisch berechnet
                        // (die aufgezeichnete Pause des zurückgenommenen Chunks gilt erneut).
                        // Das Beenden-Flag wird hier ebenfalls geprüft, damit kein Chunk mehr
                        // angewendet wird, nachdem der StepForward-Endpfad die Termination
                        // angefordert hat.
                        if (_loopExitRequested || _playedChunks.Count != i)
                            break;

                        ApplyChunkUnderLock(i);
                        applied = true;
                    }
                }

                if (!applied)
                    continue;

                BufferChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            error = true;
            _logger.LogWarning(ex, "Fehler in der Wiedergabe-Schleife der Terminal-Replay-Session.");
        }
        finally
        {
            // Reguläres Schleifenende: Exited nur signalisieren, wenn die Position auch jetzt
            // noch am Ende steht — ein Rückwärtsschritt kann das Ende zwischen dem Verlassen
            // der Schleife und diesem finally bereits wieder verlassen haben.
            if (!ct.IsCancellationRequested)
                RaiseExited(onlyAtEnd: !error);
            // Lauf-Flag zurücksetzen — StartPlayback ist danach wieder armiert
            // (Re-Armierung für „beendet → StepBackward → ab Position fortsetzen").
            Interlocked.Exchange(ref _playbackLoopActive, 0);
        }
    }

    /// <summary>Wendet den Chunk an Position <paramref name="position"/> in der
    /// korrektheitsrelevanten Reihenfolge an: <see cref="OutputChunk"/> → Add →
    /// Parse/Apply → Index-Write. Muss unter <see cref="_renderLock"/> aufgerufen werden.</summary>
    private void ApplyChunkUnderLock(int position)
    {
        var chunk = _recording.Chunks[position];
        OutputChunk?.Invoke(this, new TerminalOutputChunkEventArgs(chunk.Data));
        _playedChunks.Add(chunk.Data);
        foreach (var evt in _parser.Parse(chunk.Data))
            Buffer.Apply(evt);
        Volatile.Write(ref _currentChunkIndex, _playedChunks.Count);
    }

    /// <summary>Baut <see cref="Buffer"/> und den Parser-Zustand deterministisch aus dem Präfix
    /// der abgespielten Chunks neu auf (Buffer-Reset + Parser-Reset + Re-Parse über
    /// <see cref="_parser"/>). Muss unter <see cref="_renderLock"/> aufgerufen werden.</summary>
    private void RebuildBufferAndParserFromPrefix()
    {
        // TerminalBuffer.Reset() erhält absichtlich seine aktuelle Geometrie. Beim Replay muss
        // der Neuaufbau jedoch beim Header-Zustand beginnen, weil das Präfix CSI-8-Resizes
        // enthalten kann, die beim Rückwärtsschritt nicht mehr gelten.
        Buffer.Resize(Math.Max(1, _recording.Cols), Math.Max(1, _recording.Rows));
        Buffer.Reset();
        _parser.Reset();
        foreach (var chunk in _playedChunks)
            foreach (var evt in _parser.Parse(chunk))
                Buffer.Apply(evt);
    }

    /// <summary>Wartet asynchron auf das Ende einer Pausierung (sofort erfüllt, wenn nicht pausiert).</summary>
    private Task WaitPauseGateAsync(CancellationToken ct)
    {
        Task waitTask;
        lock (_pauseLock)
            waitTask = _pauseGate.Task;
        return waitTask.WaitAsync(ct);
    }

    private static TaskCompletionSource CreateCompletedPauseGate()
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        tcs.SetResult();
        return tcs;
    }

    /// <param name="onlyAtEnd"><c>true</c>: Das Signal wird nur gesetzt, wenn die Position
    /// aktuell am Ende der Aufzeichnung steht — Positions-Prüfung und Signal-Flag werden dazu
    /// atomar unter <see cref="_renderLock"/> geprüft/gesetzt (Serialisierung gegen
    /// <see cref="StepBackward"/> und den Flag-Reset in <see cref="StartPlayback"/>).
    /// Das <see cref="Exited"/>-Event selbst feuert bewusst erst nach Lock-Freigabe: Es ist
    /// ein Flanken-Ereignis („Ende wurde erreicht"), kein Positions-Snapshot — ein in die
    /// Lücke fallender <see cref="StepBackward"/> zieht das bereits gesetzte Signal nicht
    /// zurück. Ein Invoke unter dem Lock hätte Deadlock-Risiko gegenüber Handlern, die
    /// synchron auf einen anderen Thread marshaln (z. B. <c>Dispatcher.Invoke</c>, während
    /// der UI-Thread selbst in <see cref="StepBackward"/> auf <see cref="_renderLock"/>
    /// wartet).</param>
    private void RaiseExited(bool onlyAtEnd = false)
    {
        if (Volatile.Read(ref _disposed) != 0)
            return;
        if (onlyAtEnd)
        {
            lock (_renderLock)
            {
                if (_playedChunks.Count < _recording.Chunks.Count)
                    return;
                if (Interlocked.CompareExchange(ref _exitedSignaled, 1, 0) != 0)
                    return;
            }
        }
        else if (Interlocked.CompareExchange(ref _exitedSignaled, 1, 0) != 0)
        {
            return;
        }

        SetRuntimeStatus(CliRuntimeStatus.Inactive);
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
