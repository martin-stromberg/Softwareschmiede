using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
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
    private readonly ProjektService _projektService;
    private readonly AufgabeService _aufgabeService;
    private readonly IServiceProvider _serviceProvider;
    private readonly IDialogService _dialogService;
    private readonly IPluginManager _pluginManager;
    private readonly ILogger<ProjectDetailViewModel> _logger;

    /// <summary>Wird aufgerufen, wenn der Nutzer zur Listenansicht zurückkehren möchte.</summary>
    public Action? ZurueckAction { get; set; }

    /// <summary>Wird nach dem Erstellen oder Löschen eines Projekts aufgerufen, damit die Listenansicht die Liste aktualisiert.</summary>
    public Func<Task>? ProjektListeAktualisierenCallback { get; set; }

    /// <summary>Wird aufgerufen, um zur separaten Aufgabendetailansicht zu navigieren.</summary>
    public Action<TaskDetailViewModel>? NavigateToTaskViewCallback { get; set; }

    /// <summary>Wird aufgerufen, um von der Aufgabendetailansicht zurück zur Projektdetailansicht zu navigieren.</summary>
    public Action? NavigateBackToProjectCallback { get; set; }

    private Guid _projektId;
    private Projekt? _projekt;
    private bool _isLoading;
    private string? _fehlerMeldung;
    private CancellationTokenSource? _ladenCts;
    private string _projektName = string.Empty;
    private string? _projektBeschreibung;
    private GitRepository? _selectedRepository;
    private AufgabenFilterTyp _aufgabenFilter = AufgabenFilterTyp.Alle;
    private bool _isFilterOverlayVisible;
    private bool _isLoadingIssues;
    private bool _kannIssuesLaden;
    private bool _disposed;
    private Guid _aktuelleAufgabeId;

    /// <summary>Die Projekt-ID, deren Details angezeigt werden.</summary>
    public Guid ProjektId
    {
        get => _projektId;
        set
        {
            if (SetProperty(ref _projektId, value))
            {
                OnPropertyChanged(nameof(IsNeuanlage));
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

    /// <summary>Liste der Aufgaben des Projekts.</summary>
    public ObservableCollection<Aufgabe> Aufgaben { get; } = new();

    /// <summary>Gefilterte Aufgaben (entsprechend AufgabenFilter).</summary>
    public ObservableCollection<Aufgabe> GefilterteAufgaben { get; } = new();

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
        set
        {
            if (SetProperty(ref _selectedRepository, value))
            {
                AktualisiereKannIssuesLaden();
            }
        }
    }

    /// <summary>Aktueller Aufgabenfilter.</summary>
    public AufgabenFilterTyp AufgabenFilter
    {
        get => _aufgabenFilter;
        set
        {
            if (SetProperty(ref _aufgabenFilter, value))
                AktualisiereGefilterteAufgaben();
        }
    }

    /// <summary>Gibt an, ob das Filter-Overlay sichtbar ist.</summary>
    public bool IsFilterOverlayVisible
    {
        get => _isFilterOverlayVisible;
        set => SetProperty(ref _isFilterOverlayVisible, value);
    }

    /// <summary>Gibt an, ob die Ansicht im Neuanlage-Modus ist (noch kein persistiertes Projekt).</summary>
    public bool IsNeuanlage => _projektId == Guid.Empty;

    /// <summary>Collection von geladenen Issues aus dem SCM-Plugin.</summary>
    public ObservableCollection<Issue> IssueVorschlaege { get; } = new();

    /// <summary>Gibt an, ob Issues gerade geladen werden.</summary>
    public bool IsLoadingIssues
    {
        get => _isLoadingIssues;
        private set => SetProperty(ref _isLoadingIssues, value);
    }

    /// <summary>true wenn das Repository ein SCM-Plugin mit Issue-Support hat.</summary>
    public bool KannIssuesLaden
    {
        get => _kannIssuesLaden;
        private set => SetProperty(ref _kannIssuesLaden, value);
    }

    /// <summary>Erstellt eine Aufgabe aus einem Issue-Vorschlag.</summary>
    public AsyncRelayCommand<Issue> AufgabeAusIssueErstellenCommand { get; }

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

    /// <inheritdoc cref="ProjectDetailViewModel"/>
    public ProjectDetailViewModel(
        ProjektService projektService,
        AufgabeService aufgabeService,
        IServiceProvider serviceProvider,
        IDialogService dialogService,
        IPluginManager pluginManager,
        ILogger<ProjectDetailViewModel> logger)
    {
        _projektService = projektService;
        _aufgabeService = aufgabeService;
        _serviceProvider = serviceProvider;
        _dialogService = dialogService;
        _pluginManager = pluginManager;
        _logger = logger;

        LadenCommand = new AsyncRelayCommand(LadenAsync);
        AufgabeErstellenCommand = new AsyncRelayCommand(
            AufgabeErstellenAsync,
            () => _projektId != Guid.Empty);
        AufgabeOeffnenCommand = new RelayCommand<Guid>(id => OeffneAufgabe(id));
        ZurueckCommand = new RelayCommand(() => ZurueckAction?.Invoke());
        SpeichernCommand = new AsyncRelayCommand(ProjektSpeichernAsync, () => !string.IsNullOrWhiteSpace(_projektName));
        LoeschenCommand = new AsyncRelayCommand(ProjektLoeschenAsync, () => _projektId != Guid.Empty);
        FilterCommand = new RelayCommand(() => IsFilterOverlayVisible = !IsFilterOverlayVisible);
        RepositoryZuweisenCommand = new AsyncRelayCommand(RepositoryZuweisenAsync, () => _projektId != Guid.Empty);
        RepositoryOeffnenCommand = new RelayCommand(RepositoryOeffnen, () => _selectedRepository != null);
        AufgabeAusIssueErstellenCommand = new AsyncRelayCommand<Issue>(AufgabeAusIssueErstellenAsync);
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
            AktualisiereGefilterteAufgaben();

            if (Projekt != null)
            {
                ProjektName = Projekt.Name;
                ProjektBeschreibung = Projekt.Beschreibung;
                SelectedRepository = Projekt.Repositories.FirstOrDefault();
            }

            await LadenIssuesAsync(ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Laden des Projekts {ProjektId}.", _projektId);
            SetFehler(ex);
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
            AktualisiereGefilterteAufgaben();

            OeffneAufgabe(aufgabe.Id);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Erstellen einer Aufgabe.");
            SetFehler(ex);
        }
    }

    private async Task ProjektSpeichernAsync(CancellationToken ct)
    {
        try
        {
            if (_projektId == Guid.Empty)
            {
                await _projektService.CreateAsync(ProjektName.Trim(), ProjektBeschreibung?.Trim(), ct);
                try
                {
                    await (ProjektListeAktualisierenCallback?.Invoke() ?? Task.CompletedTask);
                }
                catch (Exception callbackEx)
                {
                    _logger.LogError(callbackEx, "Fehler im ProjektListeAktualisierenCallback nach Projekterstellung.");
                }
                ZurueckAction?.Invoke();
            }
            else
            {
                await _projektService.UpdateAsync(_projektId, ProjektName.Trim(), ProjektBeschreibung?.Trim(), ct);
                try
                {
                    await (ProjektListeAktualisierenCallback?.Invoke() ?? Task.CompletedTask);
                }
                catch (Exception callbackEx)
                {
                    _logger.LogError(callbackEx, "Fehler im ProjektListeAktualisierenCallback nach Projektaktualisierung.");
                }
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
            SetFehler(ex);
        }
    }

    private async Task ProjektLoeschenAsync(CancellationToken ct)
    {
        if (_projektId == Guid.Empty)
            return;

        if (!_dialogService.BestaetigenDialog("Soll das Projekt wirklich gelöscht werden?", "Löschen bestätigen"))
            return;

        try
        {
            await _projektService.DeleteAsync(_projektId, ct);
            try
            {
                await (ProjektListeAktualisierenCallback?.Invoke() ?? Task.CompletedTask);
            }
            catch (Exception callbackEx)
            {
                _logger.LogError(callbackEx, "Fehler im ProjektListeAktualisierenCallback nach Projektlöschung.");
            }
            ZurueckAction?.Invoke();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Löschen des Projekts {ProjektId}.", _projektId);
            SetFehler(ex);
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
            var confirmed = _dialogService.RepositoryZuweisenDialog(vm);

            if (_disposed || ct.IsCancellationRequested)
                return;

            if (confirmed && vm.SelectedRepository is { } repo && vm.SelectedScmPlugin is { } scmPlugin)
            {
                await _projektService.AddRepositoryAsync(
                    _projektId,
                    scmPlugin.PluginPrefix,
                    repo.Url,
                    repo.Name,
                    ct);

                if (!_disposed && !ct.IsCancellationRequested)
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
            SetFehler(ex);
        }
    }

    private void RepositoryOeffnen()
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
            SetFehler(ex);
        }
    }

    private void OeffneAufgabe(Guid id)
    {
        var vm = _serviceProvider.GetRequiredService<TaskDetailViewModel>();
        vm.ZurueckAction = () => NavigateBackToProjectCallback?.Invoke();
        _aktuelleAufgabeId = id;
        vm.AufgabeListeAktualisierenCallback = ReloadAufgabenListAsync;
        vm.AufgabeId = id;
        NavigateToTaskViewCallback?.Invoke(vm);
    }

    private async Task ReloadAufgabenListAsync()
    {
        var aktualisiert = await _aufgabeService.GetByIdAsync(_aktuelleAufgabeId);
        if (aktualisiert is null)
            return;

        ReplaceOrAddAufgabe(aktualisiert);
        AktualisiereGefilterteAufgaben();
    }

    private void ReplaceOrAddAufgabe(Aufgabe aufgabe)
    {
        for (var i = 0; i < Aufgaben.Count; i++)
        {
            if (Aufgaben[i].Id == aufgabe.Id)
            {
                Aufgaben[i] = aufgabe;
                return;
            }
        }
        Aufgaben.Add(aufgabe);
    }

    private void AktualisiereGefilterteAufgaben()
    {
        GefilterteAufgaben.Clear();
        var quelle = _aufgabenFilter switch
        {
            AufgabenFilterTyp.Aktiv => Aufgaben.Where(a => a.Status != AufgabeStatus.Archiviert),
            AufgabenFilterTyp.Archiviert => Aufgaben.Where(a => a.Status == AufgabeStatus.Archiviert),
            _ => Aufgaben
        };
        foreach (var aufgabe in quelle)
            GefilterteAufgaben.Add(aufgabe);
    }

    private async Task LadenIssuesAsync(CancellationToken ct)
    {
        var repository = _selectedRepository;
        if (repository == null)
            return;

        var scmPlugins = _pluginManager.GetSourceCodeManagementPlugins();
        var gitPlugin = scmPlugins
            .FirstOrDefault(p => string.Equals(p.PluginPrefix, repository.PluginTyp, StringComparison.OrdinalIgnoreCase));
        if (gitPlugin == null)
            return;

        IsLoadingIssues = true;
        IssueVorschlaege.Clear();

        try
        {
            var bereitsKonvertierteNummern = Aufgaben
                .Where(a => a.IssueReferenz?.IssueNummer != null)
                .Select(a => a.IssueReferenz!.IssueNummer!.Value)
                .ToHashSet();

            var issues = await gitPlugin.GetIssuesAsync(repository.RepositoryUrl, ct);
            foreach (var issue in issues)
            {
                if (!bereitsKonvertierteNummern.Contains(issue.Nummer))
                    IssueVorschlaege.Add(issue);
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

    private async Task AufgabeAusIssueErstellenAsync(Issue? issue, CancellationToken ct)
    {
        if (issue == null || _projektId == Guid.Empty)
            return;

        if (!_dialogService.BestaetigenDialog(
                $"Issue '{issue.Titel}' als Aufgabe erstellen?",
                "Issue konvertieren"))
            return;

        try
        {
            var aufgabe = await _aufgabeService.CreateFromIssueAsync(
                _projektId,
                issue,
                _selectedRepository?.Id,
                ct);

            var zuEntfernen = IssueVorschlaege.FirstOrDefault(i => i.Nummer == issue.Nummer);
            if (zuEntfernen != null)
                IssueVorschlaege.Remove(zuEntfernen);

            Aufgaben.Add(aufgabe);
            AktualisiereGefilterteAufgaben();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Erstellen einer Aufgabe aus Issue #{IssueNummer}.", issue.Nummer);
            SetFehler(ex);
        }
    }

    private void AktualisiereKannIssuesLaden()
    {
        KannIssuesLaden = _selectedRepository != null
            && _pluginManager.GetSourceCodeManagementPlugins()
                .Any(p => string.Equals(p.PluginPrefix, _selectedRepository.PluginTyp, StringComparison.OrdinalIgnoreCase));
    }

    private void SetFehler(Exception ex) => SetFehler(ref _fehlerMeldung, nameof(FehlerMeldung), ex);

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        _ladenCts?.Cancel();
        _ladenCts?.Dispose();
        _ladenCts = null;
    }
}
