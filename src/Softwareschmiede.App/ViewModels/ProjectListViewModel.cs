using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;

namespace Softwareschmiede.App.ViewModels;

/// <summary>ViewModel für die Projektliste.</summary>
public sealed class ProjectListViewModel : ViewModelBase, IDisposable
{
    private readonly ProjektService _projektService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ProjectListViewModel> _logger;
    private readonly SemaphoreSlim _ladenSemaphore = new(1, 1);

    private Projekt? _selectedProjekt;
    private bool _isLoading;
    private string? _fehlerMeldung;
    private ViewModelBase? _detailViewModel;
    private ProjectDetailViewModel? _currentProjectDetailViewModel;

    /// <summary>Liste aller Projekte.</summary>
    public ObservableCollection<Projekt> Projekte { get; } = new();

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
            if (!ReferenceEquals(old, value) && !ReferenceEquals(old, _currentProjectDetailViewModel) && old is IDisposable d) d.Dispose();
        }
    }

    /// <summary>Gibt an, ob Daten geladen werden.</summary>
    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
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

    /// <summary>Wird aufgerufen, wenn sich der Titel des Detailbereichs ändert (z. B. Projektname). Null bedeutet, kein Detailtitel aktiv.</summary>
    public Action<string?>? DetailTitelAenderungAction { get; set; }

    /// <inheritdoc cref="ProjectListViewModel"/>
    public ProjectListViewModel(
        ProjektService projektService,
        IServiceProvider serviceProvider,
        ILogger<ProjectListViewModel> logger)
    {
        _projektService = projektService;
        _serviceProvider = serviceProvider;
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
    }

    private async Task LadenAsync(CancellationToken ct)
    {
        await _ladenSemaphore.WaitAsync(ct);
        try
        {
            IsLoading = true;
            FehlerMeldung = null;

            var projekte = await _projektService.GetAllAsync(ct);
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
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _ladenSemaphore.Dispose();
    }
}
