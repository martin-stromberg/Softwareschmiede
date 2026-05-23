using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Interfaces;

namespace Softwareschmiede.Tests.Application.Services;

public sealed class AutoShutdownOrchestratorTests
{
    [Fact]
    public async Task ShouldRequestShutdown_WhenEnabledAndTransitionFromOneToZero()
    {
        var runningSource = new FakeRunningAutomationStatusSource();
        var shutdownService = new FakeSystemShutdownService();
        using var sut = new AutoShutdownOrchestrator(runningSource, shutdownService, NullLogger<AutoShutdownOrchestrator>.Instance);

        sut.SetEnabled(true);
        runningSource.Raise(previous: 1, current: 0, observedCurrentCount: 0);

        await shutdownService.WaitForRequestsAsync(expectedCount: 1);
        shutdownService.RequestCount.Should().Be(1);
    }

    [Fact]
    public async Task ShouldNotRequestShutdown_WhenDisabled()
    {
        var runningSource = new FakeRunningAutomationStatusSource();
        var shutdownService = new FakeSystemShutdownService();
        using var sut = new AutoShutdownOrchestrator(runningSource, shutdownService, NullLogger<AutoShutdownOrchestrator>.Instance);

        sut.SetEnabled(false);
        runningSource.Raise(previous: 1, current: 0, observedCurrentCount: 0);

        await Task.Delay(200);
        shutdownService.RequestCount.Should().Be(0);
    }

    [Fact]
    public async Task ShouldRequestShutdownOnlyOnce_PerZeroTransition()
    {
        var runningSource = new FakeRunningAutomationStatusSource();
        var shutdownService = new FakeSystemShutdownService();
        using var sut = new AutoShutdownOrchestrator(runningSource, shutdownService, NullLogger<AutoShutdownOrchestrator>.Instance);

        sut.SetEnabled(true);
        runningSource.Raise(previous: 1, current: 0, observedCurrentCount: 0);
        runningSource.Raise(previous: 1, current: 0, observedCurrentCount: 0);
        runningSource.Raise(previous: 0, current: 0, observedCurrentCount: 0);

        await shutdownService.WaitForRequestsAsync(expectedCount: 1);
        shutdownService.RequestCount.Should().Be(1);
    }

    [Fact]
    public async Task ShouldResetIdempotencyGuard_WhenCountBecomesPositiveAgain()
    {
        var runningSource = new FakeRunningAutomationStatusSource();
        var shutdownService = new FakeSystemShutdownService();
        using var sut = new AutoShutdownOrchestrator(runningSource, shutdownService, NullLogger<AutoShutdownOrchestrator>.Instance);

        sut.SetEnabled(true);
        runningSource.Raise(previous: 1, current: 0, observedCurrentCount: 0);
        await shutdownService.WaitForRequestsAsync(expectedCount: 1);

        runningSource.Raise(previous: 0, current: 1, observedCurrentCount: 1);
        runningSource.Raise(previous: 1, current: 0, observedCurrentCount: 0);
        await shutdownService.WaitForRequestsAsync(expectedCount: 2);

        shutdownService.RequestCount.Should().Be(2);
    }

    [Fact]
    public async Task ShouldSkipShutdown_WhenFinalRecheckFindsRunningAutomation()
    {
        var runningSource = new FakeRunningAutomationStatusSource();
        var shutdownService = new FakeSystemShutdownService();
        using var sut = new AutoShutdownOrchestrator(runningSource, shutdownService, NullLogger<AutoShutdownOrchestrator>.Instance);

        sut.SetEnabled(true);
        runningSource.Raise(previous: 1, current: 0, observedCurrentCount: 1);

        await Task.Delay(200);
        shutdownService.RequestCount.Should().Be(0);
    }

    [Fact]
    public async Task ShouldCatchException_WhenShutdownServiceThrows()
    {
        var runningSource = new FakeRunningAutomationStatusSource();
        var shutdownService = new FakeSystemShutdownService { ThrowOnRequest = true };
        using var sut = new AutoShutdownOrchestrator(runningSource, shutdownService, NullLogger<AutoShutdownOrchestrator>.Instance);
        sut.SetEnabled(true);

        runningSource.Raise(previous: 1, current: 0, observedCurrentCount: 0);

        await shutdownService.WaitForRequestsAsync(expectedCount: 1);
        shutdownService.RequestCount.Should().Be(1);
    }

    [Fact]
    public async Task ShouldUnsubscribeFromRunningCountChanged_OnDispose()
    {
        var runningSource = new FakeRunningAutomationStatusSource();
        var shutdownService = new FakeSystemShutdownService();
        var sut = new AutoShutdownOrchestrator(runningSource, shutdownService, NullLogger<AutoShutdownOrchestrator>.Instance);
        sut.SetEnabled(true);
        runningSource.SubscriberCount.Should().Be(1);

        sut.Dispose();
        runningSource.Raise(previous: 1, current: 0, observedCurrentCount: 0);

        await Task.Delay(200);
        runningSource.SubscriberCount.Should().Be(0);
        shutdownService.RequestCount.Should().Be(0);
    }

    [Fact]
    public async Task ShouldResetGuardAfterFinalRecheckSkip_AndRequestShutdownOnNextValidTransition()
    {
        var runningSource = new FakeRunningAutomationStatusSource();
        var shutdownService = new FakeSystemShutdownService();
        using var sut = new AutoShutdownOrchestrator(runningSource, shutdownService, NullLogger<AutoShutdownOrchestrator>.Instance);
        sut.SetEnabled(true);

        runningSource.Raise(previous: 1, current: 0, observedCurrentCount: 1);
        await Task.Delay(200);
        shutdownService.RequestCount.Should().Be(0);

        runningSource.Raise(previous: 1, current: 0, observedCurrentCount: 0);
        await shutdownService.WaitForRequestsAsync(expectedCount: 1);
        shutdownService.RequestCount.Should().Be(1);
    }

    private sealed class FakeRunningAutomationStatusSource : IRunningAutomationStatusSource
    {
        private int _runningCount;
        public event Action<int, int>? RunningCountChanged;
        public int SubscriberCount => RunningCountChanged?.GetInvocationList().Length ?? 0;

        public int GetRunningCount() => _runningCount;
        public bool IsRunning(Guid aufgabeId) => _runningCount > 0;

        public void Raise(int previous, int current, int observedCurrentCount)
        {
            _runningCount = observedCurrentCount;
            RunningCountChanged?.Invoke(previous, current);
        }
    }

    private sealed class FakeSystemShutdownService : ISystemShutdownService
    {
        private readonly Lock _syncLock = new();
        private int _requestCount;
        private TaskCompletionSource _signal = NewSignal();
        public bool ThrowOnRequest { get; set; }

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

        public Task RequestShutdownAsync(CancellationToken cancellationToken = default)
        {
            lock (_syncLock)
            {
                _requestCount++;
                _signal.TrySetResult();
                _signal = NewSignal();
            }

            if (ThrowOnRequest)
            {
                throw new InvalidOperationException("Test exception from shutdown service.");
            }

            return Task.CompletedTask;
        }

        public async Task WaitForRequestsAsync(int expectedCount)
        {
            while (RequestCount < expectedCount)
            {
                Task waitTask;
                lock (_syncLock)
                {
                    waitTask = _signal.Task;
                }

                await waitTask.WaitAsync(TimeSpan.FromSeconds(5));
            }
        }

        private static TaskCompletionSource NewSignal()
            => new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
