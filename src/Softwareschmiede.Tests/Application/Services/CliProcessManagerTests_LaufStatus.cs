using System.Collections.Concurrent;
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
using Softwareschmiede.Infrastructure.Terminal;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>
/// Regressionstests für Issue 108 (Folgefehler des Rückwegs Läuft → Wartet): <see cref="CliProcessManager"/>
/// muss den Laufzeit-Substatus einer <see cref="PseudoConsoleSession"/> (<see cref="PseudoConsoleSession.RuntimeStatusChanged"/>)
/// über <see cref="AufgabeService.AktualisiereLaufStatusAsync"/> persistieren, damit
/// <c>KiAusfuehrungsStatusConverter</c> die Seitenleisten-Kachel zwischen "▶ Läuft" und "⏸ Wartet"
/// umschalten kann, während der CLI-Prozess noch lebt. Vor dem Fix wurde dieser Substatus ausschließlich
/// lokal in der <see cref="PseudoConsoleSession"/> gehalten und speiste nur die Fußzeile der
/// Aufgabendetailansicht — die Kachel blieb dauerhaft bei "▶ Läuft" stehen, solange der Prozess lief.
/// </summary>
public sealed class CliProcessManagerTests_LaufStatus : IDisposable
{
    private readonly string _dbName = Guid.NewGuid().ToString();
    private readonly ServiceProvider _provider;
    private readonly KiAusfuehrungsService _kiService;
    private readonly CliProcessManager _sut;
    private readonly Guid _projektId = Guid.NewGuid();
    private readonly Guid _aufgabeId = Guid.NewGuid();
    private readonly List<PseudoConsoleSession> _sessions = new();

    /// <summary>CliProcessManagerTests_LaufStatus.</summary>
    public CliProcessManagerTests_LaufStatus()
    {
        var services = new ServiceCollection();
        services.AddDbContext<SoftwareschmiededDbContext>(options => options.UseInMemoryDatabase(_dbName));
        services.AddScoped<AufgabeService>();
        services.AddSingleton<ILogger<AufgabeService>>(NullLogger<AufgabeService>.Instance);
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
        _kiService = TestKiAusfuehrungsServiceFactory.Create();
        _sut = new CliProcessManager(_kiService, scopeFactory, NullLogger<CliProcessManager>.Instance);
    }

