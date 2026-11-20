using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using Softwareschmiede.App.Services;
using Softwareschmiede.Infrastructure.Terminal;

namespace Softwareschmiede.App.ViewModels;

/// <summary>ViewModel des Konsolentestfensters: lädt eine <c>.clireplay</c>-Aufzeichnung, spielt sie über
/// eine <see cref="TerminalReplaySession"/> durch den echten Renderpfad ab und zeigt parallel die
/// Quell-Repräsentation der Chunks synchron zur Wiedergabeposition an.</summary>
public sealed class ConsoleTestViewModel : ViewModelBase, IDisposable
{
    private const string TimelapseValidationError = "Zeitraffer-Schwelle muss eine Dezimalzahl ≥ 0 sein (Sekunden; 0 = maximale Geschwindigkeit).";

    private readonly IDialogService _dialogService;
    private readonly CliReplayRecordingStore _store;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ConsoleTestViewModel> _logger;
    private readonly Action<Action> _dispatcherInvoke;

    private TerminalReplaySession? _replaySession;
    private CliOutputRecording? _recording;
    private ITerminalSession? _session;
    private string? _filePath;
    private string _statusText = "Keine Aufzeichnung geladen.";
    private string _positionText = "Chunk 0/0";
    private string _geometrieText = "";
    private bool _isReplayActive;
    private bool _isPaused;
    private bool _replayExited;
    private string _timelapseThresholdText = "1";
    // Zuletzt gültige Zeitraffer-Schwelle — muss zum Initialwert von _zeitrafferSchwelleText
    // passen. Bleibt bei ungültiger Eingabe unverändert, damit eine neu erzeugte Replay-Session
    // (Neustart/neues Laden) dieselbe Schwelle übernimmt statt still auf ihren Default
    // (Echtzeit) zurückzufallen.
    private TimeSpan _timelapseThreshold = TimeSpan.FromSeconds(1);
    private string? _errorMessage;
    private string? _incompleteNotice;
    private CliChunkDisplayEntry? _currentSourceEntry;

    /// <inheritdoc cref="ConsoleTestViewModel"/>
    public ConsoleTestViewModel(
        IDialogService dialogService,
        CliReplayRecordingStore store,
        TimeProvider timeProvider,
        ILogger<ConsoleTestViewModel> logger,
        Action<Action>? dispatcherInvoke = null)
    {
        _dialogService = dialogService;
        _store = store;
        _timeProvider = timeProvider;
        _logger = logger;
        _dispatcherInvoke = DispatcherInvokeFactory.Create(dispatcherInvoke);

        OpenRecordingCommand = new AsyncRelayCommand(OpenRecordingAsync);
        StartReplayCommand = new RelayCommand(StartPlayback, () => _replaySession is not null && !IsReplayActive);
        // Aktiv während laufender Wiedergabe — und im reinen Schrittmodus (Position > 0
        // ohne gestartete Wiedergabe) als direkter Rückweg zu Position 0.
        RestartPlaybackCommand = new RelayCommand(RestartPlayback,
            () => _replaySession is not null && (IsReplayActive || _replaySession.CurrentChunkIndex > 0));
        PauseReplayCommand = new RelayCommand(ReplayPauseToggle, () => IsReplayActive);
        StepBeforeCommand = new RelayCommand(StepForward,
            () => _replaySession is not null && (!IsReplayActive || IsPaused)
                && _replaySession.CurrentChunkIndex < SourceEntries.Count);
        StepBackCommand = new RelayCommand(StepBackward,
            () => _replaySession is not null && (!IsReplayActive || IsPaused)
                && _replaySession.CurrentChunkIndex > 0);
        CloseCommand = new RelayCommand(Close);
    }

    /// <summary>Wird ausgelöst, wenn der Dialog geschlossen werden soll.</summary>
    public event EventHandler<bool>? CloseRequested;

    /// <summary>Öffnet eine .clireplay-Datei über den Datei-Dialog.</summary>
    public ICommand OpenRecordingCommand { get; }

    /// <summary>Startet die Wiedergabe der geladenen Aufzeichnung.</summary>
    public ICommand StartReplayCommand { get; }

    /// <summary>Bricht eine laufende/pausierte Wiedergabe ab und spielt die geladene
    /// Aufzeichnung sofort wieder ab Position 0 ab — aus dem reinen Schrittmodus
    /// (Position &gt; 0 ohne laufende Wiedergabe) der direkte Rückweg zum Anfang.</summary>
    public ICommand RestartPlaybackCommand { get; }

    /// <summary>Hält die Wiedergabe an bzw. setzt sie fort (Toggle).</summary>
    public ICommand PauseReplayCommand { get; }

    /// <summary>Wendet den nächsten aufgezeichneten Chunk als Einzelschritt an (zeitstempel-unabhängig).</summary>
    public ICommand StepBeforeCommand { get; }

