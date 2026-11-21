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
/// über <see cref="WorkItemService.EnsureActiveRunAsync"/> persistieren, damit
/// <c>KiAusfuehrungsStatusConverter</c> die Seitenleisten-Kachel zwischen "▶ Läuft" und "⏸ Wartet"
/// umschalten kann, während der CLI-Prozess noch lebt. Vor dem Fix wurde dieser Substatus ausschließlich
/// lokal in der <see cref="PseudoConsoleSession"/> gehalten und speiste nur die Fußzeile der
/// Aufgabendetailansicht — die Kachel blieb dauerhaft bei "▶ Läuft" stehen, solange der Prozess lief.
/// </summary>
public sealed class CliProcessManagerTests_RunStatus : IDisposable
{
    private readonly string _dbName = Guid.NewGuid().ToString();
    private readonly ServiceProvider _provider;
    private readonly AiExecutionService _aiService;
    private readonly CliProcessManager _sut;
    private readonly Guid _projectId = Guid.NewGuid();
    private readonly Guid _workItemId = Guid.NewGuid();
    private readonly List<PseudoConsoleSession> _sessions = new();

    /// <summary>CliProcessManagerTests_LaufStatus.</summary>
    public CliProcessManagerTests_RunStatus()
    {
        var services = new ServiceCollection();
        services.AddDbContext<SoftwareschmiedeDbContext>(options => options.UseInMemoryDatabase(_dbName));
        services.AddScoped<WorkItemService>();
        services.AddSingleton<ILogger<WorkItemService>>(NullLogger<WorkItemService>.Instance);
        services.AddScoped<TodoService>();
        services.AddSingleton<ILogger<TodoService>>(NullLogger<TodoService>.Instance);
        _provider = services.BuildServiceProvider();

        using (var scope = _provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
            db.Database.EnsureCreated();
            db.Projects.Add(new Project
            {
                Id = _projectId,
                Name = "Testprojekt",
                CreatedAt = DateTimeOffset.UtcNow,
                Status = ProjectStatus.Active
            });
            db.WorkItems.Add(new WorkItem
            {
                Id = _workItemId,
                ProjectId = _projectId,
                Title = "CLI-Prozess-Aufgabe",
                Status = WorkItemStatus.Started,
                CreatedAt = DateTimeOffset.UtcNow
            });
            db.SaveChanges();
        }

        var scopeFactory = _provider.GetRequiredService<IServiceScopeFactory>();
        _aiService = TestAiExecutionServiceFactory.Create();
        _sut = new CliProcessManager(
            _aiService,
            scopeFactory,
            NullLogger<CliProcessManager>.Instance,
            new WorkItemRunDataChangedNotifier());
    }

    /// <summary>Dispose.</summary>
    public void Dispose()
    {
        foreach (var session in _sessions)
        {
            session.Dispose();
        }

        _sut.Dispose();
        _aiService.Dispose();
        _provider.Dispose();
    }

