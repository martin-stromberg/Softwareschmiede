using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Infrastructure.Services;

namespace Softwareschmiede.Tests.Infrastructure.Services;

/// <summary>
/// Tests des <see cref="RecordingSystemShutdownService"/>: Testmodus-Fake, der niemals
/// einen echten OS-Shutdown auslöst, sondern jede Anforderung (inkl. Force-Flag) in eine
/// Logdatei schreibt.
/// </summary>
public sealed class RecordingSystemShutdownServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"sw-shutdown-log-{Guid.NewGuid():N}");

    /// <summary>Räumt das temporäre Logverzeichnis auf.</summary>
    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    /// <summary>Die Logdatei wird neben der Test-Datenbank im selben Verzeichnis abgelegt.</summary>
    [Fact]
    public void ResolveLogFilePath_CreatesLogBesideTestDatabase()
    {
        var testDbPath = Path.Combine(_directory, "test.db");

        var path = RecordingSystemShutdownService.ResolveLogFilePath(testDbPath);

        path.Should().Be(Path.Combine(_directory, RecordingSystemShutdownService.LogFileName));
    }

    /// <summary>Ein leerer Test-DB-Pfad fällt auf das temporäre Verzeichnis zurück.</summary>
    [Fact]
    public void ResolveLogFilePath_FallsAtEmptyPathOnTempBack()
    {
        var path = RecordingSystemShutdownService.ResolveLogFilePath(null);

        path.Should().Be(Path.Combine(Path.GetTempPath(), RecordingSystemShutdownService.LogFileName));
    }

    /// <summary>Jede Anforderung wird als Zeile mit Force-Flag, UTC-Zeitstempel und PID angehängt — ohne echten Shutdown.</summary>
    [Fact]
    public async Task RequestShutdownAsync_WritesLineWithForceFlagTimestampAndPid()
    {
        Directory.CreateDirectory(_directory);
        var logPath = Path.Combine(_directory, RecordingSystemShutdownService.LogFileName);
        var sut = new RecordingSystemShutdownService(
            NullLogger<RecordingSystemShutdownService>.Instance, logPath);

        await sut.RequestShutdownAsync(force: true, cancellationToken: TestContext.Current.CancellationToken);
        await sut.RequestShutdownAsync(force: false, cancellationToken: TestContext.Current.CancellationToken);

        File.Exists(logPath).Should().BeTrue();
        var lines = await File.ReadAllLinesAsync(logPath, TestContext.Current.CancellationToken);
        lines.Should().HaveCount(2);
        lines[0].Should().StartWith("erzwingen=True|");
        lines[0].Should().EndWith($"|pid={Environment.ProcessId}");
        lines[0].Split('|').Should().HaveCount(3);
        lines[1].Should().StartWith("erzwingen=False|");
    }

    /// <summary>Der Testmodus-Fake startet keinen Prozess und liefert einen erfolgreich abgeschlossenen Task.</summary>
    [Fact]
    public async Task RequestShutdownAsync_ReturnsCompletedTask()
    {
        var logPath = Path.Combine(_directory, RecordingSystemShutdownService.LogFileName);
        var sut = new RecordingSystemShutdownService(
            NullLogger<RecordingSystemShutdownService>.Instance, logPath);

        var task = sut.RequestShutdownAsync(force: false, cancellationToken: TestContext.Current.CancellationToken);

        task.IsCompletedSuccessfully.Should().BeTrue();
        await task;
    }
}
