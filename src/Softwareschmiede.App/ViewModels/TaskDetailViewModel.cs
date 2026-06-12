using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;

namespace Softwareschmiede.App.ViewModels;

/// <summary>
/// ViewModel für die Aufgabendetailansicht.
/// Verwaltet Status, Protokoll, CLI-Prozessstart und Fenstereinbettung.
/// </summary>
public sealed class TaskDetailViewModel : ViewModelBase, IDisposable
{
    private readonly AufgabeService _aufgabeService;
    private readonly ProtokollService _protokollService;
    private readonly KiAusfuehrungsService _kiService;
    private readonly EntwicklungsprozessService _entwicklungsprozessService;
    private readonly PluginSelectionService _pluginSelectionService;
    private readonly ILogger<TaskDetailViewModel> _logger;

    private Guid _aufgabeId;
    private Aufgabe? _aufgabe;
    private bool _isLoading;
    private string? _fehlerMeldung;
    private bool _isCliRunning;
    private string? _selectedKiPluginPrefix;
    private string? _optionalCliParameters;
    private IntPtr _embeddedWindowHandle = IntPtr.Zero;
    private CancellationTokenSource? _ladenCts;

    /// <summary>Die ID der angezeigten Aufgabe.</summary>
    public Guid AufgabeId
    {
        get => _aufgabeId;
        set
        {
            if (SetProperty(ref _aufgabeId, value))
            {
                _ladenCts?.Cancel();
                _ladenCts?.Dispose();
                _ladenCts = new CancellationTokenSource();
                _ = LadenAsync(_ladenCts.Token);
            }
        }
    }

    /// <summary>Die geladene Aufgabe.</summary>
    public Aufgabe? Aufgabe
    {
        get => _aufgabe;
        private set
        {
            SetProperty(ref _aufgabe, value);
            OnPropertyChanged(nameof(AufgabeTitel));
            OnPropertyChanged(nameof(AufgabeStatus));
            OnPropertyChanged(nameof(KannCliStarten));
            OnPropertyChanged(nameof(KannCliStoppen));
        }
    }

    /// <summary>Titel der Aufgabe.</summary>
    public string AufgabeTitel => _aufgabe?.Titel ?? "(wird geladen…)";

    /// <summary>Status der Aufgabe.</summary>
    public AufgabeStatus AufgabeStatus => _aufgabe?.Status ?? Domain.Enums.AufgabeStatus.Neu;

    /// <summary>Gibt an, ob Daten geladen werden.</summary>
    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    /// <summary>Fehlermeldung bei Fehlern.</summary>
    public string? FehlerMeldung
    {
        get => _fehlerMeldung;
        private set => SetProperty(ref _fehlerMeldung, value);
    }

    /// <summary>Gibt an, ob ein CLI-Prozess läuft.</summary>
    public bool IsCliRunning
    {
        get => _isCliRunning;
        private set
        {
            SetProperty(ref _isCliRunning, value);
            OnPropertyChanged(nameof(KannCliStarten));
            OnPropertyChanged(nameof(KannCliStoppen));
        }
    }

    /// <summary>Gibt an, ob ein CLI-Prozess gestartet werden kann.</summary>
    public bool KannCliStarten => !_isCliRunning
        && _aufgabe?.Status is Domain.Enums.AufgabeStatus.Gestartet or Domain.Enums.AufgabeStatus.Wartend
        && !string.IsNullOrEmpty(_selectedKiPluginPrefix);

    /// <summary>Gibt an, ob der laufende CLI-Prozess gestoppt werden kann.</summary>
    public bool KannCliStoppen => _isCliRunning;

    /// <summary>Gewähltes KI-Plugin (Prefix).</summary>
    public string? SelectedKiPluginPrefix
    {
        get => _selectedKiPluginPrefix;
        set
        {
            SetProperty(ref _selectedKiPluginPrefix, value);
            OnPropertyChanged(nameof(KannCliStarten));
        }
    }

    /// <summary>Optionale Parameter für den CLI-Start.</summary>
    public string? OptionalCliParameters
    {
        get => _optionalCliParameters;
        set => SetProperty(ref _optionalCliParameters, value);
    }

    /// <summary>Handle des eingebetteten CLI-Fensters (für ProcessWindowHost).</summary>
    public IntPtr EmbeddedWindowHandle
    {
        get => _embeddedWindowHandle;
        set => SetProperty(ref _embeddedWindowHandle, value);
    }

    /// <summary>Protokolleinträge der Aufgabe.</summary>
    public ObservableCollection<Protokolleintrag> Protokolleintraege { get; } = new();

    /// <summary>Verfügbare KI-Plugin-Prefixe.</summary>
    public ObservableCollection<string> VerfuegbareKiPlugins { get; } = new();

    /// <summary>Lädt die Aufgabe.</summary>
    public ICommand LadenCommand { get; }

    /// <summary>Startet den CLI-Prozess.</summary>
    public ICommand CliStartenCommand { get; }

    /// <summary>Stoppt den CLI-Prozess.</summary>
    public ICommand CliStoppenCommand { get; }

    /// <summary>Setzt den Status auf "Gestartet".</summary>
    public ICommand StatusGestartetSetzenCommand { get; }

    /// <summary>Schließt die Aufgabe ab (Status: Beendet).</summary>
    public ICommand AufgabeAbschliessenCommand { get; }

    /// <summary>Event: Ein CLI-Prozess wurde gestartet, Handle ist verfügbar.</summary>
    public event Action<System.Diagnostics.Process>? CliProzessGestartet;

