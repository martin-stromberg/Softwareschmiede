using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.App.ViewModels;

/// <summary>ViewModel für den Initialisierungsdialog einer Autonomen Aufgabe.</summary>
public sealed class AutonomAufgabeInitialisierungsDialogViewModel : ViewModelBase
{
    private readonly AutonomAufgabenInitialisierungsService _initialisierungsService;
    private readonly AutonomAufgabenOptions _options;
    private readonly ILogger<AutonomAufgabeInitialisierungsDialogViewModel> _logger;
    private readonly IPluginManager _pluginManager;
    private readonly PromptVorlagenService _promptVorlagenService;
    private readonly PromptVorlagenPlatzhalterService _promptVorlagenPlatzhalterService;
    private Aufgabe? _aufgabe;

    private string? _selectedProjectBranch;
    private string _initialPrompt = string.Empty;
    private PermissionsJsonOption _selectedPermissionsOption = PermissionsJsonOption.Generate;
    private int _tokenBudget;
    private bool _allowTokenExtension;
    private int _runtimeLimitMinutes;
    private PersistenzModus _selectedPersistenceMode = PersistenzModus.Standard;
    private bool _autoGenerateSkills;
    private string? _errorMessage;
    private bool _isSubmitting;
    private ObservableCollection<string> _availableProjectBranches = new();
    private bool _isLoadingProjectBranches;
    private bool _isProjectBranchManualInput = true;
    private bool _isCreatingBranch;
    private string _newBranchName = string.Empty;
    private string? _newBranchError;
    private PromptVorlage? _selectedInitialPromptVorlage;

    /// <summary>Wird ausgelöst, wenn der Dialog geschlossen werden soll. Parameter: true = erfolgreich, false = abgebrochen.</summary>
    public event EventHandler<bool>? CloseRequested;

    /// <summary>Ausgewählter oder neu vergebener Projektbranch.</summary>
    public string? SelectedProjectBranch
    {
        get => _selectedProjectBranch;
        set => SetProperty(ref _selectedProjectBranch, value);
    }

    /// <summary>Verfügbare Remote-Branches des mit der Aufgabe verknüpften Repositories.</summary>
    public ObservableCollection<string> AvailableProjectBranches
    {
        get => _availableProjectBranches;
        private set => SetProperty(ref _availableProjectBranches, value);
    }

    /// <summary>Gibt an, ob die verfügbaren Projektbranches gerade geladen werden.</summary>
    public bool IsLoadingProjectBranches
    {
        get => _isLoadingProjectBranches;
        private set => SetProperty(ref _isLoadingProjectBranches, value);
    }

    /// <summary>Gibt an, ob der Projektbranch manuell eingegeben wird (keine Vorschlagsliste verfügbar).</summary>
    public bool IsProjectBranchManualInput
    {
        get => _isProjectBranchManualInput;
        private set => SetProperty(ref _isProjectBranchManualInput, value);
    }

    /// <summary>Gibt an, ob gerade die Eingabe für einen neu anzulegenden Branch angezeigt wird.</summary>
    public bool IsCreatingBranch
    {
        get => _isCreatingBranch;
        private set => SetProperty(ref _isCreatingBranch, value);
    }

    /// <summary>Name des über den "+"-Button neu anzulegenden Branches.</summary>
    public string NewBranchName
    {
        get => _newBranchName;
        set => SetProperty(ref _newBranchName, value);
    }

    /// <summary>Fehlermeldung der Branch-Neuanlage.</summary>
    public string? NewBranchError
    {
        get => _newBranchError;
        private set => SetProperty(ref _newBranchError, value);
    }

    /// <summary>Verfügbare Promptvorlagen für den Initialprompt (aus <see cref="PromptVorlagenService"/>).</summary>
    public ObservableCollection<PromptVorlage> InitialPromptVorlagen { get; } = new();

