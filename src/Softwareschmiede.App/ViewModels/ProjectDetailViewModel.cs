using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Softwareschmiede.App.Views;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.App.ViewModels;

/// <summary>ViewModel für die Projektdetailansicht mit Aufgabenliste.</summary>
public sealed class ProjectDetailViewModel : ViewModelBase, IDisposable
{
    private readonly ProjektService _projektService;
    private readonly AufgabeService _aufgabeService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ProjectDetailViewModel> _logger;

    /// <summary>Wird aufgerufen, wenn der Nutzer zur Listenansicht zurückkehren möchte.</summary>
    public Action? ZurueckAction { get; set; }

    /// <summary>Wird nach dem Erstellen oder Löschen eines Projekts aufgerufen, damit die Listenansicht die Liste aktualisiert.</summary>
    public Action? ProjektListeAktualisierenCallback { get; set; }

    private Guid _projektId;
    private Projekt? _projekt;
    private bool _isLoading;
    private string? _fehlerMeldung;
    private ViewModelBase? _selectedTaskViewModel;
    private CancellationTokenSource? _ladenCts;
    private string _projektName = string.Empty;
    private string? _projektBeschreibung;
    private GitRepository? _selectedRepository;
    private AufgabenFilterTyp _aufgabenFilter = AufgabenFilterTyp.Alle;
    private bool _isFilterOverlayVisible;

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
            var old = _selectedTaskViewModel;
            SetProperty(ref _selectedTaskViewModel, value);
            if (old is IDisposable disposable)
                disposable.Dispose();
        }
    }

    /// <summary>Liste der Aufgaben des Projekts.</summary>
    public ObservableCollection<Aufgabe> Aufgaben { get; } = new();

    /// <summary>Bearbeitbarer Projektname.</summary>
    public string ProjektName
    {
        get => _projektName;
        set => SetProperty(ref _projektName, value);
    }

    /// <summary>Bearbeitbare Projektbeschreibung.</summary>
    public string? ProjektBeschreibung
    {
        get => _projektBeschreibung;
        set => SetProperty(ref _projektBeschreibung, value);
    }

    /// <summary>Ausgewähltes Repository.</summary>
    public GitRepository? SelectedRepository
    {
        get => _selectedRepository;
        set => SetProperty(ref _selectedRepository, value);
    }

    /// <summary>Aktueller Aufgabenfilter.</summary>
    public AufgabenFilterTyp AufgabenFilter
    {
        get => _aufgabenFilter;
        set => SetProperty(ref _aufgabenFilter, value);
    }

    /// <summary>Gibt an, ob das Filter-Overlay sichtbar ist.</summary>
    public bool IsFilterOverlayVisible
    {
        get => _isFilterOverlayVisible;
        set => SetProperty(ref _isFilterOverlayVisible, value);
    }

    /// <summary>Lädt das Projekt neu.</summary>
    public ICommand LadenCommand { get; }

    /// <summary>Erstellt eine neue Aufgabe für das Projekt.</summary>
    public ICommand AufgabeErstellenCommand { get; }

    /// <summary>Öffnet eine Aufgabe im Detail.</summary>
    public ICommand AufgabeOeffnenCommand { get; }

    /// <summary>Navigiert zurück zur Projektübersicht.</summary>
    public ICommand ZurueckCommand { get; }

    /// <summary>Speichert Projektänderungen.</summary>
    public ICommand SpeichernCommand { get; }

    /// <summary>Löscht das Projekt.</summary>
    public ICommand LoeschenCommand { get; }

    /// <summary>Öffnet das Filter-Overlay.</summary>
    public ICommand FilterCommand { get; }

    /// <summary>Öffnet den Repository-Zuweisungs-Dialog.</summary>
    public ICommand RepositoryZuweisenCommand { get; }

    /// <summary>Öffnet das Repository im Browser.</summary>
    public ICommand RepositoryOeffnenCommand { get; }

    /// <summary>Bestätigungsfunktion für das Löschen. Kann in Tests überschrieben werden.</summary>
    public Func<bool> LoeschenBestaetigenFunc { get; set; } = () =>
        MessageBox.Show(
            "Soll das Projekt wirklich gelöscht werden?",
            "Löschen bestätigen",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning) == MessageBoxResult.Yes;

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
        ZurueckCommand = new RelayCommand(() => ZurueckAction?.Invoke());
        SpeichernCommand = new AsyncRelayCommand(ProjektSpeichernAsync, () => !string.IsNullOrWhiteSpace(_projektName));
        LoeschenCommand = new AsyncRelayCommand(ProjektLoeschenAsync, () => _projektId != Guid.Empty);
        FilterCommand = new RelayCommand(() => IsFilterOverlayVisible = !IsFilterOverlayVisible);
        RepositoryZuweisenCommand = new AsyncRelayCommand(RepositoryZuweisenAsync, () => _projektId != Guid.Empty);
        RepositoryOeffnenCommand = new AsyncRelayCommand(RepositoryOeffnenAsync, () => _selectedRepository != null);
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

            if (Projekt != null)
            {
                ProjektName = Projekt.Name;
                ProjektBeschreibung = Projekt.Beschreibung;
                SelectedRepository = Projekt.Repositories.FirstOrDefault();
            }
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

    private async Task ProjektSpeichernAsync(CancellationToken ct)
    {
        try
        {
            if (_projektId == Guid.Empty)
            {
                var neuesProjekt = await _projektService.CreateAsync(ProjektName.Trim(), ProjektBeschreibung?.Trim(), ct);
                ProjektId = neuesProjekt.Id;
                ProjektListeAktualisierenCallback?.Invoke();
            }
            else
            {
                await _projektService.UpdateAsync(_projektId, ProjektName.Trim(), ProjektBeschreibung?.Trim(), ct);
                await LadenAsync(ct);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Speichern des Projekts {ProjektId}.", _projektId);
            FehlerMeldung = $"Fehler: {ex.Message}";
        }
    }

    private async Task ProjektLoeschenAsync(CancellationToken ct)
    {
        if (_projektId == Guid.Empty)
            return;

        if (!LoeschenBestaetigenFunc())
            return;

        try
        {
            await _projektService.DeleteAsync(_projektId, ct);
            ProjektListeAktualisierenCallback?.Invoke();
            ZurueckAction?.Invoke();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Löschen des Projekts {ProjektId}.", _projektId);
            FehlerMeldung = $"Fehler: {ex.Message}";
        }
    }

    private async Task RepositoryZuweisenAsync(CancellationToken ct)
    {
        if (_projektId == Guid.Empty)
            return;

        try
        {
            var vm = _serviceProvider.GetRequiredService<RepositoryAssignViewModel>();
            await vm.LadenAsync(ct);
            var dialog = new RepositoryAssignDialog(vm)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };
            var confirmed = dialog.ShowDialog() == true;

            if (confirmed && vm.SelectedRepository is { } repo)
            {
                await _projektService.AddRepositoryAsync(
                    _projektId,
                    repo.PluginTyp,
                    repo.RepositoryUrl,
                    repo.RepositoryName,
                    ct);
                await LadenAsync(ct);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Zuweisen des Repositories.");
            FehlerMeldung = $"Fehler: {ex.Message}";
        }
    }

    private async Task RepositoryOeffnenAsync(CancellationToken ct)
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
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Öffnen der Repository-URL.");
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
