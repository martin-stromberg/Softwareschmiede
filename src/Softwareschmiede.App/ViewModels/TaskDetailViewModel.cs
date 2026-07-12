using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.App.Services;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Infrastructure.Terminal;

namespace Softwareschmiede.App.ViewModels;

/// <summary>
/// ViewModel für die Aufgabendetailansicht.
/// Verwaltet Status, Protokoll, CLI-Prozessstart und Fenstereinbettung.
/// </summary>
public sealed class TaskDetailViewModel : ViewModelBase, IDisposable
{
    private enum DetailAnsicht
    {
        Info,
        Cli,
        Diff
    }

    private readonly AufgabeService _aufgabeService;
    private readonly ProtokollService _protokollService;
    private readonly KiAusfuehrungsService _kiService;
    private readonly EntwicklungsprozessService _entwicklungsprozessService;
    private readonly PluginSelectionService _pluginSelectionService;
    private readonly PromptVorlagenService _promptVorlagenService;
    private readonly PromptVorlagenPlatzhalterService _promptVorlagenPlatzhalterService;
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
    private CancellationTokenSource? _ladenCts;
    private string? _editTitel;
    private string? _editAnforderungsBeschreibung;
    private bool _disposed;
    private string _cliStatusText = "CLI inaktiv";
    private string? _aktiverCliName;
    private PseudoConsoleSession? _cliStatusSession;
    private PromptVorlage? _selectedPromptVorlage;
    private DetailAnsicht _ausgewaehlteAnsicht = DetailAnsicht.Info;

    /// <summary>Wird aufgerufen, wenn der Nutzer zur vorherigen Ansicht zurückkehren möchte.</summary>
    public Action? ZurueckAction { get; set; }

    /// <summary>Wird nach dem Löschen einer Aufgabe aufgerufen, damit die übergeordnete Ansicht die Liste aktualisiert.</summary>
    public Func<Task>? AufgabeListeAktualisierenCallback { get; set; }

