using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using Softwareschmiede.App.Services;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using System.Windows.Input;

namespace Softwareschmiede.App.ViewModels;

/// <summary>ViewModel für die Einstellungsseite.</summary>
public sealed class SettingsViewModel : ViewModelBase, IDisposable
{
    private const string CodexPluginPrefix = "Softwareschmiede.Codex";
    private const string CommandLineParametersKey = "CommandLineParameters";

    private readonly AppEinstellungService _einstellungService;
    private readonly ArbeitsverzeichnisSettingsService _arbeitsverzeichnisService;
    private readonly DarkModeService _darkModeService;
    private readonly IPluginManager _pluginManager;
    private readonly PluginSettingsService _pluginSettingsService;
    private readonly PromptVorlagenService _promptVorlagenService;
    private readonly ILogger<SettingsViewModel> _logger;
    private readonly List<Guid> _geloeschtePromptVorlagenIds = [];

    private string? _arbeitsverzeichnis;
    private string _designMode;
    private string? _defaultKiPlugin;
    private IGitPlugin? _defaultScmPlugin;
    private BenachrichtigungsModus _benachrichtigungsModus;
    private bool _isLoading;
    private string? _fehlerMeldung;
    private string? _erfolgsMeldung;
    private IReadOnlyList<PluginSettingGroupEntry> _selectedScmPluginSettings = [];
    private IReadOnlyList<PluginSettingGroupEntry> _selectedKiPluginSettings = [];
    private bool _openVisualStudioCodeWhenNoSolutionFound;

    /// <summary>Arbeitsverzeichnis für Repository-Klone.</summary>
    public string? Arbeitsverzeichnis
    {
        get => _arbeitsverzeichnis;
        set => SetProperty(ref _arbeitsverzeichnis, value);
    }

    /// <summary>Aktuell gewählter Design-Modus (z. B. "Hell", "Dunkel", "System").</summary>
    public string DesignMode
    {
        get => _designMode;
        set => SetProperty(ref _designMode, value);
    }

    /// <summary>Alle verfügbaren Design-Modi.</summary>
    /// <value>Die Liste der vom Dark-Mode-Service bereitgestellten Modi.</value>
    public IEnumerable<string> DesignModes => _darkModeService.GetAvailableModes();

    /// <summary>Standard-KI-Plugin-Prefix.</summary>
    public string? DefaultKiPlugin
    {
        get => _defaultKiPlugin;
        set => SetProperty(ref _defaultKiPlugin, value);
    }

    /// <summary>Alle verfügbaren SCM-Plugins.</summary>
    public IReadOnlyList<IGitPlugin> ScmPlugins { get; private set; } = [];

    /// <summary>Alle verfügbaren KI-Plugins.</summary>
    public IReadOnlyList<IKiPlugin> KiPlugins { get; private set; } = [];

    /// <summary>Aktuell gewähltes Standard-SCM-Plugin.</summary>
    public IGitPlugin? DefaultScmPlugin
    {
        get => _defaultScmPlugin;
        set => SetProperty(ref _defaultScmPlugin, value);
    }

    /// <summary>Benachrichtigungsmodus.</summary>
    public BenachrichtigungsModus BenachrichtigungsModus
    {
        get => _benachrichtigungsModus;
        set => SetProperty(ref _benachrichtigungsModus, value);
    }

    /// <summary>Gibt an, ob Daten geladen werden.</summary>
    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    /// <summary>Fehlermeldung.</summary>
    public string? FehlerMeldung
    {
        get => _fehlerMeldung;
        private set => SetProperty(ref _fehlerMeldung, value);
    }

    /// <summary>Erfolgsmeldung nach dem Speichern.</summary>
    public string? ErfolgsMeldung
    {
        get => _erfolgsMeldung;
        private set => SetProperty(ref _erfolgsMeldung, value);
    }

    /// <summary>Einstellungsgruppen des aktuell gewählten SCM-Plugins.</summary>
    public IReadOnlyList<PluginSettingGroupEntry> SelectedScmPluginSettings
    {
        get => _selectedScmPluginSettings;
        private set => SetProperty(ref _selectedScmPluginSettings, value);
    }

    /// <summary>Einstellungsgruppen des aktuell gewählten KI-Plugins.</summary>
    public IReadOnlyList<PluginSettingGroupEntry> SelectedKiPluginSettings
    {
        get => _selectedKiPluginSettings;
        private set => SetProperty(ref _selectedKiPluginSettings, value);
    }

