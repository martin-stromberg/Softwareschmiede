using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Softwareschmiede.Infrastructure.Terminal;

/// <summary><see cref="ITerminalOutputSink"/>, die die Rohbytes einer Terminal-Session mit Zeitstempel
/// pro Chunk im Speicher aufzeichnet (Diagnose-Mitschnitt für das Konsolentestfenster). Budgetbegrenzt:
/// bei Überschreitung wird die Aufnahme gestoppt und das intakte Präfix als unvollständig markiert.</summary>
public sealed class CliOutputRecorder : ITerminalOutputSink
{
    private readonly object _lock = new();
    private readonly List<CliOutputChunkRecord> _chunks = [];
    private readonly Guid _aufgabeId;
    private readonly string _pluginName;
    private readonly int _cols;
    private readonly int _rows;
    private readonly int _byteBudget;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _logger;
    private readonly DateTimeOffset _startUtc;
    private int _bufferedBytes;
    private bool _istVollstaendig = true;
    private DateTimeOffset? _endeUtc;

    /// <summary>Erstellt einen neuen Recorder.</summary>
    /// <param name="aufgabeId">ID der aufgezeichneten Aufgabe.</param>
    /// <param name="pluginName">Anzeigename des aufgezeichneten KI-Plugins.</param>
    /// <param name="cols">Initiale Spaltenanzahl der Session.</param>
    /// <param name="rows">Initiale Zeilenanzahl der Session.</param>
    /// <param name="byteBudget">Byte-Budget der Aufzeichnung; bei Überschreitung stoppt die Aufnahme.</param>
    /// <param name="timeProvider">Zeitquelle für die Chunk-Zeitstempel (Test-Hook).</param>
    /// <param name="logger">Logger für Diagnosemeldungen (optional).</param>
    public CliOutputRecorder(
        Guid aufgabeId,
        string pluginName,
        int cols,
        int rows,
        int byteBudget,
        TimeProvider timeProvider,
        ILogger? logger = null)
    {
        _aufgabeId = aufgabeId;
        _pluginName = pluginName;
        _cols = cols;
        _rows = rows;
        _byteBudget = byteBudget;
        _timeProvider = timeProvider;
        _logger = logger ?? NullLogger.Instance;
        _startUtc = timeProvider.GetUtcNow();
    }

    /// <inheritdoc/>
    public void OnOutputChunk(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
            return;

        var offset = _timeProvider.GetUtcNow() - _startUtc;
        lock (_lock)
        {
            // Bei Budget-Überschreitung wird das intakte Präfix behalten (ein Replay ab Position 0
            // braucht den Anfang für den Parser-Zustand) — statt älteste Chunks zu verwerfen.
            if (!_istVollstaendig || _bufferedBytes + bytes.Length > _byteBudget)
            {
                if (_istVollstaendig)
                {
                    _istVollstaendig = false;
                    _logger.LogInformation(
                        "CLI-Aufzeichnung für Aufgabe {AufgabeId} gestoppt: Byte-Budget {ByteBudget} überschritten (vollständiges Präfix von {BufferedBytes} Bytes bleibt erhalten).",
                        _aufgabeId, _byteBudget, _bufferedBytes);
                }
                return;
            }

            _chunks.Add(new CliOutputChunkRecord(offset, bytes.ToArray()));
            _bufferedBytes += bytes.Length;
        }
    }

    /// <inheritdoc/>
    public void Complete()
    {
        lock (_lock)
        {
            _endeUtc ??= _timeProvider.GetUtcNow();
        }
    }

    /// <inheritdoc/>
    public Task CompleteAsync(TimeSpan timeout, CancellationToken ct = default)
    {
        Complete();
        return Task.CompletedTask;
    }

    /// <summary>Liefert eine Momentaufnahme der Aufzeichnung (auch nach dem Session-Ende abrufbar).</summary>
    /// <returns>Die <see cref="CliOutputAufzeichnung"/> mit den bis dahin aufgezeichneten Chunks.</returns>
    public CliOutputAufzeichnung GetAufzeichnung()
    {
        lock (_lock)
        {
            return new CliOutputAufzeichnung
            {
                AufgabeId = _aufgabeId,
                PluginName = _pluginName,
                StartUtc = _startUtc,
                Cols = _cols,
                Rows = _rows,
                IstVollstaendig = _istVollstaendig,
                EndeUtc = _endeUtc,
                Chunks = _chunks.ToArray(),
            };
        }
    }
}