    /// <summary>Ausgewählte Promptvorlage. Beim Setzen wird <see cref="InitialPrompt"/> mit dem aufgelösten Vorlagentext befüllt.</summary>
    public PromptVorlage? SelectedInitialPromptVorlage
    {
        get => _selectedInitialPromptVorlage;
        set
        {
            if (!SetProperty(ref _selectedInitialPromptVorlage, value) || value is null)
                return;

            InitialPrompt = _promptVorlagenPlatzhalterService.Resolve(value.Prompttext, _aufgabe);
        }
    }

    /// <summary>Initialprompt für den Projektleiter.</summary>
    public string InitialPrompt
    {
        get => _initialPrompt;
        set => SetProperty(ref _initialPrompt, value);
    }

    /// <summary>Ausgewählte Quelle der permissions.json.</summary>
    public PermissionsJsonOption SelectedPermissionsOption
    {
        get => _selectedPermissionsOption;
        set => SetProperty(ref _selectedPermissionsOption, value);
    }

    /// <summary>Token-Budget für die Gesamtaufgabe.</summary>
    public int TokenBudget
    {
        get => _tokenBudget;
        set => SetProperty(ref _tokenBudget, value);
    }

    /// <summary>Gibt an, ob der Anwender das Token-Budget später erweitern darf.</summary>
    public bool AllowTokenExtension
    {
        get => _allowTokenExtension;
        set => SetProperty(ref _allowTokenExtension, value);
    }

    /// <summary>Laufzeitbegrenzung (Nettozeit) in Minuten.</summary>
    public int RuntimeLimitMinutes
    {
        get => _runtimeLimitMinutes;
        set => SetProperty(ref _runtimeLimitMinutes, value);
    }

    /// <summary>Ausgewählter Persistenz-Modus (Standard, SessionReset).</summary>
    public PersistenzModus SelectedPersistenceMode
    {
        get => _selectedPersistenceMode;
        set => SetProperty(ref _selectedPersistenceMode, value);
    }

    /// <summary>Gibt an, ob Skills automatisch aus Anforderungen generiert werden sollen.</summary>
    public bool AutoGenerateSkills
    {
        get => _autoGenerateSkills;
        set => SetProperty(ref _autoGenerateSkills, value);
    }

    /// <summary>Fehlermeldung im Dialog.</summary>
    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    /// <summary>Gibt an, ob die Initialisierung gerade läuft.</summary>
    public bool IsSubmitting
    {
        get => _isSubmitting;
        private set => SetProperty(ref _isSubmitting, value);
    }

    /// <summary>Die erfolgreich erstellte Konfiguration der Autonomen Aufgabe.</summary>
    public AutonomAufgabeKonfiguration? ErstellteKonfiguration { get; private set; }

    /// <summary>Bestätigt und erstellt die Autonome Aufgabe.</summary>
    public ICommand BestaetigenCommand { get; }

    /// <summary>Bricht den Dialog ab.</summary>
    public ICommand AbbrechenCommand { get; }

    /// <summary>Zeigt die Eingabe für einen neu anzulegenden Branch an.</summary>
    public ICommand ShowCreateBranchCommand { get; }

    /// <summary>Legt den in <see cref="NewBranchName"/> angegebenen Branch im Repository der Aufgabe an.</summary>
    public ICommand CreateBranchCommand { get; }

    /// <summary>Bricht die Neuanlage eines Branches ab.</summary>
    public ICommand CancelCreateBranchCommand { get; }

    /// <inheritdoc cref="AutonomAufgabeInitialisierungsDialogViewModel"/>
    public AutonomAufgabeInitialisierungsDialogViewModel(
        AutonomAufgabenInitialisierungsService initialisierungsService,
        IOptions<AutonomAufgabenOptions> options,
        ILogger<AutonomAufgabeInitialisierungsDialogViewModel> logger,
        IPluginManager pluginManager,
        PromptVorlagenService promptVorlagenService,
        PromptVorlagenPlatzhalterService promptVorlagenPlatzhalterService)
    {
        _initialisierungsService = initialisierungsService;
        _options = options.Value;
        _logger = logger;
        _pluginManager = pluginManager;
        _promptVorlagenService = promptVorlagenService;
        _promptVorlagenPlatzhalterService = promptVorlagenPlatzhalterService;

        _tokenBudget = _options.DefaultTokenBudget;
        _runtimeLimitMinutes = _options.DefaultRuntimeLimitMinutes;
        _autoGenerateSkills = _options.SkillAutogenerationEnabled;

        BestaetigenCommand = new AsyncRelayCommand(BestaetigenAsync, () => !IsSubmitting);
        AbbrechenCommand = new RelayCommand(Abbrechen, () => !IsSubmitting);
        ShowCreateBranchCommand = new RelayCommand(ZeigeBranchAnlegen, () => !IsSubmitting);
        CreateBranchCommand = new AsyncRelayCommand(NeuenBranchAnlegenAsync, () => !IsSubmitting && !string.IsNullOrWhiteSpace(NewBranchName));
        CancelCreateBranchCommand = new RelayCommand(AbbrechenBranchAnlegen);
    }

