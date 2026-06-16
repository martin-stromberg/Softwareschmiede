using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using Softwareschmiede.App.Services;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;

namespace Softwareschmiede.App.ViewModels;

/// <summary>
/// ViewModel für die Aufgabendetailansicht.
/// Verwaltet Status, Protokoll, CLI-Prozessstart und Fenstereinbettung.
/// </summary>
public sealed class TaskDetailViewModel : ViewModelBase, IDisposable
{
    private readonly AufgabeService _aufgabeService;
    private readonly ProtokollService _protokollService;
    private readonly KiAusfuehrungsService _kiService;
    private readonly EntwicklungsprozessService _entwicklungsprozessService;
    private readonly PluginSelectionService _pluginSelectionService;
    private readonly IDialogService _dialogService;
    private readonly ILogger<TaskDetailViewModel> _logger;
    private readonly Action<Action> _dispatcherInvoke;

    private Guid _aufgabeId;
    private Aufgabe? _aufgabe;
    private bool _isLoading;
    private string? _fehlerMeldung;
    private bool _isCliRunning;
    private string? _selectedKiPluginPrefix;
    private string? _optionalCliParameters;
    private IntPtr _embeddedWindowHandle = IntPtr.Zero;
    private CancellationTokenSource? _ladenCts;
    private bool _isInfoViewVisible;
    private string? _editTitel;
    private string? _editAnforderungsBeschreibung;
    private bool _disposed;

    /// <summary>Wird aufgerufen, wenn der Nutzer zur vorherigen Ansicht zurückkehren möchte.</summary>
    public Action? ZurueckAction { get; set; }

    /// <summary>Wird nach dem Löschen einer Aufgabe aufgerufen, damit die übergeordnete Ansicht die Liste aktualisiert.</summary>
    public Func<Task>? AufgabeListeAktualisierenCallback { get; set; }

    /// <summary>Die ID der angezeigten Aufgabe.</summary>
    public Guid AufgabeId
    {
        get => _aufgabeId;
        set
        {
            if (SetProperty(ref _aufgabeId, value))
            {
                _ladenCts?.Cancel();
                _ladenCts?.Dispose();
                _ladenCts = new CancellationTokenSource();
                _ = LadenAsync(_ladenCts.Token);
            }
        }
    }

    /// <summary>Die geladene Aufgabe.</summary>
    public Aufgabe? Aufgabe
    {
        get => _aufgabe;
        private set
        {
            SetProperty(ref _aufgabe, value);
            OnPropertyChanged(nameof(AufgabeTitel));
            OnPropertyChanged(nameof(AufgabeStatus));
            OnPropertyChanged(nameof(KannCliStarten));
            OnPropertyChanged(nameof(KannCliStoppen));
            OnPropertyChanged(nameof(ShowEditPanel));
            OnPropertyChanged(nameof(ShowCliPanel));
            OnPropertyChanged(nameof(ShowDiffPanel));
            OnPropertyChanged(nameof(KannSpeichern));
            OnPropertyChanged(nameof(KannLoeschen));
        }
    }

    /// <summary>Titel der Aufgabe.</summary>
    public string AufgabeTitel => _aufgabe?.Titel ?? "(wird geladen…)";

    /// <summary>Status der Aufgabe.</summary>
    public AufgabeStatus AufgabeStatus => _aufgabe?.Status ?? Domain.Enums.AufgabeStatus.Neu;

    /// <summary>Gibt an, ob Daten geladen werden.</summary>
    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    /// <summary>Fehlermeldung bei Fehlern.</summary>
    public string? FehlerMeldung
    {
        get => _fehlerMeldung;
        private set => SetProperty(ref _fehlerMeldung, value);
    }

