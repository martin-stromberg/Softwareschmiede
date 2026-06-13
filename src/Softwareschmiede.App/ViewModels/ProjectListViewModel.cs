using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;

namespace Softwareschmiede.App.ViewModels;

/// <summary>ViewModel für die Projektliste.</summary>
public sealed class ProjectListViewModel : ViewModelBase
{
    private readonly ProjektService _projektService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ProjectListViewModel> _logger;

    private Projekt? _selectedProjekt;
    private bool _isLoading;
    private string? _fehlerMeldung;
    private ViewModelBase? _detailViewModel;

    /// <summary>Liste aller Projekte.</summary>
    public ObservableCollection<Projekt> Projekte { get; } = new();

    /// <summary>Das aktuell ausgewählte Projekt.</summary>
    public Projekt? SelectedProjekt
    {
        get => _selectedProjekt;
        set
        {
            if (SetProperty(ref _selectedProjekt, value) && value is not null)
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
            if (old is IDisposable d) d.Dispose();
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

    private async Task LadenProjekteInternAsync(CancellationToken ct = default)
    {
        var projekte = await _projektService.GetAllAsync(ct);
        Projekte.Clear();
        foreach (var projekt in projekte)
            Projekte.Add(projekt);
    }

    private async Task LadenAsync(CancellationToken ct)
    {
        IsLoading = true;
        FehlerMeldung = null;

        try
        {
            await LadenProjekteInternAsync(ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Laden der Projekte.");
            FehlerMeldung = $"Fehler: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
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
            FehlerMeldung = $"Fehler: {ex.Message}";
        }
    }

    private void InitDetailViewModel(ProjectDetailViewModel viewModel)
    {
        viewModel.ZurueckAction = () => DetailViewModel = null;
        viewModel.ProjektListeAktualisierenCallback = NeuesProjektHinzufuegen;
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
        try
        {
            await LadenProjekteInternAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Nachladen der Projektliste.");
            FehlerMeldung = $"Fehler: {ex.Message}";
        }
    }
}
