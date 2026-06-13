using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;

namespace Softwareschmiede.App.ViewModels;

/// <summary>ViewModel für das Dashboard mit Zusammenfassung aller Projekte und Aufgaben.</summary>
public sealed class DashboardViewModel : ViewModelBase
{
    private readonly ProjektService _projektService;
    private readonly AufgabeService _aufgabeService;
    private readonly AufgabeRecoveryService _recoveryService;
    private readonly ILogger<DashboardViewModel> _logger;

    private int _projektAnzahl;
    private int _aktiveAufgaben;
    private int _wartendAufgaben;
    private bool _isLoading;
    private string? _fehlerMeldung;

    /// <summary>Anzahl aller Projekte.</summary>
    public int ProjektAnzahl
    {
        get => _projektAnzahl;
        private set => SetProperty(ref _projektAnzahl, value);
    }

    /// <summary>Anzahl aktiver Aufgaben (Status InArbeit).</summary>
    public int AktiveAufgaben
    {
        get => _aktiveAufgaben;
        private set => SetProperty(ref _aktiveAufgaben, value);
    }

    /// <summary>Anzahl wartender Aufgaben (Status Wartend).</summary>
    public int WartendAufgaben
    {
        get => _wartendAufgaben;
        private set => SetProperty(ref _wartendAufgaben, value);
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

    /// <summary>Liste von Recovery-Kandidaten.</summary>
    public ObservableCollection<Guid> RecoveryKandidaten { get; } = new();

    /// <summary>Gibt an, ob es Recovery-Kandidaten gibt.</summary>
    public bool HatRecoveryKandidaten => RecoveryKandidaten.Count > 0;

    /// <summary>Liste der zuletzt aktiven Projekte.</summary>
    public ObservableCollection<Projekt> LetzteProjects { get; } = new();

    /// <summary>Lädt die Dashboard-Daten neu.</summary>
    public ICommand LadenCommand { get; }

    /// <inheritdoc cref="DashboardViewModel"/>
    public DashboardViewModel(
        ProjektService projektService,
        AufgabeService aufgabeService,
        AufgabeRecoveryService recoveryService,
        ILogger<DashboardViewModel> logger)
    {
        _projektService = projektService;
        _aufgabeService = aufgabeService;
        _recoveryService = recoveryService;
        _logger = logger;

        LadenCommand = new AsyncRelayCommand(LadenAsync);
        RecoveryKandidaten.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HatRecoveryKandidaten));
    }

    private async Task LadenAsync(CancellationToken ct)
    {
        IsLoading = true;
        FehlerMeldung = null;

        try
        {
            var projekte = await _projektService.GetAllAsync(ct);
            ProjektAnzahl = projekte.Count;

            RecoveryKandidaten.Clear();
            var kandidaten = await _recoveryService.ScanForRecoveryCandidatesAsync(ct);
            foreach (var id in kandidaten)
                RecoveryKandidaten.Add(id);

            LetzteProjects.Clear();
            foreach (var projekt in projekte.Take(5))
                LetzteProjects.Add(projekt);

            var (aktiveCount, wartendCount) = await _aufgabeService.GetAktiveUndWartendeCountAsync(ct);
            AktiveAufgaben = aktiveCount;
            WartendAufgaben = wartendCount;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Laden der Dashboard-Daten.");
            FehlerMeldung = $"Fehler beim Laden: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
