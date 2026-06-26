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
    private readonly ProjektService _projektService;
    private readonly IServiceProvider _serviceProvider;
    private readonly IPluginManager _pluginManager;
    private readonly ILogger<ProjectListViewModel> _logger;
    private readonly SemaphoreSlim _ladenSemaphore = new(1, 1);

    private Projekt? _selectedProjekt;
    private bool _isLoading;
    private bool _isLoadingRepositories;
    private string? _fehlerMeldung;
    private ViewModelBase? _detailViewModel;
    private ProjectDetailViewModel? _currentProjectDetailViewModel;

    /// <summary>Liste aller Projekte.</summary>
    public ObservableCollection<Projekt> Projekte { get; } = new();

    /// <summary>Liste der unzugeordneten Repositories aus allen SCM-Plugins.</summary>
    public ObservableCollection<AvailableRepository> UnassignedRepositories { get; } = new();

    /// <summary>Das aktuell ausgewählte Projekt.</summary>
    public Projekt? SelectedProjekt
    {
        get => _selectedProjekt;
        set
        {
            var changed = SetProperty(ref _selectedProjekt, value);
            if (value is not null && (changed || DetailViewModel is null))
            {
                ZeigeDetail(value.Id);
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
    public string? FehlerMeldung
    {
        get => _fehlerMeldung;
        private set => SetProperty(ref _fehlerMeldung, value);
    }

    /// <summary>Lädt die Projektliste.</summary>
    public ICommand LadenCommand { get; }

    /// <summary>Öffnet die Detailansicht im Anlage-Modus.</summary>
    public ICommand ZeigeErstellungsFormularCommand { get; }

    /// <summary>Archiviert das ausgewählte Projekt.</summary>
    public ICommand ProjektArchivierenCommand { get; }

    /// <summary>Schließt die Detailansicht.</summary>
    public ICommand SchliesseDetailCommand { get; }

    /// <summary>Wählt ein Projekt aus und zeigt die Details.</summary>
    public ICommand WaehleProjektCommand { get; }

    /// <summary>Erstellt ein neues Projekt aus einem unzugeordneten Repository und ordnet das Repository zu.</summary>
    public AsyncRelayCommand<AvailableRepository> RepositoryDoubleclickCommand { get; }

    /// <summary>Wird aufgerufen, wenn sich der Titel des Detailbereichs ändert (z. B. Projektname). Null bedeutet, kein Detailtitel aktiv.</summary>
    public Action<string?>? DetailTitelAenderungAction { get; set; }

    /// <inheritdoc cref="ProjectListViewModel"/>
    public ProjectListViewModel(
        ProjektService projektService,
        IServiceProvider serviceProvider,
        IPluginManager pluginManager,
        ILogger<ProjectListViewModel> logger)
    {
        _projektService = projektService;
        _serviceProvider = serviceProvider;
        _pluginManager = pluginManager;
        _logger = logger;

        LadenCommand = new AsyncRelayCommand(LadenAsync);
        ZeigeErstellungsFormularCommand = new RelayCommand(ZeigeDetailErstellungsFormular);
        ProjektArchivierenCommand = new AsyncRelayCommand(
            ProjektArchivierenAsync,
            () => SelectedProjekt is not null);
        SchliesseDetailCommand = new RelayCommand(() => DetailViewModel = null);
        WaehleProjektCommand = new RelayCommand<Projekt>(projekt =>
        {
            if (projekt is not null)
            {
                SelectedProjekt = projekt;
            }
        });
        RepositoryDoubleclickCommand = new AsyncRelayCommand<AvailableRepository>(ProjektAusRepositoryErstellen);
    }

    private async Task LadenAsync(CancellationToken ct)
    {
        await _ladenSemaphore.WaitAsync(ct);
        try
        {
            IsLoading = true;
            FehlerMeldung = null;

            var projektTask = _projektService.GetAllAsync(ct);
            var suggestionsTask = LadenRepositorienSuggestionsAsync(ct);
            await Task.WhenAll(projektTask, suggestionsTask);

            var projekte = projektTask.Result;
            Projekte.Clear();
            foreach (var projekt in projekte)
                Projekte.Add(projekt);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Laden der Projekte.");
            SetFehler(ref _fehlerMeldung, nameof(FehlerMeldung), ex);
        }
        finally
        {
            IsLoading = false;
            _ladenSemaphore.Release();
        }
    }

    private async Task LadenRepositorienSuggestionsAsync(CancellationToken ct)
    {
        IsLoadingRepositories = true;
        try
        {
            var repos = await _projektService.GetUnassignedRepositoriesAsync(ct);
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

    private async Task ProjektAusRepositoryErstellen(AvailableRepository? repo, CancellationToken ct)
    {
        if (repo is null)
            return;

        try
        {
            var pluginPrefix = await FindPluginPrefixForRepositoryAsync(repo.Url, ct);
            var projekt = await _projektService.CreateAsync(repo.Name, null, ct);
            await _projektService.AddRepositoryAsync(projekt.Id, pluginPrefix, repo.Url, repo.Name, ct);
            await NeuesProjektHinzufuegen();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Erstellen eines Projekts aus Repository '{RepositoryName}'.", repo.Name);
            SetFehler(ref _fehlerMeldung, nameof(FehlerMeldung), ex);
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

    private async Task ProjektArchivierenAsync(CancellationToken ct)
    {
        if (SelectedProjekt is null)
            return;

        try
        {
            await _projektService.ArchivierenAsync(SelectedProjekt.Id, ct);
            Projekte.Remove(SelectedProjekt);
            SelectedProjekt = null;
            DetailViewModel = null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Archivieren des Projekts.");
            SetFehler(ref _fehlerMeldung, nameof(FehlerMeldung), ex);
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
            if (e.PropertyName == nameof(ProjectDetailViewModel.ProjektName))
                DetailTitelAenderungAction?.Invoke(viewModel.ProjektName);
        };
        viewModel.PropertyChanged += propertyChangedHandler;

        viewModel.ZurueckAction = () =>
        {
            viewModel.PropertyChanged -= propertyChangedHandler;
            DetailViewModel = null;
            DetailTitelAenderungAction?.Invoke(null);
        };
        viewModel.ProjektListeAktualisierenCallback = NeuesProjektHinzufuegen;
        viewModel.NavigateToTaskViewCallback = ZeigeTaskDetailView;
        viewModel.NavigateBackToProjectCallback = KehreZuProjectZurueck;
        _currentProjectDetailViewModel = viewModel;
    }

    private void ZeigeDetail(Guid projektId)
    {
        var viewModel = _serviceProvider.GetRequiredService<ProjectDetailViewModel>();
        InitDetailViewModel(viewModel);
        viewModel.ProjektId = projektId;
        DetailViewModel = viewModel;
    }

    private void ZeigeDetailErstellungsFormular()
    {
        var viewModel = _serviceProvider.GetRequiredService<ProjectDetailViewModel>();
        InitDetailViewModel(viewModel);
        viewModel.ProjektId = Guid.Empty;
        viewModel.ProjektName = string.Empty;
        DetailViewModel = viewModel;
    }

    private async Task NeuesProjektHinzufuegen()
    {
        await LadenAsync(CancellationToken.None);
    }

    private void ZeigeTaskDetailView(TaskDetailViewModel vm)
    {
        DetailViewModel = vm;
    }

    private void KehreZuProjectZurueck()
    {
        DetailViewModel = _currentProjectDetailViewModel;
        _ = LadenRepositorienSuggestionsAsync(CancellationToken.None);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _ladenSemaphore.Dispose();
    }
}
