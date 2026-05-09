using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Application.Services;

/// <summary>
/// Service zum Lesen und Schreiben von Plugin-Einstellungen über den <see cref="ICredentialStore"/>.
/// Schlüssel werden als <c>&lt;PluginPrefix&gt;.&lt;FieldKey&gt;</c> gespeichert.
/// </summary>
public sealed class PluginSettingsService
{
    private readonly ICredentialStore _credentialStore;
    private readonly ILogger<PluginSettingsService> _logger;

    /// <summary>Erstellt eine neue Instanz des <see cref="PluginSettingsService"/>.</summary>
    public PluginSettingsService(ICredentialStore credentialStore, ILogger<PluginSettingsService> logger)
    {
        _credentialStore = credentialStore;
        _logger = logger;
    }

    /// <summary>Gibt alle konfigurierten Plugins zurück (Git- und KI-Plugins).</summary>
    public IReadOnlyList<IPlugin> GetAllPlugins(IEnumerable<IGitPlugin> gitPlugins, IEnumerable<IKiPlugin> kiPlugins)
    {
        return [.. gitPlugins.Cast<IPlugin>(), .. kiPlugins.Cast<IPlugin>()];
    }

    /// <summary>
    /// Gibt den gespeicherten Wert für ein Einstellungsfeld zurück.
    /// Der Schlüssel wird als <c>&lt;PluginPrefix&gt;.&lt;FieldKey&gt;</c> aufgelöst.
    /// </summary>
    public string? GetValue(IPlugin plugin, PluginSettingField field)
    {
        var key = BuildKey(plugin, field);
        _logger.LogDebug("Plugin-Einstellung lesen: {Key}", key);
        return _credentialStore.GetCredential(key);
    }

    /// <summary>
    /// Speichert den Wert für ein Einstellungsfeld.
    /// Der Schlüssel wird als <c>&lt;PluginPrefix&gt;.&lt;FieldKey&gt;</c> aufgelöst.
    /// </summary>
    public void SetValue(IPlugin plugin, PluginSettingField field, string value)
    {
        var key = BuildKey(plugin, field);
        _logger.LogInformation("Plugin-Einstellung schreiben: {Key}", key);
        _credentialStore.SetCredential(key, value);
    }

    /// <summary>Löscht den gespeicherten Wert für ein Einstellungsfeld.</summary>
    public void DeleteValue(IPlugin plugin, PluginSettingField field)
    {
        var key = BuildKey(plugin, field);
        _logger.LogInformation("Plugin-Einstellung löschen: {Key}", key);
        _credentialStore.DeleteCredential(key);
    }

    /// <summary>Gibt an, ob für ein Feld bereits ein Wert gespeichert ist.</summary>
    public bool HasValue(IPlugin plugin, PluginSettingField field)
    {
        return !string.IsNullOrEmpty(GetValue(plugin, field));
    }

    private static string BuildKey(IPlugin plugin, PluginSettingField field) =>
        $"{plugin.PluginPrefix}.{field.Key}";
}