    /// <summary>Gibt an, ob ohne gefundene Visual-Studio-Solution optional Visual Studio Code geöffnet wird.</summary>
    public bool OpenVisualStudioCodeWhenNoSolutionFound
    {
        get => _openVisualStudioCodeWhenNoSolutionFound;
        set => SetProperty(ref _openVisualStudioCodeWhenNoSolutionFound, value);
    }

    /// <summary>Editierbare Promptvorlagen.</summary>
    public ObservableCollection<PromptVorlageEntry> PromptVorlagen { get; } = [];

    /// <summary>Lädt alle Einstellungen.</summary>
    public ICommand LadenCommand { get; }

    /// <summary>Speichert alle Einstellungen.</summary>
    public ICommand SpeichernCommand { get; }

    /// <summary>Verwirft alle nicht gespeicherten Einstellungen.</summary>
    public ICommand VerwerfenCommand { get; }

    /// <summary>Wird ausgelöst wenn der Nutzer ein SCM-Plugin wählt. Lädt die Einstellungsgruppen des Plugins.</summary>
    public ICommand ScmPluginSelectedCommand { get; }

    /// <summary>Wird ausgelöst wenn der Nutzer ein KI-Plugin wählt. Lädt die Einstellungsgruppen des Plugins.</summary>
    public ICommand KiPluginSelectedCommand { get; }

    /// <summary>Fügt eine neue Promptvorlage hinzu.</summary>
    public ICommand PromptVorlageHinzufuegenCommand { get; }

    /// <summary>Entfernt eine Promptvorlage aus der Liste.</summary>
    public ICommand PromptVorlageLoeschenCommand { get; }

    /// <inheritdoc cref="SettingsViewModel"/>
    public SettingsViewModel(
        AppEinstellungService einstellungService,
        ArbeitsverzeichnisSettingsService arbeitsverzeichnisService,
        DarkModeService darkModeService,
        IPluginManager pluginManager,
        PluginSettingsService pluginSettingsService,
        PromptVorlagenService promptVorlagenService,
        ILogger<SettingsViewModel> logger)
    {
        _einstellungService = einstellungService;
        _arbeitsverzeichnisService = arbeitsverzeichnisService;
        _darkModeService = darkModeService;
        _pluginManager = pluginManager;
        _pluginSettingsService = pluginSettingsService;
        _promptVorlagenService = promptVorlagenService;
        _logger = logger;

        _designMode = darkModeService.Current;
        _darkModeService.ModeChanged += OnDarkModeChanged;

        LadenCommand = new AsyncRelayCommand(LadenAsync);
        SpeichernCommand = new AsyncRelayCommand(SpeichernAsync);
        VerwerfenCommand = new AsyncRelayCommand(VerwerfenAsync);
        ScmPluginSelectedCommand = new RelayCommand<IGitPlugin>(plugin =>
        {
            if (plugin is not null)
                LoadScmPluginSettings(plugin);
        });
        KiPluginSelectedCommand = new RelayCommand<IKiPlugin>(plugin =>
        {
            if (plugin is not null)
                LoadKiPluginSettings(plugin);
        });
        PromptVorlageHinzufuegenCommand = new RelayCommand(PromptVorlageHinzufuegen);
        PromptVorlageLoeschenCommand = new RelayCommand<PromptVorlageEntry>(PromptVorlageLoeschen, entry => entry is not null);
    }