    /// <summary>Gibt an, ob ein CLI-Prozess läuft.</summary>
    public bool IsCliRunning
    {
        get => _isCliRunning;
        private set
        {
            SetProperty(ref _isCliRunning, value);
            OnPropertyChanged(nameof(KannCliStarten));
            OnPropertyChanged(nameof(KannCliStoppen));
            OnPropertyChanged(nameof(KannSpeichern));
            OnPropertyChanged(nameof(KannLoeschen));
        }
    }

    /// <summary>Gibt an, ob ein CLI-Prozess gestartet werden kann.</summary>
    public bool KannCliStarten => !_isCliRunning
        && _aufgabe?.Status is Domain.Enums.AufgabeStatus.Gestartet or Domain.Enums.AufgabeStatus.Wartend
        && !string.IsNullOrEmpty(_selectedKiPluginPrefix);

    /// <summary>Gibt an, ob der laufende CLI-Prozess gestoppt werden kann.</summary>
    public bool KannCliStoppen => _isCliRunning;

    /// <summary>Gewähltes KI-Plugin (Prefix).</summary>
    public string? SelectedKiPluginPrefix
    {
        get => _selectedKiPluginPrefix;
        set
        {
            SetProperty(ref _selectedKiPluginPrefix, value);
            OnPropertyChanged(nameof(KannCliStarten));
        }
    }

    /// <summary>Optionale Parameter für den CLI-Start.</summary>
    public string? OptionalCliParameters
    {
        get => _optionalCliParameters;
        set => SetProperty(ref _optionalCliParameters, value);
    }

    /// <summary>Handle des eingebetteten CLI-Fensters (für ProcessWindowHost).</summary>
    public IntPtr EmbeddedWindowHandle
    {
        get => _embeddedWindowHandle;
        set => SetProperty(ref _embeddedWindowHandle, value);
    }

    /// <summary>Protokolleinträge der Aufgabe.</summary>
    public ObservableCollection<Protokolleintrag> Protokolleintraege { get; } = new();

    /// <summary>Verfügbare KI-Plugin-Prefixe.</summary>
    public ObservableCollection<string> VerfuegbareKiPlugins { get; } = new();

    /// <summary>Steuert Sichtbarkeit zwischen Info-Panel und CLI-Fenster; Initial = false (CLI sichtbar).</summary>
    public bool IsInfoViewVisible
    {
        get => _isInfoViewVisible;
        set => SetProperty(ref _isInfoViewVisible, value);
    }

    /// <summary>Editable Kopie von Aufgabe.Titel für den Edit-Modus (Two-Way-Binding).</summary>
    public string? EditTitel
    {
        get => _editTitel;
        set
        {
            SetProperty(ref _editTitel, value);
            OnPropertyChanged(nameof(KannSpeichern));
        }
    }

    /// <summary>Editable Kopie von Aufgabe.AnforderungsBeschreibung für den Edit-Modus.</summary>
    public string? EditAnforderungsBeschreibung
    {
        get => _editAnforderungsBeschreibung;
        set => SetProperty(ref _editAnforderungsBeschreibung, value);
    }

    /// <summary>True wenn Status == Neu, sonst false.</summary>
    public bool ShowEditPanel => _aufgabe?.Status == Domain.Enums.AufgabeStatus.Neu;

    /// <summary>True wenn Status ∈ {Gestartet, InArbeit, Wartend}, sonst false.</summary>
    public bool ShowCliPanel => _aufgabe?.Status is Domain.Enums.AufgabeStatus.Gestartet
        or Domain.Enums.AufgabeStatus.InArbeit
        or Domain.Enums.AufgabeStatus.Wartend;

    /// <summary>True wenn Status == Beendet, sonst false.</summary>
    public bool ShowDiffPanel => _aufgabe?.Status == Domain.Enums.AufgabeStatus.Beendet;

    /// <summary>CanExecute für SpeichernCommand: Status ∈ {Neu, Gestartet} &amp;&amp; !IsCliRunning &amp;&amp; Titel.Length > 0.</summary>
    public bool KannSpeichern => _aufgabe?.Status is Domain.Enums.AufgabeStatus.Neu or Domain.Enums.AufgabeStatus.Gestartet
        && !_isCliRunning
        && !string.IsNullOrEmpty(_editTitel);

