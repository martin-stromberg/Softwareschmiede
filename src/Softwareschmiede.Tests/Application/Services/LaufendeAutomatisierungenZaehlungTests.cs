using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>
/// Tests der <see cref="LaufendeAutomatisierungenZaehlung"/>: fachliche Klassifikation über die
/// gemeinsame <see cref="KiAusfuehrungsStatusKlassifikation"/> — nur der Zustand
/// <see cref="KiAusfuehrungsZustand.Laeuft"/> zählt als „laufend".
/// </summary>
public sealed class LaufendeAutomatisierungenZaehlungTests
{
    private static LaufendeAutomatisierungenZaehlung ErstelleZaehlung(
        List<Aufgabe> aufgaben,
        IRunningAutomationStatusSource statusSource,
        out ServiceProvider provider)
        => ErstelleZaehlung(aufgaben, statusSource, out provider, out _);

    private static LaufendeAutomatisierungenZaehlung ErstelleZaehlung(
        List<Aufgabe> aufgaben,
        IRunningAutomationStatusSource statusSource,
        out ServiceProvider provider,
        out PromptZeitVersandService promptZeitVersandService)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAktiveAufgabenService>(_ => new FakeAktiveAufgabenService(aufgaben));
        provider = services.BuildServiceProvider();

        var kiService = TestKiAusfuehrungsServiceFactory.Create();
        promptZeitVersandService = new PromptZeitVersandService(
            kiService,
            TimeProvider.System,
            NullLogger<PromptZeitVersandService>.Instance);

