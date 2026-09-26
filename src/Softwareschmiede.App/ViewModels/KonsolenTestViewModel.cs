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
public sealed class KonsolenTestViewModel : ViewModelBase, IDisposable
{
    private const string ZeitrafferValidierungsFehler = "Zeitraffer-Schwelle muss eine Dezimalzahl ≥ 0 sein (Sekunden; 0 = maximale Geschwindigkeit).";

    private readonly IDialogService _dialogService;
    private readonly CliReplayAufzeichnungStore _store;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<KonsolenTestViewModel> _logger;
    private readonly Action<Action> _dispatcherInvoke;

    private TerminalReplaySession? _replaySession;
    private CliOutputAufzeichnung? _aufzeichnung;
    private ITerminalSession? _session;
    private string? _dateiPfad;
    private string _statusText = "Keine Aufzeichnung geladen.";
    private string _positionsText = "Chunk 0/0";
    private bool _istWiedergabeAktiv;
    private bool _istPausiert;
    private bool _wiedergabeBeendet;
    private string _zeitrafferSchwelleText = "1";
    private string? _fehlerMeldung;
    private string? _unvollstaendigHinweis;
    private CliChunkAnzeigeEintrag? _aktuellerQuellEintrag;

    /// <inheritdoc cref="KonsolenTestViewModel"/>
    public KonsolenTestViewModel(
        IDialogService dialogService,
        CliReplayAufzeichnungStore store,
        TimeProvider timeProvider,
        ILogger<KonsolenTestViewModel> logger,
        Action<Action>? dispatcherInvoke = null)
    {
        _dialogService = dialogService;
        _store = store;
        _timeProvider = timeProvider;
        _logger = logger;
        _dispatcherInvoke = DispatcherInvokeFactory.Create(dispatcherInvoke);

        AufzeichnungOeffnenCommand = new AsyncRelayCommand(OeffneAufzeichnungAsync);
        WiedergabeStartenCommand = new RelayCommand(WiedergabeStarten, () => _replaySession is not null && !IstWiedergabeAktiv);
        WiedergabeNeustartenCommand = new RelayCommand(WiedergabeNeustarten, () => _replaySession is not null && IstWiedergabeAktiv);
        WiedergabePausierenCommand = new RelayCommand(WiedergabePausierenToggle, () => IstWiedergabeAktiv);
        SchliessenCommand = new RelayCommand(Schliessen);
    }

    /// <summary>Wird ausgelöst, wenn der Dialog geschlossen werden soll.</summary>
    public event EventHandler<bool>? CloseRequested;

    /// <summary>Öffnet eine .clireplay-Datei über den Datei-Dialog.</summary>
    public ICommand AufzeichnungOeffnenCommand { get; }

    /// <summary>Startet die Wiedergabe der geladenen Aufzeichnung.</summary>
    public ICommand WiedergabeStartenCommand { get; }

    /// <summary>Bricht eine laufende/pausierte Wiedergabe ab und spielt die geladene
    /// Aufzeichnung sofort wieder ab Position 0 ab.</summary>
    public ICommand WiedergabeNeustartenCommand { get; }

    /// <summary>Hält die Wiedergabe an bzw. setzt sie fort (Toggle).</summary>
    public ICommand WiedergabePausierenCommand { get; }

    /// <summary>Schließt den Dialog.</summary>
    public ICommand SchliessenCommand { get; }

    /// <summary>Die an das Terminal-Control gebundene Session (Replay-Session oder null).</summary>
    public ITerminalSession? Session
    {
        get => _session;
        private set => SetProperty(ref _session, value);
    }

    /// <summary>Zeilen der Quell-Ansicht (ein Eintrag pro aufgezeichnetem Chunk).</summary>
    public ObservableCollection<CliChunkAnzeigeEintrag> QuellEintraege { get; } = [];

    /// <summary>Der der Wiedergabeposition entsprechende Eintrag in der Quell-Ansicht.</summary>
    public CliChunkAnzeigeEintrag? AktuellerQuellEintrag
    {
        get => _aktuellerQuellEintrag;
        set => SetProperty(ref _aktuellerQuellEintrag, value);
    }

    /// <summary>Pfad der geladenen Aufzeichnungsdatei, oder null.</summary>
    public string? DateiPfad
    {
        get => _dateiPfad;
        private set => SetProperty(ref _dateiPfad, value);
    }

    /// <summary>Statusanzeige des Fensters.</summary>
    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    /// <summary>Positionsanzeige der Wiedergabe („Chunk x/y").</summary>
    public string PositionsText
    {
        get => _positionsText;
        private set => SetProperty(ref _positionsText, value);
    }

