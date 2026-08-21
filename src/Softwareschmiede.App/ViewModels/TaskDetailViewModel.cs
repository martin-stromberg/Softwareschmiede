using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.App.Services;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.PluginImpl;
using Softwareschmiede.Infrastructure.Terminal;

namespace Softwareschmiede.App.ViewModels;

/// <summary>
/// ViewModel für die Aufgabendetailansicht.
/// Verwaltet Status, Protokoll, CLI-Prozessstart und Fenstereinbettung.
/// </summary>
public sealed class TaskDetailViewModel : ViewModelBase, IDisposable
{
    private const string RepositoryPreparationStatusText = "Bereit Repository vor...";

    private enum DetailAnsicht
    {
        Info,
        Cli,
        Diff,
        Dateibrowser,
        PullRequests,
        Todos
    }

    private readonly AufgabeService _aufgabeService;
    private readonly ProtokollService _protokollService;
    private readonly KiAusfuehrungsService _kiService;
    private readonly EntwicklungsprozessService _entwicklungsprozessService;
    private readonly PluginSelectionService _pluginSelectionService;
    private readonly PromptVorlagenService _promptVorlagenService;
    private readonly PromptVorlagenPlatzhalterService _promptVorlagenPlatzhalterService;
    private readonly PromptZeitVersandService _promptZeitVersandService;
    private readonly IDialogService _dialogService;
    private readonly IPluginManager _pluginManager;
    private readonly IServiceProvider _serviceProvider;
    private readonly FileExplorerViewModel _fileExplorerViewModel;
    private readonly TodoListViewModel _todoListViewModel;
    private readonly ArbeitsverzeichnisOeffnenService _arbeitsverzeichnisOeffnenService;
    private readonly AutonomAufgabeStartService _autonomAufgabeStartService;
    private readonly ILogger<TaskDetailViewModel> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly Action<Action> _dispatcherInvoke;

    private Guid _aufgabeId;
    private Aufgabe? _aufgabe;
    private bool _isLoading;
    private string? _fehlerMeldung;
    private bool _isCliRunning;
    private string? _selectedKiPluginPrefix;
    private bool _zeigeKiPluginAuswahl;
    private string? _optionalCliParameters;
    private CancellationTokenSource? _ladenCts;
    private CancellationTokenSource? _protokollLadenCts;
    private string? _editTitel;
    private string? _editAnforderungsBeschreibung;
    private bool _disposed;
    private string _cliStatusText = "CLI inaktiv";
    private string? _aktiverCliName;
    private PseudoConsoleSession? _cliStatusSession;
    private PromptVorlage? _selectedPromptVorlage;
    private DetailAnsicht _ausgewaehlteAnsicht = DetailAnsicht.Info;
    private int? _scheduledPromptTargetHours;
    private int? _scheduledPromptTargetMinutes;
    private string? _scheduledPromptStatus;
    private string? _scheduledPromptTimeDisplay;
    private bool _showFileExplorerPanel;
    private bool _canCreatePullRequest;
    private bool _canCreateIssue;
    private bool _isRefreshingPullRequests;
    private bool _kannIdeAuswaehlen;

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
            _showFileExplorerPanel = !string.IsNullOrEmpty(value?.LokalerKlonPfad) && Directory.Exists(value.LokalerKlonPfad);
            OnPropertyChanged(nameof(AufgabeTitel));
            OnPropertyChanged(nameof(AufgabeStatus));
            OnPropertyChanged(nameof(AufgabeBranchName));
            OnPropertyChanged(nameof(KannCliStoppen));
            OnPropertyChanged(nameof(KannCliNeuStarten));
            OnPropertyChanged(nameof(KannAufgabeAbschliessen));
            OnPropertyChanged(nameof(ShowEditPanel));
            OnPropertyChanged(nameof(ShowCliPanel));
            OnPropertyChanged(nameof(ShowDiffPanel));
            OnPropertyChanged(nameof(ShowFileExplorerPanel));
            OnPropertyChanged(nameof(ShowPullRequestPanel));
            OnPropertyChanged(nameof(KannIdeOeffnen));
            OnPropertyChanged(nameof(KannSpeichern));
            OnPropertyChanged(nameof(KannLoeschen));
            OnPropertyChanged(nameof(KannPullRequestErstellen));
            OnPropertyChanged(nameof(CanAssignIssue));
            OnPropertyChanged(nameof(CanCreateIssue));
            OnPropertyChanged(nameof(ShowIssueGroup));
            OnPropertyChanged(nameof(CurrentIssueReferenz));
            OnPropertyChanged(nameof(ShowInfoPanel));
            OnPropertyChanged(nameof(IsPullRequestViewSelected));
            WaehleStandardAnsicht();
            DetailTitelAenderungAction?.Invoke(value?.Titel);

            _fileExplorerViewModel.InitialisierenAsync(value?.LokalerKlonPfad, CancellationToken.None)
                .SafeFireAndForget(_logger, "TaskDetailViewModel.FileExplorer.InitialisierenAsync");

            CommandManager.InvalidateRequerySuggested();
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
        private set
        {
            SetProperty(ref _isLoading, value);
            OnPropertyChanged(nameof(CanCreateIssue));
            OnPropertyChanged(nameof(ShowIssueGroup));
        }
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
            OnPropertyChanged(nameof(KannAufgabeAbschliessen));
            OnPropertyChanged(nameof(KannPromptVorlageSenden));
            OnPropertyChanged(nameof(KannSpeichern));
            OnPropertyChanged(nameof(KannLoeschen));
            OnPropertyChanged(nameof(KannPullRequestErstellen));
            OnPropertyChanged(nameof(CanAssignIssue));
            OnPropertyChanged(nameof(CanCreateIssue));
            OnPropertyChanged(nameof(ShowIssueGroup));
            OnPropertyChanged(nameof(KannPromptPlanen));
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

    /// <summary>Gibt an, ob die CLI fuer eine aktive Ausfuehrung wiederhergestellt werden kann.</summary>
    public bool KannCliNeuStarten => _aufgabe?.AusfuehrungsStatus.SollCliAnzeigen(_aufgabe.Status) == true
        && !_isCliRunning;

    /// <summary>Gibt an, ob die Aufgabe endgueltig abgeschlossen werden kann.</summary>
    public bool KannAufgabeAbschliessen => _aufgabe?.Status.IstAktivOderWartend() == true
        && !_isCliRunning;

    /// <summary>Gewähltes KI-Plugin (Prefix).</summary>
    public string? SelectedKiPluginPrefix
    {
        get => _selectedKiPluginPrefix;
        set => SetProperty(ref _selectedKiPluginPrefix, value);
    }