    /// <summary>CanExecute für LoeschenCommand: Status ∉ {Beendet, Archiviert} &amp;&amp; !IsCliRunning.</summary>
    public bool KannLoeschen => _aufgabe?.Status is not (Domain.Enums.AufgabeStatus.Beendet or Domain.Enums.AufgabeStatus.Archiviert)
        && _aufgabe != null
        && !_isCliRunning;

    /// <summary>Lädt die Aufgabe.</summary>
    public ICommand LadenCommand { get; }

    /// <summary>Startet den CLI-Prozess.</summary>
    public ICommand CliStartenCommand { get; }

    /// <summary>Stoppt den CLI-Prozess.</summary>
    public ICommand CliStoppenCommand { get; }

    /// <summary>Setzt den Status auf "Gestartet".</summary>
    public ICommand StatusGestartetSetzenCommand { get; }

    /// <summary>Schließt die Aufgabe ab (Status: Beendet).</summary>
    public ICommand AufgabeAbschliessenCommand { get; }

    /// <summary>Speichert Titel und AnforderungsBeschreibung der Aufgabe.</summary>
    public ICommand SpeichernCommand { get; }

    /// <summary>Löscht die Aufgabe nach Bestätigungsdialog.</summary>
    public ICommand LoeschenCommand { get; }

    /// <summary>Toggled IsInfoViewVisible zwischen Info-Panel und CLI-Fenster.</summary>
    public ICommand InfoCliToggleCommand { get; }

    /// <summary>Navigiert zurück zur vorherigen Ansicht.</summary>
    public ICommand ZurueckCommand { get; }

    /// <summary>Event: Ein CLI-Prozess wurde gestartet, Handle ist verfügbar.</summary>
    public event Action<System.Diagnostics.Process>? CliProzessGestartet;

    /// <inheritdoc cref="TaskDetailViewModel"/>
    public TaskDetailViewModel(
        AufgabeService aufgabeService,
        ProtokollService protokollService,
        KiAusfuehrungsService kiService,
        EntwicklungsprozessService entwicklungsprozessService,
        PluginSelectionService pluginSelectionService,
        IDialogService dialogService,
        ILogger<TaskDetailViewModel> logger,
        Action<Action>? dispatcherInvoke = null)
    {
        _aufgabeService = aufgabeService;
        _protokollService = protokollService;
        _kiService = kiService;
        _entwicklungsprozessService = entwicklungsprozessService;
        _pluginSelectionService = pluginSelectionService;
        _dialogService = dialogService;
        _logger = logger;
        // Dispatcher einmalig bei der Konstruktion erfassen, damit kein späterer Zugriff auf
        // Application.Current nötig ist (kann beim Shutdown null sein).
        if (dispatcherInvoke != null)
        {
            _dispatcherInvoke = dispatcherInvoke;
        }
        else
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            _dispatcherInvoke = dispatcher != null
                ? (Action<Action>)(action => dispatcher.Invoke(action))
                : (Action<Action>)(action => action());
        }

        _kiService.CliProcessStatusChanged += OnCliProcessStatusChanged;

