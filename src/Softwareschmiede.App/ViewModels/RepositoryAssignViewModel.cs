using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.App.ViewModels;

/// <summary>ViewModel für den Repository-Zuweisungs-Dialog.</summary>
public sealed class RepositoryAssignViewModel : ViewModelBase, IDisposable
{
    private readonly ILogger<RepositoryAssignViewModel> _logger;
    private readonly IPluginManager? _pluginManager;
    private readonly DirectoryStructureBrowserService? _directoryStructureService;
    private AvailableRepository? _selectedRepository;
    private bool _isLoading;
    private ObservableCollection<IGitPlugin> _availableScmPlugins = new();
    private IGitPlugin? _selectedScmPlugin;
    private bool _hasScmPlugins;
    private CancellationTokenSource? _reloadCts;
    private ObservableCollection<string> _availableWorkingDirectories = new();
    private string? _selectedWorkingDirectory;
    private bool _isLoadingDirectoryStructure;
    private CancellationTokenSource? _dirStructureCts;

    /// <summary>Wird ausgelöst wenn der Dialog geschlossen werden soll. Parameter: true = bestätigt, false = abgebrochen.</summary>
    public event EventHandler<bool>? CloseRequested;

    /// <summary>Verfügbare Repositories zur Auswahl (aus externer Plugin-Quelle).</summary>
    public ObservableCollection<AvailableRepository> VerfuegbareRepositories { get; } = new();

    /// <summary>Ausgewähltes Repository.</summary>
    public AvailableRepository? SelectedRepository
    {
        get => _selectedRepository;
        set => SetProperty(ref _selectedRepository, value, OnSelectedRepositoryChanged);
    }

    /// <summary>Gibt an, ob Daten geladen werden.</summary>
    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    /// <summary>Liste aller verfügbaren SCM-Plugins.</summary>
    public ObservableCollection<IGitPlugin> AvailableScmPlugins
    {
        get => _availableScmPlugins;
        private set => SetProperty(ref _availableScmPlugins, value);
    }

    /// <summary>Aktuell vom Benutzer gewähltes SCM-Plugin.</summary>
    public IGitPlugin? SelectedScmPlugin
    {
        get => _selectedScmPlugin;
        set => SetProperty(ref _selectedScmPlugin, value, OnSelectedScmPluginChanged);
    }

    /// <summary>Gibt an, ob SCM-Plugins vorhanden sind.</summary>
    public bool HasScmPlugins
    {
        get => _hasScmPlugins;
        private set => SetProperty(ref _hasScmPlugins, value);
    }

    /// <summary>Verfügbare Arbeitsverzeichnisse des ausgewählten Repositories.</summary>
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

    /// <summary>Gibt an, ob die Verzeichnisstruktur des ausgewählten Repositories gerade geladen wird.</summary>
    public bool IsLoadingDirectoryStructure
    {
        get => _isLoadingDirectoryStructure;
        private set => SetProperty(ref _isLoadingDirectoryStructure, value);
    }

    /// <summary>Bestätigt die Auswahl und schließt den Dialog.</summary>
    public ICommand BestaetigenCommand { get; }

    /// <summary>Bricht die Auswahl ab und schließt den Dialog.</summary>
    public ICommand AbbrechenCommand { get; }

    /// <summary>Der laufende Reload-Task; nur für Tests.</summary>
    internal Task? CurrentReloadTask { get; private set; }

    /// <summary>Der laufende Directory-Struktur-Lade-Task; nur für Tests.</summary>
    internal Task? CurrentLoadDirectoryStructureTask { get; private set; }

    /// <inheritdoc cref="RepositoryAssignViewModel"/>
    public RepositoryAssignViewModel(
        ILogger<RepositoryAssignViewModel> logger,
        IPluginManager? pluginManager = null,
        DirectoryStructureBrowserService? directoryStructureService = null)
    {
        _logger = logger;
        _pluginManager = pluginManager;
        _directoryStructureService = directoryStructureService;

        BestaetigenCommand = new RelayCommand(
            () => CloseRequested?.Invoke(this, true),
            () => _selectedRepository != null);
        AbbrechenCommand = new RelayCommand(() => CloseRequested?.Invoke(this, false));
    }