        return new LaufendeAutomatisierungenZaehlung(
            provider.GetRequiredService<IServiceScopeFactory>(),
            statusSource,
            promptZeitVersandService);
    }

    private static Aufgabe ErstelleLaufendeAufgabe() => new()
    {
        Id = Guid.NewGuid(),
        Titel = "Laufende Aufgabe",
        Status = AufgabeStatus.Gestartet,
        AusfuehrungsStatus = AufgabeAusfuehrungsStatus.Aktiv,
        AktiveRunId = Guid.NewGuid().ToString("N"),
        LastHeartbeatUtc = DateTimeOffset.UtcNow
    };

    /// <summary>Ohne aktive Aufgaben ist die Anzahl 0.</summary>
    [Fact]
    public async Task ZaehleAsync_LiefertNull_BeiLeererKandidatenmenge()
    {
        var sut = ErstelleZaehlung([], new FakeStatusSource(), out var provider);
        using var _ = provider;

        (await sut.ZaehleAsync()).Should().Be(0);
    }

    /// <summary>Eine Aufgabe mit aktivem Lauf-Nachweis zaehlt als laufend.</summary>
    [Fact]
    public async Task ZaehleAsync_ZaehltAufgabeMitAktivemLauf()
    {
        var sut = ErstelleZaehlung([ErstelleLaufendeAufgabe()], new FakeStatusSource(), out var provider);
        using var _ = provider;

        (await sut.ZaehleAsync()).Should().Be(1);
    }

    /// <summary>Persistierter Substatus WartetAufEingabe zaehlt nicht als laufend.</summary>
    [Fact]
    public async Task ZaehleAsync_ZaehltNicht_WennPersistierterLaufstatusWartetAufEingabe()
    {
        var aufgabe = ErstelleLaufendeAufgabe();
        aufgabe.LaufStatus = AufgabeLaufStatus.WartetAufEingabe;
        var sut = ErstelleZaehlung([aufgabe], new FakeStatusSource(), out var provider);
        using var _ = provider;

        (await sut.ZaehleAsync()).Should().Be(0);
    }

    /// <summary>Eine wartende Aufgabe ohne Lauf-Nachweis zaehlt nicht.</summary>
    [Fact]
    public async Task ZaehleAsync_ZaehltNicht_BeiWartendemAufgabenstatus()
    {
        var aufgabe = ErstelleLaufendeAufgabe();
        aufgabe.Status = AufgabeStatus.Wartend;
        aufgabe.LastHeartbeatUtc = DateTimeOffset.UtcNow.AddMinutes(-10); // abgelaufener Heartbeat
        var sut = ErstelleZaehlung([aufgabe], new FakeStatusSource(), out var provider);
        using var _ = provider;

        (await sut.ZaehleAsync()).Should().Be(0);
    }

    /// <summary>Eine pausierte Aufgabe zaehlt nicht als laufend.</summary>
    [Fact]
    public async Task ZaehleAsync_ZaehltNicht_BeiPausierterAufgabe()
    {
        var aufgabe = ErstelleLaufendeAufgabe();
        aufgabe.PausiertBisUtc = DateTimeOffset.UtcNow.AddMinutes(10);
        var sut = ErstelleZaehlung([aufgabe], new FakeStatusSource(), out var provider);
        using var _ = provider;

        (await sut.ZaehleAsync()).Should().Be(0);
    }

    /// <summary>Eine beendete Aufgabe zaehlt nicht.</summary>
    [Fact]
    public async Task ZaehleAsync_ZaehltNicht_BeiBeendeterAusfuehrung()
    {
        var aufgabe = new Aufgabe
        {
            Id = Guid.NewGuid(),
            Titel = "Beendete Aufgabe",
            Status = AufgabeStatus.Beendet,
            AusfuehrungsStatus = AufgabeAusfuehrungsStatus.Beendet
        };
        var sut = ErstelleZaehlung([aufgabe], new FakeStatusSource(), out var provider);
        using var _ = provider;

        (await sut.ZaehleAsync()).Should().Be(0);
    }

    /// <summary>Live-Status Laeuft schlaegt auf Laeuft durch und zaehlt.</summary>
    [Fact]
    public async Task ZaehleAsync_ZaehltLiveStatus_LaeuftUeberPersistenzHinweg()
    {
        // Ohne Herzschlag-Nachweis wäre der persistierte Zustand "Bereit"; der Live-Substatus
        // schlägt auf Laeuft durch und muss fachlich zählen.
        var aufgabe = ErstelleLaufendeAufgabe();
        var statusSource = new FakeStatusSource();
        statusSource.LiveStatus[aufgabe.Id] = AufgabeLaufStatus.Laeuft;
        var sut = ErstelleZaehlung([aufgabe], statusSource, out var provider);
        using var _ = provider;

        (await sut.ZaehleAsync()).Should().Be(1);
    }

    /// <summary>Live-Status WartetAufEingabe zaehlt nicht (wartender Prozess blockiert nicht).</summary>
    [Fact]
    public async Task ZaehleAsync_ZaehltNicht_BeiLiveStatusWartetAufEingabe()
    {
        // Wartender physischer CLI-Prozess: fachlich "Wartet" — blockiert den Countdown nicht.
        var aufgabe = ErstelleLaufendeAufgabe();
        var statusSource = new FakeStatusSource();
        statusSource.LiveStatus[aufgabe.Id] = AufgabeLaufStatus.WartetAufEingabe;
        var sut = ErstelleZaehlung([aufgabe], statusSource, out var provider);
        using var _ = provider;

        (await sut.ZaehleAsync()).Should().Be(0);
    }

    /// <summary>
    /// Eine Aufgabe mit zeitgesteuertem Prompt in Wartestellung wird als
    /// <see cref="KiAusfuehrungsZustand.PromptInWartestellung"/> klassifiziert und zaehlt nicht —
    /// auch dann, wenn ihr persistierter Lauf-Beleg ansonsten aktiv wäre.
    /// </summary>
    [Fact]
    public async Task ZaehleAsync_ZaehltNicht_BeiPromptInWartestellung()
    {
        var aufgabe = ErstelleLaufendeAufgabe();
        var sut = ErstelleZaehlung([aufgabe], new FakeStatusSource(), out var provider, out var promptZeitVersandService);
        using var _ = provider;
        using var promptService = promptZeitVersandService;

        await promptService.SchedulePromptAsync(aufgabe.Id, "spaeter senden", DateTimeOffset.UtcNow.AddMinutes(10));
        promptService.GetScheduledPromptStatus(aufgabe.Id).Should().NotBeNull();

        (await sut.ZaehleAsync()).Should().Be(0);
    }

    /// <summary>In einer gemischten Kandidatenmenge zaehlt nur der Zustand Laeuft.</summary>
    [Fact]
    public async Task ZaehleAsync_ZaehltGemischteMengeKorrekt()
    {
        var laufend = ErstelleLaufendeAufgabe();
        var wartend = ErstelleLaufendeAufgabe();
        wartend.LaufStatus = AufgabeLaufStatus.WartetAufEingabe;
        var pausiert = ErstelleLaufendeAufgabe();
        pausiert.PausiertBisUtc = DateTimeOffset.UtcNow.AddMinutes(10);
        var bereit = new Aufgabe
        {
            Id = Guid.NewGuid(),
            Titel = "Bereite Aufgabe",
            Status = AufgabeStatus.Beendet,
            AusfuehrungsStatus = AufgabeAusfuehrungsStatus.Beendet
        };
        var sut = ErstelleZaehlung([laufend, wartend, pausiert, bereit], new FakeStatusSource(), out var provider);
        using var _ = provider;

        (await sut.ZaehleAsync()).Should().Be(1);
    }

    private sealed class FakeAktiveAufgabenService(List<Aufgabe> aufgaben) : IAktiveAufgabenService
    {
        public Task<List<Aufgabe>> GetAktiveAufgabenAsync(CancellationToken ct = default)
            => Task.FromResult(aufgaben);
    }

    private sealed class FakeStatusSource : IRunningAutomationStatusSource
    {
        public Dictionary<Guid, AufgabeLaufStatus> LiveStatus { get; } = new();
#pragma warning disable CS0067 // Event ist Teil der Schnittstelle, wird in den Tests nicht ausgelöst.
        public event Action<int, int>? RunningCountChanged;
#pragma warning restore CS0067
        public int GetRunningCount() => LiveStatus.Count;
        public bool IsRunning(Guid aufgabeId) => LiveStatus.ContainsKey(aufgabeId);
        public AufgabeLaufStatus? GetLiveLaufStatus(Guid aufgabeId)
            => LiveStatus.TryGetValue(aufgabeId, out var status) ? status : null;
    }
}
