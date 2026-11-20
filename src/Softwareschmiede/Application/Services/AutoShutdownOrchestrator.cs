using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Interfaces;

namespace Softwareschmiede.Application.Services;

/// <summary>
/// Orchestriert den automatischen OS-Shutdown: Sobald bei aktiviertem Schalter keine
/// Automatisierung mehr fachlich läuft (<see cref="IRunningAutomationsCount"/>),
/// armiert er einen Countdown über die konfigurierte Toleranzzeit. Statuswechsel-Ereignisse
/// lösen eine koaleszierte Neuauswertung aus — ein laufender Countdown läuft bei unveränderter
/// Anzahl mit dem bestehenden Endzeitpunkt weiter (kein Verhungern durch periodische
/// Heartbeat-Ereignisse), bei einer Anzahl &gt; 0 wird er disarmiert. Nach Ablauf wird der
/// Shutdown genau einmal pro Null-Phase über <see cref="ISystemShutdownService"/> angefordert.
/// </summary>
public sealed class AutoShutdownOrchestrator : IAutoShutdownOrchestrator, IDisposable
{
    private static readonly TimeSpan SafetyTickInterval = TimeSpan.FromSeconds(5);

    private readonly IRunningAutomationStatusSource _runningAutomationStatusSource;
    private readonly WorkItemRunDataChangedNotifier _runDataChangedNotifier;
    private readonly IRunningAutomationsCount _count;
    private readonly ISystemShutdownService _systemShutdownService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AutoShutdownOrchestrator> _logger;
    private readonly Lock _syncLock = new();
    private readonly ITimer _countdownTimer;
    private readonly ITimer _safetyTimer;
    private readonly SemaphoreSlim _evalSemaphore = new(1, 1);

    private bool _enabled;
    private bool _force;
    private int _toleranceMinutes = AppSettingService.AutoShutdownDefaultToleranceMinutes;
    private DateTimeOffset? _countdownEndUtc;
    private bool _shutdownTriggered;
    private bool _reEvaluationRequested;
    private bool _disposed;

    /// <summary>Erstellt eine neue Instanz von <see cref="AutoShutdownOrchestrator"/>.</summary>
    public AutoShutdownOrchestrator(
        IRunningAutomationStatusSource runningAutomationStatusSource,
        WorkItemRunDataChangedNotifier runDataChangedNotifier,
        IRunningAutomationsCount count,
        ISystemShutdownService systemShutdownService,
        TimeProvider timeProvider,
        ILogger<AutoShutdownOrchestrator> logger)
    {
        _runningAutomationStatusSource = runningAutomationStatusSource;
        _runDataChangedNotifier = runDataChangedNotifier;
        _count = count;
        _systemShutdownService = systemShutdownService;
        _timeProvider = timeProvider;
        _logger = logger;

        _countdownTimer = _timeProvider.CreateTimer(
            _ => OnCountdownElapsedAsync().SafeFireAndForget(_logger, "AutoShutdownOrchestrator.OnCountdownElapsedAsync"),
            null,
            Timeout.InfiniteTimeSpan,
            Timeout.InfiniteTimeSpan);
        _safetyTimer = _timeProvider.CreateTimer(
            _ => OnSafetyTick(),
            null,
            Timeout.InfiniteTimeSpan,
            Timeout.InfiniteTimeSpan);

        _runningAutomationStatusSource.RunningCountChanged += OnRunningCountChanged;
        _runDataChangedNotifier.RunDataChanged += OnRunDataChanged;
    }

    /// <inheritdoc/>
    public event Action? StateChanged;

    /// <inheritdoc/>
    public DateTimeOffset? CountdownEndUtc
    {
        get
        {
            lock (_syncLock)
            {
                return _countdownEndUtc;
            }
        }
    }

    /// <inheritdoc/>
    public bool IsEnabled
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
        var changed = false;
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
                changed = true;

