using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.App.ViewModels;

/// <summary>ViewModel für die Aufgabenliste eines Projekts mit Filterung.</summary>
public sealed class TaskListViewModel : ViewModelBase
{
    private readonly AufgabeService _aufgabeService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TaskListViewModel> _logger;

    private Guid _projektId;
    private AufgabeStatus? _statusFilter;
    private bool _isLoading;
    private string? _fehlerMeldung;

    /// <summary>Die Projekt-ID, deren Aufgaben angezeigt werden.</summary>
    public Guid ProjektId
    {
        get => _projektId;
        set
        {
            if (SetProperty(ref _projektId, value))
                _ = LadenAsync(CancellationToken.None);
        }
    }

    /// <summary>Optionaler Statusfilter.</summary>
    public AufgabeStatus? StatusFilter
    {
        get => _statusFilter;
        set
        {
            if (SetProperty(ref _statusFilter, value))
                AktualisierteGefilterteAufgaben();
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

    /// <summary>Alle geladenen Aufgaben.</summary>
    public ObservableCollection<Aufgabe> AlleAufgaben { get; } = new();

    /// <summary>Gefilterte Aufgaben (entsprechend StatusFilter).</summary>
    public ObservableCollection<Aufgabe> GefiltereAufgaben { get; } = new();

    /// <summary>Lädt die Aufgabenliste.</summary>
    public ICommand LadenCommand { get; }

    /// <summary>Navigiert zur Aufgabendetailansicht.</summary>
    public ICommand AufgabeOeffnenCommand { get; }

    /// <summary>Setzt den Statusfilter zurück.</summary>
    public ICommand FilterZuruecksetzenCommand { get; }

    /// <summary>Event: Aufgabe wurde ausgewählt, Detail soll angezeigt werden.</summary>
    public event Action<Guid>? AufgabeAusgewaehlt;

    /// <inheritdoc cref="TaskListViewModel"/>
    public TaskListViewModel(
        AufgabeService aufgabeService,
        IServiceProvider serviceProvider,
        ILogger<TaskListViewModel> logger)
    {
        _aufgabeService = aufgabeService;
        _serviceProvider = serviceProvider;
        _logger = logger;

        LadenCommand = new AsyncRelayCommand(ct => LadenAsync(ct));
        AufgabeOeffnenCommand = new RelayCommand<Guid>(id => AufgabeAusgewaehlt?.Invoke(id));
        FilterZuruecksetzenCommand = new RelayCommand(() => StatusFilter = null);
    }

    private async Task LadenAsync(CancellationToken ct)
    {
        if (_projektId == Guid.Empty)
            return;

        IsLoading = true;
        FehlerMeldung = null;

        try
        {
            var aufgaben = await _aufgabeService.GetByProjektAsync(_projektId, ct);
            AlleAufgaben.Clear();
            foreach (var aufgabe in aufgaben)
                AlleAufgaben.Add(aufgabe);
            AktualisierteGefilterteAufgaben();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Laden der Aufgaben für Projekt {ProjektId}.", _projektId);
            FehlerMeldung = $"Fehler: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void AktualisierteGefilterteAufgaben()
    {
        GefiltereAufgaben.Clear();
        var quelle = _statusFilter.HasValue
            ? AlleAufgaben.Where(a => a.Status == _statusFilter.Value)
            : AlleAufgaben;
        foreach (var aufgabe in quelle)
            GefiltereAufgaben.Add(aufgabe);
    }
}