    /// <summary>Stellt den Zustand vor dem zuletzt angewendeten Chunk wieder her.</summary>
    public ICommand StepBackCommand { get; }

    /// <summary>Schließt den Dialog.</summary>
    public ICommand CloseCommand { get; }

    /// <summary>Die an das Terminal-Control gebundene Session (Replay-Session oder null).</summary>
    public ITerminalSession? Session
    {
        get => _session;
        private set => SetProperty(ref _session, value);
    }

    /// <summary>Zeilen der Quell-Ansicht (ein Eintrag pro aufgezeichnetem Chunk).</summary>
    public ObservableCollection<CliChunkDisplayEntry> SourceEntries { get; } = [];

    /// <summary>Der der Wiedergabeposition entsprechende Eintrag in der Quell-Ansicht.</summary>
    public CliChunkDisplayEntry? CurrentSourceEntry
    {
        get => _currentSourceEntry;
        set => SetProperty(ref _currentSourceEntry, value);
    }

    /// <summary>Pfad der geladenen Aufzeichnungsdatei, oder null.</summary>
    public string? FilePath
    {
        get => _filePath;
        private set => SetProperty(ref _filePath, value);
    }

    /// <summary>Statusanzeige des Fensters.</summary>
    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    /// <summary>Positionsanzeige der Wiedergabe („Chunk x/y").</summary>
    public string PositionText
    {
        get => _positionText;
        private set => SetProperty(ref _positionText, value);
    }

    /// <summary>Anzeigetext der aufgezeichneten Terminal-Geometrie („Aufzeichnung: {Cols}×{Rows}");
    /// leer, solange keine Aufzeichnung geladen ist.</summary>
    public string GeometrieText
    {
        get => _geometrieText;
        private set => SetProperty(ref _geometrieText, value);
    }

    /// <summary>true, solange eine Wiedergabe läuft (inkl. Pausiert-Zustand).</summary>
    public bool IsReplayActive
    {
        get => _isReplayActive;
        // CanExecute der Wiedergabe-Buttons sofort neu auswerten — der Wechsel kommt vom
        // Dispatcher der Replay-Session, nicht aus Nutzereingaben, sodass RequerySuggested
        // ohne diesen Impuls erst bei der nächsten Mausbewegung feuern würde.
        private set => SetProperty(ref _isReplayActive, value, RelayCommand.Refresh);
    }

    /// <summary>true, solange die Wiedergabe pausiert ist.</summary>
    public bool IsPaused
    {
        get => _isPaused;
        // Der Pausiert-Wechsel steuert die CanExecute der Schritt-Buttons (Schritte sind nur
        // bei nicht unpausiert laufender Wiedergabe erlaubt) — daher wie IstWiedergabeAktiv
        // einen Refresh-Impuls auslösen.
        private set => SetProperty(ref _isPaused, value, RelayCommand.Refresh);
    }

    /// <summary>Eingabe der Zeitraffer-Schwelle in Sekunden (Dezimalzahl ≥ 0; 0 = maximale Geschwindigkeit).</summary>
    public string TimelapseThresholdText
    {
        get => _timelapseThresholdText;
        set
        {
            if (!SetProperty(ref _timelapseThresholdText, value))
                return;

            if (TryParseTimelapseThreshold(value, out var threshold))
            {
                _timelapseThreshold = threshold;
                if (_replaySession is not null)
                    _replaySession.TimeLapseThreshold = threshold;
                if (ErrorMessage == TimelapseValidationError)
                    ErrorMessage = null;
            }
            else
            {
                // Ungültige Eingabe: die zuletzt gültige Schwelle der Session bleibt wirksam.
                ErrorMessage = TimelapseValidationError;
            }
        }
    }

    /// <summary>Fehlermeldung des Dialogs (Laden/Validierung), oder null.</summary>
    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    /// <summary>Hinweistext, wenn die geladene Aufzeichnung unvollständig ist, sonst null.</summary>
    public string? IncompleteNotice
    {
        get => _incompleteNotice;
        private set => SetProperty(ref _incompleteNotice, value);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        EntsorgeReplaySession();
    }

    private async Task OpenRecordingAsync(CancellationToken ct)
    {
        var path = await _dialogService.ShowOpenFileDialogAsync(
            "CLI-Aufzeichnung öffnen",
            "CLI-Replay-Dateien (*.clireplay)|*.clireplay",
            null,
            ct);

        if (string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            var recording = await _store.LoadAsync(path, ct);
            // Die Quelltext-Formatierung großer Aufzeichnungen läuft abseits des UI-Threads;
            // die ObservableCollection wird erst danach auf dem UI-Thread befüllt.
            var entries = await Task.Run(() => CreateSourceEntries(recording), ct);
            LoadRecording(path, recording, entries);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Die CLI-Aufzeichnung {Pfad} konnte nicht geladen werden.", path);
            ErrorMessage = $"Die Aufzeichnung konnte nicht geladen werden: {ex.Message}";
        }
    }

