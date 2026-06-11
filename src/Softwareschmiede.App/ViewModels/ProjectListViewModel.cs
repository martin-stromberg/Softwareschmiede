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
    private string _neuerProjektName = string.Empty;
    private string _neuerProjektBeschreibung = string.Empty;
    private bool _isCreateFormVisible;
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
                ZeigeDetailAsync(value.Id);
            }
        }
    }

    /// <summary>Das aktuell angezeigte Detail-ViewModel.</summary>
    public ViewModelBase? DetailViewModel
    {
        get => _detailViewModel;
        private set => SetProperty(ref _detailViewModel, value);
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

    /// <summary>Name für das neue Projekt.</summary>
    public string NeuerProjektName
    {
        get => _neuerProjektName;
        set => SetProperty(ref _neuerProjektName, value);
    }

    /// <summary>Beschreibung für das neue Projekt.</summary>
    public string NeuerProjektBeschreibung
    {
        get => _neuerProjektBeschreibung;
        set => SetProperty(ref _neuerProjektBeschreibung, value);
    }

    /// <summary>Gibt an, ob das Erstellungsformular sichtbar ist.</summary>
    public bool IsCreateFormVisible
    {
        get => _isCreateFormVisible;
        set => SetProperty(ref _isCreateFormVisible, value);
    }

    /// <summary>Lädt die Projektliste.</summary>
    public ICommand LadenCommand { get; }

    /// <summary>Zeigt das Erstellungsformular an.</summary>
    public ICommand ZeigeErstellungsFormularCommand { get; }

    /// <summary>Erstellt ein neues Projekt.</summary>
    public ICommand ProjektErstellenCommand { get; }

    /// <summary>Archiviert das ausgewählte Projekt.</summary>
    public ICommand ProjektArchivierenCommand { get; }

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
        ZeigeErstellungsFormularCommand = new RelayCommand(() => IsCreateFormVisible = true);
        ProjektErstellenCommand = new AsyncRelayCommand(
            ProjektErstellenAsync,
            () => !string.IsNullOrWhiteSpace(NeuerProjektName));
        ProjektArchivierenCommand = new AsyncRelayCommand(
            ProjektArchivierenAsync,
            () => SelectedProjekt is not null);
    }

    private async Task LadenAsync(CancellationToken ct)
    {
        IsLoading = true;
        FehlerMeldung = null;

        try
        {
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
            FehlerMeldung = $"Fehler: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task ProjektErstellenAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(NeuerProjektName))
            return;

        try
        {
            var projekt = await _projektService.CreateAsync(
                NeuerProjektName.Trim(),
                string.IsNullOrWhiteSpace(NeuerProjektBeschreibung) ? null : NeuerProjektBeschreibung.Trim(),
                ct);

            Projekte.Add(projekt);
            SelectedProjekt = projekt;
            NeuerProjektName = string.Empty;
            NeuerProjektBeschreibung = string.Empty;
            IsCreateFormVisible = false;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Erstellen des Projekts.");
            FehlerMeldung = $"Fehler: {ex.Message}";
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

    private void ZeigeDetailAsync(Guid projektId)
    {
        var viewModel = _serviceProvider.GetRequiredService<ProjectDetailViewModel>();
        viewModel.ProjektId = projektId;
        DetailViewModel = viewModel;
    }
}
