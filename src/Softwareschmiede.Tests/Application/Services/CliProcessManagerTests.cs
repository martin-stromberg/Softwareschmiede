using System.Collections.Concurrent;
using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.Application.Services;

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
        _kiService = new KiAusfuehrungsService(NullLogger<KiAusfuehrungsService>.Instance, scopeFactoryMock.Object);
        _sut = new CliProcessManager(_kiService, scopeFactoryMock.Object, NullLogger<CliProcessManager>.Instance);
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
}
