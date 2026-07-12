using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Domain.Terminal;

namespace Softwareschmiede.Infrastructure.Terminal;

/// <summary>Koordiniert eine laufende Pseudo-Console-Sitzung bestehend aus <see cref="PseudoConsole"/>,
/// <see cref="Process"/>, Eingabe- und Ausgabe-Stream. Betreibt die Leseschleife (<see cref="ReadLoopAsync"/>)
/// ab Konstruktion bis <see cref="Dispose"/> unabhängig vom Lebenszyklus eines anzeigenden Controls, damit
/// mehrere CLI-Prozesse parallel weiterlaufen können, auch wenn ihre Aufgabenseite nicht angezeigt wird.</summary>
public sealed class PseudoConsoleSession : IDisposable
{
    private const int DefaultCols = 220;
    private const int DefaultRows = 50;

    private readonly IPseudoConsoleHandle _pseudoConsole;
    private readonly Process _process;
    private readonly TimeProvider _timeProvider;
    private readonly Timer _runtimeStatusTimer;
    private readonly TimeSpan _waitingThreshold;
    private readonly DateTimeOffset _startedUtc;
    private readonly object _runtimeStatusLock = new();
    private readonly ILogger _logger;
    private readonly AnsiSequenceParser _parser = new();
    private readonly CancellationTokenSource _readCts = new();
    private readonly Task _readLoopTask;
    private int _disposed;
    private CliRuntimeStatus _runtimeStatus = CliRuntimeStatus.Laeuft;
    private DateTimeOffset? _lastOutputUtc;
    private DateTimeOffset? _lastInputUtc;

    /// <summary>Schreibbarer Stream für Tastatureingaben an den Prozess.</summary>
    public Stream InputStream { get; }

    /// <summary>Lesbarer Stream für die Prozessausgabe.</summary>
    public Stream OutputStream { get; }

    /// <summary>Der verwaltete Prozess der Sitzung.</summary>
    public Process Process => _process;

    /// <summary>Laufzeitstatus der aktiven CLI innerhalb der Sitzung.</summary>
    public CliRuntimeStatus RuntimeStatus
    {
        get
        {
            lock (_runtimeStatusLock)
            {
                return _runtimeStatus;
            }
        }
    }

    /// <summary>Wird ausgelöst, wenn sich der Laufzeitstatus der CLI ändert.</summary>
    public event EventHandler<CliRuntimeStatusChangedEventArgs>? RuntimeStatusChanged;

    /// <summary>Der Terminal-Buffer dieser Sitzung. Wird bereits bei Konstruktion angelegt und von der
    /// Leseschleife dieser Sitzung befüllt, unabhängig davon, ob ein <c>TerminalControl</c> gebunden ist.</summary>
    public TerminalBuffer Buffer { get; } = new(DefaultCols, DefaultRows);

    /// <summary>Wird nach jeder erfolgreichen Verarbeitung eines Ausgabe-Chunks durch die Leseschleife ausgelöst,
    /// damit ein gebundenes <c>TerminalControl</c> seine Anzeige aktualisieren kann.</summary>
    public event EventHandler? BufferChanged;

    /// <summary>Erstellt eine neue <see cref="PseudoConsoleSession"/> und startet sofort die Leseschleife.</summary>
    /// <param name="pseudoConsole">Die zugehörige Pseudo Console.</param>
    /// <param name="process">Der gestartete Prozess.</param>
    /// <param name="inputStream">Schreibbarer Stream für Eingaben an den Prozess.</param>
    /// <param name="outputStream">Lesbarer Stream für die Prozessausgabe.</param>
    /// <param name="logger">Logger für Fehler- und Diagnosemeldungen der Leseschleife (optional).</param>
    internal PseudoConsoleSession(IPseudoConsoleHandle pseudoConsole, Process process, Stream inputStream, Stream outputStream, ILogger? logger = null)
        : this(pseudoConsole, process, inputStream, outputStream, TimeProvider.System, TimeSpan.FromSeconds(4), logger)
    {
    }

