using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Interfaces;

namespace Softwareschmiede.Application.Services;

/// <summary>
/// Fordert eine SCM-Startprüfung außerhalb des Poll-Zyklus an. Wird von den Pausen-Setzpfaden
/// (<see cref="WorkItemService.SetPauseAsync"/>, <see cref="ScmEventStore"/> für die
/// Verzögerungsdirektive, <see cref="AiPluginLimitService.ProcessRateLimitAsync"/>,
/// <see cref="ScmEventOrchestrator.CancelAsync"/>)
/// benachrichtigt; der <see cref="ScmStartCheckService"/> fasst Häufungen zusammen und
/// führt die Prüfung serialisiert aus. Ohne Abonnenten verfällt eine Anforderung wirkungslos —
/// der Poll des <see cref="ScmEventMonitoringService"/> bleibt als Fallback bestehen.
/// </summary>
public sealed class ScmStartCheckTrigger
{
    /// <summary>Eine Startprüfung wurde angefordert.</summary>
    public event Action? Angefordert;

    /// <summary>Fordert eine Startprüfung an (non-blocking; Häufungen werden zusammengefasst).</summary>
    public void RequestStartCheck() => Angefordert?.Invoke();
}

/// <summary>
/// Ereignisgesteuerter Auslöser der SCM-Startprüfung: Füllt frei werdende
/// Ausführungsplätze ohne Wartezeit auf den nächsten Poll, sobald eine CLI-Ausführung
/// endet (sinkende Laufzählung über <see cref="IRunningAutomationStatusSource.RunningCountChanged"/>)
/// oder eine Aufgabe pausiert wird (<see cref="ScmStartCheckTrigger"/>). Jeder
/// Durchlauf ruft dieselbe priorisierte Batch-Prüfung wie der Poll-Zyklus auf
/// (<see cref="ScmEventOrchestrator.DispatchDueEventsAsync"/>); Serialisierung und
/// Idempotenz kommen aus den bestehenden Aufgabensperren, Leases und der atomaren
/// Platzreservierung — der Dienst führt keine eigene Polling-Abfrage ein.
/// </summary>
public sealed class ScmStartCheckService : BackgroundService
{
    /// <summary>Standard-Sammelphase vor einer getriggerten Startprüfung.</summary>
    internal static readonly TimeSpan DefaultCollectionPeriod = TimeSpan.FromMilliseconds(250);

    private readonly IServiceScopeFactory _scopes;
    private readonly ScmStartCheckTrigger _trigger;
    private readonly IRunningAutomationStatusSource _runCounter;
    private readonly ScmEventStartupGate _startup;
    private readonly TimeProvider _time;
    private readonly ILogger<ScmStartCheckService> _logger;
    // Höchstens ein eingereihter Durchlauf: weitere Auslöser koaleszieren im Signal.
    private readonly SemaphoreSlim _signal = new(0, 1);

    /// <inheritdoc cref="ScmStartCheckService"/>
    /// <remarks>
    /// Die Abonnements werden bereits im Konstruktor gesetzt: Auslöser vor dem Dienststart
    /// (z. B. während der Datenbankmigration) sammeln sich im Signal und werden nach dem
    /// <see cref="ScmEventStartupGate"/> einmal zusammengefasst abgearbeitet. Die Handler sind
    /// bewusst non-blocking — der Invoke-Kontext von
    /// <see cref="IRunningAutomationStatusSource.RunningCountChanged"/> liegt teils unter der
    /// Start-Sperre des <see cref="AiExecutionService"/>.
    /// </remarks>
    public ScmStartCheckService(
        IServiceScopeFactory scopes,
        ScmStartCheckTrigger trigger,
        IRunningAutomationStatusSource runCounter,
        ScmEventStartupGate startup,
        TimeProvider time,
        ILogger<ScmStartCheckService> logger)
    {
        _scopes = scopes;
        _trigger = trigger;
        _runCounter = runCounter;
        _startup = startup;
        _time = time;
        _logger = logger;
        trigger.Angefordert += OnAngefordert;
        runCounter.RunningCountChanged += OnRunningCountChanged;
    }

    /// <summary>
    /// Sammelphase zwischen Auslöser und Durchlauf: bündelt Burst-Auslöser (z. B. mehrere
    /// gleichzeitig endende Prozesse oder eine Pausen-Anwendung auf viele Aufgaben) und gibt
    /// dem Exited-Handler Zeit, den beendeten <see cref="Domain.Enums.WorkItemExecutionStatus"/>
    /// zu persistieren — <see cref="IRunningAutomationStatusSource.RunningCountChanged"/> feuert
    /// vor dieser Persistenz. Testbar über das injizierte <see cref="TimeProvider"/>.
    /// </summary>
    internal TimeSpan CollectionPeriod { get; set; } = DefaultCollectionPeriod;

    /// <summary>Anzahl noch nicht verbrauchter Auslöser im Signal (Diagnose/Tests).</summary>
    internal int PendingChecks => _signal.CurrentCount;

    /// <summary>
    /// Führt eine Startprüfung aus: Respektiert denselben Laufzeit-Schalter wie der Poll
    /// („Aufgaben ausführen") und ruft die priorisierte Batch-Prüfung des Orchestrierers.
    /// Wiederholbar und ohne eigenen Zustand aufrufbar.
    /// </summary>
    /// <param name="ct">Abbruchtoken.</param>
    public async Task RunOnceAsync(CancellationToken ct = default)
    {
        using var scope = _scopes.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<AppSettingService>();
        // Gleicher Laufzeit-Schalter wie der Poll-Zyklus: Bei deaktivierter Überwachung wird
        // auch ereignisgesteuert nichts automatisch zugestellt.
        if (!await settings.GetScmMonitoringEnabledAsync(ct))
        {
            _logger.LogDebug("SCM-Startprüfung übersprungen: Ereignisüberwachung ist deaktiviert.");
            return;
        }
        await scope.ServiceProvider.GetRequiredService<ScmEventOrchestrator>().DispatchDueEventsAsync(ct);
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await _startup.WaitAsync(stoppingToken);
            while (!stoppingToken.IsCancellationRequested)
            {
                await _signal.WaitAsync(stoppingToken);
                // Kurze Sammelphase, danach werden weitere bereits wartende Auslöser
                // zusammengefasst — Auslöser während des Durchlaufs lösen genau eine
                // weitere Prüfung aus.
                await Task.Delay(CollectionPeriod, _time, stoppingToken);
                while (_signal.Wait(0))
                {
                }
                try
                {
                    await RunOnceAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "SCM-Startprüfung fehlgeschlagen.");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        _trigger.Angefordert -= OnAngefordert;
        _runCounter.RunningCountChanged -= OnRunningCountChanged;
        _signal.Dispose();
        base.Dispose();
    }

    private void OnAngefordert()
    {
        try { _signal.Release(); }
        // Bereits ein Durchlauf eingereiht: Häufungen werden zu einer Prüfung zusammengefasst.
        catch (SemaphoreFullException) { }
        // Rennbedingung beim Herunterfahren: Ein bereits laufender Invoke darf nach dem
        // Entsorgen des Signals nicht mehr fehlschlagen.
        catch (ObjectDisposedException) { }
    }

    private void OnRunningCountChanged(int previousCount, int currentCount)
    {
        // Nur ein freiwerdender Platz löst die Prüfung aus; Starts und unveränderte
        // Zählungen sind kein Auslöser. Nicht-SCM-Ausführungen lösen ebenfalls aus —
        // die Zählung bleibt davon unberührt.
        if (currentCount < previousCount)
            OnAngefordert();
    }
}
