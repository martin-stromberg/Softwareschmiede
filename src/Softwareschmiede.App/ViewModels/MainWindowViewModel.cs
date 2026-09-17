using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Softwareschmiede.App.Extensions;
using Softwareschmiede.App.Services;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Application.Services.Updates;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;

namespace Softwareschmiede.App.ViewModels;

/// <summary>ViewModel für das Hauptfenster: Navigation und Dark-Mode-Toggle.</summary>
public sealed class MainWindowViewModel : ViewModelBase, IDisposable
{
    private const int AktualisierungsIntervallSekunden = 5;
    private const string AktiveAufgabenAktualisierenKontext = "MainWindowViewModel.AktiveAufgabenAktualisierenAsync";
    private const string VersionUnbekanntText = "Version unbekannt";

    private readonly DarkModeService _darkModeService;
    private readonly IServiceProvider _serviceProvider;
    private readonly IAktiveAufgabenService _aufgabeService;
    private readonly TodoService _todoService;
    private readonly PromptZeitVersandService _promptZeitVersandService;
    private readonly ILogger<MainWindowViewModel> _logger;
    private readonly IRunningAutomationStatusSource _runningStatusSource;
    private readonly MainWindowUpdateFlow _updateFlow;
    private readonly IDialogService? _dialogService;
    private readonly IApplicationVersionProvider? _versionProvider;
    private readonly IPluginManager? _pluginManager;
    private readonly AufgabeLaufdatenChangedNotifier? _laufdatenChangedNotifier;
    private readonly Action<Action> _dispatcherInvoke;
    private readonly DispatcherTimer _aktualisierungsTimer;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);

    private ViewModelBase? _currentView;
    private bool _isNavigationExpanded = true;
    private string _title = "Softwareschmiede";
    private string? _currentVersion;
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

    /// <summary>Gibt an, ob ein neueres Programmupdate verfügbar ist.</summary>
    public bool UpdateVerfuegbar => _updateFlow.UpdateVerfuegbar;

    /// <summary>Informationen zum aktuell verfügbaren Update.</summary>
    public UpdateInfo? VerfuegbaresUpdate => _updateFlow.VerfuegbaresUpdate;

    /// <summary>Gibt an, ob gerade eine Update-Prüfung läuft.</summary>
    public bool UpdateCheckLaeuft => _updateFlow.UpdateCheckLaeuft;

    /// <summary>Gibt an, ob gerade ein Update heruntergeladen oder vorbereitet wird.</summary>
    public bool UpdateWirdVorbereitet => _updateFlow.UpdateWirdVorbereitet;

    /// <summary>Optionaler Hinweis zur letzten Update-Prüfung.</summary>
    public string? UpdateHinweis => _updateFlow.UpdateHinweis;

    /// <summary>Tooltip der Prüfen-Schaltfläche (begründet eine Deaktivierung im Modus „Aus").</summary>
    public string UpdatePruefenTooltip => _updateFlow.UpdatePruefenTooltip;

    /// <summary>Die aktuell installierte Programmversion als Anzeigetext.</summary>
    public string? CurrentVersion
    {
        get => _currentVersion;
        private set => SetProperty(ref _currentVersion, value);
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

    /// <summary>Startet den geführten Update-Ablauf.</summary>
    public ICommand UpdateStartenCommand { get; }

    /// <summary>Prüft manuell erneut auf Programmupdates.</summary>
    public ICommand UpdatePruefenCommand { get; }

    /// <inheritdoc cref="MainWindowViewModel"/>
    public MainWindowViewModel(
        DarkModeService darkModeService,
        IServiceProvider serviceProvider,
        IAktiveAufgabenService aufgabeService,
        TodoService todoService,
        PromptZeitVersandService promptZeitVersandService,
        ILogger<MainWindowViewModel> logger,
        IRunningAutomationStatusSource runningStatusSource,
        MainWindowOptionaleDienste? optionaleDienste = null)
    {
        _darkModeService = darkModeService;
        _serviceProvider = serviceProvider;
        _aufgabeService = aufgabeService;
        _todoService = todoService;
        _promptZeitVersandService = promptZeitVersandService;
        _logger = logger;
        _runningStatusSource = runningStatusSource;
        _dialogService = optionaleDienste?.DialogService;
        _versionProvider = optionaleDienste?.VersionProvider;
        _laufdatenChangedNotifier = optionaleDienste?.LaufdatenChangedNotifier;
        _pluginManager = serviceProvider.GetService<IPluginManager>();
        _dispatcherInvoke = DispatcherInvokeFactory.Create(optionaleDienste?.DispatcherInvoke);
        _updateFlow = new MainWindowUpdateFlow(
            serviceProvider, logger, optionaleDienste?.UpdateDienste, optionaleDienste?.DialogService);
        _updateFlow.PropertyChanged += (_, e) => OnPropertyChanged(e.PropertyName);

        NavigateToDashboardCommand = new RelayCommand(NavigateToDashboard);
        NavigateToProjectListCommand = new RelayCommand(NavigateToProjectList);
        NavigateToSettingsCommand = new RelayCommand(NavigateToSettings);
        ToggleDarkModeCommand = new AsyncRelayCommand(async ct =>
            await _darkModeService.SetModeAsync(_darkModeService.Current == "Dark" ? "Light" : "Dark", ct));
        ToggleNavigationCommand = new RelayCommand(() => IsNavigationExpanded = !IsNavigationExpanded);
        NavigateZuAufgabeCommand = new RelayCommand<Guid>(NavigateZuAufgabe);
        UpdatePruefenCommand = new AsyncRelayCommand(_updateFlow.PruefenAsync, _updateFlow.KannPruefen);
        UpdateStartenCommand = new AsyncRelayCommand(_updateFlow.StartenAsync, _updateFlow.KannStarten);

        _runningStatusSource.RunningCountChanged += OnRunningCountChanged;
        if (_laufdatenChangedNotifier is not null)
        {
            _laufdatenChangedNotifier.LaufdatenChanged += OnLaufdatenChanged;
        }

        _aktualisierungsTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(AktualisierungsIntervallSekunden)
        };
        _aktualisierungsTimer.Tick += OnAktualisierungsTimerTick;
        _aktualisierungsTimer.Start();

        NavigateToDashboard();
        VersionLadenImHintergrund();
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
        else if (_projectListViewModel.DetailViewModel is not null)
        {
            // Die Seitenleisten-Navigation "Projekte" ist eine explizite Rückkehr zur Projektübersicht.
            // _projectListViewModel wird über die Lebensdauer des Fensters wiederverwendet (siehe oben) -
            // ein zuvor über das Vollbild-Overlay geöffnetes Projekt- oder Aufgabendetail (siehe
            // ProjectListView.xaml / ProjectListViewModel.DetailViewModel) bleibt sonst bestehen, auch
            // wenn es nie explizit über "Zurück" geschlossen wurde. Ohne dieses Zurücksetzen zeigt das
            // Fenster nach einem erneuten Klick auf "Projekte" weiterhin die alte Detailansicht statt der
            // Projektliste (Issue #231-Fortsetzung: WindowExtensions.CurrentView() erkannte dadurch
            // reproduzierbar fälschlich TaskDetailView statt ProjectListView). SchliesseDetailCommand ist
            // der bereits vorhandene, sichere Weg dafür - er disposed sowohl das aktuell offene
            // Detail-ViewModel als auch ein ggf. zurückgehaltenes ProjectDetailViewModel korrekt
            // (siehe ProjectListViewModel.DetailViewModel-Setter).
            _projectListViewModel.SchliesseDetailCommand.Execute(null);
        }
        CurrentView = _projectListViewModel;
        Title = "Softwareschmiede – Projekte";
    }

    private SettingsViewModel? _settingsViewModel;

    private void NavigateToSettings()
    {
        if (_settingsViewModel is null)
        {
            _settingsViewModel = _serviceProvider.GetRequiredService<SettingsViewModel>();
            _settingsViewModel.UpdateSettingsSaved += OnUpdateSettingsSaved;
        }
        CurrentView = _settingsViewModel;
        Title = "Softwareschmiede – Einstellungen";
    }

    private void OnUpdateSettingsSaved(object? sender, UpdateSettings settings)
        => _dispatcherInvoke(() => _updateFlow.ApplyGespeicherteUpdateSettings(settings));

    /// <summary>Lädt die aktuell aktiven Aufgaben und aktualisiert die Seitenleisten-Anzeige.</summary>
    /// <param name="ct">Token zum Abbrechen der Operation.</param>
    public async Task AktiveAufgabenAktualisierenAsync(CancellationToken ct = default)
    {
        if (!await _refreshGate.WaitAsync(0, ct))
            return;

        try
        {
            var aufgaben = await _aufgabeService.GetAktiveAufgabenAsync(ct);
            var offeneTodoCounts = await _todoService.GetOpenTodoCountsAsync(aufgaben.Select(a => a.Id), ct);
            AktiveAufgabenListe.ReplaceAll(aufgaben.Select(a => MapAktiveAufgabePanelItem(a, offeneTodoCounts)));
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

    private AktiveAufgabePanelItem MapAktiveAufgabePanelItem(
        Aufgabe aufgabe,
        IReadOnlyDictionary<Guid, int> offeneTodoCounts)
    {
        var offeneTodoCount = offeneTodoCounts.GetValueOrDefault(aufgabe.Id);
        var offeneTodosCommand = new AsyncRelayCommand(
            ct => OffeneTodosDialogOeffnenAsync(aufgabe.Id, aufgabe.Titel, ct));
        offeneTodosCommand.OnError = ex =>
            _logger.LogWarning(ex, "Fehler beim Öffnen des offene To-Dos-Dialogs für Aufgabe {AufgabeId}.", aufgabe.Id);

        return new AktiveAufgabePanelItem
        {
            Id = aufgabe.Id,
            Titel = aufgabe.Titel,
            ProjektName = aufgabe.Projekt?.Name ?? string.Empty,
            ScmPluginName = ResolvePluginName(PluginType.SourceCodeManagement, aufgabe.GitRepository?.PluginTyp),
            KiPluginName = ResolvePluginName(PluginType.DevelopmentAutomation, aufgabe.KiPluginPrefix),
            Status = aufgabe.Status,
            AusfuehrungsStatus = aufgabe.AusfuehrungsStatus,
            AktiveRunId = aufgabe.AktiveRunId,
            LastHeartbeatUtc = aufgabe.LastHeartbeatUtc,
            LaufStatus = aufgabe.LaufStatus,
            LetzterCliStartUtc = aufgabe.LetzterCliStartUtc,
            PausiertBisUtc = aufgabe.PausiertBisUtc,
            IsAktiv = GetAktiveAufgabeId() == aufgabe.Id,
            HasScheduledPrompt = _promptZeitVersandService.GetScheduledPromptStatus(aufgabe.Id) is not null,
            OffeneTodoCount = offeneTodoCount,
            OffeneTodosAnzeigenCommand = offeneTodosCommand
        };
    }

    private async Task OffeneTodosDialogOeffnenAsync(Guid aufgabeId, string aufgabenTitel, CancellationToken ct)
    {
        if (_dialogService is null)
            return;

        var viewModel = _serviceProvider.GetRequiredService<OpenTodosDialogViewModel>();
        await viewModel.LoadAsync(aufgabeId, aufgabenTitel, ct);
        await _dialogService.ShowOpenTodosDialogAsync(viewModel, ct);
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

    private void OnLaufdatenChanged(Guid aufgabeId)
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

    /// <summary>
    /// Führt die einmalige Update-Startautomatik aus, sobald das Hauptfenster gerendert ist.
    /// Das Startkennzeichen wird vor dem ersten <c>await</c> gesetzt; weitere Aufrufe sind wirkungslos.
    /// Je nach gespeichertem Modus wird nichts, nur geprüft oder geprüft und installiert.
    /// </summary>
    /// <param name="ct">Token zum Abbrechen der Operation.</param>
    public Task InitializeUpdatesAfterWindowReadyAsync(CancellationToken ct = default)
        => _updateFlow.InitializeAfterWindowReadyAsync(ct);

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        _aktualisierungsTimer.Stop();
        _aktualisierungsTimer.Tick -= OnAktualisierungsTimerTick;
        _runningStatusSource.RunningCountChanged -= OnRunningCountChanged;
        if (_laufdatenChangedNotifier is not null)
        {
            _laufdatenChangedNotifier.LaufdatenChanged -= OnLaufdatenChanged;
        }

        if (_settingsViewModel is not null)
        {
            _settingsViewModel.UpdateSettingsSaved -= OnUpdateSettingsSaved;
        }

        _updateFlow.CancelLaufendenUpdateAblauf();

        // _refreshGate wird bewusst nicht disposed: Ein noch laufender Fire-and-Forget-Refresh
        // (Timer-Tick oder RunningCountChanged kurz vor dem Schließen) würde in seinem finally-Block
        // sonst auf ein bereits entsorgtes Semaphore treffen (ObjectDisposedException). Reine
        // WaitAsync(0)/Release()-Nutzung ohne Zugriff auf AvailableWaitHandle benötigt kein Dispose.
    }

    private void VersionLadenImHintergrund()
    {
        VersionLadenAsync(CancellationToken.None).SafeFireAndForget(_logger, "MainWindowViewModel.VersionLadenAsync");
    }

    private async Task VersionLadenAsync(CancellationToken ct)
    {
        var versionText = VersionUnbekanntText;
        try
        {
            var versionInfo = _versionProvider is not null
                ? await _versionProvider.GetInstalledVersionAsync(ct)
                : null;
            if (versionInfo is not null)
                versionText = $"Version {versionInfo.Version}";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fehler beim Laden der installierten Programmversion.");
        }

        _dispatcherInvoke(() => CurrentVersion = versionText);
    }
}
