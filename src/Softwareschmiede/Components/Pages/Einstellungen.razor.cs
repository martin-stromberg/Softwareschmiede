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
    [Inject] private IPluginManager PluginManager { get; set; } = default!;
    [Inject] private PluginSettingsService PluginSettings { get; set; } = default!;
    [Inject] private ArbeitsverzeichnisSettingsService ArbeitsverzeichnisSettings { get; set; } = default!;
    [Inject] private IArbeitsverzeichnisResolver ArbeitsverzeichnisResolver { get; set; } = default!;
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
    protected string _arbeitsverzeichnisInput = string.Empty;
    protected string? _arbeitsverzeichnisValidationError;
    protected string? _arbeitsverzeichnisStatusMessage;
    protected bool _arbeitsverzeichnisStatusIsError;
    protected string? _arbeitsverzeichnisFallbackHinweis;

    /// <inheritdoc/>
    protected override async Task OnInitializedAsync()
    {
        Logger.LogInformation("Einstellungsseite geladen.");

        var allPlugins = PluginSettings.GetAllPlugins(
            PluginManager.GetSourceCodeManagementPlugins(),
            PluginManager.GetDevelopmentAutomationPlugins());
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

        var savedWorkdir = await ArbeitsverzeichnisSettings.GetArbeitsverzeichnisAsync();
        _arbeitsverzeichnisInput = savedWorkdir ?? string.Empty;

        var resolution = await ArbeitsverzeichnisResolver.ResolveAsync();
        if (resolution.UsedFallback)
        {
            _arbeitsverzeichnisFallbackHinweis =
                $"Aktuell wird Fallback verwendet ({resolution.ReasonCode}): {Path.Combine(Path.GetTempPath(), "softwareschmiede")}. " +
                "Bitte Pfad prüfen und neu speichern.";
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

    /// <summary>Speichert das globale Arbeitsverzeichnis.</summary>
    protected async Task ArbeitsverzeichnisSpeichernAsync()
    {
        _saving = true;
        _arbeitsverzeichnisStatusMessage = null;
        _arbeitsverzeichnisValidationError = null;
        _arbeitsverzeichnisFallbackHinweis = null;

        try
        {
            // Validierung (nur syntaktisch/strukturell)
            if (!string.IsNullOrWhiteSpace(_arbeitsverzeichnisInput))
            {
                ArbeitsverzeichnisSettingsService.ValidatePathForConfiguration(_arbeitsverzeichnisInput);
            }

            // Speicherung (inkl. Verzeichniserstellung)
            await ArbeitsverzeichnisSettings.SaveArbeitsverzeichnisAsync(_arbeitsverzeichnisInput);

            _arbeitsverzeichnisStatusMessage = "Arbeitsverzeichnis gespeichert.";
            _arbeitsverzeichnisStatusIsError = false;

            var resolution = await ArbeitsverzeichnisResolver.ResolveAsync();
            if (resolution.UsedFallback)
            {
                _arbeitsverzeichnisFallbackHinweis =
                    $"Hinweis: Laufzeit-Fallback aktiv ({resolution.ReasonCode}) auf {Path.Combine(Path.GetTempPath(), "softwareschmiede")}. " +
                    "Pfad prüfen/neu speichern.";
            }
        }
        catch (ArgumentException ex)
        {
            _arbeitsverzeichnisValidationError = ex.Message;
            _arbeitsverzeichnisStatusMessage = "Arbeitsverzeichnis konnte nicht gespeichert werden.";
            _arbeitsverzeichnisStatusIsError = true;
        }
        catch (Exception ex)
        {
            _arbeitsverzeichnisStatusMessage = $"Fehler beim Speichern: {ex.Message}";
            _arbeitsverzeichnisStatusIsError = true;
            Logger.LogError(ex, "Fehler beim Speichern des Arbeitsverzeichnisses.");
        }
        finally
        {
            _saving = false;
        }
    }

    /// <summary>Verarbeitet Änderungen am Arbeitsverzeichnis-Input inkl. Inline-Validierung (ohne Verzeichniserstellung).</summary>
    protected void ArbeitsverzeichnisInputChanged(string value)
    {
        _arbeitsverzeichnisInput = value;
        _arbeitsverzeichnisValidationError = null;

        if (string.IsNullOrWhiteSpace(_arbeitsverzeichnisInput))
        {
            return;
        }

        try
        {
            // Validierung nur - keine Verzeichniserstellung bei jedem Keystroke!
            ArbeitsverzeichnisSettingsService.ValidatePathForConfiguration(_arbeitsverzeichnisInput);
        }
        catch (ArgumentException ex)
        {
            _arbeitsverzeichnisValidationError = ex.Message;
        }
    }

    /// <summary>Setzt das Arbeitsverzeichnis auf den Default-Fallback zurück.</summary>
    protected async Task ArbeitsverzeichnisZuruecksetzenAsync()
    {
        _arbeitsverzeichnisInput = string.Empty;
        await ArbeitsverzeichnisSpeichernAsync();
    }
}
