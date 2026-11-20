using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
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

    /// <summary>Sichtbarer Hinweis im <see cref="CliStatusText"/>, dass die Session im
    /// Pipe-Fallback (kein Pseudo-Terminal) läuft und damit nicht dem voll interaktiven
    /// Modus entspricht.</summary>
    private const string EingeschraenkterModeNotice = " (eingeschränkter Modus – kein Pseudo-Terminal)";

    private enum DetailView
    {
        Info,
        Cli,
        Diff,
        FileBrowser,
        PullRequests,
        Todos
    }

    private readonly WorkItemService _workItemService;
    private readonly LogService _logService;
    private readonly AiExecutionService _aiService;
    private readonly DevelopmentProcessService _developmentprocessService;
    private readonly PluginSelectionService _pluginSelectionService;
    private readonly PromptTemplateService _promptTemplatesService;
    private readonly PromptTemplatePlaceholderService _promptTemplatesPlaceholderService;
    private readonly PromptScheduleDispatchService _promptScheduleDispatchService;
    private readonly IDialogService _dialogService;
    private readonly IPluginManager _pluginManager;
    private readonly IServiceProvider _serviceProvider;
    private readonly FileExplorerViewModel _fileExplorerViewModel;
    private readonly TodoListViewModel _todoListViewModel;
    private readonly WorkingDirectoryOpenService _workingDirectoryOpenService;
    private readonly ILogger<TaskDetailViewModel> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly ICliRawExportService _cliRawExportService;
    private readonly ICliReplayExportService _cliReplayExportService;
    private readonly WorkItemRunDataChangedNotifier? _runDataChangedNotifier;
    private readonly Action<Action> _dispatcherInvoke;

    private Guid _workItemId;
    private WorkItem? _workItem;
    private bool _isLoading;
    private string? _errorMessage;
    private bool _isCliRunning;
    private string? _selectedAiPluginPrefix;
    private bool _showAiPluginSelection;
    private string? _optionalCliParameter;
    private CancellationTokenSource? _loadCts;
    private CancellationTokenSource? _logLoadCts;
    private string? _editTitle;
    private string? _editRequirementDescription;
    private bool _disposed;
    private string _cliStatusText = "CLI inaktiv";
    private string? _activeCliName;
    private ITerminalSession? _cliStatusSession;
    private PromptTemplate? _selectedPromptTemplate;
    private DetailView _selectedView = DetailView.Info;
    private int? _scheduledPromptTargetHours;
    private int? _scheduledPromptTargetMinutes;
    private string? _scheduledPromptStatus;
    private string? _scheduledPromptTimeDisplay;
    private bool _showFileExplorerPanel;
    private bool _canCreatePullRequest;
    private bool _canCreateIssue;
    private bool _isRefreshingPullRequests;
    private bool _canIdeSelect;

    /// <summary>Wird aufgerufen, wenn der Nutzer zur vorherigen Ansicht zurückkehren möchte.</summary>
    public Action? BackAction { get; set; }

    /// <summary>Wird nach dem Löschen einer Aufgabe aufgerufen, damit die übergeordnete Ansicht die Liste aktualisiert.</summary>
    public Func<Task>? RefreshWorkItemListCallback { get; set; }

    /// <summary>Wird aufgerufen, wenn sich der Titel der angezeigten Detailaufgabe ändert.</summary>
    public Action<string?>? DetailTitleChangeAction { get; set; }

    /// <summary>Die ID der angezeigten Aufgabe.</summary>
    public Guid WorkItemId
    {
        get => _workItemId;
        set
        {
            if (SetProperty(ref _workItemId, value))
            {
                _loadCts?.Cancel();
                _loadCts?.Dispose();
                _loadCts = new CancellationTokenSource();
                OnPropertyChanged(nameof(ExportCanCliRaw));
                LoadAsync(_loadCts.Token).SafeFireAndForget(_logger, "TaskDetailViewModel.LadenAsync");
            }
        }
    }

    /// <summary>Die geladene Aufgabe.</summary>
    public WorkItem? WorkItem
    {
        get => _workItem;
        private set
        {
            SetProperty(ref _workItem, value);
            _showFileExplorerPanel = !string.IsNullOrEmpty(value?.LocalClonePath) && Directory.Exists(value.LocalClonePath);
            OnPropertyChanged(nameof(WorkItemTitle));
            OnPropertyChanged(nameof(WorkItemStatus));
            OnPropertyChanged(nameof(WorkItemBranchName));
            OnPropertyChanged(nameof(StopCanCli));
            OnPropertyChanged(nameof(StartCanCliNew));
            OnPropertyChanged(nameof(CompleteCanWorkItem));
            OnPropertyChanged(nameof(ShowEditPanel));
            OnPropertyChanged(nameof(ShowCliPanel));
            OnPropertyChanged(nameof(ShowDiffPanel));
            OnPropertyChanged(nameof(ShowFileExplorerPanel));
            OnPropertyChanged(nameof(ShowPullRequestPanel));
            OnPropertyChanged(nameof(OpenCanIde));
            OnPropertyChanged(nameof(SaveCan));
            OnPropertyChanged(nameof(DeleteCan));
            OnPropertyChanged(nameof(CreateCanPullRequest));
            OnPropertyChanged(nameof(CanAssignIssue));
            OnPropertyChanged(nameof(CanCreateIssue));
            OnPropertyChanged(nameof(ShowIssueGroup));
            OnPropertyChanged(nameof(CurrentIssueReference));
            OnPropertyChanged(nameof(ShowInfoPanel));
            OnPropertyChanged(nameof(IsPullRequestViewSelected));
            OnPropertyChanged(nameof(ExportCanCliRaw));
            OnPropertyChanged(nameof(PausedUntilUtc));
            OnPropertyChanged(nameof(IsPaused));
            OnPropertyChanged(nameof(PauseDisplayText));
            OnPropertyChanged(nameof(PauseCan));
            OnPropertyChanged(nameof(SendCanPromptTemplate));
            OnPropertyChanged(nameof(CanPromptPlan));
            SelectDefaultView();
            DetailTitleChangeAction?.Invoke(value?.Title);

            _fileExplorerViewModel.InitializeAsync(value?.LocalClonePath, CancellationToken.None)
                .SafeFireAndForget(_logger, "TaskDetailViewModel.FileExplorer.InitialisierenAsync");

            CommandManager.InvalidateRequerySuggested();
        }
    }

    /// <summary>Titel der Aufgabe.</summary>
    /// <value>Der Titel der Aufgabe, oder ein Platzhaltertext während des Ladens.</value>
    public string WorkItemTitle => _workItem?.Title ?? "(wird geladen…)";

    /// <summary>Status der Aufgabe.</summary>
    public WorkItemStatus WorkItemStatus => _workItem?.Status ?? Domain.Enums.WorkItemStatus.New;

    /// <summary>Branch-Name der Aufgabe.</summary>
    public string WorkItemBranchName => _workItem?.BranchName ?? string.Empty;

    /// <summary>Gibt an, ob Daten geladen werden.</summary>
    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            SetProperty(ref _isLoading, value);
            OnPropertyChanged(nameof(CanCreateIssue));
            OnPropertyChanged(nameof(ShowIssueGroup));
            OnPropertyChanged(nameof(PauseCan));
        }
    }

    /// <summary>Fehlermeldung bei Fehlern.</summary>
    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    /// <summary>Gibt an, ob ein CLI-Prozess läuft.</summary>
    public bool IsCliRunning
    {
        get => _isCliRunning;
        private set
        {
            SetProperty(ref _isCliRunning, value);
            OnPropertyChanged(nameof(StopCanCli));
            OnPropertyChanged(nameof(StartCanCliNew));
            OnPropertyChanged(nameof(CompleteCanWorkItem));
            OnPropertyChanged(nameof(SendCanPromptTemplate));
            OnPropertyChanged(nameof(SaveCan));
            OnPropertyChanged(nameof(DeleteCan));
            OnPropertyChanged(nameof(CreateCanPullRequest));
            OnPropertyChanged(nameof(CanAssignIssue));
            OnPropertyChanged(nameof(CanCreateIssue));
            OnPropertyChanged(nameof(ShowIssueGroup));
            OnPropertyChanged(nameof(CanPromptPlan));
            OnPropertyChanged(nameof(IsPaused));
            OnPropertyChanged(nameof(PauseDisplayText));
        }
    }

    /// <summary>Aktueller Laufzeitstatus der CLI für die Fußzeile.</summary>
    public string CliStatusText
    {
        get => _cliStatusText;
        private set => SetProperty(ref _cliStatusText, value);
    }

    /// <summary>Name der aktuell ausgeführten CLI für die Fußzeile.</summary>
    public string? ActiveCliName
    {
        get => _activeCliName;
        private set => SetProperty(ref _activeCliName, value);
    }

    /// <summary>Gibt an, ob der laufende CLI-Prozess gestoppt werden kann.</summary>
    public bool StopCanCli => _isCliRunning;

    /// <summary>Gibt an, ob die CLI fuer eine aktive Ausfuehrung wiederhergestellt werden kann.</summary>
    public bool StartCanCliNew => _workItem?.ExecutionStatus.ShouldShowCli(_workItem.Status) == true
        && !_isCliRunning
        && !IsPaused;

    /// <summary>Gibt an, ob die Aufgabe endgueltig abgeschlossen werden kann.</summary>
    public bool CompleteCanWorkItem => _workItem?.Status.IsActiveOrWaiting() == true
        && !_isCliRunning;

    /// <summary>Gewähltes KI-Plugin (Prefix).</summary>
    public string? SelectedAiPluginPrefix
    {
        get => _selectedAiPluginPrefix;
        set => SetProperty(ref _selectedAiPluginPrefix, value);
    }

    /// <summary>Steuert die Sichtbarkeit des KI-Plugin-Selectors (false bei genau einem aktiven Plugin).</summary>
    public bool ShowAiPluginSelection
    {
        get => _showAiPluginSelection;
        private set => SetProperty(ref _showAiPluginSelection, value);
    }

    /// <summary>Optionale Parameter für den CLI-Start.</summary>
    public string? OptionalCliParameter
    {
        get => _optionalCliParameter;
        set => SetProperty(ref _optionalCliParameter, value);
    }

    /// <summary>Protokolleinträge der Aufgabe.</summary>
    /// <value>Die geladenen Protokolleinträge der Aufgabe.</value>
    public ObservableCollection<LogEntry> LogEntries { get; } = new();

    /// <summary>Pull Requests der Aufgabe.</summary>
    public ObservableCollection<PullRequestReference> PullRequests { get; } = new();

    /// <summary>Komponiertes Presentation Model der To-Do-Liste.</summary>
    public TodoListViewModel TodoList => _todoListViewModel;

    /// <summary>Verfügbare KI-Plugin-Prefixe.</summary>
    /// <value>Die Liste der verfügbaren KI-Plugin-Prefixe.</value>
    public ObservableCollection<string> AvailableAiPlugins { get; } = new();

    /// <summary>Verfügbare Promptvorlagen.</summary>
    public ObservableCollection<PromptTemplate> PromptTemplates { get; } = new();

    /// <summary>Aktuell gewählte Promptvorlage im Ribbon.</summary>
    public PromptTemplate? SelectedPromptTemplate
    {
        get => _selectedPromptTemplate;
        set
        {
            var changed = SetProperty(ref _selectedPromptTemplate, value);
            OnPropertyChanged(nameof(CanPromptPlan));

            if (!changed || value is null)
                return;

            SelectPromptTemplateCommand.Execute(value);
        }
    }

    /// <summary>Gibt an, ob eine Promptvorlage an die laufende CLI gesendet werden kann.</summary>
    public bool SendCanPromptTemplate => _isCliRunning && PromptTemplates.Count > 0 && !IsPaused;

    /// <summary>Bindung Stunde-Eingabefeld für den zeitgesteuerten Prompt-Versand (null bedeutet leer).</summary>
    public int? ScheduledPromptTargetHours
    {
        get => _scheduledPromptTargetHours;
        set
        {
            SetProperty(ref _scheduledPromptTargetHours, value);
            OnPropertyChanged(nameof(CanPromptPlan));
        }
    }

    /// <summary>Bindung Minute-Eingabefeld für den zeitgesteuerten Prompt-Versand (null bedeutet leer).</summary>
    public int? ScheduledPromptTargetMinutes
    {
        get => _scheduledPromptTargetMinutes;
        set
        {
            SetProperty(ref _scheduledPromptTargetMinutes, value);
            OnPropertyChanged(nameof(CanPromptPlan));
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
    public bool CanPromptPlan => _isCliRunning
        && _selectedPromptTemplate is not null
        && !string.IsNullOrWhiteSpace(_selectedPromptTemplate.PromptText)
        && (_scheduledPromptTargetHours.HasValue || _scheduledPromptTargetMinutes.HasValue)
        && !IsPaused;

    /// <summary>Steuert die Info-Ansicht als Kompatibilitätsschicht für ältere Tests.</summary>
    public bool IsInfoViewVisible
    {
        get => IsInfoViewSelected;
        set
        {
            if (value)
            {
                SelectView(DetailView.Info);
            }
            else if (ShowCliPanel)
            {
                SelectView(DetailView.Cli);
            }
            else if (ShowDiffPanel)
            {
                SelectView(DetailView.Diff);
            }
        }
    }

    /// <summary>Gibt an, ob die Stammdatenansicht ausgewählt ist.</summary>
    public bool IsInfoViewSelected => _selectedView == DetailView.Info;

    /// <summary>Gibt an, ob die CLI-Ansicht ausgewählt ist.</summary>
    public bool IsCliViewSelected => _selectedView == DetailView.Cli;

    /// <summary>Gibt an, ob die Diff-Ansicht ausgewählt ist.</summary>
    public bool IsDiffViewSelected => _selectedView == DetailView.Diff;

    /// <summary>Gibt an, ob die Dateiexplorer-Ansicht ausgewählt ist.</summary>
    public bool IsFileExplorerViewSelected => _selectedView == DetailView.FileBrowser;

    /// <summary>Gibt an, ob die Pull-Request-Ansicht ausgewählt ist.</summary>
    public bool IsPullRequestViewSelected => _selectedView == DetailView.PullRequests;

    /// <summary>Gibt an, ob die Todo-Ansicht ausgewählt ist.</summary>
    public bool IsTodoViewSelected => _selectedView == DetailView.Todos;

    /// <summary>Gibt an, ob Pull Requests angezeigt werden koennen.</summary>
    public bool ShowPullRequestPanel => _workItem is not null;

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
    public bool CanRefreshPullRequests => _workItemId != Guid.Empty
        && !_isRefreshingPullRequests
        && PullRequests.Any(p => PullRequestMonitoringPolicy.CanMonitor(p.Role, p.Provider));

    /// <summary>Editable Kopie von Aufgabe.Titel für den Edit-Modus (Two-Way-Binding).</summary>
    public string? EditTitle
    {
        get => _editTitle;
        set
        {
            SetProperty(ref _editTitle, value);
            OnPropertyChanged(nameof(SaveCan));
        }
    }

    /// <summary>Editable Kopie von Aufgabe.AnforderungsBeschreibung für den Edit-Modus.</summary>
    public string? EditRequirementDescription
    {
        get => _editRequirementDescription;
        set => SetProperty(ref _editRequirementDescription, value);
    }

    /// <summary>True wenn Status == Neu, sonst false.</summary>
    public bool ShowEditPanel => _workItem?.Status == Domain.Enums.WorkItemStatus.New;

    /// <summary>True wenn die Aufgabe eine aktive KI-Ausfuehrung anzeigen soll.</summary>
    public bool ShowCliPanel => _workItem?.ExecutionStatus.ShouldShowCli(_workItem.Status) == true;

    /// <summary>True wenn Status == Beendet, sonst false.</summary>
    public bool ShowDiffPanel => _workItem?.Status == Domain.Enums.WorkItemStatus.Completed;

    /// <summary>True wenn Aufgabe.LokalerKlonPfad gesetzt ist und das Verzeichnis existiert. Wird beim Setzen von <see cref="WorkItem"/> einmalig ermittelt und gecacht, um wiederholte synchrone Dateisystemzugriffe bei jedem Property-Zugriff zu vermeiden.</summary>
    public bool ShowFileExplorerPanel => _showFileExplorerPanel;

    /// <summary>
    /// True wenn die IDE-Aktion ausgeführt werden kann. Die konkrete IDE wird erst beim Ausführen von
    /// <see cref="OpenIdeCommand"/> über <see cref="PluginSelectionService.ResolveIdePluginAsync"/> aufgelöst
    /// (explizit/fallback-kompatibles Plugin oder Default-Plugin) - da mindestens ein IDE-Plugin systemseitig
    /// stets aktiv bleiben muss, genügt als Bedingung ein gültiges, vorhandenes Arbeitsverzeichnis.
    /// </summary>
    public bool OpenCanIde => ShowFileExplorerPanel;

    /// <summary>
    /// Gibt an, ob mehrere Einstiegspunkte für die aktuell aufgelöste IDE verfügbar sind. Steuert die
    /// Sichtbarkeit des Dropdown-Buttons im <see cref="Controls.RibbonSplitButton"/>. Wird bei jedem Aufruf
    /// von <see cref="OpenIdeAsync"/> oder <see cref="OpenIdeSelectionAsync"/> aktualisiert.
    /// </summary>
    public bool SelectCanIde
    {
        get => _canIdeSelect;
        private set => SetProperty(ref _canIdeSelect, value);
    }

    /// <summary>True wenn die Info-Ansicht angezeigt werden soll.</summary>
    public bool ShowInfoPanel => IsInfoViewSelected;

    /// <summary>Komponiertes Presentation Model des Dateiexplorers.</summary>
    public FileExplorerViewModel FileExplorer => _fileExplorerViewModel;

    /// <summary>CanExecute für SpeichernCommand: Status ∈ {Neu, Gestartet} &amp;&amp; !IsCliRunning &amp;&amp; Titel.Length > 0.</summary>
    public bool SaveCan => _workItem?.Status is Domain.Enums.WorkItemStatus.New or Domain.Enums.WorkItemStatus.Started
        && !_isCliRunning
        && !string.IsNullOrEmpty(_editTitle);

    /// <summary>CanExecute für LoeschenCommand: Status ∉ {Archiviert} &amp;&amp; !IsCliRunning.</summary>
    /// <value>true wenn die Aufgabe gelöscht werden kann.</value>
    public bool DeleteCan => _workItem?.Status is not Domain.Enums.WorkItemStatus.Archived
        && _workItem != null
        && !_isCliRunning;

    /// <summary>true wenn für die Aufgabe ein Pull Request erstellt werden kann.</summary>
    public bool CreateCanPullRequest => _workItem != null
        && !string.IsNullOrWhiteSpace(_workItem.BranchName)
        && !string.IsNullOrWhiteSpace(_workItem.GitRepository?.RepositoryUrl)
        && _canCreatePullRequest;

    /// <summary>true wenn Aufgabe vorhanden, SCM-Plugin Issues unterstützt und kein CLI läuft.</summary>
    public bool CanAssignIssue => _workItem != null
        && !_isCliRunning
        && _pluginManager.GetSourceCodeManagementPlugins().Any(p => p is IGitPlugin);

    /// <summary>true wenn ein neues Issue für die Aufgabe angelegt werden kann.</summary>
    public bool CanCreateIssue => _workItem != null
        && !_isLoading
        && !_isCliRunning
        && _workItem.IssueReference is null
        && !string.IsNullOrWhiteSpace(_workItem.GitRepository?.RepositoryUrl)
        && _canCreateIssue;

    /// <summary>true wenn die Issue-Ribbon-Gruppe sichtbar sein soll.</summary>
    public bool ShowIssueGroup => CanAssignIssue || CanCreateIssue || CurrentIssueReference?.IssueUrl is not null;

    /// <summary>Aktuelle Issue-Zuweisung der Aufgabe.</summary>
    public IssueReference? CurrentIssueReference => _workItem?.IssueReference;

    /// <summary>Gibt an, ob die CLI-Rohausgabe exportiert werden kann.</summary>
    public bool ExportCanCliRaw => _workItem is not null && _workItemId != Guid.Empty;

    /// <summary>Gibt an, ob die CLI-Aufzeichnung exportiert werden kann.</summary>
    public bool ExportCanCliReplay => _workItem is not null && _workItemId != Guid.Empty;

    /// <summary>Der persistierte Pause-Endzeitpunkt der Aufgabe (UTC), oder null.</summary>
    public DateTimeOffset? PausedUntilUtc => _workItem?.PausedUntilUtc;

    /// <summary>true, wenn eine aktive Pause gesetzt ist (Ende liegt in der Zukunft).</summary>
    public bool IsPaused => _workItem?.PausedUntilUtc > DateTimeOffset.UtcNow;

    /// <summary>Anzeigetext der aktiven Pause für den Ribbon-Bereich, oder null wenn nicht pausiert.</summary>
    public string? PauseDisplayText => IsPaused
        ? $"⏸ Pausiert bis {_workItem!.PausedUntilUtc!.Value.ToLocalTime():dd.MM.yyyy HH:mm}"
        : null;

    /// <summary>true, wenn für die Aufgabe eine Pause eingestellt werden kann (Status ∈ {Neu, Gestartet, Wartend}, nicht am Laden).</summary>
    public bool PauseCan => _workItem?.Status is Domain.Enums.WorkItemStatus.New
            or Domain.Enums.WorkItemStatus.Started
            or Domain.Enums.WorkItemStatus.Waiting
        && !_isLoading;

    /// <summary>Lädt die Aufgabe.</summary>
    public ICommand LoadCommand { get; }

    /// <summary>Stoppt den CLI-Prozess.</summary>
    public ICommand StopCliCommand { get; }

    /// <summary>Startet die Aufgabe: kombiniertes Klonen, Plugin-Auflösung und CLI-Start.</summary>
    public ICommand StartCommand { get; }

    /// <summary>Startet die CLI für eine bereits laufende Aufgabe neu (nach manuellem Stopp).</summary>
    public ICommand RestartCliCommand { get; }

    /// <summary>Wechselt das KI-Plugin bei laufender CLI: Dialog, Stop, Restart.</summary>
    public ICommand SwitchPluginCommand { get; }

    /// <summary>Schließt die Aufgabe ab (Status: Beendet).</summary>
    public ICommand CompleteWorkItemCommand { get; }

    /// <summary>Speichert Titel und AnforderungsBeschreibung der Aufgabe.</summary>
    public ICommand SaveCommand { get; }

    /// <summary>Löscht die Aufgabe nach Bestätigungsdialog.</summary>
    public ICommand DeleteCommand { get; }

    /// <summary>Erstellt einen Pull Request für die Aufgabe.</summary>
    public ICommand CreatePullRequestCommand { get; }

    /// <summary>Aktualisiert Pull Requests und zugeordnete Workflow-Runs der Aufgabe.</summary>
    public ICommand RefreshPullRequestsCommand { get; }

    /// <summary>Öffnet eine Pull-Request-URL im Standard-Browser.</summary>
    public ICommand OpenPullRequestUrlCommand { get; }

    /// <summary>Toggled IsInfoViewVisible zwischen Info-Panel und CLI-Fenster.</summary>
    public ICommand InfoCliToggleCommand { get; }

    /// <summary>Wechselt zur Info-Ansicht.</summary>
    public ICommand InfoViewCommand { get; }

    /// <summary>Wechselt zur CLI-Ansicht.</summary>
    public ICommand CliViewCommand { get; }

    /// <summary>Wechselt zur Diff-Ansicht.</summary>
    public ICommand DiffViewCommand { get; }

    /// <summary>Wechselt zur Dateiexplorer-Ansicht.</summary>
    public ICommand FileViewCommand { get; }

    /// <summary>Wechselt zur Pull-Request-Ansicht.</summary>
    public ICommand PullRequestViewCommand { get; }

    /// <summary>Navigiert zurück zur vorherigen Ansicht.</summary>
    public ICommand BackCommand { get; }

    /// <summary>Öffnet den Issue-Auswahl-Dialog und weist das gewählte Issue der Aufgabe zu.</summary>
    public ICommand AssignIssueCommand { get; }

    /// <summary>Öffnet den Issue-Anlage-Dialog und weist das neu erstellte Issue der Aufgabe zu.</summary>
    public ICommand IssueAnlegenCommand { get; }

    /// <summary>Öffnet die Issue-URL im Standard-Browser.</summary>
    public ICommand OpenIssueBrowserCommand { get; }

    /// <summary>Exportiert die protokollierte CLI-Rohausgabe in eine *.raw-Datei.</summary>
    public ICommand ExportCliRawCommand { get; }

    /// <summary>Exportiert den Rohbyte-Mitschnitt der CLI-Session in eine *.clireplay-Datei.</summary>
    public ICommand ExportCliReplayCommand { get; }

    /// <summary>Öffnet den Pause-Dialog und setzt bzw. hebt die Pause der Aufgabe.</summary>
    public ICommand SetPauseCommand { get; }

    /// <summary>Sendet die gewählte Promptvorlage an die laufende CLI.</summary>
    public ICommand SelectPromptTemplateCommand { get; }

    /// <summary>Plant den Versand der aktuell gewählten Promptvorlage zur eingegebenen Zielzeit.</summary>
    public ICommand SchedulePromptCommand { get; }

    /// <summary>Hebt die Wartestellung eines geplanten zeitgesteuerten Prompts auf.</summary>
    public ICommand CancelScheduledPromptCommand { get; }

    /// <summary>Öffnet das Arbeitsverzeichnis der Aufgabe im OS-Dateiexplorer.</summary>
    public ICommand OpenWorkingDirectoryCommand { get; }

    /// <summary>Öffnet die Solution des Arbeitsverzeichnisses mit der registrierten IDE.</summary>
    public ICommand OpenIdeCommand { get; }

    /// <summary>Zeigt die Auswahl der verfügbaren IDE-Einstiegspunkte an und öffnet den gewählten.</summary>
    public ICommand OpenIdeSelectionCommand { get; }

    /// <summary>Wird gefeuert, wenn eine neue <see cref="ITerminalSession"/> gestartet wurde. Löst weiterhin
    /// das Binden von <c>TerminalControl.Session</c> in <c>TaskDetailView</c> aus, unabhängig davon, ob die
    /// Leseschleife der Session bereits vor der UI-Bindung läuft (parallele CLI-Ausführungen, Issue-86).</summary>
    public event Action<ITerminalSession>? TerminalSessionStarted;

    /// <summary>Wird gefeuert, wenn der CLI-Prozess der aktuellen Aufgabe beendet wurde.</summary>
    public event Action? CliStopped;

    /// <summary>Wird gefeuert, nachdem eine Promptvorlage erfolgreich an die CLI gesendet wurde.</summary>
    public event Action? PromptTemplateSent;

    /// <summary>Gibt die aktive <see cref="ITerminalSession"/> für die aktuelle Aufgabe zurück, oder null.
    /// Die Session (und ihre Leseschleife) läuft unabhängig vom Lebenszyklus der View, die diese Methode
    /// aufruft — der zurückgegebene Prozess kann also bereits vor dem Öffnen dieser Aufgabenseite gestartet
    /// worden sein und weiterlaufen, nachdem die Seite wieder verlassen wird.</summary>
    /// <returns>Die aktive <see cref="ITerminalSession"/>, oder null wenn keine Session läuft.</returns>
    public ITerminalSession? GetTerminalSession() => _aiService.GetTerminalSession(_workItemId);

    /// <inheritdoc cref="TaskDetailViewModel"/>
    public TaskDetailViewModel(
        WorkItemService workItemService,
        LogService logService,
        AiExecutionService aiService,
        DevelopmentProcessService developmentprocessService,
        PluginSelectionService pluginSelectionService,
        PromptTemplateService promptTemplatesService,
        PromptTemplatePlaceholderService promptTemplatesPlaceholderService,
        PromptScheduleDispatchService promptScheduleDispatchService,
        IDialogService dialogService,
        IPluginManager pluginManager,
        IServiceProvider serviceProvider,
        ILogger<TaskDetailViewModel> logger,
        TimeProvider timeProvider,
        FileExplorerViewModel fileExplorerViewModel,
        TodoListViewModel todoListViewModel,
        WorkingDirectoryOpenService workingDirectoryOpenService,
        Action<Action>? dispatcherInvoke = null,
        ICliRawExportService? cliRawExportService = null,
        ICliReplayExportService? cliReplayExportService = null,
        WorkItemRunDataChangedNotifier? runDataChangedNotifier = null)
    {
        _workItemService = workItemService;
        _logService = logService;
        _aiService = aiService;
        _developmentprocessService = developmentprocessService;
        _pluginSelectionService = pluginSelectionService;
        _promptTemplatesService = promptTemplatesService;
        _promptTemplatesPlaceholderService = promptTemplatesPlaceholderService;
        _promptScheduleDispatchService = promptScheduleDispatchService;
        _dialogService = dialogService;
        _pluginManager = pluginManager;
        _serviceProvider = serviceProvider;
        _logger = logger;
        _fileExplorerViewModel = fileExplorerViewModel;
        _todoListViewModel = todoListViewModel;
        _workingDirectoryOpenService = workingDirectoryOpenService;
        _timeProvider = timeProvider;
        _cliRawExportService = cliRawExportService ?? new CliRawExportService(logService, NullLogger<CliRawExportService>.Instance);
        _cliReplayExportService = cliReplayExportService ?? new CliReplayExportService(aiService, new CliReplayRecordingStore(), NullLogger<CliReplayExportService>.Instance);
        _runDataChangedNotifier = runDataChangedNotifier;
        _dispatcherInvoke = DispatcherInvokeFactory.Create(dispatcherInvoke);

        _aiService.CliProcessStatusChanged += OnCliProcessStatusChanged;
        _promptScheduleDispatchService.PromptSent += OnPromptSent;
        _promptScheduleDispatchService.ScheduledPromptChanged += OnScheduledPromptChanged;
        _todoListViewModel.ActivateViewCallback = () => SelectView(DetailView.Todos);
        _todoListViewModel.ErrorCallback = message => ErrorMessage = message;

        LoadCommand = new AsyncRelayCommand(ct => LoadAsync(ct));
        StopCliCommand = new AsyncRelayCommand(StopCliAsync, () => StopCanCli);
        RestartCliCommand = new AsyncRelayCommand(RestartCliAsync, () => StartCanCliNew);
        StartCommand = new AsyncRelayCommand(
            StartAsync,
            () => _workItem?.ExecutionStatus.MayStartExecution(_workItem.Status) == true
                && !_isCliRunning
                && !IsPaused);
        SwitchPluginCommand = new AsyncRelayCommand(SwitchPluginAsync, () => WorkItemStatus is Domain.Enums.WorkItemStatus.Started or Domain.Enums.WorkItemStatus.Waiting && _isCliRunning);
        CompleteWorkItemCommand = new AsyncRelayCommand(CompleteWorkItemAsync, () => CompleteCanWorkItem);
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => SaveCan);
        DeleteCommand = new AsyncRelayCommand(DeleteAsync, () => DeleteCan);
        CreatePullRequestCommand = new AsyncRelayCommand(CreatePullRequestAsync, () => CreateCanPullRequest && !_isLoading);
        RefreshPullRequestsCommand = new AsyncRelayCommand(RefreshPullRequestsAsync, () => CanRefreshPullRequests);
        OpenPullRequestUrlCommand = new RelayCommand<string>(
            url => OpenPullRequestUrl(url),
            url => !string.IsNullOrWhiteSpace(url));
        InfoCliToggleCommand = new RelayCommand(InfoCliToggle);
        InfoViewCommand = new RelayCommand(() => SelectView(DetailView.Info));
        CliViewCommand = new RelayCommand(() => SelectView(DetailView.Cli), () => ShowCliPanel);
        DiffViewCommand = new RelayCommand(() => SelectView(DetailView.Diff), () => ShowDiffPanel);
        FileViewCommand = new RelayCommand(() => SelectView(DetailView.FileBrowser), () => ShowFileExplorerPanel);
        PullRequestViewCommand = new RelayCommand(SelectPullRequestView, () => ShowPullRequestPanel);
        BackCommand = new RelayCommand(() => BackAction?.Invoke());
        AssignIssueCommand = new AsyncRelayCommand(AssignIssueAsync, () => CanAssignIssue && !_isLoading);
        IssueAnlegenCommand = new AsyncRelayCommand(IssueAnlegenAsync, () => CanCreateIssue);
        OpenIssueBrowserCommand = new RelayCommand(
            OpenIssueBrowser,
            () => CurrentIssueReference?.IssueUrl != null);
        ExportCliRawCommand = new AsyncRelayCommand(ExportCliRawAsync, () => ExportCanCliRaw);
        ExportCliReplayCommand = new AsyncRelayCommand(ExportCliReplayAsync, () => ExportCanCliReplay);
        SetPauseCommand = new AsyncRelayCommand(SetPauseAsync, () => PauseCan);
        SelectPromptTemplateCommand = new AsyncRelayCommand<PromptTemplate>(
            SelectPromptTemplateAsync,
            template => template is not null && SendCanPromptTemplate);
        SchedulePromptCommand = new AsyncRelayCommand(SchedulePromptAsync, () => CanPromptPlan);
        CancelScheduledPromptCommand = new AsyncRelayCommand(ct => _promptScheduleDispatchService.CancelScheduledPromptAsync(_workItemId));
        OpenWorkingDirectoryCommand = new AsyncRelayCommand(OpenWorkingDirectoryAsync, () => ShowFileExplorerPanel);
        OpenIdeCommand = new AsyncRelayCommand(OpenIdeAsync, () => OpenCanIde);
        OpenIdeSelectionCommand = new AsyncRelayCommand(OpenIdeSelectionAsync, () => OpenCanIde);
    }

    private async Task LoadAsync(CancellationToken ct)
    {
        if (_workItemId == Guid.Empty)
            return;

        IsLoading = true;
        ErrorMessage = null;

        try
        {
            WorkItem = await _workItemService.GetDetailAsync(_workItemId, ct);
            var mayCliShow = WorkItem?.ExecutionStatus.ShouldShowCli(WorkItem.Status) == true;
            IsCliRunning = mayCliShow && _aiService.IsRunning(_workItemId);

            var session = mayCliShow ? _aiService.GetTerminalSession(_workItemId) : null;
            AttachCliStatusSession(session);
            // Explizit erneut auslösen (nicht nur AttachCliStatusSession): Wechselt CurrentView in
            // MainWindowViewModel/ProjectDetailViewModel zwischen zwei TaskDetailViewModel-Instanzen
            // desselben Typs, kann TaskDetailView.OnDataContextChanged bereits synchron vor dem Setzen
            // von AufgabeId gefeuert haben und liest dabei eine veraltete/leere Sitzung. Ohne diesen
            // erneuten Abgleich bliebe TerminalControl.Session dauerhaft auf der vorherigen Aufgabe
            // stehen, wenn eine bereits laufende Sitzung wiederangebunden statt neu gestartet wird.
            if (session is not null)
                TerminalSessionStarted?.Invoke(session);
            else
                CliStopped?.Invoke();

            await UpdateActiveCliNameFromWorkItemAsync(ct);

            EditTitle = WorkItem?.Title;
            EditRequirementDescription = WorkItem?.RequirementDescription;

            _logLoadCts?.Cancel();
            _logLoadCts?.Dispose();
            _logLoadCts = new CancellationTokenSource();
            LoadLogAsync(_logLoadCts.Token).SafeFireAndForget(_logger, "TaskDetailViewModel.LoadLogAsync");

            await LoadPullRequestsAsync(ct);
            await _todoListViewModel.LoadAsync(_workItemId, ct);

            await LoadAvailablePluginsAsync(ct);
            await LoadPromptTemplatesAsync(ct);
            await UpdatePullRequestCapabilityAsync(ct);
            await UpdateIssueCreateCapabilityAsync(ct);
            await UpdateCanSelectIdeAsync(ct);

            // Ein persistierter Plan (auch aus früheren Sitzungen/App-Läufen) wird beim
            // (Wieder-)Öffnen der Ansicht sofort in der Statusanzeige sichtbar.
            UpdateQueueDisplay();

            // LadenAsync bindet nur vorhandene laufende Sitzungen wieder an. Ein neuer CLI-Prozess
            // wird ausschließlich über explizite Start-/Neustartaktionen gestartet.
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Laden der Aufgabe {AufgabeId}.", _workItemId);
            SetError(ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadLogAsync(CancellationToken ct)
    {
        LogEntries.Clear();
        var protokolleintraege = await _logService.GetByWorkItemAsync(_workItemId, ct);
        foreach (var entry in protokolleintraege)
            LogEntries.Add(entry);
    }

    private async Task LoadAvailablePluginsAsync(CancellationToken ct)
    {
        try
        {
            var pluginName = await _pluginSelectionService.GetAvailableAiPluginPrefixesAsync(ct);
            AvailableAiPlugins.Clear();
            foreach (var name in pluginName)
                AvailableAiPlugins.Add(name);

            if (AvailableAiPlugins.Count > 0 && _selectedAiPluginPrefix is null)
                SelectedAiPluginPrefix = AvailableAiPlugins[0];

            ShowAiPluginSelection = AvailableAiPlugins.Count > 1;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "KI-Plugin-Liste konnte nicht geladen werden.");
        }
    }

    private async Task LoadPullRequestsAsync(CancellationToken ct)
    {
        PullRequests.Clear();

        var pullRequestService = _serviceProvider.GetService<PullRequestReferenceService>();
        if (pullRequestService is null || _workItemId == Guid.Empty)
        {
            OnPropertyChanged(nameof(HasNoPullRequests));
            OnPropertyChanged(nameof(HasPullRequests));
            OnPropertyChanged(nameof(CanRefreshPullRequests));
            return;
        }

        var pullRequests = await pullRequestService.GetByWorkItemAsync(_workItemId, ct);
        foreach (var pullRequest in pullRequests)
        {
            PullRequests.Add(pullRequest);
        }

        OnPropertyChanged(nameof(HasNoPullRequests));
        OnPropertyChanged(nameof(HasPullRequests));
        OnPropertyChanged(nameof(ShowPullRequestPanel));
        OnPropertyChanged(nameof(CanRefreshPullRequests));
    }

    private async Task RefreshPullRequestsAsync(CancellationToken ct)
    {
        if (_workItemId == Guid.Empty)
            return;

        IsRefreshingPullRequests = true;
        ErrorMessage = null;

        try
        {
            var monitoringService = _serviceProvider.GetService<PullRequestMonitoringService>();
            if (monitoringService is not null)
            {
                await monitoringService.RefreshWorkItemAsync(_workItemId, ct);
            }

            await LoadPullRequestsAsync(ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Pull Requests fuer Aufgabe {AufgabeId} konnten nicht aktualisiert werden.", _workItemId);
            ErrorMessage = $"Pull Requests konnten nicht aktualisiert werden: {ex.Message}";
            await LoadPullRequestsAsync(CancellationToken.None);
        }
        finally
        {
            IsRefreshingPullRequests = false;
        }
    }

    private async Task LoadPromptTemplatesAsync(CancellationToken ct)
    {
        var templates = await _promptTemplatesService.GetAllAsync(ct);
        PromptTemplates.Clear();
        foreach (var template in templates)
            PromptTemplates.Add(template);

        OnPropertyChanged(nameof(SendCanPromptTemplate));
    }

    private async Task SelectPromptTemplateAsync(PromptTemplate? template, CancellationToken ct)
    {
        if (template is null || string.IsNullOrWhiteSpace(template.PromptText))
            return;

        if (_scheduledPromptTargetHours.HasValue || _scheduledPromptTargetMinutes.HasValue)
            return;

        var session = _aiService.GetTerminalSession(_workItemId);
        if (session is null || !_isCliRunning)
            return;

        var prompt = _promptTemplatesPlaceholderService.Resolve(template.PromptText, _workItem);
        if (string.IsNullOrWhiteSpace(prompt))
            return;

        await session.WritePromptAsync(prompt, ct);

        SelectView(DetailView.Cli);
        SelectedPromptTemplate = null;
        PromptTemplateSent?.Invoke();
    }

    private async Task StopCliAsync(CancellationToken ct)
    {
        if (_workItemId == Guid.Empty)
            return;

        ErrorMessage = null;

        try
        {
            await _aiService.StopCliAsync(_workItemId, ct);

            if (_aiService.IsRunning(_workItemId))
            {
                // Der Prozess konnte nicht beendet werden (Beendigungsfehler werden in StopCliAsync
                // nur geloggt): Laufdaten und Session-Anbindung bleiben bestehen, damit Fußzeile
                // und Kachel den tatsächlich weiterlaufenden Prozess zeigen statt fälschlich
                // "Bereit" (Issue 342, Divergenzpfad F1). KannCliStoppen bleibt erneut auslösbar.
                ErrorMessage = "Der CLI-Prozess konnte nicht beendet werden und läuft weiter. Bitte erneut versuchen.";
                return;
            }

            await _workItemService.EndActiveRunAsync(_workItemId, ct);
            _dispatcherInvoke(() =>
            {
                IsCliRunning = false;
                ActiveCliName = null;
                if (_workItem is not null)
                {
                    _workItem.ExecutionStatus = WorkItemExecutionStatus.Completed;
                    OnPropertyChanged(nameof(StartCanCliNew));
                    OnPropertyChanged(nameof(CompleteCanWorkItem));
                    OnPropertyChanged(nameof(ShowCliPanel));
                    SelectDefaultView();
                }
            });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Stoppen des CLI für Aufgabe {AufgabeId}.", _workItemId);
            ErrorMessage = $"CLI-Stoppfehler: {ex.Message}";
        }
    }

    private async Task RestartCliAsync(CancellationToken ct)
    {
        if (_workItemId == Guid.Empty || _workItem is null)
            return;

        ErrorMessage = null;

        try
        {
            var pluginPrefix = await _pluginSelectionService.ResolveDevelopmentAutomationPluginWithProjectScopeAsync(
                _workItem.AiPluginPrefix,
                _workItem.ProjectId,
                ct);

            if (string.IsNullOrEmpty(pluginPrefix))
                return;

            await StartCliAndUpdateStateAsync(pluginPrefix, _workItem.LocalClonePath ?? string.Empty, null, ct);
            await LoadAsync(ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim manuellen CLI-Neustart für Aufgabe {AufgabeId}.", _workItemId);
            ErrorMessage = $"CLI konnte nicht gestartet werden: {ex.Message}";
        }
    }

    private async Task UpdatePullRequestCapabilityAsync(CancellationToken ct)
    {
        _canCreatePullRequest = false;

        if (_workItem is null
            || string.IsNullOrWhiteSpace(_workItem.BranchName)
            || string.IsNullOrWhiteSpace(_workItem.GitRepository?.RepositoryUrl))
        {
            OnPropertyChanged(nameof(CreateCanPullRequest));
            return;
        }

        var gitPlugin = ResolveGitPluginForWorkItem();
        if (gitPlugin is null)
        {
            OnPropertyChanged(nameof(CreateCanPullRequest));
            return;
        }

        try
        {
            var capabilities = await gitPlugin.GetGitActionCapabilitiesAsync(_workItem.LocalClonePath, ct);
            _canCreatePullRequest = capabilities.CanCreatePullRequest;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Pull-Request-Capability für Aufgabe {AufgabeId} konnte nicht ermittelt werden.", _workItemId);
        }
        finally
        {
            OnPropertyChanged(nameof(CreateCanPullRequest));
        }
    }

    private async Task UpdateIssueCreateCapabilityAsync(CancellationToken ct)
    {
        _canCreateIssue = false;

        if (_workItem is null
            || _workItem.IssueReference is not null
            || string.IsNullOrWhiteSpace(_workItem.GitRepository?.RepositoryUrl))
        {
            OnPropertyChanged(nameof(CanCreateIssue));
            OnPropertyChanged(nameof(ShowIssueGroup));
            return;
        }

        var gitPlugin = ResolveGitPluginForWorkItem();
        if (gitPlugin is not IIssueCreateProvider issueCreateProvider)
        {
            OnPropertyChanged(nameof(CanCreateIssue));
            OnPropertyChanged(nameof(ShowIssueGroup));
            return;
        }

        try
        {
            _canCreateIssue = await issueCreateProvider.CanCreateIssueAsync(GetRepositoryIdentifier(_workItem), ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Issue-Anlage-Capability für Aufgabe {AufgabeId} konnte nicht ermittelt werden.", _workItemId);
        }
        finally
        {
            OnPropertyChanged(nameof(CanCreateIssue));
            OnPropertyChanged(nameof(ShowIssueGroup));
        }
    }

    private IGitPlugin? ResolveGitPluginForWorkItem()
    {
        var gitPlugins = _pluginManager.GetSourceCodeManagementPlugins().OfType<IGitPlugin>().ToList();
        if (gitPlugins.Count == 0)
            return null;

        var pluginType = _workItem?.GitRepository?.PluginType;
        if (!string.IsNullOrWhiteSpace(pluginType))
        {
            var matchingPlugin = gitPlugins.FirstOrDefault(p =>
                string.Equals(p.PluginPrefix, pluginType, StringComparison.OrdinalIgnoreCase));
            if (matchingPlugin is not null)
                return matchingPlugin;
        }

        return gitPlugins.FirstOrDefault();
    }

    private static string GetRepositoryIdentifier(WorkItem workItem)
        => !string.IsNullOrWhiteSpace(workItem.GitRepository?.RepositoryUrl)
            ? workItem.GitRepository.RepositoryUrl
            : workItem.GitRepository?.RepositoryName ?? string.Empty;

    private async Task CompleteWorkItemAsync(CancellationToken ct)
    {
        if (_workItemId == Guid.Empty)
            return;

        try
        {
            if (!await _workItemService.CanCompleteTaskAsync(_workItemId, ct))
            {
                ErrorMessage = string.Format(WorkItemService.OpenTodosErrorMessageFormat, _todoListViewModel.OpenTodoCount);
                return;
            }

            await _developmentprocessService.CompleteAsync(_workItemId, ct);
            await LoadAsync(ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Abschließen der Aufgabe {AufgabeId}.", _workItemId);
            SetError(ex);
        }
    }

    private async Task SaveAsync(CancellationToken ct)
    {
        if (_workItemId == Guid.Empty)
            return;

        IsLoading = true;
        ErrorMessage = null;

        try
        {
            await _workItemService.UpdateAsync(_workItemId, _editTitle ?? string.Empty, _editRequirementDescription, null, ct);
            await LoadAsync(ct);

            try
            {
                await (RefreshWorkItemListCallback?.Invoke() ?? Task.CompletedTask);
            }
            catch (Exception callbackEx)
            {
                _logger.LogError(callbackEx, "Fehler im RefreshWorkItemListCallback nach Aufgabenspeicherung.");
            }

        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Speichern der Aufgabe {AufgabeId}.", _workItemId);
            ErrorMessage = $"Aufgabe konnte nicht gespeichert werden: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task DeleteAsync(CancellationToken ct)
    {
        if (_workItemId == Guid.Empty)
            return;

        var dialogMessage = $"Aufgabe '{WorkItemTitle}' wirklich löschen? Diese Aktion kann nicht rückgängig gemacht werden.";
        if (!_dialogService.ConfirmDialog(dialogMessage, "Löschen bestätigen"))
            return;

        IsLoading = true;
        ErrorMessage = null;

        try
        {
            await _workItemService.DeleteAsync(_workItemId, ct);

            try
            {
                await (RefreshWorkItemListCallback?.Invoke() ?? Task.CompletedTask);
            }
            catch (Exception callbackEx)
            {
                _logger.LogError(callbackEx, "Fehler im RefreshWorkItemListCallback nach Aufgabenlöschung.");
            }

            BackAction?.Invoke();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Löschen der Aufgabe {AufgabeId}.", _workItemId);
            ErrorMessage = $"Aufgabe konnte nicht gelöscht werden: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task CreatePullRequestAsync(CancellationToken ct)
    {
        if (_workItemId == Guid.Empty || _workItem is null)
            return;

        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var gitOrchestrationService = _serviceProvider.GetRequiredService<GitOrchestrationService>();
            var pullRequest = await gitOrchestrationService.CreatePullRequestAsync(
                _workItemId,
                title: _workItem.Title,
                body: _workItem.RequirementDescription ?? string.Empty,
                ct: ct);

            await LoadAsync(ct);

            if (!string.IsNullOrWhiteSpace(pullRequest.Url))
                OpenPullRequestUrl(pullRequest.Url);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Erstellen des Pull Requests für Aufgabe {AufgabeId}.", _workItemId);
            SetError(ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void OpenPullRequestUrl(string? url)
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

    private async Task AssignIssueAsync(CancellationToken ct)
    {
        if (_workItem == null)
            return;

        var pluginType = _workItem.GitRepository?.PluginType;
        var gitPlugin = pluginType != null
            ? _pluginManager.GetSourceCodeManagementPlugins()
                .FirstOrDefault(p => string.Equals(p.PluginPrefix, pluginType, StringComparison.OrdinalIgnoreCase))
            : _pluginManager.GetSourceCodeManagementPlugins().FirstOrDefault();
        if (gitPlugin == null)
            return;

        var dialogVm = _serviceProvider.GetRequiredService<IssueSelectionDialogViewModel>();

        var repositoryId = _workItem.GitRepository?.RepositoryUrl ?? string.Empty;
        await dialogVm.LoadAsync(repositoryId, ct);

        var selectedIssue = await _dialogService.ShowIssueSelectionDialogAsync(dialogVm, ct);
        if (selectedIssue == null)
            return;

        try
        {
            await _workItemService.UpdateIssueReferenceAsync(_workItemId, selectedIssue, ct);
            await LoadAsync(ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Zuweisen des Issues für Aufgabe {AufgabeId}.", _workItemId);
            SetError(ex);
        }
    }

    private async Task IssueAnlegenAsync(CancellationToken ct)
    {
        if (_workItem == null)
            return;

        var currentState = await _workItemService.GetDetailAsync(_workItemId, ct);
        if (currentState?.IssueReference is not null)
        {
            WorkItem = currentState;
            ErrorMessage = "Der Aufgabe ist bereits ein Issue zugeordnet.";
            return;
        }

        var gitPlugin = ResolveGitPluginForWorkItem();
        if (gitPlugin is not IIssueCreateProvider issueCreateProvider)
        {
            ErrorMessage = "Der Repository-Provider unterstützt die Issue-Anlage nicht.";
            return;
        }

        var repositoryId = GetRepositoryIdentifier(_workItem);
        if (!await issueCreateProvider.CanCreateIssueAsync(repositoryId, ct))
        {
            ErrorMessage = "Der Repository-Provider unterstützt die Issue-Anlage für dieses Repository nicht.";
            return;
        }

        var dialogVm = _serviceProvider.GetRequiredService<IssueCreateDialogViewModel>();
        var preferredAiPluginPrefix = await ResolvePreferredAiPluginPrefixAsync(ct);
        await dialogVm.InitializeAsync(
            issueCreateProvider,
            gitPlugin as IIssueTemplateProvider,
            repositoryId,
            _workItem.Title,
            _workItem.RequirementDescription,
            preferredAiPluginPrefix,
            () => _workItem?.IssueReference is not null,
            async token => (await _workItemService.GetDetailAsync(_workItemId, token))?.IssueReference is not null,
            ct);

        var dialogResult = await _dialogService.ShowIssueCreateDialogAsync(dialogVm, ct);
        if (dialogResult is null)
        {
            return;
        }

        var createdIssue = dialogResult.Issue;

        try
        {
            var stateBeforeAssignment = await _workItemService.GetDetailAsync(_workItemId, ct);
            if (stateBeforeAssignment?.IssueReference is not null)
            {
                WorkItem = stateBeforeAssignment;
                ErrorMessage = $"Issue wurde extern erstellt ({DescribeIssue(createdIssue)}), aber nicht lokal zugeordnet, weil der Aufgabe inzwischen ein Issue zugeordnet ist.";
                return;
            }

            var newRequirementDescription = dialogResult.UpdateTaskDescription
                ? !string.IsNullOrWhiteSpace(createdIssue.Body)
                    ? createdIssue.Body
                    : dialogResult.LocalBody ?? string.Empty
                : null;

            if (!await _workItemService.TryAssignIssueReferenceAndUpdateDescriptionIfNoneAsync(
                    _workItemId,
                    createdIssue,
                    dialogResult.UpdateTaskDescription,
                    newRequirementDescription,
                    ct))
            {
                var stateAfterFailedAssignment = await _workItemService.GetDetailAsync(_workItemId, ct);
                if (stateAfterFailedAssignment is not null)
                {
                    WorkItem = stateAfterFailedAssignment;
                }

                ErrorMessage = $"Issue wurde extern erstellt ({DescribeIssue(createdIssue)}), aber nicht lokal zugeordnet, weil der Aufgabe inzwischen ein Issue zugeordnet ist.";
                return;
            }

            await LoadAsync(ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Externes Issue wurde erstellt, lokale Zuordnung oder Aufgabenbeschreibung für Aufgabe {AufgabeId} ist fehlgeschlagen.", _workItemId);
            ErrorMessage = $"Issue wurde extern erstellt ({DescribeIssue(createdIssue)}), die lokale Zuordnung oder Aufgabenbeschreibung konnte aber nicht gespeichert werden: {ex.Message}";
        }
    }

    private async Task<string?> ResolvePreferredAiPluginPrefixAsync(CancellationToken ct)
    {
        if (_workItem is null)
        {
            return null;
        }

        try
        {
            return await _pluginSelectionService.ResolveDevelopmentAutomationPluginWithProjectScopeAsync(
                _workItem.AiPluginPrefix,
                _workItem.ProjectId,
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Standard-KI-Provider für Issue-Anlage konnte nicht ermittelt werden.");
            return _selectedAiPluginPrefix;
        }
    }

    private static string DescribeIssue(Issue issue)
    {
        if (!string.IsNullOrWhiteSpace(issue.IssueUrl))
        {
            return issue.IssueUrl;
        }

        return issue.Number > 0 ? $"#{issue.Number}" : issue.Title;
    }

    private void OpenIssueBrowser()
    {
        var url = CurrentIssueReference?.IssueUrl;
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
            SetError(ex);
        }
    }

    private void SetError(Exception ex) => SetError(ref _errorMessage, nameof(ErrorMessage), ex);

    private void InfoCliToggle()
    {
        IsInfoViewVisible = !IsInfoViewVisible;
    }

    private void SelectDefaultView()
    {
        var defaultView = ShowCliPanel
            ? DetailView.Cli
            : WorkItemStatus switch
            {
                Domain.Enums.WorkItemStatus.Completed => DetailView.Diff,
                _ => DetailView.Info
            };

        SelectView(defaultView);
    }

    private void SelectView(DetailView view)
    {
        if (view == DetailView.Cli && !ShowCliPanel)
            view = DetailView.Info;
        if (view == DetailView.Diff && !ShowDiffPanel)
            view = DetailView.Info;
        if (view == DetailView.FileBrowser && !ShowFileExplorerPanel)
            view = DetailView.Info;
        if (view == DetailView.PullRequests && !ShowPullRequestPanel)
            view = DetailView.Info;

        if (_selectedView == view)
            return;

        _selectedView = view;
        OnPropertyChanged(nameof(IsInfoViewVisible));
        OnPropertyChanged(nameof(IsInfoViewSelected));
        OnPropertyChanged(nameof(IsCliViewSelected));
        OnPropertyChanged(nameof(IsDiffViewSelected));
        OnPropertyChanged(nameof(IsFileExplorerViewSelected));
        OnPropertyChanged(nameof(IsPullRequestViewSelected));
        OnPropertyChanged(nameof(IsTodoViewSelected));
        OnPropertyChanged(nameof(ShowInfoPanel));
    }

    private void SelectPullRequestView()
    {
        SelectView(DetailView.PullRequests);
        RefreshPullRequestsCommand.Execute(null);
    }

    private async Task UpdateActiveCliNameFromWorkItemAsync(CancellationToken ct)
    {
        if (!_isCliRunning)
        {
            ActiveCliName = null;
            return;
        }

        var pluginPrefix = await _pluginSelectionService.ResolveDevelopmentAutomationPluginWithProjectScopeAsync(
            _workItem?.AiPluginPrefix,
            _workItem?.ProjectId ?? Guid.Empty,
            ct);

        SetActiveCliName(pluginPrefix);
    }

    private void SetActiveCliName(string? pluginPrefix)
    {
        ActiveCliName = ResolveAiPluginName(pluginPrefix);
    }

    private string? ResolveAiPluginName(string? pluginPrefix)
    {
        if (string.IsNullOrWhiteSpace(pluginPrefix))
            return null;

        return _pluginManager.GetDevelopmentAutomationPlugins()
            .FirstOrDefault(p => string.Equals(p.PluginPrefix, pluginPrefix, StringComparison.OrdinalIgnoreCase))
            ?.PluginName ?? pluginPrefix;
    }

    private void OnCliProcessStatusChanged(Guid workItemId, CliProcessStatus status)
    {
        if (workItemId != _workItemId)
            return;

        _dispatcherInvoke(() =>
        {
            try
            {
                var isRunning = _aiService.IsRunning(_workItemId);
                IsCliRunning = isRunning;
                if (_workItem is not null)
                {
                    _workItem.ExecutionStatus = isRunning
                        ? WorkItemExecutionStatus.Active
                        : WorkItemExecutionStatus.Completed;
                    OnPropertyChanged(nameof(StartCanCliNew));
                    OnPropertyChanged(nameof(CompleteCanWorkItem));
                    OnPropertyChanged(nameof(ShowCliPanel));
                    SelectDefaultView();
                }

                if (status == CliProcessStatus.Started)
                {
                    SetActiveCliName(_workItem?.AiPluginPrefix);
                    CliStatusText = _aiService.GetTerminalSession(_workItemId) is { IsPseudoTerminal: false }
                        ? "Gestartet" + EingeschraenkterModeNotice
                        : "Gestartet";
                }
                else
                {
                    if (!isRunning)
                    {
                        AttachCliStatusSession(null);
                        // Ein geplanter zeitgesteuerter Prompt überlebt den CLI-Stopp (persistierter
                        // Plan) — die Statusanzeige wird ausschließlich über ScheduledPromptChanged
                        // bzw. PromptSent synchronisiert.
                    }

                    CliStatusText = status == CliProcessStatus.Error
                        ? "CLI-Status: Fehler"
                        : "CLI inaktiv";
                    CliStopped?.Invoke();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Verarbeiten des CLI-Status-Wechsels für Aufgabe {AufgabeId}.", workItemId);
            }
        });
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        _aiService.CliProcessStatusChanged -= OnCliProcessStatusChanged;
        _promptScheduleDispatchService.PromptSent -= OnPromptSent;
        _promptScheduleDispatchService.ScheduledPromptChanged -= OnScheduledPromptChanged;
        // Kein CancelScheduledPrompt mehr: Ein persistierter Plan überlebt das Verlassen der Ansicht.
        AttachCliStatusSession(null);
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = null;
        _logLoadCts?.Cancel();
        _logLoadCts?.Dispose();
        _logLoadCts = null;
    }

    private async Task StartAsync(CancellationToken ct)
    {
        if (_workItemId == Guid.Empty || _workItem is null)
            return;

        ErrorMessage = null;

        try
        {
            var pluginPrefix = await _pluginSelectionService.ResolveDevelopmentAutomationPluginWithProjectScopeAsync(
                _workItem.AiPluginPrefix,
                _workItem.ProjectId,
                ct);

            if (string.IsNullOrEmpty(pluginPrefix))
            {
                pluginPrefix = await ResolvePluginViaDialogAsync(_workItem, ct);
                if (string.IsNullOrEmpty(pluginPrefix))
                    return;
            }

            var repositoryUrl = _workItem.GitRepository?.RepositoryUrl ?? string.Empty;

            if (_workItem.ExecutionStatus == WorkItemExecutionStatus.Completed)
            {
                await StartCliAndUpdateStateAsync(pluginPrefix, _workItem.LocalClonePath ?? string.Empty, _optionalCliParameter, ct);
            }
            else
            {
                CliStatusText = RepositoryPreparationStatusText;

                await _developmentprocessService.StartProcessAndCliAsync(
                    _workItemId,
                    repositoryUrl,
                    null,
                    pluginPrefix,
                    ct);
            }

            await LoadAsync(ct);
            SetActiveCliName(pluginPrefix);

            var session = _aiService.GetTerminalSession(_workItemId);
            if (session != null)
            {
                AttachCliStatusSession(session);
                TerminalSessionStarted?.Invoke(session);
            }
        }
        catch (OperationCanceledException)
        {
            AttachCliStatusSession(null);
            throw;
        }
        catch (Exception ex)
        {
            ActiveCliName = null;
            AttachCliStatusSession(null);
            _logger.LogError(ex, "Fehler beim Starten der Aufgabe {AufgabeId}.", _workItemId);
            ErrorMessage = $"Aufgabe konnte nicht gestartet werden: {ex.Message}";
        }
    }

    private async Task SwitchPluginAsync(CancellationToken ct)
    {
        if (_workItemId == Guid.Empty || _workItem is null)
            return;

        ErrorMessage = null;

        var pluginPrefix = await ResolvePluginViaDialogAsync(_workItem, ct);
        if (string.IsNullOrEmpty(pluginPrefix))
            return;

        try
        {
            await _aiService.StopCliAsync(_workItemId, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Stoppen des CLI für Aufgabe {AufgabeId} während Plugin-Wechsel.", _workItemId);
            ErrorMessage = $"CLI konnte nicht gestoppt werden: {ex.Message}";
            return;
        }

        try
        {
            _dispatcherInvoke(() =>
            {
                IsCliRunning = false;
            });

            var localClonePath = _workItem.LocalClonePath ?? string.Empty;
            await StartCliAndUpdateStateAsync(pluginPrefix, localClonePath, _optionalCliParameter, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            ActiveCliName = null;
            _logger.LogError(ex, "Fehler beim Neustarten des CLI für Aufgabe {AufgabeId} nach Plugin-Wechsel.", _workItemId);
            ErrorMessage = $"CLI konnte nicht neu gestartet werden: {ex.Message}";
        }
    }

    private async Task StartCliAndUpdateStateAsync(string pluginPrefix, string localClonePath, string? optionalParameter, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(localClonePath))
        {
            throw new InvalidOperationException($"Aufgabe {_workItemId} hat keinen lokalen Klonpfad.");
        }

        IsCliRunning = true;
        ActiveCliName = ResolveAiPluginName(pluginPrefix) ?? pluginPrefix;
        try
        {
            await _developmentprocessService.RestartCliAsync(_workItemId, pluginPrefix, optionalParameter, ct);

            SelectedAiPluginPrefix = pluginPrefix;
            if (!_aiService.IsRunning(_workItemId))
            {
                await _workItemService.EndActiveRunAsync(_workItemId, ct);
                IsCliRunning = false;
                ActiveCliName = null;
                return;
            }

            // Vollständige Laufdaten sicherstellen statt nur AusfuehrungsStatus=Aktiv (Issue 342,
            // Divergenzpfad F2): Der Pfad persistiert denselben Lauf-Zustand wie ein echter
            // Gestartet-Lauf — auch wenn CliNeustartenAsync in einen Wiederverwendungs-Early-Return
            // ohne Gestartet-Ereignis lief. Der laeuftGuard und der Live-Substatus-Provider werden
            // innerhalb der serialisierten Schreibphase erneut ausgewertet: Endet der Prozess zwischen
            // dem IsRunning-Check oben und der Persistenz, wird kein Phantom-Lauf geschrieben, und der
            // geschriebene Substatus ist der frischeste Session-Stand (TOCTOU-/Stale-Write-Fix).
            await _workItemService.EnsureActiveRunAsync(
                _workItemId,
                runStatusProvider: () => _aiService.GetTerminalSession(_workItemId)?.RuntimeStatus.ToWorkItemRunStatus(),
                ct,
                runningGuard: () => _aiService.IsRunning(_workItemId));
            _runDataChangedNotifier?.NotifyRunDataChanged(_workItemId);

            var session = _aiService.GetTerminalSession(_workItemId);
            if (session != null)
            {
                AttachCliStatusSession(session);
                TerminalSessionStarted?.Invoke(session);
            }
        }
        catch
        {
            IsCliRunning = false;
            ActiveCliName = null;
            throw;
        }
    }

    private async Task<string?> ResolvePluginViaDialogAsync(WorkItem workItem, CancellationToken ct)
    {
        if (AvailableAiPlugins.Count == 1)
        {
            var singlePluginPrefix = AvailableAiPlugins[0];
            await _workItemService.UpdateAsync(_workItemId, workItem.Title, workItem.RequirementDescription, singlePluginPrefix, ct);
            workItem.AiPluginPrefix = singlePluginPrefix;
            SelectedAiPluginPrefix = singlePluginPrefix;
            SetActiveCliName(singlePluginPrefix);
            _runDataChangedNotifier?.NotifyRunDataChanged(_workItemId);
            return singlePluginPrefix;
        }

        var dialogResult = await _dialogService.ShowPluginSelectionDialogAsync(
            AvailableAiPlugins,
            _selectedAiPluginPrefix,
            ct);

        if (string.IsNullOrEmpty(dialogResult.SelectedPluginPrefix))
            return null;

        if (dialogResult.SaveAsProjectDefault)
        {
            await _pluginSelectionService.SaveProjectDefaultPluginPrefixAsync(workItem.ProjectId, PluginType.DevelopmentAutomation, dialogResult.SelectedPluginPrefix, ct);
        }

        await _workItemService.UpdateAsync(_workItemId, workItem.Title, workItem.RequirementDescription, dialogResult.SelectedPluginPrefix, ct);
        workItem.AiPluginPrefix = dialogResult.SelectedPluginPrefix;
        SelectedAiPluginPrefix = dialogResult.SelectedPluginPrefix;
        SetActiveCliName(dialogResult.SelectedPluginPrefix);
        _runDataChangedNotifier?.NotifyRunDataChanged(_workItemId);

        return dialogResult.SelectedPluginPrefix;
    }

    private void AttachCliStatusSession(ITerminalSession? session)
    {
        if (ReferenceEquals(_cliStatusSession, session))
        {
            UpdateCliStatusText(session?.RuntimeStatus ?? CliRuntimeStatus.Inactive);
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
            UpdateCliStatusText(CliRuntimeStatus.Inactive);
        }
    }

    private void OnCliRuntimeStatusChanged(object? sender, CliRuntimeStatusChangedEventArgs e)
    {
        _dispatcherInvoke(() => UpdateCliStatusText(e.Status));
    }

    private void UpdateCliStatusText(CliRuntimeStatus status)
    {
        var statusText = status switch
        {
            CliRuntimeStatus.Running => "CLI-Status: Ausführung läuft",
            CliRuntimeStatus.WaitingForInput => "CLI-Status: Wartet auf Eingabe",
            CliRuntimeStatus.Inactive => "CLI inaktiv",
            _ => "CLI-Status: unbekannt"
        };

        CliStatusText = _cliStatusSession is { IsPseudoTerminal: false }
            ? statusText + EingeschraenkterModeNotice
            : statusText;
    }

    private async Task SchedulePromptAsync(CancellationToken ct)
    {
        if (_selectedPromptTemplate is null || string.IsNullOrWhiteSpace(_selectedPromptTemplate.PromptText))
            return;

        if (_scheduledPromptTargetHours is < 0 or > 23)
        {
            ErrorMessage = "Ungültige Stunde (0–23)";
            return;
        }

        if (_scheduledPromptTargetMinutes is < 0 or > 59)
        {
            ErrorMessage = "Ungültige Minute (0–59)";
            return;
        }

        if (!_scheduledPromptTargetHours.HasValue && !_scheduledPromptTargetMinutes.HasValue)
            return;

        ErrorMessage = null;

        var now = _timeProvider.GetLocalNow();
        var hour = _scheduledPromptTargetHours ?? 0;
        var minute = _scheduledPromptTargetMinutes ?? 0;
        var targetTime = new DateTimeOffset(new DateTime(now.Year, now.Month, now.Day, hour, minute, 0), now.Offset);

        var prompt = _promptTemplatesPlaceholderService.Resolve(_selectedPromptTemplate.PromptText, _workItem);
        if (string.IsNullOrWhiteSpace(prompt))
            return;

        await _promptScheduleDispatchService.SchedulePromptAsync(_workItemId, prompt, targetTime);

        // targetTime kann bereits erreicht/vergangen sein (z. B. Uhrzeit des heutigen Tages liegt vor "jetzt")
        // oder vom Service auf ein Pausenende verschoben worden sein — die Anzeige wird daher immer aus
        // dem tatsächlichen Service-Zustand befüllt (ScheduledPromptChanged feuert bereits synchron).
        UpdateQueueDisplay();

        ScheduledPromptTargetHours = null;
        ScheduledPromptTargetMinutes = null;
        SelectedPromptTemplate = null;
    }

    private void OnPromptSent(Guid workItemId)
    {
        if (workItemId != _workItemId)
            return;

        _dispatcherInvoke(() =>
        {
            ScheduledPromptStatus = null;
            ScheduledPromptTimeDisplay = null;
            SelectView(DetailView.Cli);
        });
    }

    private void OnScheduledPromptChanged(Guid workItemId)
    {
        if (workItemId != _workItemId)
            return;

        _dispatcherInvoke(UpdateQueueDisplay);
    }

    /// <summary>Spiegelt den Wartestellungs-Zustand des <see cref="PromptScheduleDispatchService"/> in die Statusanzeige.</summary>
    private void UpdateQueueDisplay()
    {
        if (_promptScheduleDispatchService.GetScheduledPromptStatus(_workItemId) is { } status)
        {
            if (status.WaitingForSession)
            {
                // Zielzeit ist verstrichen, aber es gab keine zustellfähige CLI-Session: Der Plan
                // bleibt gültig und wartet auf den nächsten CLI-Start. Bewusst keine vergangene
                // Uhrzeit anzeigen — die würde den Plan als verfallen/defekt wirken lassen.
                ScheduledPromptStatus = "Prompt wartet auf CLI-Start";
                ScheduledPromptTimeDisplay = null;
            }
            else
            {
                ScheduledPromptStatus = "Prompt in Wartestellung";
                ScheduledPromptTimeDisplay = status.TargetTime.ToLocalTime().ToString("HH:mm");
            }
        }
        else
        {
            ScheduledPromptStatus = null;
            ScheduledPromptTimeDisplay = null;
        }
    }

    private Task<string> ResolveEffectiveWorkingDirectoryAsync(string localClonePath, CancellationToken ct)
    {
        var startConfig = _workItem?.GitRepository?.StartConfiguration;

        return WorkingDirectoryResolver.DetermineEffectiveWorkingDirectoryAsync(
            localClonePath,
            startConfig,
            gitPlugin: null,
            ct: ct);
    }

    /// <summary>
    /// Löst das zuständige IDE-Plugin über <see cref="PluginSelectionService.ResolveIdePluginAsync"/> auf und
    /// liefert dessen Einstiegspunkte über <see cref="IIdePlugin.FindEntryPointsAsync"/> für das übergebene,
    /// bereits ermittelte effektive Arbeitsverzeichnis. Gemeinsam genutzt von <see cref="OpenIdeInternalAsync"/>
    /// (öffnet anschließend einen Einstiegspunkt) und <see cref="UpdateCanSelectIdeAsync"/> (ermittelt
    /// nur die Anzahl, ohne zu öffnen).
    /// </summary>
    /// <param name="effectiveWorkdir">Das bereits über <see cref="ResolveEffectiveWorkingDirectoryAsync"/> aufgelöste effektive Arbeitsverzeichnis.</param>
    /// <param name="ct">Abbruchtoken.</param>
    /// <returns>Das aufgelöste IDE-Plugin sowie dessen gefundene Einstiegspunkte.</returns>
    private async Task<(IIdePlugin Plugin, IReadOnlyList<IdeEntryPoint> EntryPoints)> ResolveIdeEntryPointsAsync(string effectiveWorkdir, CancellationToken ct)
    {
        var plugin = await _pluginSelectionService.ResolveIdePluginAsync(effectiveWorkdir, ct);
        var entryPoints = await plugin.FindEntryPointsAsync(effectiveWorkdir, ct);
        return (plugin, entryPoints);
    }

    /// <summary>
    /// Löst für das übergebene, bereits ermittelte effektive Arbeitsverzeichnis über
    /// <see cref="PluginSelectionService.ResolveAllCompatibleIdePluginsAsync"/> ALLE aktivierten, zum
    /// Repository <c>Explicit</c>- oder <c>Fallback</c>-kompatiblen IDE-Plugins auf (statt nur eines einzelnen
    /// wie <see cref="ResolveIdeEntryPointsAsync"/>). Ruft anschließend <see cref="IIdePlugin.FindEntryPointsAsync"/>
    /// auf jedem dieser Plugins auf und aggregiert die Ergebnisse zu einer Liste von
    /// <c>(Plugin, EntryPoint)</c>-Tupeln (Plugin-Reihenfolge sowie Einstiegspunkt-Reihenfolge je Plugin bleiben
    /// erhalten). Schlägt die Einstiegspunkt-Ermittlung für ein einzelnes Plugin fehl, wird der Fehler geloggt
    /// und mit den übrigen Plugins fortgefahren, statt die gesamte Aggregation abzubrechen. Genutzt vom
    /// Dropdown-Button-Pfad in <see cref="OpenIdeInternalAsync"/> sowie von
    /// <see cref="UpdateCanSelectIdeAsync"/>.
    /// </summary>
    /// <param name="effectiveWorkdir">Das bereits über <see cref="ResolveEffectiveWorkingDirectoryAsync"/> aufgelöste effektive Arbeitsverzeichnis.</param>
    /// <param name="ct">Abbruchtoken.</param>
    /// <returns>Die aggregierte Liste der (Plugin, EntryPoint)-Tupel aller kompatiblen Plugins, die erfolgreich Einstiegspunkte geliefert haben.</returns>
    private async Task<IReadOnlyList<(IIdePlugin Plugin, IdeEntryPoint EntryPoint)>> ResolveAggregierteIdeEinstiegspunkteAsync(string effectiveWorkdir, CancellationToken ct)
    {
        var plugins = await _pluginSelectionService.ResolveAllCompatibleIdePluginsAsync(effectiveWorkdir, ct);

        var entries = new List<(IIdePlugin Plugin, IdeEntryPoint EntryPoint)>();
        foreach (var plugin in plugins)
        {
            try
            {
                var entryPoints = await plugin.FindEntryPointsAsync(effectiveWorkdir, ct);
                entries.AddRange(entryPoints.Select(entryPoint => (plugin, entryPoint)));
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

        return entries;
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
    private static string FormatDisplayValue(IIdePlugin plugin, IdeEntryPoint entryPoint)
    {
        var designation = entryPoint.DisplayName ?? Path.GetFileName(entryPoint.Path);
        return string.Equals(designation, plugin.PluginName, StringComparison.Ordinal)
            ? plugin.PluginName
            : $"{plugin.PluginName}: {designation}";
    }

    /// <summary>
    /// Berechnet <see cref="SelectCanIde"/> einmalig am Ende von <see cref="LoadAsync"/>, damit der
    /// Dropdown-Button des <see cref="Controls.RibbonSplitButton"/> bereits beim ersten Anzeigen der View
    /// korrekt sichtbar/unsichtbar ist (ohne einen Einstiegspunkt zu öffnen). Ermittlungsfehler, fehlendes
    /// Plugin oder fehlendes Arbeitsverzeichnis werden hier nicht als <see cref="ErrorMessage"/> angezeigt,
    /// sondern führen lediglich zu <c>KannIdeAuswaehlen = false</c>.
    /// </summary>
    /// <param name="ct">Abbruchtoken.</param>
    private async Task UpdateCanSelectIdeAsync(CancellationToken ct)
    {
        if (_workItem?.LocalClonePath is not { } localClonePath)
        {
            SelectCanIde = false;
            return;
        }

        try
        {
            var effectiveWorkdir = await ResolveEffectiveWorkingDirectoryAsync(localClonePath, ct);
            var entries = await ResolveAggregierteIdeEinstiegspunkteAsync(effectiveWorkdir, ct);
            SelectCanIde = CalculateCanIdeSelect(entries.Count);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Einstiegspunkte für Aufgabe {AufgabeId} konnten beim Laden nicht ermittelt werden.", _workItemId);
            SelectCanIde = false;
        }
    }

    private async Task OpenWorkingDirectoryAsync(CancellationToken ct)
    {
        if (_workItem?.LocalClonePath is not { } localClonePath)
            return;

        ErrorMessage = null;

        try
        {
            var effectiveWorkdir = await ResolveEffectiveWorkingDirectoryAsync(localClonePath, ct);

            _workingDirectoryOpenService.Open(effectiveWorkdir);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Öffnen des Arbeitsverzeichnisses {LokalerKlonPfad}.", localClonePath);
            ErrorMessage = $"Arbeitsverzeichnis konnte nicht geöffnet werden: {ex.Message}";
        }
    }

    /// <summary>
    /// Öffnet das effektive Arbeitsverzeichnis in der zuständigen IDE (Haupt-Button des Split-Buttons):
    /// löst über <see cref="OpenIdeInternalAsync"/> Plugin und Einstiegspunkte auf und öffnet bei mehreren
    /// gefundenen Einstiegspunkten direkt den ersten (Fallback-Verhalten), ohne Auswahl-Dialog. Die gezielte
    /// Auswahl übernimmt <see cref="OpenIdeSelectionAsync"/> über den Dropdown-Button.
    /// </summary>
    /// <param name="ct">Abbruchtoken.</param>
    private Task OpenIdeAsync(CancellationToken ct) => OpenIdeInternalAsync(null, ct);

    /// <summary>
    /// Öffnet das effektive Arbeitsverzeichnis in der zuständigen IDE (Dropdown-Button des Split-Buttons):
    /// löst über <see cref="OpenIdeInternalAsync"/> Plugin und Einstiegspunkte auf und erzwingt bei mehreren
    /// gefundenen Einstiegspunkten die Auswahl über den <see cref="SelectEntryPointAsync"/>-Callback, statt
    /// automatisch den ersten zu öffnen.
    /// </summary>
    /// <param name="ct">Abbruchtoken.</param>
    private Task OpenIdeSelectionAsync(CancellationToken ct) => OpenIdeInternalAsync(SelectEntryPointAsync, ct);

    /// <summary>
    /// Gemeinsame Implementierung für <see cref="OpenIdeAsync"/> und <see cref="OpenIdeSelectionAsync"/>:
    /// ermittelt das effektive Arbeitsverzeichnis, löst das zuständige IDE-Plugin über
    /// <see cref="PluginSelectionService.ResolveIdePluginAsync"/> auf und ermittelt dessen Einstiegspunkte
    /// über <see cref="IIdePlugin.FindEntryPointsAsync"/> — jeweils genau einmal pro Aufruf — und öffnet
    /// anschließend über <see cref="IIdePlugin.OpenEntryPointAsync"/> direkt auf Basis desselben Ergebnisses,
    /// mit dem auch <see cref="SelectCanIde"/> aktualisiert wird. Existiert genau ein Einstiegspunkt,
    /// wird dieser direkt geöffnet. Existieren mehrere und ist <paramref name="selectEntryPointAsync"/>
    /// gesetzt, wird der Callback zur Auswahl aufgerufen; liefert er <c>null</c> (Abbruch), wird nichts
    /// geöffnet. Existieren mehrere ohne Callback, wird der erste geöffnet (Fallback).
    /// </summary>
    /// <param name="selectEntryPointAsync">
    /// Optionaler Callback zur Auswahl eines Einstiegspunkts bei mehreren Treffern, oder <c>null</c> für das
    /// Fallback-Verhalten des Haupt-Buttons.
    /// </param>
    /// <param name="ct">Abbruchtoken.</param>
    /// <returns>Ein Task, der abgeschlossen ist, sobald die IDE geöffnet wurde (oder der Vorgang abgebrochen bzw. mit Fehleranzeige beendet wurde).</returns>
    private async Task OpenIdeInternalAsync(
        Func<IReadOnlyList<(IIdePlugin Plugin, IdeEntryPoint EntryPoint)>, CancellationToken, Task<(IIdePlugin Plugin, IdeEntryPoint EntryPoint)?>>? selectEntryPointAsync,
        CancellationToken ct)
    {
        ErrorMessage = null;

        if (_workItem?.LocalClonePath is not { } localClonePath)
            return;

        try
        {
            var effectiveWorkdir = await ResolveEffectiveWorkingDirectoryAsync(localClonePath, ct);

            if (selectEntryPointAsync is null)
            {
                // Haupt-Button: bestehender Single-Plugin-Pfad bleibt unverändert.
                var (plugin, entryPoints) = await ResolveIdeEntryPointsAsync(effectiveWorkdir, ct);

                // Zusätzlich: KannIdeAuswaehlen aus der aggregierten Ermittlung über alle kompatiblen Plugins aktualisieren.
                var aggregierteEntries = await ResolveAggregierteIdeEinstiegspunkteAsync(effectiveWorkdir, ct);
                SelectCanIde = CalculateCanIdeSelect(aggregierteEntries.Count);

                if (entryPoints.Count == 0)
                    throw new FileNotFoundException($"Keine Einstiegspunkte im Repository gefunden: {effectiveWorkdir}");

                await plugin.OpenEntryPointAsync(entryPoints[0], ct);
                return;
            }

            // Dropdown-Button: ausschließlich die aggregierte Ermittlung über alle kompatiblen Plugins.
            var entries = await ResolveAggregierteIdeEinstiegspunkteAsync(effectiveWorkdir, ct);
            SelectCanIde = CalculateCanIdeSelect(entries.Count);

            if (entries.Count == 0)
                throw new FileNotFoundException($"Keine Einstiegspunkte im Repository gefunden: {effectiveWorkdir}");

            if (entries.Count == 1)
            {
                await entries[0].Plugin.OpenEntryPointAsync(entries[0].EntryPoint, ct);
                return;
            }

            var selected = await selectEntryPointAsync(entries, ct);
            if (selected is null)
                return;

            await selected.Value.Plugin.OpenEntryPointAsync(selected.Value.EntryPoint, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Öffnen der IDE für Aufgabe {AufgabeId}.", _workItemId);
            ErrorMessage = $"IDE konnte nicht geöffnet werden: {ex.Message}";
        }
    }

    /// <summary>
    /// Callback für <see cref="OpenIdeInternalAsync"/> (Dropdown-Button): Formatiert die aggregierten
    /// (Plugin, EntryPoint)-Tupel via <see cref="FormatDisplayValue"/> plugin-qualifiziert, zeigt sie im
    /// Auswahl-Dialog an und liefert das zum gewählten Anzeigewert gehörende Tupel über den Listenindex
    /// zurück (nicht über Stringgleichheit, da mehrere Einträge theoretisch denselben Anzeige-String liefern
    /// könnten), oder <c>null</c> bei Abbruch durch den Anwender.
    /// </summary>
    /// <param name="entries">Die zur Auswahl stehenden (Plugin, EntryPoint)-Tupel aus allen kompatiblen IDE-Plugins.</param>
    /// <param name="ct">Abbruchtoken.</param>
    /// <returns>Das gewählte (Plugin, EntryPoint)-Tupel, oder <c>null</c> bei Abbruch.</returns>
    private async Task<(IIdePlugin Plugin, IdeEntryPoint EntryPoint)?> SelectEntryPointAsync(IReadOnlyList<(IIdePlugin Plugin, IdeEntryPoint EntryPoint)> entries, CancellationToken ct)
    {
        var displayValues = entries.Select(e => FormatDisplayValue(e.Plugin, e.EntryPoint)).ToList();
        var selectedValue = await _dialogService.ShowSolutionSelectionDialogAsync(displayValues, ct);
        if (selectedValue is null)
            return null;

        var index = displayValues.IndexOf(selectedValue);
        return index >= 0 ? entries[index] : null;
    }

    private async Task ExportCliRawAsync(CancellationToken ct)
    {
        if (!ExportCanCliRaw)
            return;

        var defaultFilename = $"cli-output-{_workItemId:N}.raw";
        var targetPath = await _dialogService.ShowSaveFileDialogAsync(
            "CLI-Rohausgabe exportieren",
            "Raw files (*.raw)|*.raw",
            defaultFilename,
            null,
            ct);

        if (string.IsNullOrWhiteSpace(targetPath))
            return;

        if (!targetPath.EndsWith(".raw", StringComparison.OrdinalIgnoreCase))
        {
            ErrorMessage = "Export-Zielpfad muss auf .raw enden.";
            return;
        }

        ErrorMessage = null;

        try
        {
            await _cliRawExportService.ExportCliRawAsync(_workItemId, targetPath, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CLI-Rohausgabe für Aufgabe {AufgabeId} konnte nicht exportiert werden.", _workItemId);
            ErrorMessage = $"CLI-Rohausgabe konnte nicht exportiert werden: {ex.Message}";
        }
    }

    private async Task ExportCliReplayAsync(CancellationToken ct)
    {
        if (!ExportCanCliReplay)
            return;

        // Vorab prüfen, ob überhaupt ein Mitschnitt existiert — der Speicherdialog darf
        // nicht umsonst geöffnet werden.
        if (!_cliReplayExportService.HasRecording(_workItemId))
        {
            ErrorMessage = "Für diese Aufgabe liegt keine Aufzeichnung vor — der Mitschnitt wird während einer CLI-Ausführung automatisch erstellt, aber nur flüchtig im Arbeitsspeicher gehalten (maximal die letzten 8 Aufgaben) und geht bei einem Neustart der Anwendung oder durch neuere Ausführungen verloren.";
            return;
        }

        var defaultFilename = $"cli-replay-{_workItemId:N}.clireplay";
        var targetPath = await _dialogService.ShowSaveFileDialogAsync(
            "CLI-Aufzeichnung exportieren",
            "CLI-Replay-Dateien (*.clireplay)|*.clireplay",
            defaultFilename,
            null,
            ct);

        if (string.IsNullOrWhiteSpace(targetPath))
            return;

        if (!targetPath.EndsWith(".clireplay", StringComparison.OrdinalIgnoreCase))
        {
            ErrorMessage = "Export-Zielpfad muss auf .clireplay enden.";
            return;
        }

        ErrorMessage = null;

        try
        {
            await _cliReplayExportService.ExportCliReplayAsync(_workItemId, targetPath, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CLI-Aufzeichnung für Aufgabe {AufgabeId} konnte nicht exportiert werden.", _workItemId);
            ErrorMessage = $"CLI-Aufzeichnung konnte nicht exportiert werden: {ex.Message}";
        }
    }

    private async Task SetPauseAsync(CancellationToken ct)
    {
        if (_workItemId == Guid.Empty || _workItem is null)
            return;

        ErrorMessage = null;

        try
        {
            var dialogVm = _serviceProvider.GetRequiredService<PauseWorkItemDialogViewModel>();
            dialogVm.Initialize(_workItem.PausedUntilUtc);

            var result = await _dialogService.ShowWorkItemPauseDialogAsync(dialogVm, ct);
            if (result is null)
                return;

            var pausedUntilUtc = result.Aufheben ? null : result.PausedUntilUtc;
            await _workItemService.SetPauseAsync(_workItemId, pausedUntilUtc, ct);
            await LoadAsync(ct);
            _runDataChangedNotifier?.NotifyRunDataChanged(_workItemId);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Einstellen der Pause für Aufgabe {AufgabeId}.", _workItemId);
            ErrorMessage = $"Pause konnte nicht gesetzt werden: {ex.Message}";
        }
    }

    /// <summary>
    /// Berechnet, ob mehr als ein Einstiegspunkt vorhanden ist und somit der Dropdown-Button des
    /// <see cref="Controls.RibbonSplitButton"/> zur Auswahl angeboten werden soll.
    /// </summary>
    /// <param name="entryPointCount">Die Gesamtanzahl der ermittelten Einstiegspunkte.</param>
    /// <returns><c>true</c>, wenn mehr als ein Einstiegspunkt vorhanden ist, sonst <c>false</c>.</returns>
    private static bool CalculateCanIdeSelect(int entryPointCount) => entryPointCount >= 2;
}
