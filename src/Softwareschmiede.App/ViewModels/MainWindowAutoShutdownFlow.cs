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
/// Einstellungen ohne Write-Back sowie die sekündliche Countdown-Anzeige. Das
/// <see cref="MainWindowViewModel"/> reicht die gebundenen Properties durch und kombiniert
/// die Blocksichtbarkeit zusätzlich mit der fachlichen Lauf-Anzahl — analog zum
/// <see cref="MainWindowUpdateFlow"/>-Muster.
/// </summary>
public sealed class MainWindowAutoShutdownFlow : ViewModelBase, IDisposable
{
    private readonly IAutoShutdownOrchestrator? _orchestrator;
    private readonly AutoShutdownEinstellungenService? _einstellungenService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _logger;
    private readonly Action<Action> _dispatcherInvoke;
    private readonly DispatcherTimer _countdownTimer;

    private bool _autoShutdownAktiviert;
    private bool _autoShutdownErzwingen;
    private string _autoShutdownToleranzMinuten =
        AppEinstellungService.AutoShutdownDefaultToleranzMinuten.ToString(CultureInfo.InvariantCulture);
    private int _letzteGueltigeAutoShutdownToleranzMinuten = AppEinstellungService.AutoShutdownDefaultToleranzMinuten;
    private string? _autoShutdownToleranzFehler;
    private string? _autoShutdownCountdownText;
    private DateTimeOffset? _autoShutdownCountdownEndeUtc;
    private bool _autoShutdownEinstellungenGeladen;
    private bool _autoShutdownEinstellungenVomBenutzerGeaendert;