                if (enabled)
                {
                    _safetyTimer.Change(SafetyTickInterval, SafetyTickInterval);
                }
                else
                {
                    _countdownEndUtc = null;
                    _countdownTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                    _safetyTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                }
            }
        }

        if (changed)
        {
            StateChanged?.Invoke();
        }

        if (enabled)
        {
            TriggerEvaluation();
        }
    }

    /// <inheritdoc/>
    public void SetOptions(int toleranceMinutes, bool force)
    {
        var stateChanged = false;
        var newTolerance = Math.Max(0, toleranceMinutes);

        lock (_syncLock)
        {
            if (_disposed)
            {
                return;
            }

            if (_toleranceMinutes != newTolerance)
            {
                _toleranceMinutes = newTolerance;
                stateChanged = true;

                if (_countdownEndUtc is not null)
                {
                    var end = _timeProvider.GetUtcNow() + TimeSpan.FromMinutes(newTolerance);
                    _countdownEndUtc = end;
                    ArmCountdownTimerUnderLock(end);
                }
            }

            if (_force != force)
            {
                _force = force;
                stateChanged = true;
            }
        }

        if (stateChanged)
        {
            StateChanged?.Invoke();
        }
    }

    private void OnRunningCountChanged(int previousCount, int currentCount)
        => TriggerEvaluation();

    private void OnRunDataChanged(Guid workItemId)
        => TriggerEvaluation();

    private void OnSafetyTick()
        => TriggerEvaluation();

    private void TriggerEvaluation()
        => EvaluateAsync().SafeFireAndForget(_logger, "AutoShutdownOrchestrator.EvaluateAsync");

    /// <summary>
    /// Koaleszierte Auswertung: Parallel eintreffende Trigger erzeugen während einer laufenden
    /// Auswertung höchstens einen Nachlauf (<see cref="_reEvaluationRequested"/>). Das Flag wird
    /// vor dem Semaphorversuch unter <see cref="_syncLock"/> gesetzt; die letzte Prüfung und die
    /// Semaphorfreigabe erfolgen im selben Lock — ein Trigger, der dazwischen ankommt, erwirbt
    /// das freigegebene Semaphor danach garantiert selbst (kein verlorener Auswertungsauftrag).
    /// </summary>
    private async Task EvaluateAsync()
    {
        lock (_syncLock)
        {
            if (_disposed)
            {
                return;
            }

            _reEvaluationRequested = true;
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
                    _reEvaluationRequested = false;
                }

                await EvaluateOnceAsync();

                lock (_syncLock)
                {
                    if (!_reEvaluationRequested)
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

    private async Task EvaluateOnceAsync()
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
            if (_disposed || (!_enabled && _countdownEndUtc is null && !_shutdownTriggered))
            {
                return;
            }
        }

        int count;
        try
        {
            count = await _count.CountAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Laufende Automatisierungen konnten für den Auto-Shutdown nicht gezählt werden — Zustand bleibt unverändert.");
            return;
        }

        var stateChanged = false;
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
                _shutdownTriggered = false;
                if (_countdownEndUtc is not null)
                {
                    _countdownEndUtc = null;
                    _countdownTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                    stateChanged = true;
                }
            }
            else if (_enabled && !_shutdownTriggered && _countdownEndUtc is null)
            {
                var end = _timeProvider.GetUtcNow() + TimeSpan.FromMinutes(_toleranceMinutes);
                _countdownEndUtc = end;
                ArmCountdownTimerUnderLock(end);
                stateChanged = true;
            }
        }

        if (stateChanged)
        {
            StateChanged?.Invoke();
        }
    }

    private void ArmCountdownTimerUnderLock(DateTimeOffset end)
    {
        var rest = end - _timeProvider.GetUtcNow();
        _countdownTimer.Change(rest > TimeSpan.Zero ? rest : TimeSpan.Zero, Timeout.InfiniteTimeSpan);
    }

    /// <summary>
    /// Countdown-Ende: Final-Recheck — ist der Auto-Shutdown noch aktiviert und die fachliche
    /// Anzahl weiterhin 0, wird der Shutdown einmalig angefordert; sonst erneute Auswertung.
    /// </summary>
    private async Task OnCountdownElapsedAsync()
    {
        try
        {
            int count;
            try
            {
                count = await _count.CountAsync();
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
                    _countdownEndUtc = null;
                }

                StateChanged?.Invoke();
                await EvaluateAsync();
                return;
            }

            var triggered = false;
            lock (_syncLock)
            {
                if (_enabled && !_disposed && !_shutdownTriggered && count <= 0)
                {
                    _shutdownTriggered = true;
                    _countdownEndUtc = null;
                    triggered = true;
                }
            }

            if (!triggered)
            {
                // Recheck negativ: über die reguläre Auswertung disarmieren/neu bewerten.
                await EvaluateAsync();
                return;
            }

            StateChanged?.Invoke();

            bool force;
            lock (_syncLock)
            {
                force = _force;
            }

            _logger.LogWarning(
                "Auto-Shutdown wird ausgelöst (Toleranzzeit abgelaufen, Erzwingen={Erzwingen}).",
                force);
            try
            {
                await _systemShutdownService.RequestShutdownAsync(force);
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
                    _shutdownTriggered = false;
                }

                StateChanged?.Invoke();
                await EvaluateAsync();
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
        _runDataChangedNotifier.RunDataChanged -= OnRunDataChanged;
        _countdownTimer.Dispose();
        _safetyTimer.Dispose();
    }
}
