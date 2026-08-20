using System.Windows.Input;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.App.ViewModels;

/// <summary>ViewModel für den Initialisierungsdialog einer Autonomen Aufgabe.</summary>
public sealed class AutonomAufgabeInitialisierungsDialogViewModel : ViewModelBase
{
    private readonly AutonomAufgabenInitialisierungsService _initialisierungsService;
    private readonly ILogger<AutonomAufgabeInitialisierungsDialogViewModel> _logger;
    private Aufgabe? _aufgabe;

    private string? _selectedProjectBranch;
    private string _initialPrompt = string.Empty;
    private PermissionsJsonOption _selectedPermissionsOption = PermissionsJsonOption.Generate;
    private int _tokenBudget = 500000;
    private bool _allowTokenExtension;
    private int _runtimeLimitMinutes = 480;
    private string _selectedPersistenceMode = nameof(Domain.Enums.PersistenzModus.Standard);
    private bool _autoGenerateSkills;
    private string? _errorMessage;
    private bool _isSubmitting;

    /// <summary>Wird ausgelöst, wenn der Dialog geschlossen werden soll. Parameter: true = erfolgreich, false = abgebrochen.</summary>
    public event EventHandler<bool>? CloseRequested;

    /// <summary>Ausgewählter oder neu vergebener Projektbranch.</summary>
    public string? SelectedProjectBranch
    {
        get => _selectedProjectBranch;
        set => SetProperty(ref _selectedProjectBranch, value);
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
    public string SelectedPersistenceMode
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

    /// <inheritdoc cref="AutonomAufgabeInitialisierungsDialogViewModel"/>
    public AutonomAufgabeInitialisierungsDialogViewModel(
        AutonomAufgabenInitialisierungsService initialisierungsService,
        ILogger<AutonomAufgabeInitialisierungsDialogViewModel> logger)
    {
        _initialisierungsService = initialisierungsService;
        _logger = logger;

        BestaetigenCommand = new AsyncRelayCommand(_ => BestaetigenAsync(), () => !IsSubmitting);
        AbbrechenCommand = new RelayCommand(Abbrechen, () => !IsSubmitting);
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

    /// <summary>Validiert die Eingaben und ruft <see cref="AutonomAufgabenInitialisierungsService"/> mit allen Formularwerten auf.</summary>
    public async Task BestaetigenAsync()
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
            var persistenzModus = Enum.TryParse<PersistenzModus>(SelectedPersistenceMode, ignoreCase: true, out var geparsterModus)
                ? geparsterModus
                : Domain.Enums.PersistenzModus.Standard;

            ErstellteKonfiguration = await _initialisierungsService.InitialisiereAsync(
                _aufgabe,
                InitialPrompt,
                SelectedProjectBranch,
                TokenBudget,
                AllowTokenExtension ? TokenBudget : null,
                RuntimeLimitMinutes,
                persistenzModus,
                AutoGenerateSkills,
                SelectedPermissionsOption);
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
