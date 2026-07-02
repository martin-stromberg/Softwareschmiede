using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Softwareschmiede.App.Extensions;
using Softwareschmiede.App.Services;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;

namespace Softwareschmiede.App.ViewModels;

/// <summary>ViewModel für das Hauptfenster: Navigation und Dark-Mode-Toggle.</summary>
public sealed class MainWindowViewModel : ViewModelBase
{
    private readonly DarkModeService _darkModeService;
    private readonly IServiceProvider _serviceProvider;
    private readonly AufgabeService _aufgabeService;
    private readonly ILogger<MainWindowViewModel> _logger;

    private ViewModelBase? _currentView;
    private bool _isNavigationExpanded = true;
    private string _title = "Softwareschmiede";

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
            _ = AktiveAufgabenAktualisierenAsync();
        });
    }

    /// <summary>Gibt an, ob die Navigation aufgeklappt ist.</summary>
    public bool IsNavigationExpanded
    {
        get => _isNavigationExpanded;
        set => SetProperty(ref _isNavigationExpanded, value);
    }

    /// <summary>Aktuell aktive Aufgaben (Status Gestartet oder Wartend) für die Seitenleisten-Anzeige.</summary>
    public ObservableCollection<Aufgabe> AktiveAufgabenListe { get; } = new();

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
        ILogger<MainWindowViewModel> logger)
    {
        _darkModeService = darkModeService;
        _serviceProvider = serviceProvider;
        _aufgabeService = aufgabeService;
        _logger = logger;

        NavigateToDashboardCommand = new RelayCommand(NavigateToDashboard);
        NavigateToProjectListCommand = new RelayCommand(NavigateToProjectList);
        NavigateToSettingsCommand = new RelayCommand(NavigateToSettings);
        ToggleNavigationCommand = new RelayCommand(() => IsNavigationExpanded = !IsNavigationExpanded);
        NavigateZuAufgabeCommand = new RelayCommand<Guid>(NavigateZuAufgabe);

        NavigateToDashboard();
    }

    private DashboardViewModel? _dashboardViewModel;

    private void NavigateToDashboard()
    {
        if (_dashboardViewModel is null)
        {
            _dashboardViewModel = _serviceProvider.GetRequiredService<DashboardViewModel>();
            _dashboardViewModel.AktiveAufgabenListe = AktiveAufgabenListe;
            _dashboardViewModel.NavigateZuAufgabeAction = NavigateZuAufgabe;
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
        try
        {
            var aufgaben = await _aufgabeService.GetAktiveAufgabenAsync(ct);
            AktiveAufgabenListe.ReplaceAll(aufgaben);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fehler beim Aktualisieren der aktiven Aufgaben in der Seitenleiste.");
        }
    }

    private void NavigateZuAufgabe(Guid aufgabeId)
    {
        var viewModel = _serviceProvider.GetRequiredService<TaskDetailViewModel>();
        viewModel.ZurueckAction = NavigateToDashboard;
        viewModel.AufgabeId = aufgabeId;
        CurrentView = viewModel;
    }
}
