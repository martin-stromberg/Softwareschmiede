using System.Globalization;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using Softwareschmiede.App.Services;
using Softwareschmiede.Application.Services;

namespace Softwareschmiede.App.ViewModels;

/// <summary>
/// Kapselt den Auto-Shutdown-Block der Seitenleisten-Fußzeile des Hauptfensters:
/// Session-Aktivierung und persistierte Nebenwerte (Erzwingen/Toleranz inkl. Validierung),
/// Weitergabe an den <see cref="IAutoShutdownOrchestrator"/>, einmaliges Laden der
/// Settings ohne Write-Back sowie die sekündliche Countdown-Anzeige. Das
/// <see cref="MainWindowViewModel"/> reicht die gebundenen Properties durch und kombiniert
/// die Blocksichtbarkeit zusätzlich mit der fachlichen Lauf-Anzahl — analog zum
/// <see cref="MainWindowUpdateFlow"/>-Muster.
/// </summary>
public sealed class MainWindowAutoShutdownFlow : ViewModelBase, IDisposable
{
    private readonly IAutoShutdownOrchestrator? _orchestrator;
    private readonly AutoShutdownSettingsService? _settingsService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _logger;
    private readonly Action<Action> _dispatcherInvoke;
    private readonly DispatcherTimer _countdownTimer;

    private bool _autoShutdownEnabled;
    private bool _autoShutdownForce;
    private string _autoShutdownToleranceMinutes =
        AppSettingService.AutoShutdownDefaultToleranceMinutes.ToString(CultureInfo.InvariantCulture);
    private int _lastValidAutoShutdownToleranceMinutes = AppSettingService.AutoShutdownDefaultToleranceMinutes;
    private string? _autoShutdownToleranceError;
    private string? _autoShutdownCountdownText;
    private DateTimeOffset? _autoShutdownCountdownEndUtc;
    private bool _autoShutdownSettingsLoaded;
    private bool _autoShutdownSettingsFromUserChanged;

    /// <inheritdoc cref="MainWindowAutoShutdownFlow"/>
    /// <param name="orchestrator">Optionaler Orchestrator des automatischen OS-Shutdowns; <c>null</c> deaktiviert die Weitergabe.</param>
    /// <param name="settingsService">Optionaler Persistenz-Service der Nebenwerte; <c>null</c> deaktiviert Laden und Speichern.</param>
    /// <param name="timeProvider">Zeitgeber für den Countdown-Text (für Tests injizierbar).</param>
    /// <param name="logger">Logger für die Fire-and-Forget-Kontexte.</param>
    /// <param name="dispatcherInvoke">Abstraktion des UI-Dispatchers (Tests: synchron).</param>
    public MainWindowAutoShutdownFlow(
        IAutoShutdownOrchestrator? orchestrator,
        AutoShutdownSettingsService? settingsService,
        TimeProvider timeProvider,
        ILogger logger,
        Action<Action> dispatcherInvoke)
    {
        _orchestrator = orchestrator;
        _settingsService = settingsService;
        _timeProvider = timeProvider;
        _logger = logger;
        _dispatcherInvoke = dispatcherInvoke;

        _countdownTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _countdownTimer.Tick += OnCountdownTick;

        if (_orchestrator is not null)
        {
            _orchestrator.StateChanged += OnStateChanged;
        }
    }

    /// <summary>
    /// Schalter „Nach Leerlauf PC herunterfahren" (nur Session-Aktivierung, wird nicht persistiert):
    /// aktiviert/disarmiert den <see cref="IAutoShutdownOrchestrator"/>.
    /// </summary>
    public bool AutoShutdownEnabled
    {
        get => _autoShutdownEnabled;
        set
        {
            if (SetProperty(ref _autoShutdownEnabled, value))
            {
                _orchestrator?.SetEnabled(value);
                OnPropertyChanged(nameof(AutoShutdownOptionsVisible));
            }
        }
    }

    /// <summary>Schalter „Herunterfahren erzwingen" (persistiert).</summary>
    public bool ForceAutoShutdown
    {
        get => _autoShutdownForce;
        set
        {
            if (SetProperty(ref _autoShutdownForce, value))
            {
                _autoShutdownSettingsFromUserChanged = true;
                _settingsService?.SaveForceAsync(value, CancellationToken.None)
                    .SafeFireAndForget(_logger, "Auto-Shutdown-Erzwingen speichern");
                // Unbedingt weiterreichen — steht gerade ein ungültiger Wert im Toleranzfeld,
                // gilt die zuletzt gültige Toleranz weiter.
                _orchestrator?.SetOptions(_lastValidAutoShutdownToleranceMinutes, value);
            }
        }
    }

    /// <summary>
    /// Toleranzzeit des Auto-Shutdown-Countdowns in Minuten als Texteingabe (persistiert,
    /// gültig: ganzzahlig 0–<see cref="AppSettingService.AutoShutdownMaxToleranceMinutes"/>).
    /// </summary>
    public string AutoShutdownToleranceMinutes
    {
        get => _autoShutdownToleranceMinutes;
        set
        {
            if (SetProperty(ref _autoShutdownToleranceMinutes, value))
            {
                _autoShutdownSettingsFromUserChanged = true;
                if (TryParseToleranceMinutes(out var toleranceMinutes))
                {
                    AutoShutdownToleranceError = null;
                    _lastValidAutoShutdownToleranceMinutes = toleranceMinutes;
                    _settingsService?.SaveToleranceMinutesAsync(toleranceMinutes, CancellationToken.None)
                        .SafeFireAndForget(_logger, "Auto-Shutdown-Toleranz speichern");
                    _orchestrator?.SetOptions(toleranceMinutes, _autoShutdownForce);
                }
                else
                {
                    AutoShutdownToleranceError =
                        $"Wartezeit muss eine ganze Zahl zwischen 0 und {AppSettingService.AutoShutdownMaxToleranceMinutes} Minuten sein.";
                }
            }
        }
    }

