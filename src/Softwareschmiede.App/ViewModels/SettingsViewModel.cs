using System.Windows.Input;
using Microsoft.Extensions.Logging;
using Softwareschmiede.App.Services;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.App.ViewModels;

/// <summary>ViewModel für die Einstellungsseite.</summary>
public sealed class SettingsViewModel : ViewModelBase
{
    private readonly AppEinstellungService _einstellungService;
    private readonly ArbeitsverzeichnisSettingsService _arbeitsverzeichnisService;
    private readonly DarkModeService _darkModeService;
    private readonly ILogger<SettingsViewModel> _logger;

    private string? _arbeitsverzeichnis;
    private bool _isDarkMode;
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

    /// <summary>Gibt an, ob Dark Mode aktiviert ist (read-only; Toggle über MainWindowViewModel.ToggleDarkModeCommand).</summary>
    public bool IsDarkMode
    {
        get => _isDarkMode;
        private set => SetProperty(ref _isDarkMode, value);
    }

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

        _isDarkMode = _darkModeService.IsDarkMode;
        _darkModeService.DarkModeChanged += enabled => IsDarkMode = enabled;

        LadenCommand = new AsyncRelayCommand(LadenAsync);
        SpeichernCommand = new AsyncRelayCommand(SpeichernAsync);
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
}