    /// <inheritdoc cref="MainWindowAutoShutdownFlow"/>
    /// <param name="orchestrator">Optionaler Orchestrator des automatischen OS-Shutdowns; <c>null</c> deaktiviert die Weitergabe.</param>
    /// <param name="einstellungenService">Optionaler Persistenz-Service der Nebenwerte; <c>null</c> deaktiviert Laden und Speichern.</param>
    /// <param name="timeProvider">Zeitgeber für den Countdown-Text (für Tests injizierbar).</param>
    /// <param name="logger">Logger für die Fire-and-Forget-Kontexte.</param>
    /// <param name="dispatcherInvoke">Abstraktion des UI-Dispatchers (Tests: synchron).</param>
    public MainWindowAutoShutdownFlow(
        IAutoShutdownOrchestrator? orchestrator,
        AutoShutdownEinstellungenService? einstellungenService,
        TimeProvider timeProvider,
        ILogger logger,
        Action<Action> dispatcherInvoke)
    {
        _orchestrator = orchestrator;
        _einstellungenService = einstellungenService;
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
            _orchestrator.ZustandGeaendert += OnZustandGeaendert;
        }
    }

    /// <summary>
    /// Schalter „Nach Leerlauf PC herunterfahren" (nur Session-Aktivierung, wird nicht persistiert):
    /// aktiviert/disarmiert den <see cref="IAutoShutdownOrchestrator"/>.
    /// </summary>
    public bool AutoShutdownAktiviert
    {
        get => _autoShutdownAktiviert;
        set
        {
            if (SetProperty(ref _autoShutdownAktiviert, value))
            {
                _orchestrator?.SetEnabled(value);
                OnPropertyChanged(nameof(AutoShutdownOptionenSichtbar));
            }
        }
    }

    /// <summary>Schalter „Herunterfahren erzwingen" (persistiert).</summary>
    public bool AutoShutdownErzwingen
    {
        get => _autoShutdownErzwingen;
        set
        {
            if (SetProperty(ref _autoShutdownErzwingen, value))
            {
                _autoShutdownEinstellungenVomBenutzerGeaendert = true;
                _einstellungenService?.SpeichereErzwingenAsync(value, CancellationToken.None)
                    .SafeFireAndForget(_logger, "Auto-Shutdown-Erzwingen speichern");
                // Unbedingt weiterreichen — steht gerade ein ungültiger Wert im Toleranzfeld,
                // gilt die zuletzt gültige Toleranz weiter.
                _orchestrator?.SetOptionen(_letzteGueltigeAutoShutdownToleranzMinuten, value);
            }
        }
    }

    /// <summary>
    /// Toleranzzeit des Auto-Shutdown-Countdowns in Minuten als Texteingabe (persistiert,
    /// gültig: ganzzahlig 0–<see cref="AppEinstellungService.AutoShutdownMaxToleranzMinuten"/>).
    /// </summary>
    public string AutoShutdownToleranzMinuten
    {
        get => _autoShutdownToleranzMinuten;
        set
        {
            if (SetProperty(ref _autoShutdownToleranzMinuten, value))
            {
                _autoShutdownEinstellungenVomBenutzerGeaendert = true;
                if (TryParseToleranzMinuten(out var toleranzMinuten))
                {
                    AutoShutdownToleranzFehler = null;
                    _letzteGueltigeAutoShutdownToleranzMinuten = toleranzMinuten;
                    _einstellungenService?.SpeichereToleranzMinutenAsync(toleranzMinuten, CancellationToken.None)
                        .SafeFireAndForget(_logger, "Auto-Shutdown-Toleranz speichern");
                    _orchestrator?.SetOptionen(toleranzMinuten, _autoShutdownErzwingen);
                }
                else
                {
                    AutoShutdownToleranzFehler =
                        $"Wartezeit muss eine ganze Zahl zwischen 0 und {AppEinstellungService.AutoShutdownMaxToleranzMinuten} Minuten sein.";
                }
            }
        }
    }

    /// <summary>Validierungsfehler der Toleranz-Eingabe; <c>null</c> bei gültigem Wert.</summary>
    public string? AutoShutdownToleranzFehler
    {
        get => _autoShutdownToleranzFehler;
        private set => SetProperty(ref _autoShutdownToleranzFehler, value);
    }

    /// <summary>Angezeigter Countdown-Text („Herunterfahren in 5 Min. 0 Sek."); <c>null</c>, wenn kein Countdown armiert ist.</summary>
    public string? AutoShutdownCountdownText
    {
        get => _autoShutdownCountdownText;
        private set => SetProperty(ref _autoShutdownCountdownText, value);
    }

    /// <summary>Sichtbarkeit der Auto-Shutdown-Optionen (Toleranz, Erzwingen) — nur bei aktiviertem Schalter.</summary>
    public bool AutoShutdownOptionenSichtbar => _autoShutdownAktiviert;

    /// <summary>Ob aktuell ein Auto-Shutdown-Countdown armiert ist.</summary>
    public bool AutoShutdownCountdownAktiv => _autoShutdownCountdownEndeUtc is not null;

    /// <summary>
    /// Lädt die persistierten Auto-Shutdown-Nebenwerte (Erzwingen, Toleranz) einmalig und
    /// schiebt sie ohne Write-Back in den Orchestrator — die Aktivierung selbst bleibt
    /// absichtlich ausgeschaltet (Session-Schalter). Ein zwischenzeitlich vom Anwender
    /// geänderter Wert wird nicht überschrieben.
    /// </summary>
    /// <param name="ct">Token zum Abbrechen der Operation.</param>
    public async Task EinstellungenLadenAsync(CancellationToken ct = default)
    {
        if (_einstellungenService is null || _autoShutdownEinstellungenGeladen)
            return;

        (bool Erzwingen, int ToleranzMinuten) einstellungen;
        try
        {
            einstellungen = await _einstellungenService.LadeAsync(ct);
            // Erst nach erfolgreichem Laden verbrauchen — ein Fehlschlag darf den
            // Einmal-Guard nicht konsumieren, sonst ist kein erneutes Laden möglich.
            _autoShutdownEinstellungenGeladen = true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Auto-Shutdown-Einstellungen konnten nicht geladen werden.");
            return;
        }

        _dispatcherInvoke(() =>
        {
            if (_autoShutdownEinstellungenVomBenutzerGeaendert)
                return;

            _autoShutdownErzwingen = einstellungen.Erzwingen;
            OnPropertyChanged(nameof(AutoShutdownErzwingen));
            _autoShutdownToleranzMinuten = einstellungen.ToleranzMinuten.ToString(CultureInfo.InvariantCulture);
            OnPropertyChanged(nameof(AutoShutdownToleranzMinuten));
            _letzteGueltigeAutoShutdownToleranzMinuten = einstellungen.ToleranzMinuten;
            AutoShutdownToleranzFehler = null;
            _orchestrator?.SetOptionen(einstellungen.ToleranzMinuten, einstellungen.Erzwingen);
        });
    }

    private bool TryParseToleranzMinuten(out int toleranzMinuten)
    {
        var gueltig = int.TryParse(
                _autoShutdownToleranzMinuten,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out toleranzMinuten)
            && toleranzMinuten is >= 0 and <= AppEinstellungService.AutoShutdownMaxToleranzMinuten;
        if (!gueltig)
        {
            toleranzMinuten = 0;
        }

        return gueltig;
    }

    private void OnZustandGeaendert()
        => _dispatcherInvoke(CountdownAnzeigeAktualisieren);

    private void OnCountdownTick(object? sender, EventArgs e)
    {
        if (_autoShutdownCountdownEndeUtc is not null)
        {
            AutoShutdownCountdownText = FormatCountdownText();
        }
    }

    private void CountdownAnzeigeAktualisieren()
    {
        _autoShutdownCountdownEndeUtc = _orchestrator?.CountdownEndeUtc;

        if (_autoShutdownCountdownEndeUtc is null)
        {
            _countdownTimer.Stop();
            AutoShutdownCountdownText = null;
        }
        else
        {
            AutoShutdownCountdownText = FormatCountdownText();
            _countdownTimer.Start();
        }

        OnPropertyChanged(nameof(AutoShutdownCountdownAktiv));
    }

    private string FormatCountdownText()
    {
        var rest = _autoShutdownCountdownEndeUtc!.Value - _timeProvider.GetUtcNow();
        if (rest < TimeSpan.Zero)
        {
            rest = TimeSpan.Zero;
        }

        // Mit Einheit und Stunden-Umrechnung: „05:00" ist für Laien mehrdeutig und Werte
        // über 60 Minuten (Toleranz bis 1440) wären im mm:ss-Format unüblich („120:00").
        var gesamtSekunden = (int)rest.TotalSeconds;
        var stunden = gesamtSekunden / 3600;
        var minuten = (gesamtSekunden % 3600) / 60;
        var sekunden = gesamtSekunden % 60;

        var zeitText = stunden > 0
            ? $"{stunden} Std. {minuten} Min."
            : minuten > 0
                ? $"{minuten} Min. {sekunden} Sek."
                : $"{sekunden} Sek.";

        return $"Herunterfahren in {zeitText}";
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _countdownTimer.Stop();
        _countdownTimer.Tick -= OnCountdownTick;
        if (_orchestrator is not null)
        {
            _orchestrator.ZustandGeaendert -= OnZustandGeaendert;
        }
    }
}
