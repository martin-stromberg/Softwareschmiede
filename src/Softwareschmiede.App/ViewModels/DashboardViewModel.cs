using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using Softwareschmiede.App.Services;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;

namespace Softwareschmiede.App.ViewModels;

/// <summary>ViewModel für das Dashboard mit Zusammenfassung aller Projekte und Aufgaben.</summary>
public sealed class DashboardViewModel : ViewModelBase
{
    private readonly ProjectService _projectService;
    private readonly WorkItemService _workItemService;
    private readonly WorkItemRecoveryService _recoveryService;
    private readonly WorkItemListModeBinding _modeBinding;
    private readonly ILogger<DashboardViewModel> _logger;

    private int _projectCount;
    private int _activeWorkItems;
    private int _waitingWorkItems;
    private bool _isLoading;
    private string? _errorMessage;
    private ObservableCollection<ActiveWorkItemPanelItem> _activeWorkItemsList = new();

    /// <summary>Anzahl aller Projekte.</summary>
    public int ProjectCount
    {
        get => _projectCount;
        private set => SetProperty(ref _projectCount, value);
    }

    /// <summary>Anzahl aktiver Aufgaben (Status Gestartet).</summary>
    public int ActiveWorkItems
    {
        get => _activeWorkItems;
        private set => SetProperty(ref _activeWorkItems, value);
    }

    /// <summary>Anzahl wartender Aufgaben (Status Wartend).</summary>
    public int WaitingWorkItems
    {
        get => _waitingWorkItems;
        private set => SetProperty(ref _waitingWorkItems, value);
    }

    /// <summary>Gibt an, ob Daten geladen werden.</summary>
    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    /// <summary>Fehlermeldung bei Ladefehlern.</summary>
    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    /// <summary>Liste von Recovery-Kandidaten.</summary>
    public ObservableCollection<Guid> RecoveryKandidaten { get; } = new();

    /// <summary>Gibt an, ob es Recovery-Kandidaten gibt.</summary>
    public bool HasRecoveryKandidaten => RecoveryKandidaten.Count > 0;

    /// <summary>Liste der zuletzt aktiven Projekte.</summary>
    public ObservableCollection<Project> LastProjects { get; } = new();

    /// <summary>Liste der aktuell aktiven Aufgaben (Status Gestartet oder Wartend). Wird vom MainWindowViewModel über <see cref="Initialize"/> als gemeinsame Datenquelle gesetzt.</summary>
    public ObservableCollection<ActiveWorkItemPanelItem> ActiveWorkItemsList
    {
        get => _activeWorkItemsList;
        private set => SetProperty(ref _activeWorkItemsList, value);
    }

    /// <summary>Delegat zur Navigation zu einer aktiven Aufgabe, wird vom MainWindowViewModel über <see cref="Initialize"/> gesetzt.</summary>
    private Action<Guid>? NavigateToWorkItemAction { get; set; }

    /// <summary>Navigiert zur Aufgabendetailansicht einer aktiven Aufgabe.</summary>
    public ICommand NavigateToWorkItemCommand { get; }

    /// <summary>Lädt die Dashboard-Daten neu.</summary>
    public ICommand LoadCommand { get; }

    /// <summary>
    /// Anzeigemodus der Dashboard-Aufgabenliste (flach nach Datum oder gruppiert nach Repository/KI).
    /// Änderungen werden über den <see cref="WorkItemListModeService"/> unter dem
    /// Dashboard-eigenen Einstellungsschlüssel persistiert — unabhängig vom Seitenleisten-Modus.
    /// </summary>
    public WorkItemListMode WorkItemListMode
    {
        get => _modeBinding.Value;
        set => _modeBinding.Value = value;
    }

    /// <inheritdoc cref="DashboardViewModel"/>
    public DashboardViewModel(
        ProjectService projectService,
        WorkItemService workItemService,
        WorkItemRecoveryService recoveryService,
        ILogger<DashboardViewModel> logger,
        WorkItemListModeService? workItemsListModeService = null)
    {
        _projectService = projectService;
        _workItemService = workItemService;
        _recoveryService = recoveryService;
        _modeBinding = WorkItemListModeBinding.ForDashboard(workItemsListModeService, logger);
        _modeBinding.ValueChanged += () => OnPropertyChanged(nameof(WorkItemListMode));
        _logger = logger;

        LoadCommand = new AsyncRelayCommand(LoadAsync);
        NavigateToWorkItemCommand = new RelayCommand<Guid>(id => NavigateToWorkItemAction?.Invoke(id));
        RecoveryKandidaten.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasRecoveryKandidaten));
    }

    private async Task LoadAsync(CancellationToken ct)
    {
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var projects = await _projectService.GetAllAsync(ct);
            ProjectCount = projects.Count;

            RecoveryKandidaten.Clear();
            var candidates = await _recoveryService.ScanForRecoveryCandidatesAsync(ct);
            foreach (var id in candidates)
                RecoveryKandidaten.Add(id);

            LastProjects.Clear();
            foreach (var project in projects.Take(5))
                LastProjects.Add(project);

            var (activeCount, waitingCount) = await _workItemService.GetActiveAndWaitingCountAsync(ct);
            ActiveWorkItems = activeCount;
            WaitingWorkItems = waitingCount;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Laden der Dashboard-Daten.");
            ErrorMessage = $"Fehler beim Laden: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>Verdrahtet die vom MainWindowViewModel bereitgestellte gemeinsame Aufgabenliste und die Navigationsaktion, statt sie über extern beschreibbare Properties zu setzen.</summary>
    /// <param name="activeWorkItemsList">Die gemeinsame Datenquelle für aktive Aufgaben, die auch das MainWindowViewModel für die Seitenleiste verwendet.</param>
    /// <param name="navigateToWorkItemAction">Der Delegat, der die Navigation zur Aufgabendetailansicht auslöst.</param>
    public void Initialize(ObservableCollection<ActiveWorkItemPanelItem> activeWorkItemsList, Action<Guid> navigateToWorkItemAction)
    {
        ActiveWorkItemsList = activeWorkItemsList;
        NavigateToWorkItemAction = navigateToWorkItemAction;
        LoadSavedWorkItemsListModeAsync()
            .SafeFireAndForget(_logger, "Dashboard-Aufgabenlisten-Modus laden");
    }

    /// <summary>
    /// Lädt den persistierten Anzeigemodus der Dashboard-Aufgabenliste und wendet ihn an, ohne den
    /// Wert zurückzuschreiben. Wird aus <see cref="Initialize"/> per <c>SafeFireAndForget</c>
    /// angestoßen und ist für Tests direkt awaitbar. Ein zwischenzeitlich vom Anwender gewählter
    /// Modus wird nicht überschrieben (Race-Guard in <see cref="WorkItemListModeBinding"/>).
    /// </summary>
    /// <param name="ct">Token zum Abbrechen der Operation.</param>
    public Task LoadSavedWorkItemsListModeAsync(CancellationToken ct = default)
        => _modeBinding.LoadSavedValueAsync(ct);
}