        LadenCommand = new AsyncRelayCommand(ct => LadenAsync(ct));
        CliStartenCommand = new AsyncRelayCommand(CliStartenAsync, () => KannCliStarten);
        CliStoppenCommand = new AsyncRelayCommand(CliStoppenAsync, () => KannCliStoppen);
        StatusGestartetSetzenCommand = new AsyncRelayCommand(StatusGestartetSetzenAsync, () => AufgabeStatus == Domain.Enums.AufgabeStatus.Neu && !_isCliRunning);
        AufgabeAbschliessenCommand = new AsyncRelayCommand(AufgabeAbschliessenAsync, () => ShowCliPanel && !_isCliRunning);
        SpeichernCommand = new AsyncRelayCommand(SpeichernAsync, () => KannSpeichern);
        LoeschenCommand = new AsyncRelayCommand(LoeschenAsync, () => KannLoeschen);
        InfoCliToggleCommand = new RelayCommand(InfoCliToggle);
        ZurueckCommand = new RelayCommand(() => ZurueckAction?.Invoke());
    }

    private async Task LadenAsync(CancellationToken ct)
    {
        if (_aufgabeId == Guid.Empty)
            return;

        IsLoading = true;
        FehlerMeldung = null;

        try
        {
            Aufgabe = await _aufgabeService.GetDetailAsync(_aufgabeId, ct);
            IsCliRunning = _kiService.IsRunning(_aufgabeId);

            EditTitel = Aufgabe?.Titel;
            EditAnforderungsBeschreibung = Aufgabe?.AnforderungsBeschreibung;

            var protokolleintraege = await _protokollService.GetByAufgabeAsync(_aufgabeId, ct);
            Protokolleintraege.Clear();
            foreach (var eintrag in protokolleintraege)
                Protokolleintraege.Add(eintrag);

            await LadeVerfuegbarePluginsAsync(ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Laden der Aufgabe {AufgabeId}.", _aufgabeId);
            SetFehler(ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LadeVerfuegbarePluginsAsync(CancellationToken ct)
    {
        try
        {
            var pluginNames = await _pluginSelectionService.GetAvailableKiPluginPrefixesAsync(ct);
            VerfuegbareKiPlugins.Clear();
            foreach (var name in pluginNames)
                VerfuegbareKiPlugins.Add(name);

            if (VerfuegbareKiPlugins.Count > 0 && _selectedKiPluginPrefix is null)
                SelectedKiPluginPrefix = VerfuegbareKiPlugins[0];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "KI-Plugin-Liste konnte nicht geladen werden.");
        }
    }

    private async Task CliStartenAsync(CancellationToken ct)
    {
        if (_aufgabeId == Guid.Empty || string.IsNullOrEmpty(_selectedKiPluginPrefix))
            return;

        FehlerMeldung = null;

        try
        {
            var kiPlugin = await _pluginSelectionService.ResolveDevelopmentAutomationPluginAsync(_selectedKiPluginPrefix, ct);
            var lokalerKlonPfad = _aufgabe?.LokalerKlonPfad ?? string.Empty;

            var handle = await _kiService.StartCliAsync(
                _aufgabeId,
                kiPlugin,
                lokalerKlonPfad,
                _optionalCliParameters,
                ct);

            IsCliRunning = true;
            CliProzessGestartet?.Invoke(handle.Process);

            if (_aufgabe?.Status == Domain.Enums.AufgabeStatus.Gestartet)
            {
                await _aufgabeService.SetStatusAsync(_aufgabeId, Domain.Enums.AufgabeStatus.InArbeit, ct);
                try
                {
                    Aufgabe = await _aufgabeService.GetDetailAsync(_aufgabeId, ct);
                }
                catch (Exception reloadEx)
                {
                    _logger.LogWarning(reloadEx, "Aufgabe {AufgabeId} konnte nach Status-Update nicht neu geladen werden.", _aufgabeId);
                }
            }

            _logger.LogInformation("CLI für Aufgabe {AufgabeId} gestartet.", _aufgabeId);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Starten des CLI für Aufgabe {AufgabeId}.", _aufgabeId);
            FehlerMeldung = $"CLI-Startfehler: {ex.Message}";
        }
    }

    private async Task CliStoppenAsync(CancellationToken ct)
    {
        if (_aufgabeId == Guid.Empty)
            return;

        FehlerMeldung = null;

        try
        {
            await _kiService.StopCliAsync(_aufgabeId, ct);
            IsCliRunning = false;
            EmbeddedWindowHandle = IntPtr.Zero;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Stoppen des CLI für Aufgabe {AufgabeId}.", _aufgabeId);
            FehlerMeldung = $"CLI-Stoppfehler: {ex.Message}";
        }
    }

    private async Task StatusGestartetSetzenAsync(CancellationToken ct)
    {
        if (_aufgabeId == Guid.Empty)
            return;

        try
        {
            await _aufgabeService.SetStatusAsync(_aufgabeId, Domain.Enums.AufgabeStatus.Gestartet, ct);
            await LadenAsync(ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Setzen des Status für Aufgabe {AufgabeId}.", _aufgabeId);
            SetFehler(ex);
        }
    }

    private async Task AufgabeAbschliessenAsync(CancellationToken ct)
    {
        if (_aufgabeId == Guid.Empty)
            return;

        try
        {
            await _entwicklungsprozessService.AbschliessenAsync(_aufgabeId, ct);
            await LadenAsync(ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Abschließen der Aufgabe {AufgabeId}.", _aufgabeId);
            SetFehler(ex);
        }
    }

    private async Task SpeichernAsync(CancellationToken ct)
    {
        if (_aufgabeId == Guid.Empty)
            return;

        IsLoading = true;
        FehlerMeldung = null;

        try
        {
            await _aufgabeService.UpdateAsync(_aufgabeId, _editTitel ?? string.Empty, _editAnforderungsBeschreibung, null, ct);
            await LadenAsync(ct);

            try
            {
                await (AufgabeListeAktualisierenCallback?.Invoke() ?? Task.CompletedTask);
            }
            catch (Exception callbackEx)
            {
                _logger.LogError(callbackEx, "Fehler im AufgabeListeAktualisierenCallback nach Aufgabenspeicherung.");
            }

            ZurueckAction?.Invoke();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Speichern der Aufgabe {AufgabeId}.", _aufgabeId);
            FehlerMeldung = $"Aufgabe konnte nicht gespeichert werden: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoeschenAsync(CancellationToken ct)
    {
        if (_aufgabeId == Guid.Empty)
            return;

        var dialogNachricht = $"Aufgabe '{AufgabeTitel}' wirklich löschen? Diese Aktion kann nicht rückgängig gemacht werden.";
        if (!_dialogService.BestaetigenDialog(dialogNachricht, "Löschen bestätigen"))
            return;

        IsLoading = true;
        FehlerMeldung = null;

        try
        {
            await _aufgabeService.DeleteAsync(_aufgabeId, ct);

            try
            {
                await (AufgabeListeAktualisierenCallback?.Invoke() ?? Task.CompletedTask);
            }
            catch (Exception callbackEx)
            {
                _logger.LogError(callbackEx, "Fehler im AufgabeListeAktualisierenCallback nach Aufgabenlöschung.");
            }

            ZurueckAction?.Invoke();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Löschen der Aufgabe {AufgabeId}.", _aufgabeId);
            FehlerMeldung = $"Aufgabe konnte nicht gelöscht werden: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void SetFehler(Exception ex) => SetFehler(ref _fehlerMeldung, nameof(FehlerMeldung), ex);

    private void InfoCliToggle()
    {
        IsInfoViewVisible = !IsInfoViewVisible;
    }

    private void OnCliProcessStatusChanged(Guid aufgabeId, CliProcessStatus status)
    {
        if (aufgabeId != _aufgabeId)
            return;

        _dispatcherInvoke(() =>
        {
            IsCliRunning = status == CliProcessStatus.Gestartet;
            if (status != CliProcessStatus.Gestartet)
                EmbeddedWindowHandle = IntPtr.Zero;
        });
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        _kiService.CliProcessStatusChanged -= OnCliProcessStatusChanged;
        _ladenCts?.Cancel();
        _ladenCts?.Dispose();
        _ladenCts = null;
    }
}