    private void LoadRecording(string path, CliOutputRecording recording, IReadOnlyList<CliChunkDisplayEntry> entries)
    {
        EntsorgeReplaySession();

        // Cols/Rows > 0 ist durch die Store-Validierung beim Laden bereits garantiert —
        // ein weiterer Default-Fallback an dieser Stelle wäre unerreichbarer Code.
        _recording = recording;
        _replaySession = CreateReplaySession(recording);

        FilePath = path;
        SourceEntries.Clear();
        foreach (var entry in entries)
            SourceEntries.Add(entry);

        CurrentSourceEntry = null;
        IsReplayActive = false;
        IsPaused = false;
        _replayExited = false;
        ErrorMessage = null;
        IncompleteNotice = recording.IsComplete
            ? null
            : "Aufzeichnung unvollständig — das Speicher-Limit wurde erreicht; die Wiedergabe endet vor dem tatsächlichen Ende der Session.";
        PositionText = $"Chunk 0/{recording.Chunks.Count}";
        SetGeometrieText(recording);
        StatusText = $"Aufzeichnung geladen ({recording.PluginName}, {recording.Chunks.Count} Chunks) — bereit.";
        Session = _replaySession;
    }

    private static List<CliChunkDisplayEntry> CreateSourceEntries(CliOutputRecording recording)
    {
        var entries = new List<CliChunkDisplayEntry>(recording.Chunks.Count);
        for (var i = 0; i < recording.Chunks.Count; i++)
        {
            var chunk = recording.Chunks[i];
            entries.Add(new CliChunkDisplayEntry
            {
                // 1-basiert: die #-Spalte zählt wie der PositionsText die angewendeten
                // Chunks — „Chunk n/y" selektiert die Zeile „#n".
                Index = i + 1,
                Offset = chunk.Offset,
                Length = chunk.Data.Length,
                Sourcetext = CliChunkSourceTextFormatter.Format(chunk.Data),
            });
        }
        return entries;
    }

    private TerminalReplaySession CreateReplaySession(CliOutputRecording recording)
    {
        var session = new TerminalReplaySession(recording, _timeProvider);
        // Immer die zuletzt gültige Schwelle übernehmen — steht im Textfeld gerade ein
        // ungültiger Wert (Fehlerbanner), darf die frische Session nicht still auf ihren
        // Default (Echtzeit-Wiedergabe) zurückfallen.
        session.TimeLapseThreshold = _timelapseThreshold;
        session.BufferChanged += OnReplayBufferChanged;
        session.Exited += OnReplayExited;
        return session;
    }

    private void EntsorgeReplaySession()
    {
        if (_replaySession is null)
            return;

        // Events der alten Session abmelden, bevor sie disposed wird — ein noch in-flight
        // feuernder BufferChanged der abgebrochenen Session darf den Zustand der neuen
        // Aufzeichnung nicht überschreiben.
        _replaySession.BufferChanged -= OnReplayBufferChanged;
        _replaySession.Exited -= OnReplayExited;
        _replaySession.Dispose();
        // Referenzen auf die disposed Session auflösen, damit weder CanExecute noch das
        // gebundene TerminalControl einen Dead-State abbilden.
        _replaySession = null;
        Session = null;
        GeometrieText = "";
    }

    private void SetGeometrieText(CliOutputRecording recording)
        => GeometrieText = $"Aufzeichnung: {recording.Cols}×{recording.Rows}";

    private void StartPlayback()
    {
        if (_replaySession is null || _recording is null || IsReplayActive)
            return;

        // StartPlayback der Session ist bewusst idempotent — für ein echtes erneutes
        // Abspielen derselben geladenen Aufzeichnung wird eine frische Session ab Position 0
        // erzeugt (inkl. Buffer-Reset über die Rebind des TerminalControl).
        if (_replayExited)
            ErsetzeReplaySessionThroughFresh();

        _replaySession.StartPlayback();
        IsReplayActive = true;
        IsPaused = false;
        StatusText = "Wiedergabe läuft.";
    }

    private void RestartPlayback()
    {
        if (_replaySession is null || _recording is null
            || (!IsReplayActive && _replaySession.CurrentChunkIndex == 0))
            return;

        // Neustart mitten im Lauf (auch aus dem Pausiert-Zustand) oder aus dem
        // Schrittmodus: die Session verwerfen und dieselbe geladene Aufzeichnung
        // sofort wieder ab Position 0 abspielen.
        ErsetzeReplaySessionThroughFresh();
        _replaySession.StartPlayback();
        IsReplayActive = true;
        IsPaused = false;
        StatusText = "Wiedergabe läuft.";
    }