    internal PseudoConsoleSession(
        IPseudoConsoleHandle pseudoConsole,
        Process process,
        Stream inputStream,
        Stream outputStream,
        TimeProvider timeProvider,
        TimeSpan waitingThreshold,
        ILogger? logger = null)
    {
        _pseudoConsole = pseudoConsole;
        _process = process;
        InputStream = inputStream;
        OutputStream = outputStream;
        _timeProvider = timeProvider;
        _waitingThreshold = waitingThreshold;
        _startedUtc = _timeProvider.GetUtcNow();
        _logger = logger ?? NullLogger.Instance;
        _runtimeStatusTimer = new Timer(_ => RefreshRuntimeStatus(), null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        _readLoopTask = Task.Run(() => ReadLoopAsync(_readCts.Token));
    }

    /// <summary>Meldet gelesene Ausgabe der CLI an die Status-Erkennung.</summary>
    public void MarkOutputActivity()
    {
        lock (_runtimeStatusLock)
        {
            _lastOutputUtc = _timeProvider.GetUtcNow();
        }

        SetRuntimeStatus(CliRuntimeStatus.Laeuft);
    }

    /// <summary>Meldet eine Benutzereingabe an die Status-Erkennung.</summary>
    public void MarkInputActivity()
    {
        lock (_runtimeStatusLock)
        {
            _lastInputUtc = _timeProvider.GetUtcNow();
        }

        SetRuntimeStatus(CliRuntimeStatus.Laeuft);
    }

    /// <summary>Ändert die Größe der Pseudo Console.</summary>
    /// <param name="cols">Neue Spaltenanzahl.</param>
    /// <param name="rows">Neue Zeilenanzahl.</param>
    /// <returns><c>true</c>, wenn die Größenänderung erfolgreich war; andernfalls <c>false</c>.</returns>
    public bool Resize(int cols, int rows)
        => _pseudoConsole.Resize((short)cols, (short)rows);

    /// <inheritdoc/>
    public void Dispose()
    {
        // Atomarer Check-and-Set statt eines einfachen bool-Felds: Process.Exited-Handler (Hintergrundthread)
        // und ein expliziter Aufrufer (z. B. KiAusfuehrungsService.Dispose() beim App-Shutdown) können diese
        // Methode gleichzeitig aufrufen. Ohne Interlocked könnten beide Threads den Check bestehen, bevor einer
        // das Flag setzt, und dadurch die nativen ConPTY-/Pipe-Handles doppelt freigeben.
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
            return;

        try { _readCts.Cancel(); } catch { }
        // OutputStream vor dem Beenden schließen: Ein bereits blockierter, nicht kooperativ abbrechbarer
        // nativer Read (siehe ReadLoopAsync) wird dadurch sofort mit einem I/O-Fehler beendet, statt sich
        // ausschließlich auf _readCts.Cancel() zu verlassen, das einen laufenden Read nicht unterbricht.
        try { OutputStream.Dispose(); } catch { }
        // Bewusst kein synchrones _readLoopTask.Wait(...) hier: Dispose() wird u. a. aus dem Process.Exited-
        // Handler auf einem ThreadPool-Thread aufgerufen. Bei vielen gleichzeitigen Sitzungen (parallele
        // CLI-Ausführung) blockiert ein solches Wait() genau die ThreadPool-Threads, die auch zum Fortsetzen
        // der eigenen Leseschleife benötigt werden – das führt unter ThreadPool-Druck zu mehrsekündigen
        // Verzögerungen bis hin zu Timeouts (siehe Dispose_ReadLoopNeverCompletes_ReturnsPromptlyWithoutWaiting).
        // OutputStream ist bereits geschlossen und der Read entsprechend bereits am Beenden; die Schleife läuft
        // asynchron aus, ohne dass Dispose() darauf warten muss.
        // _readCts erst verwerfen, nachdem die Leseschleife tatsächlich beendet ist (nicht blockierend über eine
        // Continuation): Ein sofortiges Dispose des CTS, während die Schleife die Cancellation noch verarbeitet,
        // kann zu einer unerwarteten Exception im laufenden Read führen.
        _readLoopTask.ContinueWith(
            t =>
            {
                if (t.IsFaulted)
                    _logger.LogWarning(t.Exception, "Unerwarteter Fehler beim asynchronen Beenden der Leseschleife nach Dispose().");
                try { _readCts.Dispose(); } catch { }
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
        try { InputStream.Dispose(); } catch { }
        try { _runtimeStatusTimer.Dispose(); } catch { }
        try { _pseudoConsole.Dispose(); } catch { }
        try { _process.Dispose(); } catch { }
    }

    /// <summary>
    /// Kontinuierliche Leseschleife: liest Bytes aus <see cref="OutputStream"/>, parsed sie mit
    /// <see cref="AnsiSequenceParser"/>, wendet die daraus resultierenden Ereignisse auf <see cref="Buffer"/> an
    /// und löst danach <see cref="BufferChanged"/> aus. Läuft unabhängig davon, ob ein <c>TerminalControl</c>
    /// gebunden ist, bis <paramref name="ct"/> abgebrochen wird oder der Output-Stream endet.
    /// </summary>
    /// <param name="ct">Abbruch-Token, das bei <see cref="Dispose"/> ausgelöst wird.</param>
    private async Task ReadLoopAsync(CancellationToken ct)
    {
        var data = new byte[4096];
        try
        {
            while (!ct.IsCancellationRequested)
            {
                int bytesRead;
                try
                {
                    bytesRead = await OutputStream.ReadAsync(data, ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Fehler beim Lesen aus dem Terminal-Output-Stream der Sitzung.");
                    break;
                }

                if (bytesRead == 0)
                    break;

                MarkOutputActivity();

                var events = _parser.Parse(data.AsSpan(0, bytesRead));
                foreach (var evt in events)
                    Buffer.Apply(evt);

                BufferChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unerwarteter Fehler im Terminal-Lesevorgang der Sitzung.");
        }
    }

    private void RefreshRuntimeStatus()
    {
        if (Volatile.Read(ref _disposed) != 0)
            return;

        DateTimeOffset? lastOutputUtc;
        DateTimeOffset? lastInputUtc;
        DateTimeOffset startedUtc;
        lock (_runtimeStatusLock)
        {
            lastOutputUtc = _lastOutputUtc;
            lastInputUtc = _lastInputUtc;
            startedUtc = _startedUtc;
        }

        var isRunning = true;
        try { isRunning = !_process.HasExited; }
        catch { isRunning = false; }

        var nextStatus = CliRuntimeStatusEvaluator.Determine(
            isRunning,
            startedUtc,
            lastOutputUtc,
            lastInputUtc,
            _timeProvider.GetUtcNow(),
            _waitingThreshold);

        SetRuntimeStatus(nextStatus);
    }

    private void SetRuntimeStatus(CliRuntimeStatus status)
    {
        var changed = false;
        lock (_runtimeStatusLock)
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

    /// <summary>Schreibt einen Prompt inkl. abschließendem Absenden auf <see cref="InputStream"/>, flusht ihn und
    /// meldet die Eingabe an die Status-Erkennung. Zeilenumbrüche (im Prompttext sowie der abschließende Submit)
    /// werden bewusst einheitlich als bloßes <c>\r</c> (CR) übertragen statt als <see cref="Environment.NewLine"/>
    /// (<c>\r\n</c> unter Windows): Ein echter physischer Tastatur-Enter wird von <c>KeyToVt100Encoder.Encode</c>
    /// ebenfalls als alleinstehendes <c>0x0D</c> kodiert, und eingefügter Zwischenablage-Text wird von
    /// <c>KeyToVt100Encoder.EncodeClipboardText</c> aus demselben Grund auf <c>\r</c> normalisiert. Ein
    /// zusätzliches <c>\n</c> nach dem CR wird von der CLI nicht wie ein normaler Tastatur-Enter interpretiert
    /// und kann den Submit der Zeile verhindern.</summary>
    /// <param name="prompt">Der zu sendende Prompttext.</param>
    /// <param name="ct">Abbruch-Token.</param>
    public async Task WritePromptAsync(string prompt, CancellationToken ct)
    {
        var bytes = Encoding.UTF8.GetBytes(NormalizeToCarriageReturn(prompt).TrimEnd('\r') + "\r");
        await InputStream.WriteAsync(bytes, ct);
        await InputStream.FlushAsync(ct);
        MarkInputActivity();
    }

    /// <summary>Wandelt alle Zeilenenden (<c>\r\n</c> und alleinstehendes <c>\n</c>) in ein einzelnes <c>\r</c>
    /// um, wie es ein echter Enter-Tastendruck an die Pseudo Console sendet (siehe <c>KeyToVt100Encoder</c>).</summary>
    /// <param name="text">Der zu normalisierende Text.</param>
    /// <returns>Der Text mit ausschließlich <c>\r</c> als Zeilenende.</returns>
    public static string NormalizeToCarriageReturn(string text)
    {
        var normalized = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            switch (c)
            {
                case '\r':
                    normalized.Append('\r');
                    if (i + 1 < text.Length && text[i + 1] == '\n')
                        i++;
                    break;
                case '\n':
                    normalized.Append('\r');
                    break;
                default:
                    normalized.Append(c);
                    break;
            }
        }

        return normalized.ToString();
    }
}

/// <summary>Laufzeitstatus einer aktiven CLI-Sitzung.</summary>
public enum CliRuntimeStatus
{
    /// <summary>Kein laufender CLI-Prozess ist aktiv.</summary>
    Inaktiv,
    /// <summary>Die CLI laeuft und hat kuerzlich Ausgabe oder Eingabe verarbeitet.</summary>
    Laeuft,
    /// <summary>Die CLI laeuft, erzeugt aber seit laengerer Zeit keine Ausgabe und wartet vermutlich auf Benutzereingabe.</summary>
    WartetAufEingabe
}

/// <summary>Argumente fuer Aenderungen des CLI-Laufzeitstatus.</summary>
public sealed class CliRuntimeStatusChangedEventArgs : EventArgs
{
    /// <summary>Neuer Laufzeitstatus.</summary>
    public CliRuntimeStatus Status { get; }

    /// <summary>Erstellt neue Ereignisargumente.</summary>
    /// <param name="status">Neuer Laufzeitstatus.</param>
    public CliRuntimeStatusChangedEventArgs(CliRuntimeStatus status)
    {
        Status = status;
    }
}

/// <summary>Ermittelt den CLI-Laufzeitstatus aus Prozess- und I/O-Aktivitaet.</summary>
public static class CliRuntimeStatusEvaluator
{
    /// <summary>Bestimmt den Laufzeitstatus einer CLI-Sitzung.</summary>
    /// <param name="isRunning">Gibt an, ob der Prozess noch laeuft.</param>
    /// <param name="startedUtc">Startzeitpunkt der Sitzung.</param>
    /// <param name="lastOutputUtc">Zeitpunkt der letzten gelesenen Ausgabe.</param>
    /// <param name="lastInputUtc">Zeitpunkt der letzten gesendeten Eingabe.</param>
    /// <param name="nowUtc">Aktueller Zeitpunkt.</param>
    /// <param name="waitingThreshold">Dauer ohne I/O, ab der Warten angenommen wird.</param>
    /// <returns>Den abgeleiteten Laufzeitstatus.</returns>
    public static CliRuntimeStatus Determine(
        bool isRunning,
        DateTimeOffset startedUtc,
        DateTimeOffset? lastOutputUtc,
        DateTimeOffset? lastInputUtc,
        DateTimeOffset nowUtc,
        TimeSpan waitingThreshold)
    {
        if (!isRunning)
            return CliRuntimeStatus.Inaktiv;

        var lastActivityUtc = startedUtc;
        if (lastOutputUtc.HasValue && lastOutputUtc.Value > lastActivityUtc)
            lastActivityUtc = lastOutputUtc.Value;
        if (lastInputUtc.HasValue && lastInputUtc.Value > lastActivityUtc)
            lastActivityUtc = lastInputUtc.Value;

        return nowUtc - lastActivityUtc >= waitingThreshold
            ? CliRuntimeStatus.WartetAufEingabe
            : CliRuntimeStatus.Laeuft;
    }
}
