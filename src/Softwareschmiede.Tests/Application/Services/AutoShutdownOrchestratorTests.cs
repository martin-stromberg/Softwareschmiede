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
    private static AutoShutdownOrchestrator ErstelleOrchestrator(
        FakeRunningAutomationStatusSource runningSource,
        FakeLaufendeAutomatisierungenZaehlung zaehlung,
        FakeSystemShutdownService shutdownService,
        FakeTimeProvider timeProvider,
        AufgabeLaufdatenChangedNotifier? notifier = null)
        => new(
            runningSource,
            notifier ?? new AufgabeLaufdatenChangedNotifier(),
            zaehlung,
            shutdownService,
            timeProvider,
            NullLogger<AutoShutdownOrchestrator>.Instance);

    /// <summary>Bei Aktivierung und Anzahl 0 wird der Countdown mit dem Ende now+Toleranz armiert.</summary>
    [Fact]
    public void SetEnabled_ArmiertCountdown_MitToleranzEndzeitpunkt()
    {
        var runningSource = new FakeRunningAutomationStatusSource();
        var zaehlung = new FakeLaufendeAutomatisierungenZaehlung { Count = 0 };
        var shutdownService = new FakeSystemShutdownService();
        var timeProvider = new FakeTimeProvider();
        using var sut = ErstelleOrchestrator(runningSource, zaehlung, shutdownService, timeProvider);

        sut.SetOptionen(toleranzMinuten: 5, erzwingen: false);
        sut.SetEnabled(true);

        sut.CountdownEndeUtc.Should().Be(timeProvider.GetUtcNow() + TimeSpan.FromMinutes(5));
        sut.IstAktiviert.Should().BeTrue();
        shutdownService.RequestCount.Should().Be(0);
    }

    /// <summary>Der Shutdown wird erst nach Ablauf der vollen Toleranzzeit angefordert.</summary>
    [Fact]
    public void CountdownAblauf_LoestShutdownErstNachToleranzAus()
    {
        var runningSource = new FakeRunningAutomationStatusSource();
        var zaehlung = new FakeLaufendeAutomatisierungenZaehlung { Count = 0 };
        var shutdownService = new FakeSystemShutdownService();
        var timeProvider = new FakeTimeProvider();
        using var sut = ErstelleOrchestrator(runningSource, zaehlung, shutdownService, timeProvider);

        sut.SetOptionen(toleranzMinuten: 5, erzwingen: false);
        sut.SetEnabled(true);

        timeProvider.Advance(TimeSpan.FromMinutes(4).Add(TimeSpan.FromSeconds(59)));
        shutdownService.RequestCount.Should().Be(0);

        timeProvider.Advance(TimeSpan.FromSeconds(1));
        shutdownService.RequestCount.Should().Be(1);
    }

    /// <summary>Das Erzwingen-Flag wird an den Shutdown-Service weitergereicht.</summary>
    [Fact]
    public void CountdownAblauf_UebergibtErzwingenFlag()
    {
        var runningSource = new FakeRunningAutomationStatusSource();
        var zaehlung = new FakeLaufendeAutomatisierungenZaehlung { Count = 0 };
        var shutdownService = new FakeSystemShutdownService();
        var timeProvider = new FakeTimeProvider();
        using var sut = ErstelleOrchestrator(runningSource, zaehlung, shutdownService, timeProvider);

        sut.SetOptionen(toleranzMinuten: 2, erzwingen: true);
        sut.SetEnabled(true);
        timeProvider.Advance(TimeSpan.FromMinutes(2));

        shutdownService.ErzwingenWerte.Should().Equal(true);
    }

    /// <summary>Statuswechsel bei unveraenderter Anzahl 0 setzen den Countdown nicht zurueck (kein Verhungern).</summary>
    [Fact]
    public void StatuswechselBeiUnveraenderterAnzahl_LaesstCountdownEndeUnveraendert()
    {
        var runningSource = new FakeRunningAutomationStatusSource();
        var zaehlung = new FakeLaufendeAutomatisierungenZaehlung { Count = 0 };
        var shutdownService = new FakeSystemShutdownService();
        var timeProvider = new FakeTimeProvider();
        var notifier = new AufgabeLaufdatenChangedNotifier();
        using var sut = ErstelleOrchestrator(runningSource, zaehlung, shutdownService, timeProvider, notifier);

        sut.SetOptionen(toleranzMinuten: 5, erzwingen: false);
        sut.SetEnabled(true);
        var urspruenglichesEnde = sut.CountdownEndeUtc;

        timeProvider.Advance(TimeSpan.FromMinutes(2));
        runningSource.Raise(previous: 0, current: 0);
        notifier.NotifyLaufdatenChanged(Guid.NewGuid());

        sut.CountdownEndeUtc.Should().Be(urspruenglichesEnde);
        shutdownService.RequestCount.Should().Be(0);
    }

    /// <summary>Eine positive fachliche Anzahl disarmiert den laufenden Countdown.</summary>
    [Fact]
    public void StatuswechselMitPositiverAnzahl_DisarmiertCountdown()
    {
        var runningSource = new FakeRunningAutomationStatusSource();
        var zaehlung = new FakeLaufendeAutomatisierungenZaehlung { Count = 0 };
        var shutdownService = new FakeSystemShutdownService();
        var timeProvider = new FakeTimeProvider();
        using var sut = ErstelleOrchestrator(runningSource, zaehlung, shutdownService, timeProvider);

        sut.SetOptionen(toleranzMinuten: 5, erzwingen: false);
        sut.SetEnabled(true);
        timeProvider.Advance(TimeSpan.FromMinutes(1));

        zaehlung.Count = 1;
        runningSource.Raise(previous: 0, current: 1);

        sut.CountdownEndeUtc.Should().BeNull();
        timeProvider.Advance(TimeSpan.FromMinutes(10));
        shutdownService.RequestCount.Should().Be(0);
    }

    /// <summary>Nach Rueckkehr zur Anzahl 0 wird ein neuer Countdown ueber die volle Toleranz armiert.</summary>
    [Fact]
    public void RueckkehrZuNull_ArmiertNeueVolleToleranz()
    {
        var runningSource = new FakeRunningAutomationStatusSource();
        var zaehlung = new FakeLaufendeAutomatisierungenZaehlung { Count = 0 };
        var shutdownService = new FakeSystemShutdownService();
        var timeProvider = new FakeTimeProvider();
        using var sut = ErstelleOrchestrator(runningSource, zaehlung, shutdownService, timeProvider);

        sut.SetOptionen(toleranzMinuten: 5, erzwingen: false);
        sut.SetEnabled(true);

        zaehlung.Count = 1;
        runningSource.Raise(previous: 0, current: 1);
        timeProvider.Advance(TimeSpan.FromMinutes(1));

        zaehlung.Count = 0;
        runningSource.Raise(previous: 1, current: 0);

        sut.CountdownEndeUtc.Should().Be(timeProvider.GetUtcNow() + TimeSpan.FromMinutes(5));

        timeProvider.Advance(TimeSpan.FromMinutes(4).Add(TimeSpan.FromSeconds(59)));
        shutdownService.RequestCount.Should().Be(0);
        timeProvider.Advance(TimeSpan.FromSeconds(1));
        shutdownService.RequestCount.Should().Be(1);
    }

    /// <summary>Deaktivieren disarmiert den Countdown und verhindert den Shutdown.</summary>
    [Fact]
    public void SetEnabledFalse_DisarmiertLaufendenCountdown()
    {
        var runningSource = new FakeRunningAutomationStatusSource();
        var zaehlung = new FakeLaufendeAutomatisierungenZaehlung { Count = 0 };
        var shutdownService = new FakeSystemShutdownService();
        var timeProvider = new FakeTimeProvider();
        using var sut = ErstelleOrchestrator(runningSource, zaehlung, shutdownService, timeProvider);

        sut.SetOptionen(toleranzMinuten: 5, erzwingen: false);
        sut.SetEnabled(true);
        sut.SetEnabled(false);

        sut.CountdownEndeUtc.Should().BeNull();
        sut.IstAktiviert.Should().BeFalse();
        timeProvider.Advance(TimeSpan.FromMinutes(10));
        shutdownService.RequestCount.Should().Be(0);
    }

    /// <summary>
    /// Im deaktivierten Ruhezustand (kein Feature, kein Countdown, kein ausgelöster Shutdown)
    /// ist jede Auswertung wirkungslos — Trigger dürfen dann keine DB-Zählung auslösen.
    /// </summary>
    [Fact]
    public async Task TriggerBeiDeaktiviertemRuhezustand_FuehrtKeineZaehlungAus()
    {
        var runningSource = new FakeRunningAutomationStatusSource();
        var zaehlung = new FakeLaufendeAutomatisierungenZaehlung { Count = 0 };
        var shutdownService = new FakeSystemShutdownService();
        var timeProvider = new FakeTimeProvider();
        var notifier = new AufgabeLaufdatenChangedNotifier();
        using var sut = ErstelleOrchestrator(runningSource, zaehlung, shutdownService, timeProvider, notifier);

        runningSource.Raise(previous: 0, current: 1);
        runningSource.Raise(previous: 1, current: 0);
        notifier.NotifyLaufdatenChanged(Guid.NewGuid());
        timeProvider.Advance(TimeSpan.FromMinutes(10));
        await Task.Delay(100);

        zaehlung.ZaehleAufrufe.Should().Be(0,
            "ohne aktiviertes Feature, armierten Countdown oder ausgelösten Shutdown ist die Zählung wirkungslos");

        // Gegenprobe: Dasselbe Fake zählt zuverlässig, sobald das Feature aktiviert ist.
        sut.SetEnabled(true);
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (zaehlung.ZaehleAufrufe < 1 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }

        zaehlung.ZaehleAufrufe.Should().BeGreaterThanOrEqualTo(1);
    }

    /// <summary>Eine Toleranzaenderung re-armiert den laufenden Countdown ab jetzt mit dem neuen Wert.</summary>
    [Fact]
    public void ToleranzAenderung_ReArmiertLaufendenCountdownAbJetzt()
    {
        var runningSource = new FakeRunningAutomationStatusSource();
        var zaehlung = new FakeLaufendeAutomatisierungenZaehlung { Count = 0 };
        var shutdownService = new FakeSystemShutdownService();
        var timeProvider = new FakeTimeProvider();
        using var sut = ErstelleOrchestrator(runningSource, zaehlung, shutdownService, timeProvider);

        sut.SetOptionen(toleranzMinuten: 5, erzwingen: false);
        sut.SetEnabled(true);
        timeProvider.Advance(TimeSpan.FromMinutes(4));

        sut.SetOptionen(toleranzMinuten: 2, erzwingen: false);

        sut.CountdownEndeUtc.Should().Be(timeProvider.GetUtcNow() + TimeSpan.FromMinutes(2));
        timeProvider.Advance(TimeSpan.FromMinutes(2));
        shutdownService.RequestCount.Should().Be(1);
    }

    /// <summary>Der Final-Recheck bei Ablauf verhindert den Shutdown bei positiver Anzahl.</summary>
    [Fact]
    public void PositiveAnzahlBeimCountdownAblauf_LoestKeinShutdownAus()
    {
        var runningSource = new FakeRunningAutomationStatusSource();
        var zaehlung = new FakeLaufendeAutomatisierungenZaehlung { Count = 0 };
        var shutdownService = new FakeSystemShutdownService();
        var timeProvider = new FakeTimeProvider();
        using var sut = ErstelleOrchestrator(runningSource, zaehlung, shutdownService, timeProvider);

        sut.SetOptionen(toleranzMinuten: 5, erzwingen: false);
        sut.SetEnabled(true);

        // Anzahl wird erst unmittelbar vor dem Ablauf positiv (Final-Recheck / Sicherheits-Tick).
        zaehlung.Count = 1;
        timeProvider.Advance(TimeSpan.FromMinutes(5));

        shutdownService.RequestCount.Should().Be(0);
        sut.CountdownEndeUtc.Should().BeNull();
    }

    /// <summary>Der Shutdown wird pro Null-Phase hoechstens einmal angefordert.</summary>
    [Fact]
    public void Shutdown_WirdProNullPhaseNurEinmalAusgeloest()
    {
        var runningSource = new FakeRunningAutomationStatusSource();
        var zaehlung = new FakeLaufendeAutomatisierungenZaehlung { Count = 0 };
        var shutdownService = new FakeSystemShutdownService();
        var timeProvider = new FakeTimeProvider();
        using var sut = ErstelleOrchestrator(runningSource, zaehlung, shutdownService, timeProvider);

        sut.SetOptionen(toleranzMinuten: 1, erzwingen: false);
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
    public void AusgeloesterShutdown_LoestNachZwischenphaseErneutAus()
    {
        var runningSource = new FakeRunningAutomationStatusSource();
        var zaehlung = new FakeLaufendeAutomatisierungenZaehlung { Count = 0 };
        var shutdownService = new FakeSystemShutdownService();
        var timeProvider = new FakeTimeProvider();
        using var sut = ErstelleOrchestrator(runningSource, zaehlung, shutdownService, timeProvider);

        sut.SetOptionen(toleranzMinuten: 1, erzwingen: false);
        sut.SetEnabled(true);
        timeProvider.Advance(TimeSpan.FromMinutes(1));
        shutdownService.RequestCount.Should().Be(1);

        zaehlung.Count = 1;
        runningSource.Raise(previous: 0, current: 1);
        zaehlung.Count = 0;
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
    public void AusnahmeBeimShutdownAufruf_ReArmiertCountdownFuerErneutenVersuch()
    {
        var runningSource = new FakeRunningAutomationStatusSource();
        var zaehlung = new FakeLaufendeAutomatisierungenZaehlung { Count = 0 };
        var shutdownService = new FakeSystemShutdownService { ThrowOnRequest = true };
        var timeProvider = new FakeTimeProvider();
        using var sut = ErstelleOrchestrator(runningSource, zaehlung, shutdownService, timeProvider);

        sut.SetOptionen(toleranzMinuten: 5, erzwingen: false);
        sut.SetEnabled(true);
        timeProvider.Advance(TimeSpan.FromMinutes(5));

        shutdownService.RequestCount.Should().Be(1);
        sut.CountdownEndeUtc.Should().Be(timeProvider.GetUtcNow() + TimeSpan.FromMinutes(5),
            "ein fehlgeschlagener Shutdown-Aufruf darf das Feature nicht für den Rest der Null-Phase blockieren");

        shutdownService.ThrowOnRequest = false;
        timeProvider.Advance(TimeSpan.FromMinutes(5));
        shutdownService.RequestCount.Should().Be(2);
    }

    /// <summary>Eine Exception der Zaehlung aendert den Orchestrator-Zustand nicht.</summary>
    [Fact]
    public void AusnahmeBeimZaehlen_LaesstZustandUnveraendert()
    {
        var runningSource = new FakeRunningAutomationStatusSource();
        var zaehlung = new FakeLaufendeAutomatisierungenZaehlung { Count = 0, ThrowOnZaehlen = true };
        var shutdownService = new FakeSystemShutdownService();
        var timeProvider = new FakeTimeProvider();
        using var sut = ErstelleOrchestrator(runningSource, zaehlung, shutdownService, timeProvider);

        var act = () => sut.SetEnabled(true);
        act.Should().NotThrow();
        sut.CountdownEndeUtc.Should().BeNull();
        shutdownService.RequestCount.Should().Be(0);
    }

    /// <summary>
    /// Wirft der Final-Recheck beim Countdown-Ablauf, bleibt kein toter Countdown zurück:
    /// Der Endzeitpunkt wird zurückgesetzt und der nächste Sicherheitstick armiert erneut.
    /// </summary>
    [Fact]
    public void AusnahmeImFinalRecheck_ReArmiertUeberSicherheitstick()
    {
        var runningSource = new FakeRunningAutomationStatusSource();
        var zaehlung = new FakeLaufendeAutomatisierungenZaehlung { Count = 0 };
        var shutdownService = new FakeSystemShutdownService();
        var timeProvider = new FakeTimeProvider();
        using var sut = ErstelleOrchestrator(runningSource, zaehlung, shutdownService, timeProvider);

        sut.SetOptionen(toleranzMinuten: 5, erzwingen: false);
        sut.SetEnabled(true);

        zaehlung.ThrowOnZaehlen = true;
        timeProvider.Advance(TimeSpan.FromMinutes(5));

        sut.CountdownEndeUtc.Should().BeNull("der abgelaufene One-Shot-Timer darf keinen toten Countdown hinterlassen");
        shutdownService.RequestCount.Should().Be(0);

        zaehlung.ThrowOnZaehlen = false;
        timeProvider.Advance(TimeSpan.FromSeconds(5));

        sut.CountdownEndeUtc.Should().Be(timeProvider.GetUtcNow() + TimeSpan.FromMinutes(5));

        timeProvider.Advance(TimeSpan.FromMinutes(5));
        shutdownService.RequestCount.Should().Be(1);
    }

    /// <summary>
    /// Trigger, die während einer laufenden Auswertung eintreffen, werden auf genau einen
    /// Nachlauf koalesziert — kein verlorener Auftrag, kein Sturm einzelner Auswertungen.
    /// </summary>
    [Fact]
    public async Task TriggerWaehrendLaufenderAuswertung_WerdenAufEinenNachlaufKoalesziert()
    {
        var runningSource = new FakeRunningAutomationStatusSource();
        var zaehlung = new FakeLaufendeAutomatisierungenZaehlung { Count = 0 };
        var shutdownService = new FakeSystemShutdownService();
        var timeProvider = new FakeTimeProvider();
        using var sut = ErstelleOrchestrator(runningSource, zaehlung, shutdownService, timeProvider);

        zaehlung.BlockiereNaechsteAufrufe(1);
        sut.SetEnabled(true);

        // Warten, bis die erste Auswertung im blockierten Zaehlaufruf angekommen ist.
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (zaehlung.ZaehleAufrufe < 1 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }

        zaehlung.ZaehleAufrufe.Should().Be(1);

        runningSource.Raise(previous: 0, current: 0);
        runningSource.Raise(previous: 0, current: 0);
        zaehlung.GibBlockierungFrei();

        deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (zaehlung.ZaehleAufrufe < 2 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }

        await Task.Delay(100);

        zaehlung.ZaehleAufrufe.Should().Be(2, "zwei Trigger während der Blockade ergeben genau einen koaleszierten Nachlauf");
        sut.CountdownEndeUtc.Should().NotBeNull();
    }

    /// <summary>ZustandGeaendert feuert bei Optionswechsel, Armierung und Disarmierung.</summary>
    [Fact]
    public void ZustandGeaendert_WirdBeiArmierungDisarmierungUndOptionenAusgeloest()
    {
        var runningSource = new FakeRunningAutomationStatusSource();
        var zaehlung = new FakeLaufendeAutomatisierungenZaehlung { Count = 0 };
        var shutdownService = new FakeSystemShutdownService();
        var timeProvider = new FakeTimeProvider();
        using var sut = ErstelleOrchestrator(runningSource, zaehlung, shutdownService, timeProvider);

        var eventCount = 0;
        sut.ZustandGeaendert += () => eventCount++;

        sut.SetOptionen(toleranzMinuten: 7, erzwingen: true);
        eventCount.Should().BeGreaterThanOrEqualTo(1);

        var vorher = eventCount;
        sut.SetEnabled(true);
        eventCount.Should().BeGreaterThan(vorher);

        vorher = eventCount;
        sut.SetEnabled(false);
        eventCount.Should().BeGreaterThan(vorher);
    }

    /// <summary>Dispose meldet beide Events ab und stoppt beide Timer.</summary>
    [Fact]
    public void Dispose_MeldetAlleEreignisseAbUndStopptTimer()
    {
        var runningSource = new FakeRunningAutomationStatusSource();
        var zaehlung = new FakeLaufendeAutomatisierungenZaehlung { Count = 0 };
        var shutdownService = new FakeSystemShutdownService();
        var timeProvider = new FakeTimeProvider();
        var notifier = new AufgabeLaufdatenChangedNotifier();
        var sut = ErstelleOrchestrator(runningSource, zaehlung, shutdownService, timeProvider, notifier);

        sut.SetEnabled(true);
        runningSource.SubscriberCount.Should().Be(1);

        sut.Dispose();

        runningSource.SubscriberCount.Should().Be(0);
        runningSource.Raise(previous: 1, current: 0);
        notifier.NotifyLaufdatenChanged(Guid.NewGuid());
        timeProvider.Advance(TimeSpan.FromMinutes(10));
        shutdownService.RequestCount.Should().Be(0);
    }

    /// <summary>
    /// Nach Dispose sind Mutationsaufrufe wirkungslose No-Ops: Sie dürfen weder den Zustand
    /// ändern noch auf die entsorgten Timer zugreifen (kein <see cref="ObjectDisposedException"/>-
    /// Risiko für späte Aufrufer).
    /// </summary>
    [Fact]
    public void MutationenNachDispose_SindNoOpsOhneAusnahme()
    {
        var runningSource = new FakeRunningAutomationStatusSource();
        var zaehlung = new FakeLaufendeAutomatisierungenZaehlung { Count = 0 };
        var shutdownService = new FakeSystemShutdownService();
        var timeProvider = new FakeTimeProvider();
        var sut = ErstelleOrchestrator(runningSource, zaehlung, shutdownService, timeProvider);

        sut.SetEnabled(true);
        sut.Dispose();

        var actEnabled = () => sut.SetEnabled(false);
        var actOptionen = () => sut.SetOptionen(3, true);

        actEnabled.Should().NotThrow();
        actOptionen.Should().NotThrow();
        sut.IstAktiviert.Should().BeTrue("der Zustand wird nach Dispose nicht mehr verändert");
        shutdownService.RequestCount.Should().Be(0);
    }

    private sealed class FakeRunningAutomationStatusSource : IRunningAutomationStatusSource
    {
        private int _runningCount;
        public event Action<int, int>? RunningCountChanged;
        public int SubscriberCount => RunningCountChanged?.GetInvocationList().Length ?? 0;

        public int GetRunningCount() => _runningCount;
        public bool IsRunning(Guid aufgabeId) => _runningCount > 0;
        public AufgabeLaufStatus? GetLiveLaufStatus(Guid aufgabeId)
            => _runningCount > 0 ? AufgabeLaufStatus.Laeuft : null;

        public void Raise(int previous, int current)
        {
            _runningCount = current;
            RunningCountChanged?.Invoke(previous, current);
        }
    }

    private sealed class FakeLaufendeAutomatisierungenZaehlung : ILaufendeAutomatisierungenZaehlung
    {
        private readonly SemaphoreSlim _blockierGatter = new(0);
        private int _blockierteAufrufe;
        private int _zaehleAufrufe;

        public int Count { get; set; }
        public bool ThrowOnZaehlen { get; set; }
        public int ZaehleAufrufe => _zaehleAufrufe;

        /// <summary>Lässt die nächsten <paramref name="anzahl"/> Zählaufrufe an einem Gatter hängen (bis <see cref="GibBlockierungFrei"/>).</summary>
        public void BlockiereNaechsteAufrufe(int anzahl) => _blockierteAufrufe = anzahl;

        /// <summary>Lässt alle am Gatter wartenden Zählaufrufe weiterlaufen.</summary>
        public void GibBlockierungFrei() => _blockierGatter.Release(int.MaxValue - _blockierGatter.CurrentCount);

        public async Task<int> ZaehleAsync(CancellationToken ct = default)
        {
            Interlocked.Increment(ref _zaehleAufrufe);
            if (ThrowOnZaehlen)
            {
                throw new InvalidOperationException("Test exception from zaehlung.");
            }

            if (_blockierteAufrufe > 0)
            {
                _blockierteAufrufe--;
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
        public List<bool> ErzwingenWerte { get; } = new();

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

        public Task RequestShutdownAsync(bool erzwingen, CancellationToken cancellationToken = default)
        {
            lock (_syncLock)
            {
                _requestCount++;
                ErzwingenWerte.Add(erzwingen);
            }

            if (ThrowOnRequest)
            {
                throw new InvalidOperationException("Test exception from shutdown service.");
            }

            return Task.CompletedTask;
        }
    }
}
