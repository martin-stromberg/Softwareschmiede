using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.App.ViewModels;

/// <summary>ViewModel für die Projektliste.</summary>
public sealed class ProjectListViewModel : ViewModelBase, IDisposable
{
    private readonly ProjectService _projectService;
    private readonly IServiceProvider _serviceProvider;
    private readonly IPluginManager _pluginManager;
    private readonly ILogger<ProjectListViewModel> _logger;
    private readonly SemaphoreSlim _loadSemaphore = new(1, 1);

    private Project? _selectedProject;
    private bool _isLoading;
    private bool _isLoadingRepositories;
    private string? _errorMessage;
    private ViewModelBase? _detailViewModel;
    private ProjectDetailViewModel? _currentProjectDetailViewModel;

    /// <summary>Liste aller Projekte.</summary>
    public ObservableCollection<Project> Projects { get; } = new();

    /// <summary>Liste der unzugeordneten Repositories aus allen SCM-Plugins.</summary>
    public ObservableCollection<AvailableRepository> UnassignedRepositories { get; } = new();

    /// <summary>Das aktuell ausgewählte Projekt.</summary>
    public Project? SelectedProject
    {
        get => _selectedProject;
        set
        {
            var changed = SetProperty(ref _selectedProject, value);
            if (value is not null && (changed || DetailViewModel is null))
            {
                ShowDetail(value.Id);
            }
        }
    }

    /// <summary>Das aktuell angezeigte Detail-ViewModel.</summary>
    public ViewModelBase? DetailViewModel
    {
        get => _detailViewModel;
        private set
        {
            var old = _detailViewModel;
            SetProperty(ref _detailViewModel, value);
            if (ReferenceEquals(old, value))
                return;

            if (!ReferenceEquals(old, _currentProjectDetailViewModel) && old is IDisposable d)
                d.Dispose();

            // Wenn weder die alte noch die neue Anzeige dem zurückgehaltenen ProjectDetailViewModel
            // entspricht, wurde von der Aufgabenansicht direkt weg navigiert (z. B. SchliesseDetailCommand)
            // statt über KehreZuProjectZurueck() — das zurückgehaltene ViewModel ist dann verwaist.
            if (_currentProjectDetailViewModel is not null
                && !ReferenceEquals(old, _currentProjectDetailViewModel)
                && !ReferenceEquals(value, _currentProjectDetailViewModel))
            {
                _currentProjectDetailViewModel.Dispose();
                _currentProjectDetailViewModel = null;
            }
        }
    }

    /// <summary>Gibt an, ob Daten geladen werden.</summary>
    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    /// <summary>Gibt an, ob unzugeordnete Repositories geladen werden.</summary>
    public bool IsLoadingRepositories
    {
        get => _isLoadingRepositories;
        private set => SetProperty(ref _isLoadingRepositories, value);
    }

    /// <summary>Fehlermeldung bei Ladefehlern.</summary>
    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    /// <summary>Lädt die Projektliste.</summary>
    public ICommand LoadCommand { get; }

    /// <summary>Öffnet die Detailansicht im Anlage-Modus.</summary>
    public ICommand ShowCreationFormCommand { get; }

    /// <summary>Archiviert das ausgewählte Projekt.</summary>
    public ICommand ProjectArchiveCommand { get; }

    /// <summary>Schließt die Detailansicht.</summary>
    public ICommand CloseDetailCommand { get; }

    /// <summary>Wählt ein Projekt aus und zeigt die Details.</summary>
    public ICommand SelectProjectCommand { get; }

    /// <summary>Erstellt ein neues Projekt aus einem unzugeordneten Repository und ordnet das Repository zu.</summary>
    public AsyncRelayCommand<AvailableRepository> RepositoryDoubleclickCommand { get; }

    /// <summary>Wird aufgerufen, wenn sich der Titel des Detailbereichs ändert (z. B. Projektname). Null bedeutet, kein Detailtitel aktiv.</summary>
    public Action<string?>? DetailTitleChangeAction { get; set; }

    /// <inheritdoc cref="ProjectListViewModel"/>
    public ProjectListViewModel(
        ProjectService projectService,
        IServiceProvider serviceProvider,
        IPluginManager pluginManager,
        ILogger<ProjectListViewModel> logger)
    {
        _projectService = projectService;
        _serviceProvider = serviceProvider;
        _pluginManager = pluginManager;
        _logger = logger;

        LoadCommand = new AsyncRelayCommand(LoadAsync);
        ShowCreationFormCommand = new RelayCommand(ShowDetailCreationForm);
        ProjectArchiveCommand = new AsyncRelayCommand(
            ProjectArchiveAsync,
            () => SelectedProject is not null);
        CloseDetailCommand = new RelayCommand(() => DetailViewModel = null);
        SelectProjectCommand = new RelayCommand<Project>(project =>
        {
            if (project is not null)
            {
                SelectedProject = project;
            }
        });
        RepositoryDoubleclickCommand = new AsyncRelayCommand<AvailableRepository>(CreateProjectFromRepository);
    }

    private async Task LoadAsync(CancellationToken ct)
    {
        await _loadSemaphore.WaitAsync(ct);
        try
        {
            IsLoading = true;
            ErrorMessage = null;

            var projectTask = _projectService.GetAllAsync(ct);
            var suggestionTask = LoadRepositoriesSuggestionsAsync(ct);
            await Task.WhenAll(projectTask, suggestionTask);

            var projects = projectTask.Result;
            Projects.Clear();
            foreach (var project in projects)
                Projects.Add(project);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Laden der Projekte.");
            SetError(ref _errorMessage, nameof(ErrorMessage), ex);
        }
        finally
        {
            IsLoading = false;
            _loadSemaphore.Release();
        }
    }

