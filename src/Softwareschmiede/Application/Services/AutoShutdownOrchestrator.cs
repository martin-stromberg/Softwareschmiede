using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Interfaces;

namespace Softwareschmiede.Application.Services;

/// <summary>
/// Orchestriert den automatischen OS-Shutdown: Sobald bei aktiviertem Schalter keine
/// Automatisierung mehr fachlich läuft (<see cref="ILaufendeAutomatisierungenZaehlung"/>),
/// armiert er einen Countdown über die konfigurierte Toleranzzeit. Statuswechsel-Ereignisse
/// lösen eine koaleszierte Neuauswertung aus — ein laufender Countdown läuft bei unveränderter
/// Anzahl mit dem bestehenden Endzeitpunkt weiter (kein Verhungern durch periodische
/// Heartbeat-Ereignisse), bei einer Anzahl &gt; 0 wird er disarmiert. Nach Ablauf wird der
/// Shutdown genau einmal pro Null-Phase über <see cref="ISystemShutdownService"/> angefordert.
/// </summary>
public sealed class AutoShutdownOrchestrator : IAutoShutdownOrchestrator, IDisposable
{
    private static readonly TimeSpan SicherheitsTickIntervall = TimeSpan.FromSeconds(5);

    private readonly IRunningAutomationStatusSource _runningAutomationStatusSource;
    private readonly AufgabeLaufdatenChangedNotifier _laufdatenChangedNotifier;
    private readonly ILaufendeAutomatisierungenZaehlung _zaehlung;
    private readonly ISystemShutdownService _systemShutdownService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AutoShutdownOrchestrator> _logger;
    private readonly Lock _syncLock = new();
    private readonly ITimer _countdownTimer;
    private readonly ITimer _sicherheitsTimer;
    private readonly SemaphoreSlim _evalSemaphore = new(1, 1);

    private bool _enabled;
    private bool _erzwingen;
    private int _toleranzMinuten = AppEinstellungService.AutoShutdownDefaultToleranzMinuten;
    private DateTimeOffset? _countdownEndeUtc;
    private bool _shutdownAusgeloest;
    private bool _evalErneutAngefordert;
    private bool _disposed;

    /// <summary>Erstellt eine neue Instanz von <see cref="AutoShutdownOrchestrator"/>.</summary>
    public AutoShutdownOrchestrator(
        IRunningAutomationStatusSource runningAutomationStatusSource,
        AufgabeLaufdatenChangedNotifier laufdatenChangedNotifier,
        ILaufendeAutomatisierungenZaehlung zaehlung,
        ISystemShutdownService systemShutdownService,
        TimeProvider timeProvider,
        ILogger<AutoShutdownOrchestrator> logger)
    {
        _runningAutomationStatusSource = runningAutomationStatusSource;
        _laufdatenChangedNotifier = laufdatenChangedNotifier;
        _zaehlung = zaehlung;
        _systemShutdownService = systemShutdownService;
        _timeProvider = timeProvider;
        _logger = logger;

        _countdownTimer = _timeProvider.CreateTimer(
            _ => OnCountdownAbgelaufenAsync().SafeFireAndForget(_logger, "AutoShutdownOrchestrator.OnCountdownAbgelaufenAsync"),
            null,
            Timeout.InfiniteTimeSpan,
            Timeout.InfiniteTimeSpan);
        _sicherheitsTimer = _timeProvider.CreateTimer(
            _ => OnSicherheitsTick(),
            null,
            Timeout.InfiniteTimeSpan,
            Timeout.InfiniteTimeSpan);

        _runningAutomationStatusSource.RunningCountChanged += OnRunningCountChanged;
        _laufdatenChangedNotifier.LaufdatenChanged += OnLaufdatenChanged;
    }

    /// <inheritdoc/>
    public event Action? ZustandGeaendert;

    /// <inheritdoc/>
    public DateTimeOffset? CountdownEndeUtc
    {
        get
        {
            lock (_syncLock)
            {
                return _countdownEndeUtc;
            }
        }
    }

    /// <inheritdoc/>
    public bool IstAktiviert
    {
        get
        {
            lock (_syncLock)
            {
                return _enabled;
            }
        }
    }