    /// <summary>Steuert die Sichtbarkeit des KI-Plugin-Selectors (false bei genau einem aktiven Plugin).</summary>
    public bool ZeigeKiPluginAuswahl
    {
        get => _zeigeKiPluginAuswahl;
        private set => SetProperty(ref _zeigeKiPluginAuswahl, value);
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

    /// <summary>Pull Requests der Aufgabe.</summary>
    public ObservableCollection<PullRequestReferenz> PullRequests { get; } = new();

    /// <summary>Komponiertes Presentation Model der To-Do-Liste.</summary>
    public TodoListViewModel TodoList => _todoListViewModel;

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
            var geaendert = SetProperty(ref _selectedPromptVorlage, value);
            OnPropertyChanged(nameof(KannPromptPlanen));

            if (!geaendert || value is null)
                return;

            PromptVorlageAuswaehlenCommand.Execute(value);
        }
    }

    /// <summary>Gibt an, ob eine Promptvorlage an die laufende CLI gesendet werden kann.</summary>
    public bool KannPromptVorlageSenden => _isCliRunning && PromptVorlagen.Count > 0;

    /// <summary>Bindung Stunde-Eingabefeld für den zeitgesteuerten Prompt-Versand (null bedeutet leer).</summary>
    public int? ScheduledPromptTargetHours
    {
        get => _scheduledPromptTargetHours;
        set
        {
            SetProperty(ref _scheduledPromptTargetHours, value);
            OnPropertyChanged(nameof(KannPromptPlanen));
        }
    }

    /// <summary>Bindung Minute-Eingabefeld für den zeitgesteuerten Prompt-Versand (null bedeutet leer).</summary>
    public int? ScheduledPromptTargetMinutes
    {
        get => _scheduledPromptTargetMinutes;
        set
        {
            SetProperty(ref _scheduledPromptTargetMinutes, value);
            OnPropertyChanged(nameof(KannPromptPlanen));
        }
    }

    /// <summary>Anzeigetext, während ein Prompt zeitgesteuert in Wartestellung ist, oder null.</summary>
    public string? ScheduledPromptStatus
    {
        get => _scheduledPromptStatus;
        private set => SetProperty(ref _scheduledPromptStatus, value);
    }

    /// <summary>Zielzeit des geplanten Prompts im Format HH:mm, oder null wenn kein Prompt geplant ist.</summary>
    public string? ScheduledPromptTimeDisplay
    {
        get => _scheduledPromptTimeDisplay;
        private set => SetProperty(ref _scheduledPromptTimeDisplay, value);
    }

    /// <summary>Gibt an, ob die zeitgesteuerte Versendung aktuell geplant werden kann (CLI läuft, Vorlage gewählt, Zeit eingegeben).</summary>
    public bool KannPromptPlanen => _isCliRunning
        && _selectedPromptVorlage is not null
        && !string.IsNullOrWhiteSpace(_selectedPromptVorlage.Prompttext)
        && (_scheduledPromptTargetHours.HasValue || _scheduledPromptTargetMinutes.HasValue);

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

    /// <summary>Gibt an, ob die Dateiexplorer-Ansicht ausgewählt ist.</summary>
    public bool IsFileExplorerViewSelected => _ausgewaehlteAnsicht == DetailAnsicht.Dateibrowser;

    /// <summary>Gibt an, ob die Pull-Request-Ansicht ausgewählt ist.</summary>
    public bool IsPullRequestViewSelected => _ausgewaehlteAnsicht == DetailAnsicht.PullRequests;

    /// <summary>Gibt an, ob die Todo-Ansicht ausgewählt ist.</summary>
    public bool IsTodoViewSelected => _ausgewaehlteAnsicht == DetailAnsicht.Todos;

    /// <summary>Gibt an, ob Pull Requests angezeigt werden koennen.</summary>
    public bool ShowPullRequestPanel => _aufgabe is not null;

    /// <summary>Gibt an, ob keine Pull Requests gespeichert sind.</summary>
    public bool HasNoPullRequests => PullRequests.Count == 0;

    /// <summary>Gibt an, ob Pull Requests gespeichert sind.</summary>
    public bool HasPullRequests => PullRequests.Count > 0;

    /// <summary>Gibt an, ob die PR-Daten gerade aktualisiert werden.</summary>
    public bool IsRefreshingPullRequests
    {
        get => _isRefreshingPullRequests;
        private set
        {
            SetProperty(ref _isRefreshingPullRequests, value);
            OnPropertyChanged(nameof(CanRefreshPullRequests));
            CommandManager.InvalidateRequerySuggested();
        }
    }

    /// <summary>Gibt an, ob Pull Requests manuell aktualisiert werden koennen.</summary>
    public bool CanRefreshPullRequests => _aufgabeId != Guid.Empty && !_isRefreshingPullRequests;

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

    /// <summary>True wenn die Aufgabe eine aktive KI-Ausfuehrung anzeigen soll.</summary>
    public bool ShowCliPanel => _aufgabe?.AusfuehrungsStatus.SollCliAnzeigen(_aufgabe.Status) == true;

    /// <summary>True wenn Status == Beendet, sonst false.</summary>
    public bool ShowDiffPanel => _aufgabe?.Status == Domain.Enums.AufgabeStatus.Beendet;

    /// <summary>True wenn Aufgabe.LokalerKlonPfad gesetzt ist und das Verzeichnis existiert. Wird beim Setzen von <see cref="Aufgabe"/> einmalig ermittelt und gecacht, um wiederholte synchrone Dateisystemzugriffe bei jedem Property-Zugriff zu vermeiden.</summary>
    public bool ShowFileExplorerPanel => _showFileExplorerPanel;

    /// <summary>
    /// True wenn die IDE-Aktion ausgeführt werden kann. Die konkrete IDE wird erst beim Ausführen von
    /// <see cref="OeffneIdeCommand"/> über <see cref="PluginSelectionService.ResolveIdePluginAsync"/> aufgelöst
    /// (explizit/fallback-kompatibles Plugin oder Default-Plugin) - da mindestens ein IDE-Plugin systemseitig
    /// stets aktiv bleiben muss, genügt als Bedingung ein gültiges, vorhandenes Arbeitsverzeichnis.
    /// </summary>
    public bool KannIdeOeffnen => ShowFileExplorerPanel;

    /// <summary>
    /// Gibt an, ob mehrere Einstiegspunkte für die aktuell aufgelöste IDE verfügbar sind. Steuert die
    /// Sichtbarkeit des Dropdown-Buttons im <see cref="Controls.RibbonSplitButton"/>. Wird bei jedem Aufruf
    /// von <see cref="OeffneIdeAsync"/> oder <see cref="OeffneIdeAuswahlAsync"/> aktualisiert.
    /// </summary>
    public bool KannIdeAuswaehlen
    {
        get => _kannIdeAuswaehlen;
        private set => SetProperty(ref _kannIdeAuswaehlen, value);
    }

    /// <summary>True wenn die Info-Ansicht angezeigt werden soll.</summary>
    public bool ShowInfoPanel => IsInfoViewSelected;

    /// <summary>Komponiertes Presentation Model des Dateiexplorers.</summary>
    public FileExplorerViewModel FileExplorer => _fileExplorerViewModel;

    /// <summary>CanExecute für SpeichernCommand: Status ∈ {Neu, Gestartet} &amp;&amp; !IsCliRunning &amp;&amp; Titel.Length > 0.</summary>
    public bool KannSpeichern => _aufgabe?.Status is Domain.Enums.AufgabeStatus.Neu or Domain.Enums.AufgabeStatus.Gestartet
        && !_isCliRunning
        && !string.IsNullOrEmpty(_editTitel);

    /// <summary>CanExecute für LoeschenCommand: Status ∉ {Archiviert} &amp;&amp; !IsCliRunning.</summary>
    /// <value>true wenn die Aufgabe gelöscht werden kann.</value>
    public bool KannLoeschen => _aufgabe?.Status is not Domain.Enums.AufgabeStatus.Archiviert
        && _aufgabe != null
        && !_isCliRunning;

    /// <summary>true wenn für die Aufgabe ein Pull Request erstellt werden kann.</summary>
    public bool KannPullRequestErstellen => _aufgabe != null
        && !string.IsNullOrWhiteSpace(_aufgabe.BranchName)
        && !string.IsNullOrWhiteSpace(_aufgabe.GitRepository?.RepositoryUrl)
        && _canCreatePullRequest;

    /// <summary>true wenn Aufgabe vorhanden, SCM-Plugin Issues unterstützt und kein CLI läuft.</summary>
    public bool CanAssignIssue => _aufgabe != null
        && !_isCliRunning
        && _pluginManager.GetSourceCodeManagementPlugins().Any(p => p is IGitPlugin);

    /// <summary>true wenn ein neues Issue für die Aufgabe angelegt werden kann.</summary>
    public bool CanCreateIssue => _aufgabe != null
        && !_isLoading
        && !_isCliRunning
        && _aufgabe.IssueReferenz is null
        && !string.IsNullOrWhiteSpace(_aufgabe.GitRepository?.RepositoryUrl)
        && _canCreateIssue;

    /// <summary>true wenn die Issue-Ribbon-Gruppe sichtbar sein soll.</summary>
    public bool ShowIssueGroup => CanAssignIssue || CanCreateIssue || CurrentIssueReferenz?.IssueUrl is not null;

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

    /// <summary>Erstellt einen Pull Request für die Aufgabe.</summary>
    public ICommand PullRequestErstellenCommand { get; }

    /// <summary>Aktualisiert Pull Requests und zugeordnete Workflow-Runs der Aufgabe.</summary>
    public ICommand PullRequestsAktualisierenCommand { get; }

    /// <summary>Öffnet eine Pull-Request-URL im Standard-Browser.</summary>
    public ICommand PullRequestUrlOeffnenCommand { get; }

    /// <summary>Toggled IsInfoViewVisible zwischen Info-Panel und CLI-Fenster.</summary>
    public ICommand InfoCliToggleCommand { get; }

    /// <summary>Wechselt zur Info-Ansicht.</summary>
    public ICommand InfoViewCommand { get; }

    /// <summary>Wechselt zur CLI-Ansicht.</summary>
    public ICommand CliViewCommand { get; }

    /// <summary>Wechselt zur Diff-Ansicht.</summary>
    public ICommand DiffViewCommand { get; }

    /// <summary>Wechselt zur Dateiexplorer-Ansicht.</summary>
    public ICommand DateiViewCommand { get; }

    /// <summary>Wechselt zur Pull-Request-Ansicht.</summary>
    public ICommand PullRequestViewCommand { get; }

    /// <summary>Navigiert zurück zur vorherigen Ansicht.</summary>
    public ICommand ZurueckCommand { get; }

    /// <summary>Öffnet den Issue-Auswahl-Dialog und weist das gewählte Issue der Aufgabe zu.</summary>
    public ICommand IssueZuweisenCommand { get; }

    /// <summary>Öffnet den Issue-Anlage-Dialog und weist das neu erstellte Issue der Aufgabe zu.</summary>
    public ICommand IssueAnlegenCommand { get; }

    /// <summary>Öffnet die Issue-URL im Standard-Browser.</summary>
    public ICommand IssueBrowserOeffnenCommand { get; }

    /// <summary>Öffnet den Initialisierungsdialog für eine Autonome Aufgabe und anschließend deren Detail-Ansicht.</summary>
    public ICommand AutonomAufgabeInitialisierenCommand { get; }

    /// <summary>Sendet die gewählte Promptvorlage an die laufende CLI.</summary>
    public ICommand PromptVorlageAuswaehlenCommand { get; }

    /// <summary>Plant den Versand der aktuell gewählten Promptvorlage zur eingegebenen Zielzeit.</summary>
    public ICommand SchedulePromptCommand { get; }

    /// <summary>Öffnet das Arbeitsverzeichnis der Aufgabe im OS-Dateiexplorer.</summary>
    public ICommand OeffneArbeitsverzeichnisCommand { get; }

    /// <summary>Öffnet die Solution des Arbeitsverzeichnisses mit der registrierten IDE.</summary>
    public ICommand OeffneIdeCommand { get; }

    /// <summary>Zeigt die Auswahl der verfügbaren IDE-Einstiegspunkte an und öffnet den gewählten.</summary>
    public ICommand OeffneIdeAuswahlCommand { get; }

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
        PromptZeitVersandService promptZeitVersandService,
        IDialogService dialogService,
        IPluginManager pluginManager,
        IServiceProvider serviceProvider,
        ILogger<TaskDetailViewModel> logger,
        TimeProvider timeProvider,
        FileExplorerViewModel fileExplorerViewModel,
        TodoListViewModel todoListViewModel,
        ArbeitsverzeichnisOeffnenService arbeitsverzeichnisOeffnenService,
        AutonomAufgabeStartService autonomAufgabeStartService,
        Action<Action>? dispatcherInvoke = null)
    {
        _aufgabeService = aufgabeService;
        _protokollService = protokollService;
        _kiService = kiService;
        _entwicklungsprozessService = entwicklungsprozessService;
        _pluginSelectionService = pluginSelectionService;
        _promptVorlagenService = promptVorlagenService;
        _promptVorlagenPlatzhalterService = promptVorlagenPlatzhalterService;
        _promptZeitVersandService = promptZeitVersandService;
        _dialogService = dialogService;
        _pluginManager = pluginManager;
        _serviceProvider = serviceProvider;
        _logger = logger;
        _fileExplorerViewModel = fileExplorerViewModel;
        _todoListViewModel = todoListViewModel;
        _arbeitsverzeichnisOeffnenService = arbeitsverzeichnisOeffnenService;
        _autonomAufgabeStartService = autonomAufgabeStartService;
        _timeProvider = timeProvider;
        _dispatcherInvoke = DispatcherInvokeFactory.Create(dispatcherInvoke);

        _kiService.CliProcessStatusChanged += OnCliProcessStatusChanged;
        _promptZeitVersandService.PromptSent += OnPromptSent;
        _todoListViewModel.AnsichtAktivierenCallback = () => WaehleAnsicht(DetailAnsicht.Todos);
        _todoListViewModel.FehlerCallback = meldung => FehlerMeldung = meldung;

        LadenCommand = new AsyncRelayCommand(ct => LadenAsync(ct));
        CliStoppenCommand = new AsyncRelayCommand(CliStoppenAsync, () => KannCliStoppen);
        CliNeustartenCommand = new AsyncRelayCommand(CliNeustartenAsync, () => KannCliNeuStarten);
        StartenCommand = new AsyncRelayCommand(
            StartenAsync,
            () => _aufgabe?.AusfuehrungsStatus.DarfAusfuehrungStarten(_aufgabe.Status) == true && !_isCliRunning);
        PluginAendernCommand = new AsyncRelayCommand(PluginWechselAsync, () => AufgabeStatus is Domain.Enums.AufgabeStatus.Gestartet or Domain.Enums.AufgabeStatus.Wartend && _isCliRunning);
        AufgabeAbschliessenCommand = new AsyncRelayCommand(AufgabeAbschliessenAsync, () => KannAufgabeAbschliessen);
        SpeichernCommand = new AsyncRelayCommand(SpeichernAsync, () => KannSpeichern);
        LoeschenCommand = new AsyncRelayCommand(LoeschenAsync, () => KannLoeschen);
        PullRequestErstellenCommand = new AsyncRelayCommand(PullRequestErstellenAsync, () => KannPullRequestErstellen && !_isLoading);
        PullRequestsAktualisierenCommand = new AsyncRelayCommand(PullRequestsAktualisierenAsync, () => CanRefreshPullRequests);
        PullRequestUrlOeffnenCommand = new RelayCommand<string>(
            url => OeffnePullRequestUrl(url),
            url => !string.IsNullOrWhiteSpace(url));
        InfoCliToggleCommand = new RelayCommand(InfoCliToggle);
        InfoViewCommand = new RelayCommand(() => WaehleAnsicht(DetailAnsicht.Info));
        CliViewCommand = new RelayCommand(() => WaehleAnsicht(DetailAnsicht.Cli), () => ShowCliPanel);
        DiffViewCommand = new RelayCommand(() => WaehleAnsicht(DetailAnsicht.Diff), () => ShowDiffPanel);
        DateiViewCommand = new RelayCommand(() => WaehleAnsicht(DetailAnsicht.Dateibrowser), () => ShowFileExplorerPanel);
        PullRequestViewCommand = new RelayCommand(PullRequestAnsichtWaehlen, () => ShowPullRequestPanel);
        ZurueckCommand = new RelayCommand(() => ZurueckAction?.Invoke());
        IssueZuweisenCommand = new AsyncRelayCommand(IssueZuweisenAsync, () => CanAssignIssue && !_isLoading);
        IssueAnlegenCommand = new AsyncRelayCommand(IssueAnlegenAsync, () => CanCreateIssue);
        IssueBrowserOeffnenCommand = new RelayCommand(
            IssueBrowserOeffnen,
            () => CurrentIssueReferenz?.IssueUrl != null);
        AutonomAufgabeInitialisierenCommand = new AsyncRelayCommand(AutonomAufgabeInitialisierenAsync, () => _aufgabe is not null);
        PromptVorlageAuswaehlenCommand = new AsyncRelayCommand<PromptVorlage>(
            PromptVorlageAuswaehlenAsync,
            vorlage => vorlage is not null && KannPromptVorlageSenden);
        SchedulePromptCommand = new AsyncRelayCommand(SchedulePromptAsync, () => KannPromptPlanen);
        OeffneArbeitsverzeichnisCommand = new AsyncRelayCommand(OeffneArbeitsverzeichnisAsync, () => ShowFileExplorerPanel);
        OeffneIdeCommand = new AsyncRelayCommand(OeffneIdeAsync, () => KannIdeOeffnen);
        OeffneIdeAuswahlCommand = new AsyncRelayCommand(OeffneIdeAuswahlAsync, () => KannIdeOeffnen);
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
            var darfCliAnzeigen = Aufgabe?.AusfuehrungsStatus.SollCliAnzeigen(Aufgabe.Status) == true;
            IsCliRunning = darfCliAnzeigen && _kiService.IsRunning(_aufgabeId);

            var session = darfCliAnzeigen ? _kiService.GetPseudoConsoleSession(_aufgabeId) : null;
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

            _protokollLadenCts?.Cancel();
            _protokollLadenCts?.Dispose();
            _protokollLadenCts = new CancellationTokenSource();
            LadeProtokolleAsync(_protokollLadenCts.Token).SafeFireAndForget(_logger, "TaskDetailViewModel.LadeProtokolleAsync");

            await LadePullRequestsAsync(ct);
            await _todoListViewModel.LadenAsync(_aufgabeId, ct);

            await LadeVerfuegbarePluginsAsync(ct);
            await LadePromptVorlagenAsync(ct);
            await AktualisierePullRequestCapabilityAsync(ct);
            await AktualisiereIssueCreateCapabilityAsync(ct);
            await AktualisiereKannIdeAuswaehlenAsync(ct);

            // LadenAsync bindet nur vorhandene laufende Sitzungen wieder an. Ein neuer CLI-Prozess
            // wird ausschließlich über explizite Start-/Neustartaktionen gestartet.
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

    private async Task LadeProtokolleAsync(CancellationToken ct)
    {
        Protokolleintraege.Clear();
        var protokolleintraege = await _protokollService.GetByAufgabeAsync(_aufgabeId, ct);
        foreach (var eintrag in protokolleintraege)
            Protokolleintraege.Add(eintrag);
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

            ZeigeKiPluginAuswahl = VerfuegbareKiPlugins.Count > 1;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "KI-Plugin-Liste konnte nicht geladen werden.");
        }
    }

    private async Task LadePullRequestsAsync(CancellationToken ct)
    {
        PullRequests.Clear();

        var pullRequestService = _serviceProvider.GetService<PullRequestReferenzService>();
        if (pullRequestService is null || _aufgabeId == Guid.Empty)
        {
            OnPropertyChanged(nameof(HasNoPullRequests));
            OnPropertyChanged(nameof(HasPullRequests));
            return;
        }

        var pullRequests = await pullRequestService.GetByAufgabeAsync(_aufgabeId, ct);
        foreach (var pullRequest in pullRequests)
        {
            PullRequests.Add(pullRequest);
        }

        OnPropertyChanged(nameof(HasNoPullRequests));
        OnPropertyChanged(nameof(HasPullRequests));
        OnPropertyChanged(nameof(ShowPullRequestPanel));
    }

    private async Task PullRequestsAktualisierenAsync(CancellationToken ct)
    {
        if (_aufgabeId == Guid.Empty)
            return;

        IsRefreshingPullRequests = true;
        FehlerMeldung = null;

        try
        {
            var monitoringService = _serviceProvider.GetService<PullRequestMonitoringService>();
            if (monitoringService is not null)
            {
                await monitoringService.RefreshAufgabeAsync(_aufgabeId, ct);
            }

            await LadePullRequestsAsync(ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Pull Requests fuer Aufgabe {AufgabeId} konnten nicht aktualisiert werden.", _aufgabeId);
            FehlerMeldung = $"Pull Requests konnten nicht aktualisiert werden: {ex.Message}";
            await LadePullRequestsAsync(CancellationToken.None);
        }
        finally
        {
            IsRefreshingPullRequests = false;
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

        if (_scheduledPromptTargetHours.HasValue || _scheduledPromptTargetMinutes.HasValue)
            return;

        var session = _kiService.GetPseudoConsoleSession(_aufgabeId);
        if (session is null || !_isCliRunning)
            return;

        var prompt = _promptVorlagenPlatzhalterService.Resolve(vorlage.Prompttext, _aufgabe);
        if (string.IsNullOrWhiteSpace(prompt))
            return;

        await session.WritePromptAsync(prompt, ct);

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
            await _aufgabeService.AktivenLaufBeendenAsync(_aufgabeId, ct);
            _dispatcherInvoke(() =>
            {
                IsCliRunning = false;
                AktiverCliName = null;
                if (_aufgabe is not null)
                {
                    _aufgabe.AusfuehrungsStatus = AufgabeAusfuehrungsStatus.Beendet;
                    OnPropertyChanged(nameof(KannCliNeuStarten));
                    OnPropertyChanged(nameof(KannAufgabeAbschliessen));
                    OnPropertyChanged(nameof(ShowCliPanel));
                    WaehleStandardAnsicht();
                }
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
            var pluginPrefix = await _pluginSelectionService.ResolveDevelopmentAutomationPluginWithProjectScopeAsync(
                _aufgabe.KiPluginPrefix,
                _aufgabe.ProjektId,
                ct);

            if (string.IsNullOrEmpty(pluginPrefix))
                return;

            await StartCliAndUpdateStateAsync(pluginPrefix, _aufgabe.LokalerKlonPfad ?? string.Empty, null, ct);
            await LadenAsync(ct);
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

    private async Task AktualisierePullRequestCapabilityAsync(CancellationToken ct)
    {
        _canCreatePullRequest = false;

        if (_aufgabe is null
            || string.IsNullOrWhiteSpace(_aufgabe.BranchName)
            || string.IsNullOrWhiteSpace(_aufgabe.GitRepository?.RepositoryUrl))
        {
            OnPropertyChanged(nameof(KannPullRequestErstellen));
            return;
        }

        var gitPlugin = ResolveGitPluginForAufgabe();
        if (gitPlugin is null)
        {
            OnPropertyChanged(nameof(KannPullRequestErstellen));
            return;
        }

        try
        {
            var capabilities = await gitPlugin.GetGitActionCapabilitiesAsync(_aufgabe.LokalerKlonPfad, ct);
            _canCreatePullRequest = capabilities.CanCreatePullRequest;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Pull-Request-Capability für Aufgabe {AufgabeId} konnte nicht ermittelt werden.", _aufgabeId);
        }
        finally
        {
            OnPropertyChanged(nameof(KannPullRequestErstellen));
        }
    }

    private async Task AktualisiereIssueCreateCapabilityAsync(CancellationToken ct)
    {
        _canCreateIssue = false;

        if (_aufgabe is null
            || _aufgabe.IssueReferenz is not null
            || string.IsNullOrWhiteSpace(_aufgabe.GitRepository?.RepositoryUrl))
        {
            OnPropertyChanged(nameof(CanCreateIssue));
            OnPropertyChanged(nameof(ShowIssueGroup));
            return;
        }

        var gitPlugin = ResolveGitPluginForAufgabe();
        if (gitPlugin is not IIssueCreateProvider issueCreateProvider)
        {
            OnPropertyChanged(nameof(CanCreateIssue));
            OnPropertyChanged(nameof(ShowIssueGroup));
            return;
        }

        try
        {
            _canCreateIssue = await issueCreateProvider.CanCreateIssueAsync(GetRepositoryIdentifier(_aufgabe), ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Issue-Anlage-Capability für Aufgabe {AufgabeId} konnte nicht ermittelt werden.", _aufgabeId);
        }
        finally
        {
            OnPropertyChanged(nameof(CanCreateIssue));
            OnPropertyChanged(nameof(ShowIssueGroup));
        }
    }

    private IGitPlugin? ResolveGitPluginForAufgabe()
    {
        var gitPlugins = _pluginManager.GetSourceCodeManagementPlugins().OfType<IGitPlugin>().ToList();
        if (gitPlugins.Count == 0)
            return null;

        var pluginTyp = _aufgabe?.GitRepository?.PluginTyp;
        if (!string.IsNullOrWhiteSpace(pluginTyp))
        {
            var matchingPlugin = gitPlugins.FirstOrDefault(p =>
                string.Equals(p.PluginPrefix, pluginTyp, StringComparison.OrdinalIgnoreCase));
            if (matchingPlugin is not null)
                return matchingPlugin;
        }

        return gitPlugins.FirstOrDefault();
    }

    private static string GetRepositoryIdentifier(Aufgabe aufgabe)
        => !string.IsNullOrWhiteSpace(aufgabe.GitRepository?.RepositoryUrl)
            ? aufgabe.GitRepository.RepositoryUrl
            : aufgabe.GitRepository?.RepositoryName ?? string.Empty;

    private async Task AufgabeAbschliessenAsync(CancellationToken ct)
    {
        if (_aufgabeId == Guid.Empty)
            return;

        try
        {
            if (!await _aufgabeService.CanCompleteTaskAsync(_aufgabeId, ct))
            {
                FehlerMeldung = string.Format(AufgabeService.OffeneTodosFehlermeldungFormat, _todoListViewModel.OffeneTodoCount);
                return;
            }

            _promptZeitVersandService.CancelScheduledPrompt(_aufgabeId);
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

    private async Task PullRequestErstellenAsync(CancellationToken ct)
    {
        if (_aufgabeId == Guid.Empty || _aufgabe is null)
            return;

        IsLoading = true;
        FehlerMeldung = null;

        try
        {
            var gitOrchestrationService = _serviceProvider.GetRequiredService<GitOrchestrationService>();
            var pullRequest = await gitOrchestrationService.PullRequestErstellenAsync(
                _aufgabeId,
                title: _aufgabe.Titel,
                body: _aufgabe.AnforderungsBeschreibung ?? string.Empty,
                ct: ct);

            await LadenAsync(ct);

            if (!string.IsNullOrWhiteSpace(pullRequest.Url))
                OeffnePullRequestUrl(pullRequest.Url);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Erstellen des Pull Requests für Aufgabe {AufgabeId}.", _aufgabeId);
            SetFehler(ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void OeffnePullRequestUrl(string? url)
    {
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
            _logger.LogWarning(ex, "Pull-Request-URL {PullRequestUrl} konnte nicht geöffnet werden.", url);
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

    private async Task AutonomAufgabeInitialisierenAsync(CancellationToken ct)
    {
        if (_aufgabe is null)
        {
            return;
        }

        var ergebnis = await _autonomAufgabeStartService.StarteAsync(_aufgabe, ct);
        if (ergebnis is null)
        {
            return;
        }

        if (ergebnis.AktualisierteAufgabe is not null)
        {
            Aufgabe = ergebnis.AktualisierteAufgabe;
        }

        if (ergebnis.FehlerMeldung is not null)
        {
            FehlerMeldung = ergebnis.FehlerMeldung;
        }
    }

    private async Task IssueAnlegenAsync(CancellationToken ct)
    {
        if (_aufgabe == null)
            return;

        var aktuellerStand = await _aufgabeService.GetDetailAsync(_aufgabeId, ct);
        if (aktuellerStand?.IssueReferenz is not null)
        {
            Aufgabe = aktuellerStand;
            FehlerMeldung = "Der Aufgabe ist bereits ein Issue zugeordnet.";
            return;
        }

        var gitPlugin = ResolveGitPluginForAufgabe();
        if (gitPlugin is not IIssueCreateProvider issueCreateProvider)
        {
            FehlerMeldung = "Der Repository-Provider unterstützt die Issue-Anlage nicht.";
            return;
        }

        var repositoryId = GetRepositoryIdentifier(_aufgabe);
        if (!await issueCreateProvider.CanCreateIssueAsync(repositoryId, ct))
        {
            FehlerMeldung = "Der Repository-Provider unterstützt die Issue-Anlage für dieses Repository nicht.";
            return;
        }

        var dialogVm = _serviceProvider.GetRequiredService<IssueCreateDialogViewModel>();
        var preferredKiPluginPrefix = await ResolvePreferredKiPluginPrefixAsync(ct);
        await dialogVm.InitializeAsync(
            issueCreateProvider,
            gitPlugin as IIssueTemplateProvider,
            repositoryId,
            _aufgabe.Titel,
            _aufgabe.AnforderungsBeschreibung,
            preferredKiPluginPrefix,
            () => _aufgabe?.IssueReferenz is not null,
            async token => (await _aufgabeService.GetDetailAsync(_aufgabeId, token))?.IssueReferenz is not null,
            ct);

        var dialogResult = await _dialogService.ShowIssueCreateDialogAsync(dialogVm, ct);
        if (dialogResult is null)
        {
            return;
        }

        var createdIssue = dialogResult.Issue;

        try
        {
            var standVorZuordnung = await _aufgabeService.GetDetailAsync(_aufgabeId, ct);
            if (standVorZuordnung?.IssueReferenz is not null)
            {
                Aufgabe = standVorZuordnung;
                FehlerMeldung = $"Issue wurde extern erstellt ({DescribeIssue(createdIssue)}), aber nicht lokal zugeordnet, weil der Aufgabe inzwischen ein Issue zugeordnet ist.";
                return;
            }

            var neueAnforderungsBeschreibung = dialogResult.UpdateTaskDescription
                ? !string.IsNullOrWhiteSpace(createdIssue.Body)
                    ? createdIssue.Body
                    : dialogResult.LocalBody ?? string.Empty
                : null;

            if (!await _aufgabeService.TryAssignIssueReferenzAndUpdateDescriptionIfNoneAsync(
                    _aufgabeId,
                    createdIssue,
                    dialogResult.UpdateTaskDescription,
                    neueAnforderungsBeschreibung,
                    ct))
            {
                var standNachFehlgeschlagenerZuordnung = await _aufgabeService.GetDetailAsync(_aufgabeId, ct);
                if (standNachFehlgeschlagenerZuordnung is not null)
                {
                    Aufgabe = standNachFehlgeschlagenerZuordnung;
                }

                FehlerMeldung = $"Issue wurde extern erstellt ({DescribeIssue(createdIssue)}), aber nicht lokal zugeordnet, weil der Aufgabe inzwischen ein Issue zugeordnet ist.";
                return;
            }

            await LadenAsync(ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Externes Issue wurde erstellt, lokale Zuordnung oder Aufgabenbeschreibung für Aufgabe {AufgabeId} ist fehlgeschlagen.", _aufgabeId);
            FehlerMeldung = $"Issue wurde extern erstellt ({DescribeIssue(createdIssue)}), die lokale Zuordnung oder Aufgabenbeschreibung konnte aber nicht gespeichert werden: {ex.Message}";
        }
    }

    private async Task<string?> ResolvePreferredKiPluginPrefixAsync(CancellationToken ct)
    {
        if (_aufgabe is null)
        {
            return null;
        }

        try
        {
            return await _pluginSelectionService.ResolveDevelopmentAutomationPluginWithProjectScopeAsync(
                _aufgabe.KiPluginPrefix,
                _aufgabe.ProjektId,
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Standard-KI-Provider für Issue-Anlage konnte nicht ermittelt werden.");
            return _selectedKiPluginPrefix;
        }
    }

    private static string DescribeIssue(Issue issue)
    {
        if (!string.IsNullOrWhiteSpace(issue.IssueUrl))
        {
            return issue.IssueUrl;
        }

        return issue.Nummer > 0 ? $"#{issue.Nummer}" : issue.Titel;
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
        var standardAnsicht = ShowCliPanel
            ? DetailAnsicht.Cli
            : AufgabeStatus switch
            {
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
        if (ansicht == DetailAnsicht.Dateibrowser && !ShowFileExplorerPanel)
            ansicht = DetailAnsicht.Info;
        if (ansicht == DetailAnsicht.PullRequests && !ShowPullRequestPanel)
            ansicht = DetailAnsicht.Info;

        if (_ausgewaehlteAnsicht == ansicht)
            return;

        _ausgewaehlteAnsicht = ansicht;
        OnPropertyChanged(nameof(IsInfoViewVisible));
        OnPropertyChanged(nameof(IsInfoViewSelected));
        OnPropertyChanged(nameof(IsCliViewSelected));
        OnPropertyChanged(nameof(IsDiffViewSelected));
        OnPropertyChanged(nameof(IsFileExplorerViewSelected));
        OnPropertyChanged(nameof(IsPullRequestViewSelected));
        OnPropertyChanged(nameof(IsTodoViewSelected));
        OnPropertyChanged(nameof(ShowInfoPanel));
    }

    private void PullRequestAnsichtWaehlen()
    {
        WaehleAnsicht(DetailAnsicht.PullRequests);
        PullRequestsAktualisierenCommand.Execute(null);
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
                if (_aufgabe is not null)
                {
                    _aufgabe.AusfuehrungsStatus = status == CliProcessStatus.Gestartet
                        ? AufgabeAusfuehrungsStatus.Aktiv
                        : AufgabeAusfuehrungsStatus.Beendet;
                    OnPropertyChanged(nameof(KannCliNeuStarten));
                    OnPropertyChanged(nameof(KannAufgabeAbschliessen));
                    OnPropertyChanged(nameof(ShowCliPanel));
                    WaehleStandardAnsicht();
                }

                if (status != CliProcessStatus.Gestartet)
                {
                    AttachCliStatusSession(null);
                    AktiverCliName = null;
                    CliStatusText = status == CliProcessStatus.Fehler
                        ? "CLI-Status: Fehler"
                        : "CLI inaktiv";
                    _promptZeitVersandService.CancelScheduledPrompt(aufgabeId);
                    ScheduledPromptStatus = null;
                    ScheduledPromptTimeDisplay = null;
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
        _promptZeitVersandService.PromptSent -= OnPromptSent;
        _promptZeitVersandService.CancelScheduledPrompt(_aufgabeId);
        AttachCliStatusSession(null);
        _ladenCts?.Cancel();
        _ladenCts?.Dispose();
        _ladenCts = null;
        _protokollLadenCts?.Cancel();
        _protokollLadenCts?.Dispose();
        _protokollLadenCts = null;
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

            if (_aufgabe.AusfuehrungsStatus == AufgabeAusfuehrungsStatus.Beendet)
            {
                await StartCliAndUpdateStateAsync(pluginPrefix, _aufgabe.LokalerKlonPfad ?? string.Empty, _optionalCliParameters, ct);
            }
            else
            {
                CliStatusText = RepositoryPreparationStatusText;

                await _entwicklungsprozessService.ProzessStartenUndCliStartenAsync(
                    _aufgabeId,
                    repositoryUrl,
                    null,
                    pluginPrefix,
                    ct);
            }

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
            AttachCliStatusSession(null);
            throw;
        }
        catch (Exception ex)
        {
            AktiverCliName = null;
            AttachCliStatusSession(null);
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

    private async Task StartCliAndUpdateStateAsync(string pluginPrefix, string lokalerKlonPfad, string? optionalParameters, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(lokalerKlonPfad))
        {
            throw new InvalidOperationException($"Aufgabe {_aufgabeId} hat keinen lokalen Klonpfad.");
        }

        IsCliRunning = true;
        AktiverCliName = ResolveKiPluginName(pluginPrefix) ?? pluginPrefix;
        try
        {
            await _entwicklungsprozessService.CliNeustartenAsync(_aufgabeId, pluginPrefix, optionalParameters, ct);

            SelectedKiPluginPrefix = pluginPrefix;
            if (!_kiService.IsRunning(_aufgabeId))
            {
                await _aufgabeService.AktivenLaufBeendenAsync(_aufgabeId, ct);
                IsCliRunning = false;
                AktiverCliName = null;
                return;
            }

            await _aufgabeService.AusfuehrungAktivSetzenAsync(_aufgabeId, ct);

            var session = _kiService.GetPseudoConsoleSession(_aufgabeId);
            if (session != null)
            {
                AttachCliStatusSession(session);
                PseudoConsoleSessionGestartet?.Invoke(session);
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
        if (VerfuegbareKiPlugins.Count == 1)
        {
            var einzigesPluginPrefix = VerfuegbareKiPlugins[0];
            await _aufgabeService.UpdateAsync(_aufgabeId, aufgabe.Titel, aufgabe.AnforderungsBeschreibung, einzigesPluginPrefix, ct);
            return einzigesPluginPrefix;
        }

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

    private async Task SchedulePromptAsync(CancellationToken ct)
    {
        if (_selectedPromptVorlage is null || string.IsNullOrWhiteSpace(_selectedPromptVorlage.Prompttext))
            return;

        if (_scheduledPromptTargetHours is < 0 or > 23)
        {
            FehlerMeldung = "Ungültige Stunde (0–23)";
            return;
        }

        if (_scheduledPromptTargetMinutes is < 0 or > 59)
        {
            FehlerMeldung = "Ungültige Minute (0–59)";
            return;
        }

        if (!_scheduledPromptTargetHours.HasValue && !_scheduledPromptTargetMinutes.HasValue)
            return;

        FehlerMeldung = null;

        var jetzt = _timeProvider.GetLocalNow();
        var stunde = _scheduledPromptTargetHours ?? 0;
        var minute = _scheduledPromptTargetMinutes ?? 0;
        var targetTime = new DateTimeOffset(new DateTime(jetzt.Year, jetzt.Month, jetzt.Day, stunde, minute, 0), jetzt.Offset);

        var prompt = _promptVorlagenPlatzhalterService.Resolve(_selectedPromptVorlage.Prompttext, _aufgabe);
        if (string.IsNullOrWhiteSpace(prompt))
            return;

        await _promptZeitVersandService.SchedulePromptAsync(_aufgabeId, prompt, targetTime);

        // targetTime kann bereits erreicht/vergangen sein (z. B. Uhrzeit des heutigen Tages liegt vor "jetzt").
        // Der Service versendet in diesem Fall sofort statt zu puffern — dann existiert kein Warteschlangeneintrag
        // mehr, und der "Wartestellung"-Status darf nicht gesetzt werden, da er dem Nutzer sonst einen nicht
        // (mehr) gepufferten Prompt als wartend anzeigen würde.
        if (_promptZeitVersandService.GetScheduledPromptStatus(_aufgabeId) is not null)
        {
            ScheduledPromptStatus = "Prompt in Wartestellung";
            ScheduledPromptTimeDisplay = targetTime.ToString("HH:mm");
        }

        ScheduledPromptTargetHours = null;
        ScheduledPromptTargetMinutes = null;
        SelectedPromptVorlage = null;
    }

    private void OnPromptSent(Guid aufgabeId)
    {
        if (aufgabeId != _aufgabeId)
            return;

        _dispatcherInvoke(() =>
        {
            ScheduledPromptStatus = null;
            ScheduledPromptTimeDisplay = null;
            WaehleAnsicht(DetailAnsicht.Cli);
        });
    }

    private Task<string> ErmittleEffektivesArbeitsverzeichnisAsync(string lokalerKlonPfad, CancellationToken ct)
    {
        var startConfig = _aufgabe?.GitRepository?.StartKonfiguration;

        return WorkingDirectoryResolver.DetermineEffectiveWorkingDirectoryAsync(
            lokalerKlonPfad,
            startConfig,
            gitPlugin: null,
            ct: ct);
    }

    /// <summary>
    /// Löst das zuständige IDE-Plugin über <see cref="PluginSelectionService.ResolveIdePluginAsync"/> auf und
    /// liefert dessen Einstiegspunkte über <see cref="IIdePlugin.FindEntryPointsAsync"/> für das übergebene,
    /// bereits ermittelte effektive Arbeitsverzeichnis. Gemeinsam genutzt von <see cref="OeffneIdeInternAsync"/>
    /// (öffnet anschließend einen Einstiegspunkt) und <see cref="AktualisiereKannIdeAuswaehlenAsync"/> (ermittelt
    /// nur die Anzahl, ohne zu öffnen).
    /// </summary>
    /// <param name="effectiveWorkdir">Das bereits über <see cref="ErmittleEffektivesArbeitsverzeichnisAsync"/> aufgelöste effektive Arbeitsverzeichnis.</param>
    /// <param name="ct">Abbruchtoken.</param>
    /// <returns>Das aufgelöste IDE-Plugin sowie dessen gefundene Einstiegspunkte.</returns>
    private async Task<(IIdePlugin Plugin, IReadOnlyList<IdeEntryPoint> EntryPoints)> ErmittleIdeEntryPointsAsync(string effectiveWorkdir, CancellationToken ct)
    {
        var plugin = await _pluginSelectionService.ResolveIdePluginAsync(effectiveWorkdir, ct);
        var entryPoints = await plugin.FindEntryPointsAsync(effectiveWorkdir, ct);
        return (plugin, entryPoints);
    }

    /// <summary>
    /// Löst für das übergebene, bereits ermittelte effektive Arbeitsverzeichnis über
    /// <see cref="PluginSelectionService.ResolveAlleKompatiblenIdePluginsAsync"/> ALLE aktivierten, zum
    /// Repository <c>Explicit</c>- oder <c>Fallback</c>-kompatiblen IDE-Plugins auf (statt nur eines einzelnen
    /// wie <see cref="ErmittleIdeEntryPointsAsync"/>). Ruft anschließend <see cref="IIdePlugin.FindEntryPointsAsync"/>
    /// auf jedem dieser Plugins auf und aggregiert die Ergebnisse zu einer Liste von
    /// <c>(Plugin, EntryPoint)</c>-Tupeln (Plugin-Reihenfolge sowie Einstiegspunkt-Reihenfolge je Plugin bleiben
    /// erhalten). Schlägt die Einstiegspunkt-Ermittlung für ein einzelnes Plugin fehl, wird der Fehler geloggt
    /// und mit den übrigen Plugins fortgefahren, statt die gesamte Aggregation abzubrechen. Genutzt vom
    /// Dropdown-Button-Pfad in <see cref="OeffneIdeInternAsync"/> sowie von
    /// <see cref="AktualisiereKannIdeAuswaehlenAsync"/>.
    /// </summary>
    /// <param name="effectiveWorkdir">Das bereits über <see cref="ErmittleEffektivesArbeitsverzeichnisAsync"/> aufgelöste effektive Arbeitsverzeichnis.</param>
    /// <param name="ct">Abbruchtoken.</param>
    /// <returns>Die aggregierte Liste der (Plugin, EntryPoint)-Tupel aller kompatiblen Plugins, die erfolgreich Einstiegspunkte geliefert haben.</returns>
    private async Task<IReadOnlyList<(IIdePlugin Plugin, IdeEntryPoint EntryPoint)>> ErmittleAggregierteIdeEinstiegspunkteAsync(string effectiveWorkdir, CancellationToken ct)
    {
        var plugins = await _pluginSelectionService.ResolveAlleKompatiblenIdePluginsAsync(effectiveWorkdir, ct);

        var eintraege = new List<(IIdePlugin Plugin, IdeEntryPoint EntryPoint)>();
        foreach (var plugin in plugins)
        {
            try
            {
                var entryPoints = await plugin.FindEntryPointsAsync(effectiveWorkdir, ct);
                eintraege.AddRange(entryPoints.Select(entryPoint => (plugin, entryPoint)));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Einstiegspunkte für IDE-Plugin {PluginName} konnten nicht ermittelt werden.", plugin.PluginName);
            }
        }

        return eintraege;
    }

    /// <summary>
    /// Formatiert einen Einstiegspunkt für die Anzeige im Auswahl-Dialog plugin-qualifiziert:
    /// <c>"{PluginName}: {DisplayName ?? Dateiname}"</c>, außer die ermittelte Bezeichnung ist bereits
    /// identisch mit dem <see cref="IPlugin.PluginName"/> (z. B. bei <c>VisualStudioCodeIdePlugin</c>,
    /// dessen einziger Einstiegspunkt bereits <c>DisplayName == PluginName</c> liefert) — dann wird nur der
    /// Plugin-Name angezeigt, um ein Doppel-Label zu vermeiden.
    /// </summary>
    /// <param name="plugin">Das Plugin, dem der Einstiegspunkt zugeordnet ist.</param>
    /// <param name="entryPoint">Der zu formatierende Einstiegspunkt.</param>
    /// <returns>Der formatierte Anzeige-String, z. B. „Visual Studio: MyProject.sln" oder „Visual Studio Code".</returns>
    private static string FormatiereAnzeigeWert(IIdePlugin plugin, IdeEntryPoint entryPoint)
    {
        var bezeichnung = entryPoint.DisplayName ?? Path.GetFileName(entryPoint.Path);
        return string.Equals(bezeichnung, plugin.PluginName, StringComparison.Ordinal)
            ? plugin.PluginName
            : $"{plugin.PluginName}: {bezeichnung}";
    }

    /// <summary>
    /// Berechnet <see cref="KannIdeAuswaehlen"/> einmalig am Ende von <see cref="LadenAsync"/>, damit der
    /// Dropdown-Button des <see cref="Controls.RibbonSplitButton"/> bereits beim ersten Anzeigen der View
    /// korrekt sichtbar/unsichtbar ist (ohne einen Einstiegspunkt zu öffnen). Ermittlungsfehler, fehlendes
    /// Plugin oder fehlendes Arbeitsverzeichnis werden hier nicht als <see cref="FehlerMeldung"/> angezeigt,
    /// sondern führen lediglich zu <c>KannIdeAuswaehlen = false</c>.
    /// </summary>
    /// <param name="ct">Abbruchtoken.</param>
    private async Task AktualisiereKannIdeAuswaehlenAsync(CancellationToken ct)
    {
        if (_aufgabe?.LokalerKlonPfad is not { } lokalerKlonPfad)
        {
            KannIdeAuswaehlen = false;
            return;
        }

        try
        {
            var effectiveWorkdir = await ErmittleEffektivesArbeitsverzeichnisAsync(lokalerKlonPfad, ct);
            var eintraege = await ErmittleAggregierteIdeEinstiegspunkteAsync(effectiveWorkdir, ct);
            KannIdeAuswaehlen = BerechneKannIdeAuswaehlen(eintraege.Count);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Einstiegspunkte für Aufgabe {AufgabeId} konnten beim Laden nicht ermittelt werden.", _aufgabeId);
            KannIdeAuswaehlen = false;
        }
    }

    private async Task OeffneArbeitsverzeichnisAsync(CancellationToken ct)
    {
        if (_aufgabe?.LokalerKlonPfad is not { } lokalerKlonPfad)
            return;

        FehlerMeldung = null;

        try
        {
            var effectiveWorkdir = await ErmittleEffektivesArbeitsverzeichnisAsync(lokalerKlonPfad, ct);

            _arbeitsverzeichnisOeffnenService.Oeffne(effectiveWorkdir);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Öffnen des Arbeitsverzeichnisses {LokalerKlonPfad}.", lokalerKlonPfad);
            FehlerMeldung = $"Arbeitsverzeichnis konnte nicht geöffnet werden: {ex.Message}";
        }
    }

    /// <summary>
    /// Öffnet das effektive Arbeitsverzeichnis in der zuständigen IDE (Haupt-Button des Split-Buttons):
    /// löst über <see cref="OeffneIdeInternAsync"/> Plugin und Einstiegspunkte auf und öffnet bei mehreren
    /// gefundenen Einstiegspunkten direkt den ersten (Fallback-Verhalten), ohne Auswahl-Dialog. Die gezielte
    /// Auswahl übernimmt <see cref="OeffneIdeAuswahlAsync"/> über den Dropdown-Button.
    /// </summary>
    /// <param name="ct">Abbruchtoken.</param>
    private Task OeffneIdeAsync(CancellationToken ct) => OeffneIdeInternAsync(null, ct);

    /// <summary>
    /// Öffnet das effektive Arbeitsverzeichnis in der zuständigen IDE (Dropdown-Button des Split-Buttons):
    /// löst über <see cref="OeffneIdeInternAsync"/> Plugin und Einstiegspunkte auf und erzwingt bei mehreren
    /// gefundenen Einstiegspunkten die Auswahl über den <see cref="WaehleEntryPointAsync"/>-Callback, statt
    /// automatisch den ersten zu öffnen.
    /// </summary>
    /// <param name="ct">Abbruchtoken.</param>
    private Task OeffneIdeAuswahlAsync(CancellationToken ct) => OeffneIdeInternAsync(WaehleEntryPointAsync, ct);

    /// <summary>
    /// Gemeinsame Implementierung für <see cref="OeffneIdeAsync"/> und <see cref="OeffneIdeAuswahlAsync"/>:
    /// ermittelt das effektive Arbeitsverzeichnis, löst das zuständige IDE-Plugin über
    /// <see cref="PluginSelectionService.ResolveIdePluginAsync"/> auf und ermittelt dessen Einstiegspunkte
    /// über <see cref="IIdePlugin.FindEntryPointsAsync"/> — jeweils genau einmal pro Aufruf — und öffnet
    /// anschließend über <see cref="IIdePlugin.OpenEntryPointAsync"/> direkt auf Basis desselben Ergebnisses,
    /// mit dem auch <see cref="KannIdeAuswaehlen"/> aktualisiert wird. Existiert genau ein Einstiegspunkt,
    /// wird dieser direkt geöffnet. Existieren mehrere und ist <paramref name="waehleEntryPointAsync"/>
    /// gesetzt, wird der Callback zur Auswahl aufgerufen; liefert er <c>null</c> (Abbruch), wird nichts
    /// geöffnet. Existieren mehrere ohne Callback, wird der erste geöffnet (Fallback).
    /// </summary>
    /// <param name="waehleEntryPointAsync">
    /// Optionaler Callback zur Auswahl eines Einstiegspunkts bei mehreren Treffern, oder <c>null</c> für das
    /// Fallback-Verhalten des Haupt-Buttons.
    /// </param>
    /// <param name="ct">Abbruchtoken.</param>
    /// <returns>Ein Task, der abgeschlossen ist, sobald die IDE geöffnet wurde (oder der Vorgang abgebrochen bzw. mit Fehleranzeige beendet wurde).</returns>
    private async Task OeffneIdeInternAsync(
        Func<IReadOnlyList<(IIdePlugin Plugin, IdeEntryPoint EntryPoint)>, CancellationToken, Task<(IIdePlugin Plugin, IdeEntryPoint EntryPoint)?>>? waehleEntryPointAsync,
        CancellationToken ct)
    {
        FehlerMeldung = null;

        if (_aufgabe?.LokalerKlonPfad is not { } lokalerKlonPfad)
            return;

        try
        {
            var effectiveWorkdir = await ErmittleEffektivesArbeitsverzeichnisAsync(lokalerKlonPfad, ct);

            if (waehleEntryPointAsync is null)
            {
                // Haupt-Button: bestehender Single-Plugin-Pfad bleibt unverändert.
                var (plugin, entryPoints) = await ErmittleIdeEntryPointsAsync(effectiveWorkdir, ct);

                // Zusätzlich: KannIdeAuswaehlen aus der aggregierten Ermittlung über alle kompatiblen Plugins aktualisieren.
                var aggregierteEintraege = await ErmittleAggregierteIdeEinstiegspunkteAsync(effectiveWorkdir, ct);
                KannIdeAuswaehlen = BerechneKannIdeAuswaehlen(aggregierteEintraege.Count);

                if (entryPoints.Count == 0)
                    throw new FileNotFoundException($"Keine Einstiegspunkte im Repository gefunden: {effectiveWorkdir}");

                await plugin.OpenEntryPointAsync(entryPoints[0], ct);
                return;
            }

            // Dropdown-Button: ausschließlich die aggregierte Ermittlung über alle kompatiblen Plugins.
            var eintraege = await ErmittleAggregierteIdeEinstiegspunkteAsync(effectiveWorkdir, ct);
            KannIdeAuswaehlen = BerechneKannIdeAuswaehlen(eintraege.Count);

            if (eintraege.Count == 0)
                throw new FileNotFoundException($"Keine Einstiegspunkte im Repository gefunden: {effectiveWorkdir}");

            if (eintraege.Count == 1)
            {
                await eintraege[0].Plugin.OpenEntryPointAsync(eintraege[0].EntryPoint, ct);
                return;
            }

            var gewaehlt = await waehleEntryPointAsync(eintraege, ct);
            if (gewaehlt is null)
                return;

            await gewaehlt.Value.Plugin.OpenEntryPointAsync(gewaehlt.Value.EntryPoint, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Öffnen der IDE für Aufgabe {AufgabeId}.", _aufgabeId);
            FehlerMeldung = $"IDE konnte nicht geöffnet werden: {ex.Message}";
        }
    }

    /// <summary>
    /// Callback für <see cref="OeffneIdeInternAsync"/> (Dropdown-Button): Formatiert die aggregierten
    /// (Plugin, EntryPoint)-Tupel via <see cref="FormatiereAnzeigeWert"/> plugin-qualifiziert, zeigt sie im
    /// Auswahl-Dialog an und liefert das zum gewählten Anzeigewert gehörende Tupel über den Listenindex
    /// zurück (nicht über Stringgleichheit, da mehrere Einträge theoretisch denselben Anzeige-String liefern
    /// könnten), oder <c>null</c> bei Abbruch durch den Anwender.
    /// </summary>
    /// <param name="eintraege">Die zur Auswahl stehenden (Plugin, EntryPoint)-Tupel aus allen kompatiblen IDE-Plugins.</param>
    /// <param name="ct">Abbruchtoken.</param>
    /// <returns>Das gewählte (Plugin, EntryPoint)-Tupel, oder <c>null</c> bei Abbruch.</returns>
    private async Task<(IIdePlugin Plugin, IdeEntryPoint EntryPoint)?> WaehleEntryPointAsync(IReadOnlyList<(IIdePlugin Plugin, IdeEntryPoint EntryPoint)> eintraege, CancellationToken ct)
    {
        var anzeigeWerte = eintraege.Select(e => FormatiereAnzeigeWert(e.Plugin, e.EntryPoint)).ToList();
        var gewaehlterWert = await _dialogService.ShowSolutionSelectionDialogAsync(anzeigeWerte, ct);
        if (gewaehlterWert is null)
            return null;

        var index = anzeigeWerte.IndexOf(gewaehlterWert);
        return index >= 0 ? eintraege[index] : null;
    }

    /// <summary>
    /// Berechnet, ob mehr als ein Einstiegspunkt vorhanden ist und somit der Dropdown-Button des
    /// <see cref="Controls.RibbonSplitButton"/> zur Auswahl angeboten werden soll.
    /// </summary>
    /// <param name="entryPointCount">Die Gesamtanzahl der ermittelten Einstiegspunkte.</param>
    /// <returns><c>true</c>, wenn mehr als ein Einstiegspunkt vorhanden ist, sonst <c>false</c>.</returns>
    private static bool BerechneKannIdeAuswaehlen(int entryPointCount) => entryPointCount >= 2;
}
