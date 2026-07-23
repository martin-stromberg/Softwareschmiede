using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Interfaces;

namespace Softwareschmiede.Application.Services;

/// <summary>Verwaltet den benutzerspezifischen Aktivierungsstatus je Plugin und filtert Plugin-Listen entsprechend.</summary>
public sealed class PluginActivationService
{
    private const string EnabledKeyPrefix = "plugins.enabled.";

    private readonly AppEinstellungService _einstellungService;
    private readonly IPluginManager _pluginManager;
    private readonly ILogger<PluginActivationService> _logger;

    /// <inheritdoc cref="PluginActivationService"/>
    public PluginActivationService(
        AppEinstellungService einstellungService,
        IPluginManager pluginManager,
        ILogger<PluginActivationService> logger)
    {
        _einstellungService = einstellungService;
        _pluginManager = pluginManager;
        _logger = logger;
    }

    /// <summary>Prüft, ob das Plugin mit dem angegebenen Prefix aktiviert ist. Fehlender Eintrag bedeutet aktiviert.</summary>
    /// <param name="pluginPrefix">Der eindeutige Prefix des Plugins.</param>
    /// <param name="ct">Abbruchtoken.</param>
    /// <returns><c>true</c>, wenn das Plugin aktiviert ist oder kein Eintrag existiert; sonst <c>false</c>.</returns>
    public async Task<bool> IsPluginEnabledAsync(string pluginPrefix, CancellationToken ct = default)
    {
        var wert = await _einstellungService.GetSettingAsync(BuildKey(pluginPrefix), ct);
        return IsEnabledValue(wert);
    }

    /// <summary>Speichert den Aktivierungsstatus für das Plugin mit dem angegebenen Prefix.</summary>
    /// <param name="pluginPrefix">Der eindeutige Prefix des Plugins.</param>
    /// <param name="enabled">Der zu speichernde Aktivierungsstatus.</param>
    /// <param name="ct">Abbruchtoken.</param>
    public async Task SetPluginEnabledAsync(string pluginPrefix, bool enabled, CancellationToken ct = default)
    {
        await _einstellungService.SetSettingAsync(BuildKey(pluginPrefix), enabled.ToString(), ct);

        _logger.LogInformation(
            "Plugin-Aktivierungsstatus gespeichert: {PluginPrefix} => {Enabled}",
            pluginPrefix,
            enabled);
    }

    /// <summary>Gibt alle aktiven SCM-Plugins zurück.</summary>
    /// <param name="ct">Abbruchtoken.</param>
    /// <returns>Die Liste der aktiven SCM-Plugins.</returns>
    public async Task<IReadOnlyList<IGitPlugin>> GetEnabledSourceCodeManagementPluginsAsync(CancellationToken ct = default)
        => await FilterEnabledAsync(_pluginManager.GetSourceCodeManagementPlugins(), ct);

    /// <summary>Gibt alle aktiven KI-Plugins zurück.</summary>
    /// <param name="ct">Abbruchtoken.</param>
    /// <returns>Die Liste der aktiven KI-Plugins.</returns>
    public async Task<IReadOnlyList<IKiPlugin>> GetEnabledDevelopmentAutomationPluginsAsync(CancellationToken ct = default)
        => await FilterEnabledAsync(_pluginManager.GetDevelopmentAutomationPlugins(), ct);

    private async Task<IReadOnlyList<TPlugin>> FilterEnabledAsync<TPlugin>(IReadOnlyList<TPlugin> plugins, CancellationToken ct)
        where TPlugin : IPlugin
    {
        if (plugins.Count == 0)
            return plugins;

        var keys = plugins.Select(p => BuildKey(p.PluginPrefix)).ToArray();
        var werte = await _einstellungService.GetSettingsAsync(keys, ct);

        return plugins
            .Where(p => IsEnabledValue(werte.GetValueOrDefault(BuildKey(p.PluginPrefix))))
            .ToList();
    }

    private static bool IsEnabledValue(string? wert)
    {
        if (string.IsNullOrWhiteSpace(wert))
            return true;

        return !bool.TryParse(wert, out var enabled) || enabled;
    }

    private static string BuildKey(string pluginPrefix) => $"{EnabledKeyPrefix}{pluginPrefix}";
}