    /// <summary>Wird aufgerufen, wenn sich der Titel der angezeigten Detailaufgabe ändert.</summary>
    public Action<string?>? DetailTitelAenderungAction { get; set; }

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
                LadenAsync(_ladenCts.Token).SafeFireAndForget(_logger, "TaskDetailViewModel.LadenAsync");
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
            OnPropertyChanged(nameof(AufgabeBranchName));
            OnPropertyChanged(nameof(KannCliStoppen));
            OnPropertyChanged(nameof(KannCliNeuStarten));
            OnPropertyChanged(nameof(ShowEditPanel));
            OnPropertyChanged(nameof(ShowCliPanel));
            OnPropertyChanged(nameof(ShowDiffPanel));
            OnPropertyChanged(nameof(KannSpeichern));
            OnPropertyChanged(nameof(KannLoeschen));
            OnPropertyChanged(nameof(CanAssignIssue));
            OnPropertyChanged(nameof(CurrentIssueReferenz));
            OnPropertyChanged(nameof(ShowInfoPanel));
            WaehleStandardAnsicht();
            DetailTitelAenderungAction?.Invoke(value?.Titel);
        }
    }

    /// <summary>Titel der Aufgabe.</summary>
    /// <value>Der Titel der Aufgabe, oder ein Platzhaltertext während des Ladens.</value>
    public string AufgabeTitel => _aufgabe?.Titel ?? "(wird geladen…)";

    /// <summary>Status der Aufgabe.</summary>
    public AufgabeStatus AufgabeStatus => _aufgabe?.Status ?? Domain.Enums.AufgabeStatus.Neu;

    /// <summary>Branch-Name der Aufgabe.</summary>
    public string AufgabeBranchName => _aufgabe?.BranchName ?? string.Empty;

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
            OnPropertyChanged(nameof(KannCliNeuStarten));
            OnPropertyChanged(nameof(KannPromptVorlageSenden));
            OnPropertyChanged(nameof(KannSpeichern));
            OnPropertyChanged(nameof(KannLoeschen));
            OnPropertyChanged(nameof(CanAssignIssue));
        }
    }

    /// <summary>Aktueller Laufzeitstatus der CLI für die Fußzeile.</summary>
    public string CliStatusText
    {
        get => _cliStatusText;
        private set => SetProperty(ref _cliStatusText, value);
    }

    /// <summary>Name der aktuell ausgeführten CLI für die Fußzeile.</summary>
    public string? AktiverCliName
    {
        get => _aktiverCliName;
        private set => SetProperty(ref _aktiverCliName, value);
    }

    /// <summary>Gibt an, ob der laufende CLI-Prozess gestoppt werden kann.</summary>
    public bool KannCliStoppen => _isCliRunning;

    /// <summary>Gibt an, ob die CLI neu gestartet werden kann (Status Gestartet/Wartend, aber kein Prozess läuft).</summary>
    public bool KannCliNeuStarten => (_aufgabe?.Status is Domain.Enums.AufgabeStatus.Gestartet
        or Domain.Enums.AufgabeStatus.Wartend)
        && !_isCliRunning;

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

    /// <summary>Protokolleinträge der Aufgabe.</summary>
    /// <value>Die geladenen Protokolleinträge der Aufgabe.</value>
    public ObservableCollection<Protokolleintrag> Protokolleintraege { get; } = new();

    /// <summary>Verfügbare KI-Plugin-Prefixe.</summary>
    /// <value>Die Liste der verfügbaren KI-Plugin-Prefixe.</value>
    public ObservableCollection<string> VerfuegbareKiPlugins { get; } = new();

    /// <summary>Verfügbare Promptvorlagen.</summary>
    public ObservableCollection<PromptVorlage> PromptVorlagen { get; } = new();

    /// <summary>Aktuell gewählte Promptvorlage im Ribbon.</summary>
    public PromptVorlage? SelectedPromptVorlage
    {
        get => _selectedPromptVorlage;
        set
        {
            if (!SetProperty(ref _selectedPromptVorlage, value) || value is null)
                return;

            PromptVorlageAuswaehlenCommand.Execute(value);
        }
    }

    /// <summary>Gibt an, ob eine Promptvorlage an die laufende CLI gesendet werden kann.</summary>
    public bool KannPromptVorlageSenden => _isCliRunning && PromptVorlagen.Count > 0;

    /// <summary>Steuert die Info-Ansicht als Kompatibilitätsschicht für ältere Tests.</summary>
    public bool IsInfoViewVisible
    {
        get => IsInfoViewSelected;
        set
        {
            if (value)
            {
                WaehleAnsicht(DetailAnsicht.Info);
            }
            else if (ShowCliPanel)
            {
                WaehleAnsicht(DetailAnsicht.Cli);
            }
            else if (ShowDiffPanel)
            {
                WaehleAnsicht(DetailAnsicht.Diff);
            }
        }
    }

    /// <summary>Gibt an, ob die Stammdatenansicht ausgewählt ist.</summary>
    public bool IsInfoViewSelected => _ausgewaehlteAnsicht == DetailAnsicht.Info;

    /// <summary>Gibt an, ob die CLI-Ansicht ausgewählt ist.</summary>
    public bool IsCliViewSelected => _ausgewaehlteAnsicht == DetailAnsicht.Cli;

    /// <summary>Gibt an, ob die Diff-Ansicht ausgewählt ist.</summary>
    public bool IsDiffViewSelected => _ausgewaehlteAnsicht == DetailAnsicht.Diff;

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

    /// <summary>True wenn die Info-Ansicht angezeigt werden soll.</summary>
    public bool ShowInfoPanel => IsInfoViewSelected;

    /// <summary>CanExecute für SpeichernCommand: Status ∈ {Neu, Gestartet} &amp;&amp; !IsCliRunning &amp;&amp; Titel.Length > 0.</summary>
    public bool KannSpeichern => _aufgabe?.Status is Domain.Enums.AufgabeStatus.Neu or Domain.Enums.AufgabeStatus.Gestartet
        && !_isCliRunning
        && !string.IsNullOrEmpty(_editTitel);

    /// <summary>CanExecute für LoeschenCommand: Status ∉ {Archiviert} &amp;&amp; !IsCliRunning.</summary>
    /// <value>true wenn die Aufgabe gelöscht werden kann.</value>
    public bool KannLoeschen => _aufgabe?.Status is not Domain.Enums.AufgabeStatus.Archiviert
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

    /// <summary>Startet die CLI für eine bereits laufende Aufgabe neu (nach manuellem Stopp).</summary>
    public ICommand CliNeustartenCommand { get; }

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

    /// <summary>Wechselt zur Info-Ansicht.</summary>
    public ICommand InfoViewCommand { get; }

    /// <summary>Wechselt zur CLI-Ansicht.</summary>
    public ICommand CliViewCommand { get; }

    /// <summary>Wechselt zur Diff-Ansicht.</summary>
    public ICommand DiffViewCommand { get; }

    /// <summary>Navigiert zurück zur vorherigen Ansicht.</summary>
    public ICommand ZurueckCommand { get; }

    /// <summary>Öffnet den Issue-Auswahl-Dialog und weist das gewählte Issue der Aufgabe zu.</summary>
    public ICommand IssueZuweisenCommand { get; }

    /// <summary>Öffnet die Issue-URL im Standard-Browser.</summary>
    public ICommand IssueBrowserOeffnenCommand { get; }

    /// <summary>Sendet die gewählte Promptvorlage an die laufende CLI.</summary>
    public ICommand PromptVorlageAuswaehlenCommand { get; }

    /// <summary>Wird gefeuert, wenn eine neue <see cref="PseudoConsoleSession"/> gestartet wurde. Löst weiterhin
    /// das Binden von <c>TerminalControl.Session</c> in <c>TaskDetailView</c> aus, unabhängig davon, ob die
    /// Leseschleife der Session bereits vor der UI-Bindung läuft (parallele CLI-Ausführungen, Issue-86).</summary>
    public event Action<PseudoConsoleSession>? PseudoConsoleSessionGestartet;

    /// <summary>Wird gefeuert, wenn der CLI-Prozess der aktuellen Aufgabe beendet wurde.</summary>
    public event Action? CliGestoppt;

    /// <summary>Wird gefeuert, nachdem eine Promptvorlage erfolgreich an die CLI gesendet wurde.</summary>
    public event Action? PromptVorlageGesendet;

    /// <summary>Gibt die aktive <see cref="PseudoConsoleSession"/> für die aktuelle Aufgabe zurück, oder null.
    /// Die Session (und ihre Leseschleife) läuft unabhängig vom Lebenszyklus der View, die diese Methode
    /// aufruft — der zurückgegebene Prozess kann also bereits vor dem Öffnen dieser Aufgabenseite gestartet
    /// worden sein und weiterlaufen, nachdem die Seite wieder verlassen wird.</summary>
    /// <returns>Die aktive <see cref="PseudoConsoleSession"/>, oder null wenn keine Session läuft.</returns>
    public PseudoConsoleSession? GetPseudoConsoleSession() => _kiService.GetPseudoConsoleSession(_aufgabeId);

    /// <inheritdoc cref="TaskDetailViewModel"/>
    public TaskDetailViewModel(
        AufgabeService aufgabeService,
        ProtokollService protokollService,
        KiAusfuehrungsService kiService,
        EntwicklungsprozessService entwicklungsprozessService,
        PluginSelectionService pluginSelectionService,
        PromptVorlagenService promptVorlagenService,
        PromptVorlagenPlatzhalterService promptVorlagenPlatzhalterService,
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
        _promptVorlagenService = promptVorlagenService;
        _promptVorlagenPlatzhalterService = promptVorlagenPlatzhalterService;
        _dialogService = dialogService;
        _pluginManager = pluginManager;
        _serviceProvider = serviceProvider;
        _logger = logger;
        _dispatcherInvoke = DispatcherInvokeFactory.Create(dispatcherInvoke);

        _kiService.CliProcessStatusChanged += OnCliProcessStatusChanged;

        LadenCommand = new AsyncRelayCommand(ct => LadenAsync(ct));
        CliStoppenCommand = new AsyncRelayCommand(CliStoppenAsync, () => KannCliStoppen);
        CliNeustartenCommand = new AsyncRelayCommand(CliNeustartenAsync, () => KannCliNeuStarten);
        StartenCommand = new AsyncRelayCommand(StartenAsync, () => AufgabeStatus == Domain.Enums.AufgabeStatus.Neu && !_isCliRunning);
        PluginAendernCommand = new AsyncRelayCommand(PluginWechselAsync, () => AufgabeStatus is Domain.Enums.AufgabeStatus.Gestartet or Domain.Enums.AufgabeStatus.Wartend && _isCliRunning);
        AufgabeAbschliessenCommand = new AsyncRelayCommand(AufgabeAbschliessenAsync, () => ShowCliPanel && !_isCliRunning);
        SpeichernCommand = new AsyncRelayCommand(SpeichernAsync, () => KannSpeichern);
        LoeschenCommand = new AsyncRelayCommand(LoeschenAsync, () => KannLoeschen);
        InfoCliToggleCommand = new RelayCommand(InfoCliToggle);
        InfoViewCommand = new RelayCommand(() => WaehleAnsicht(DetailAnsicht.Info));
        CliViewCommand = new RelayCommand(() => WaehleAnsicht(DetailAnsicht.Cli), () => ShowCliPanel);
        DiffViewCommand = new RelayCommand(() => WaehleAnsicht(DetailAnsicht.Diff), () => ShowDiffPanel);
        ZurueckCommand = new RelayCommand(() => ZurueckAction?.Invoke());
        IssueZuweisenCommand = new AsyncRelayCommand(IssueZuweisenAsync, () => CanAssignIssue && !_isLoading);
        IssueBrowserOeffnenCommand = new RelayCommand(
            IssueBrowserOeffnen,
            () => CurrentIssueReferenz?.IssueUrl != null);
        PromptVorlageAuswaehlenCommand = new AsyncRelayCommand<PromptVorlage>(
            PromptVorlageAuswaehlenAsync,
            vorlage => vorlage is not null && KannPromptVorlageSenden);
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

            var session = _kiService.GetPseudoConsoleSession(_aufgabeId);
            AttachCliStatusSession(session);
            // Explizit erneut auslösen (nicht nur AttachCliStatusSession): Wechselt CurrentView in
            // MainWindowViewModel/ProjectDetailViewModel zwischen zwei TaskDetailViewModel-Instanzen
            // desselben Typs, kann TaskDetailView.OnDataContextChanged bereits synchron vor dem Setzen
            // von AufgabeId gefeuert haben und liest dabei eine veraltete/leere Sitzung. Ohne diesen
            // erneuten Abgleich bliebe TerminalControl.Session dauerhaft auf der vorherigen Aufgabe
            // stehen, wenn eine bereits laufende Sitzung wiederangebunden statt neu gestartet wird.
            if (session is not null)
                PseudoConsoleSessionGestartet?.Invoke(session);
            else
                CliGestoppt?.Invoke();

            await AktualisiereAktivenCliNameAusAufgabeAsync(ct);

            EditTitel = Aufgabe?.Titel;
            EditAnforderungsBeschreibung = Aufgabe?.AnforderungsBeschreibung;

            var protokolleintraege = await _protokollService.GetByAufgabeAsync(_aufgabeId, ct);
            Protokolleintraege.Clear();
            foreach (var eintrag in protokolleintraege)
                Protokolleintraege.Add(eintrag);

            await LadeVerfuegbarePluginsAsync(ct);
            await LadePromptVorlagenAsync(ct);

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

    private async Task LadePromptVorlagenAsync(CancellationToken ct)
    {
        var vorlagen = await _promptVorlagenService.GetAllAsync(ct);
        PromptVorlagen.Clear();
        foreach (var vorlage in vorlagen)
            PromptVorlagen.Add(vorlage);

        OnPropertyChanged(nameof(KannPromptVorlageSenden));
    }

    private async Task PromptVorlageAuswaehlenAsync(PromptVorlage? vorlage, CancellationToken ct)
    {
        if (vorlage is null || string.IsNullOrWhiteSpace(vorlage.Prompttext))
            return;

        var session = _kiService.GetPseudoConsoleSession(_aufgabeId);
        if (session is null || !_isCliRunning)
            return;

        var prompt = _promptVorlagenPlatzhalterService.Resolve(vorlage.Prompttext, _aufgabe);
        if (string.IsNullOrWhiteSpace(prompt))
            return;

        var bytes = Encoding.UTF8.GetBytes(prompt + Environment.NewLine);
        await session.InputStream.WriteAsync(bytes, ct);
        await session.InputStream.FlushAsync(ct);
        session.MarkInputActivity();

        WaehleAnsicht(DetailAnsicht.Cli);
        SelectedPromptVorlage = null;
        PromptVorlageGesendet?.Invoke();
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
                AktiverCliName = null;
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

    private async Task CliNeustartenAsync(CancellationToken ct)
    {
        if (_aufgabeId == Guid.Empty || _aufgabe is null)
            return;

        FehlerMeldung = null;

        try
        {
            await CliAutomatischNeustartenAsync(ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim manuellen CLI-Neustart für Aufgabe {AufgabeId}.", _aufgabeId);
            FehlerMeldung = $"CLI konnte nicht gestartet werden: {ex.Message}";
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

    private void WaehleStandardAnsicht()
    {
        var standardAnsicht = AufgabeStatus switch
        {
            Domain.Enums.AufgabeStatus.Gestartet or Domain.Enums.AufgabeStatus.Wartend => DetailAnsicht.Cli,
            Domain.Enums.AufgabeStatus.Beendet => DetailAnsicht.Diff,
            _ => DetailAnsicht.Info
        };

        WaehleAnsicht(standardAnsicht);
    }

    private void WaehleAnsicht(DetailAnsicht ansicht)
    {
        if (ansicht == DetailAnsicht.Cli && !ShowCliPanel)
            ansicht = DetailAnsicht.Info;
        if (ansicht == DetailAnsicht.Diff && !ShowDiffPanel)
            ansicht = DetailAnsicht.Info;

        if (_ausgewaehlteAnsicht == ansicht)
            return;

        _ausgewaehlteAnsicht = ansicht;
        OnPropertyChanged(nameof(IsInfoViewVisible));
        OnPropertyChanged(nameof(IsInfoViewSelected));
        OnPropertyChanged(nameof(IsCliViewSelected));
        OnPropertyChanged(nameof(IsDiffViewSelected));
        OnPropertyChanged(nameof(ShowInfoPanel));
    }

    private async Task AktualisiereAktivenCliNameAusAufgabeAsync(CancellationToken ct)
    {
        if (!_isCliRunning)
        {
            AktiverCliName = null;
            return;
        }

        var pluginPrefix = await _pluginSelectionService.ResolveDevelopmentAutomationPluginWithProjectScopeAsync(
            _aufgabe?.KiPluginPrefix,
            _aufgabe?.ProjektId ?? Guid.Empty,
            ct);

        SetAktiverCliName(pluginPrefix);
    }

    private void SetAktiverCliName(string? pluginPrefix)
    {
        AktiverCliName = ResolveKiPluginName(pluginPrefix);
    }

    private string? ResolveKiPluginName(string? pluginPrefix)
    {
        if (string.IsNullOrWhiteSpace(pluginPrefix))
            return null;

        return _pluginManager.GetDevelopmentAutomationPlugins()
            .FirstOrDefault(p => string.Equals(p.PluginPrefix, pluginPrefix, StringComparison.OrdinalIgnoreCase))
            ?.PluginName ?? pluginPrefix;
    }

    private void OnCliProcessStatusChanged(Guid aufgabeId, CliProcessStatus status)
    {
        if (aufgabeId != _aufgabeId)
            return;

        _dispatcherInvoke(() =>
        {
            try
            {
                IsCliRunning = status == CliProcessStatus.Gestartet;
                if (status != CliProcessStatus.Gestartet)
                {
                    AttachCliStatusSession(null);
                    AktiverCliName = null;
                    CliStatusText = status == CliProcessStatus.Fehler
                        ? "CLI-Status: Fehler"
                        : "CLI inaktiv";
                    CliGestoppt?.Invoke();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Verarbeiten des CLI-Status-Wechsels für Aufgabe {AufgabeId}.", aufgabeId);
            }
        });
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        _kiService.CliProcessStatusChanged -= OnCliProcessStatusChanged;
        AttachCliStatusSession(null);
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
            SetAktiverCliName(pluginPrefix);

            var session = _kiService.GetPseudoConsoleSession(_aufgabeId);
            if (session != null)
            {
                AttachCliStatusSession(session);
                PseudoConsoleSessionGestartet?.Invoke(session);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            AktiverCliName = null;
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
                AktiverCliName = null;
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
            AktiverCliName = null;
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
        AktiverCliName = ResolveKiPluginName(kiPlugin.PluginPrefix) ?? kiPlugin.PluginName ?? kiPlugin.PluginPrefix;
        try
        {
            var handle = await _kiService.StartWithPseudoConsoleAsync(_aufgabeId, kiPlugin, lokalerKlonPfad, optionalParameters, ct);

            SelectedKiPluginPrefix = pluginPrefix;
            if (!_kiService.IsRunning(_aufgabeId))
            {
                IsCliRunning = false;
                AktiverCliName = null;
                return;
            }

            if (handle.PseudoConsoleSession != null)
            {
                AttachCliStatusSession(handle.PseudoConsoleSession);
                PseudoConsoleSessionGestartet?.Invoke(handle.PseudoConsoleSession);
            }
        }
        catch
        {
            IsCliRunning = false;
            AktiverCliName = null;
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

    private void AttachCliStatusSession(PseudoConsoleSession? session)
    {
        if (ReferenceEquals(_cliStatusSession, session))
        {
            UpdateCliStatusText(session?.RuntimeStatus ?? CliRuntimeStatus.Inaktiv);
            return;
        }

        if (_cliStatusSession != null)
            _cliStatusSession.RuntimeStatusChanged -= OnCliRuntimeStatusChanged;

        _cliStatusSession = session;

        if (_cliStatusSession != null)
        {
            _cliStatusSession.RuntimeStatusChanged += OnCliRuntimeStatusChanged;
            UpdateCliStatusText(_cliStatusSession.RuntimeStatus);
        }
        else
        {
            UpdateCliStatusText(CliRuntimeStatus.Inaktiv);
        }
    }

    private void OnCliRuntimeStatusChanged(object? sender, CliRuntimeStatusChangedEventArgs e)
    {
        _dispatcherInvoke(() => UpdateCliStatusText(e.Status));
    }

    private void UpdateCliStatusText(CliRuntimeStatus status)
    {
        CliStatusText = status switch
        {
            CliRuntimeStatus.Laeuft => "CLI-Status: Ausführung läuft",
            CliRuntimeStatus.WartetAufEingabe => "CLI-Status: Wartet auf Eingabe",
            CliRuntimeStatus.Inaktiv => "CLI inaktiv",
            _ => "CLI-Status: unbekannt"
        };
    }
}
