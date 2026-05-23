using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Components.Pages;

/// <summary>Code-Behind für die generische Einstellungsseite.</summary>
public class EinstellungenBase : ComponentBase
{
    private static readonly IReadOnlyDictionary<string, string> WorkspaceModeDisplayLabels =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["InSourceDirectory"] = "Direkt im Quellverzeichnis arbeiten",
            ["SeparateWorkingDirectory"] = "Mit separatem Arbeitsverzeichnis arbeiten"
        };

    // TODO: Konstanten für Boolean-Werte in Einstellungsfeldern
    protected const string BoolTrue = "true";
    protected const string BoolFalse = "false";
    [Inject] private IPluginManager PluginManager { get; set; } = default!;
    [Inject] private PluginSelectionService PluginSelection { get; set; } = default!;
    [Inject] private PluginSettingsService PluginSettings { get; set; } = default!;
    [Inject] private ArbeitsverzeichnisSettingsService ArbeitsverzeichnisSettings { get; set; } = default!;
    [Inject] private IArbeitsverzeichnisResolver ArbeitsverzeichnisResolver { get; set; } = default!;
    [Inject] private BenachrichtigungsEinstellungenService BenachrichtigungsEinstellungen { get; set; } = default!;
    [Inject] private IBenutzerkontextService Benutzerkontext { get; set; } = default!;
    [Inject] private IJSRuntime JsRuntime { get; set; } = default!;
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
    protected Dictionary<string, string> _fieldValidationMessages = [];
    protected bool _saving;
    protected IReadOnlyList<IGitPlugin> _sourceCodeManagementPlugins = [];
    protected IReadOnlyList<IKiPlugin> _developmentAutomationPlugins = [];
    protected Dictionary<PluginType, string?> _defaultPluginSelections = new()
    {
        [PluginType.SourceCodeManagement] = null,
        [PluginType.DevelopmentAutomation] = null
    };
    protected Dictionary<PluginType, string> _defaultPluginStatusMessages = [];
    protected Dictionary<PluginType, bool> _defaultPluginStatusIsError = [];
    protected string _arbeitsverzeichnisInput = string.Empty;
    protected string? _arbeitsverzeichnisValidationError;
    protected string? _arbeitsverzeichnisStatusMessage;
    protected bool _arbeitsverzeichnisStatusIsError;
    protected string? _arbeitsverzeichnisFallbackHinweis;
    protected BenachrichtigungsModus _toastModus = BenachrichtigungsModus.Global;
    protected BenachrichtigungsModus _tonModus = BenachrichtigungsModus.NurAufgabenseite;
    protected string? _benachrichtigungStatus;
    protected bool _benachrichtigungStatusIsError;
    protected string? _audioDateiInfo;
    protected IReadOnlyList<BenachrichtigungsModus> _benachrichtigungsModi = Enum.GetValues<BenachrichtigungsModus>();

    /// <inheritdoc/>
    protected override async Task OnInitializedAsync()
    {
        Logger.LogInformation("Einstellungsseite geladen.");

        _sourceCodeManagementPlugins = PluginManager.GetSourceCodeManagementPlugins();
        _developmentAutomationPlugins = PluginManager.GetDevelopmentAutomationPlugins();
        var allPlugins = PluginSettings.GetAllPlugins(_sourceCodeManagementPlugins, _developmentAutomationPlugins);
        _plugins = allPlugins.Select(p => new PluginViewModel(p)).ToList();

        await LadeDefaultPluginAuswahlAsync(PluginType.SourceCodeManagement, _sourceCodeManagementPlugins.Select(p => p.PluginPrefix));
        await LadeDefaultPluginAuswahlAsync(PluginType.DevelopmentAutomation, _developmentAutomationPlugins.Select(p => p.PluginPrefix));

        foreach (var plugin in allPlugins)
        {
            foreach (var group in plugin.GetSettingGroups())
            {
                foreach (var field in group.Fields)
                {
                    var key = $"{plugin.PluginPrefix}.{field.Key}";
                    var stored = PluginSettings.GetValue(plugin, field);
                    _hasValues[key] = !string.IsNullOrEmpty(stored);
                    _fieldValidationMessages[key] = string.Empty;
                    _inputValues[key] = BuildInitialInputValue(field, stored, key);
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

        await LadeBenachrichtigungenAsync();
    }

    /// <summary>Speichert das Standardplugin für den angegebenen Plugin-Typ.</summary>
    protected async Task StandardPluginSpeichernAsync(PluginType pluginType)
    {
        _saving = true;
        _defaultPluginStatusMessages[pluginType] = string.Empty;

        try
        {
            var selectedPrefix = _defaultPluginSelections.GetValueOrDefault(pluginType);
            if (!IstPluginPrefixGueltig(pluginType, selectedPrefix))
            {
                _defaultPluginStatusMessages[pluginType] = "Ungültige Plugin-Auswahl für diesen Plugin-Typ.";
                _defaultPluginStatusIsError[pluginType] = true;
                return;
            }

            await PluginSelection.SaveDefaultPluginPrefixAsync(pluginType, selectedPrefix);
            _defaultPluginStatusMessages[pluginType] = "Standardplugin gespeichert.";
            _defaultPluginStatusIsError[pluginType] = false;
            Logger.LogInformation("Standardplugin gespeichert: {PluginType} => {PluginPrefix}", pluginType, selectedPrefix);
        }
        catch (Exception ex)
        {
            _defaultPluginStatusMessages[pluginType] = $"Fehler beim Speichern: {ex.Message}";
            _defaultPluginStatusIsError[pluginType] = true;
            Logger.LogError(ex, "Fehler beim Speichern des Standardplugins für {PluginType}.", pluginType);
        }
        finally
        {
            _saving = false;
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
                    _fieldValidationMessages[key] = string.Empty;

                    if (field.FieldType == PluginSettingFieldType.Enum && !IsValidEnumOption(field, value))
                    {
                        _fieldValidationMessages[key] = "Ungültige Auswahl.";
                        throw new InvalidOperationException($"Ungültige Enum-Auswahl für '{field.Label}'.");
                    }

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
                    _fieldValidationMessages[key] = string.Empty;
                    _inputValues[key] = field.FieldType == PluginSettingFieldType.Enum
                        ? GetDefaultEnumOption(field)
                        : string.Empty;
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

    private async Task LadeDefaultPluginAuswahlAsync(PluginType pluginType, IEnumerable<string> gueltigePluginPrefixe)
    {
        var gespeicherterPrefix = await PluginSelection.GetStoredDefaultPluginPrefixAsync(pluginType);
        var normalized = string.IsNullOrWhiteSpace(gespeicherterPrefix)
            ? null
            : gespeicherterPrefix.Trim();

        if (normalized is not null && !gueltigePluginPrefixe.Contains(normalized, StringComparer.OrdinalIgnoreCase))
        {
            normalized = null;
        }

        _defaultPluginSelections[pluginType] = normalized;
        _defaultPluginStatusMessages[pluginType] = string.Empty;
        _defaultPluginStatusIsError[pluginType] = false;
    }

    private bool IstPluginPrefixGueltig(PluginType pluginType, string? pluginPrefix)
    {
        if (string.IsNullOrWhiteSpace(pluginPrefix))
        {
            return true;
        }

        return pluginType switch
        {
            PluginType.SourceCodeManagement => _sourceCodeManagementPlugins.Any(p =>
                string.Equals(p.PluginPrefix, pluginPrefix, StringComparison.OrdinalIgnoreCase)),
            PluginType.DevelopmentAutomation => _developmentAutomationPlugins.Any(p =>
                string.Equals(p.PluginPrefix, pluginPrefix, StringComparison.OrdinalIgnoreCase)),
            _ => false
        };
    }

    private string BuildInitialInputValue(PluginSettingField field, string? stored, string key)
    {
        if (field.FieldType != PluginSettingFieldType.Enum)
        {
            return string.Empty;
        }

        var fallback = GetDefaultEnumOption(field);
        if (string.IsNullOrWhiteSpace(stored))
        {
            return fallback;
        }

        if (IsValidEnumOption(field, stored))
        {
            return stored.Trim();
        }

        _fieldValidationMessages[key] = $"Ungültiger gespeicherter Wert '{stored}'. Fallback auf '{fallback}'.";
        return fallback;
    }

    private static string GetDefaultEnumOption(PluginSettingField field)
    {
        if (field.EnumOptions is null || field.EnumOptions.Count == 0)
        {
            return string.Empty;
        }

        return field.EnumOptions.Contains("SeparateWorkingDirectory", StringComparer.Ordinal)
            ? "SeparateWorkingDirectory"
            : field.EnumOptions[0];
    }

    private static bool IsValidEnumOption(PluginSettingField field, string value)
    {
        if (field.EnumOptions is null || field.EnumOptions.Count == 0)
        {
            return false;
        }

        return field.EnumOptions.Contains(value.Trim(), StringComparer.Ordinal);
    }

    protected static string GetEnumOptionDisplayLabel(PluginSettingField field, string enumOption)
    {
        if (string.Equals(field.Key, "WorkspaceMode", StringComparison.Ordinal)
            && WorkspaceModeDisplayLabels.TryGetValue(enumOption, out var label))
        {
            return label;
        }

        return enumOption;
    }

    protected static string GetBenachrichtigungsModusLabel(BenachrichtigungsModus modus)
    {
        return modus switch
        {
            BenachrichtigungsModus.Deaktiviert => "Deaktiviert",
            BenachrichtigungsModus.NurAufgabenseite => "Nur auf Aufgabenseite",
            BenachrichtigungsModus.Global => "Global",
            _ => modus.ToString()
        };
    }

    protected async Task BenachrichtigungsEinstellungenSpeichernAsync()
    {
        _saving = true;
        _benachrichtigungStatus = null;
        _benachrichtigungStatusIsError = false;
        try
        {
            var benutzerId = Benutzerkontext.GetBenutzerId();
            await BenachrichtigungsEinstellungen.SaveAsync(
                benutzerId,
                new BenachrichtigungsEinstellungenDto(_toastModus, _tonModus));
            _benachrichtigungStatus = "Benachrichtigungseinstellungen gespeichert.";
        }
        catch (Exception ex)
        {
            _benachrichtigungStatus = $"Fehler beim Speichern: {ex.Message}";
            _benachrichtigungStatusIsError = true;
            Logger.LogError(ex, "Fehler beim Speichern der Benachrichtigungseinstellungen.");
        }
        finally
        {
            _saving = false;
        }
    }

    protected async Task BenachrichtigungsAudioDateiAusgewaehltAsync(InputFileChangeEventArgs args)
    {
        if (args.FileCount == 0)
        {
            return;
        }

        _saving = true;
        _benachrichtigungStatus = null;
        _benachrichtigungStatusIsError = false;
        try
        {
            var file = args.File;
            await using var stream = file.OpenReadStream(BenachrichtigungsEinstellungenService.MaxAudioDateigroesseBytes);
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory);
            var benutzerId = Benutzerkontext.GetBenutzerId();
            await BenachrichtigungsEinstellungen.UploadAudioAsync(
                benutzerId,
                file.Name,
                file.ContentType,
                memory.ToArray());

            _benachrichtigungStatus = "Audio-Datei gespeichert.";
            await LadeBenachrichtigungenAsync();
        }
        catch (Exception ex)
        {
            _benachrichtigungStatus = $"Upload fehlgeschlagen: {ex.Message}";
            _benachrichtigungStatusIsError = true;
            Logger.LogWarning(ex, "Audio-Upload fehlgeschlagen.");
        }
        finally
        {
            _saving = false;
        }
    }

    protected async Task BenachrichtigungsAudioEntfernenAsync()
    {
        _saving = true;
        _benachrichtigungStatus = null;
        _benachrichtigungStatusIsError = false;
        try
        {
            await BenachrichtigungsEinstellungen.RemoveAudioAsync(Benutzerkontext.GetBenutzerId());
            _benachrichtigungStatus = "Benutzerdefinierter Ton entfernt. Standardton ist aktiv.";
            await LadeBenachrichtigungenAsync();
        }
        catch (Exception ex)
        {
            _benachrichtigungStatus = $"Fehler beim Entfernen: {ex.Message}";
            _benachrichtigungStatusIsError = true;
            Logger.LogWarning(ex, "Audio-Datei konnte nicht entfernt werden.");
        }
        finally
        {
            _saving = false;
        }
    }

    protected async Task TesttonAbspielenAsync()
    {
        try
        {
            var audio = await BenachrichtigungsEinstellungen.GetAudioPayloadAsync(Benutzerkontext.GetBenutzerId());
            var result = await JsRuntime.InvokeAsync<string>(
                "softwareschmiedeNotifications.playAlert",
                audio?.Base64Inhalt,
                audio?.MimeType);

            _benachrichtigungStatus = string.Equals(result, "deferred", StringComparison.OrdinalIgnoreCase)
                ? "Browser hat den Ton blockiert. Nach der nächsten Interaktion wird automatisch erneut versucht."
                : "Testton abgespielt.";
            _benachrichtigungStatusIsError = string.Equals(result, "failed", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _benachrichtigungStatus = $"Testton konnte nicht abgespielt werden: {ex.Message}";
            _benachrichtigungStatusIsError = true;
            Logger.LogWarning(ex, "Testton fehlgeschlagen.");
        }
    }

    private async Task LadeBenachrichtigungenAsync()
    {
        var benutzerId = Benutzerkontext.GetBenutzerId();
        var settings = await BenachrichtigungsEinstellungen.GetAsync(benutzerId);
        _toastModus = settings.ToastModus;
        _tonModus = settings.TonModus;

        var audioInfo = await BenachrichtigungsEinstellungen.GetAudioInfoAsync(benutzerId);
        _audioDateiInfo = audioInfo.HatBenutzerdefinierteDatei
            ? $"Aktive Datei: {audioInfo.Dateiname} ({audioInfo.GroesseBytes} Bytes)"
            : "Kein eigener Ton hinterlegt – Standardton aktiv.";
    }
}
