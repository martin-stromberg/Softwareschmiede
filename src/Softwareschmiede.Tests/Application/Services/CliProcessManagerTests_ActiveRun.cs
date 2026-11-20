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
/// Stopp eines CLI-Prozesses <see cref="WorkItem.ActiveRunId"/> über <see cref="WorkItemService"/> persistieren,
/// damit der <c>KiAusfuehrungsStatusConverter</c> die Seitenleisten-Kachel korrekt auf "▶ Läuft" umschaltet.
/// Vor dem Fix wurde <c>AktiveRunId</c> nirgends im Produktivcode gesetzt — der zugehörige E2E-Test simulierte
/// den Heartbeat direkt in der Datenbank und deckte diese Lücke daher nicht auf.
/// </summary>
public sealed class CliProcessManagerTests_ActiveRun : IDisposable
{
    private readonly string _dbName = Guid.NewGuid().ToString();
    private readonly ServiceProvider _provider;
    private readonly AiExecutionService _aiService;
    private readonly CliProcessManager _sut;
    private readonly WorkItemRunDataChangedNotifier _runDataChangedNotifier = new();
    private readonly Guid _projectId = Guid.NewGuid();
    private readonly Guid _workItemId = Guid.NewGuid();

    /// <summary>CliProcessManagerTests_AktiverLauf.</summary>
    public CliProcessManagerTests_ActiveRun()
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
        var aiScopeFactoryMock = new Mock<IServiceScopeFactory>();
        _aiService = TestAiExecutionServiceFactory.Create(aiScopeFactoryMock.Object);
        _sut = new CliProcessManager(
            _aiService,
            scopeFactory,
            NullLogger<CliProcessManager>.Instance,
            _runDataChangedNotifier);
    }

    /// <summary>Dispose.</summary>
    public void Dispose()
    {
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

    private async Task RaiseStatusChangedAsync(CliProcessStatus status)
    {
        var method = typeof(CliProcessManager).GetMethod("OnCliProcessStatusChanged", BindingFlags.NonPublic | BindingFlags.Instance)!;
        method.Invoke(_sut, new object[] { _workItemId, status });

        // OnCliProcessStatusChanged persistiert über SafeFireAndForget asynchron im Hintergrund;
        // kurze Wartezeit, bis der Fire-and-Forget-Task abgeschlossen ist.
        await PollUntilAsync(() => LoadWorkItem().ActiveRunId != null == (status == CliProcessStatus.Started));
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
    /// <see cref="WorkItem.ActiveRunId"/> setzen und <see cref="WorkItem.LastHeartbeatUtc"/> aktualisieren —
    /// ohne auf den ersten periodischen 30s-Heartbeat zu warten.
    /// </summary>
    [Fact]
    public async Task OnCliProcessStatusChanged_WithStarted_SetsActiveRunIdImmediately()
    {
        var beforeStart = DateTimeOffset.UtcNow;
        // Laufendes Handle simulieren: Der Setzen-Pfad ist per laeuftGuard abgesichert und schreibt
        // nur, solange der Prozess nachweislich läuft (Issue 342, TOCTOU).
        SchedulesRunningHandle(_workItemId);

        await RaiseStatusChangedAsync(CliProcessStatus.Started);

        var workItem = LoadWorkItem();
        workItem.ActiveRunId.Should().NotBeNull();
        workItem.LastHeartbeatUtc.Should().NotBeNull();
        workItem.LastHeartbeatUtc!.Value.Should().BeOnOrAfter(beforeStart.AddSeconds(-1));
    }

    /// <summary>
    /// Beim regulären Stopp eines CLI-Prozesses (Status <c>Stopped</c>) muss CliProcessManager
    /// <see cref="WorkItem.ActiveRunId"/> wieder entfernen, damit die Kachel nicht dauerhaft "▶ Läuft" zeigt.
    /// </summary>
    [Fact]
    public async Task OnCliProcessStatusChanged_WithStopped_RemovesActiveRunId()
    {
        SchedulesRunningHandle(_workItemId);
        await RaiseStatusChangedAsync(CliProcessStatus.Started);
        LoadWorkItem().ActiveRunId.Should().NotBeNull();

        await RaiseStatusChangedAsync(CliProcessStatus.Stopped);

        LoadWorkItem().ActiveRunId.Should().BeNull();
    }

    /// <summary>
    /// Auch bei einem Fehler-Stopp (Status <c>Fehler</c>) muss <see cref="WorkItem.ActiveRunId"/> entfernt
    /// werden — die Aufgabe bleibt zwar im Status Gestartet (CLI kann erneut gestartet werden), zeigt aber
    /// korrekt "✓ Bereit" statt fälschlich weiter "▶ Läuft".
    /// </summary>
    [Fact]
    public async Task OnCliProcessStatusChanged_WithError_RemovesActiveRunId()
    {
        SchedulesRunningHandle(_workItemId);
        await RaiseStatusChangedAsync(CliProcessStatus.Started);
        LoadWorkItem().ActiveRunId.Should().NotBeNull();

        await RaiseStatusChangedAsync(CliProcessStatus.Error);

        LoadWorkItem().ActiveRunId.Should().BeNull();
    }

    private void SchedulesRunningHandle(Guid workItemId)
    {
        var handlesField = typeof(AiExecutionService).GetField("_handles", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var handles = (System.Collections.Concurrent.ConcurrentDictionary<Guid, CliProcessHandle>)handlesField.GetValue(_aiService)!;
        handles[workItemId] = new CliProcessHandle(workItemId, System.Diagnostics.Process.GetCurrentProcess());
    }

    /// <summary>
    /// Das CliLaufAktiv-Signal (laufender Prozess ohne Gestartet-Ereignis, z. B. Wiederverwendungs-
    /// Early-Return) löst die Laufdaten-Sicherstellung aus: vollständige Laufdaten werden persistiert
    /// und LaufdatenChanged gemeldet (Issue 342, H1/H4).
    /// </summary>
    [Fact]
    public async Task CliRunActive_SetsRundataSafe_AtReuse()
    {
        SchedulesRunningHandle(_workItemId);
        LoadWorkItem().ActiveRunId.Should().BeNull("Ausgangslage: keine persistierten Laufdaten");

        var notifierFired = new ManualResetEventSlim(false);
        _runDataChangedNotifier.RunDataChanged += _ => notifierFired.Set();

        _aiService.NotifyRunActive(_workItemId);

        await PollUntilAsync(() => LoadWorkItem().ActiveRunId is not null);
        var workItem = LoadWorkItem();
        workItem.ActiveRunId.Should().NotBeNullOrEmpty();
        workItem.LastHeartbeatUtc.Should().NotBeNull();
        workItem.ExecutionStatus.Should().Be(WorkItemExecutionStatus.Active);
        notifierFired.Wait(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken).Should().BeTrue(
            "erfolgreiche Laufdaten-Persistierung meldet die UI-Aktualisierung");
    }

    /// <summary>
    /// Der Heartbeat-Tick heilt gelöschte Laufdaten bei laufendem Prozess selbst, statt nur einen
    /// No-Op-Heartbeat zu schreiben (Issue 342, H4): AktualisierungAsync ruft jetzt
    /// AktivenLaufSicherstellenAsync auf.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_RepairsDeletedRundata_AtRunningProcess()
    {
        SchedulesRunningHandle(_workItemId);
        await RaiseStatusChangedAsync(CliProcessStatus.Started);
        LoadWorkItem().ActiveRunId.Should().NotBeNull();

        // Laufdaten direkt in der DB löschen — simuliert einen verworfenen SaveChanges oder einen
        // früheren Persistierungsfehler, während der Prozess weiterläuft.
        using (var scope = _provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
            var tracked = await db.WorkItems.FindAsync(new object?[] { _workItemId }, TestContext.Current.CancellationToken);
            tracked!.ActiveRunId = null;
            tracked.LastHeartbeatUtc = null;
            tracked.RunStatus = null;
            tracked.ExecutionStatus = WorkItemExecutionStatus.Completed;
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var method = typeof(CliProcessManager).GetMethod("UpdateAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        await (Task)method.Invoke(_sut, new object[] { _workItemId })!;

        var workItem = LoadWorkItem();
        workItem.ActiveRunId.Should().NotBeNullOrEmpty("der Tick stellt die vollständigen Laufdaten wieder her");
        workItem.LastHeartbeatUtc.Should().NotBeNull();
        workItem.ExecutionStatus.Should().Be(WorkItemExecutionStatus.Active);
    }

    /// <summary>
    /// Der periodische Abgleich (AbgleichAsync) stellt für alle laufenden Aufgaben fehlende Laufdaten
    /// sicher — unabhängig von jeglicher Event-Zustellung (Issue 342, letztes Sicherheitsnetz).
    /// </summary>
    [Fact]
    public async Task ReconciliationAsync_SetsRundataSafe_ForRunningWorkItems()
    {
        SchedulesRunningHandle(_workItemId);
        LoadWorkItem().ActiveRunId.Should().BeNull();

        var method = typeof(CliProcessManager).GetMethod("ReconcileAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        await (Task)method.Invoke(_sut, Array.Empty<object>())!;

        var workItem = LoadWorkItem();
        workItem.ActiveRunId.Should().NotBeNullOrEmpty();
        workItem.LastHeartbeatUtc.Should().NotBeNull();
        workItem.ExecutionStatus.Should().Be(WorkItemExecutionStatus.Active);

        // Der Abgleich hat zusätzlich den Heartbeat-Timer (Wiederherstellung der Beobachtungskette)
        // und — bei Session — die RuntimeStatus-Subscription sichergestellt; für das gepflanzte
        // Session-lose Handle zählt hier der Timer-Nachweis.
        var heartbeatField = typeof(CliProcessManager).GetField("_heartbeatTimers", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var heartbeatTimers = (System.Collections.Concurrent.ConcurrentDictionary<Guid, Timer>)heartbeatField.GetValue(_sut)!;
        heartbeatTimers.ContainsKey(_workItemId).Should().BeTrue("der Abgleich stellt die laufende Heartbeat-Beobachtung wieder her");
    }
}
