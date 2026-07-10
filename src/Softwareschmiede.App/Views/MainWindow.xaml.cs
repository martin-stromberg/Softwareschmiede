using System.Windows;
using Microsoft.Extensions.Logging;
using Softwareschmiede.App.Services;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.Application.Services;

namespace Softwareschmiede.App.Views;

/// <summary>Hauptfenster der Softwareschmiede-Desktopanwendung.</summary>
public sealed partial class MainWindow : Window
{
    private readonly AppEinstellungService _einstellungService;
    private readonly DarkModeService _darkModeService;
    private readonly ILogger<MainWindow> _logger;

    /// <inheritdoc cref="MainWindow"/>
    public MainWindow(
        MainWindowViewModel viewModel,
        AppEinstellungService einstellungService,
        DarkModeService darkModeService,
        ILogger<MainWindow> logger)
    {
        InitializeComponent();
        _einstellungService = einstellungService;
        _darkModeService = darkModeService;
        _logger = logger;
        DataContext = viewModel;
    }

    /// <inheritdoc/>
    protected override async void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        try
        {
            await _darkModeService.InitializeAsync();
            await RestoreWindowGeometryAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fehler beim Initialisieren der Fenstergeometrie.");
        }
    }

    /// <inheritdoc/>
    protected override async void OnClosed(EventArgs e)
    {
        try
        {
            await PersistWindowGeometryAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fenstergeometrie konnte nicht gespeichert werden.");
        }

        (DataContext as IDisposable)?.Dispose();

        base.OnClosed(e);
    }

    private async Task RestoreWindowGeometryAsync()
    {
        var geometry = await _einstellungService.GetWindowGeometryAsync();

        if (geometry.X.HasValue && geometry.Y.HasValue)
        {
            Left = geometry.X.Value;
            Top = geometry.Y.Value;
        }

        if (geometry.Width is > 0)
            Width = geometry.Width.Value;

        if (geometry.Height is > 0)
            Height = geometry.Height.Value;
    }

    private async Task PersistWindowGeometryAsync()
    {
        var geometry = new WindowGeometrySettings(
            (int)Left,
            (int)Top,
            (int)Width,
            (int)Height);

        await _einstellungService.SetWindowGeometryAsync(geometry);
    }
}
