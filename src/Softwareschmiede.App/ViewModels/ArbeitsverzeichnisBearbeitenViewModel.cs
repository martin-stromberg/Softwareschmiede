using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Interfaces;

namespace Softwareschmiede.App.ViewModels;

/// <summary>ViewModel für den Dialog zur nachträglichen Bearbeitung des Arbeitsverzeichnisses eines bereits zugewiesenen Repositories.</summary>
public sealed class ArbeitsverzeichnisBearbeitenViewModel : ViewModelBase
{
    private readonly ILogger<ArbeitsverzeichnisBearbeitenViewModel> _logger;
    private readonly DirectoryStructureBrowserService? _directoryStructureService;
    private ObservableCollection<string> _availableWorkingDirectories = new();
    private string? _selectedWorkingDirectory;
    private bool _isLoadingDirectoryStructure;
    private bool _isWorkingDirectoryManualInput;
    private string? _workingDirectoryInputText;
    private string? _workingDirectoryInputError;

    /// <summary>Wird ausgelöst wenn der Dialog geschlossen werden soll. Parameter: true = bestätigt, false = abgebrochen.</summary>
    public event EventHandler<bool>? CloseRequested;

    /// <summary>Verfügbare Arbeitsverzeichnisse des Repositories.</summary>
    public ObservableCollection<string> AvailableWorkingDirectories
    {
        get => _availableWorkingDirectories;
        private set => SetProperty(ref _availableWorkingDirectories, value);
    }

    /// <summary>Vom Benutzer ausgewähltes Arbeitsverzeichnis (relativer Pfad, <c>"."</c> = Repository-Root).</summary>
    public string? SelectedWorkingDirectory
    {
        get => _selectedWorkingDirectory;
        set => SetProperty(ref _selectedWorkingDirectory, value);
    }

    /// <summary>Gibt an, ob das Arbeitsverzeichnis manuell eingegeben wird.</summary>
    public bool IsWorkingDirectoryManualInput
    {
        get => _isWorkingDirectoryManualInput;
        private set => SetProperty(ref _isWorkingDirectoryManualInput, value);
    }

    /// <summary>Manuell eingegebener relativer Arbeitsverzeichnis-Pfad.</summary>
    public string? WorkingDirectoryInputText
    {
        get => _workingDirectoryInputText;
        set
        {
            if (SetProperty(ref _workingDirectoryInputText, value))
            {
                ValidateManualWorkingDirectoryInput();
                RelayCommand.Refresh();
            }
        }
    }

    /// <summary>Validierungsfehler der manuellen Arbeitsverzeichnis-Eingabe.</summary>
    public string? WorkingDirectoryInputError
    {
        get => _workingDirectoryInputError;
        private set => SetProperty(ref _workingDirectoryInputError, value);
    }

    /// <summary>Gibt an, ob die Verzeichnisstruktur gerade geladen wird.</summary>
    public bool IsLoadingDirectoryStructure
    {
        get => _isLoadingDirectoryStructure;
        private set => SetProperty(ref _isLoadingDirectoryStructure, value);
    }

    /// <summary>Bestätigt die Auswahl und schließt den Dialog.</summary>
    public ICommand BestaetigenCommand { get; }

    /// <summary>Bricht die Bearbeitung ab und schließt den Dialog.</summary>
    public ICommand AbbrechenCommand { get; }

    /// <summary>Der laufende Lade-Task; nur für Tests.</summary>
    internal Task? CurrentLoadDirectoryStructureTask { get; private set; }

    /// <inheritdoc cref="ArbeitsverzeichnisBearbeitenViewModel"/>
    public ArbeitsverzeichnisBearbeitenViewModel(
        ILogger<ArbeitsverzeichnisBearbeitenViewModel> logger,
        DirectoryStructureBrowserService? directoryStructureService = null)
    {
        _logger = logger;
        _directoryStructureService = directoryStructureService;

        BestaetigenCommand = new RelayCommand(Confirm, CanConfirm);
        AbbrechenCommand = new RelayCommand(() => CloseRequested?.Invoke(this, false));
    }

