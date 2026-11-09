using System.Reflection;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Infrastructure.Data;

using Softwareschmiede.Tests.Helpers;
namespace Softwareschmiede.Tests.Application.Services;

/// <summary>
/// Regressionstests für Issue 108 (Kundenrückmeldung): <see cref="CliProcessManager"/> muss beim Start und
/// Stopp eines CLI-Prozesses <see cref="Aufgabe.AktiveRunId"/> über <see cref="AufgabeService"/> persistieren,
/// damit der <c>KiAusfuehrungsStatusConverter</c> die Seitenleisten-Kachel korrekt auf "▶ Läuft" umschaltet.
/// Vor dem Fix wurde <c>AktiveRunId</c> nirgends im Produktivcode gesetzt — der zugehörige E2E-Test simulierte
/// den Heartbeat direkt in der Datenbank und deckte diese Lücke daher nicht auf.
/// </summary>
public sealed class CliProcessManagerTests_AktiverLauf : IDisposable
{
    private readonly string _dbName = Guid.NewGuid().ToString();
    private readonly ServiceProvider _provider;
    private readonly KiAusfuehrungsService _kiService;
    private readonly CliProcessManager _sut;
    private readonly AufgabeLaufdatenChangedNotifier _laufdatenChangedNotifier = new();
    private readonly Guid _projektId = Guid.NewGuid();
    private readonly Guid _aufgabeId = Guid.NewGuid();

    /// <summary>CliProcessManagerTests_AktiverLauf.</summary>
    public CliProcessManagerTests_AktiverLauf()
    {
        var services = new ServiceCollection();
        services.AddDbContext<SoftwareschmiededDbContext>(options => options.UseInMemoryDatabase(_dbName));
        services.AddScoped<AufgabeService>();
        services.AddSingleton<ILogger<AufgabeService>>(NullLogger<AufgabeService>.Instance);
        services.AddScoped<TodoService>();
        services.AddSingleton<ILogger<TodoService>>(NullLogger<TodoService>.Instance);
        _provider = services.BuildServiceProvider();

        using (var scope = _provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
            db.Database.EnsureCreated();
            db.Projekte.Add(new Projekt
            {
                Id = _projektId,
                Name = "Testprojekt",
                ErstellungsDatum = DateTimeOffset.UtcNow,
                Status = ProjektStatus.Aktiv
            });
            db.Aufgaben.Add(new Aufgabe
            {
                Id = _aufgabeId,
                ProjektId = _projektId,
                Titel = "CLI-Prozess-Aufgabe",
                Status = AufgabeStatus.Gestartet,
                ErstellungsDatum = DateTimeOffset.UtcNow
            });
            db.SaveChanges();
        }

        var scopeFactory = _provider.GetRequiredService<IServiceScopeFactory>();
        var kiScopeFactoryMock = new Mock<IServiceScopeFactory>();
        _kiService = TestKiAusfuehrungsServiceFactory.Create(kiScopeFactoryMock.Object);
        _sut = new CliProcessManager(
            _kiService,
            scopeFactory,
            NullLogger<CliProcessManager>.Instance,
            _laufdatenChangedNotifier);
    }

    /// <summary>Dispose.</summary>
    public void Dispose()
    {
        _sut.Dispose();
        _kiService.Dispose();
        _provider.Dispose();
    }

