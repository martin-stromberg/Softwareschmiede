using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Abstractions;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;

namespace Softwareschmiede.Application.Services;

/// <summary>
/// Löst die effektive Plugin-Instanz auf (explizite Auswahl → gespeicherter Default → Fallback).
/// </summary>
public sealed class PluginSelectionService
{
    private readonly IPluginManager _pluginManager;
    private readonly PluginDefaultSettingsService _defaultSettingsService;
    private readonly ILogger<PluginSelectionService> _logger;

    /// <inheritdoc cref="PluginSelectionService"/>
    public PluginSelectionService(
        IPluginManager pluginManager,
        PluginDefaultSettingsService defaultSettingsService,
        ILogger<PluginSelectionService> logger)
    {
        _pluginManager = pluginManager;
        _defaultSettingsService = defaultSettingsService;
        _logger = logger;
    }

    /// <summary>Liest den gespeicherten PluginPrefix für den Plugin-Typ.</summary>
    public Task<string?> GetStoredDefaultPluginPrefixAsync(PluginType pluginType, CancellationToken ct = default)
        => _defaultSettingsService.GetDefaultPluginPrefixAsync(pluginType, ct);

    /// <summary>Speichert den PluginPrefix als Standard für den Plugin-Typ.</summary>
    public Task SaveDefaultPluginPrefixAsync(PluginType pluginType, string? pluginPrefix, CancellationToken ct = default)
        => _defaultSettingsService.SaveDefaultPluginPrefixAsync(pluginType, pluginPrefix, ct);

    /// <summary>Speichert den PluginPrefix als Projekt-Standard für den Plugin-Typ.</summary>
    public Task SaveProjectDefaultPluginPrefixAsync(Guid projektId, PluginType pluginType, string? pluginPrefix, CancellationToken ct = default)
        => _defaultSettingsService.SaveProjectDefaultPluginPrefixAsync(projektId, pluginType, pluginPrefix, ct);

    /// <summary>Löst das SCM-Plugin auf.</summary>
    public async Task<IGitPlugin> ResolveSourceCodeManagementPluginAsync(string? selectedPluginPrefix, CancellationToken ct = default)
    {
        var available = _pluginManager.GetSourceCodeManagementPlugins();
        var resolved = await ResolvePluginAsync(
            PluginType.SourceCodeManagement,
            selectedPluginPrefix,
            available,
            _pluginManager.GetDefaultSourceCodeManagementPlugin,
            p => p.PluginName,
            ct);

        return resolved;
    }

    /// <summary>Gibt alle verfügbaren KI-Plugin-Prefixe zurück.</summary>
    public Task<IReadOnlyList<string>> GetAvailableKiPluginPrefixesAsync(CancellationToken ct = default)
    {
        var plugins = _pluginManager.GetDevelopmentAutomationPlugins();
        var prefixe = (IReadOnlyList<string>)plugins
            .Select(p => p.PluginPrefix)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return Task.FromResult(prefixe);
    }

    /// <summary>Löst das KI-Plugin auf.</summary>
    public async Task<IKiPlugin> ResolveDevelopmentAutomationPluginAsync(string? selectedPluginPrefix, CancellationToken ct = default)
    {
        var available = _pluginManager.GetDevelopmentAutomationPlugins();
        var resolved = await ResolvePluginAsync(
            PluginType.DevelopmentAutomation,
            selectedPluginPrefix,
            available,
            _pluginManager.GetDefaultDevelopmentAutomationPlugin,
            GetKiFallbackSortKey,
            ct);

        return resolved;
    }

    /// <summary>
    /// Löst das KI-Plugin-Prefix mit Projekt-Kontext auf: Aufgaben-Plugin → Projekt-Default → Global-Default.
    /// Wird nichts gefunden, gibt die Methode <c>null</c> zurück; der Aufrufer muss in diesem Fall
    /// den Plugin-Auswahl-Dialog anzeigen.
    /// </summary>
    public async Task<string?> ResolveDevelopmentAutomationPluginWithProjectScopeAsync(
        string? aufgabenPluginPrefix,
        Guid projektId,
        CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(aufgabenPluginPrefix))
        {
            return aufgabenPluginPrefix.Trim();
        }

        var projectDefault = await _defaultSettingsService.GetProjectDefaultPluginPrefixAsync(projektId, PluginType.DevelopmentAutomation, ct);
        if (!string.IsNullOrWhiteSpace(projectDefault))
        {
            return projectDefault;
        }

        var globalDefault = await _defaultSettingsService.GetDefaultPluginPrefixAsync(PluginType.DevelopmentAutomation, ct);
        if (!string.IsNullOrWhiteSpace(globalDefault))
        {
            return globalDefault;
        }

        return null;
    }

    private async Task<TPlugin> ResolvePluginAsync<TPlugin>(
        PluginType pluginType,
        string? selectedPluginPrefix,
        IReadOnlyList<TPlugin> availablePlugins,
        Func<TPlugin> defaultResolver,
        Func<TPlugin, string> fallbackSortKey,
        CancellationToken ct)
        where TPlugin : IPlugin
    {
        if (availablePlugins.Count == 0)
        {
            return defaultResolver();
        }

        var explicitSelection = TryResolveByPrefix(availablePlugins, selectedPluginPrefix);
        if (explicitSelection is not null)
        {
            return explicitSelection;
        }

        var storedDefaultPrefix = await _defaultSettingsService.GetDefaultPluginPrefixAsync(pluginType, ct);
        var storedDefault = TryResolveByPrefix(availablePlugins, storedDefaultPrefix);
        if (storedDefault is not null)
        {
            return storedDefault;
        }

        if (!string.IsNullOrWhiteSpace(storedDefaultPrefix))
        {
            _logger.LogWarning(
                "Gespeichertes Standardplugin '{PluginPrefix}' für {PluginType} nicht verfügbar. Fallback wird verwendet.",
                storedDefaultPrefix,
                pluginType);
        }

        return availablePlugins
                   .OrderBy(fallbackSortKey, StringComparer.OrdinalIgnoreCase)
                   .FirstOrDefault()
               ?? defaultResolver();
    }

    private static TPlugin? TryResolveByPrefix<TPlugin>(IReadOnlyList<TPlugin> plugins, string? pluginPrefix)
        where TPlugin : IPlugin
    {
        if (string.IsNullOrWhiteSpace(pluginPrefix))
        {
            return default;
        }

        return plugins.FirstOrDefault(plugin =>
            string.Equals(plugin.PluginPrefix, pluginPrefix.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private static string GetKiFallbackSortKey(IKiPlugin plugin)
    {
        if (plugin is CliKiPluginBase cliPlugin
            && string.Equals(cliPlugin.ProviderDateiPraefix, "copilot", StringComparison.OrdinalIgnoreCase))
        {
            return $"0-{plugin.PluginName}";
        }

        return $"1-{plugin.PluginName}";
    }
}