    /// <summary>Validierungsfehler der Toleranz-Eingabe; <c>null</c> bei gültigem Wert.</summary>
    public string? AutoShutdownToleranceError
    {
        get => _autoShutdownToleranceError;
        private set => SetProperty(ref _autoShutdownToleranceError, value);
    }

    /// <summary>Angezeigter Countdown-Text („Herunterfahren in 5 Min. 0 Sek."); <c>null</c>, wenn kein Countdown armiert ist.</summary>
    public string? AutoShutdownCountdownText
    {
        get => _autoShutdownCountdownText;
        private set => SetProperty(ref _autoShutdownCountdownText, value);
    }

    /// <summary>Sichtbarkeit der Auto-Shutdown-Optionen (Toleranz, Erzwingen) — nur bei aktiviertem Schalter.</summary>
    public bool AutoShutdownOptionsVisible => _autoShutdownEnabled;

    /// <summary>Ob aktuell ein Auto-Shutdown-Countdown armiert ist.</summary>
    public bool AutoShutdownCountdownActive => _autoShutdownCountdownEndUtc is not null;

    /// <summary>
    /// Lädt die persistierten Auto-Shutdown-Nebenwerte (Erzwingen, Toleranz) einmalig und
    /// schiebt sie ohne Write-Back in den Orchestrator — die Aktivierung selbst bleibt
    /// absichtlich ausgeschaltet (Session-Schalter). Ein zwischenzeitlich vom Anwender
    /// geänderter Wert wird nicht überschrieben.
    /// </summary>
    /// <param name="ct">Token zum Abbrechen der Operation.</param>
    public async Task LoadSettingsAsync(CancellationToken ct = default)
    {
        if (_settingsService is null || _autoShutdownSettingsLoaded)
            return;

        (bool Force, int ToleranceMinutes) settings;
        try
        {
            settings = await _settingsService.LoadAsync(ct);
            // Erst nach erfolgreichem Laden verbrauchen — ein Fehlschlag darf den
            // Einmal-Guard nicht konsumieren, sonst ist kein erneutes Laden möglich.
            _autoShutdownSettingsLoaded = true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Auto-Shutdown--Einstellungen konnten nicht geladen werden.");
            return;
        }

        _dispatcherInvoke(() =>
        {
            if (_autoShutdownSettingsFromUserChanged)
                return;

            _autoShutdownForce = settings.Force;
            OnPropertyChanged(nameof(ForceAutoShutdown));
            _autoShutdownToleranceMinutes = settings.ToleranceMinutes.ToString(CultureInfo.InvariantCulture);
            OnPropertyChanged(nameof(AutoShutdownToleranceMinutes));
            _lastValidAutoShutdownToleranceMinutes = settings.ToleranceMinutes;
            AutoShutdownToleranceError = null;
            _orchestrator?.SetOptions(settings.ToleranceMinutes, settings.Force);
        });
    }

    private bool TryParseToleranceMinutes(out int toleranceMinutes)
    {
        var valid = int.TryParse(
                _autoShutdownToleranceMinutes,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out toleranceMinutes)
            && toleranceMinutes is >= 0 and <= AppSettingService.AutoShutdownMaxToleranceMinutes;
        if (!valid)
        {
            toleranceMinutes = 0;
        }

        return valid;
    }

    private void OnStateChanged()
        => _dispatcherInvoke(RefreshCountdownDisplay);

    private void OnCountdownTick(object? sender, EventArgs e)
    {
        if (_autoShutdownCountdownEndUtc is not null)
        {
            AutoShutdownCountdownText = FormatCountdownText();
        }
    }

    private void RefreshCountdownDisplay()
    {
        _autoShutdownCountdownEndUtc = _orchestrator?.CountdownEndUtc;

        if (_autoShutdownCountdownEndUtc is null)
        {
            _countdownTimer.Stop();
            AutoShutdownCountdownText = null;
        }
        else
        {
            AutoShutdownCountdownText = FormatCountdownText();
            _countdownTimer.Start();
        }

        OnPropertyChanged(nameof(AutoShutdownCountdownActive));
    }

    private string FormatCountdownText()
    {
        var remainder = _autoShutdownCountdownEndUtc!.Value - _timeProvider.GetUtcNow();
        if (remainder < TimeSpan.Zero)
        {
            remainder = TimeSpan.Zero;
        }

        // Mit Einheit und Stunden-Umrechnung: „05:00" ist für Laien mehrdeutig und Werte
        // über 60 Minuten (Toleranz bis 1440) wären im mm:ss-Format unüblich („120:00").
        var totalSeconds = (int)remainder.TotalSeconds;
        var hours = totalSeconds / 3600;
        var minutes = (totalSeconds % 3600) / 60;
        var seconds = totalSeconds % 60;

        var timeText = hours > 0
            ? $"{hours} Std. {minutes} Min."
            : minutes > 0
                ? $"{minutes} Min. {seconds} Sek."
                : $"{seconds} Sek.";

        return $"Herunterfahren in {timeText}";
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _countdownTimer.Stop();
        _countdownTimer.Tick -= OnCountdownTick;
        if (_orchestrator is not null)
        {
            _orchestrator.StateChanged -= OnStateChanged;
        }
    }
}