    /// <summary>Dispose.</summary>
    public void Dispose()
    {
        foreach (var session in _sessions)
        {
            session.Dispose();
        }

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

    /// <summary>
    /// Legt eine <see cref="PseudoConsoleSession"/> mit einem nie endenden Output-Stream an (verhindert, dass
    /// die interne Leseschleife durch gelesene Bytes selbst <c>MarkOutputActivity()</c> auslöst und damit den
    /// für diesen Test kontrolliert herbeigeführten Statuswechsel überschreibt) und trägt sie über Reflection
    /// in das private <c>_handles</c>-Dictionary von <see cref="KiAusfuehrungsService"/> ein — genau wie es
    /// <c>StartWithPseudoConsoleAsync</c> in der echten Anwendung tut, nur ohne tatsächlich einen cmd.exe-
    /// Prozess zu starten.
    /// </summary>
    /// <param name="aufgabeId">ID der Aufgabe, für die eine Fake-Sitzung registriert werden soll.</param>
    /// <returns>Die neu angelegte, in <see cref="KiAusfuehrungsService"/> registrierte Sitzung.</returns>
    private PseudoConsoleSession RegisterFakeSessionForAufgabe(Guid aufgabeId)
    {
        var process = System.Diagnostics.Process.GetCurrentProcess();
        var session = TestPseudoConsoleSessionFactory.Create(
            new MemoryStream(),
            new NeverEndingStream(),
            TimeProvider.System,
            TimeSpan.FromMilliseconds(1),
            NullLogger.Instance);
        _sessions.Add(session);

        var handlesField = typeof(KiAusfuehrungsService).GetField("_handles", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var handles = (ConcurrentDictionary<Guid, CliProcessHandle>)handlesField.GetValue(_kiService)!;
        handles[aufgabeId] = new CliProcessHandle(aufgabeId, process) { PseudoConsoleSession = session };

        return session;
    }

    private async Task RaiseStatusChangedAsync(CliProcessStatus status, Func<Aufgabe, bool> bedingung)
    {
        var method = typeof(CliProcessManager).GetMethod("OnCliProcessStatusChanged", BindingFlags.NonPublic | BindingFlags.Instance)!;
        method.Invoke(_sut, new object[] { _aufgabeId, status });

        await PollUntilAsync(() => bedingung(LadeAufgabe()));
    }

    /// <summary>Ruft die private RefreshRuntimeStatus-Methode der Sitzung direkt auf, statt auf den echten
    /// 1-Sekunden-Timer zu warten — deterministisch und ohne künstliche Testverzögerung.</summary>
    /// <param name="session">Die Sitzung, deren Laufzeitstatus neu bewertet werden soll.</param>
    private static void ForceRuntimeStatusRefresh(PseudoConsoleSession session)
    {
        var method = typeof(PseudoConsoleSession).GetMethod("RefreshRuntimeStatus", BindingFlags.NonPublic | BindingFlags.Instance)!;
        method.Invoke(session, null);
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
    /// Voller Zyklus über die tatsächliche Produktionsverdrahtung: Gestartet setzt AktiveRunId + LaufStatus
    /// Laeuft sofort; ein anschließender RuntimeStatusChanged-Wechsel der Sitzung auf WartetAufEingabe muss
    /// bei weiterhin laufendem Prozess (AktiveRunId bleibt gesetzt) als LaufStatus persistiert werden;
    /// Gestoppt entfernt danach sowohl AktiveRunId als auch LaufStatus wieder vollständig.
    /// </summary>
    [Fact]
    public async Task VollerZyklus_GestartetWartetGestoppt_PersistiertLaufStatusUeberDieSitzung()
    {
        var session = RegisterFakeSessionForAufgabe(_aufgabeId);

        // Läuft: CliProcessManager setzt AktiveRunId + LaufStatus=Laeuft sofort beim Start und abonniert
        // die Sitzung.
        await RaiseStatusChangedAsync(CliProcessStatus.Gestartet, a => a.AktiveRunId != null);
        var nachStart = LadeAufgabe();
        nachStart.AktiveRunId.Should().NotBeNull();
        nachStart.LaufStatus.Should().Be(AufgabeLaufStatus.Laeuft);

        // Wartet: Die Sitzung erkennt fehlende I/O-Aktivität (RefreshRuntimeStatus, hier direkt statt über
        // den echten 1s-Timer ausgelöst) und wechselt auf WartetAufEingabe — der Prozess selbst lebt
        // weiterhin (Process.GetCurrentProcess() ist der Testprozess und hat nicht beendet).
        ForceRuntimeStatusRefresh(session);
        await PollUntilAsync(() => LadeAufgabe().LaufStatus == AufgabeLaufStatus.WartetAufEingabe);
        var waehrendWarten = LadeAufgabe();
        waehrendWarten.AktiveRunId.Should().NotBeNull("der Prozess lebt während des Wartens auf Eingabe weiterhin");
        waehrendWarten.LaufStatus.Should().Be(AufgabeLaufStatus.WartetAufEingabe);

        // Bereit: Prozess wird beendet — AktiveRunId und LaufStatus müssen beide entfernt werden.
        await RaiseStatusChangedAsync(CliProcessStatus.Gestoppt, a => a.AktiveRunId == null);
        var nachStopp = LadeAufgabe();
        nachStopp.AktiveRunId.Should().BeNull();
        nachStopp.LaufStatus.Should().BeNull();
    }

    /// <summary>
    /// Nach dem Stopp darf ein späterer RuntimeStatusChanged-Event der (inzwischen entsorgten) Sitzung keinen
    /// Substatus mehr persistieren — CliProcessManager muss die Event-Registrierung beim Stopp sauber
    /// abmelden, statt sie an eine nicht mehr zur Aufgabe gehörende Sitzung gebunden zu lassen.
    /// </summary>
    [Fact]
    public async Task RaisesRuntimeStatusChanged_AfterGestoppt_AktualisiertLaufStatusNichtMehr()
    {
        var session = RegisterFakeSessionForAufgabe(_aufgabeId);
        await RaiseStatusChangedAsync(CliProcessStatus.Gestartet, a => a.AktiveRunId != null);
        await RaiseStatusChangedAsync(CliProcessStatus.Gestoppt, a => a.AktiveRunId == null);

        // Verspätetes Event nach dem Stopp: darf keinen Effekt mehr haben (weder über die Abmeldung in
        // CliProcessManager, noch nachgelagert über die AktiveRunId-Prüfung in AktualisiereLaufStatusAsync).
        ForceRuntimeStatusRefresh(session);
        await Task.Delay(200);

        LadeAufgabe().LaufStatus.Should().BeNull();
    }

    /// <summary>Stream, dessen Lesevorgang niemals zurückkehrt — verhindert, dass die Leseschleife der
    /// Sitzung durch gelesene Bytes selbst Aktivität meldet und damit den in diesem Test kontrolliert
    /// herbeigeführten Statuswechsel überschreibt.</summary>
    private sealed class NeverEndingStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => 0;
        public override long Position { get; set; }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => new(new TaskCompletionSource<int>().Task);

        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
