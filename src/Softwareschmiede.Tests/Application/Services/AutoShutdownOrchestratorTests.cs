using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>
/// Tests des <see cref="AutoShutdownOrchestrator"/>: Countdown-Armierung über die
/// <see cref="FakeTimeProvider"/>-Zeit, Disarmierung bei positiver fachlicher Anzahl,
/// Neubewertung ohne Countdown-Reset sowie Einmaligkeit der Shutdown-Anforderung pro Null-Phase.
/// </summary>
public sealed class AutoShutdownOrchestratorTests
{
    private static AutoShutdownOrchestrator CreateOrchestrator(
        FakeRunningAutomationStatusSource runningSource,
        FakeRunningAutomationsCount count,
        FakeSystemShutdownService shutdownService,
        FakeTimeProvider timeProvider,
        WorkItemRunDataChangedNotifier? notifier = null)
        => new(
            runningSource,
            notifier ?? new WorkItemRunDataChangedNotifier(),
            count,
            shutdownService,
            timeProvider,
            NullLogger<AutoShutdownOrchestrator>.Instance);

    /// <summary>Bei Aktivierung und Anzahl 0 wird der Countdown mit dem Ende now+Toleranz armiert.</summary>
    [Fact]
    public void SetEnabled_ArmedCountdown_WithToleranceEndtime()
    {
        var runningSource = new FakeRunningAutomationStatusSource();
        var count = new FakeRunningAutomationsCount { Count = 0 };
        var shutdownService = new FakeSystemShutdownService();
        var timeProvider = new FakeTimeProvider();
        using var sut = CreateOrchestrator(runningSource, count, shutdownService, timeProvider);

        sut.SetOptions(toleranceMinutes: 5, force: false);
        sut.SetEnabled(true);

        sut.CountdownEndUtc.Should().Be(timeProvider.GetUtcNow() + TimeSpan.FromMinutes(5));
        sut.IsEnabled.Should().BeTrue();
        shutdownService.RequestCount.Should().Be(0);
    }

    /// <summary>Der Shutdown wird erst nach Ablauf der vollen Toleranzzeit angefordert.</summary>
    [Fact]
    public void CountdownFlow_TriggersShutdownFirstAfterToleranceOut()
    {
        var runningSource = new FakeRunningAutomationStatusSource();
        var count = new FakeRunningAutomationsCount { Count = 0 };
        var shutdownService = new FakeSystemShutdownService();
        var timeProvider = new FakeTimeProvider();
        using var sut = CreateOrchestrator(runningSource, count, shutdownService, timeProvider);

        sut.SetOptions(toleranceMinutes: 5, force: false);
        sut.SetEnabled(true);

        timeProvider.Advance(TimeSpan.FromMinutes(4).Add(TimeSpan.FromSeconds(59)));
        shutdownService.RequestCount.Should().Be(0);

        timeProvider.Advance(TimeSpan.FromSeconds(1));
        shutdownService.RequestCount.Should().Be(1);
    }

    /// <summary>Das Erzwingen-Flag wird an den Shutdown-Service weitergereicht.</summary>
    [Fact]
    public void CountdownFlow_PassesForceFlag()
    {
        var runningSource = new FakeRunningAutomationStatusSource();
        var count = new FakeRunningAutomationsCount { Count = 0 };
        var shutdownService = new FakeSystemShutdownService();
        var timeProvider = new FakeTimeProvider();
        using var sut = CreateOrchestrator(runningSource, count, shutdownService, timeProvider);

        sut.SetOptions(toleranceMinutes: 2, force: true);
        sut.SetEnabled(true);
        timeProvider.Advance(TimeSpan.FromMinutes(2));

        shutdownService.ForceValues.Should().Equal(true);
    }

