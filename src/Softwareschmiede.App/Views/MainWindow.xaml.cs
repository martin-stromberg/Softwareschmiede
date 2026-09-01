using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.Logging;
using Softwareschmiede.App.Services;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Application.Services.Updates;

namespace Softwareschmiede.App.Views;

/// <summary>Hauptfenster der Softwareschmiede-Desktopanwendung.</summary>
public sealed partial class MainWindow : Window
{
    private readonly AppEinstellungService _einstellungService;
    private readonly DarkModeService _darkModeService;
    private readonly IApplicationVersionProvider _applicationVersionProvider;
    private readonly ILogger<MainWindow> _logger;

    /// <inheritdoc cref="MainWindow"/>
    public MainWindow(
        MainWindowViewModel viewModel,
        AppEinstellungService einstellungService,
        DarkModeService darkModeService,
        IApplicationVersionProvider applicationVersionProvider,
        ILogger<MainWindow> logger)
    {
        InitializeComponent();
        _einstellungService = einstellungService;
        _darkModeService = darkModeService;
        _applicationVersionProvider = applicationVersionProvider;
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
            await SetIconAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fehler beim Initialisieren des Fensters.");
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

    private async Task SetIconAsync()
    {
        try
        {
            var versionInfo = await _applicationVersionProvider.GetInstalledVersionAsync();
            if (versionInfo?.TagName is null)
                return;

            if (versionInfo.TagName.Contains("-rc", StringComparison.OrdinalIgnoreCase))
            {
                var rcIcon = CreateReleaseCandidateIcon();
                if (rcIcon is not null)
                    Icon = rcIcon;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fenstersymbol für RC-Version konnte nicht gesetzt werden.");
        }
    }

    private static ImageSource? CreateReleaseCandidateIcon()
    {
        var uri = new Uri("pack://application:,,,/images/Softwareschmiede.ico");

        var decoder = new IconBitmapDecoder(
            uri,
            BitmapCreateOptions.PreservePixelFormat,
            BitmapCacheOption.OnLoad);

        var frame = decoder.Frames
            .OrderBy(f => f.PixelWidth * f.PixelHeight)
            .LastOrDefault();

        if (frame is null)
            return null;

        var width = frame.PixelWidth;
        var height = frame.PixelHeight;

        var grid = new Grid
        {
            Width = width,
            Height = height,
        };

        grid.Children.Add(new Image
        {
            Source = frame,
            Stretch = Stretch.None,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        });

        var badgeSize = Math.Min(width, height) / 3.5;
        var margin = Math.Min(width, height) / 16.0;

        var badge = new Border
        {
            Width = badgeSize,
            Height = badgeSize,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(margin),
            Background = new SolidColorBrush(Color.FromRgb(192, 0, 0)),
            CornerRadius = new CornerRadius(badgeSize / 2),
            Child = new TextBlock
            {
                Text = "RC",
                Foreground = Brushes.White,
                FontSize = badgeSize / 2.5,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };

        grid.Children.Add(badge);
        grid.Measure(new Size(width, height));
        grid.Arrange(new Rect(0, 0, width, height));

        var render = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        render.Render(grid);
        render.Freeze();

        return render;
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