    private async Task LadenAsync(CancellationToken ct)
    {
        IsLoading = true;
        FehlerMeldung = null;

        try
        {
            _arbeitsverzeichnis = await _arbeitsverzeichnisService.GetArbeitsverzeichnisAsync(ct);
            OnPropertyChanged(nameof(Arbeitsverzeichnis));

            _defaultKiPlugin = await _einstellungService.GetSettingAsync(AppEinstellungService.DefaultKiPluginKey, ct);
            OnPropertyChanged(nameof(DefaultKiPlugin));

            _openVisualStudioCodeWhenNoSolutionFound =
                await _einstellungService.GetBoolSettingAsync(AppEinstellungService.OpenVisualStudioCodeWhenNoSolutionFoundKey, ct)
                ?? false;
            OnPropertyChanged(nameof(OpenVisualStudioCodeWhenNoSolutionFound));

            var savedMode = await _einstellungService.GetSettingAsync(AppEinstellungService.DesignModeKey, ct);
            _designMode = savedMode ?? _darkModeService.Current;
            OnPropertyChanged(nameof(DesignMode));

            ScmPlugins = _pluginManager.GetSourceCodeManagementPlugins();
            OnPropertyChanged(nameof(ScmPlugins));

            KiPlugins = _pluginManager.GetDevelopmentAutomationPlugins();
            OnPropertyChanged(nameof(KiPlugins));

            var savedScmPluginName = await _einstellungService.GetSettingAsync(AppEinstellungService.DefaultScmPluginKey, ct);
            _defaultScmPlugin = ScmPlugins.FirstOrDefault(p => p.PluginName == savedScmPluginName)
                ?? ScmPlugins.FirstOrDefault();
            OnPropertyChanged(nameof(DefaultScmPlugin));

            if (_defaultScmPlugin is not null)
                LoadScmPluginSettings(_defaultScmPlugin);

            var defaultKiPluginObj = KiPlugins.FirstOrDefault(p => p.PluginName == _defaultKiPlugin)
                ?? KiPlugins.FirstOrDefault();
            if (defaultKiPluginObj is not null)
                LoadKiPluginSettings(defaultKiPluginObj);

            await LadePromptVorlagenAsync(ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Laden der Einstellungen.");
            FehlerMeldung = $"Fehler beim Laden: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task SpeichernAsync(CancellationToken ct)
    {
        FehlerMeldung = null;
        ErfolgsMeldung = null;

        try
        {
            if (!ValidierePflichtfelder())
                return;

            SpeicherePluginEinstellungen(_defaultScmPlugin, _selectedScmPluginSettings);

            var defaultKiPluginObj = KiPlugins.FirstOrDefault(p => p.PluginName == _defaultKiPlugin);
            SpeicherePluginEinstellungen(defaultKiPluginObj, _selectedKiPluginSettings);

            await _arbeitsverzeichnisService.SaveArbeitsverzeichnisAsync(_arbeitsverzeichnis, ct);
            await _einstellungService.SetSettingAsync(AppEinstellungService.DefaultKiPluginKey, _defaultKiPlugin, ct);
            await _einstellungService.SetSettingAsync(AppEinstellungService.DefaultScmPluginKey, _defaultScmPlugin?.PluginName, ct);
            await _einstellungService.SetBoolSettingAsync(
                AppEinstellungService.OpenVisualStudioCodeWhenNoSolutionFoundKey,
                OpenVisualStudioCodeWhenNoSolutionFound,
                ct);
            await SpeicherePromptVorlagenAsync(ct);

            try
            {
                await _darkModeService.SetModeAsync(DesignMode, ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Design-Modus konnte nicht angewendet werden.");
            }

            ErfolgsMeldung = "Einstellungen gespeichert.";
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Speichern der Einstellungen.");
            FehlerMeldung = $"Fehler beim Speichern: {ex.Message}";
        }
    }

    private async Task VerwerfenAsync(CancellationToken ct)
    {
        await LadenAsync(ct);
    }

    private void LoadScmPluginSettings(IGitPlugin plugin)
    {
        SelectedScmPluginSettings = LadePluginEinstellungen(plugin);
    }

    private void LoadKiPluginSettings(IKiPlugin plugin)
    {
        SelectedKiPluginSettings = LadePluginEinstellungen(plugin);
    }

    private IReadOnlyList<PluginSettingGroupEntry> LadePluginEinstellungen(IPlugin plugin)
    {
        return plugin.GetSettingGroups()
            .Select(group => new PluginSettingGroupEntry(
                group.GroupName,
                group.Fields.Select(field => new PluginSettingEntry(
                    field,
                    _pluginSettingsService.GetValue(plugin, field),
                    useDefaultValue: !IsCodexCommandLineParameters(plugin, field)))
                .ToList()))
            .ToList();
    }

    private static bool IsCodexCommandLineParameters(IPlugin plugin, PluginSettingField field) =>
        string.Equals(plugin.PluginPrefix, CodexPluginPrefix, StringComparison.Ordinal)
        && string.Equals(field.Key, CommandLineParametersKey, StringComparison.Ordinal);

    private void SpeicherePluginEinstellungen(IPlugin? plugin, IReadOnlyList<PluginSettingGroupEntry> settings)
    {
        if (plugin is null)
            return;

        foreach (var group in settings)
        {
            foreach (var entry in group.Entries)
            {
                _pluginSettingsService.SetValue(plugin, entry.Field, entry.Value);
            }
        }
    }

    private bool ValidierePflichtfelder()
    {
        return ValidierePflichtfelderFuerSettings(_selectedScmPluginSettings)
            && ValidierePflichtfelderFuerSettings(_selectedKiPluginSettings)
            && ValidierePromptVorlagen();
    }

    private bool ValidierePflichtfelderFuerSettings(IReadOnlyList<PluginSettingGroupEntry> settings)
    {
        foreach (var group in settings)
        {
            foreach (var entry in group.Entries)
            {
                if (entry.Field.IsRequired && string.IsNullOrEmpty(entry.Value))
                {
                    FehlerMeldung = $"Pflichtfeld '{entry.Field.Label}' darf nicht leer sein.";
                    return false;
                }

                if (entry.Field.FieldType == PluginSettingFieldType.Integer
                    && !string.IsNullOrEmpty(entry.Value)
                    && !int.TryParse(entry.Value, out _))
                {
                    FehlerMeldung = $"Feld '{entry.Field.Label}' muss eine gültige Ganzzahl sein.";
                    return false;
                }

                if (entry.Field.FieldType == PluginSettingFieldType.Enum
                    && !string.IsNullOrEmpty(entry.Value)
                    && entry.Field.EnumOptions is not null
                    && !entry.Field.EnumOptions.Contains(entry.Value))
                {
                    FehlerMeldung = $"Feld '{entry.Field.Label}' enthält einen ungültigen Wert.";
                    return false;
                }
            }
        }

        return true;
    }

    private async Task LadePromptVorlagenAsync(CancellationToken ct)
    {
        var vorlagen = await _promptVorlagenService.GetAllAsync(ct);
        PromptVorlagen.Clear();
        _geloeschtePromptVorlagenIds.Clear();

        foreach (var vorlage in vorlagen)
        {
            PromptVorlagen.Add(new PromptVorlageEntry(
                vorlage.Id,
                vorlage.Name,
                vorlage.Prompttext,
                false));
        }
    }

    private async Task SpeicherePromptVorlagenAsync(CancellationToken ct)
    {
        foreach (var id in _geloeschtePromptVorlagenIds)
            await _promptVorlagenService.DeleteAsync(id, ct);

        foreach (var entry in PromptVorlagen)
        {
            if (entry.IsNeu)
            {
                await _promptVorlagenService.CreateAsync(entry.Name, entry.Prompttext, ct);
            }
            else
            {
                await _promptVorlagenService.UpdateAsync(entry.Id, entry.Name, entry.Prompttext, ct);
            }
        }

        _geloeschtePromptVorlagenIds.Clear();
        await LadePromptVorlagenAsync(ct);
    }

    private bool ValidierePromptVorlagen()
    {
        foreach (var vorlage in PromptVorlagen)
        {
            if (string.IsNullOrWhiteSpace(vorlage.Name))
            {
                FehlerMeldung = "Der Name einer Promptvorlage darf nicht leer sein.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(vorlage.Prompttext))
            {
                FehlerMeldung = $"Der Prompttext der Vorlage '{vorlage.Name}' darf nicht leer sein.";
                return false;
            }
        }

        return true;
    }

    private void PromptVorlageHinzufuegen()
    {
        PromptVorlagen.Add(new PromptVorlageEntry(Guid.NewGuid(), "Neue Promptvorlage", string.Empty, true));
    }

    private void PromptVorlageLoeschen(PromptVorlageEntry? entry)
    {
        if (entry is null)
            return;

        if (!entry.IsNeu)
            _geloeschtePromptVorlagenIds.Add(entry.Id);

        PromptVorlagen.Remove(entry);
    }

    private void OnDarkModeChanged(string mode) => DesignMode = mode;

    /// <inheritdoc/>
    public void Dispose()
    {
        _darkModeService.ModeChanged -= OnDarkModeChanged;
    }
}

/// <summary>Editierbarer Eintrag fuer eine Promptvorlage in den Einstellungen.</summary>
public sealed class PromptVorlageEntry : ViewModelBase
{
    private string _name;
    private string _prompttext;

    /// <inheritdoc cref="PromptVorlageEntry"/>
    public PromptVorlageEntry(Guid id, string name, string prompttext, bool isNeu)
    {
        Id = id;
        _name = name;
        _prompttext = prompttext;
        IsNeu = isNeu;
    }

    /// <summary>ID der Promptvorlage.</summary>
    public Guid Id { get; }

    /// <summary>Gibt an, ob der Eintrag noch nicht persistiert ist.</summary>
    public bool IsNeu { get; }

    /// <summary>Anzeigename.</summary>
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    /// <summary>Prompttext.</summary>
    public string Prompttext
    {
        get => _prompttext;
        set => SetProperty(ref _prompttext, value);
    }
}
