using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Interfaces;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>AutoShutdownOrchestratorTests.</summary>
public sealed class AutoShutdownOrchestratorTests
{
    /// <summary><summary>ShouldRequestShutdown_WhenEnabledAndTransitionFromOneToZero.</summary>.</summary>
    [Fact]
    /// <summary>ShouldRequestShutdown_WhenEnabledAndTransitionFromOneToZero.</summary>
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

    /// <summary><summary>ShouldNotRequestShutdown_WhenDisabled.</summary>.</summary>
    [Fact]
    /// <summary>ShouldNotRequestShutdown_WhenDisabled.</summary>
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

    /// <summary><summary>ShouldRequestShutdownOnlyOnce_PerZeroTransition.</summary>.</summary>
    [Fact]
    /// <summary>ShouldRequestShutdownOnlyOnce_PerZeroTransition.</summary>
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

    /// <summary><summary>ShouldResetIdempotencyGuard_WhenCountBecomesPositiveAgain.</summary>.</summary>
    [Fact]
    /// <summary>ShouldResetIdempotencyGuard_WhenCountBecomesPositiveAgain.</summary>
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

    /// <summary><summary>ShouldSkipShutdown_WhenFinalRecheckFindsRunningAutomation.</summary>.</summary>
    [Fact]
    /// <summary>ShouldSkipShutdown_WhenFinalRecheckFindsRunningAutomation.</summary>
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

    /// <summary><summary>ShouldCatchException_WhenShutdownServiceThrows.</summary>.</summary>
    [Fact]
    /// <summary>ShouldCatchException_WhenShutdownServiceThrows.</summary>
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

    /// <summary><summary>ShouldUnsubscribeFromRunningCountChanged_OnDispose.</summary>.</summary>
    [Fact]
    /// <summary>ShouldUnsubscribeFromRunningCountChanged_OnDispose.</summary>
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

    /// <summary><summary>ShouldResetGuardAfterFinalRecheckSkip_AndRequestShutdownOnNextValidTransition.</summary>.</summary>
    [Fact]
    /// <summary>ShouldResetGuardAfterFinalRecheckSkip_AndRequestShutdownOnNextValidTransition.</summary>
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

        /// <summary>GetRunningCount.</summary>
        public int GetRunningCount() => _runningCount;
        /// <summary>IsRunning.</summary>
        public bool IsRunning(Guid aufgabeId) => _runningCount > 0;

        /// <summary>Raise.</summary>
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

        /// <summary>RequestShutdownAsync.</summary>
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

        /// <summary>WaitForRequestsAsync.</summary>
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