    /// <summary>Initialisiert den Dialog mit der Zielaufgabe.</summary>
    public void Initialize(Aufgabe aufgabe)
    {
        ArgumentNullException.ThrowIfNull(aufgabe);

        _aufgabe = aufgabe;
        SelectedProjectBranch = aufgabe.BranchName;
        ErrorMessage = null;
        ErstellteKonfiguration = null;
    }

    /// <summary>Lädt die verfügbaren Remote-Branches des mit der Aufgabe verknüpften Repositories sowie die verfügbaren Promptvorlagen. Sollte nach <see cref="Initialize"/> und vor Anzeige des Dialogs aufgerufen werden.</summary>
    public async Task LadeAsync(CancellationToken ct = default)
    {
        await LadeProjektBranchesAsync(ct);
        await LadePromptVorlagenAsync(ct);
    }

    private async Task LadeProjektBranchesAsync(CancellationToken ct)
    {
        var repositoryUrl = _aufgabe?.GitRepository?.RepositoryUrl;
        var gitPlugin = ResolveGitPlugin();
        if (gitPlugin is null || string.IsNullOrWhiteSpace(repositoryUrl))
        {
            IsProjectBranchManualInput = true;
            return;
        }

        IsLoadingProjectBranches = true;
        try
        {
            var branches = await gitPlugin.GetRemoteBranchesAsync(repositoryUrl, ct);
            ct.ThrowIfCancellationRequested();

            AvailableProjectBranches.Clear();
            foreach (var branch in branches.OrderBy(b => b, StringComparer.OrdinalIgnoreCase))
                AvailableProjectBranches.Add(branch);
            IsProjectBranchManualInput = AvailableProjectBranches.Count == 0;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Abgebrochen - kein Fehler
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Verfügbare Projektbranches konnten nicht geladen werden.");
            AvailableProjectBranches.Clear();
            IsProjectBranchManualInput = true;
        }
        finally
        {
            if (!ct.IsCancellationRequested)
                IsLoadingProjectBranches = false;
        }
    }

    private async Task LadePromptVorlagenAsync(CancellationToken ct)
    {
        try
        {
            var vorlagen = await _promptVorlagenService.GetAllAsync(ct);
            InitialPromptVorlagen.Clear();
            foreach (var vorlage in vorlagen)
                InitialPromptVorlagen.Add(vorlage);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Abgebrochen - kein Fehler
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Promptvorlagen konnten nicht geladen werden.");
        }
    }

    private IGitPlugin? ResolveGitPlugin()
    {
        var gitPlugins = _pluginManager.GetSourceCodeManagementPlugins();
        if (gitPlugins.Count == 0)
            return null;

        var pluginTyp = _aufgabe?.GitRepository?.PluginTyp;
        if (!string.IsNullOrWhiteSpace(pluginTyp))
        {
            var passendesPlugin = gitPlugins.FirstOrDefault(p => string.Equals(p.PluginPrefix, pluginTyp, StringComparison.OrdinalIgnoreCase));
            if (passendesPlugin is not null)
                return passendesPlugin;
        }

        return gitPlugins.FirstOrDefault();
    }

    private void ZeigeBranchAnlegen()
    {
        NewBranchName = string.Empty;
        NewBranchError = null;
        IsCreatingBranch = true;
    }