    /// <summary>true, solange eine Wiedergabe läuft (inkl. Pausiert-Zustand).</summary>
    public bool IstWiedergabeAktiv
    {
        get => _istWiedergabeAktiv;
        // CanExecute der Wiedergabe-Buttons sofort neu auswerten — der Wechsel kommt vom
        // Dispatcher der Replay-Session, nicht aus Nutzereingaben, sodass RequerySuggested
        // ohne diesen Impuls erst bei der nächsten Mausbewegung feuern würde.
        private set => SetProperty(ref _istWiedergabeAktiv, value, RelayCommand.Refresh);
    }

    /// <summary>true, solange die Wiedergabe pausiert ist.</summary>
    public bool IstPausiert
    {
        get => _istPausiert;
        private set => SetProperty(ref _istPausiert, value);
    }

    /// <summary>Eingabe der Zeitraffer-Schwelle in Sekunden (Dezimalzahl ≥ 0; 0 = maximale Geschwindigkeit).</summary>
    public string ZeitrafferSchwelleText
    {
        get => _zeitrafferSchwelleText;
        set
        {
            if (!SetProperty(ref _zeitrafferSchwelleText, value))
                return;

            if (TryParseZeitrafferSchwelle(value, out var schwelle))
            {
                if (_replaySession is not null)
                    _replaySession.ZeitrafferSchwelle = schwelle;
                if (FehlerMeldung == ZeitrafferValidierungsFehler)
                    FehlerMeldung = null;
            }
            else
            {
                // Ungültige Eingabe: die zuletzt gültige Schwelle der Session bleibt wirksam.
                FehlerMeldung = ZeitrafferValidierungsFehler;
            }
        }
    }

    /// <summary>Fehlermeldung des Dialogs (Laden/Validierung), oder null.</summary>
    public string? FehlerMeldung
    {
        get => _fehlerMeldung;
        private set => SetProperty(ref _fehlerMeldung, value);
    }

    /// <summary>Hinweistext, wenn die geladene Aufzeichnung unvollständig ist, sonst null.</summary>
    public string? UnvollstaendigHinweis
    {
        get => _unvollstaendigHinweis;
        private set => SetProperty(ref _unvollstaendigHinweis, value);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        EntsorgeReplaySession();
    }

    private async Task OeffneAufzeichnungAsync(CancellationToken ct)
    {
        var pfad = await _dialogService.ShowOpenFileDialogAsync(
            "CLI-Aufzeichnung öffnen",
            "CLI-Replay-Dateien (*.clireplay)|*.clireplay",
            null,
            ct);

        if (string.IsNullOrWhiteSpace(pfad))
            return;

        try
        {
            var aufzeichnung = await _store.LadeAsync(pfad, ct);
            // Die Quelltext-Formatierung großer Aufzeichnungen läuft abseits des UI-Threads;
            // die ObservableCollection wird erst danach auf dem UI-Thread befüllt.
            var eintraege = await Task.Run(() => ErzeugeQuellEintraege(aufzeichnung), ct);
            LadeAufzeichnung(pfad, aufzeichnung, eintraege);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Die CLI-Aufzeichnung {Pfad} konnte nicht geladen werden.", pfad);
            FehlerMeldung = $"Die Aufzeichnung konnte nicht geladen werden: {ex.Message}";
        }
    }

    private void LadeAufzeichnung(string pfad, CliOutputAufzeichnung aufzeichnung, IReadOnlyList<CliChunkAnzeigeEintrag> eintraege)
    {
        EntsorgeReplaySession();

        // Cols/Rows > 0 ist durch die Store-Validierung beim Laden bereits garantiert —
        // ein weiterer Default-Fallback an dieser Stelle wäre unerreichbarer Code.
        _aufzeichnung = aufzeichnung;
        _replaySession = ErzeugeReplaySession(aufzeichnung);

        DateiPfad = pfad;
        QuellEintraege.Clear();
        foreach (var eintrag in eintraege)
            QuellEintraege.Add(eintrag);

        AktuellerQuellEintrag = null;
        IstWiedergabeAktiv = false;
        IstPausiert = false;
        _wiedergabeBeendet = false;
        FehlerMeldung = null;
        UnvollstaendigHinweis = aufzeichnung.IstVollstaendig
            ? null
            : "Aufzeichnung unvollständig — das Speicher-Limit wurde erreicht; die Wiedergabe endet vor dem tatsächlichen Ende der Session.";
        PositionsText = $"Chunk 0/{aufzeichnung.Chunks.Count}";
        StatusText = $"Aufzeichnung geladen ({aufzeichnung.PluginName}, {aufzeichnung.Chunks.Count} Chunks) — bereit.";
        Session = _replaySession;
    }

