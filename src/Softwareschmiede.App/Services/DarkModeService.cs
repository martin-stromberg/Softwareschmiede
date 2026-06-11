using System.Windows;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Application.Services;

namespace Softwareschmiede.App.Services;

/// <summary>
/// Verwaltet den Dark-Mode-Zustand der Anwendung.
/// Wechselt zwischen Light- und Dark-Theme via WPF-ResourceDictionary.
/// Persistiert die Einstellung über <see cref="AppEinstellungService"/>.
/// </summary>
public sealed class DarkModeService
{
    private readonly AppEinstellungService _einstellungService;
    private readonly ILogger<DarkModeService> _logger;

    private static readonly Uri LightThemeUri = new("pack://application:,,,/Softwareschmiede.App;component/Themes/LightTheme.xaml");
    private static readonly Uri DarkThemeUri = new("pack://application:,,,/Softwareschmiede.App;component/Themes/DarkTheme.xaml");

    private bool _isDarkMode;

    /// <summary>Gibt an, ob der Dark-Mode derzeit aktiv ist.</summary>
    public bool IsDarkMode => _isDarkMode;

    /// <summary>Wird ausgelöst, wenn der Dark-Mode-Zustand geändert wird.</summary>
    public event Action<bool>? DarkModeChanged;

    /// <inheritdoc cref="DarkModeService"/>
    public DarkModeService(AppEinstellungService einstellungService, ILogger<DarkModeService> logger)
    {
        _einstellungService = einstellungService;
        _logger = logger;
    }

    /// <summary>
    /// Lädt die gespeicherte Einstellung und wendet das Theme an.
    /// Muss auf dem UI-Thread aufgerufen werden.
    /// </summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        var darkModeEnabled = await _einstellungService.GetBoolSettingAsync(
            AppEinstellungService.DarkModeEnabledKey, ct);
        _isDarkMode = darkModeEnabled ?? false;
        ApplyTheme(_isDarkMode);
    }

    /// <summary>
    /// Schaltet den Dark-Mode um und persistiert die Einstellung.
    /// Muss auf dem UI-Thread aufgerufen werden.
    /// </summary>
    public async Task ToggleAsync(CancellationToken ct = default)
    {
        await SetDarkModeAsync(!_isDarkMode, ct);
    }

    /// <summary>
    /// Setzt den Dark-Mode-Zustand und persistiert die Einstellung.
    /// Muss auf dem UI-Thread aufgerufen werden.
    /// </summary>
    public async Task SetDarkModeAsync(bool enabled, CancellationToken ct = default)
    {
        if (_isDarkMode == enabled)
            return;

        _isDarkMode = enabled;
        ApplyTheme(enabled);
        DarkModeChanged?.Invoke(enabled);

        await _einstellungService.SetBoolSettingAsync(AppEinstellungService.DarkModeEnabledKey, enabled, ct);
        _logger.LogInformation("Dark Mode {Status}.", enabled ? "aktiviert" : "deaktiviert");
    }

    private static void ApplyTheme(bool darkMode)
    {
        var targetUri = darkMode ? DarkThemeUri : LightThemeUri;
        var resources = System.Windows.Application.Current.Resources;
        var mergedDictionaries = resources.MergedDictionaries;

        var existing = mergedDictionaries.FirstOrDefault(d =>
            d.Source == LightThemeUri || d.Source == DarkThemeUri);

        if (existing is not null)
        {
            var index = mergedDictionaries.IndexOf(existing);
            var newDict = new ResourceDictionary { Source = targetUri };
            mergedDictionaries[index] = newDict;
        }
        else
        {
            mergedDictionaries.Add(new ResourceDictionary { Source = targetUri });
        }
    }
}
