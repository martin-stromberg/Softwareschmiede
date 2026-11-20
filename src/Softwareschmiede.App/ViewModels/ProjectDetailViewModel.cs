using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Softwareschmiede.App.Services;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.App.ViewModels;

/// <summary>ViewModel für die Projektdetailansicht mit Aufgabenliste.</summary>
public sealed class ProjectDetailViewModel : ViewModelBase, IDisposable
{
    private readonly ProjectService _projectService;
    private readonly WorkItemService _workItemService;
    private readonly IServiceProvider _serviceProvider;
    private readonly IDialogService _dialogService;
    private readonly IPluginManager _pluginManager;
    private readonly ILogger<ProjectDetailViewModel> _logger;
    private readonly DirectoryStructureBrowserService? _directoryStructureService;

    /// <summary>Wird aufgerufen, wenn der Nutzer zur Listenansicht zurückkehren möchte.</summary>
    public Action? BackAction { get; set; }

    /// <summary>Wird nach dem Erstellen oder Löschen eines Projekts aufgerufen, damit die Listenansicht die Liste aktualisiert.</summary>
    public Func<Task>? RefreshProjectListCallback { get; set; }

    /// <summary>Wird aufgerufen, um zur separaten Aufgabendetailansicht zu navigieren.</summary>
    public Action<TaskDetailViewModel>? NavigateToTaskViewCallback { get; set; }

    /// <summary>Wird aufgerufen, um von der Aufgabendetailansicht zurück zur Projektdetailansicht zu navigieren.</summary>
    public Action? NavigateBackToProjectCallback { get; set; }

    private Guid _projectId;
    private Project? _project;
    private bool _isLoading;
    private string? _errorMessage;
    private CancellationTokenSource? _loadCts;
    private string _projectName = string.Empty;
    private string? _projectDescription;
    private GitRepository? _selectedRepository;
    private WorkItemFilterType _workItemsFilter = WorkItemFilterType.All;
    private bool _isFilterOverlayVisible;
    private bool _isLoadingIssues;
    private bool _canRequirementLoad;
    private bool _disposed;
    private Guid _currentWorkItemId;
    private readonly HashSet<string> _runningAlertConversion = new(StringComparer.OrdinalIgnoreCase);
    private string? _selectedRepositorySourceBranchName;
    private bool _isEditingSourceBranch;
    private ObservableCollection<string> _availableSourceBranchesForEdit = new();
    private string? _sourceBranchInputError;
    private string? _sourceBranchNameBeforeEdit;
    private bool _isLoadingSourceBranchesForEdit;
    private bool _isEditingSourceBranchManualInput;
    private string? _selectedInitializationScript;
    private bool _isEditingInitializationScript;
    private bool? _initializationScriptLoadingFailed;
    private string? _initializationScriptBeforeEdit;
    private bool _isBackmergeAutoMergeEnabled;
    private bool _isBackmergeModeProjektbezogen;

    /// <summary>Die Projekt-ID, deren Details angezeigt werden.</summary>
    public Guid ProjectId
    {
        get => _projectId;
        set
        {
            if (SetProperty(ref _projectId, value))
            {
                OnPropertyChanged(nameof(IsNeuanlage));
                _loadCts?.Cancel();
                _loadCts?.Dispose();
                _loadCts = new CancellationTokenSource();
                LoadAsync(_loadCts.Token).SafeFireAndForget(_logger, "ProjectDetailViewModel.LadenAsync");
            }
        }
    }