    /// <summary>Statuswechsel bei unveraenderter Anzahl 0 setzen den Countdown nicht zurueck (kein Verhungern).</summary>
    [Fact]
    public void StatuschangeAtUnchangedCount_LetsCountdownEndUnchanged()
    {
        var runningSource = new FakeRunningAutomationStatusSource();
        var count = new FakeRunningAutomationsCount { Count = 0 };
        var shutdownService = new FakeSystemShutdownService();
        var timeProvider = new FakeTimeProvider();
        var notifier = new WorkItemRunDataChangedNotifier();
        using var sut = CreateOrchestrator(runningSource, count, shutdownService, timeProvider, notifier);

        sut.SetOptions(toleranceMinutes: 5, force: false);
        sut.SetEnabled(true);
        var originalEnd = sut.CountdownEndUtc;

        timeProvider.Advance(TimeSpan.FromMinutes(2));
        runningSource.Raise(previous: 0, current: 0);
        notifier.NotifyRunDataChanged(Guid.NewGuid());

        sut.CountdownEndUtc.Should().Be(originalEnd);
        shutdownService.RequestCount.Should().Be(0);
    }

    /// <summary>Eine positive fachliche Anzahl disarmiert den laufenden Countdown.</summary>
    [Fact]
    public void StatuschangeWithPositiveCount_DisarmedCountdown()
    {
        var runningSource = new FakeRunningAutomationStatusSource();
        var count = new FakeRunningAutomationsCount { Count = 0 };
        var shutdownService = new FakeSystemShutdownService();
        var timeProvider = new FakeTimeProvider();
        using var sut = CreateOrchestrator(runningSource, count, shutdownService, timeProvider);

        sut.SetOptions(toleranceMinutes: 5, force: false);
        sut.SetEnabled(true);
        timeProvider.Advance(TimeSpan.FromMinutes(1));

        count.Count = 1;
        runningSource.Raise(previous: 0, current: 1);

        sut.CountdownEndUtc.Should().BeNull();
        timeProvider.Advance(TimeSpan.FromMinutes(10));
        shutdownService.RequestCount.Should().Be(0);
    }

    /// <summary>Nach Rueckkehr zur Anzahl 0 wird ein neuer Countdown ueber die volle Toleranz armiert.</summary>
    [Fact]
    public void ReturnToNull_ArmedNewFullTolerance()
    {
        var runningSource = new FakeRunningAutomationStatusSource();
        var count = new FakeRunningAutomationsCount { Count = 0 };
        var shutdownService = new FakeSystemShutdownService();
        var timeProvider = new FakeTimeProvider();
        using var sut = CreateOrchestrator(runningSource, count, shutdownService, timeProvider);

        sut.SetOptions(toleranceMinutes: 5, force: false);
        sut.SetEnabled(true);

        count.Count = 1;
        runningSource.Raise(previous: 0, current: 1);
        timeProvider.Advance(TimeSpan.FromMinutes(1));

        count.Count = 0;
        runningSource.Raise(previous: 1, current: 0);

        sut.CountdownEndUtc.Should().Be(timeProvider.GetUtcNow() + TimeSpan.FromMinutes(5));

        timeProvider.Advance(TimeSpan.FromMinutes(4).Add(TimeSpan.FromSeconds(59)));
        shutdownService.RequestCount.Should().Be(0);
        timeProvider.Advance(TimeSpan.FromSeconds(1));
        shutdownService.RequestCount.Should().Be(1);
    }

    /// <summary>Deaktivieren disarmiert den Countdown und verhindert den Shutdown.</summary>
    [Fact]
    public void SetEnabledFalse_DisarmedRunningCountdown()
    {
        var runningSource = new FakeRunningAutomationStatusSource();
        var count = new FakeRunningAutomationsCount { Count = 0 };
        var shutdownService = new FakeSystemShutdownService();
        var timeProvider = new FakeTimeProvider();
        using var sut = CreateOrchestrator(runningSource, count, shutdownService, timeProvider);

        sut.SetOptions(toleranceMinutes: 5, force: false);
        sut.SetEnabled(true);
        sut.SetEnabled(false);

        sut.CountdownEndUtc.Should().BeNull();
        sut.IsEnabled.Should().BeFalse();
        timeProvider.Advance(TimeSpan.FromMinutes(10));
        shutdownService.RequestCount.Should().Be(0);
    }