    /// <summary>Initialisiert den Dialog für das übergebene Repository und lädt dessen Verzeichnisstruktur.</summary>
    /// <param name="gitPlugin">Das Git-Plugin des zugewiesenen Repositories, oder <c>null</c> wenn kein passendes Plugin gefunden wurde.</param>
    /// <param name="repositoryUrl">URL bzw. Pfad des Repositories.</param>
    /// <param name="currentWorkingDirectory">Aktuell konfiguriertes relatives Arbeitsverzeichnis, oder <c>null</c> für das Repository-Root.</param>
    /// <param name="ct">Cancellation Token.</param>
    public async Task LadenAsync(IGitPlugin? gitPlugin, string repositoryUrl, string? currentWorkingDirectory, CancellationToken ct = default)
    {
        var loadTask = LoadDirectoryStructureAsync(gitPlugin, repositoryUrl, currentWorkingDirectory, ct);
        CurrentLoadDirectoryStructureTask = loadTask;
        await loadTask;
    }

    private async Task LoadDirectoryStructureAsync(IGitPlugin? gitPlugin, string repositoryUrl, string? currentWorkingDirectory, CancellationToken ct)
    {
        WorkingDirectoryLoadResult? loadResult = null;

        if (_directoryStructureService != null && gitPlugin != null)
        {
            loadResult = await DirectoryStructureLoadHelper.LoadWithLoadingStateAsync(
                _directoryStructureService,
                gitPlugin,
                repositoryUrl,
                _logger,
                isLoading => IsLoadingDirectoryStructure = isLoading,
                ct);

            if (loadResult is null)
            {
                // Erwarteter Abbruch: anders als bei RepositoryAssignViewModel gibt es hier keinen
                // Folgeaufruf, der den Lade-Status zurücksetzt (LadenAsync wird pro Dialogaufruf nur
                // einmal aufgerufen). Ohne dieses explizite Zurücksetzen bliebe der Dialog dauerhaft im
                // "Wird geladen…"-Zustand hängen. Die bisherige Auswahl/Liste bleibt unverändert, statt
                // sie durch den Root-Fallback zu überschreiben.
                IsLoadingDirectoryStructure = false;
                return;
            }
        }

        if (loadResult is null || loadResult.RequiresManualInput)
        {
            AvailableWorkingDirectories.Clear();
            IsWorkingDirectoryManualInput = true;
            WorkingDirectoryInputText = !string.IsNullOrWhiteSpace(currentWorkingDirectory)
                ? currentWorkingDirectory
                : ".";
            SelectedWorkingDirectory = WorkingDirectoryInputText;
            ValidateManualWorkingDirectoryInput();
            RelayCommand.Refresh();
            return;
        }

        IsWorkingDirectoryManualInput = false;
        WorkingDirectoryInputText = null;
        WorkingDirectoryInputError = null;
        AvailableWorkingDirectories.Clear();
        foreach (var dir in loadResult.WorkingDirectories)
            AvailableWorkingDirectories.Add(dir);

        if (!string.IsNullOrWhiteSpace(currentWorkingDirectory)
            && currentWorkingDirectory != "."
            && !AvailableWorkingDirectories.Contains(currentWorkingDirectory))
        {
            AvailableWorkingDirectories.Add(currentWorkingDirectory);
        }

        SelectedWorkingDirectory = !string.IsNullOrWhiteSpace(currentWorkingDirectory) ? currentWorkingDirectory : ".";
    }

    private bool CanConfirm()
        => !IsWorkingDirectoryManualInput || ValidateManualWorkingDirectoryInput();

    private void Confirm()
    {
        if (IsWorkingDirectoryManualInput)
        {
            if (!WorkingDirectoryInputValidator.TryNormalize(WorkingDirectoryInputText, out var normalized, out var error))
            {
                WorkingDirectoryInputError = error;
                RelayCommand.Refresh();
                return;
            }

            WorkingDirectoryInputText = normalized;
            SelectedWorkingDirectory = normalized;
        }

        CloseRequested?.Invoke(this, true);
    }

    private bool ValidateManualWorkingDirectoryInput()
    {
        if (!IsWorkingDirectoryManualInput)
        {
            WorkingDirectoryInputError = null;
            return true;
        }

        var isValid = WorkingDirectoryInputValidator.TryNormalize(WorkingDirectoryInputText, out _, out var error);
        WorkingDirectoryInputError = error;
        return isValid;
    }
}
