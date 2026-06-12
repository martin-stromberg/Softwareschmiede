using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Softwareschmiede.App.Services;

namespace Softwareschmiede.App.ViewModels;

/// <summary>ViewModel für das Hauptfenster: Navigation und Dark-Mode-Toggle.</summary>
public sealed class MainWindowViewModel : ViewModelBase, IDisposable
{
    private readonly DarkModeService _darkModeService;
    private readonly IServiceProvider _serviceProvider;

    private ViewModelBase? _currentView;
    private bool _isNavigationExpanded = true;
    private bool _isDarkMode;
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
        private set => SetProperty(ref _currentView, value);
    }

    /// <summary>Gibt an, ob die Navigation aufgeklappt ist.</summary>
    public bool IsNavigationExpanded
    {
        get => _isNavigationExpanded;
        set => SetProperty(ref _isNavigationExpanded, value);
    }

    /// <summary>Gibt an, ob der Dark-Mode aktiv ist.</summary>
    public bool IsDarkMode
    {
        get => _isDarkMode;
        private set => SetProperty(ref _isDarkMode, value);
    }

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

    /// <inheritdoc cref="MainWindowViewModel"/>
    public MainWindowViewModel(DarkModeService darkModeService, IServiceProvider serviceProvider)
    {
        _darkModeService = darkModeService;
        _serviceProvider = serviceProvider;

        _isDarkMode = _darkModeService.IsDarkMode;
        _darkModeService.DarkModeChanged += OnDarkModeChanged;

        NavigateToDashboardCommand = new RelayCommand(NavigateToDashboard);
        NavigateToProjectListCommand = new RelayCommand(NavigateToProjectList);
        NavigateToSettingsCommand = new RelayCommand(NavigateToSettings);
        ToggleDarkModeCommand = new AsyncRelayCommand(ct => _darkModeService.ToggleAsync(ct));
        ToggleNavigationCommand = new RelayCommand(() => IsNavigationExpanded = !IsNavigationExpanded);

        NavigateToDashboard();
    }

    private void NavigateToDashboard()
    {
        CurrentView = _serviceProvider.GetRequiredService<DashboardViewModel>();
        Title = "Softwareschmiede – Dashboard";
    }

    private void NavigateToProjectList()
    {
        CurrentView = _serviceProvider.GetRequiredService<ProjectListViewModel>();
        Title = "Softwareschmiede – Projekte";
    }

    private void NavigateToSettings()
    {
        CurrentView = _serviceProvider.GetRequiredService<SettingsViewModel>();
        Title = "Softwareschmiede – Einstellungen";
    }

    private void OnDarkModeChanged(bool enabled)
    {
        IsDarkMode = enabled;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _darkModeService.DarkModeChanged -= OnDarkModeChanged;
    }
}
