using Microsoft.Extensions.Logging;
using Softwareschmiede.App.Services;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Enums;
using System.Windows.Input;
using Windows.ApplicationModel;

namespace Softwareschmiede.App.ViewModels;

/// <summary>ViewModel für die Einstellungsseite.</summary>
public sealed class SettingsViewModel : ViewModelBase
{
    private readonly AppEinstellungService _einstellungService;
    private readonly ArbeitsverzeichnisSettingsService _arbeitsverzeichnisService;
    private readonly DarkModeService _darkModeService;
    private readonly ILogger<SettingsViewModel> _logger;

    private string? _arbeitsverzeichnis;
    private string _designMode;
    private string? _defaultKiPlugin;
    private BenachrichtigungsModus _benachrichtigungsModus;
    private bool _isLoading;
    private string? _fehlerMeldung;
    private string? _erfolgsMeldung;

    /// <summary>Arbeitsverzeichnis für Repository-Klone.</summary>
    public string? Arbeitsverzeichnis
    {
        get => _arbeitsverzeichnis;
        set => SetProperty(ref _arbeitsverzeichnis, value);
    }

    public string DesignMode
    {
        get => _designMode;
        set => SetProperty(ref _designMode, value);
    }
    public IEnumerable<string> DesignModes => _darkModeService.GetAvailableModes();

    /// <summary>Standard-KI-Plugin-Prefix.</summary>
    public string? DefaultKiPlugin
    {
        get => _defaultKiPlugin;
        set => SetProperty(ref _defaultKiPlugin, value);
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

    /// <summary>Lädt alle Einstellungen.</summary>
    public ICommand LadenCommand { get; }

    /// <summary>Speichert alle Einstellungen.</summary>
    public ICommand SpeichernCommand { get; }
    
    /// <summary>Verwirft alle nicht gespeicherten Einstellungen.</summary>
    public ICommand VerwerfenCommand { get; }

    /// <inheritdoc cref="SettingsViewModel"/>
    public SettingsViewModel(
        AppEinstellungService einstellungService,
        ArbeitsverzeichnisSettingsService arbeitsverzeichnisService,
        DarkModeService darkModeService,
        ILogger<SettingsViewModel> logger)
    {
        _einstellungService = einstellungService;
        _arbeitsverzeichnisService = arbeitsverzeichnisService;
        _darkModeService = darkModeService;
        _logger = logger;

        _designMode = darkModeService.Current;
        _darkModeService.ModeChanged += mode => DesignMode = mode;

        LadenCommand = new AsyncRelayCommand(LadenAsync);
        SpeichernCommand = new AsyncRelayCommand(SpeichernAsync);
        VerwerfenCommand = new AsyncRelayCommand(VerwerfenAsync);
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
            await _arbeitsverzeichnisService.SaveArbeitsverzeichnisAsync(_arbeitsverzeichnis, ct);
            await _einstellungService.SetSettingAsync(AppEinstellungService.DefaultKiPluginKey, _defaultKiPlugin, ct);
            await _darkModeService.SetModeAsync(DesignMode, ct);
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
}
