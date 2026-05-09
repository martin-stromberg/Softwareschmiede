using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Components.Pages;

/// <summary>Code-Behind für die generische Einstellungsseite.</summary>
public class EinstellungenBase : ComponentBase
{
    // TODO: Konstanten für Boolean-Werte in Einstellungsfeldern
    protected const string BoolTrue = "true";
    protected const string BoolFalse = "false";
    [Inject] private IEnumerable<IGitPlugin> GitPlugins { get; set; } = default!;
    [Inject] private IEnumerable<IKiPlugin> KiPlugins { get; set; } = default!;
    [Inject] private PluginSettingsService PluginSettings { get; set; } = default!;
    [Inject] private ILogger<EinstellungenBase> Logger { get; set; } = default!;

    // TODO: Jedes Plugin wird als PluginViewModel verwaltet
    protected record PluginViewModel(IPlugin Plugin);

    protected IReadOnlyList<PluginViewModel> _plugins = [];

    // Aktuell eingegebene Werte: Key = "PluginPrefix.FieldKey"
    protected Dictionary<string, string> _inputValues = [];
    // Gibt an, ob im Credential Store ein Wert hinterlegt ist
    protected Dictionary<string, bool> _hasValues = [];
    // Secret-Felder: Sichtbarkeit togglebar
    protected Dictionary<string, bool> _visibleSecrets = [];
    // Status-Meldungen pro Plugin-Prefix
    protected Dictionary<string, string> _statusMessages = [];
    protected Dictionary<string, bool> _errorFlags = [];
    protected bool _saving;

    /// <inheritdoc/>
    protected override void OnInitialized()
    {
        Logger.LogInformation("Einstellungsseite geladen.");

        var allPlugins = PluginSettings.GetAllPlugins(GitPlugins, KiPlugins);
        _plugins = allPlugins.Select(p => new PluginViewModel(p)).ToList();

        foreach (var plugin in allPlugins)
        {
            foreach (var group in plugin.GetSettingGroups())
            {
                foreach (var field in group.Fields)
                {
                    var key = $"{plugin.PluginPrefix}.{field.Key}";
                    var stored = PluginSettings.GetValue(plugin, field);
                    _hasValues[key] = !string.IsNullOrEmpty(stored);
                    _inputValues[key] = string.Empty;
                    _visibleSecrets[key] = false;
                }
            }
        }
    }

    /// <summary>Schaltet die Sichtbarkeit eines Secret-Feldes um.</summary>
    protected void ToggleVisible(string key)
    {
        _visibleSecrets[key] = !_visibleSecrets.GetValueOrDefault(key);
    }

    /// <summary>Speichert alle nicht-leeren Felder des Plugins.</summary>
    protected async Task SpeichernAsync(IPlugin plugin)
    {
        _saving = true;
        _statusMessages[plugin.PluginPrefix] = string.Empty;

        try
        {
            foreach (var group in plugin.GetSettingGroups())
            {
                foreach (var field in group.Fields)
                {
                    var key = $"{plugin.PluginPrefix}.{field.Key}";
                    var value = _inputValues.GetValueOrDefault(key, string.Empty);

                    if (string.IsNullOrWhiteSpace(value))
                    {
                        continue;
                    }

                    await Task.Run(() => PluginSettings.SetValue(plugin, field, value.Trim()));
                    _hasValues[key] = true;
                    _inputValues[key] = string.Empty;
                    _visibleSecrets[key] = false;
                }
            }

            _statusMessages[plugin.PluginPrefix] = "Einstellungen gespeichert.";
            _errorFlags[plugin.PluginPrefix] = false;
            Logger.LogInformation("Plugin-Einstellungen für {Plugin} gespeichert.", plugin.PluginName);
        }
        catch (Exception ex)
        {
            _statusMessages[plugin.PluginPrefix] = $"Fehler beim Speichern: {ex.Message}";
            _errorFlags[plugin.PluginPrefix] = true;
            Logger.LogError(ex, "Fehler beim Speichern der Einstellungen für Plugin {Plugin}.", plugin.PluginName);
        }
        finally
        {
            _saving = false;
        }
    }

    /// <summary>Löscht alle gespeicherten Felder des Plugins und setzt Eingaben zurück.</summary>
    protected async Task ZuruecksetzenAsync(IPlugin plugin)
    {
        _saving = true;
        _statusMessages[plugin.PluginPrefix] = string.Empty;

        try
        {
            foreach (var group in plugin.GetSettingGroups())
            {
                foreach (var field in group.Fields)
                {
                    var key = $"{plugin.PluginPrefix}.{field.Key}";
                    await Task.Run(() => PluginSettings.DeleteValue(plugin, field));
                    _hasValues[key] = false;
                    _inputValues[key] = string.Empty;
                }
            }

            _statusMessages[plugin.PluginPrefix] = "Alle Einstellungen zurückgesetzt.";
            _errorFlags[plugin.PluginPrefix] = false;
            Logger.LogInformation("Plugin-Einstellungen für {Plugin} zurückgesetzt.", plugin.PluginName);
        }
        catch (Exception ex)
        {
            _statusMessages[plugin.PluginPrefix] = $"Fehler beim Zurücksetzen: {ex.Message}";
            _errorFlags[plugin.PluginPrefix] = true;
            Logger.LogError(ex, "Fehler beim Zurücksetzen der Einstellungen für Plugin {Plugin}.", plugin.PluginName);
        }
        finally
        {
            _saving = false;
        }
    }
}