    /// <inheritdoc cref="TaskDetailViewModel"/>
    public TaskDetailViewModel(
        AufgabeService aufgabeService,
        ProtokollService protokollService,
        KiAusfuehrungsService kiService,
        EntwicklungsprozessService entwicklungsprozessService,
        PluginSelectionService pluginSelectionService,
        ILogger<TaskDetailViewModel> logger)
    {
        _aufgabeService = aufgabeService;
        _protokollService = protokollService;
        _kiService = kiService;
        _entwicklungsprozessService = entwicklungsprozessService;
        _pluginSelectionService = pluginSelectionService;
        _logger = logger;

        _kiService.CliProcessStatusChanged += OnCliProcessStatusChanged;

        LadenCommand = new AsyncRelayCommand(ct => LadenAsync(ct));
        CliStartenCommand = new AsyncRelayCommand(CliStartenAsync, () => KannCliStarten);
        CliStoppenCommand = new AsyncRelayCommand(CliStoppenAsync, () => KannCliStoppen);
        StatusGestartetSetzenCommand = new AsyncRelayCommand(StatusGestartetSetzenAsync);
        AufgabeAbschliessenCommand = new AsyncRelayCommand(AufgabeAbschliessenAsync);
    }

    private async Task LadenAsync(CancellationToken ct)
    {
        if (_aufgabeId == Guid.Empty)
            return;

        IsLoading = true;
        FehlerMeldung = null;

        try
        {
            Aufgabe = await _aufgabeService.GetDetailAsync(_aufgabeId, ct);
            IsCliRunning = _kiService.IsRunning(_aufgabeId);

            var protokolleintraege = await _protokollService.GetByAufgabeAsync(_aufgabeId, ct);
            Protokolleintraege.Clear();
            foreach (var eintrag in protokolleintraege)
                Protokolleintraege.Add(eintrag);

            await LadeVerfuegbarePluginsAsync(ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Laden der Aufgabe {AufgabeId}.", _aufgabeId);
            FehlerMeldung = $"Fehler: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LadeVerfuegbarePluginsAsync(CancellationToken ct)
    {
        try
        {
            var pluginNames = await _pluginSelectionService.GetAvailableKiPluginPrefixesAsync(ct);
            VerfuegbareKiPlugins.Clear();
            foreach (var name in pluginNames)
                VerfuegbareKiPlugins.Add(name);

            if (VerfuegbareKiPlugins.Count > 0 && _selectedKiPluginPrefix is null)
                SelectedKiPluginPrefix = VerfuegbareKiPlugins[0];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "KI-Plugin-Liste konnte nicht geladen werden.");
        }
    }

    private async Task CliStartenAsync(CancellationToken ct)
    {
        if (_aufgabeId == Guid.Empty || string.IsNullOrEmpty(_selectedKiPluginPrefix))
            return;

        FehlerMeldung = null;

        try
        {
            var kiPlugin = await _pluginSelectionService.ResolveDevelopmentAutomationPluginAsync(_selectedKiPluginPrefix, ct);
            var lokalerKlonPfad = _aufgabe?.LokalerKlonPfad ?? string.Empty;

            var handle = await _kiService.StartCliAsync(
                _aufgabeId,
                kiPlugin,
                lokalerKlonPfad,
                _optionalCliParameters,
                ct);

            IsCliRunning = true;
            CliProzessGestartet?.Invoke(handle.Process);

            if (_aufgabe?.Status == Domain.Enums.AufgabeStatus.Gestartet)
            {
                await _aufgabeService.SetStatusAsync(_aufgabeId, Domain.Enums.AufgabeStatus.InArbeit, ct);
                Aufgabe = await _aufgabeService.GetByIdAsync(_aufgabeId, ct);
            }

            _logger.LogInformation("CLI für Aufgabe {AufgabeId} gestartet.", _aufgabeId);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Starten des CLI für Aufgabe {AufgabeId}.", _aufgabeId);
            FehlerMeldung = $"CLI-Startfehler: {ex.Message}";
        }
    }

    private async Task CliStoppenAsync(CancellationToken ct)
    {
        if (_aufgabeId == Guid.Empty)
            return;

        FehlerMeldung = null;

        try
        {
            await _kiService.StopCliAsync(_aufgabeId, ct);
            IsCliRunning = false;
            EmbeddedWindowHandle = IntPtr.Zero;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Stoppen des CLI für Aufgabe {AufgabeId}.", _aufgabeId);
            FehlerMeldung = $"CLI-Stoppfehler: {ex.Message}";
        }
    }

    private async Task StatusGestartetSetzenAsync(CancellationToken ct)
    {
        if (_aufgabeId == Guid.Empty)
            return;

        try
        {
            await _aufgabeService.SetStatusAsync(_aufgabeId, Domain.Enums.AufgabeStatus.Gestartet, ct);
            Aufgabe = await _aufgabeService.GetByIdAsync(_aufgabeId, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Setzen des Status für Aufgabe {AufgabeId}.", _aufgabeId);
            FehlerMeldung = $"Fehler: {ex.Message}";
        }
    }

    private async Task AufgabeAbschliessenAsync(CancellationToken ct)
    {
        if (_aufgabeId == Guid.Empty)
            return;

        try
        {
            await _entwicklungsprozessService.AbschliessenAsync(_aufgabeId, ct);
            Aufgabe = await _aufgabeService.GetByIdAsync(_aufgabeId, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Abschließen der Aufgabe {AufgabeId}.", _aufgabeId);
            FehlerMeldung = $"Fehler: {ex.Message}";
        }
    }

    private void OnCliProcessStatusChanged(Guid aufgabeId, CliProcessStatus status)
    {
        if (aufgabeId != _aufgabeId)
            return;

        IsCliRunning = status == CliProcessStatus.Gestartet;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _kiService.CliProcessStatusChanged -= OnCliProcessStatusChanged;
        _ladenCts?.Cancel();
        _ladenCts?.Dispose();
        _ladenCts = null;
    }
}