    private static List<CliChunkAnzeigeEintrag> ErzeugeQuellEintraege(CliOutputAufzeichnung aufzeichnung)
    {
        var eintraege = new List<CliChunkAnzeigeEintrag>(aufzeichnung.Chunks.Count);
        for (var i = 0; i < aufzeichnung.Chunks.Count; i++)
        {
            var chunk = aufzeichnung.Chunks[i];
            eintraege.Add(new CliChunkAnzeigeEintrag
            {
                Index = i,
                Offset = chunk.Offset,
                Laenge = chunk.Data.Length,
                Quelltext = CliChunkQuelltextFormatter.Formatiere(chunk.Data),
            });
        }
        return eintraege;
    }

    private TerminalReplaySession ErzeugeReplaySession(CliOutputAufzeichnung aufzeichnung)
    {
        var session = new TerminalReplaySession(aufzeichnung, _timeProvider);
        if (TryParseZeitrafferSchwelle(_zeitrafferSchwelleText, out var schwelle))
            session.ZeitrafferSchwelle = schwelle;
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
    }

    private void WiedergabeStarten()
    {
        if (_replaySession is null || _aufzeichnung is null || IstWiedergabeAktiv)
            return;

        // WiedergabeStarten der Session ist bewusst idempotent — für ein echtes erneutes
        // Abspielen derselben geladenen Aufzeichnung wird eine frische Session ab Position 0
        // erzeugt (inkl. Buffer-Reset über die Rebind des TerminalControl).
        if (_wiedergabeBeendet)
            ErsetzeReplaySessionDurchFrische();

        _replaySession.WiedergabeStarten();
        IstWiedergabeAktiv = true;
        IstPausiert = false;
        StatusText = "Wiedergabe läuft.";
    }

    private void WiedergabeNeustarten()
    {
        if (_replaySession is null || _aufzeichnung is null || !IstWiedergabeAktiv)
            return;

        // Neustart mitten im Lauf (auch aus dem Pausiert-Zustand): die laufende Session
        // verwerfen und dieselbe geladene Aufzeichnung sofort wieder ab Position 0 abspielen.
        ErsetzeReplaySessionDurchFrische();
        _replaySession.WiedergabeStarten();
        IstPausiert = false;
        StatusText = "Wiedergabe läuft.";
    }

    private void ErsetzeReplaySessionDurchFrische()
    {
        EntsorgeReplaySession();
        _replaySession = ErzeugeReplaySession(_aufzeichnung!);
        AktuellerQuellEintrag = null;
        PositionsText = $"Chunk 0/{_aufzeichnung!.Chunks.Count}";
        Session = _replaySession;
        _wiedergabeBeendet = false;
    }

    private void WiedergabePausierenToggle()
    {
        if (_replaySession is null || !IstWiedergabeAktiv)
            return;

        if (_replaySession.IstPausiert)
        {
            _replaySession.Fortsetzen();
            IstPausiert = false;
            StatusText = "Wiedergabe läuft.";
        }
        else
        {
            _replaySession.Pausieren();
            IstPausiert = true;
            StatusText = "Pausiert.";
        }
    }

    private void Schliessen()
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

            var index = session.AktuellerChunkIndex;
            PositionsText = $"Chunk {index}/{QuellEintraege.Count}";
            if (index > 0 && index <= QuellEintraege.Count)
                AktuellerQuellEintrag = QuellEintraege[index - 1];
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

            IstWiedergabeAktiv = false;
            IstPausiert = false;
            _wiedergabeBeendet = true;
            StatusText = "Wiedergabe beendet.";
        });
    }

    private static bool TryParseZeitrafferSchwelle(string? text, out TimeSpan schwelle)
    {
        schwelle = TimeSpan.Zero;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out var sekunden)
            && !double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out sekunden))
            return false;

        // Obergrenze: TimeSpan.FromSeconds wirft für Sekunden > ~9,2·10^11 eine
        // OverflowException — der Try-Vertrag verlangt stattdessen false.
        if (double.IsNaN(sekunden) || double.IsInfinity(sekunden) || sekunden < 0
            || sekunden > TimeSpan.MaxValue.TotalSeconds)
            return false;

        schwelle = TimeSpan.FromSeconds(sekunden);
        return true;
    }
}
