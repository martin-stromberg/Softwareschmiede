using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Softwareschmiede.Application.Services;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Tests für <see cref="PeriodicBackgroundService"/>.</summary>
public sealed class PeriodicBackgroundServiceTests
{
    private sealed class TestPeriodicBackgroundService : PeriodicBackgroundService
    {
        private readonly Func<CancellationToken, Task> _runOnce;

        public TestPeriodicBackgroundService(TimeSpan pollingInterval, TimeProvider timeProvider, Func<CancellationToken, Task> runOnce)
            : base(pollingInterval, timeProvider, NullLogger.Instance, "Fehler im Testdurchlauf")
        {
            _runOnce = runOnce;
        }

        public override Task RunOnceAsync(CancellationToken ct = default) => _runOnce(ct);

        public Task InvokeExecuteAsync(CancellationToken ct) => ExecuteAsync(ct);
    }

    /// <summary>
    /// Wird das Token während des Polling-Delays (nicht während RunOnceAsync) abgebrochen, darf keine unbehandelte
    /// TaskCanceledException aus ExecuteAsync propagieren (regulärer Shutdown, kein Fehler).
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_ShouldNotThrow_WhenTokenIsCanceledDuringDelay()
    {
        var timeProvider = new FakeTimeProvider();
        using var cts = new CancellationTokenSource();
        var sut = new TestPeriodicBackgroundService(TimeSpan.FromMinutes(5), timeProvider, _ => Task.CompletedTask);

        var executeTask = sut.InvokeExecuteAsync(cts.Token);
        cts.Cancel();

        var act = () => executeTask;

        await act.Should().NotThrowAsync();
    }
}
