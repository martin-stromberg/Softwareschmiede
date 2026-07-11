using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Softwareschmiede.App.Extensions;
using Softwareschmiede.App.Services;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;

namespace Softwareschmiede.App.ViewModels;

/// <summary>ViewModel für das Hauptfenster: Navigation und Dark-Mode-Toggle.</summary>
public sealed class MainWindowViewModel : ViewModelBase, IDisposable
{
    private const int AktualisierungsIntervallSekunden = 5;
    private const string AktiveAufgabenAktualisierenKontext = "MainWindowViewModel.AktiveAufgabenAktualisierenAsync";

    private readonly DarkModeService _darkModeService;
    private readonly IServiceProvider _serviceProvider;
    private readonly AufgabeService _aufgabeService;
    private readonly ILogger<MainWindowViewModel> _logger;
    private readonly IRunningAutomationStatusSource _runningStatusSource;
    private readonly IPluginManager? _pluginManager;
    private readonly Action<Action> _dispatcherInvoke;
    private readonly DispatcherTimer _aktualisierungsTimer;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);

    private ViewModelBase? _currentView;
    private bool _isNavigationExpanded = true;
    private string _title = "Softwareschmiede";
    private bool _disposed;

    /// <summary>Gibt den Fenstertitel zurück.</summary>
    public string Title
    {
        get => _title;
        private set => SetProperty(ref _title, value);
    }

    /// <summary>Das aktuell angezeigte ViewModel (Navigationsinhalt).</summary>
    public ViewModelBase? CurrentView
    {
        get => _currentView;
        private set => SetProperty(ref _currentView, value, () =>
        {
            OnPropertyChanged(nameof(IsDashboardVisible));
            AktiveAufgabenImHintergrundAktualisieren();
        });
    }

    /// <summary>Gibt an, ob die Navigation aufgeklappt ist.</summary>
    public bool IsNavigationExpanded
    {
        get => _isNavigationExpanded;
        set => SetProperty(ref _isNavigationExpanded, value);
    }

    /// <summary>Aktuell aktive Aufgaben (Status Gestartet oder Wartend) für die Seitenleisten-Anzeige.</summary>
    public ObservableCollection<AktiveAufgabePanelItem> AktiveAufgabenListe { get; } = new();

    /// <summary>Gibt an, ob aktuell das Dashboard angezeigt wird.</summary>
    public bool IsDashboardVisible => CurrentView is DashboardViewModel;

    /// <summary>Navigiert zum Dashboard.</summary>
    public ICommand NavigateToDashboardCommand { get; }

    /// <summary>Navigiert zur Projektliste.</summary>
    public ICommand NavigateToProjectListCommand { get; }

    /// <summary>Navigiert zu den Einstellungen.</summary>
    public ICommand NavigateToSettingsCommand { get; }

    /// <summary>Schaltet den Dark-Mode um.</summary>
    public ICommand ToggleDarkModeCommand { get; }

    /// <summary>Klappt die Navigation ein oder aus.</summary>
    public ICommand ToggleNavigationCommand { get; }

    /// <summary>Navigiert zur Aufgabendetailansicht einer aktiven Aufgabe.</summary>
    public ICommand NavigateZuAufgabeCommand { get; }

    /// <inheritdoc cref="MainWindowViewModel"/>
    public MainWindowViewModel(
        DarkModeService darkModeService,
        IServiceProvider serviceProvider,
        AufgabeService aufgabeService,
        ILogger<MainWindowViewModel> logger,
        IRunningAutomationStatusSource runningStatusSource,
        Action<Action>? dispatcherInvoke = null)
    {
        _darkModeService = darkModeService;
        _serviceProvider = serviceProvider;
        _aufgabeService = aufgabeService;
        _logger = logger;
        _runningStatusSource = runningStatusSource;
        _pluginManager = serviceProvider.GetService<IPluginManager>();
        _dispatcherInvoke = DispatcherInvokeFactory.Create(dispatcherInvoke);

        NavigateToDashboardCommand = new RelayCommand(NavigateToDashboard);
        NavigateToProjectListCommand = new RelayCommand(NavigateToProjectList);
        NavigateToSettingsCommand = new RelayCommand(NavigateToSettings);
        ToggleNavigationCommand = new RelayCommand(() => IsNavigationExpanded = !IsNavigationExpanded);
        NavigateZuAufgabeCommand = new RelayCommand<Guid>(NavigateZuAufgabe);

        _runningStatusSource.RunningCountChanged += OnRunningCountChanged;

        _aktualisierungsTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(AktualisierungsIntervallSekunden)
        };
        _aktualisierungsTimer.Tick += OnAktualisierungsTimerTick;
        _aktualisierungsTimer.Start();

        NavigateToDashboard();
    }

    private DashboardViewModel? _dashboardViewModel;

    private void NavigateToDashboard()
    {
        if (_dashboardViewModel is null)
        {
            _dashboardViewModel = _serviceProvider.GetRequiredService<DashboardViewModel>();
            _dashboardViewModel.Initialize(AktiveAufgabenListe, NavigateZuAufgabe);
        }
        CurrentView = _dashboardViewModel;
        Title = "Softwareschmiede – Dashboard";
    }

    private ProjectListViewModel? _projectListViewModel;

    private void NavigateToProjectList()
    {
        if (_projectListViewModel is null)
        {
            _projectListViewModel = _serviceProvider.GetRequiredService<ProjectListViewModel>();
            _projectListViewModel.DetailTitelAenderungAction = detailTitel =>
            {
                if (!ReferenceEquals(CurrentView, _projectListViewModel))
                    return;

                Title = string.IsNullOrWhiteSpace(detailTitel)
                    ? "Softwareschmiede – Projekte"
                    : $"Softwareschmiede – {detailTitel}";
            };
        }
        CurrentView = _projectListViewModel;
        Title = "Softwareschmiede – Projekte";
    }

    private SettingsViewModel? _settingsViewModel;

    private void NavigateToSettings()
    {
        _settingsViewModel ??= _serviceProvider.GetRequiredService<SettingsViewModel>();
        CurrentView = _settingsViewModel;
        Title = "Softwareschmiede – Einstellungen";
    }

    /// <summary>Lädt die aktuell aktiven Aufgaben und aktualisiert die Seitenleisten-Anzeige.</summary>
    /// <param name="ct">Token zum Abbrechen der Operation.</param>
    public async Task AktiveAufgabenAktualisierenAsync(CancellationToken ct = default)
    {
        if (!await _refreshGate.WaitAsync(0, ct))
            return;

        try
        {
            var aufgaben = await _aufgabeService.GetAktiveAufgabenAsync(ct);
            AktiveAufgabenListe.ReplaceAll(aufgaben.Select(MapAktiveAufgabePanelItem));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fehler beim Aktualisieren der aktiven Aufgaben in der Seitenleiste.");
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private void NavigateZuAufgabe(Guid aufgabeId)
    {
        var viewModel = _serviceProvider.GetRequiredService<TaskDetailViewModel>();
        viewModel.ZurueckAction = NavigateToDashboard;
        viewModel.DetailTitelAenderungAction = detailTitel =>
        {
            if (!ReferenceEquals(CurrentView, viewModel))
                return;

            Title = string.IsNullOrWhiteSpace(detailTitel)
                ? "Softwareschmiede – Aufgabe"
                : $"Softwareschmiede – {detailTitel}";
        };
        Title = "Softwareschmiede – Aufgabe";
        CurrentView = viewModel;
        viewModel.AufgabeId = aufgabeId;
    }

    private AktiveAufgabePanelItem MapAktiveAufgabePanelItem(Aufgabe aufgabe)
    {
        return new AktiveAufgabePanelItem
        {
            Id = aufgabe.Id,
            Titel = aufgabe.Titel,
            ProjektName = aufgabe.Projekt?.Name ?? string.Empty,
            ScmPluginName = ResolvePluginName(PluginType.SourceCodeManagement, aufgabe.GitRepository?.PluginTyp),
            KiPluginName = ResolvePluginName(PluginType.DevelopmentAutomation, aufgabe.KiPluginPrefix),
            Status = aufgabe.Status,
            AktiveRunId = aufgabe.AktiveRunId,
            LastHeartbeatUtc = aufgabe.LastHeartbeatUtc,
            LaufStatus = aufgabe.LaufStatus,
            LetzterCliStartUtc = aufgabe.LetzterCliStartUtc,
            IsAktiv = GetAktiveAufgabeId() == aufgabe.Id
        };
    }

    private string? ResolvePluginName(PluginType pluginType, string? pluginPrefix)
    {
        if (string.IsNullOrWhiteSpace(pluginPrefix))
        {
            return null;
        }

        var plugins = pluginType == PluginType.SourceCodeManagement
            ? _pluginManager?.GetSourceCodeManagementPlugins().Cast<IPlugin>()
            : _pluginManager?.GetDevelopmentAutomationPlugins().Cast<IPlugin>();

        return plugins?.FirstOrDefault(p => string.Equals(p.PluginPrefix, pluginPrefix, StringComparison.OrdinalIgnoreCase))?.PluginName
            ?? pluginPrefix;
    }

    private Guid? GetAktiveAufgabeId()
    {
        return CurrentView is TaskDetailViewModel { AufgabeId: var aufgabeId } && aufgabeId != Guid.Empty
            ? aufgabeId
            : null;
    }

    private void OnRunningCountChanged(int previousCount, int currentCount)
    {
        _dispatcherInvoke(AktiveAufgabenImHintergrundAktualisieren);
    }

    private void OnAktualisierungsTimerTick(object? sender, EventArgs e)
    {
        AktiveAufgabenImHintergrundAktualisieren();
    }

    private void AktiveAufgabenImHintergrundAktualisieren()
    {
        AktiveAufgabenAktualisierenAsync().SafeFireAndForget(_logger, AktiveAufgabenAktualisierenKontext);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        _aktualisierungsTimer.Stop();
        _aktualisierungsTimer.Tick -= OnAktualisierungsTimerTick;
        _runningStatusSource.RunningCountChanged -= OnRunningCountChanged;

        // _refreshGate wird bewusst nicht disposed: Ein noch laufender Fire-and-Forget-Refresh
        // (Timer-Tick oder RunningCountChanged kurz vor dem Schließen) würde in seinem finally-Block
        // sonst auf ein bereits entsorgtes Semaphore treffen (ObjectDisposedException). Reine
        // WaitAsync(0)/Release()-Nutzung ohne Zugriff auf AvailableWaitHandle benötigt kein Dispose.
    }
}
