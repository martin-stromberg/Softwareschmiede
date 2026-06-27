using System.Diagnostics;
using Softwareschmiede.Domain.Terminal;

namespace Softwareschmiede.Infrastructure.Terminal;

/// <summary>Koordiniert eine laufende Pseudo-Console-Sitzung bestehend aus <see cref="PseudoConsole"/>,
/// <see cref="Process"/>, Eingabe- und Ausgabe-Stream.</summary>
public sealed class PseudoConsoleSession : IDisposable
{
    private readonly PseudoConsole _pseudoConsole;
    private readonly Process _process;
    private readonly TimeProvider _timeProvider;
    private readonly Timer _runtimeStatusTimer;
    private readonly TimeSpan _waitingThreshold;
    private readonly DateTimeOffset _startedUtc;
    private readonly object _runtimeStatusLock = new();
    private bool _disposed;
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

    /// <summary>Der Terminal-Buffer dieser Sitzung. Wird von <c>TerminalControl</c> gesetzt und
    /// bei erneuter Anzeige wiederverwendet, damit der Bildschirminhalt erhalten bleibt.</summary>
    public TerminalBuffer? Buffer { get; set; }

    /// <summary>Erstellt eine neue <see cref="PseudoConsoleSession"/>.</summary>
    /// <param name="pseudoConsole">Die zugehörige Pseudo Console.</param>
    /// <param name="process">Der gestartete Prozess.</param>
    /// <param name="inputStream">Schreibbarer Stream für Eingaben an den Prozess.</param>
    /// <param name="outputStream">Lesbarer Stream für die Prozessausgabe.</param>
    internal PseudoConsoleSession(PseudoConsole pseudoConsole, Process process, Stream inputStream, Stream outputStream)
        : this(pseudoConsole, process, inputStream, outputStream, TimeProvider.System, TimeSpan.FromSeconds(4))
    {
    }

    internal PseudoConsoleSession(
        PseudoConsole pseudoConsole,
        Process process,
        Stream inputStream,
        Stream outputStream,
        TimeProvider timeProvider,
        TimeSpan waitingThreshold)
    {
        _pseudoConsole = pseudoConsole;
        _process = process;
        InputStream = inputStream;
        OutputStream = outputStream;
        _timeProvider = timeProvider;
        _waitingThreshold = waitingThreshold;
        _startedUtc = _timeProvider.GetUtcNow();
        _runtimeStatusTimer = new Timer(_ => RefreshRuntimeStatus(), null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
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
        if (_disposed)
            return;
        _disposed = true;

        try { InputStream.Dispose(); } catch { }
        try { OutputStream.Dispose(); } catch { }
        try { _runtimeStatusTimer.Dispose(); } catch { }
        try { _pseudoConsole.Dispose(); } catch { }
        try { _process.Dispose(); } catch { }
    }

    private void RefreshRuntimeStatus()
    {
        if (_disposed)
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