    /// <summary>Lädt alle verfügbaren SCM-Plugins.</summary>
    public async Task LadenAsync(CancellationToken ct = default)
    {
        IsLoading = true;
        try
        {
            if (_pluginManager != null)
            {
                var plugins = _pluginManager.GetSourceCodeManagementPlugins();
                AvailableScmPlugins.Clear();
                foreach (var plugin in plugins)
                    AvailableScmPlugins.Add(plugin);
                HasScmPlugins = AvailableScmPlugins.Count > 0;

                if (HasScmPlugins)
                {
                    SelectedScmPlugin = AvailableScmPlugins[0];
                    if (CurrentReloadTask != null)
                        await CurrentReloadTask;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Laden der verfügbaren SCM-Plugins.");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void OnSelectedScmPluginChanged()
    {
        _reloadCts?.Cancel();
        _reloadCts?.Dispose();
        _reloadCts = new CancellationTokenSource();
        CurrentReloadTask = ReloadRepositoriesForSelectedPlugin(_reloadCts.Token);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _reloadCts?.Cancel();
        _reloadCts?.Dispose();
        _dirStructureCts?.Cancel();
        _dirStructureCts?.Dispose();
    }

    private async Task ReloadRepositoriesForSelectedPlugin(CancellationToken ct)
    {
        if (_pluginManager == null || SelectedScmPlugin == null)
        {
            VerfuegbareRepositories.Clear();
            return;
        }

        try
        {
            IsLoading = true;
            var repos = await SelectedScmPlugin.GetAvailableRepositoriesAsync(ct);
            ct.ThrowIfCancellationRequested();
            var sorted = repos.OrderByDescending(r => r.UpdatedAt).ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase).ToList();
            VerfuegbareRepositories.Clear();
            foreach (var repo in sorted)
                VerfuegbareRepositories.Add(repo);
            SelectedRepository = null;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Abgebrochen durch Plugin-Wechsel — kein Fehler
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Laden der Repositories für das ausgewählte Plugin.");
            VerfuegbareRepositories.Clear();
        }
        finally
        {
            if (!ct.IsCancellationRequested)
                IsLoading = false;
        }
    }

    private void OnSelectedRepositoryChanged()
    {
        SelectedWorkingDirectory = null;
        _dirStructureCts?.Cancel();
        _dirStructureCts?.Dispose();
        _dirStructureCts = new CancellationTokenSource();
        CurrentLoadDirectoryStructureTask = LoadDirectoryStructureAsync(_dirStructureCts.Token);
    }

    private async Task LoadDirectoryStructureAsync(CancellationToken ct)
    {
        if (_directoryStructureService == null || SelectedScmPlugin == null || SelectedRepository == null)
        {
            AvailableWorkingDirectories.Clear();
            return;
        }

        try
        {
            IsLoadingDirectoryStructure = true;
            var directories = await _directoryStructureService.GetDirectoriesAsync(SelectedScmPlugin, SelectedRepository.Url, ct);
            ct.ThrowIfCancellationRequested();

            AvailableWorkingDirectories.Clear();
            AvailableWorkingDirectories.Add(".");
            foreach (var dir in directories)
                AvailableWorkingDirectories.Add(dir);

            SelectedWorkingDirectory = ".";
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Abgebrochen durch Repository-/Plugin-Wechsel — kein Fehler
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fehler beim Laden der Verzeichnisstruktur.");
            AvailableWorkingDirectories.Clear();
            AvailableWorkingDirectories.Add(".");
            SelectedWorkingDirectory = ".";
        }
        finally
        {
            if (!ct.IsCancellationRequested)
                IsLoadingDirectoryStructure = false;
        }
    }
}
