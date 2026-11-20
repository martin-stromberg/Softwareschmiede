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
    private readonly ProjectService _projektService;
    private readonly WorkItemService _workItemService;
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
    private Project? _projekt;
    private bool _isLoading;
    private string? _errorMessage;
    private CancellationTokenSource? _ladenCts;
    private string _projektName = string.Empty;
    private string? _projektBeschreibung;
    private GitRepository? _selectedRepository;
    private WorkItemFilterType _aufgabenFilter = WorkItemFilterType.All;
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
    private bool _isBackmergeAutoMergeEnabled;
    private bool _isBackmergeModusProjektbezogen;

    /// <summary>Die Projekt-ID, deren Details angezeigt werden.</summary>
    public Guid ProjectId
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
    public Project? Project
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
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    /// <summary>Liste der Aufgaben des Projekts.</summary>
    public ObservableCollection<WorkItem> WorkItems { get; } = new();

    /// <summary>Gefilterte Aufgaben (entsprechend AufgabenFilter).</summary>
    public ObservableCollection<WorkItem> GefilterteAufgaben { get; } = new();

    /// <summary>Nicht beendete Aufgaben der Projektdetailansicht.</summary>
    public ObservableCollection<WorkItem> NichtBeendeteAufgaben { get; } = new();

    /// <summary>Beendete Aufgaben der Projektdetailansicht.</summary>
    public ObservableCollection<WorkItem> BeendeteAufgaben { get; } = new();

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
                OnPropertyChanged(nameof(IsBackmergeOptionVisible));
                IsEditingInitialisierungsskript = false;
                InitialisierungsskriptSuggestionen.Clear();
                InitialisierungsskriptLoadingFailed = null;
                SelectedInitialisierungsskript = value?.InitializationConfiguration?.InitializationScriptRelativePath;
            }
        }
    }

    /// <summary>Aktueller Aufgabenfilter.</summary>
    public WorkItemFilterType AufgabenFilter
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
        set
        {
            if (SetProperty(ref _selectedRepositorySourceBranchName, value))
                OnPropertyChanged(nameof(IsBackmergeOptionVisible));
        }
    }

    /// <summary>Projektbezogene Freigabe: Backmerge-Pull-Requests dieses Projekts automatisch mergen.</summary>
    public bool IsBackmergeAutoMergeEnabled
    {
        get => _isBackmergeAutoMergeEnabled;
        set
        {
            if (SetProperty(ref _isBackmergeAutoMergeEnabled, value))
                SpeichereBackmergeAktivAsync(value).SafeFireAndForget(_logger, "ProjectDetailViewModel.SpeichereBackmergeAktivAsync");
        }
    }

    /// <summary>Gibt an, ob die projektbezogene Backmerge-Option angeboten wird: nur im globalen
    /// Modus „Projektbezogen aktivierbar“ und nur bei einem konfigurierten Basis-Branch.</summary>
    public bool IsBackmergeOptionVisible
        => _isBackmergeModusProjektbezogen
           && _selectedRepository != null
           && !string.IsNullOrWhiteSpace(SelectedRepositorySourceBranchName);

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
        ProjectService projektService,
        WorkItemService workItemService,
        IServiceProvider serviceProvider,
        IDialogService dialogService,
        IPluginManager pluginManager,
        ILogger<ProjectDetailViewModel> logger,
        DirectoryStructureBrowserService? directoryStructureService = null)
    {
        _projektService = projektService;
        _workItemService = workItemService;
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
            Project = await _projektService.GetDetailAsync(_projektId, ct);
            var workItems = await _workItemService.GetByProjektAsync(_projektId, ct);
            WorkItems.Clear();
            foreach (var workItem in workItems)
                WorkItems.Add(workItem);
            AktualisiereAufgabenAnsichten();

            if (Project != null)
            {
                ProjektName = Project.Name;
                ProjektBeschreibung = Project.Description;
                SelectedRepository = Project.Repositories.FirstOrDefault();
            }

            // Die Backmerge-Option ist nicht kritisch: Steht der Einstellungsdienst nicht zur
            // Verfügung, bleibt sie schlicht verborgen statt das Laden des Projekts zu verhindern.
            try
            {
                using var einstellungenScope = _serviceProvider.CreateScope();
                var settings = einstellungenScope.ServiceProvider.GetRequiredService<AppSettingService>();
                _isBackmergeModusProjektbezogen = await settings.GetScmBackmergeModeAsync(ct) == ScmBackmergeMode.ProjectScoped;
                _isBackmergeAutoMergeEnabled = await settings.GetBackmergeProjectEnabledAsync(_projektId, ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Backmerge-Einstellungen für Projekt {ProjektId} konnten nicht geladen werden.", _projektId);
            }
            OnPropertyChanged(nameof(IsBackmergeAutoMergeEnabled));
            OnPropertyChanged(nameof(IsBackmergeOptionVisible));

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
            var workItem = await _workItemService.CreateAsync(
                _projektId,
                "Neue Aufgabe",
                string.Empty,
                SelectedRepository?.Id,
                ct);

            WorkItems.Add(workItem);
            AktualisiereAufgabenAnsichten();

            OeffneAufgabe(workItem.Id);
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
                .FirstOrDefault(p => string.Equals(p.PluginPrefix, repository.PluginType, StringComparison.OrdinalIgnoreCase));

            var vm = _serviceProvider.GetRequiredService<ArbeitsverzeichnisBearbeitenViewModel>();
            await vm.LadenAsync(gitPlugin, repository.RepositoryUrl, repository.StartConfiguration?.WorkingDirectoryRelativePath, ct);

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
                .FirstOrDefault(p => string.Equals(p.PluginPrefix, repository.PluginType, StringComparison.OrdinalIgnoreCase));

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

    /// <summary>Persistiert die projektbezogene Backmerge-Freigabe sofort beim Umschalten der Option.</summary>
    private async Task SpeichereBackmergeAktivAsync(bool active)
    {
        if (_projektId == Guid.Empty)
            return;

        using var scope = _serviceProvider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppSettingService>()
            .SetBackmergeProjectEnabledAsync(_projektId, active, CancellationToken.None);
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
        var repository = Project?.Repositories.FirstOrDefault(r => r.Id == repositoryId);
        if (repository == null)
            return;

        _initialisierungsskriptBeforeEdit = SelectedInitialisierungsskript;
        IsEditingInitialisierungsskript = true;
        InitialisierungsskriptLoadingFailed = null;
        InitialisierungsskriptSuggestionen.Clear();

        var gitPlugin = _pluginManager.GetSourceCodeManagementPlugins()
            .FirstOrDefault(p => string.Equals(p.PluginPrefix, repository.PluginType, StringComparison.OrdinalIgnoreCase));

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

            var updatedKonfiguration = await _projektService.SaveRepositoryInitializationScriptAsync(repository.Id, normalized, ct);

            repository.InitializationConfiguration = updatedKonfiguration;
            SelectedInitialisierungsskript = updatedKonfiguration?.InitializationScriptRelativePath;
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
        vm.WorkItemId = id;
    }

    private async Task ReloadAufgabenListAsync()
    {
        var aktualisiert = await _workItemService.GetByIdAsync(_aktuelleAufgabeId);
        if (aktualisiert is null)
            return;

        ReplaceOrAddAufgabe(aktualisiert);
        AktualisiereAufgabenAnsichten();
    }

    private void ReplaceOrAddAufgabe(WorkItem workItem)
    {
        for (var i = 0; i < WorkItems.Count; i++)
        {
            if (WorkItems[i].Id == workItem.Id)
            {
                WorkItems[i] = workItem;
                return;
            }
        }
        WorkItems.Add(workItem);
    }

    private void AktualisiereAufgabenAnsichten()
    {
        GefilterteAufgaben.Clear();
        var source = _aufgabenFilter switch
        {
            WorkItemFilterType.Active => WorkItems.Where(a => a.Status != WorkItemStatus.Archived),
            WorkItemFilterType.Archived => WorkItems.Where(a => a.Status == WorkItemStatus.Archived),
            _ => WorkItems
        };
        foreach (var workItem in source)
            GefilterteAufgaben.Add(workItem);

        NichtBeendeteAufgaben.Clear();
        BeendeteAufgaben.Clear();
        foreach (var workItem in WorkItems)
        {
            switch (workItem.Status)
            {
                case WorkItemStatus.New:
                case WorkItemStatus.Started:
                case WorkItemStatus.Waiting:
                    NichtBeendeteAufgaben.Add(workItem);
                    break;
                case WorkItemStatus.Completed:
                    BeendeteAufgaben.Add(workItem);
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
            .FirstOrDefault(p => string.Equals(p.PluginPrefix, repository.PluginType, StringComparison.OrdinalIgnoreCase));
        if (gitPlugin == null)
            return;

        IsLoadingIssues = true;
        OffeneAnforderungen.Clear();
        IssueVorschlaege.Clear();

        try
        {
            var bereitsKonvertierteNummern = WorkItems
                .Where(a => a.IssueReference?.IssueNumber != null)
                .Select(a => a.IssueReference!.IssueNumber!.Value)
                .ToHashSet();
            var bereitsKonvertierteAlerts = WorkItems
                .Where(a => !string.IsNullOrWhiteSpace(a.AlertReference?.SourceKey))
                .Select(a => a.AlertReference!.SourceKey)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var repositoryContext = new ScmRepositoryContext(
                repository.Id,
                repository.PluginType,
                string.IsNullOrWhiteSpace(repository.RepositoryName) ? repository.RepositoryUrl : repository.RepositoryName);

            var issues = await gitPlugin.GetIssuesAsync(repository.RepositoryUrl, ct);
            foreach (var issue in issues)
            {
                if (!bereitsKonvertierteNummern.Contains(issue.Number))
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

                var alreadyLinked = await _workItemService.IsPullRequestLinkedAsync(
                    pullRequest.Provider,
                    normalizedRepositoryId,
                    pullRequest.Number,
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

    private async Task AufgabeAusAnforderungErstellenAsync(ScmRequirement? requirement, CancellationToken ct)
    {
        if (requirement == null)
            return;

        if (requirement.Kind == ScmRequirementKind.Issue)
        {
            await AufgabeAusIssueErstellenAsync(requirement.Issue, ct);
            return;
        }

        if (requirement.Kind == ScmRequirementKind.PullRequest)
        {
            await AufgabeAusPullRequestErstellenAsync(requirement, ct);
            return;
        }

        await AufgabeAusAlertErstellenAsync(requirement, ct);
    }

    private async Task AufgabeAusPullRequestErstellenAsync(ScmRequirement requirement, CancellationToken ct)
    {
        var pullRequest = requirement.PullRequest;
        var repositoryContext = requirement.RepositoryContext;
        if (pullRequest == null || repositoryContext == null || _projektId == Guid.Empty)
            return;

        if (!_dialogService.BestaetigenDialog(
                $"Pull Request '{pullRequest.Title}' als Review-Aufgabe erstellen?",
                "Pull Request konvertieren"))
            return;

        try
        {
            var workItem = await _workItemService.CreateFromPullRequestAsync(
                _projektId,
                pullRequest,
                repositoryContext,
                ct);

            var zuEntfernen = OffeneAnforderungen.FirstOrDefault(a =>
                a.Kind == ScmRequirementKind.PullRequest
                && a.PullRequest?.Provider == pullRequest.Provider
                && a.PullRequest?.Number == pullRequest.Number
                && string.Equals(a.RepositoryContext?.RepositoryId, repositoryContext.RepositoryId, StringComparison.OrdinalIgnoreCase));
            if (zuEntfernen != null)
                OffeneAnforderungen.Remove(zuEntfernen);

            WorkItems.Add(workItem);
            AktualisiereAufgabenAnsichten();
            OeffneAufgabe(workItem.Id);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Erstellen einer Aufgabe aus Pull Request #{PullRequestNumber}.", pullRequest.Number);
            SetFehler(ex);
        }
    }

    private async Task AufgabeAusIssueErstellenAsync(Issue? issue, CancellationToken ct)
    {
        if (issue == null || _projektId == Guid.Empty)
            return;

        if (!_dialogService.BestaetigenDialog(
                $"Issue '{issue.Title}' als Aufgabe erstellen?",
                "Issue konvertieren"))
            return;

        try
        {
            var workItem = await _workItemService.CreateFromIssueAsync(
                _projektId,
                issue,
                _selectedRepository?.Id,
                ct);

            var zuEntfernen = IssueVorschlaege.FirstOrDefault(i => i.Number == issue.Number);
            if (zuEntfernen != null)
                IssueVorschlaege.Remove(zuEntfernen);
            var requirement = OffeneAnforderungen.FirstOrDefault(a => a.Kind == ScmRequirementKind.Issue && a.Issue?.Number == issue.Number);
            if (requirement != null)
                OffeneAnforderungen.Remove(requirement);

            WorkItems.Add(workItem);
            AktualisiereAufgabenAnsichten();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Erstellen einer Aufgabe aus Issue #{IssueNummer}.", issue.Number);
            SetFehler(ex);
        }
    }

    private async Task AufgabeAusAlertErstellenAsync(ScmRequirement requirement, CancellationToken ct)
    {
        var alert = requirement.Alert;
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

            var existing = await _workItemService.GetByAlertSourceKeyAsync(alert.SourceKey, ct);
            if (existing != null)
            {
                EntferneOffeneAnforderung(alert.SourceKey);
                ReplaceOrAddAufgabe(existing);
                AktualisiereAufgabenAnsichten();
                return;
            }

            var gitPlugin = _pluginManager.GetSourceCodeManagementPlugins()
                .FirstOrDefault(p => string.Equals(p.PluginPrefix, _selectedRepository.PluginType, StringComparison.OrdinalIgnoreCase));

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

            var workItem = await _workItemService.CreateFromAlertAsync(
                _projektId,
                alert,
                createdIssueResult.Issue,
                _selectedRepository.Id,
                _selectedRepository.PluginType,
                _selectedRepository.RepositoryUrl,
                ct);

            EntferneOffeneAnforderung(alert.SourceKey);

            ReplaceOrAddAufgabe(workItem);
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
                .Any(p => string.Equals(p.PluginPrefix, _selectedRepository.PluginType, StringComparison.OrdinalIgnoreCase));
    }

    private void SetFehler(Exception ex) => SetFehler(ref _errorMessage, nameof(FehlerMeldung), ex);

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
