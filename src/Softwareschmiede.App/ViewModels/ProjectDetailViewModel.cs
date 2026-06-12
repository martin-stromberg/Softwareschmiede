using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;

namespace Softwareschmiede.App.ViewModels;

/// <summary>ViewModel für die Projektdetailansicht mit Aufgabenliste.</summary>
public sealed class ProjectDetailViewModel : ViewModelBase, IDisposable
{
    private readonly ProjektService _projektService;
    private readonly AufgabeService _aufgabeService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ProjectDetailViewModel> _logger;

    private Guid _projektId;
    private Projekt? _projekt;
    private bool _isLoading;
    private string? _fehlerMeldung;
    private ViewModelBase? _selectedTaskViewModel;
    private CancellationTokenSource? _ladenCts;

    /// <summary>Die Projekt-ID, deren Details angezeigt werden.</summary>
    public Guid ProjektId
    {
        get => _projektId;
        set
        {
            if (SetProperty(ref _projektId, value))
            {
                _ladenCts?.Cancel();
                _ladenCts?.Dispose();
                _ladenCts = new CancellationTokenSource();
                _ = LadenAsync(_ladenCts.Token);
            }
        }
    }

    /// <summary>Das geladene Projekt.</summary>
    public Projekt? Projekt
    {
        get => _projekt;
        private set => SetProperty(ref _projekt, value);
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

    /// <summary>Das aktuell angezeigte Aufgaben-ViewModel.</summary>
    public ViewModelBase? SelectedTaskViewModel
    {
        get => _selectedTaskViewModel;
        private set
        {
            if (_selectedTaskViewModel is IDisposable disposable)
                disposable.Dispose();
            SetProperty(ref _selectedTaskViewModel, value);
        }
    }

    /// <summary>Liste der Aufgaben des Projekts.</summary>
    public ObservableCollection<Aufgabe> Aufgaben { get; } = new();

    /// <summary>Lädt das Projekt neu.</summary>
    public ICommand LadenCommand { get; }

    /// <summary>Erstellt eine neue Aufgabe für das Projekt.</summary>
    public ICommand AufgabeErstellenCommand { get; }

    /// <summary>Öffnet eine Aufgabe im Detail.</summary>
    public ICommand AufgabeOeffnenCommand { get; }

    /// <inheritdoc cref="ProjectDetailViewModel"/>
    public ProjectDetailViewModel(
        ProjektService projektService,
        AufgabeService aufgabeService,
        IServiceProvider serviceProvider,
        ILogger<ProjectDetailViewModel> logger)
    {
        _projektService = projektService;
        _aufgabeService = aufgabeService;
        _serviceProvider = serviceProvider;
        _logger = logger;

        LadenCommand = new AsyncRelayCommand(ct => LadenAsync(ct));
        AufgabeErstellenCommand = new AsyncRelayCommand(
            AufgabeErstellenAsync,
            () => _projektId != Guid.Empty);
        AufgabeOeffnenCommand = new RelayCommand<Guid>(id =>
        {
            var vm = _serviceProvider.GetRequiredService<TaskDetailViewModel>();
            vm.AufgabeId = id;
            SelectedTaskViewModel = vm;
        });
    }

    private async Task LadenAsync(CancellationToken ct)
    {
        if (_projektId == Guid.Empty)
            return;

        IsLoading = true;
        FehlerMeldung = null;

        try
        {
            Projekt = await _projektService.GetDetailAsync(_projektId, ct);
            var aufgaben = await _aufgabeService.GetByProjektAsync(_projektId, ct);
            Aufgaben.Clear();
            foreach (var aufgabe in aufgaben)
                Aufgaben.Add(aufgabe);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Laden des Projekts {ProjektId}.", _projektId);
            FehlerMeldung = $"Fehler: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task AufgabeErstellenAsync(CancellationToken ct)
    {
        if (_projektId == Guid.Empty)
            return;

        try
        {
            var aufgabe = await _aufgabeService.CreateAsync(
                _projektId,
                "Neue Aufgabe",
                string.Empty,
                null,
                ct);

            Aufgaben.Add(aufgabe);

            var vm = _serviceProvider.GetRequiredService<TaskDetailViewModel>();
            vm.AufgabeId = aufgabe.Id;
            SelectedTaskViewModel = vm;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Erstellen einer Aufgabe.");
            FehlerMeldung = $"Fehler: {ex.Message}";
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _ladenCts?.Cancel();
        _ladenCts?.Dispose();
        if (_selectedTaskViewModel is IDisposable disposable)
            disposable.Dispose();
    }
}