    /// <inheritdoc/>
    public void SetEnabled(bool enabled)
    {
        var geaendert = false;
        lock (_syncLock)
        {
            // Nach Dispose keine Mutationen/Timer-Zugriffe mehr: Der Guard muss unter
            // dem Lock stehen, damit er atomar mit dem _disposed-Setzen in Dispose
            // entscheidet — sonst könnte ein später Aufruf ITimer.Change auf bereits
            // entsorgte Timer absetzen (ObjectDisposedException).
            if (_disposed)
            {
                return;
            }

            if (_enabled != enabled)
            {
                _enabled = enabled;
                geaendert = true;

                if (enabled)
                {
                    _sicherheitsTimer.Change(SicherheitsTickIntervall, SicherheitsTickIntervall);
                }
                else
                {
                    _countdownEndeUtc = null;
                    _countdownTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                    _sicherheitsTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                }
            }
        }

        if (geaendert)
        {
            ZustandGeaendert?.Invoke();
        }

        if (enabled)
        {
            TriggerEvaluierung();
        }
    }

    /// <inheritdoc/>
    public void SetOptionen(int toleranzMinuten, bool erzwingen)
    {
        var zustandGeaendert = false;
        var neueToleranz = Math.Max(0, toleranzMinuten);

        lock (_syncLock)
        {
            if (_disposed)
            {
                return;
            }

            if (_toleranzMinuten != neueToleranz)
            {
                _toleranzMinuten = neueToleranz;
                zustandGeaendert = true;

                if (_countdownEndeUtc is not null)
                {
                    var ende = _timeProvider.GetUtcNow() + TimeSpan.FromMinutes(neueToleranz);
                    _countdownEndeUtc = ende;
                    ArmiereCountdownTimerUnterLock(ende);
                }
            }

            if (_erzwingen != erzwingen)
            {
                _erzwingen = erzwingen;
                zustandGeaendert = true;
            }
        }

        if (zustandGeaendert)
        {
            ZustandGeaendert?.Invoke();
        }
    }

    private void OnRunningCountChanged(int previousCount, int currentCount)
        => TriggerEvaluierung();

    private void OnLaufdatenChanged(Guid aufgabeId)
        => TriggerEvaluierung();

    private void OnSicherheitsTick()
        => TriggerEvaluierung();

    private void TriggerEvaluierung()
        => EvaluiereAsync().SafeFireAndForget(_logger, "AutoShutdownOrchestrator.EvaluiereAsync");

    /// <summary>
    /// Koaleszierte Auswertung: Parallel eintreffende Trigger erzeugen während einer laufenden
    /// Auswertung höchstens einen Nachlauf (<see cref="_evalErneutAngefordert"/>). Das Flag wird
    /// vor dem Semaphorversuch unter <see cref="_syncLock"/> gesetzt; die letzte Prüfung und die
    /// Semaphorfreigabe erfolgen im selben Lock — ein Trigger, der dazwischen ankommt, erwirbt
    /// das freigegebene Semaphor danach garantiert selbst (kein verlorener Auswertungsauftrag).
    /// </summary>
    private async Task EvaluiereAsync()
    {
        lock (_syncLock)
        {
            if (_disposed)
            {
                return;
            }

            _evalErneutAngefordert = true;
        }

        if (!await _evalSemaphore.WaitAsync(0))
        {
            // Eine Auswertung läuft bereits: Sie liest das Flag unter demselben Lock und
            // führt den Nachlauf aus.
            return;
        }

        try
        {
            while (true)
            {
                lock (_syncLock)
                {
                    _evalErneutAngefordert = false;
                }

                await EvaluiereEinmalAsync();

                lock (_syncLock)
                {
                    if (!_evalErneutAngefordert)
                    {
                        _evalSemaphore.Release();
                        return;
                    }
                }
            }
        }
        catch
        {
            _evalSemaphore.Release();
            throw;
        }
    }