    /// <summary>Das geladene Projekt.</summary>
    public Project? Project
    {
        get => _project;
        private set => SetProperty(ref _project, value);
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

    /// <summary>Liste der Aufgaben des Projekts.</summary>
    public ObservableCollection<WorkItem> WorkItems { get; } = new();

    /// <summary>Gefilterte Aufgaben (entsprechend AufgabenFilter).</summary>
    public ObservableCollection<WorkItem> FilteredWorkItems { get; } = new();

    /// <summary>Nicht beendete Aufgaben der Projektdetailansicht.</summary>
    public ObservableCollection<WorkItem> NotFinishedWorkItems { get; } = new();

    /// <summary>Beendete Aufgaben der Projektdetailansicht.</summary>
    public ObservableCollection<WorkItem> FinishedWorkItems { get; } = new();

    /// <summary>Bearbeitbarer Projektname.</summary>
    public string ProjectName
    {
        get => _projectName;
        set => SetProperty(ref _projectName, value);
    }

    /// <summary>Bearbeitbare Projektbeschreibung.</summary>
    public string? ProjectDescription
    {
        get => _projectDescription;
        set => SetProperty(ref _projectDescription, value);
    }

    /// <summary>Ausgewähltes Repository.</summary>
    public GitRepository? SelectedRepository
    {
        get => _selectedRepository;
        set
        {
            if (SetProperty(ref _selectedRepository, value))
            {
                UpdateCanLoadIssues();
                IsEditingSourceBranch = false;
                AvailableSourceBranchesForEdit.Clear();
                IsEditingSourceBranchManualInput = false;
                SourceBranchInputError = null;
                SelectedRepositorySourceBranchName = value?.DefaultSourceBranchName;
                OnPropertyChanged(nameof(IsBackmergeOptionVisible));
                IsEditingInitializationScript = false;
                InitializationScriptSuggestions.Clear();
                InitializationScriptLoadingFailed = null;
                SelectedInitializationScript = value?.InitializationConfiguration?.InitializationScriptRelativePath;
            }
        }
    }

    /// <summary>Aktueller Aufgabenfilter.</summary>
    public WorkItemFilterType WorkItemsFilter
    {
        get => _workItemsFilter;
        set
        {
            if (SetProperty(ref _workItemsFilter, value))
                UpdateWorkItemViews();
        }
    }

    /// <summary>Gibt an, ob das Filter-Overlay sichtbar ist.</summary>
    public bool IsFilterOverlayVisible
    {
        get => _isFilterOverlayVisible;
        set => SetProperty(ref _isFilterOverlayVisible, value);
    }

    /// <summary>Gibt an, ob die Ansicht im Neuanlage-Modus ist (noch kein persistiertes Projekt).</summary>
    public bool IsNeuanlage => _projectId == Guid.Empty;

    /// <summary>Collection von geladenen Anforderungen aus dem SCM-Plugin.</summary>
    public ObservableCollection<ScmRequirement> OpenRequirement { get; } = new();

    /// <summary>Collection von geladenen Issues aus dem SCM-Plugin. Bleibt als kompatible Weiterleitung erhalten.</summary>
    public ObservableCollection<Issue> IssueSuggestions { get; } = new();

    /// <summary>Gibt an, ob Issues gerade geladen werden.</summary>
    public bool IsLoadingIssues
    {
        get => _isLoadingIssues;
        private set => SetProperty(ref _isLoadingIssues, value);
    }

    /// <summary>true wenn das Repository ein SCM-Plugin mit Anforderungssupport hat.</summary>
    public bool LoadCanRequirement
    {
        get => _canRequirementLoad;
        private set
        {
            if (SetProperty(ref _canRequirementLoad, value))
            {
                OnPropertyChanged(nameof(LoadCanIssues));
            }
        }
    }

    /// <summary>true wenn das Repository ein SCM-Plugin mit Issue-Support hat. Kompatible Weiterleitung.</summary>
    public bool LoadCanIssues => LoadCanRequirement;

    /// <summary>Aktuell konfigurierter Basis-Branch des ausgewählten Repositories, von dem neue Feature-Branches abgezweigt werden. <c>null</c> bedeutet Standard-Branch.</summary>
    public string? SelectedRepositorySourceBranchName
    {
        get => _selectedRepositorySourceBranchName;
        set
        {
            if (SetProperty(ref _selectedRepositorySourceBranchName, value))
                OnPropertyChanged(nameof(IsBackmergeOptionVisible));
        }
    }

    /// <summary>Projektbezogene Freigabe: Backmerge-Pull-Requests dieses Projekts automatisch mergen.</summary>
    public bool IsBackmergeAutoMergeEnabled
    {
        get => _isBackmergeAutoMergeEnabled;
        set
        {
            if (SetProperty(ref _isBackmergeAutoMergeEnabled, value))
                SaveBackmergeActiveAsync(value).SafeFireAndForget(_logger, "ProjectDetailViewModel.SpeichereBackmergeAktivAsync");
        }
    }

    /// <summary>Gibt an, ob die projektbezogene Backmerge-Option angeboten wird: nur im globalen
    /// Modus „Projektbezogen aktivierbar“ und nur bei einem konfigurierten Basis-Branch.</summary>
    public bool IsBackmergeOptionVisible
        => _isBackmergeModeProjektbezogen
           && _selectedRepository != null
           && !string.IsNullOrWhiteSpace(SelectedRepositorySourceBranchName);

    /// <summary>Gibt an, ob der Basis-Branch des ausgewählten Repositories gerade bearbeitet wird.</summary>
    public bool IsEditingSourceBranch
    {
        get => _isEditingSourceBranch;
        private set => SetProperty(ref _isEditingSourceBranch, value);
    }

    /// <summary>Verfügbare Branches des ausgewählten Repositories für die Basis-Branch-Bearbeitung.</summary>
    public ObservableCollection<string> AvailableSourceBranchesForEdit
    {
        get => _availableSourceBranchesForEdit;
        private set => SetProperty(ref _availableSourceBranchesForEdit, value);
    }

    /// <summary>Validierungsfehler bei der Basis-Branch-Bearbeitung.</summary>
    public string? SourceBranchInputError
    {
        get => _sourceBranchInputError;
        private set => SetProperty(ref _sourceBranchInputError, value);
    }

    /// <summary>Gibt an, ob die verfügbaren Branches für die Basis-Branch-Bearbeitung gerade geladen werden.</summary>
    public bool IsLoadingSourceBranchesForEdit
    {
        get => _isLoadingSourceBranchesForEdit;
        private set => SetProperty(ref _isLoadingSourceBranchesForEdit, value);
    }

    /// <summary>Gibt an, ob der Basis-Branch im Bearbeitungsmodus manuell eingegeben wird (keine Vorschlagsliste verfügbar).</summary>
    public bool IsEditingSourceBranchManualInput
    {
        get => _isEditingSourceBranchManualInput;
        private set => SetProperty(ref _isEditingSourceBranchManualInput, value);
    }

    /// <summary>Liste ausführbarer Dateien aus dem Remote-Repository für die Initialisierungsskript-Auswahl.</summary>
    public ObservableCollection<string> InitializationScriptSuggestions { get; } = new();

    /// <summary>Gefilterte Sicht auf <see cref="InitializationScriptSuggestions"/>, eingeengt anhand des in <see cref="SelectedInitializationScript"/> eingegebenen Texts.</summary>
    public CollectionView InitializationScriptSuggestionsView { get; }

    /// <summary>Vom Benutzer ausgewähltes oder manuell eingegebenes Initialisierungsskript.</summary>
    public string? SelectedInitializationScript
    {
        get => _selectedInitializationScript;
        set => SetProperty(ref _selectedInitializationScript, value, () => InitializationScriptSuggestionsView.Refresh());
    }

    /// <summary>Gibt an, ob das Initialisierungsskript des ausgewählten Repositories gerade bearbeitet wird.</summary>
    public bool IsEditingInitializationScript
    {
        get => _isEditingInitializationScript;
        private set => SetProperty(ref _isEditingInitializationScript, value);
    }

    /// <summary>Gibt an, ob das Laden der Initialisierungsskript-Vorschläge fehlgeschlagen ist. <c>null</c> bedeutet: noch nicht geladen.</summary>
    public bool? InitializationScriptLoadingFailed
    {
        get => _initializationScriptLoadingFailed;
        private set => SetProperty(ref _initializationScriptLoadingFailed, value);
    }

    /// <summary>Erstellt eine Aufgabe aus einer offenen SCM-Anforderung.</summary>
    public AsyncRelayCommand<ScmRequirement> CreateWorkItemFromRequirementCommand { get; }

    /// <summary>Erstellt eine Aufgabe aus einem Issue-Vorschlag.</summary>
    public AsyncRelayCommand<Issue> CreateWorkItemFromIssueCommand { get; }

    /// <summary>Lädt das Projekt neu.</summary>
    public ICommand LoadCommand { get; }

    /// <summary>Erstellt eine neue Aufgabe für das Projekt.</summary>
    public ICommand CreateWorkItemCommand { get; }

    /// <summary>Öffnet eine Aufgabe im Detail.</summary>
    public ICommand OpenWorkItemCommand { get; }

    /// <summary>Navigiert zurück zur Projektübersicht.</summary>
    public ICommand BackCommand { get; }

    /// <summary>Speichert Projektänderungen.</summary>
    public ICommand SaveCommand { get; }

    /// <summary>Löscht das Projekt.</summary>
    public ICommand DeleteCommand { get; }

    /// <summary>Öffnet das Filter-Overlay.</summary>
    public ICommand FilterCommand { get; }

    /// <summary>Öffnet den Repository-Zuweisungs-Dialog.</summary>
    public ICommand AssignRepositoryCommand { get; }

    /// <summary>Öffnet das Repository im Browser.</summary>
    public ICommand OpenRepositoryCommand { get; }

    /// <summary>Öffnet den Dialog zur nachträglichen Bearbeitung des Arbeitsverzeichnisses des zugewiesenen Repositories.</summary>
    public ICommand WorkingDirectoryEditCommand { get; }

    /// <summary>Öffnet den Bearbeitungsmodus für den Basis-Branch des ausgewählten Repositories.</summary>
    public ICommand EditSourceBranchCommand { get; }

    /// <summary>Speichert den geänderten Basis-Branch des ausgewählten Repositories.</summary>
    public ICommand SaveSourceBranchCommand { get; }

    /// <summary>Bricht die Bearbeitung des Basis-Branches ab und verwirft Änderungen.</summary>
    public ICommand CancelSourceBranchEditCommand { get; }

    /// <summary>Lädt die Initialisierungsskript-Vorschläge des ausgewählten Repositories und öffnet den Bearbeitungsmodus.</summary>
    public ICommand LoadInitializationScriptSuggestionsCommand { get; }

    /// <summary>Speichert das ausgewählte Initialisierungsskript des ausgewählten Repositories.</summary>
    public ICommand SaveInitializationScriptCommand { get; }

    /// <summary>Bricht die Bearbeitung des Initialisierungsskripts ab und verwirft Änderungen.</summary>
    public ICommand CancelInitializationScriptEditCommand { get; }

    /// <inheritdoc cref="ProjectDetailViewModel"/>
    public ProjectDetailViewModel(
        ProjectService projectService,
        WorkItemService workItemService,
        IServiceProvider serviceProvider,
        IDialogService dialogService,
        IPluginManager pluginManager,
        ILogger<ProjectDetailViewModel> logger,
        DirectoryStructureBrowserService? directoryStructureService = null)
    {
        _projectService = projectService;
        _workItemService = workItemService;
        _serviceProvider = serviceProvider;
        _dialogService = dialogService;
        _pluginManager = pluginManager;
        _logger = logger;
        _directoryStructureService = directoryStructureService;

        InitializationScriptSuggestionsView = (CollectionView)CollectionViewSource.GetDefaultView(InitializationScriptSuggestions);
        InitializationScriptSuggestionsView.Filter = FilterInitializationScriptSuggestion;

        LoadCommand = new AsyncRelayCommand(LoadAsync);
        CreateWorkItemCommand = new AsyncRelayCommand(
            CreateWorkItemAsync,
            () => _projectId != Guid.Empty);
        OpenWorkItemCommand = new RelayCommand<Guid>(id => OpenWorkItem(id));
        BackCommand = new RelayCommand(() => BackAction?.Invoke());
        SaveCommand = new AsyncRelayCommand(SaveProjectAsync, () => !string.IsNullOrWhiteSpace(_projectName));
        DeleteCommand = new AsyncRelayCommand(DeleteProjectAsync, () => _projectId != Guid.Empty);
        FilterCommand = new RelayCommand(() => IsFilterOverlayVisible = !IsFilterOverlayVisible);
        AssignRepositoryCommand = new AsyncRelayCommand(AssignRepositoryAsync, () => _projectId != Guid.Empty);
        OpenRepositoryCommand = new RelayCommand(OpenRepository, () => _selectedRepository != null);
        WorkingDirectoryEditCommand = new AsyncRelayCommand(WorkingDirectoryEditAsync, () => _selectedRepository != null);
        EditSourceBranchCommand = new AsyncRelayCommand(EditSourceBranchAsync, () => _selectedRepository != null);
        SaveSourceBranchCommand = new AsyncRelayCommand(SaveSourceBranchAsync, () => _isEditingSourceBranch);
        CancelSourceBranchEditCommand = new RelayCommand(CancelSourceBranchEdit, () => _isEditingSourceBranch);
        LoadInitializationScriptSuggestionsCommand = new AsyncRelayCommand(
            ct => LoadInitializationScriptSuggestionsAsync(_selectedRepository?.Id ?? Guid.Empty, ct),
            () => _selectedRepository != null);
        SaveInitializationScriptCommand = new AsyncRelayCommand(SaveInitializationScriptAsync, () => _isEditingInitializationScript);
        CancelInitializationScriptEditCommand = new RelayCommand(CancelInitializationScriptEdit, () => _isEditingInitializationScript);
        CreateWorkItemFromRequirementCommand = new AsyncRelayCommand<ScmRequirement>(CreateWorkItemFromRequirementAsync);
        CreateWorkItemFromIssueCommand = new AsyncRelayCommand<Issue>(CreateWorkItemFromIssueAsync);
    }

    private async Task LoadAsync(CancellationToken ct)
    {
        if (_projectId == Guid.Empty)
            return;

        IsLoading = true;
        ErrorMessage = null;

        try
        {
            Project = await _projectService.GetDetailAsync(_projectId, ct);
            var workItems = await _workItemService.GetByProjectAsync(_projectId, ct);
            WorkItems.Clear();
            foreach (var workItem in workItems)
                WorkItems.Add(workItem);
            UpdateWorkItemViews();

            if (Project != null)
            {
                ProjectName = Project.Name;
                ProjectDescription = Project.Description;
                SelectedRepository = Project.Repositories.FirstOrDefault();
            }

            // Die Backmerge-Option ist nicht kritisch: Steht der Einstellungsdienst nicht zur
            // Verfügung, bleibt sie schlicht verborgen statt das Laden des Projekts zu verhindern.
            try
            {
                using var settingsScope = _serviceProvider.CreateScope();
                var settings = settingsScope.ServiceProvider.GetRequiredService<AppSettingService>();
                _isBackmergeModeProjektbezogen = await settings.GetScmBackmergeModeAsync(ct) == ScmBackmergeMode.ProjectScoped;
                _isBackmergeAutoMergeEnabled = await settings.GetBackmergeProjectEnabledAsync(_projectId, ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Backmerge--Einstellungen für Projekt {ProjektId} konnten nicht geladen werden.", _projectId);
            }
            OnPropertyChanged(nameof(IsBackmergeAutoMergeEnabled));
            OnPropertyChanged(nameof(IsBackmergeOptionVisible));

            await LoadOpenRequirementAsync(ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Laden des Projekts {ProjektId}.", _projectId);
            SetError(ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task CreateWorkItemAsync(CancellationToken ct)
    {
        if (_projectId == Guid.Empty)
            return;

        try
        {
            var workItem = await _workItemService.CreateAsync(
                _projectId,
                "Neue Aufgabe",
                string.Empty,
                SelectedRepository?.Id,
                ct);

            WorkItems.Add(workItem);
            UpdateWorkItemViews();

            OpenWorkItem(workItem.Id);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Erstellen einer Aufgabe.");
            SetError(ex);
        }
    }

    private async Task SaveProjectAsync(CancellationToken ct)
    {
        try
        {
            if (_projectId == Guid.Empty)
            {
                await _projectService.CreateAsync(ProjectName.Trim(), ProjectDescription?.Trim(), ct);
                try
                {
                    await (RefreshProjectListCallback?.Invoke() ?? Task.CompletedTask);
                }
                catch (Exception callbackEx)
                {
                    _logger.LogError(callbackEx, "Fehler im RefreshProjectListCallback nach Projekterstellung.");
                }
                BackAction?.Invoke();
            }
            else
            {
                await _projectService.UpdateAsync(_projectId, ProjectName.Trim(), ProjectDescription?.Trim(), ct);
                try
                {
                    await (RefreshProjectListCallback?.Invoke() ?? Task.CompletedTask);
                }
                catch (Exception callbackEx)
                {
                    _logger.LogError(callbackEx, "Fehler im RefreshProjectListCallback nach Projektaktualisierung.");
                }
                await LoadAsync(ct);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Speichern des Projekts {ProjektId}.", _projectId);
            SetError(ex);
        }
    }

    private async Task DeleteProjectAsync(CancellationToken ct)
    {
        if (_projectId == Guid.Empty)
            return;

        if (!_dialogService.ConfirmDialog("Soll das Projekt wirklich gelöscht werden?", "Löschen bestätigen"))
            return;

        try
        {
            await _projectService.DeleteAsync(_projectId, ct);
            try
            {
                await (RefreshProjectListCallback?.Invoke() ?? Task.CompletedTask);
            }
            catch (Exception callbackEx)
            {
                _logger.LogError(callbackEx, "Fehler im RefreshProjectListCallback nach Projektlöschung.");
            }
            BackAction?.Invoke();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Löschen des Projekts {ProjektId}.", _projectId);
            SetError(ex);
        }
    }

    private async Task AssignRepositoryAsync(CancellationToken ct)
    {
        if (_projectId == Guid.Empty)
            return;

        try
        {
            var vm = _serviceProvider.GetRequiredService<RepositoryAssignViewModel>();
            await vm.LoadAsync(ct);
            var confirmed = _dialogService.RepositoryAssignDialog(vm);

            if (_disposed || ct.IsCancellationRequested)
                return;

            if (confirmed && vm.SelectedRepository is { } repo && vm.SelectedScmPlugin is { } scmPlugin)
            {
                var gitRepository = await _projectService.AddRepositoryAsync(
                    _projectId,
                    scmPlugin.PluginPrefix,
                    repo.Url,
                    repo.Name,
                    vm.DefaultSourceBranchName,
                    ct);

                await _projectService.SaveRepositoryWorkingDirectoryAsync(gitRepository.Id, vm.SelectedWorkingDirectory, ct);

                if (!_disposed && !ct.IsCancellationRequested)
                    await LoadAsync(ct);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Zuweisen des Repositories.");
            SetError(ex);
        }
    }

    private void OpenRepository()
    {
        if (_selectedRepository == null)
            return;

        try
        {
            var url = _selectedRepository.RepositoryUrl;
            if (!string.IsNullOrWhiteSpace(url))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Öffnen der Repository-URL.");
            SetError(ex);
        }
    }

    private async Task WorkingDirectoryEditAsync(CancellationToken ct)
    {
        if (_selectedRepository == null)
            return;

        var repository = _selectedRepository;

        try
        {
            var gitPlugin = _pluginManager.GetSourceCodeManagementPlugins()
                .FirstOrDefault(p => string.Equals(p.PluginPrefix, repository.PluginType, StringComparison.OrdinalIgnoreCase));

            var vm = _serviceProvider.GetRequiredService<WorkingDirectoryEditViewModel>();
            await vm.LoadAsync(gitPlugin, repository.RepositoryUrl, repository.StartConfiguration?.WorkingDirectoryRelativePath, ct);

            var confirmed = _dialogService.WorkingDirectoryEditDialog(vm);

            if (_disposed || ct.IsCancellationRequested)
                return;

            if (confirmed)
            {
                await _projectService.SaveRepositoryWorkingDirectoryAsync(repository.Id, vm.SelectedWorkingDirectory, ct);

                if (!_disposed && !ct.IsCancellationRequested)
                    await LoadAsync(ct);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Bearbeiten des Arbeitsverzeichnisses für Repository {RepositoryId}.", repository.Id);
            SetError(ex);
        }
    }

    private async Task EditSourceBranchAsync(CancellationToken ct)
    {
        if (_selectedRepository == null)
            return;

        var repository = _selectedRepository;
        _sourceBranchNameBeforeEdit = SelectedRepositorySourceBranchName;
        SourceBranchInputError = null;
        IsEditingSourceBranch = true;
        IsLoadingSourceBranchesForEdit = true;

        try
        {
            var gitPlugin = _pluginManager.GetSourceCodeManagementPlugins()
                .FirstOrDefault(p => string.Equals(p.PluginPrefix, repository.PluginType, StringComparison.OrdinalIgnoreCase));

            AvailableSourceBranchesForEdit.Clear();

            if (gitPlugin != null)
            {
                var branches = await gitPlugin.GetRemoteBranchesAsync(repository.RepositoryUrl, ct);
                foreach (var branch in branches.OrderBy(b => b, StringComparer.OrdinalIgnoreCase))
                    AvailableSourceBranchesForEdit.Add(branch);
            }

            IsEditingSourceBranchManualInput = AvailableSourceBranchesForEdit.Count == 0;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Laden der verfügbaren Branches für Repository {RepositoryId}.", repository.Id);
            IsEditingSourceBranchManualInput = true;
        }
        finally
        {
            IsLoadingSourceBranchesForEdit = false;
        }
    }

    private async Task SaveSourceBranchAsync(CancellationToken ct)
    {
        if (_selectedRepository == null)
            return;

        if (!ValidateSourceBranchInput())
            return;

        var repository = _selectedRepository;

        try
        {
            var normalized = string.IsNullOrWhiteSpace(SelectedRepositorySourceBranchName)
                ? null
                : SelectedRepositorySourceBranchName.Trim();

            var updatedRepository = await _projectService.UpdateRepositorySourceBranchAsync(repository.Id, normalized, ct);

            repository.DefaultSourceBranchName = updatedRepository.DefaultSourceBranchName;
            SelectedRepositorySourceBranchName = updatedRepository.DefaultSourceBranchName;
            SourceBranchInputError = null;
            IsEditingSourceBranch = false;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Speichern des Basis-Branches für Repository {RepositoryId}.", repository.Id);
            SourceBranchInputError = ex.Message;
        }
    }

    /// <summary>Persistiert die projektbezogene Backmerge-Freigabe sofort beim Umschalten der Option.</summary>
    private async Task SaveBackmergeActiveAsync(bool active)
    {
        if (_projectId == Guid.Empty)
            return;

        using var scope = _serviceProvider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppSettingService>()
            .SetBackmergeProjectEnabledAsync(_projectId, active, CancellationToken.None);
    }

    private void CancelSourceBranchEdit()
    {
        SelectedRepositorySourceBranchName = _sourceBranchNameBeforeEdit;
        SourceBranchInputError = null;
        IsEditingSourceBranch = false;
    }

    private bool ValidateSourceBranchInput()
    {
        var isValid = SourceBranchInputValidator.Validate(SelectedRepositorySourceBranchName, AvailableSourceBranchesForEdit, out var error);
        SourceBranchInputError = error;
        return isValid;
    }

    private async Task LoadInitializationScriptSuggestionsAsync(Guid repositoryId, CancellationToken ct)
    {
        var repository = Project?.Repositories.FirstOrDefault(r => r.Id == repositoryId);
        if (repository == null)
            return;

        _initializationScriptBeforeEdit = SelectedInitializationScript;
        IsEditingInitializationScript = true;
        InitializationScriptLoadingFailed = null;
        InitializationScriptSuggestions.Clear();

        var gitPlugin = _pluginManager.GetSourceCodeManagementPlugins()
            .FirstOrDefault(p => string.Equals(p.PluginPrefix, repository.PluginType, StringComparison.OrdinalIgnoreCase));

        if (gitPlugin == null || _directoryStructureService == null || string.IsNullOrWhiteSpace(repository.RepositoryUrl))
        {
            InitializationScriptLoadingFailed = true;
            return;
        }

        var result = await _directoryStructureService.GetFileLoadResultAsync(gitPlugin, repository.RepositoryUrl, ct, repository.DefaultSourceBranchName);
        ct.ThrowIfCancellationRequested();

        if (result.Status != RepositoryStructureLoadStatus.Success)
        {
            InitializationScriptLoadingFailed = true;
            return;
        }

        foreach (var entry in result.Entries
            .Where(entry => IsExecutableFile(entry.Path))
            .OrderBy(entry => entry.Path, StringComparer.OrdinalIgnoreCase))
        {
            InitializationScriptSuggestions.Add(entry.Path);
        }

        InitializationScriptLoadingFailed = false;
    }

    private async Task SaveInitializationScriptAsync(CancellationToken ct)
    {
        if (_selectedRepository == null)
            return;

        var repository = _selectedRepository;

        try
        {
            var normalized = string.IsNullOrWhiteSpace(SelectedInitializationScript)
                ? null
                : SelectedInitializationScript.Trim();

            var updatedConfiguration = await _projectService.SaveRepositoryInitializationScriptAsync(repository.Id, normalized, ct);

            repository.InitializationConfiguration = updatedConfiguration;
            SelectedInitializationScript = updatedConfiguration?.InitializationScriptRelativePath;
            IsEditingInitializationScript = false;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Speichern des Initialisierungsskripts für Repository {RepositoryId}.", repository.Id);
            SetError(ex);
        }
    }

    private void CancelInitializationScriptEdit()
    {
        SelectedInitializationScript = _initializationScriptBeforeEdit;
        IsEditingInitializationScript = false;
    }

    private static bool IsExecutableFile(string path)
    {
        var extension = Path.GetExtension(path);
        return extension is ".ps1" or ".cmd" or ".bat" or ".sh" or ".exe";
    }

    private bool FilterInitializationScriptSuggestion(object item)
    {
        if (string.IsNullOrWhiteSpace(SelectedInitializationScript))
            return true;

        return item is string path && path.Contains(SelectedInitializationScript, StringComparison.OrdinalIgnoreCase);
    }

    private void OpenWorkItem(Guid id)
    {
        var vm = _serviceProvider.GetRequiredService<TaskDetailViewModel>();
        vm.BackAction = () => NavigateBackToProjectCallback?.Invoke();
        _currentWorkItemId = id;
        vm.RefreshWorkItemListCallback = ReloadWorkItemsListAsync;
        NavigateToTaskViewCallback?.Invoke(vm);
        vm.WorkItemId = id;
    }

    private async Task ReloadWorkItemsListAsync()
    {
        var updates = await _workItemService.GetByIdAsync(_currentWorkItemId);
        if (updates is null)
            return;

        ReplaceOrAddWorkItem(updates);
        UpdateWorkItemViews();
    }

    private void ReplaceOrAddWorkItem(WorkItem workItem)
    {
        for (var i = 0; i < WorkItems.Count; i++)
        {
            if (WorkItems[i].Id == workItem.Id)
            {
                WorkItems[i] = workItem;
                return;
            }
        }
        WorkItems.Add(workItem);
    }

    private void UpdateWorkItemViews()
    {
        FilteredWorkItems.Clear();
        var source = _workItemsFilter switch
        {
            WorkItemFilterType.Active => WorkItems.Where(a => a.Status != WorkItemStatus.Archived),
            WorkItemFilterType.Archived => WorkItems.Where(a => a.Status == WorkItemStatus.Archived),
            _ => WorkItems
        };
        foreach (var workItem in source)
            FilteredWorkItems.Add(workItem);

        NotFinishedWorkItems.Clear();
        FinishedWorkItems.Clear();
        foreach (var workItem in WorkItems)
        {
            switch (workItem.Status)
            {
                case WorkItemStatus.New:
                case WorkItemStatus.Started:
                case WorkItemStatus.Waiting:
                    NotFinishedWorkItems.Add(workItem);
                    break;
                case WorkItemStatus.Completed:
                    FinishedWorkItems.Add(workItem);
                    break;
            }
        }
    }

    private async Task LoadOpenRequirementAsync(CancellationToken ct)
    {
        var repository = _selectedRepository;
        if (repository == null)
            return;

        var scmPlugins = _pluginManager.GetSourceCodeManagementPlugins();
        var gitPlugin = scmPlugins
            .FirstOrDefault(p => string.Equals(p.PluginPrefix, repository.PluginType, StringComparison.OrdinalIgnoreCase));
        if (gitPlugin == null)
            return;

        IsLoadingIssues = true;
        OpenRequirement.Clear();
        IssueSuggestions.Clear();

        try
        {
            var readyConvertedNumbers = WorkItems
                .Where(a => a.IssueReference?.IssueNumber != null)
                .Select(a => a.IssueReference!.IssueNumber!.Value)
                .ToHashSet();
            var readyConvertedAlerts = WorkItems
                .Where(a => !string.IsNullOrWhiteSpace(a.AlertReference?.SourceKey))
                .Select(a => a.AlertReference!.SourceKey)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var repositoryContext = new ScmRepositoryContext(
                repository.Id,
                repository.PluginType,
                string.IsNullOrWhiteSpace(repository.RepositoryName) ? repository.RepositoryUrl : repository.RepositoryName);

            var issues = await gitPlugin.GetIssuesAsync(repository.RepositoryUrl, ct);
            foreach (var issue in issues)
            {
                if (!readyConvertedNumbers.Contains(issue.Number))
                {
                    IssueSuggestions.Add(issue);
                    OpenRequirement.Add(ScmRequirement.FromIssue(issue, repositoryContext));
                }
            }

            var pullRequests = await gitPlugin.GetOpenPullRequestsAsync(repository.RepositoryUrl, ct);
            foreach (var pullRequest in pullRequests)
            {
                var repositoryId = pullRequest.RepositoryId ?? repositoryContext.RepositoryId;
                if (!PullRequestRepositoryId.TryNormalize(pullRequest.Provider, repositoryId, out var normalizedRepositoryId))
                    continue;

                var alreadyLinked = await _workItemService.IsPullRequestLinkedAsync(
                    pullRequest.Provider,
                    normalizedRepositoryId,
                    pullRequest.Number,
                    ct);
                if (!alreadyLinked)
                {
                    OpenRequirement.Add(ScmRequirement.FromPullRequest(
                        pullRequest with { RepositoryId = normalizedRepositoryId },
                        new ScmRepositoryContext(repositoryContext.GitRepositoryId, repositoryContext.PluginPrefix, normalizedRepositoryId)));
                }
            }

            if (gitPlugin is IScmAlertProvider alertProvider)
            {
                var alerts = await alertProvider.GetAlertsAsync(repository.RepositoryUrl, ct);
                foreach (var alert in alerts)
                {
                    if (!readyConvertedAlerts.Contains(alert.SourceKey))
                    {
                        OpenRequirement.Add(ScmRequirement.FromAlert(alert));
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Laden der Issues für Repository {RepositoryUrl}.", repository.RepositoryUrl);
        }
        finally
        {
            IsLoadingIssues = false;
        }
    }

    private async Task CreateWorkItemFromRequirementAsync(ScmRequirement? requirement, CancellationToken ct)
    {
        if (requirement == null)
            return;

        if (requirement.Kind == ScmRequirementKind.Issue)
        {
            await CreateWorkItemFromIssueAsync(requirement.Issue, ct);
            return;
        }

        if (requirement.Kind == ScmRequirementKind.PullRequest)
        {
            await CreateWorkItemFromPullRequestAsync(requirement, ct);
            return;
        }

        await CreateWorkItemFromAlertAsync(requirement, ct);
    }

    private async Task CreateWorkItemFromPullRequestAsync(ScmRequirement requirement, CancellationToken ct)
    {
        var pullRequest = requirement.PullRequest;
        var repositoryContext = requirement.RepositoryContext;
        if (pullRequest == null || repositoryContext == null || _projectId == Guid.Empty)
            return;

        if (!_dialogService.ConfirmDialog(
                $"Pull Request '{pullRequest.Title}' als Review-Aufgabe erstellen?",
                "Pull Request konvertieren"))
            return;

        try
        {
            var workItem = await _workItemService.CreateFromPullRequestAsync(
                _projectId,
                pullRequest,
                repositoryContext,
                ct);

            var toRemove = OpenRequirement.FirstOrDefault(a =>
                a.Kind == ScmRequirementKind.PullRequest
                && a.PullRequest?.Provider == pullRequest.Provider
                && a.PullRequest?.Number == pullRequest.Number
                && string.Equals(a.RepositoryContext?.RepositoryId, repositoryContext.RepositoryId, StringComparison.OrdinalIgnoreCase));
            if (toRemove != null)
                OpenRequirement.Remove(toRemove);

            WorkItems.Add(workItem);
            UpdateWorkItemViews();
            OpenWorkItem(workItem.Id);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Erstellen einer Aufgabe aus Pull Request #{PullRequestNumber}.", pullRequest.Number);
            SetError(ex);
        }
    }

    private async Task CreateWorkItemFromIssueAsync(Issue? issue, CancellationToken ct)
    {
        if (issue == null || _projectId == Guid.Empty)
            return;

        if (!_dialogService.ConfirmDialog(
                $"Issue '{issue.Title}' als Aufgabe erstellen?",
                "Issue konvertieren"))
            return;

        try
        {
            var workItem = await _workItemService.CreateFromIssueAsync(
                _projectId,
                issue,
                _selectedRepository?.Id,
                ct);

            var toRemove = IssueSuggestions.FirstOrDefault(i => i.Number == issue.Number);
            if (toRemove != null)
                IssueSuggestions.Remove(toRemove);
            var requirement = OpenRequirement.FirstOrDefault(a => a.Kind == ScmRequirementKind.Issue && a.Issue?.Number == issue.Number);
            if (requirement != null)
                OpenRequirement.Remove(requirement);

            WorkItems.Add(workItem);
            UpdateWorkItemViews();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Erstellen einer Aufgabe aus Issue #{IssueNumber}.", issue.Number);
            SetError(ex);
        }
    }

    private async Task CreateWorkItemFromAlertAsync(ScmRequirement requirement, CancellationToken ct)
    {
        var alert = requirement.Alert;
        if (alert == null || _projectId == Guid.Empty || _selectedRepository == null)
            return;

        if (!_dialogService.ConfirmDialog(
                $"Alert '{alert.Title}' als GitHub-Issue und Aufgabe erstellen?",
                "Alert konvertieren"))
            return;

        var conversionRegistered = false;

        try
        {
            conversionRegistered = _runningAlertConversion.Add(alert.SourceKey);
            if (!conversionRegistered)
                return;

            var existing = await _workItemService.GetByAlertSourceKeyAsync(alert.SourceKey, ct);
            if (existing != null)
            {
                RemoveOpenRequirement(alert.SourceKey);
                ReplaceOrAddWorkItem(existing);
                UpdateWorkItemViews();
                return;
            }

            var gitPlugin = _pluginManager.GetSourceCodeManagementPlugins()
                .FirstOrDefault(p => string.Equals(p.PluginPrefix, _selectedRepository.PluginType, StringComparison.OrdinalIgnoreCase));

            if (gitPlugin is not IIssueCreateProvider issueCreateProvider)
            {
                ErrorMessage = "Das ausgewählte SCM-Plugin kann keine Issues erstellen.";
                return;
            }

            if (!await issueCreateProvider.CanCreateIssueAsync(_selectedRepository.RepositoryUrl, ct))
            {
                ErrorMessage = "Für dieses Repository kann kein GitHub-Issue erstellt werden.";
                return;
            }

            var request = BuildIssueCreateRequest(alert);
            var createdIssueResult = await issueCreateProvider.CreateIssueAsync(_selectedRepository.RepositoryUrl, request, ct);
            if (!createdIssueResult.IsSuccess || createdIssueResult.Issue == null)
            {
                ErrorMessage = createdIssueResult.ErrorMessage ?? "GitHub-Issue konnte nicht erstellt werden.";
                return;
            }

            var workItem = await _workItemService.CreateFromAlertAsync(
                _projectId,
                alert,
                createdIssueResult.Issue,
                _selectedRepository.Id,
                _selectedRepository.PluginType,
                _selectedRepository.RepositoryUrl,
                ct);

            RemoveOpenRequirement(alert.SourceKey);

            ReplaceOrAddWorkItem(workItem);
            UpdateWorkItemViews();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Erstellen einer Aufgabe aus Alert {SourceKey}.", alert.SourceKey);
            SetError(ex);
        }
        finally
        {
            if (conversionRegistered)
                _runningAlertConversion.Remove(alert.SourceKey);
        }
    }

    private void RemoveOpenRequirement(string sourceKey)
    {
        var toRemove = OpenRequirement.FirstOrDefault(a => string.Equals(a.SourceKey, sourceKey, StringComparison.OrdinalIgnoreCase));
        if (toRemove != null)
            OpenRequirement.Remove(toRemove);
    }

    private static IssueCreateRequest BuildIssueCreateRequest(ScmAlert alert)
    {
        var title = $"Code scanning alert: {alert.Title}";
        var location = alert.FilePath is null
            ? "-"
            : alert.StartLine is null ? alert.FilePath : $"{alert.FilePath}:{alert.StartLine}";
        var body = string.Join(Environment.NewLine, new[]
        {
            "Automatisch aus einem GitHub-Code-Scanning-Alert erstellt.",
            string.Empty,
            $"Alert-Typ: {alert.AlertType}",
            $"Severity: {alert.Severity ?? "-"}",
            $"Status: {alert.State ?? "-"}",
            $"Tool: {alert.ToolName ?? "-"}",
            $"Rule: {alert.RuleName ?? alert.RuleId ?? "-"}",
            $"Betroffener Ort: {location}",
            $"Alert-URL: {alert.AlertUrl ?? "-"}",
            string.Empty,
            alert.Description ?? "-"
        });

        return new IssueCreateRequest(title, body);
    }

    private void UpdateCanLoadIssues()
    {
        LoadCanRequirement = _selectedRepository != null
            && _pluginManager.GetSourceCodeManagementPlugins()
                .Any(p => string.Equals(p.PluginPrefix, _selectedRepository.PluginType, StringComparison.OrdinalIgnoreCase));
    }

    private void SetError(Exception ex) => SetError(ref _errorMessage, nameof(ErrorMessage), ex);

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = null;
    }
}
