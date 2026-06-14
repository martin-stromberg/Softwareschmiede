using System.Windows;
using Microsoft.Extensions.DependencyInjection;
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
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DarkModeService> _logger;

    private static Dictionary<string, Uri> _themeUris = new()
    {
        { "Light", new Uri("pack://application:,,,/Softwareschmiede.App;component/Themes/LightTheme.xaml") },
        { "Dark", new Uri("pack://application:,,,/Softwareschmiede.App;component/Themes/DarkTheme.xaml") }
    };

    private string _currentMode = "Dark";

    /// <summary>
    /// Gibt den aktuellen Design-Modus zurück ("Dark" oder "Light").
    /// </summary>
    public string Current => _currentMode;

    /// <summary>Wird ausgelöst, wenn der Dark-Mode-Zustand geändert wird.</summary>
    public event Action<string>? ModeChanged;

    /// <inheritdoc cref="DarkModeService"/>
    public DarkModeService(IServiceScopeFactory scopeFactory, ILogger<DarkModeService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Lädt die gespeicherte Einstellung und wendet das Theme an.
    /// Muss auf dem UI-Thread aufgerufen werden.
    /// </summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var einstellungService = scope.ServiceProvider.GetRequiredService<AppEinstellungService>();
        var mode = await einstellungService.GetSettingAsync(
            AppEinstellungService.DesignModeKey, ct);
        _currentMode = mode ?? "Dark";
        ApplyTheme(_currentMode);
    }

    private static void ApplyTheme(string mode)
    {
        var targetUri = _themeUris[mode];
        var resources = System.Windows.Application.Current.Resources;
        var mergedDictionaries = resources.MergedDictionaries;

        var existing = mergedDictionaries.FirstOrDefault(d =>
            d.Source == _themeUris["Light"] || d.Source == _themeUris["Dark"]);

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
    /// <summary>
    /// Gibt die verfügbaren Design-Modi zurück (derzeit "Dark" und "Light").
    /// </summary>
    /// <returns>Ein Array von Strings, das die verfügbaren Design-Modi enthält.</returns>
    public string[] GetAvailableModes() => new string[] { "Dark", "Light" };

    /// <summary>
    /// Setzt den Design-Modus der Anwendung und speichert die Einstellung.
    /// </summary>
    /// <param name="designMode">Der gewünschte Design-Modus ("Dark" oder "Light").</param>
    /// <param name="ct">Ein optionaler Abbruch-Token.</param>
    /// <returns>Ein Task, der die asynchrone Operation darstellt.</returns>
    public async Task SetModeAsync(string designMode, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var einstellungService = scope.ServiceProvider.GetRequiredService<AppEinstellungService>();
        await einstellungService.SetSettingAsync(AppEinstellungService.DesignModeKey, designMode, ct);

        _currentMode = designMode;
        ApplyTheme(designMode);
        ModeChanged?.Invoke(designMode);
        _logger.LogInformation("Design Mode auf '{Mode}' gesetzt.", designMode);
    }
}
