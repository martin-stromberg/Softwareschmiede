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
    // Terminations-Flag für die Wiedergabe-Schleife: SchrittVor setzt es beim Erreichen des
    // Endes, damit eine aufgeweckte parkende Schleife deterministisch endet — auch wenn ein
    // SchrittZurueck die Position zwischen Gate-Öffnung und Positions-Lesen wieder senkt
    // (rein positionsbasierte Termination würde dann eine zeitgesteuerte „Geister-Wiedergabe"
    // starten, obwohl Exited bereits gefeuert hat). Zugriff nur unter _renderLock; Reset in
    // WiedergabeStarten, bewusst NICHT in SchrittZurueck — das Fortsetzen ist Aufgabe eines
    // neuen Durchlaufs, nicht des Rückwärtsschritts.
    private bool _schleifeBeendenAngefordert;
    // Konsum-Merkmal zu _schleifeBeendenAngefordert: die Schleife setzt es unter _renderLock
    // in dem Moment, in dem sie das Flag gelesen und sich zum Abbruch entschieden hat (sie
    // liest das Flag danach nicht erneut). Ein CAS-fehlschlagendes WiedergabeStarten kann so
    // „Flag noch ungelesen — Termination widerrufbar" von „Schleife rettungslos im Sterben —
    // Neustart an ihren Task-Abschluss hängen" unterscheiden. Zugriff nur unter _renderLock.
    private bool _schleifeBeendenKonsumiert;
    private int _aktuellerChunkIndex;
    private int _wiedergabeLoopAktiv;
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
    /// <remarks>Flanken-Ereignis: signalisiert, dass ein Durchlauf das Ende der Aufzeichnung
    /// <b>erreicht hat</b> — nicht, dass die Position zum Zeitpunkt des Handlers noch am Ende
    /// steht (ein nebenläufiger <see cref="SchrittZurueck"/> kann sie bereits wieder verlassen
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
    /// „beendet → <see cref="SchrittZurueck"/> → ab Position fortsetzen").</summary>
    public void WiedergabeStarten()
    {
        if (Volatile.Read(ref _disposed) != 0)
            return;
        if (Interlocked.CompareExchange(ref _wiedergabeLoopAktiv, 1, 0) != 0)
        {
            // CAS-Fail: eine Schleife lebt noch — sie kann aber bereits zum Abbruch
            // verurteilt sein (_schleifeBeendenAngefordert aus dem SchrittVor-Endpfad).
            // Ein blinder Return würde die angeforderte Wiedergabe dann lautlos verwerfen:
            // die sterbende Schleife feuert bei Position < Count kein Exited mehr und der
            // Aufrufer bliebe im Glauben einer laufenden Wiedergabe. Da Flag-Setzen
            // (SchrittVor), Flag-Konsum (Schleife) und dieser Pfad unter _renderLock
            // serialisiert sind, ist dort eine atomare Drei-Wege-Entscheidung möglich.
            Task? sterbendeSchleife = null;
            var terminationWiderrufen = false;
            lock (_renderLock)
            {
                if (_schleifeBeendenKonsumiert)
                {
                    // Die Schleife hat das Flag bereits gelesen und sich zum Abbruch
                    // entschieden — sie liest es nicht erneut und ist unrettbar im
                    // Sterben: den Start an ihren Task-Abschluss hängen. Der Retry-CAS
                    // greift garantiert erst nach dem finally der alten Schleife. Ein
                    // zwischenzeitlich erneut gesetztes Flag wird mitgeräumt — es gehört
                    // zur sterbenden Schleife, nicht zum neuen Durchlauf.
                    _schleifeBeendenKonsumiert = false;
                    _schleifeBeendenAngefordert = false;
                    sterbendeSchleife = _playbackTask;
                }
                else if (_schleifeBeendenAngefordert)
                {
                    // Das Flag wurde noch nicht konsumiert: Termination widerrufen —
                    // die Schleife setzt die Wiedergabe an der aktuellen Position fort.
                    // Das Ende dieses fortgesetzten Durchlaufs darf erneut Exited feuern.
                    _schleifeBeendenAngefordert = false;
                    Interlocked.Exchange(ref _exitedSignaled, 0);
                    terminationWiderrufen = true;
                }
                // Sonst: gesunde laufende Wiedergabe — der Aufruf bleibt idempotent.
            }

            if (terminationWiderrufen)
                SetRuntimeStatus(CliRuntimeStatus.Laeuft);
            sterbendeSchleife?.ContinueWith(
                // Der Retry prüft den Disposed-Zustand erneut; ein zwischenzeitliches
                // Dispose() kann dennoch das CTS schon entsorgt haben — dann ist der
                // Start obsolet und die Continuation darf nicht fehlschlagen.
                _ =>
                {
                    try
                    {
                        WiedergabeStarten();
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
        // RaiseExited(nurAmEnde: true) und den Reset in SchrittZurueck serialisiert ist —
        // lock-frei könnte er ein gerade gesetztes Signal eines nebenläufigen
        // SchrittVor-End-RaiseExited wieder löschen (doppeltes Exited).
        lock (_renderLock)
        {
            Interlocked.Exchange(ref _exitedSignaled, 0);
            _schleifeBeendenAngefordert = false;
            _schleifeBeendenKonsumiert = false;
        }
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

    /// <summary>Wendet genau den nächsten aufgezeichneten Chunk an — zeitstempel-unabhängig
    /// (keine Inter-Chunk-Pause, keine <see cref="ZeitrafferSchwelle"/>-Wirkung). Am Ende der
    /// Aufzeichnung feuert die Session <see cref="Exited"/> wie am Schleifenende.</summary>
    /// <returns><c>true</c>, wenn ein Chunk angewendet wurde; <c>false</c> am Ende der
    /// Aufzeichnung oder nach <see cref="Dispose"/>.</returns>
    public bool SchrittVor()
    {
        if (Volatile.Read(ref _disposed) != 0)
            return false;

        bool endeErreicht;
        lock (_renderLock)
        {
            var position = _abgespielteChunks.Count;
            if (position >= _aufzeichnung.Chunks.Count)
                return false;

            WendeChunkAnUnterLock(position);
            endeErreicht = _abgespielteChunks.Count >= _aufzeichnung.Chunks.Count;
            if (endeErreicht)
                _schleifeBeendenAngefordert = true;
        }

        BufferChanged?.Invoke(this, EventArgs.Empty);

        if (endeErreicht)
        {
            RaiseExited(nurAmEnde: true);
            // Volle Fortsetzen-Semantik (Flag + Gate): eine evtl. pausiert parkende
            // Wiedergabe-Schleife wacht auf und terminiert am gesetzten
            // _schleifeBeendenAngefordert deterministisch — selbst wenn ein SchrittZurueck
            // die Position zwischen Gate-Öffnung und Positions-Lesen wieder senkt —
            // statt bis zum Dispose geparkt zu bleiben oder zeitgesteuert weiterzuspielen.
            Fortsetzen();
        }

        return true;
    }

    /// <summary>Stellt den gerenderten Zustand vor dem zuletzt angewendeten Chunk wieder her —
    /// deterministischer Neuaufbau des Buffers aus dem verbleibenden Präfix der abgespielten
    /// Chunks (das Rendering ist eine reine Funktion der Chunk-Sequenz).</summary>
    /// <returns><c>true</c>, wenn ein Schritt ausgeführt wurde; <c>false</c> an Position 0
    /// oder nach <see cref="Dispose"/>.</returns>
    public bool SchrittZurueck()
    {
        if (Volatile.Read(ref _disposed) != 0)
            return false;

        lock (_renderLock)
        {
            if (_abgespielteChunks.Count == 0)
                return false;

            _abgespielteChunks.RemoveAt(_abgespielteChunks.Count - 1);
            Volatile.Write(ref _aktuellerChunkIndex, _abgespielteChunks.Count);
            // Das Ende wurde verlassen — ein späterer Durchlauf darf wieder Exited feuern.
            Interlocked.Exchange(ref _exitedSignaled, 0);
            BaueBufferUndParserAusPraefixNeuAuf();
        }

        BufferChanged?.Invoke(this, EventArgs.Empty);
        return true;
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
            BaueBufferUndParserAusPraefixNeuAuf();
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
        var fehler = false;
        try
        {
            var chunks = _aufzeichnung.Chunks;
            // Die Position ist _abgespielteChunks.Count — dieselbe Quelle, die SchrittVor/
            // SchrittZurueck mutieren: Einzelschritte verändern damit auch die Fortsetzposition
            // dieser Schleife.
            while (true)
            {
                // Expliziter Abbruch-Check am Schleifenanfang: WartePauseGateAsync liefert für ein
                // bereits offenes Gate einen erfüllten Task (WaitAsync wertet den Token dann nicht
                // aus) und bei delay == 0 wird Task.Delay übersprungen — ohne diese Prüfung würde
                // die Schleife nach Dispose() alle restlichen Chunks drain-en.
                ct.ThrowIfCancellationRequested();
                await WartePauseGateAsync(ct).ConfigureAwait(false);

                int i;
                bool beendenAngefordert;
                lock (_renderLock)
                {
                    // Flag atomar mit der Position prüfen: ein SchrittVor-Endpfad hat diese
                    // Schleife aufgeweckt — die Position kann seitdem wieder unter Count
                    // liegen (SchrittZurueck), trotzdem muss die Schleife enden.
                    i = _abgespielteChunks.Count;
                    beendenAngefordert = _schleifeBeendenAngefordert;
                    if (beendenAngefordert)
                        // Konsum unter demselben Lock vermerken: ab hier ist die
                        // Termination unwiderruflich beschlossen — ein CAS-fehlschlagendes
                        // WiedergabeStarten erkennt das und hängt einen Neustart an den
                        // Task-Abschluss statt wirkungslos zu returnen.
                        _schleifeBeendenKonsumiert = true;
                }
                if (beendenAngefordert || i >= chunks.Count)
                    break;

                var realePause = chunks[i].Offset - (i > 0 ? chunks[i - 1].Offset : TimeSpan.Zero);
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
                    // Öffnet Dispose() das Gate (Fortsetzen) während hier gewartet wird, würde der
                    // in-flight Chunk sonst noch auf der disposed Session angewendet.
                    ct.ThrowIfCancellationRequested();
                    lock (_renderLock)
                    {
                        if (_istPausiert)
                            continue;

                        // Positions-Re-Check: hat ein Einzelschritt die Position seit
                        // Iterationsbeginn verändert, wird nichts angewendet und die Iteration
                        // neu begonnen — das Delay für die neue Position wird frisch berechnet
                        // (die aufgezeichnete Pause des zurückgenommenen Chunks gilt erneut).
                        // Das Beenden-Flag wird hier ebenfalls geprüft, damit kein Chunk mehr
                        // angewendet wird, nachdem der SchrittVor-Endpfad die Termination
                        // angefordert hat.
                        if (_schleifeBeendenAngefordert || _abgespielteChunks.Count != i)
                            break;

                        WendeChunkAnUnterLock(i);
                        angewendet = true;
                    }
                }

                if (!angewendet)
                    continue;

                BufferChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            fehler = true;
            _logger.LogWarning(ex, "Fehler in der Wiedergabe-Schleife der Terminal-Replay-Session.");
        }
        finally
        {
            // Reguläres Schleifenende: Exited nur signalisieren, wenn die Position auch jetzt
            // noch am Ende steht — ein Rückwärtsschritt kann das Ende zwischen dem Verlassen
            // der Schleife und diesem finally bereits wieder verlassen haben.
            if (!ct.IsCancellationRequested)
                RaiseExited(nurAmEnde: !fehler);
            // Lauf-Flag zurücksetzen — WiedergabeStarten ist danach wieder armiert
            // (Re-Armierung für „beendet → SchrittZurueck → ab Position fortsetzen").
            Interlocked.Exchange(ref _wiedergabeLoopAktiv, 0);
        }
    }

    /// <summary>Wendet den Chunk an Position <paramref name="position"/> in der
    /// korrektheitsrelevanten Reihenfolge an: <see cref="OutputChunk"/> → Add →
    /// Parse/Apply → Index-Write. Muss unter <see cref="_renderLock"/> aufgerufen werden.</summary>
    private void WendeChunkAnUnterLock(int position)
    {
        var chunk = _aufzeichnung.Chunks[position];
        OutputChunk?.Invoke(this, new TerminalOutputChunkEventArgs(chunk.Data));
        _abgespielteChunks.Add(chunk.Data);
        foreach (var evt in _parser.Parse(chunk.Data))
            Buffer.Apply(evt);
        Volatile.Write(ref _aktuellerChunkIndex, _abgespielteChunks.Count);
    }

    /// <summary>Baut <see cref="Buffer"/> und den Parser-Zustand deterministisch aus dem Präfix
    /// der abgespielten Chunks neu auf (Buffer-Reset + Parser-Reset + Re-Parse über
    /// <see cref="_parser"/>). Muss unter <see cref="_renderLock"/> aufgerufen werden.</summary>
    private void BaueBufferUndParserAusPraefixNeuAuf()
    {
        Buffer.Reset();
        _parser.Reset();
        foreach (var chunk in _abgespielteChunks)
            foreach (var evt in _parser.Parse(chunk))
                Buffer.Apply(evt);
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

    /// <param name="nurAmEnde"><c>true</c>: Das Signal wird nur gesetzt, wenn die Position
    /// aktuell am Ende der Aufzeichnung steht — Positions-Prüfung und Signal-Flag werden dazu
    /// atomar unter <see cref="_renderLock"/> geprüft/gesetzt (Serialisierung gegen
    /// <see cref="SchrittZurueck"/> und den Flag-Reset in <see cref="WiedergabeStarten"/>).
    /// Das <see cref="Exited"/>-Event selbst feuert bewusst erst nach Lock-Freigabe: Es ist
    /// ein Flanken-Ereignis („Ende wurde erreicht"), kein Positions-Snapshot — ein in die
    /// Lücke fallender <see cref="SchrittZurueck"/> zieht das bereits gesetzte Signal nicht
    /// zurück. Ein Invoke unter dem Lock hätte Deadlock-Risiko gegenüber Handlern, die
    /// synchron auf einen anderen Thread marshaln (z. B. <c>Dispatcher.Invoke</c>, während
    /// der UI-Thread selbst in <see cref="SchrittZurueck"/> auf <see cref="_renderLock"/>
    /// wartet).</param>
    private void RaiseExited(bool nurAmEnde = false)
    {
        if (Volatile.Read(ref _disposed) != 0)
            return;
        if (nurAmEnde)
        {
            lock (_renderLock)
            {
                if (_abgespielteChunks.Count < _aufzeichnung.Chunks.Count)
                    return;
                if (Interlocked.CompareExchange(ref _exitedSignaled, 1, 0) != 0)
                    return;
            }
        }
        else if (Interlocked.CompareExchange(ref _exitedSignaled, 1, 0) != 0)
        {
            return;
        }

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
