using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.App.ViewModels;

/// <summary>Repräsentiert eine bearbeitbare Einstellung eines Plugins.</summary>
public sealed class PluginSettingEntry : ViewModelBase
{
    private string _value = string.Empty;

    /// <summary>Feld-Definition des Einstellungsfelds.</summary>
    public PluginSettingField Field { get; }

    /// <summary>Aktueller Wert des Felds.</summary>
    public string Value
    {
        get => _value;
        set => SetProperty(ref _value, value);
    }

    /// <inheritdoc cref="PluginSettingEntry"/>
    public PluginSettingEntry(PluginSettingField field, string? currentValue)
    {
        Field = field;
        _value = currentValue ?? string.Empty;
    }
}

/// <summary>Repräsentiert eine Plugin-Einstellungsgruppe mit ihren Feldern.</summary>
public sealed class PluginSettingGroupEntry
{
    /// <summary>Name der Gruppe.</summary>
    public string GroupName { get; }

    /// <summary>Felder der Gruppe als bearbeitbare Einträge.</summary>
    public IReadOnlyList<PluginSettingEntry> Entries { get; }

    /// <inheritdoc cref="PluginSettingGroupEntry"/>
    public PluginSettingGroupEntry(string groupName, IReadOnlyList<PluginSettingEntry> entries)
    {
        GroupName = groupName;
        Entries = entries;
    }
}

/// <summary>Repräsentiert ein Plugin mit seinen Einstellungsgruppen.</summary>
public sealed class PluginWithSettingsEntry
{
    /// <summary>Das Plugin.</summary>
    public IPlugin Plugin { get; }

    /// <summary>Einstellungsgruppen des Plugins.</summary>
    public IReadOnlyList<PluginSettingGroupEntry> SettingGroups { get; }

    /// <inheritdoc cref="PluginWithSettingsEntry"/>
    public PluginWithSettingsEntry(IPlugin plugin, IReadOnlyList<PluginSettingGroupEntry> settingGroups)
    {
        Plugin = plugin;
        SettingGroups = settingGroups;
    }
}

/// <summary>ViewModel für die automatisch generierte Plugin-Einstellungsansicht.</summary>
public sealed class PluginSettingsViewModel : ViewModelBase
{
    private readonly IPluginManager _pluginManager;
    private readonly PluginSettingsService _pluginSettingsService;
    private readonly ILogger<PluginSettingsViewModel> _logger;

    private bool _isLoading;
    private string? _fehlerMeldung;
    private string? _erfolgMeldung;

    /// <summary>Gibt an, ob Daten geladen werden.</summary>
    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    /// <summary>Fehlermeldung bei Operationsfehlern.</summary>
    public string? FehlerMeldung
    {
        get => _fehlerMeldung;
        private set => SetProperty(ref _fehlerMeldung, value);
    }

    /// <summary>Erfolgsmeldung nach Speichern.</summary>
    public string? ErfolgMeldung
    {
        get => _erfolgMeldung;
        private set => SetProperty(ref _erfolgMeldung, value);
    }

    /// <summary>Alle Plugins mit ihren Einstellungsgruppen.</summary>
    public ObservableCollection<PluginWithSettingsEntry> Plugins { get; } = new();

    /// <summary>Lädt alle Plugin-Einstellungen.</summary>
    public ICommand LadenCommand { get; }

    /// <summary>Speichert alle geänderten Plugin-Einstellungen.</summary>
    public ICommand SpeichernCommand { get; }

    /// <inheritdoc cref="PluginSettingsViewModel"/>
    public PluginSettingsViewModel(
        IPluginManager pluginManager,
        PluginSettingsService pluginSettingsService,
        ILogger<PluginSettingsViewModel> logger)
    {
        _pluginManager = pluginManager;
        _pluginSettingsService = pluginSettingsService;
        _logger = logger;

        LadenCommand = new AsyncRelayCommand(ct => LadenAsync(ct));
        SpeichernCommand = new AsyncRelayCommand(ct => SpeichernAsync(ct));
    }

    private Task LadenAsync(CancellationToken ct)
    {
        IsLoading = true;
        FehlerMeldung = null;
        ErfolgMeldung = null;

        try
        {
            Plugins.Clear();

            var gitPlugins = _pluginManager.GetSourceCodeManagementPlugins().Cast<IPlugin>();
            var kiPlugins = _pluginManager.GetDevelopmentAutomationPlugins().Cast<IPlugin>();

            foreach (var plugin in gitPlugins.Concat(kiPlugins))
            {
                var groups = plugin.GetSettingGroups()
                    .Select(group => new PluginSettingGroupEntry(
                        group.GroupName,
                        group.Fields.Select(field => new PluginSettingEntry(
                            field,
                            _pluginSettingsService.GetValue(plugin, field)))
                        .ToList()))
                    .ToList();

                Plugins.Add(new PluginWithSettingsEntry(plugin, groups));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Laden der Plugin-Einstellungen.");
            FehlerMeldung = $"Fehler: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }

        return Task.CompletedTask;
    }

    private Task SpeichernAsync(CancellationToken ct)
    {
        FehlerMeldung = null;
        ErfolgMeldung = null;

        try
        {
            foreach (var pluginEntry in Plugins)
            {
                foreach (var group in pluginEntry.SettingGroups)
                {
                    foreach (var entry in group.Entries)
                    {
                        _pluginSettingsService.SetValue(pluginEntry.Plugin, entry.Field, entry.Value);
                    }
                }
            }

            ErfolgMeldung = "Einstellungen gespeichert.";
            _logger.LogInformation("Plugin-Einstellungen gespeichert.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Speichern der Plugin-Einstellungen.");
            FehlerMeldung = $"Fehler: {ex.Message}";
        }

        return Task.CompletedTask;
    }
}
