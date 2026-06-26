using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.ValueObjects;
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
    private readonly IPluginManager _pluginManager;
    private readonly IServiceProvider _serviceProvider;
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
            OnPropertyChanged(nameof(KannCliStoppen));
            OnPropertyChanged(nameof(ShowEditPanel));
            OnPropertyChanged(nameof(ShowCliPanel));
            OnPropertyChanged(nameof(ShowDiffPanel));
            OnPropertyChanged(nameof(KannSpeichern));
            OnPropertyChanged(nameof(KannLoeschen));
            OnPropertyChanged(nameof(CanAssignIssue));
            OnPropertyChanged(nameof(CurrentIssueReferenz));
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
            OnPropertyChanged(nameof(KannCliStoppen));
            OnPropertyChanged(nameof(KannSpeichern));
            OnPropertyChanged(nameof(KannLoeschen));
            OnPropertyChanged(nameof(CanAssignIssue));
        }
    }

    /// <summary>Gibt an, ob der laufende CLI-Prozess gestoppt werden kann.</summary>
    public bool KannCliStoppen => _isCliRunning;

    /// <summary>Gewähltes KI-Plugin (Prefix).</summary>
    public string? SelectedKiPluginPrefix
    {
        get => _selectedKiPluginPrefix;
        set => SetProperty(ref _selectedKiPluginPrefix, value);
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

    /// <summary>True wenn Status ∈ {Gestartet, Wartend}, sonst false.</summary>
    public bool ShowCliPanel => _aufgabe?.Status is Domain.Enums.AufgabeStatus.Gestartet
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

    /// <summary>true wenn Aufgabe vorhanden, SCM-Plugin Issues unterstützt und kein CLI läuft.</summary>
    public bool CanAssignIssue => _aufgabe != null
        && !_isCliRunning
        && _pluginManager.GetSourceCodeManagementPlugins().Any(p => p is IGitPlugin);

    /// <summary>Aktuelle Issue-Zuweisung der Aufgabe.</summary>
    public IssueReferenz? CurrentIssueReferenz => _aufgabe?.IssueReferenz;

    /// <summary>Lädt die Aufgabe.</summary>
    public ICommand LadenCommand { get; }

    /// <summary>Stoppt den CLI-Prozess.</summary>
    public ICommand CliStoppenCommand { get; }

    /// <summary>Startet die Aufgabe: kombiniertes Klonen, Plugin-Auflösung und CLI-Start.</summary>
    public ICommand StartenCommand { get; }

    /// <summary>Wechselt das KI-Plugin bei laufender CLI: Dialog, Stop, Restart.</summary>
    public ICommand PluginAendernCommand { get; }

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

    /// <summary>Öffnet den Issue-Auswahl-Dialog und weist das gewählte Issue der Aufgabe zu.</summary>
    public ICommand IssueZuweisenCommand { get; }

    /// <summary>Öffnet die Issue-URL im Standard-Browser.</summary>
    public ICommand IssueBrowserOeffnenCommand { get; }

    /// <summary>Event: Ein CLI-Prozess wurde gestartet, Handle ist verfügbar.</summary>
    public event Action<System.Diagnostics.Process>? CliProzessGestartet;

    /// <summary>
    /// Gibt den laufenden CLI-Prozess zurück, oder null wenn kein Prozess läuft.
    /// Wird vom View-Loaded-Handler verwendet, um das Fenster einzubetten wenn
    /// CliProzessGestartet bereits gefeuert hat bevor der Handler registriert wurde.
    /// </summary>
    public System.Diagnostics.Process? GetRunningProcess() => _kiService.GetRunningProcess(_aufgabeId);

    /// <summary>
    /// Speichert das bekannte HWND des CLI-Fensters. SetParent ändert das HWND nicht,
    /// daher kann dasselbe Handle nach Navigation-Zurück zum Wieder-Einbetten genutzt werden.
    /// </summary>
    public void SetCliWindowHandle(IntPtr handle) => _kiService.SetFensterHandle(_aufgabeId, handle);

    /// <summary>
    /// Gibt das gespeicherte HWND des CLI-Fensters zurück, oder <see cref="IntPtr.Zero"/>
    /// wenn noch kein Handle bekannt ist.
    /// </summary>
    public IntPtr GetCliWindowHandle() => _kiService.GetFensterHandle(_aufgabeId);

    /// <inheritdoc cref="TaskDetailViewModel"/>
    public TaskDetailViewModel(
        AufgabeService aufgabeService,
        ProtokollService protokollService,
        KiAusfuehrungsService kiService,
        EntwicklungsprozessService entwicklungsprozessService,
        PluginSelectionService pluginSelectionService,
        IDialogService dialogService,
        IPluginManager pluginManager,
        IServiceProvider serviceProvider,
        ILogger<TaskDetailViewModel> logger,
        Action<Action>? dispatcherInvoke = null)
    {
        _aufgabeService = aufgabeService;
        _protokollService = protokollService;
        _kiService = kiService;
        _entwicklungsprozessService = entwicklungsprozessService;
        _pluginSelectionService = pluginSelectionService;
        _dialogService = dialogService;
        _pluginManager = pluginManager;
        _serviceProvider = serviceProvider;
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
        CliStoppenCommand = new AsyncRelayCommand(CliStoppenAsync, () => KannCliStoppen);
        StartenCommand = new AsyncRelayCommand(StartenAsync, () => AufgabeStatus == Domain.Enums.AufgabeStatus.Neu && !_isCliRunning);
        PluginAendernCommand = new AsyncRelayCommand(PluginWechselAsync, () => AufgabeStatus is Domain.Enums.AufgabeStatus.Gestartet or Domain.Enums.AufgabeStatus.Wartend && _isCliRunning);
        AufgabeAbschliessenCommand = new AsyncRelayCommand(AufgabeAbschliessenAsync, () => ShowCliPanel && !_isCliRunning);
        SpeichernCommand = new AsyncRelayCommand(SpeichernAsync, () => KannSpeichern);
        LoeschenCommand = new AsyncRelayCommand(LoeschenAsync, () => KannLoeschen);
        InfoCliToggleCommand = new RelayCommand(InfoCliToggle);
        ZurueckCommand = new RelayCommand(() => ZurueckAction?.Invoke());
        IssueZuweisenCommand = new AsyncRelayCommand(IssueZuweisenAsync, () => CanAssignIssue && !_isLoading);
        IssueBrowserOeffnenCommand = new RelayCommand(
            IssueBrowserOeffnen,
            () => CurrentIssueReferenz?.IssueUrl != null);
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

            // Unmittelbar vor dem Auto-Restart nochmals live prüfen, ob der Prozess läuft.
            // Verhindert doppelten CLI-Start, wenn der Prozess nach dem Starten extrem schnell
            // abstürzt und LadenAsync ihn bereits als nicht-laufend sieht.
            if (Aufgabe?.Status == Domain.Enums.AufgabeStatus.Gestartet && !_kiService.IsRunning(_aufgabeId))
            {
                await CliAutomatischNeustartenAsync(ct);
            }
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

    private async Task CliStoppenAsync(CancellationToken ct)
    {
        if (_aufgabeId == Guid.Empty)
            return;

        FehlerMeldung = null;

        try
        {
            await _kiService.StopCliAsync(_aufgabeId, ct);
            _dispatcherInvoke(() =>
            {
                IsCliRunning = false;
                EmbeddedWindowHandle = IntPtr.Zero;
            });
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

    private async Task IssueZuweisenAsync(CancellationToken ct)
    {
        if (_aufgabe == null)
            return;

        var pluginTyp = _aufgabe.GitRepository?.PluginTyp;
        var gitPlugin = pluginTyp != null
            ? _pluginManager.GetSourceCodeManagementPlugins()
                .FirstOrDefault(p => string.Equals(p.PluginPrefix, pluginTyp, StringComparison.OrdinalIgnoreCase))
            : _pluginManager.GetSourceCodeManagementPlugins().FirstOrDefault();
        if (gitPlugin == null)
            return;

        var dialogVm = _serviceProvider.GetRequiredService<IssueSelectionDialogViewModel>();

        var repositoryId = _aufgabe.GitRepository?.RepositoryUrl ?? string.Empty;
        await dialogVm.LoadAsync(repositoryId, ct);

        var selectedIssue = await _dialogService.ShowIssueSelectionDialogAsync(dialogVm, ct);
        if (selectedIssue == null)
            return;

        try
        {
            await _aufgabeService.UpdateIssueReferenzAsync(_aufgabeId, selectedIssue, ct);
            await LadenAsync(ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Zuweisen des Issues für Aufgabe {AufgabeId}.", _aufgabeId);
            SetFehler(ex);
        }
    }

    private void IssueBrowserOeffnen()
    {
        var url = CurrentIssueReferenz?.IssueUrl;
        if (string.IsNullOrWhiteSpace(url))
            return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Öffnen der Issue-URL {IssueUrl}.", url);
            SetFehler(ex);
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

    private async Task StartenAsync(CancellationToken ct)
    {
        if (_aufgabeId == Guid.Empty || _aufgabe is null)
            return;

        FehlerMeldung = null;

        try
        {
            var pluginPrefix = await _pluginSelectionService.ResolveDevelopmentAutomationPluginWithProjectScopeAsync(
                _aufgabe.KiPluginPrefix,
                _aufgabe.ProjektId,
                ct);

            if (string.IsNullOrEmpty(pluginPrefix))
            {
                pluginPrefix = await ResolvePluginViaDialogAsync(_aufgabe, ct);
                if (string.IsNullOrEmpty(pluginPrefix))
                    return;
            }

            var repositoryUrl = _aufgabe.GitRepository?.RepositoryUrl ?? string.Empty;

            await _entwicklungsprozessService.ProzessStartenUndCliStartenAsync(
                _aufgabeId,
                repositoryUrl,
                null,
                pluginPrefix,
                ct);

            await LadenAsync(ct);

            // ProzessStartenUndCliStartenAsync startet die CLI intern ohne CliProzessGestartet zu feuern.
            // Das Event ist aber nötig, damit TaskDetailView.WaitForWindowHandleAsync den Prozess
            // überwacht und EmbeddedWindowHandle setzt.
            var runningProcess = _kiService.GetRunningProcess(_aufgabeId);
            if (runningProcess != null)
                CliProzessGestartet?.Invoke(runningProcess);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Starten der Aufgabe {AufgabeId}.", _aufgabeId);
            FehlerMeldung = $"Aufgabe konnte nicht gestartet werden: {ex.Message}";
        }
    }

    private async Task PluginWechselAsync(CancellationToken ct)
    {
        if (_aufgabeId == Guid.Empty || _aufgabe is null)
            return;

        FehlerMeldung = null;

        var pluginPrefix = await ResolvePluginViaDialogAsync(_aufgabe, ct);
        if (string.IsNullOrEmpty(pluginPrefix))
            return;

        try
        {
            await _kiService.StopCliAsync(_aufgabeId, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Stoppen des CLI für Aufgabe {AufgabeId} während Plugin-Wechsel.", _aufgabeId);
            FehlerMeldung = $"CLI konnte nicht gestoppt werden: {ex.Message}";
            return;
        }

        try
        {
            _dispatcherInvoke(() =>
            {
                IsCliRunning = false;
                EmbeddedWindowHandle = IntPtr.Zero;
            });

            var lokalerKlonPfad = _aufgabe.LokalerKlonPfad ?? string.Empty;
            await StartCliAndUpdateStateAsync(pluginPrefix, lokalerKlonPfad, _optionalCliParameters, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Neustarten des CLI für Aufgabe {AufgabeId} nach Plugin-Wechsel.", _aufgabeId);
            FehlerMeldung = $"CLI konnte nicht neu gestartet werden: {ex.Message}";
        }
    }

    private async Task CliAutomatischNeustartenAsync(CancellationToken ct)
    {
        if (_aufgabe is null || string.IsNullOrEmpty(_aufgabe.LokalerKlonPfad))
            return;

        try
        {
            var pluginPrefix = await _pluginSelectionService.ResolveDevelopmentAutomationPluginWithProjectScopeAsync(
                _aufgabe.KiPluginPrefix,
                _aufgabe.ProjektId,
                ct);

            if (string.IsNullOrEmpty(pluginPrefix))
                return;

            await StartCliAndUpdateStateAsync(pluginPrefix, _aufgabe.LokalerKlonPfad, null, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Automatischer CLI-Neustart für Aufgabe {AufgabeId} fehlgeschlagen.", _aufgabeId);
            FehlerMeldung = $"CLI konnte nicht automatisch neu gestartet werden: {ex.Message}";
        }
    }

    private async Task StartCliAndUpdateStateAsync(string pluginPrefix, string lokalerKlonPfad, string? optionalParameters, CancellationToken ct)
    {
        var kiPlugin = await _pluginSelectionService.ResolveDevelopmentAutomationPluginAsync(pluginPrefix, ct);
        IsCliRunning = true;
        try
        {
            var handle = await _kiService.StartCliAsync(_aufgabeId, kiPlugin, lokalerKlonPfad, optionalParameters, ct);

            SelectedKiPluginPrefix = pluginPrefix;
            if (!_kiService.IsRunning(_aufgabeId))
            {
                IsCliRunning = false;
                return;
            }

            CliProzessGestartet?.Invoke(handle.Process);
        }
        catch
        {
            IsCliRunning = false;
            throw;
        }
    }

    private async Task<string?> ResolvePluginViaDialogAsync(Aufgabe aufgabe, CancellationToken ct)
    {
        var dialogResult = await _dialogService.ShowPluginSelectionDialogAsync(
            VerfuegbareKiPlugins,
            _selectedKiPluginPrefix,
            ct);

        if (string.IsNullOrEmpty(dialogResult.SelectedPluginPrefix))
            return null;

        if (dialogResult.SaveAsProjectDefault)
        {
            await _pluginSelectionService.SaveProjectDefaultPluginPrefixAsync(aufgabe.ProjektId, PluginType.DevelopmentAutomation, dialogResult.SelectedPluginPrefix, ct);
        }

        await _aufgabeService.UpdateAsync(_aufgabeId, aufgabe.Titel, aufgabe.AnforderungsBeschreibung, dialogResult.SelectedPluginPrefix, ct);

        return dialogResult.SelectedPluginPrefix;
    }
}