    private WorkItem LoadWorkItem()
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        return db.WorkItems.AsNoTracking().Single(a => a.Id == _workItemId);
    }

    /// <summary>
    /// Legt eine <see cref="PseudoConsoleSession"/> mit einem nie endenden Output-Stream an (verhindert, dass
    /// die interne Leseschleife durch gelesene Bytes selbst <c>MarkOutputActivity()</c> auslöst und damit den
    /// für diesen Test kontrolliert herbeigeführten Statuswechsel überschreibt) und trägt sie über Reflection
    /// in das private <c>_handles</c>-Dictionary von <see cref="AiExecutionService"/> ein — genau wie es
    /// <c>StartTerminalSessionAsync</c> in der echten Anwendung tut, nur ohne tatsächlich einen cmd.exe-
    /// Prozess zu starten.
    /// </summary>
    /// <param name="workItemId">ID der Aufgabe, für die eine Fake-Sitzung registriert werden soll.</param>
    /// <returns>Die neu angelegte, in <see cref="AiExecutionService"/> registrierte Sitzung.</returns>
    private PseudoConsoleSession RegisterFakeSessionForWorkItem(Guid workItemId)
    {
        var process = System.Diagnostics.Process.GetCurrentProcess();
        var session = TestPseudoConsoleSessionFactory.Create(
            new MemoryStream(),
            new NeverEndingStream(),
            TimeProvider.System,
            TimeSpan.FromMilliseconds(1),
            NullLogger.Instance);
        _sessions.Add(session);

        var handlesField = typeof(AiExecutionService).GetField("_handles", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var handles = (ConcurrentDictionary<Guid, CliProcessHandle>)handlesField.GetValue(_aiService)!;
        handles[workItemId] = new CliProcessHandle(workItemId, process) { Session = session };

        return session;
    }

    private async Task RaiseStatusChangedAsync(CliProcessStatus status, Func<WorkItem, bool> condition)
    {
        var method = typeof(CliProcessManager).GetMethod("OnCliProcessStatusChanged", BindingFlags.NonPublic | BindingFlags.Instance)!;
        method.Invoke(_sut, new object[] { _workItemId, status });

        await PollUntilAsync(() => condition(LoadWorkItem()));
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
    /// Voller Zyklus über die tatsächliche Produktionsverdrahtung: Gestartet setzt ActiveRunId + LaufStatus
    /// Laeuft sofort; ein anschließender RuntimeStatusChanged-Wechsel der Sitzung auf WartetAufEingabe muss
    /// bei weiterhin laufendem Prozess (ActiveRunId bleibt gesetzt) als LaufStatus persistiert werden;
    /// Stopped entfernt danach sowohl ActiveRunId als auch LaufStatus wieder vollständig.
    /// </summary>
    [Fact]
    public async Task FullCycle_StartedWaitsStopped_PersistedRunStatusOverTheSession()
    {
        var session = RegisterFakeSessionForWorkItem(_workItemId);

        // Läuft: CliProcessManager setzt ActiveRunId + LaufStatus=Laeuft sofort beim Start und abonniert
        // die Sitzung.
        await RaiseStatusChangedAsync(CliProcessStatus.Started, a => a.ActiveRunId != null);
        var afterStart = LoadWorkItem();
        afterStart.ActiveRunId.Should().NotBeNull();
        afterStart.RunStatus.Should().Be(WorkItemRunStatus.Running);

        // Wartet: Die Sitzung erkennt fehlende I/O-Aktivität (RefreshRuntimeStatus, hier direkt statt über
        // den echten 1s-Timer ausgelöst) und wechselt auf WartetAufEingabe — der Prozess selbst lebt
        // weiterhin (Process.GetCurrentProcess() ist der Testprozess und hat nicht beendet).
        ForceRuntimeStatusRefresh(session);
        await PollUntilAsync(() => LoadWorkItem().RunStatus == WorkItemRunStatus.WaitingForInput);
        var whileWait = LoadWorkItem();
        whileWait.ActiveRunId.Should().NotBeNull("der Prozess lebt während des Wartens auf Eingabe weiterhin");
        whileWait.RunStatus.Should().Be(WorkItemRunStatus.WaitingForInput);

        // Bereit: Prozess wird beendet — ActiveRunId und LaufStatus müssen beide entfernt werden.
        await RaiseStatusChangedAsync(CliProcessStatus.Stopped, a => a.ActiveRunId == null);
        var afterStop = LoadWorkItem();
        afterStop.ActiveRunId.Should().BeNull();
        afterStop.RunStatus.Should().BeNull();
    }

    /// <summary>
    /// Nach dem Stopp darf ein späterer RuntimeStatusChanged-Event der (inzwischen entsorgten) Sitzung keinen
    /// Substatus mehr persistieren — CliProcessManager muss die Event-Registrierung beim Stopp sauber
    /// abmelden, statt sie an eine nicht mehr zur Aufgabe gehörende Sitzung gebunden zu lassen.
    /// </summary>
    [Fact]
    public async Task RaisesRuntimeStatusChanged_AfterStopped_UpdatesRunStatusNotMore()
    {
        var session = RegisterFakeSessionForWorkItem(_workItemId);
        await RaiseStatusChangedAsync(CliProcessStatus.Started, a => a.ActiveRunId != null);
        await RaiseStatusChangedAsync(CliProcessStatus.Stopped, a => a.ActiveRunId == null);

        // Verspätetes Event nach dem Stopp: darf keinen Effekt mehr haben (weder über die Abmeldung in
        // CliProcessManager, noch nachgelagert über den IsRunning-Guard der Laufdaten-Sicherstellung).
        ForceRuntimeStatusRefresh(session);
        await Task.Delay(200, TestContext.Current.CancellationToken);

        LoadWorkItem().RunStatus.Should().BeNull();
    }

    /// <summary>
    /// Auch ein RuntimeStatusChanged-Pfad heilt fehlende Laufdaten bei laufendem Prozess: Ist kein
    /// Gestartet-Ereignis gelaufen (z. B. Wiederverwendungs-Early-Return), stellt das Ereignis die
    /// vollständigen Laufdaten inkl. Substatus her statt nur den Substatus zu schreiben (Issue 342, H2).
    /// </summary>
    [Fact]
    public async Task OnRuntimeStatusChanged_RepairsMissingActiveRunId()
    {
        var session = RegisterFakeSessionForWorkItem(_workItemId);
        LoadWorkItem().ActiveRunId.Should().BeNull("Ausgangslage: keine Gestartet-Persistierung");

        // Session deterministisch in den Wartezustand bringen (Schwelle: 1 ms), damit die
        // Selbstheilung den Live-Substatus WartetAufEingabe persistiert.
        await Task.Delay(50, TestContext.Current.CancellationToken);
        ForceRuntimeStatusRefresh(session);

        var method = typeof(CliProcessManager).GetMethod("OnRuntimeStatusChanged", BindingFlags.NonPublic | BindingFlags.Instance)!;
        method.Invoke(_sut, new object[] { _workItemId, CliRuntimeStatus.WaitingForInput });

        await PollUntilAsync(() => LoadWorkItem().ActiveRunId is not null);
        var workItem = LoadWorkItem();
        workItem.ActiveRunId.Should().NotBeNullOrEmpty();
        workItem.LastHeartbeatUtc.Should().NotBeNull();
        workItem.ExecutionStatus.Should().Be(WorkItemExecutionStatus.Active);
        workItem.RunStatus.Should().Be(WorkItemRunStatus.WaitingForInput);
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