    private Aufgabe LadeAufgabe()
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        return db.Aufgaben.AsNoTracking().Single(a => a.Id == _aufgabeId);
    }

    private async Task RaiseStatusChangedAsync(CliProcessStatus status)
    {
        var method = typeof(CliProcessManager).GetMethod("OnCliProcessStatusChanged", BindingFlags.NonPublic | BindingFlags.Instance)!;
        method.Invoke(_sut, new object[] { _aufgabeId, status });

        // OnCliProcessStatusChanged persistiert über SafeFireAndForget asynchron im Hintergrund;
        // kurze Wartezeit, bis der Fire-and-Forget-Task abgeschlossen ist.
        await PollUntilAsync(() => LadeAufgabe().AktiveRunId != null == (status == CliProcessStatus.Gestartet));
    }

    private static async Task PollUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
                return;
            await Task.Delay(50);
        }
    }

    /// <summary>
    /// Beim Start eines CLI-Prozesses (Status <c>Gestartet</c>) muss CliProcessManager sofort
    /// <see cref="Aufgabe.AktiveRunId"/> setzen und <see cref="Aufgabe.LastHeartbeatUtc"/> aktualisieren —
    /// ohne auf den ersten periodischen 30s-Heartbeat zu warten.
    /// </summary>
    [Fact]
    public async Task OnCliProcessStatusChanged_WithGestartet_SetztAktiveRunIdSofort()
    {
        var vorStart = DateTimeOffset.UtcNow;
        // Laufendes Handle simulieren: Der Setzen-Pfad ist per laeuftGuard abgesichert und schreibt
        // nur, solange der Prozess nachweislich läuft (Issue 342, TOCTOU).
        PlantLaufendesHandle(_aufgabeId);

        await RaiseStatusChangedAsync(CliProcessStatus.Gestartet);

        var aufgabe = LadeAufgabe();
        aufgabe.AktiveRunId.Should().NotBeNull();
        aufgabe.LastHeartbeatUtc.Should().NotBeNull();
        aufgabe.LastHeartbeatUtc!.Value.Should().BeOnOrAfter(vorStart.AddSeconds(-1));
    }

    /// <summary>
    /// Beim regulären Stopp eines CLI-Prozesses (Status <c>Gestoppt</c>) muss CliProcessManager
    /// <see cref="Aufgabe.AktiveRunId"/> wieder entfernen, damit die Kachel nicht dauerhaft "▶ Läuft" zeigt.
    /// </summary>
    [Fact]
    public async Task OnCliProcessStatusChanged_WithGestoppt_EntferntAktiveRunId()
    {
        PlantLaufendesHandle(_aufgabeId);
        await RaiseStatusChangedAsync(CliProcessStatus.Gestartet);
        LadeAufgabe().AktiveRunId.Should().NotBeNull();

        await RaiseStatusChangedAsync(CliProcessStatus.Gestoppt);

        LadeAufgabe().AktiveRunId.Should().BeNull();
    }

    /// <summary>
    /// Auch bei einem Fehler-Stopp (Status <c>Fehler</c>) muss <see cref="Aufgabe.AktiveRunId"/> entfernt
    /// werden — die Aufgabe bleibt zwar im Status Gestartet (CLI kann erneut gestartet werden), zeigt aber
    /// korrekt "✓ Bereit" statt fälschlich weiter "▶ Läuft".
    /// </summary>
    [Fact]
    public async Task OnCliProcessStatusChanged_WithFehler_EntferntAktiveRunId()
    {
        PlantLaufendesHandle(_aufgabeId);
        await RaiseStatusChangedAsync(CliProcessStatus.Gestartet);
        LadeAufgabe().AktiveRunId.Should().NotBeNull();

        await RaiseStatusChangedAsync(CliProcessStatus.Fehler);

        LadeAufgabe().AktiveRunId.Should().BeNull();
    }

    private void PlantLaufendesHandle(Guid aufgabeId)
    {
        var handlesField = typeof(KiAusfuehrungsService).GetField("_handles", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var handles = (System.Collections.Concurrent.ConcurrentDictionary<Guid, CliProcessHandle>)handlesField.GetValue(_kiService)!;
        handles[aufgabeId] = new CliProcessHandle(aufgabeId, System.Diagnostics.Process.GetCurrentProcess());
    }

    /// <summary>
    /// Das CliLaufAktiv-Signal (laufender Prozess ohne Gestartet-Ereignis, z. B. Wiederverwendungs-
    /// Early-Return) löst die Laufdaten-Sicherstellung aus: vollständige Laufdaten werden persistiert
    /// und LaufdatenChanged gemeldet (Issue 342, H1/H4).
    /// </summary>
    [Fact]
    public async Task CliLaufAktiv_StelltLaufdatenSicher_BeiWiederverwendung()
    {
        PlantLaufendesHandle(_aufgabeId);
        LadeAufgabe().AktiveRunId.Should().BeNull("Ausgangslage: keine persistierten Laufdaten");

        var notifierFired = new ManualResetEventSlim(false);
        _laufdatenChangedNotifier.LaufdatenChanged += _ => notifierFired.Set();

        _kiService.NotifyLaufAktiv(_aufgabeId);

        await PollUntilAsync(() => LadeAufgabe().AktiveRunId is not null);
        var aufgabe = LadeAufgabe();
        aufgabe.AktiveRunId.Should().NotBeNullOrEmpty();
        aufgabe.LastHeartbeatUtc.Should().NotBeNull();
        aufgabe.AusfuehrungsStatus.Should().Be(AufgabeAusfuehrungsStatus.Aktiv);
        notifierFired.Wait(TimeSpan.FromSeconds(2)).Should().BeTrue(
            "erfolgreiche Laufdaten-Persistierung meldet die UI-Aktualisierung");
    }

    /// <summary>
    /// Der Heartbeat-Tick heilt gelöschte Laufdaten bei laufendem Prozess selbst, statt nur einen
    /// No-Op-Heartbeat zu schreiben (Issue 342, H4): AktualisierungAsync ruft jetzt
    /// AktivenLaufSicherstellenAsync auf.
    /// </summary>
    [Fact]
    public async Task AktualisierungAsync_HeiltGeloeschteLaufdaten_BeiLaufendemProzess()
    {
        PlantLaufendesHandle(_aufgabeId);
        await RaiseStatusChangedAsync(CliProcessStatus.Gestartet);
        LadeAufgabe().AktiveRunId.Should().NotBeNull();

        // Laufdaten direkt in der DB löschen — simuliert einen verworfenen SaveChanges oder einen
        // früheren Persistierungsfehler, während der Prozess weiterläuft.
        using (var scope = _provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
            var tracked = await db.Aufgaben.FindAsync(_aufgabeId);
            tracked!.AktiveRunId = null;
            tracked.LastHeartbeatUtc = null;
            tracked.LaufStatus = null;
            tracked.AusfuehrungsStatus = AufgabeAusfuehrungsStatus.Beendet;
            await db.SaveChangesAsync();
        }

        var method = typeof(CliProcessManager).GetMethod("AktualisierungAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        await (Task)method.Invoke(_sut, new object[] { _aufgabeId })!;

        var aufgabe = LadeAufgabe();
        aufgabe.AktiveRunId.Should().NotBeNullOrEmpty("der Tick stellt die vollständigen Laufdaten wieder her");
        aufgabe.LastHeartbeatUtc.Should().NotBeNull();
        aufgabe.AusfuehrungsStatus.Should().Be(AufgabeAusfuehrungsStatus.Aktiv);
    }

    /// <summary>
    /// Der periodische Abgleich (AbgleichAsync) stellt für alle laufenden Aufgaben fehlende Laufdaten
    /// sicher — unabhängig von jeglicher Event-Zustellung (Issue 342, letztes Sicherheitsnetz).
    /// </summary>
    [Fact]
    public async Task AbgleichAsync_StelltLaufdatenSicher_FuerLaufendeAufgaben()
    {
        PlantLaufendesHandle(_aufgabeId);
        LadeAufgabe().AktiveRunId.Should().BeNull();

        var method = typeof(CliProcessManager).GetMethod("AbgleichAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        await (Task)method.Invoke(_sut, Array.Empty<object>())!;

        var aufgabe = LadeAufgabe();
        aufgabe.AktiveRunId.Should().NotBeNullOrEmpty();
        aufgabe.LastHeartbeatUtc.Should().NotBeNull();
        aufgabe.AusfuehrungsStatus.Should().Be(AufgabeAusfuehrungsStatus.Aktiv);

        // Der Abgleich hat zusätzlich den Heartbeat-Timer (Wiederherstellung der Beobachtungskette)
        // und — bei Session — die RuntimeStatus-Subscription sichergestellt; für das gepflanzte
        // Session-lose Handle zählt hier der Timer-Nachweis.
        var heartbeatField = typeof(CliProcessManager).GetField("_heartbeatTimers", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var heartbeatTimers = (System.Collections.Concurrent.ConcurrentDictionary<Guid, Timer>)heartbeatField.GetValue(_sut)!;
        heartbeatTimers.ContainsKey(_aufgabeId).Should().BeTrue("der Abgleich stellt die laufende Heartbeat-Beobachtung wieder her");
    }
}
