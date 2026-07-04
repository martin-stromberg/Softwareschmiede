using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Softwareschmiede.Application.Services;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Tests für die SafeFireAndForget-Extension-Methode.</summary>
public sealed class AsyncTaskExtensionsTests
{
    /// <summary>Wenn der Task fehlschlägt, wird die Exception auf Error-Ebene geloggt.</summary>
    [Fact]
    public async Task SafeFireAndForget_LogsErrorOnTaskException()
    {
        var loggerMock = new Mock<ILogger>();
        var exception = new InvalidOperationException("Test-Fehler");
        var tcs = new TaskCompletionSource();
        loggerMock
            .Setup(l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
            .Callback(() => tcs.TrySetResult());

        Task FailingTask() => Task.FromException(exception);

        FailingTask().SafeFireAndForget(loggerMock.Object, "TestOperation");

        var finished = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        finished.Should().Be(tcs.Task, "SafeFireAndForget muss die Exception des fehlgeschlagenen Tasks loggen");

        loggerMock.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.Is<Exception?>(e => e is AggregateException && ((AggregateException)e).InnerException == exception),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>Wenn der Task abgebrochen wird, wird dies auf Info-Ebene geloggt.</summary>
    [Fact]
    public async Task SafeFireAndForget_LogsInfoOnTaskCancellation()
    {
        var loggerMock = new Mock<ILogger>();
        var tcs = new TaskCompletionSource();
        loggerMock
            .Setup(l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
            .Callback(() => tcs.TrySetResult());

        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Task CanceledTask() => Task.FromCanceled(cts.Token);

        CanceledTask().SafeFireAndForget(loggerMock.Object, "AbgebrocheneOperation");

        var finished = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        finished.Should().Be(tcs.Task, "SafeFireAndForget muss den Abbruch des Tasks loggen");
    }

    /// <summary>Wenn der Task erfolgreich abgeschlossen wird, wird kein Fehler oder Abbruch geloggt.</summary>
    [Fact]
    public async Task SafeFireAndForget_DoesNotLogErrorOrInfo_OnSuccessfulTask()
    {
        var loggerMock = new Mock<ILogger>();

        Task.CompletedTask.SafeFireAndForget(loggerMock.Object, "ErfolgreicheOperation");

        await Task.Delay(TimeSpan.FromMilliseconds(200));

        loggerMock.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
        loggerMock.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }
}
