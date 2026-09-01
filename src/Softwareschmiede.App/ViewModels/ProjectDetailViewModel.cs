using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Data;
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
    private readonly DirectoryStructureBrowserService? _directoryStructureService;

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
    private bool _kannAnforderungenLaden;
    private bool _disposed;
    private Guid _aktuelleAufgabeId;
    private readonly HashSet<string> _laufendeAlertKonvertierungen = new(StringComparer.OrdinalIgnoreCase);
    private string? _selectedRepositorySourceBranchName;
    private bool _isEditingSourceBranch;
    private ObservableCollection<string> _availableSourceBranchesForEdit = new();
    private string? _sourceBranchInputError;
    private string? _sourceBranchNameBeforeEdit;
    private bool _isLoadingSourceBranchesForEdit;
    private bool _isEditingSourceBranchManualInput;
    private string? _selectedInitialisierungsskript;
    private bool _isEditingInitialisierungsskript;
    private bool? _initialisierungsskriptLoadingFailed;
    private string? _initialisierungsskriptBeforeEdit;

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
                LadenAsync(_ladenCts.Token).SafeFireAndForget(_logger, "ProjectDetailViewModel.LadenAsync");
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

    /// <summary>Nicht beendete Aufgaben der Projektdetailansicht.</summary>
    public ObservableCollection<Aufgabe> NichtBeendeteAufgaben { get; } = new();

    /// <summary>Beendete Aufgaben der Projektdetailansicht.</summary>
    public ObservableCollection<Aufgabe> BeendeteAufgaben { get; } = new();

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
                IsEditingSourceBranch = false;
                AvailableSourceBranchesForEdit.Clear();
                IsEditingSourceBranchManualInput = false;
                SourceBranchInputError = null;
                SelectedRepositorySourceBranchName = value?.DefaultSourceBranchName;
                IsEditingInitialisierungsskript = false;
                InitialisierungsskriptSuggestionen.Clear();
                InitialisierungsskriptLoadingFailed = null;
                SelectedInitialisierungsskript = value?.InitialisierungKonfiguration?.InitialisierungsskriptRelativePath;
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
                AktualisiereAufgabenAnsichten();
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

    /// <summary>Collection von geladenen Anforderungen aus dem SCM-Plugin.</summary>
    public ObservableCollection<ScmRequirement> OffeneAnforderungen { get; } = new();

    /// <summary>Collection von geladenen Issues aus dem SCM-Plugin. Bleibt als kompatible Weiterleitung erhalten.</summary>
    public ObservableCollection<Issue> IssueVorschlaege { get; } = new();

    /// <summary>Gibt an, ob Issues gerade geladen werden.</summary>
    public bool IsLoadingIssues
    {
        get => _isLoadingIssues;
        private set => SetProperty(ref _isLoadingIssues, value);
    }

    /// <summary>true wenn das Repository ein SCM-Plugin mit Anforderungssupport hat.</summary>
    public bool KannAnforderungenLaden
    {
        get => _kannAnforderungenLaden;
        private set
        {
            if (SetProperty(ref _kannAnforderungenLaden, value))
            {
                OnPropertyChanged(nameof(KannIssuesLaden));
            }
        }
    }

    /// <summary>true wenn das Repository ein SCM-Plugin mit Issue-Support hat. Kompatible Weiterleitung.</summary>
    public bool KannIssuesLaden => KannAnforderungenLaden;

    /// <summary>Aktuell konfigurierter Basis-Branch des ausgewählten Repositories, von dem neue Feature-Branches abgezweigt werden. <c>null</c> bedeutet Standard-Branch.</summary>
    public string? SelectedRepositorySourceBranchName
    {
        get => _selectedRepositorySourceBranchName;
        set => SetProperty(ref _selectedRepositorySourceBranchName, value);
    }

    /// <summary>Gibt an, ob der Basis-Branch des ausgewählten Repositories gerade bearbeitet wird.</summary>
    public bool IsEditingSourceBranch
    {
        get => _isEditingSourceBranch;
        private set => SetProperty(ref _isEditingSourceBranch, value);
    }

    /// <summary>Verfügbare Branches des ausgewählten Repositories für die Basis-Branch-Bearbeitung.</summary>
    public ObservableCollection<string> AvailableSourceBranchesForEdit
    {
        get => _availableSourceBranchesForEdit;
        private set => SetProperty(ref _availableSourceBranchesForEdit, value);
    }

    /// <summary>Validierungsfehler bei der Basis-Branch-Bearbeitung.</summary>
    public string? SourceBranchInputError
    {
        get => _sourceBranchInputError;
        private set => SetProperty(ref _sourceBranchInputError, value);
    }

    /// <summary>Gibt an, ob die verfügbaren Branches für die Basis-Branch-Bearbeitung gerade geladen werden.</summary>
    public bool IsLoadingSourceBranchesForEdit
    {
        get => _isLoadingSourceBranchesForEdit;
        private set => SetProperty(ref _isLoadingSourceBranchesForEdit, value);
    }

    /// <summary>Gibt an, ob der Basis-Branch im Bearbeitungsmodus manuell eingegeben wird (keine Vorschlagsliste verfügbar).</summary>
    public bool IsEditingSourceBranchManualInput
    {
        get => _isEditingSourceBranchManualInput;
        private set => SetProperty(ref _isEditingSourceBranchManualInput, value);
    }

    /// <summary>Liste ausführbarer Dateien aus dem Remote-Repository für die Initialisierungsskript-Auswahl.</summary>
    public ObservableCollection<string> InitialisierungsskriptSuggestionen { get; } = new();

    /// <summary>Gefilterte Sicht auf <see cref="InitialisierungsskriptSuggestionen"/>, eingeengt anhand des in <see cref="SelectedInitialisierungsskript"/> eingegebenen Texts.</summary>
    public CollectionView InitialisierungsskriptSuggestionenView { get; }

    /// <summary>Vom Benutzer ausgewähltes oder manuell eingegebenes Initialisierungsskript.</summary>
    public string? SelectedInitialisierungsskript
    {
        get => _selectedInitialisierungsskript;
        set => SetProperty(ref _selectedInitialisierungsskript, value, () => InitialisierungsskriptSuggestionenView.Refresh());
    }

    /// <summary>Gibt an, ob das Initialisierungsskript des ausgewählten Repositories gerade bearbeitet wird.</summary>
    public bool IsEditingInitialisierungsskript
    {
        get => _isEditingInitialisierungsskript;
        private set => SetProperty(ref _isEditingInitialisierungsskript, value);
    }

    /// <summary>Gibt an, ob das Laden der Initialisierungsskript-Vorschläge fehlgeschlagen ist. <c>null</c> bedeutet: noch nicht geladen.</summary>
    public bool? InitialisierungsskriptLoadingFailed
    {
        get => _initialisierungsskriptLoadingFailed;
        private set => SetProperty(ref _initialisierungsskriptLoadingFailed, value);
    }

    /// <summary>Erstellt eine Aufgabe aus einer offenen SCM-Anforderung.</summary>
    public AsyncRelayCommand<ScmRequirement> AufgabeAusAnforderungErstellenCommand { get; }

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

    /// <summary>Öffnet den Dialog zur nachträglichen Bearbeitung des Arbeitsverzeichnisses des zugewiesenen Repositories.</summary>
    public ICommand ArbeitsverzeichnisBearbeitenCommand { get; }

    /// <summary>Öffnet den Bearbeitungsmodus für den Basis-Branch des ausgewählten Repositories.</summary>
    public ICommand EditSourceBranchCommand { get; }

    /// <summary>Speichert den geänderten Basis-Branch des ausgewählten Repositories.</summary>
    public ICommand SaveSourceBranchCommand { get; }

    /// <summary>Bricht die Bearbeitung des Basis-Branches ab und verwirft Änderungen.</summary>
    public ICommand CancelSourceBranchEditCommand { get; }

    /// <summary>Lädt die Initialisierungsskript-Vorschläge des ausgewählten Repositories und öffnet den Bearbeitungsmodus.</summary>
    public ICommand LoadInitialisierungsskriptSuggestionenCommand { get; }

    /// <summary>Speichert das ausgewählte Initialisierungsskript des ausgewählten Repositories.</summary>
    public ICommand SaveInitialisierungsskriptCommand { get; }

    /// <summary>Bricht die Bearbeitung des Initialisierungsskripts ab und verwirft Änderungen.</summary>
    public ICommand CancelInitialisierungsskriptEditCommand { get; }

    /// <inheritdoc cref="ProjectDetailViewModel"/>
    public ProjectDetailViewModel(
        ProjektService projektService,
        AufgabeService aufgabeService,
        IServiceProvider serviceProvider,
        IDialogService dialogService,
        IPluginManager pluginManager,
        ILogger<ProjectDetailViewModel> logger,
        DirectoryStructureBrowserService? directoryStructureService = null)
    {
        _projektService = projektService;
        _aufgabeService = aufgabeService;
        _serviceProvider = serviceProvider;
        _dialogService = dialogService;
        _pluginManager = pluginManager;
        _logger = logger;
        _directoryStructureService = directoryStructureService;

        InitialisierungsskriptSuggestionenView = (CollectionView)CollectionViewSource.GetDefaultView(InitialisierungsskriptSuggestionen);
        InitialisierungsskriptSuggestionenView.Filter = FilterInitialisierungsskriptSuggestion;

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
        ArbeitsverzeichnisBearbeitenCommand = new AsyncRelayCommand(ArbeitsverzeichnisBearbeitenAsync, () => _selectedRepository != null);
        EditSourceBranchCommand = new AsyncRelayCommand(EditSourceBranchAsync, () => _selectedRepository != null);
        SaveSourceBranchCommand = new AsyncRelayCommand(SaveSourceBranchAsync, () => _isEditingSourceBranch);
        CancelSourceBranchEditCommand = new RelayCommand(CancelSourceBranchEdit, () => _isEditingSourceBranch);
        LoadInitialisierungsskriptSuggestionenCommand = new AsyncRelayCommand(
            ct => LoadInitialisierungsskriptSuggestionenAsync(_selectedRepository?.Id ?? Guid.Empty, ct),
            () => _selectedRepository != null);
        SaveInitialisierungsskriptCommand = new AsyncRelayCommand(SaveInitialisierungsskriptAsync, () => _isEditingInitialisierungsskript);
        CancelInitialisierungsskriptEditCommand = new RelayCommand(CancelInitialisierungsskriptEdit, () => _isEditingInitialisierungsskript);
        AufgabeAusAnforderungErstellenCommand = new AsyncRelayCommand<ScmRequirement>(AufgabeAusAnforderungErstellenAsync);
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
            AktualisiereAufgabenAnsichten();

            if (Projekt != null)
            {
                ProjektName = Projekt.Name;
                ProjektBeschreibung = Projekt.Beschreibung;
                SelectedRepository = Projekt.Repositories.FirstOrDefault();
            }

            await LadenOffeneAnforderungenAsync(ct);
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
                SelectedRepository?.Id,
                ct);

            Aufgaben.Add(aufgabe);
            AktualisiereAufgabenAnsichten();

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
                var gitRepository = await _projektService.AddRepositoryAsync(
                    _projektId,
                    scmPlugin.PluginPrefix,
                    repo.Url,
                    repo.Name,
                    vm.DefaultSourceBranchName,
                    ct);

                await _projektService.SaveRepositoryWorkingDirectoryAsync(gitRepository.Id, vm.SelectedWorkingDirectory, ct);

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

    private async Task ArbeitsverzeichnisBearbeitenAsync(CancellationToken ct)
    {
        if (_selectedRepository == null)
            return;

        var repository = _selectedRepository;

        try
        {
            var gitPlugin = _pluginManager.GetSourceCodeManagementPlugins()
                .FirstOrDefault(p => string.Equals(p.PluginPrefix, repository.PluginTyp, StringComparison.OrdinalIgnoreCase));

            var vm = _serviceProvider.GetRequiredService<ArbeitsverzeichnisBearbeitenViewModel>();
            await vm.LadenAsync(gitPlugin, repository.RepositoryUrl, repository.StartKonfiguration?.WorkingDirectoryRelativePath, ct);

            var confirmed = _dialogService.ArbeitsverzeichnisBearbeitenDialog(vm);

            if (_disposed || ct.IsCancellationRequested)
                return;

            if (confirmed)
            {
                await _projektService.SaveRepositoryWorkingDirectoryAsync(repository.Id, vm.SelectedWorkingDirectory, ct);

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
            _logger.LogError(ex, "Fehler beim Bearbeiten des Arbeitsverzeichnisses für Repository {RepositoryId}.", repository.Id);
            SetFehler(ex);
        }
    }

    private async Task EditSourceBranchAsync(CancellationToken ct)
    {
        if (_selectedRepository == null)
            return;

        var repository = _selectedRepository;
        _sourceBranchNameBeforeEdit = SelectedRepositorySourceBranchName;
        SourceBranchInputError = null;
        IsEditingSourceBranch = true;
        IsLoadingSourceBranchesForEdit = true;

        try
        {
            var gitPlugin = _pluginManager.GetSourceCodeManagementPlugins()
                .FirstOrDefault(p => string.Equals(p.PluginPrefix, repository.PluginTyp, StringComparison.OrdinalIgnoreCase));

            AvailableSourceBranchesForEdit.Clear();

            if (gitPlugin != null)
            {
                var branches = await gitPlugin.GetRemoteBranchesAsync(repository.RepositoryUrl, ct);
                foreach (var branch in branches.OrderBy(b => b, StringComparer.OrdinalIgnoreCase))
                    AvailableSourceBranchesForEdit.Add(branch);
            }

            IsEditingSourceBranchManualInput = AvailableSourceBranchesForEdit.Count == 0;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Laden der verfügbaren Branches für Repository {RepositoryId}.", repository.Id);
            IsEditingSourceBranchManualInput = true;
        }
        finally
        {
            IsLoadingSourceBranchesForEdit = false;
        }
    }

    private async Task SaveSourceBranchAsync(CancellationToken ct)
    {
        if (_selectedRepository == null)
            return;

        if (!ValidateSourceBranchInput())
            return;

        var repository = _selectedRepository;

        try
        {
            var normalized = string.IsNullOrWhiteSpace(SelectedRepositorySourceBranchName)
                ? null
                : SelectedRepositorySourceBranchName.Trim();

            var updatedRepository = await _projektService.UpdateRepositorySourceBranchAsync(repository.Id, normalized, ct);

            repository.DefaultSourceBranchName = updatedRepository.DefaultSourceBranchName;
            SelectedRepositorySourceBranchName = updatedRepository.DefaultSourceBranchName;
            SourceBranchInputError = null;
            IsEditingSourceBranch = false;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Speichern des Basis-Branches für Repository {RepositoryId}.", repository.Id);
            SourceBranchInputError = ex.Message;
        }
    }

    private void CancelSourceBranchEdit()
    {
        SelectedRepositorySourceBranchName = _sourceBranchNameBeforeEdit;
        SourceBranchInputError = null;
        IsEditingSourceBranch = false;
    }

    private bool ValidateSourceBranchInput()
    {
        var isValid = SourceBranchInputValidator.Validate(SelectedRepositorySourceBranchName, AvailableSourceBranchesForEdit, out var error);
        SourceBranchInputError = error;
        return isValid;
    }

    private async Task LoadInitialisierungsskriptSuggestionenAsync(Guid repositoryId, CancellationToken ct)
    {
        var repository = Projekt?.Repositories.FirstOrDefault(r => r.Id == repositoryId);
        if (repository == null)
            return;

        _initialisierungsskriptBeforeEdit = SelectedInitialisierungsskript;
        IsEditingInitialisierungsskript = true;
        InitialisierungsskriptLoadingFailed = null;
        InitialisierungsskriptSuggestionen.Clear();

        var gitPlugin = _pluginManager.GetSourceCodeManagementPlugins()
            .FirstOrDefault(p => string.Equals(p.PluginPrefix, repository.PluginTyp, StringComparison.OrdinalIgnoreCase));

        if (gitPlugin == null || _directoryStructureService == null || string.IsNullOrWhiteSpace(repository.RepositoryUrl))
        {
            InitialisierungsskriptLoadingFailed = true;
            return;
        }

        var result = await _directoryStructureService.GetFileLoadResultAsync(gitPlugin, repository.RepositoryUrl, ct, repository.DefaultSourceBranchName);
        ct.ThrowIfCancellationRequested();

        if (result.Status != RepositoryStructureLoadStatus.Success)
        {
            InitialisierungsskriptLoadingFailed = true;
            return;
        }

        foreach (var entry in result.Entries
            .Where(entry => IstAusfuehrbareDatei(entry.Path))
            .OrderBy(entry => entry.Path, StringComparer.OrdinalIgnoreCase))
        {
            InitialisierungsskriptSuggestionen.Add(entry.Path);
        }

        InitialisierungsskriptLoadingFailed = false;
    }

    private async Task SaveInitialisierungsskriptAsync(CancellationToken ct)
    {
        if (_selectedRepository == null)
            return;

        var repository = _selectedRepository;

        try
        {
            var normalized = string.IsNullOrWhiteSpace(SelectedInitialisierungsskript)
                ? null
                : SelectedInitialisierungsskript.Trim();

            var updatedKonfiguration = await _projektService.SaveRepositoryInitialisierungskriptAsync(repository.Id, normalized, ct);

            repository.InitialisierungKonfiguration = updatedKonfiguration;
            SelectedInitialisierungsskript = updatedKonfiguration?.InitialisierungsskriptRelativePath;
            IsEditingInitialisierungsskript = false;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Speichern des Initialisierungsskripts für Repository {RepositoryId}.", repository.Id);
            SetFehler(ex);
        }
    }

    private void CancelInitialisierungsskriptEdit()
    {
        SelectedInitialisierungsskript = _initialisierungsskriptBeforeEdit;
        IsEditingInitialisierungsskript = false;
    }

    private static bool IstAusfuehrbareDatei(string path)
    {
        var extension = Path.GetExtension(path);
        return extension is ".ps1" or ".cmd" or ".bat" or ".sh" or ".exe";
    }

    private bool FilterInitialisierungsskriptSuggestion(object item)
    {
        if (string.IsNullOrWhiteSpace(SelectedInitialisierungsskript))
            return true;

        return item is string path && path.Contains(SelectedInitialisierungsskript, StringComparison.OrdinalIgnoreCase);
    }

    private void OeffneAufgabe(Guid id)
    {
        var vm = _serviceProvider.GetRequiredService<TaskDetailViewModel>();
        vm.ZurueckAction = () => NavigateBackToProjectCallback?.Invoke();
        _aktuelleAufgabeId = id;
        vm.AufgabeListeAktualisierenCallback = ReloadAufgabenListAsync;
        NavigateToTaskViewCallback?.Invoke(vm);
        vm.AufgabeId = id;
    }

    private async Task ReloadAufgabenListAsync()
    {
        var aktualisiert = await _aufgabeService.GetByIdAsync(_aktuelleAufgabeId);
        if (aktualisiert is null)
            return;

        ReplaceOrAddAufgabe(aktualisiert);
        AktualisiereAufgabenAnsichten();
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

    private void AktualisiereAufgabenAnsichten()
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

        NichtBeendeteAufgaben.Clear();
        BeendeteAufgaben.Clear();
        foreach (var aufgabe in Aufgaben)
        {
            switch (aufgabe.Status)
            {
                case AufgabeStatus.Neu:
                case AufgabeStatus.Gestartet:
                case AufgabeStatus.Wartend:
                    NichtBeendeteAufgaben.Add(aufgabe);
                    break;
                case AufgabeStatus.Beendet:
                    BeendeteAufgaben.Add(aufgabe);
                    break;
            }
        }
    }

    private async Task LadenOffeneAnforderungenAsync(CancellationToken ct)
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
        OffeneAnforderungen.Clear();
        IssueVorschlaege.Clear();

        try
        {
            var bereitsKonvertierteNummern = Aufgaben
                .Where(a => a.IssueReferenz?.IssueNummer != null)
                .Select(a => a.IssueReferenz!.IssueNummer!.Value)
                .ToHashSet();
            var bereitsKonvertierteAlerts = Aufgaben
                .Where(a => !string.IsNullOrWhiteSpace(a.AlertReferenz?.SourceKey))
                .Select(a => a.AlertReferenz!.SourceKey)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var repositoryContext = new ScmRepositoryContext(
                repository.Id,
                repository.PluginTyp,
                string.IsNullOrWhiteSpace(repository.RepositoryName) ? repository.RepositoryUrl : repository.RepositoryName);

            var issues = await gitPlugin.GetIssuesAsync(repository.RepositoryUrl, ct);
            foreach (var issue in issues)
            {
                if (!bereitsKonvertierteNummern.Contains(issue.Nummer))
                {
                    IssueVorschlaege.Add(issue);
                    OffeneAnforderungen.Add(ScmRequirement.FromIssue(issue, repositoryContext));
                }
            }

            var pullRequests = await gitPlugin.GetOpenPullRequestsAsync(repository.RepositoryUrl, ct);
            foreach (var pullRequest in pullRequests)
            {
                var repositoryId = pullRequest.RepositoryId ?? repositoryContext.RepositoryId;
                if (!PullRequestRepositoryId.TryNormalize(pullRequest.Provider, repositoryId, out var normalizedRepositoryId))
                    continue;

                var alreadyLinked = await _aufgabeService.IsPullRequestLinkedAsync(
                    pullRequest.Provider,
                    normalizedRepositoryId,
                    pullRequest.Nummer,
                    ct);
                if (!alreadyLinked)
                {
                    OffeneAnforderungen.Add(ScmRequirement.FromPullRequest(
                        pullRequest with { RepositoryId = normalizedRepositoryId },
                        new ScmRepositoryContext(repositoryContext.GitRepositoryId, repositoryContext.PluginPrefix, normalizedRepositoryId)));
                }
            }

            if (gitPlugin is IScmAlertProvider alertProvider)
            {
                var alerts = await alertProvider.GetAlertsAsync(repository.RepositoryUrl, ct);
                foreach (var alert in alerts)
                {
                    if (!bereitsKonvertierteAlerts.Contains(alert.SourceKey))
                    {
                        OffeneAnforderungen.Add(ScmRequirement.FromAlert(alert));
                    }
                }
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

    private async Task AufgabeAusAnforderungErstellenAsync(ScmRequirement? anforderung, CancellationToken ct)
    {
        if (anforderung == null)
            return;

        if (anforderung.Kind == ScmRequirementKind.Issue)
        {
            await AufgabeAusIssueErstellenAsync(anforderung.Issue, ct);
            return;
        }

        if (anforderung.Kind == ScmRequirementKind.PullRequest)
        {
            await AufgabeAusPullRequestErstellenAsync(anforderung, ct);
            return;
        }

        await AufgabeAusAlertErstellenAsync(anforderung, ct);
    }

    private async Task AufgabeAusPullRequestErstellenAsync(ScmRequirement anforderung, CancellationToken ct)
    {
        var pullRequest = anforderung.PullRequest;
        var repositoryContext = anforderung.RepositoryContext;
        if (pullRequest == null || repositoryContext == null || _projektId == Guid.Empty)
            return;

        if (!_dialogService.BestaetigenDialog(
                $"Pull Request '{pullRequest.Titel}' als Review-Aufgabe erstellen?",
                "Pull Request konvertieren"))
            return;

        try
        {
            var aufgabe = await _aufgabeService.CreateFromPullRequestAsync(
                _projektId,
                pullRequest,
                repositoryContext,
                ct);

            var zuEntfernen = OffeneAnforderungen.FirstOrDefault(a =>
                a.Kind == ScmRequirementKind.PullRequest
                && a.PullRequest?.Provider == pullRequest.Provider
                && a.PullRequest?.Nummer == pullRequest.Nummer
                && string.Equals(a.RepositoryContext?.RepositoryId, repositoryContext.RepositoryId, StringComparison.OrdinalIgnoreCase));
            if (zuEntfernen != null)
                OffeneAnforderungen.Remove(zuEntfernen);

            Aufgaben.Add(aufgabe);
            AktualisiereAufgabenAnsichten();
            OeffneAufgabe(aufgabe.Id);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Erstellen einer Aufgabe aus Pull Request #{PullRequestNumber}.", pullRequest.Nummer);
            SetFehler(ex);
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
            var anforderung = OffeneAnforderungen.FirstOrDefault(a => a.Kind == ScmRequirementKind.Issue && a.Issue?.Nummer == issue.Nummer);
            if (anforderung != null)
                OffeneAnforderungen.Remove(anforderung);

            Aufgaben.Add(aufgabe);
            AktualisiereAufgabenAnsichten();
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

    private async Task AufgabeAusAlertErstellenAsync(ScmRequirement anforderung, CancellationToken ct)
    {
        var alert = anforderung.Alert;
        if (alert == null || _projektId == Guid.Empty || _selectedRepository == null)
            return;

        if (!_dialogService.BestaetigenDialog(
                $"Alert '{alert.Title}' als GitHub-Issue und Aufgabe erstellen?",
                "Alert konvertieren"))
            return;

        var konvertierungRegistriert = false;

        try
        {
            konvertierungRegistriert = _laufendeAlertKonvertierungen.Add(alert.SourceKey);
            if (!konvertierungRegistriert)
                return;

            var existing = await _aufgabeService.GetByAlertSourceKeyAsync(alert.SourceKey, ct);
            if (existing != null)
            {
                EntferneOffeneAnforderung(alert.SourceKey);
                ReplaceOrAddAufgabe(existing);
                AktualisiereAufgabenAnsichten();
                return;
            }

            var gitPlugin = _pluginManager.GetSourceCodeManagementPlugins()
                .FirstOrDefault(p => string.Equals(p.PluginPrefix, _selectedRepository.PluginTyp, StringComparison.OrdinalIgnoreCase));

            if (gitPlugin is not IIssueCreateProvider issueCreateProvider)
            {
                FehlerMeldung = "Das ausgewählte SCM-Plugin kann keine Issues erstellen.";
                return;
            }

            if (!await issueCreateProvider.CanCreateIssueAsync(_selectedRepository.RepositoryUrl, ct))
            {
                FehlerMeldung = "Für dieses Repository kann kein GitHub-Issue erstellt werden.";
                return;
            }

            var request = BuildIssueCreateRequest(alert);
            var createdIssueResult = await issueCreateProvider.CreateIssueAsync(_selectedRepository.RepositoryUrl, request, ct);
            if (!createdIssueResult.IsSuccess || createdIssueResult.Issue == null)
            {
                FehlerMeldung = createdIssueResult.ErrorMessage ?? "GitHub-Issue konnte nicht erstellt werden.";
                return;
            }

            var aufgabe = await _aufgabeService.CreateFromAlertAsync(
                _projektId,
                alert,
                createdIssueResult.Issue,
                _selectedRepository.Id,
                _selectedRepository.PluginTyp,
                _selectedRepository.RepositoryUrl,
                ct);

            EntferneOffeneAnforderung(alert.SourceKey);

            ReplaceOrAddAufgabe(aufgabe);
            AktualisiereAufgabenAnsichten();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Erstellen einer Aufgabe aus Alert {SourceKey}.", alert.SourceKey);
            SetFehler(ex);
        }
        finally
        {
            if (konvertierungRegistriert)
                _laufendeAlertKonvertierungen.Remove(alert.SourceKey);
        }
    }

    private void EntferneOffeneAnforderung(string sourceKey)
    {
        var zuEntfernen = OffeneAnforderungen.FirstOrDefault(a => string.Equals(a.SourceKey, sourceKey, StringComparison.OrdinalIgnoreCase));
        if (zuEntfernen != null)
            OffeneAnforderungen.Remove(zuEntfernen);
    }

    private static IssueCreateRequest BuildIssueCreateRequest(ScmAlert alert)
    {
        var title = $"Code scanning alert: {alert.Title}";
        var location = alert.FilePath is null
            ? "-"
            : alert.StartLine is null ? alert.FilePath : $"{alert.FilePath}:{alert.StartLine}";
        var body = string.Join(Environment.NewLine, new[]
        {
            "Automatisch aus einem GitHub-Code-Scanning-Alert erstellt.",
            string.Empty,
            $"Alert-Typ: {alert.AlertType}",
            $"Severity: {alert.Severity ?? "-"}",
            $"Status: {alert.State ?? "-"}",
            $"Tool: {alert.ToolName ?? "-"}",
            $"Rule: {alert.RuleName ?? alert.RuleId ?? "-"}",
            $"Betroffener Ort: {location}",
            $"Alert-URL: {alert.AlertUrl ?? "-"}",
            string.Empty,
            alert.Description ?? "-"
        });

        return new IssueCreateRequest(title, body);
    }

    private void AktualisiereKannIssuesLaden()
    {
        KannAnforderungenLaden = _selectedRepository != null
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