    private void ErsetzeReplaySessionThroughFresh()
    {
        EntsorgeReplaySession();
        _replaySession = CreateReplaySession(_recording!);
        CurrentSourceEntry = null;
        PositionText = $"Chunk 0/{_recording!.Chunks.Count}";
        SetGeometrieText(_recording);
        Session = _replaySession;
        _replayExited = false;
    }

    private void ReplayPauseToggle()
    {
        if (_replaySession is null || !IsReplayActive)
            return;

        if (_replaySession.IsPaused)
        {
            _replaySession.Resume();
            IsPaused = false;
            StatusText = "Wiedergabe läuft.";
        }
        else
        {
            _replaySession.Pause();
            IsPaused = true;
            StatusText = "Pausiert.";
        }
    }

    private void StepForward()
    {
        // RelayCommand.Execute wertet CanExecute nicht aus — der Wiedergabe-Zustandsteil
        // der Sperre muss hier intern nachgezogen werden, damit ein programmatischer
        // Aufruf die Position nicht mitten im unpausierten Lauf mutiert (die
        // Positionsgrenzen meldet die Session über ihren Rückgabewert).
        if (_replaySession is null || (IsReplayActive && !IsPaused)
            || !_replaySession.StepForward())
            return;

        // Am Ende gilt der aus OnReplayExited gesetzte Text „Wiedergabe beendet." —
        // das Exited-Event läuft synchron über _dispatcherInvoke bereits durch.
        if (_replaySession.CurrentChunkIndex < SourceEntries.Count)
            StatusText = $"Einzelschritt — Chunk {_replaySession.CurrentChunkIndex}/{SourceEntries.Count} angewendet.";
    }

    private void StepBackward()
    {
        // Interner Guard wie in StepForward (CanExecute wird von RelayCommand.Execute
        // nicht ausgewertet).
        if (_replaySession is null || (IsReplayActive && !IsPaused))
            return;

        var previousPosition = _replaySession.CurrentChunkIndex;
        if (!_replaySession.StepBackward())
            return;

        // Die Position hat das Ende verlassen — „Abspielen" darf die Session nicht mehr
        // durch eine frische ersetzen, sondern setzt an der Schrittposition fort.
        _replayExited = false;
        // Der zurückgenommene Chunk hat die Nummer der alten Position — identisch zur
        // 1-basierten #-Spalte der Quell-Liste.
        StatusText = $"Schritt zurück — Chunk {previousPosition}/{SourceEntries.Count} zurückgenommen.";
    }

    private void Close()
    {
        EntsorgeReplaySession();
        CloseRequested?.Invoke(this, false);
    }

    private void OnReplayBufferChanged(object? sender, EventArgs e)
    {
        // Sender-Identität prüfen: ein noch in-flight feuerndes Event der bereits abgemeldeten
        // Vorgänger-Session darf den Zustand der neuen Aufzeichnung nicht überschreiben.
        if (sender is not TerminalReplaySession session || !ReferenceEquals(session, _replaySession))
            return;

        _dispatcherInvoke(() =>
        {
            if (!ReferenceEquals(session, _replaySession))
                return;

            var index = session.CurrentChunkIndex;
            PositionText = $"Chunk {index}/{SourceEntries.Count}";
            CurrentSourceEntry = index > 0 && index <= SourceEntries.Count
                ? SourceEntries[index - 1]
                : null;
            // Positionsgrenzen der Schritt-Commands (0/Ende) ändern sich auch ohne
            // Zustandswechsel — CanExecute neu auswerten lassen.
            RelayCommand.Refresh();
        });
    }

    private void OnReplayExited(object? sender, TerminalSessionExitedEventArgs e)
    {
        if (sender is not TerminalReplaySession session || !ReferenceEquals(session, _replaySession))
            return;

        _dispatcherInvoke(() =>
        {
            if (!ReferenceEquals(session, _replaySession))
                return;

            IsReplayActive = false;
            IsPaused = false;
            _replayExited = true;
            StatusText = "Wiedergabe beendet.";
        });
    }

    private static bool TryParseTimelapseThreshold(string? text, out TimeSpan threshold)
    {
        threshold = TimeSpan.Zero;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        // Kulturunabhängig: beide Dezimaltrenner akzeptieren — unter en-US scheitert
        // "0,25" (Komma ist dort ein Gruppentrenner, bei Float ohne AllowThousands unzulässig).
        if (!double.TryParse(text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
            return false;

        // Obergrenze: TimeSpan.FromSeconds wirft für Sekunden > ~9,2·10^11 eine
        // OverflowException — der Try-Vertrag verlangt stattdessen false.
        if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0
            || seconds > TimeSpan.MaxValue.TotalSeconds)
            return false;

        threshold = TimeSpan.FromSeconds(seconds);
        return true;
    }
}
