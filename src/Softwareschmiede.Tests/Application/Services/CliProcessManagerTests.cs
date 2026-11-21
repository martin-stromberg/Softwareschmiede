using System.Collections.Concurrent;
using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Infrastructure.Terminal;

using Softwareschmiede.Tests.Helpers;
namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Tests für den Concurrency-Schutz von CliProcessManager.AktualisierungAsync.</summary>
public sealed class CliProcessManagerTests : IDisposable
{
    private readonly AiExecutionService _aiService;
    private readonly CliProcessManager _sut;

    /// <summary>CliProcessManagerTests.</summary>
    public CliProcessManagerTests()
    {
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        _aiService = TestAiExecutionServiceFactory.Create(scopeFactoryMock.Object);
        _sut = new CliProcessManager(
            _aiService,
            scopeFactoryMock.Object,
            NullLogger<CliProcessManager>.Instance,
            new WorkItemRunDataChangedNotifier());
    }

    /// <summary>Dispose.</summary>
    public void Dispose()
    {
        _sut.Dispose();
        _aiService.Dispose();
    }

    /// <summary>
    /// AktualisierungAsync muss pro Aufgabe serialisieren: solange die Semaphore der Aufgabe belegt ist, darf ein
    /// weiterer Aufruf für dieselbe Aufgabe nicht fortschreiten. Erst nach Freigabe der Semaphore läuft der Aufruf weiter.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_WithConcurrentTimerTicks_Serializes()
    {
        var workItemId = Guid.NewGuid();
        _sut.StartHeartbeat(workItemId);

        var semaphoresField = typeof(CliProcessManager).GetField("_updateSemaphores", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var semaphores = (ConcurrentDictionary<Guid, SemaphoreSlim>)semaphoresField.GetValue(_sut)!;
        var semaphore = semaphores[workItemId];
        await semaphore.WaitAsync(TestContext.Current.CancellationToken);

        var method = typeof(CliProcessManager).GetMethod("UpdateAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var task = (Task)method.Invoke(_sut, new object[] { workItemId })!;

        await Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);
        task.IsCompleted.Should().BeFalse("AktualisierungAsync muss blockieren, solange die Semaphore der Aufgabe belegt ist");

        semaphore.Release();
        var finished = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        finished.Should().Be(task, "AktualisierungAsync muss nach Freigabe der Semaphore fortschreiten");
        task.IsCompletedSuccessfully.Should().BeTrue();
    }

    /// <summary>
    /// Zwei unterschiedliche Aufgaben dürfen sich beim Heartbeat-Update nicht gegenseitig blockieren:
    /// Ein Aufruf für Aufgabe B muss fortschreiten können, während die Semaphore von Aufgabe A belegt ist.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_WithDifferentWorkItems_DoesNotSerializeAcrossTasks()
    {
        var workItemIdA = Guid.NewGuid();
        var workItemIdB = Guid.NewGuid();
        _sut.StartHeartbeat(workItemIdA);
        _sut.StartHeartbeat(workItemIdB);

        var semaphoresField = typeof(CliProcessManager).GetField("_updateSemaphores", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var semaphores = (ConcurrentDictionary<Guid, SemaphoreSlim>)semaphoresField.GetValue(_sut)!;
        var semaphoreA = semaphores[workItemIdA];
        await semaphoreA.WaitAsync(TestContext.Current.CancellationToken);

        var method = typeof(CliProcessManager).GetMethod("UpdateAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var taskB = (Task)method.Invoke(_sut, new object[] { workItemIdB })!;

        var finished = await Task.WhenAny(taskB, Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        finished.Should().Be(taskB, "die Semaphore einer anderen Aufgabe darf den Heartbeat-Update von Aufgabe B nicht blockieren");
        taskB.IsCompletedSuccessfully.Should().BeTrue();

        semaphoreA.Release();
    }

    /// <summary>Shutdown-Race: AktivenLaufSetzenAsync darf nicht fehlschlagen, wenn der DI-Provider bereits disposed ist.</summary>
    [Fact]
    public async Task SetActiveRunAsync_WithDisposedScopeFactory_Completes()
    {
        await InvokePersistenceMethodWithDisposedScopeFactoryAsync("SetActiveRunAsync", Guid.NewGuid());
    }

    /// <summary>Shutdown-Race: AktivenLaufBeendenAsync darf nicht fehlschlagen, wenn der DI-Provider bereits disposed ist.</summary>
    [Fact]
    public async Task EndActiveRunAsync_WithDisposedScopeFactory_Completes()
    {
        await InvokePersistenceMethodWithDisposedScopeFactoryAsync("EndActiveRunAsync", Guid.NewGuid());
    }

    /// <summary>Shutdown-Race: LaufdatenSicherstellenAsync darf nicht fehlschlagen, wenn der DI-Provider
    /// bereits disposed ist. Damit der interne <c>IsRunning</c>-Guard den Persistenzpfad wirklich erreicht
    /// (sonst wäre der Test wirkungslos), wird zuvor ein laufendes Handle gepflanzt.</summary>
    [Fact]
    public async Task RundataEnsureAsync_WithDisposedScopeFactory_Completes()
    {
        var workItemId = Guid.NewGuid();
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        scopeFactoryMock
            .Setup(f => f.CreateScope())
            .Throws(new ObjectDisposedException("ServiceProvider"));

        using var aiService = TestAiExecutionServiceFactory.Create(scopeFactoryMock.Object);

        var handlesField = typeof(AiExecutionService).GetField("_handles", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var handles = (System.Collections.Concurrent.ConcurrentDictionary<Guid, CliProcessHandle>)handlesField.GetValue(aiService)!;
        handles[workItemId] = new CliProcessHandle(workItemId, System.Diagnostics.Process.GetCurrentProcess());

        using var manager = new CliProcessManager(
            aiService,
            scopeFactoryMock.Object,
            NullLogger<CliProcessManager>.Instance,
            new WorkItemRunDataChangedNotifier());

        var method = typeof(CliProcessManager).GetMethod("EnsureRunDataAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var task = (Task)method.Invoke(manager, new object[] { workItemId })!;
        await task;
    }

    /// <summary>
    /// Dispose-Race (Issue 342): Verspätete Selbstheilungs-Aufrufe (in-flight Abgleich-/
    /// Heartbeat-Ticks, Event-Aufrufe) dürfen nach <see cref="CliProcessManager.Dispose"/> keine neuen
    /// Heartbeat-Timer oder RuntimeStatus-Subscriptions mehr anlegen — die landeten sonst in den
    /// bereits geleerten Dictionaries, würden nie disposed/abgemeldet und tickten bei weiter
    /// laufendem Prozess permanent weiter.
    /// </summary>
    [Fact]
    public async Task Dispose_PreventsNewcreationOfTimerAndSubscription()
    {
        var workItemId = Guid.NewGuid();

        // Laufendes Handle mit Session pflanzen, damit LaufdatenSicherstellenAsync den
        // IsRunning-Check passiert und beide Anlagepfade (Timer + Subscription) erreicht würden.
        var handlesField = typeof(AiExecutionService).GetField("_handles", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var handles = (ConcurrentDictionary<Guid, CliProcessHandle>)handlesField.GetValue(_aiService)!;
        var sessionMock = new Mock<ITerminalSession>();
        handles[workItemId] = new CliProcessHandle(workItemId, System.Diagnostics.Process.GetCurrentProcess())
        {
            Session = sessionMock.Object
        };

        _sut.Dispose();

        var method = typeof(CliProcessManager).GetMethod("EnsureRunDataAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        await (Task)method.Invoke(_sut, new object[] { workItemId })!;
        _sut.StartHeartbeat(workItemId);

        // SubscribeRuntimeStatus separat aufrufen, damit dessen _disposed-Guard nicht nur indirekt
        // über den Early-Return in LaufdatenSicherstellenAsync abgedeckt ist.
        var subscribeMethod = typeof(CliProcessManager).GetMethod("SubscribeRuntimeStatus", BindingFlags.NonPublic | BindingFlags.Instance)!;
        subscribeMethod.Invoke(_sut, new object[] { workItemId });

        var heartbeatField = typeof(CliProcessManager).GetField("_heartbeatTimers", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var heartbeatTimers = (ConcurrentDictionary<Guid, Timer>)heartbeatField.GetValue(_sut)!;
        heartbeatTimers.Should().BeEmpty("nach Dispose darf kein neuer Heartbeat-Timer angelegt werden");

        var subscriptionsField = typeof(CliProcessManager).GetField("_runtimeStatusSubscriptions", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var subscriptions = (ConcurrentDictionary<Guid, (ITerminalSession, EventHandler<CliRuntimeStatusChangedEventArgs>)>)subscriptionsField.GetValue(_sut)!;
        subscriptions.Should().BeEmpty("nach Dispose darf keine neue RuntimeStatus-Subscription angelegt werden");

        sessionMock.VerifyAdd(
            s => s.RuntimeStatusChanged += It.IsAny<EventHandler<CliRuntimeStatusChangedEventArgs>>(),
            Times.Never);
    }

    private async Task InvokePersistenceMethodWithDisposedScopeFactoryAsync(string methodName, params object[] arguments)
    {
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        scopeFactoryMock
            .Setup(f => f.CreateScope())
            .Throws(new ObjectDisposedException("ServiceProvider"));

        using var aiService = TestAiExecutionServiceFactory.Create(scopeFactoryMock.Object);
        using var manager = new CliProcessManager(
            aiService,
            scopeFactoryMock.Object,
            NullLogger<CliProcessManager>.Instance,
            new WorkItemRunDataChangedNotifier());

        var method = typeof(CliProcessManager).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)!;
        var task = (Task)method.Invoke(manager, arguments)!;
        await task;
    }
}
