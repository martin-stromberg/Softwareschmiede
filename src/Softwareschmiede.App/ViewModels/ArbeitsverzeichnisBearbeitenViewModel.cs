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

        BestaetigenCommand = new RelayCommand(() => CloseRequested?.Invoke(this, true));
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
        List<string>? directories = null;

        if (_directoryStructureService != null && gitPlugin != null)
        {
            directories = await DirectoryStructureLoadHelper.LoadWithLoadingStateAsync(
                _directoryStructureService,
                gitPlugin,
                repositoryUrl,
                _logger,
                isLoading => IsLoadingDirectoryStructure = isLoading,
                ct);

            if (directories is null)
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

        // Kein Service/Plugin vorhanden (fehlender Kontext, kein Abbruch): auf den Root-Eintrag zurückfallen.
        directories ??= new List<string> { "." };

        AvailableWorkingDirectories.Clear();
        foreach (var dir in directories)
            AvailableWorkingDirectories.Add(dir);

        if (!string.IsNullOrWhiteSpace(currentWorkingDirectory)
            && currentWorkingDirectory != "."
            && !AvailableWorkingDirectories.Contains(currentWorkingDirectory))
        {
            AvailableWorkingDirectories.Add(currentWorkingDirectory);
        }

        SelectedWorkingDirectory = !string.IsNullOrWhiteSpace(currentWorkingDirectory) ? currentWorkingDirectory : ".";
    }
}