    private void AbbrechenBranchAnlegen()
    {
        IsCreatingBranch = false;
        NewBranchName = string.Empty;
        NewBranchError = null;
    }

    private async Task NeuenBranchAnlegenAsync(CancellationToken ct)
    {
        NewBranchError = null;

        if (_aufgabe is null || string.IsNullOrWhiteSpace(_aufgabe.LokalerKlonPfad))
        {
            NewBranchError = "Kein lokaler Klon der Aufgabe vorhanden; Branch kann nicht angelegt werden.";
            return;
        }

        var gitPlugin = ResolveGitPlugin();
        if (gitPlugin is null)
        {
            NewBranchError = "Kein Git-Plugin für das Repository der Aufgabe verfügbar.";
            return;
        }

        try
        {
            await gitPlugin.CreateBranchAsync(_aufgabe.LokalerKlonPfad, NewBranchName, SelectedProjectBranch, ct);

            if (!AvailableProjectBranches.Contains(NewBranchName, StringComparer.OrdinalIgnoreCase))
                AvailableProjectBranches.Add(NewBranchName);

            SelectedProjectBranch = NewBranchName;
            IsProjectBranchManualInput = false;
            IsCreatingBranch = false;
            NewBranchName = string.Empty;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Branch '{BranchName}' konnte nicht angelegt werden.", NewBranchName);
            NewBranchError = $"Branch konnte nicht angelegt werden: {ex.Message}";
        }
    }

    /// <summary>Validiert die Eingaben und ruft <see cref="AutonomAufgabenInitialisierungsService"/> mit allen Formularwerten auf.</summary>
    public async Task BestaetigenAsync(CancellationToken ct = default)
    {
        if (_aufgabe is null)
        {
            ErrorMessage = "Keine Aufgabe zugeordnet.";
            return;
        }

        var validierungsFehler = ValidiereEingaben();
        if (validierungsFehler is not null)
        {
            ErrorMessage = validierungsFehler;
            return;
        }

        IsSubmitting = true;
        ErrorMessage = null;
        try
        {
            var anfrage = new AutonomAufgabeInitialisierungsAnfrage(
                ProjektBranchName: SelectedProjectBranch ?? _aufgabe.BranchName ?? $"autonom-{_aufgabe.Id}",
                InitialPrompt: InitialPrompt,
                ArbeitsverzeichnisPfad: Path.Combine(_options.WorkingDirectoryBase, _aufgabe.Id.ToString()),
                TokenBudget: TokenBudget,
                TokenBudgetErweitert: AllowTokenExtension ? TokenBudget : null,
                LaufzeitLimitMinuten: RuntimeLimitMinutes,
                PersistenzModus: SelectedPersistenceMode,
                SkillAutogeneration: AutoGenerateSkills,
                PermissionsQuelle: SelectedPermissionsOption);

            ErstellteKonfiguration = await _initialisierungsService.InitialisiereAsync(_aufgabe, anfrage, ct);
            CloseRequested?.Invoke(this, true);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Autonome Aufgabe konnte nicht erstellt werden.");
            ErrorMessage = $"Autonome Aufgabe konnte nicht erstellt werden: {ex.Message}";
        }
        finally
        {
            IsSubmitting = false;
        }
    }

    /// <summary>Schließt den Dialog ohne Erstellung der Autonomen Aufgabe.</summary>
    public void Abbrechen() => CloseRequested?.Invoke(this, false);

    private string? ValidiereEingaben()
    {
        if (string.IsNullOrWhiteSpace(InitialPrompt) || InitialPrompt.Trim().Length < 10)
        {
            return "Initialprompt darf nicht leer sein und muss mindestens 10 Zeichen enthalten.";
        }

        if (TokenBudget <= 0 || TokenBudget > 5_000_000)
        {
            return "Token-Budget muss größer als 0 und maximal 5.000.000 sein.";
        }

        if (RuntimeLimitMinutes < 60 || RuntimeLimitMinutes > 1440)
        {
            return "Laufzeitbegrenzung muss zwischen 60 und 1440 Minuten (24h) liegen.";
        }

        return null;
    }
}