    private async Task LoadRepositoriesSuggestionsAsync(CancellationToken ct)
    {
        IsLoadingRepositories = true;
        try
        {
            var repos = await _projectService.GetUnassignedRepositoriesAsync(ct);
            UnassignedRepositories.Clear();
            foreach (var repo in repos)
                UnassignedRepositories.Add(repo);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Laden der unzugeordneten Repositories.");
        }
        finally
        {
            IsLoadingRepositories = false;
        }
    }

    private async Task CreateProjectFromRepository(AvailableRepository? repo, CancellationToken ct)
    {
        if (repo is null)
            return;

        try
        {
            var pluginPrefix = await FindPluginPrefixForRepositoryAsync(repo.Url, ct);
            var project = await _projectService.CreateAsync(repo.Name, null, ct);
            await _projectService.AddRepositoryAsync(project.Id, pluginPrefix, repo.Url, repo.Name, ct: ct);
            await AddNewProject();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Erstellen eines Projekts aus Repository '{RepositoryName}'.", repo.Name);
            SetError(ref _errorMessage, nameof(ErrorMessage), ex);
        }
    }

    private async Task<string> FindPluginPrefixForRepositoryAsync(string repositoryUrl, CancellationToken ct)
    {
        foreach (var plugin in _pluginManager.GetSourceCodeManagementPlugins())
        {
            try
            {
                var repos = await plugin.GetAvailableRepositoriesAsync(ct);
                if (repos.Any(r => string.Equals(r.Url, repositoryUrl, StringComparison.OrdinalIgnoreCase)))
                    return plugin.PluginPrefix;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Plugin '{PluginName}' konnte beim Suchen des PluginPrefix nicht abgefragt werden.", plugin.PluginName);
            }
        }

        return string.Empty;
    }

    private async Task ProjectArchiveAsync(CancellationToken ct)
    {
        if (SelectedProject is null)
            return;

        try
        {
            await _projectService.ArchiveAsync(SelectedProject.Id, ct);
            Projects.Remove(SelectedProject);
            SelectedProject = null;
            DetailViewModel = null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Archivieren des Projekts.");
            SetError(ref _errorMessage, nameof(ErrorMessage), ex);
        }
    }

    private void InitDetailViewModel(ProjectDetailViewModel viewModel)
    {
        // Falls noch ein zurückgehaltenes ProjectDetailViewModel existiert, das nicht mehr angezeigt wird
        // (z. B. weil zwischenzeitlich ein anderes Projekt direkt angewählt wurde), muss es jetzt disposed werden.
        if (_currentProjectDetailViewModel is not null && !ReferenceEquals(_currentProjectDetailViewModel, DetailViewModel))
            _currentProjectDetailViewModel.Dispose();

        PropertyChangedEventHandler propertyChangedHandler = (_, e) =>
        {
            if (e.PropertyName == nameof(ProjectDetailViewModel.ProjectName)
                && ReferenceEquals(DetailViewModel, viewModel))
            {
                DetailTitleChangeAction?.Invoke(viewModel.ProjectName);
            }
        };
        viewModel.PropertyChanged += propertyChangedHandler;

        viewModel.BackAction = () =>
        {
            viewModel.PropertyChanged -= propertyChangedHandler;
            DetailViewModel = null;
            DetailTitleChangeAction?.Invoke(null);
        };
        viewModel.RefreshProjectListCallback = AddNewProject;
        viewModel.NavigateToTaskViewCallback = ShowTaskDetailView;
        viewModel.NavigateBackToProjectCallback = KehreToProjectBack;
        _currentProjectDetailViewModel = viewModel;
    }

    private void ShowDetail(Guid projectId)
    {
        var viewModel = _serviceProvider.GetRequiredService<ProjectDetailViewModel>();
        InitDetailViewModel(viewModel);
        viewModel.ProjectId = projectId;
        DetailViewModel = viewModel;
    }

    private void ShowDetailCreationForm()
    {
        var viewModel = _serviceProvider.GetRequiredService<ProjectDetailViewModel>();
        InitDetailViewModel(viewModel);
        viewModel.ProjectId = Guid.Empty;
        viewModel.ProjectName = string.Empty;
        DetailViewModel = viewModel;
    }

    private async Task AddNewProject()
    {
        await LoadAsync(CancellationToken.None);
    }

    private void ShowTaskDetailView(TaskDetailViewModel vm)
    {
        vm.DetailTitleChangeAction = detailTitle =>
        {
            if (!ReferenceEquals(DetailViewModel, vm))
                return;

            DetailTitleChangeAction?.Invoke(string.IsNullOrWhiteSpace(detailTitle) ? "Aufgabe" : detailTitle);
        };
        DetailViewModel = vm;
        DetailTitleChangeAction?.Invoke(string.IsNullOrWhiteSpace(vm.WorkItem?.Title) ? "Aufgabe" : vm.WorkItem.Title);
    }

    private void KehreToProjectBack()
    {
        DetailViewModel = _currentProjectDetailViewModel;
        if (_currentProjectDetailViewModel is not null)
            DetailTitleChangeAction?.Invoke(_currentProjectDetailViewModel.ProjectName);
        _ = LoadRepositoriesSuggestionsAsync(CancellationToken.None);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _loadSemaphore.Dispose();
    }
}
