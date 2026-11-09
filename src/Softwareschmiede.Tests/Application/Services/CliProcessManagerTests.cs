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
    private readonly KiAusfuehrungsService _kiService;
    private readonly CliProcessManager _sut;

    /// <summary>CliProcessManagerTests.</summary>
    public CliProcessManagerTests()
    {
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        _kiService = TestKiAusfuehrungsServiceFactory.Create(scopeFactoryMock.Object);
        _sut = new CliProcessManager(
            _kiService,
            scopeFactoryMock.Object,
            NullLogger<CliProcessManager>.Instance,
            new AufgabeLaufdatenChangedNotifier());
    }

    /// <summary>Dispose.</summary>
    public void Dispose()
    {
        _sut.Dispose();
        _kiService.Dispose();
    }

    /// <summary>
    /// AktualisierungAsync muss pro Aufgabe serialisieren: solange die Semaphore der Aufgabe belegt ist, darf ein
    /// weiterer Aufruf für dieselbe Aufgabe nicht fortschreiten. Erst nach Freigabe der Semaphore läuft der Aufruf weiter.
    /// </summary>
    [Fact]
    public async Task AktualisierungAsync_WithConcurrentTimerTicks_Serializes()
    {
        var aufgabeId = Guid.NewGuid();
        _sut.StartHeartbeat(aufgabeId);

        var semaphoresField = typeof(CliProcessManager).GetField("_updateSemaphores", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var semaphores = (ConcurrentDictionary<Guid, SemaphoreSlim>)semaphoresField.GetValue(_sut)!;
        var semaphore = semaphores[aufgabeId];
        await semaphore.WaitAsync();

        var method = typeof(CliProcessManager).GetMethod("AktualisierungAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var task = (Task)method.Invoke(_sut, new object[] { aufgabeId })!;

        await Task.Delay(TimeSpan.FromMilliseconds(200));
        task.IsCompleted.Should().BeFalse("AktualisierungAsync muss blockieren, solange die Semaphore der Aufgabe belegt ist");

        semaphore.Release();
        var finished = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(5)));
        finished.Should().Be(task, "AktualisierungAsync muss nach Freigabe der Semaphore fortschreiten");
        task.IsCompletedSuccessfully.Should().BeTrue();
    }

    /// <summary>
    /// Zwei unterschiedliche Aufgaben dürfen sich beim Heartbeat-Update nicht gegenseitig blockieren:
    /// Ein Aufruf für Aufgabe B muss fortschreiten können, während die Semaphore von Aufgabe A belegt ist.
    /// </summary>
    [Fact]
    public async Task AktualisierungAsync_WithDifferentAufgaben_DoesNotSerializeAcrossTasks()
    {
        var aufgabeIdA = Guid.NewGuid();
        var aufgabeIdB = Guid.NewGuid();
        _sut.StartHeartbeat(aufgabeIdA);
        _sut.StartHeartbeat(aufgabeIdB);

        var semaphoresField = typeof(CliProcessManager).GetField("_updateSemaphores", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var semaphores = (ConcurrentDictionary<Guid, SemaphoreSlim>)semaphoresField.GetValue(_sut)!;
        var semaphoreA = semaphores[aufgabeIdA];
        await semaphoreA.WaitAsync();

        var method = typeof(CliProcessManager).GetMethod("AktualisierungAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var taskB = (Task)method.Invoke(_sut, new object[] { aufgabeIdB })!;

        var finished = await Task.WhenAny(taskB, Task.Delay(TimeSpan.FromSeconds(5)));
        finished.Should().Be(taskB, "die Semaphore einer anderen Aufgabe darf den Heartbeat-Update von Aufgabe B nicht blockieren");
        taskB.IsCompletedSuccessfully.Should().BeTrue();

        semaphoreA.Release();
    }

    /// <summary>Shutdown-Race: AktivenLaufSetzenAsync darf nicht fehlschlagen, wenn der DI-Provider bereits disposed ist.</summary>
    [Fact]
    public async Task AktivenLaufSetzenAsync_WithDisposedScopeFactory_Completes()
    {
        await InvokePersistierungsMethodeMitDisposedScopeFactoryAsync("AktivenLaufSetzenAsync", Guid.NewGuid());
    }

    /// <summary>Shutdown-Race: AktivenLaufBeendenAsync darf nicht fehlschlagen, wenn der DI-Provider bereits disposed ist.</summary>
    [Fact]
    public async Task AktivenLaufBeendenAsync_WithDisposedScopeFactory_Completes()
    {
        await InvokePersistierungsMethodeMitDisposedScopeFactoryAsync("AktivenLaufBeendenAsync", Guid.NewGuid());
    }

    /// <summary>Shutdown-Race: LaufdatenSicherstellenAsync darf nicht fehlschlagen, wenn der DI-Provider
    /// bereits disposed ist. Damit der interne <c>IsRunning</c>-Guard den Persistenzpfad wirklich erreicht
    /// (sonst wäre der Test wirkungslos), wird zuvor ein laufendes Handle gepflanzt.</summary>
    [Fact]
    public async Task LaufdatenSicherstellenAsync_WithDisposedScopeFactory_Completes()
    {
        var aufgabeId = Guid.NewGuid();
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        scopeFactoryMock
            .Setup(f => f.CreateScope())
            .Throws(new ObjectDisposedException("ServiceProvider"));

        using var kiService = TestKiAusfuehrungsServiceFactory.Create(scopeFactoryMock.Object);

        var handlesField = typeof(KiAusfuehrungsService).GetField("_handles", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var handles = (System.Collections.Concurrent.ConcurrentDictionary<Guid, CliProcessHandle>)handlesField.GetValue(kiService)!;
        handles[aufgabeId] = new CliProcessHandle(aufgabeId, System.Diagnostics.Process.GetCurrentProcess());

        using var manager = new CliProcessManager(
            kiService,
            scopeFactoryMock.Object,
            NullLogger<CliProcessManager>.Instance,
            new AufgabeLaufdatenChangedNotifier());

        var method = typeof(CliProcessManager).GetMethod("LaufdatenSicherstellenAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var task = (Task)method.Invoke(manager, new object[] { aufgabeId })!;
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
    public async Task Dispose_VerhindertNeuanlageVonTimerUndSubscription()
    {
        var aufgabeId = Guid.NewGuid();

        // Laufendes Handle mit Session pflanzen, damit LaufdatenSicherstellenAsync den
        // IsRunning-Check passiert und beide Anlagepfade (Timer + Subscription) erreicht würden.
        var handlesField = typeof(KiAusfuehrungsService).GetField("_handles", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var handles = (ConcurrentDictionary<Guid, CliProcessHandle>)handlesField.GetValue(_kiService)!;
        var sessionMock = new Mock<ITerminalSession>();
        handles[aufgabeId] = new CliProcessHandle(aufgabeId, System.Diagnostics.Process.GetCurrentProcess())
        {
            Session = sessionMock.Object
        };

        _sut.Dispose();

        var method = typeof(CliProcessManager).GetMethod("LaufdatenSicherstellenAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        await (Task)method.Invoke(_sut, new object[] { aufgabeId })!;
        _sut.StartHeartbeat(aufgabeId);

        // SubscribeRuntimeStatus separat aufrufen, damit dessen _disposed-Guard nicht nur indirekt
        // über den Early-Return in LaufdatenSicherstellenAsync abgedeckt ist.
        var subscribeMethod = typeof(CliProcessManager).GetMethod("SubscribeRuntimeStatus", BindingFlags.NonPublic | BindingFlags.Instance)!;
        subscribeMethod.Invoke(_sut, new object[] { aufgabeId });

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

    private async Task InvokePersistierungsMethodeMitDisposedScopeFactoryAsync(string methodName, params object[] arguments)
    {
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        scopeFactoryMock
            .Setup(f => f.CreateScope())
            .Throws(new ObjectDisposedException("ServiceProvider"));

        using var kiService = TestKiAusfuehrungsServiceFactory.Create(scopeFactoryMock.Object);
        using var manager = new CliProcessManager(
            kiService,
            scopeFactoryMock.Object,
            NullLogger<CliProcessManager>.Instance,
            new AufgabeLaufdatenChangedNotifier());

        var method = typeof(CliProcessManager).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)!;
        var task = (Task)method.Invoke(manager, arguments)!;
        await task;
    }
}