    /// <summary>
    /// Im deaktivierten Ruhezustand (kein Feature, kein Countdown, kein ausgelöster Shutdown)
    /// ist jede Auswertung wirkungslos — Trigger dürfen dann keine DB-Zählung auslösen.
    /// </summary>
    [Fact]
    public async Task TriggerAtDisabledIdlestate_RunsNoCountOut()
    {
        var runningSource = new FakeRunningAutomationStatusSource();
        var count = new FakeRunningAutomationsCount { Count = 0 };
        var shutdownService = new FakeSystemShutdownService();
        var timeProvider = new FakeTimeProvider();
        var notifier = new WorkItemRunDataChangedNotifier();
        using var sut = CreateOrchestrator(runningSource, count, shutdownService, timeProvider, notifier);

        runningSource.Raise(previous: 0, current: 1);
        runningSource.Raise(previous: 1, current: 0);
        notifier.NotifyRunDataChanged(Guid.NewGuid());
        timeProvider.Advance(TimeSpan.FromMinutes(10));
        await Task.Delay(100, TestContext.Current.CancellationToken);

        count.CountCalls.Should().Be(0,
            "ohne aktiviertes Feature, armierten Countdown oder ausgelösten Shutdown ist die Zählung wirkungslos");

        // Gegenprobe: Dasselbe Fake zählt zuverlässig, sobald das Feature aktiviert ist.
        sut.SetEnabled(true);
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (count.CountCalls < 1 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        count.CountCalls.Should().BeGreaterThanOrEqualTo(1);
    }

    /// <summary>Eine Toleranzaenderung re-armiert den laufenden Countdown ab jetzt mit dem neuen Wert.</summary>
    [Fact]
    public void ToleranceChange_ReArmedRunningCountdownFromNow()
    {
        var runningSource = new FakeRunningAutomationStatusSource();
        var count = new FakeRunningAutomationsCount { Count = 0 };
        var shutdownService = new FakeSystemShutdownService();
        var timeProvider = new FakeTimeProvider();
        using var sut = CreateOrchestrator(runningSource, count, shutdownService, timeProvider);

        sut.SetOptions(toleranceMinutes: 5, force: false);
        sut.SetEnabled(true);
        timeProvider.Advance(TimeSpan.FromMinutes(4));

        sut.SetOptions(toleranceMinutes: 2, force: false);

        sut.CountdownEndUtc.Should().Be(timeProvider.GetUtcNow() + TimeSpan.FromMinutes(2));
        timeProvider.Advance(TimeSpan.FromMinutes(2));
        shutdownService.RequestCount.Should().Be(1);
    }

    /// <summary>Der Final-Recheck bei Ablauf verhindert den Shutdown bei positiver Anzahl.</summary>
    [Fact]
    public void PositiveCountAtCountdownFlow_TriggersNoShutdownOut()
    {
        var runningSource = new FakeRunningAutomationStatusSource();
        var count = new FakeRunningAutomationsCount { Count = 0 };
        var shutdownService = new FakeSystemShutdownService();
        var timeProvider = new FakeTimeProvider();
        using var sut = CreateOrchestrator(runningSource, count, shutdownService, timeProvider);

        sut.SetOptions(toleranceMinutes: 5, force: false);
        sut.SetEnabled(true);

        // Anzahl wird erst unmittelbar vor dem Ablauf positiv (Final-Recheck / Sicherheits-Tick).
        count.Count = 1;
        timeProvider.Advance(TimeSpan.FromMinutes(5));

        shutdownService.RequestCount.Should().Be(0);
        sut.CountdownEndUtc.Should().BeNull();
    }

    /// <summary>Der Shutdown wird pro Null-Phase hoechstens einmal angefordert.</summary>
    [Fact]
    public void Shutdown_WillPerNullPhaseOnlyOnceTriggered()
    {
        var runningSource = new FakeRunningAutomationStatusSource();
        var count = new FakeRunningAutomationsCount { Count = 0 };
        var shutdownService = new FakeSystemShutdownService();
        var timeProvider = new FakeTimeProvider();
        using var sut = CreateOrchestrator(runningSource, count, shutdownService, timeProvider);

        sut.SetOptions(toleranceMinutes: 1, force: false);
        sut.SetEnabled(true);
        timeProvider.Advance(TimeSpan.FromMinutes(1));
        shutdownService.RequestCount.Should().Be(1);

        runningSource.Raise(previous: 0, current: 0);
        runningSource.Raise(previous: 1, current: 0);
        timeProvider.Advance(TimeSpan.FromMinutes(10));

        shutdownService.RequestCount.Should().Be(1);
    }

    /// <summary>Nach einer positiven Zwischenphase kann bei neuer Null-Phase erneut ausgeloest werden.</summary>
    [Fact]
    public void TriggeredShutdown_TriggersAfterInterimphaseAgainOut()
    {
        var runningSource = new FakeRunningAutomationStatusSource();
        var count = new FakeRunningAutomationsCount { Count = 0 };
        var shutdownService = new FakeSystemShutdownService();
        var timeProvider = new FakeTimeProvider();
        using var sut = CreateOrchestrator(runningSource, count, shutdownService, timeProvider);

        sut.SetOptions(toleranceMinutes: 1, force: false);
        sut.SetEnabled(true);
        timeProvider.Advance(TimeSpan.FromMinutes(1));
        shutdownService.RequestCount.Should().Be(1);

        count.Count = 1;
        runningSource.Raise(previous: 0, current: 1);
        count.Count = 0;
        runningSource.Raise(previous: 1, current: 0);
        timeProvider.Advance(TimeSpan.FromMinutes(1));

        shutdownService.RequestCount.Should().Be(2);
    }

    /// <summary>
    /// Wirft der Shutdown-Service beim Auslösen, wird die Ausnahme abgefangen und es bleibt
    /// kein toter Orchestrator zurück: Das Auslöse-Flag wird zurückgesetzt, die Neuauswertung
    /// armiert einen neuen Countdown und der nächste Ablauf versucht die Anforderung erneut.
    /// </summary>
    [Fact]
    public void ExceptionAtShutdownCall_ReArmedCountdownForAgainAttempt()
    {
        var runningSource = new FakeRunningAutomationStatusSource();
        var count = new FakeRunningAutomationsCount { Count = 0 };
        var shutdownService = new FakeSystemShutdownService { ThrowOnRequest = true };
        var timeProvider = new FakeTimeProvider();
        using var sut = CreateOrchestrator(runningSource, count, shutdownService, timeProvider);

        sut.SetOptions(toleranceMinutes: 5, force: false);
        sut.SetEnabled(true);
        timeProvider.Advance(TimeSpan.FromMinutes(5));

        shutdownService.RequestCount.Should().Be(1);
        sut.CountdownEndUtc.Should().Be(timeProvider.GetUtcNow() + TimeSpan.FromMinutes(5),
            "ein fehlgeschlagener Shutdown-Aufruf darf das Feature nicht für den Rest der Null-Phase blockieren");

        shutdownService.ThrowOnRequest = false;
        timeProvider.Advance(TimeSpan.FromMinutes(5));
        shutdownService.RequestCount.Should().Be(2);
    }

    /// <summary>Eine Exception der Zaehlung aendert den Orchestrator-Zustand nicht.</summary>
    [Fact]
    public void ExceptionAtCount_LetsStateUnchanged()
    {
        var runningSource = new FakeRunningAutomationStatusSource();
        var count = new FakeRunningAutomationsCount { Count = 0, ThrowOnCount = true };
        var shutdownService = new FakeSystemShutdownService();
        var timeProvider = new FakeTimeProvider();
        using var sut = CreateOrchestrator(runningSource, count, shutdownService, timeProvider);

        var act = () => sut.SetEnabled(true);
        act.Should().NotThrow();
        sut.CountdownEndUtc.Should().BeNull();
        shutdownService.RequestCount.Should().Be(0);
    }

    /// <summary>
    /// Wirft der Final-Recheck beim Countdown-Ablauf, bleibt kein toter Countdown zurück:
    /// Der Endzeitpunkt wird zurückgesetzt und der nächste Sicherheitstick armiert erneut.
    /// </summary>
    [Fact]
    public void ExceptionInFinalRecheck_ReArmedOverSecuritytick()
    {
        var runningSource = new FakeRunningAutomationStatusSource();
        var count = new FakeRunningAutomationsCount { Count = 0 };
        var shutdownService = new FakeSystemShutdownService();
        var timeProvider = new FakeTimeProvider();
        using var sut = CreateOrchestrator(runningSource, count, shutdownService, timeProvider);

        sut.SetOptions(toleranceMinutes: 5, force: false);
        sut.SetEnabled(true);

        count.ThrowOnCount = true;
        timeProvider.Advance(TimeSpan.FromMinutes(5));

        sut.CountdownEndUtc.Should().BeNull("der abgelaufene One-Shot-Timer darf keinen toten Countdown hinterlassen");
        shutdownService.RequestCount.Should().Be(0);

        count.ThrowOnCount = false;
        timeProvider.Advance(TimeSpan.FromSeconds(5));

        sut.CountdownEndUtc.Should().Be(timeProvider.GetUtcNow() + TimeSpan.FromMinutes(5));

        timeProvider.Advance(TimeSpan.FromMinutes(5));
        shutdownService.RequestCount.Should().Be(1);
    }

    /// <summary>
    /// Trigger, die während einer laufenden Auswertung eintreffen, werden auf genau einen
    /// Nachlauf koalesziert — kein verlorener Auftrag, kein Sturm einzelner Auswertungen.
    /// </summary>
    [Fact]
    public async Task TriggerWhileRunningEvaluation_BeOnATailCoalesced()
    {
        var runningSource = new FakeRunningAutomationStatusSource();
        var count = new FakeRunningAutomationsCount { Count = 0 };
        var shutdownService = new FakeSystemShutdownService();
        var timeProvider = new FakeTimeProvider();
        using var sut = CreateOrchestrator(runningSource, count, shutdownService, timeProvider);

        count.BlockNextCalls(1);
        sut.SetEnabled(true);

        // Warten, bis die erste Auswertung im blockierten Zaehlaufruf angekommen ist.
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (count.CountCalls < 1 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        count.CountCalls.Should().Be(1);

        runningSource.Raise(previous: 0, current: 0);
        runningSource.Raise(previous: 0, current: 0);
        count.GetBlockingFree();

        deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (count.CountCalls < 2 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        await Task.Delay(100, TestContext.Current.CancellationToken);

        count.CountCalls.Should().Be(2, "zwei Trigger während der Blockade ergeben genau einen koaleszierten Nachlauf");
        sut.CountdownEndUtc.Should().NotBeNull();
    }

    /// <summary>ZustandGeaendert feuert bei Optionswechsel, Armierung und Disarmierung.</summary>
    [Fact]
    public void StateChanged_WillAtArmingDisarmingAndOptionsTriggered()
    {
        var runningSource = new FakeRunningAutomationStatusSource();
        var count = new FakeRunningAutomationsCount { Count = 0 };
        var shutdownService = new FakeSystemShutdownService();
        var timeProvider = new FakeTimeProvider();
        using var sut = CreateOrchestrator(runningSource, count, shutdownService, timeProvider);

        var eventCount = 0;
        sut.StateChanged += () => eventCount++;

        sut.SetOptions(toleranceMinutes: 7, force: true);
        eventCount.Should().BeGreaterThanOrEqualTo(1);

        var before = eventCount;
        sut.SetEnabled(true);
        eventCount.Should().BeGreaterThan(before);

        before = eventCount;
        sut.SetEnabled(false);
        eventCount.Should().BeGreaterThan(before);
    }

    /// <summary>Dispose meldet beide Events ab und stoppt beide Timer.</summary>
    [Fact]
    public void Dispose_ReportsAllEventsFromAndStopsTimer()
    {
        var runningSource = new FakeRunningAutomationStatusSource();
        var count = new FakeRunningAutomationsCount { Count = 0 };
        var shutdownService = new FakeSystemShutdownService();
        var timeProvider = new FakeTimeProvider();
        var notifier = new WorkItemRunDataChangedNotifier();
        var sut = CreateOrchestrator(runningSource, count, shutdownService, timeProvider, notifier);

        sut.SetEnabled(true);
        runningSource.SubscriberCount.Should().Be(1);

        sut.Dispose();

        runningSource.SubscriberCount.Should().Be(0);
        runningSource.Raise(previous: 1, current: 0);
        notifier.NotifyRunDataChanged(Guid.NewGuid());
        timeProvider.Advance(TimeSpan.FromMinutes(10));
        shutdownService.RequestCount.Should().Be(0);
    }

    /// <summary>
    /// Nach Dispose sind Mutationsaufrufe wirkungslose No-Ops: Sie dürfen weder den Zustand
    /// ändern noch auf die entsorgten Timer zugreifen (kein <see cref="ObjectDisposedException"/>-
    /// Risiko für späte Aufrufer).
    /// </summary>
    [Fact]
    public void MutationsAfterDispose_AreNoOpsWithoutException()
    {
        var runningSource = new FakeRunningAutomationStatusSource();
        var count = new FakeRunningAutomationsCount { Count = 0 };
        var shutdownService = new FakeSystemShutdownService();
        var timeProvider = new FakeTimeProvider();
        var sut = CreateOrchestrator(runningSource, count, shutdownService, timeProvider);

        sut.SetEnabled(true);
        sut.Dispose();

        var actEnabled = () => sut.SetEnabled(false);
        var actOptions = () => sut.SetOptions(3, true);

        actEnabled.Should().NotThrow();
        actOptions.Should().NotThrow();
        sut.IsEnabled.Should().BeTrue("der Zustand wird nach Dispose nicht mehr verändert");
        shutdownService.RequestCount.Should().Be(0);
    }

    private sealed class FakeRunningAutomationStatusSource : IRunningAutomationStatusSource
    {
        private int _runningCount;
        public event Action<int, int>? RunningCountChanged;
        public int SubscriberCount => RunningCountChanged?.GetInvocationList().Length ?? 0;

        public int GetRunningCount() => _runningCount;
        public bool IsRunning(Guid workItemId) => _runningCount > 0;
        public WorkItemRunStatus? GetLiveRunStatus(Guid workItemId)
            => _runningCount > 0 ? WorkItemRunStatus.Running : null;

        public void Raise(int previous, int current)
        {
            _runningCount = current;
            RunningCountChanged?.Invoke(previous, current);
        }
    }

    private sealed class FakeRunningAutomationsCount : IRunningAutomationsCount
    {
        private readonly SemaphoreSlim _blockierGatter = new(0);
        private int _blockedCalls;
        private int _countCalls;

        public int Count { get; set; }
        public bool ThrowOnCount { get; set; }
        public int CountCalls => _countCalls;

        /// <summary>Lässt die nächsten <paramref name="count"/> Zählaufrufe an einem Gatter hängen (bis <see cref="GetBlockingFree"/>).</summary>
        public void BlockNextCalls(int count) => _blockedCalls = count;

        /// <summary>Lässt alle am Gatter wartenden Zählaufrufe weiterlaufen.</summary>
        public void GetBlockingFree() => _blockierGatter.Release(int.MaxValue - _blockierGatter.CurrentCount);

        public async Task<int> CountAsync(CancellationToken ct = default)
        {
            Interlocked.Increment(ref _countCalls);
            if (ThrowOnCount)
            {
                throw new InvalidOperationException("Test exception from zaehlung.");
            }

            if (_blockedCalls > 0)
            {
                _blockedCalls--;
                await _blockierGatter.WaitAsync(ct);
            }

            return Count;
        }
    }

    private sealed class FakeSystemShutdownService : ISystemShutdownService
    {
        private readonly Lock _syncLock = new();
        private int _requestCount;
        public bool ThrowOnRequest { get; set; }
        public List<bool> ForceValues { get; } = new();

        public int RequestCount
        {
            get
            {
                lock (_syncLock)
                {
                    return _requestCount;
                }
            }
        }

        public Task RequestShutdownAsync(bool force, CancellationToken cancellationToken = default)
        {
            lock (_syncLock)
            {
                _requestCount++;
                ForceValues.Add(force);
            }

            if (ThrowOnRequest)
            {
                throw new InvalidOperationException("Test exception from shutdown service.");
            }

            return Task.CompletedTask;
        }
    }
}