    private async Task EvaluiereEinmalAsync()
    {
        lock (_syncLock)
        {
            // Früh aussteigen, solange das Ergebnis garantiert wirkungslos bleibt: Ohne
            // aktiviertes Feature, armierten Countdown oder gesetztes Auslöse-Flag kann
            // die Auswertung keinen Zustand ändern (count > 0 würde nur das ohnehin
            // gelöschte Auslöse-Flag erneut löschen). Die volle DB-Zählung, die
            // LaufdatenChanged-Heartbeat-Events sonst zyklisch mitlaufen ließen, entfällt
            // im deaktivierten Ruhezustand damit. Nach einem ausgelösten Shutdown
            // (_shutdownAusgeloest) bleibt die Zählung weiter aktiv, damit ein
            // count > 0-Übergang das Flag zurücksetzen kann.
            if (_disposed || (!_enabled && _countdownEndeUtc is null && !_shutdownAusgeloest))
            {
                return;
            }
        }

        int count;
        try
        {
            count = await _zaehlung.ZaehleAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Laufende Automatisierungen konnten für den Auto-Shutdown nicht gezählt werden — Zustand bleibt unverändert.");
            return;
        }

        var zustandGeaendert = false;
        lock (_syncLock)
        {
            // Eine zum Dispose-Zeitpunkt in-flight befindliche Auswertung darf nach
            // _countdownTimer.Dispose() kein ArmiereCountdownTimerUnterLock mehr
            // aufrufen — früh zurückkehren statt ITimer.Change zu riskieren.
            if (_disposed)
            {
                return;
            }

            if (count > 0)
            {
                _shutdownAusgeloest = false;
                if (_countdownEndeUtc is not null)
                {
                    _countdownEndeUtc = null;
                    _countdownTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                    zustandGeaendert = true;
                }
            }
            else if (_enabled && !_shutdownAusgeloest && _countdownEndeUtc is null)
            {
                var ende = _timeProvider.GetUtcNow() + TimeSpan.FromMinutes(_toleranzMinuten);
                _countdownEndeUtc = ende;
                ArmiereCountdownTimerUnterLock(ende);
                zustandGeaendert = true;
            }
        }

        if (zustandGeaendert)
        {
            ZustandGeaendert?.Invoke();
        }
    }

    private void ArmiereCountdownTimerUnterLock(DateTimeOffset ende)
    {
        var rest = ende - _timeProvider.GetUtcNow();
        _countdownTimer.Change(rest > TimeSpan.Zero ? rest : TimeSpan.Zero, Timeout.InfiniteTimeSpan);
    }

    /// <summary>
    /// Countdown-Ende: Final-Recheck — ist der Auto-Shutdown noch aktiviert und die fachliche
    /// Anzahl weiterhin 0, wird der Shutdown einmalig angefordert; sonst erneute Auswertung.
    /// </summary>
    private async Task OnCountdownAbgelaufenAsync()
    {
        try
        {
            int count;
            try
            {
                count = await _zaehlung.ZaehleAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Final-Recheck des Auto-Shutdowns konnte die laufenden Automatisierungen nicht zählen — Shutdown wird nicht ausgelöst.");
                // Der One-Shot-Countdown-Timer ist bereits abgelaufen: Den Endzeitpunkt
                // zurücksetzen, damit die sofortige Neuauswertung bzw. der nächste
                // Sicherheitstick erneut armieren kann — sonst bliebe der Orchestrator
                // mit totem Countdown dauerhaft hängen.
                lock (_syncLock)
                {
                    _countdownEndeUtc = null;
                }

                ZustandGeaendert?.Invoke();
                await EvaluiereAsync();
                return;
            }

            var ausgeloest = false;
            lock (_syncLock)
            {
                if (_enabled && !_disposed && !_shutdownAusgeloest && count <= 0)
                {
                    _shutdownAusgeloest = true;
                    _countdownEndeUtc = null;
                    ausgeloest = true;
                }
            }

            if (!ausgeloest)
            {
                // Recheck negativ: über die reguläre Auswertung disarmieren/neu bewerten.
                await EvaluiereAsync();
                return;
            }

            ZustandGeaendert?.Invoke();

            bool erzwingen;
            lock (_syncLock)
            {
                erzwingen = _erzwingen;
            }

            _logger.LogWarning(
                "Auto-Shutdown wird ausgelöst (Toleranzzeit abgelaufen, Erzwingen={Erzwingen}).",
                erzwingen);
            try
            {
                await _systemShutdownService.RequestShutdownAsync(erzwingen);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Auto-Shutdown konnte nicht ausgeführt werden.");
                // Auslöse-Flag zurücksetzen und neu auswerten: Die Neuauswertung bzw. der
                // nächste Sicherheitstick armiert einen neuen Countdown — sonst bliebe das
                // Feature bei einem transienten Fehler für den Rest der Null-Phase ohne
                // Re-Arm/Retry still funktionslos (analog zum Zählfehler-Fall oben).
                lock (_syncLock)
                {
                    _shutdownAusgeloest = false;
                }

                ZustandGeaendert?.Invoke();
                await EvaluiereAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Auto-Shutdown konnte nicht ausgeführt werden.");
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (_syncLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        _runningAutomationStatusSource.RunningCountChanged -= OnRunningCountChanged;
        _laufdatenChangedNotifier.LaufdatenChanged -= OnLaufdatenChanged;
        _countdownTimer.Dispose();
        _sicherheitsTimer.Dispose();
    }
}
