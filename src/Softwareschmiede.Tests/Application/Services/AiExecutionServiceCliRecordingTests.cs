using System.Text;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Infrastructure.Terminal;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Unit-Tests für den Rohbyte-Mitschnitt (<see cref="CliOutputRecorder"/>) in
/// <see cref="Softwareschmiede.Application.Services.AiExecutionService"/>: Recorder-Verdrahtung,
/// Registry und Budget-Deaktivierung. Die Services werden bewusst nicht disposed — der
/// deterministische Test-Launcher bindet <c>Process.GetCurrentProcess()</c> und
/// <see cref="IDisposable.Dispose"/> würde den Testhost selbst beenden.</summary>
public sealed class AiExecutionServiceCliRecordingTests
{
    private static Mock<IAiPlugin> CreatePlugin()
    {
        var pluginMock = new Mock<IAiPlugin>();
        pluginMock.SetupTerminalSpec();
        return pluginMock;
    }

    /// <summary>Nach dem Session-Start liefert <c>GetCliAufzeichnung</c> die aufgezeichneten Roh-Chunks —
    /// inklusive derer, die bereits beim Start an die Senke gingen (kein Race-Fenster).</summary>
    [Fact]
    public async Task GetCliRecording_AfterSessionStart_ContainsRawChunks()
    {
        var chunks = new[]
        {
            Encoding.UTF8.GetBytes("früher-chunk"),
            Encoding.UTF8.GetBytes("\x1b[31mfarbchunk"),
        };
        var sut = TestAiExecutionServiceFactory.Create(
            new Mock<IServiceScopeFactory>().Object,
            launcher: TestTerminalSessionFactory.CreateChunkEmittingLauncher(chunks));
        var workItemId = Guid.NewGuid();
        var pluginMock = CreatePlugin();

        await sut.StartTerminalSessionAsync(workItemId, pluginMock.Object, Path.GetTempPath(), ct: TestContext.Current.CancellationToken);

        var recording = sut.GetCliRecording(workItemId);
        recording.Should().NotBeNull();
        recording!.WorkItemId.Should().Be(workItemId);
        recording.PluginName.Should().Be("TestPlugin");
        recording.Cols.Should().Be(new TerminalSessionOptions().DefaultCols);
        recording.Rows.Should().Be(new TerminalSessionOptions().DefaultRows);
        recording.IsComplete.Should().BeTrue();
        recording.Chunks.Should().HaveCount(2);
        recording.Chunks[0].Data.Should().Equal(chunks[0]);
        recording.Chunks[1].Data.Should().Equal(chunks[1]);
    }

    /// <summary>Ohne Session-Start liefert <c>GetCliAufzeichnung</c> null.</summary>
    [Fact]
    public void GetCliRecording_WithoutSession_ReturnsNull()
    {
        var sut = TestAiExecutionServiceFactory.Create();

        sut.GetCliRecording(Guid.NewGuid()).Should().BeNull();
    }

    /// <summary>Mit <c>AufzeichnungByteBudget &lt;= 0</c> wird kein Recorder erstellt — die Senke des
    /// Handles ist dann direkt der Protokoll-Writer (keine einelementige Composite).</summary>
    [Fact]
    public async Task StartTerminalSessionAsync_DisabledBudget_NoRecorderAndNoComposite()
    {
        var launcher = TestTerminalSessionFactory.CreateChunkEmittingLauncher([Encoding.UTF8.GetBytes("x")]);
        var sut = TestAiExecutionServiceFactory.Create(
            new Mock<IServiceScopeFactory>().Object,
            launcher: launcher,
            terminalOptions: new TerminalSessionOptions { RecordingByteBudget = 0 });
        var workItemId = Guid.NewGuid();
        var pluginMock = CreatePlugin();

        var handle = await sut.StartTerminalSessionAsync(workItemId, pluginMock.Object, Path.GetTempPath(), ct: TestContext.Current.CancellationToken);

        sut.GetCliRecording(workItemId).Should().BeNull("bei deaktiviertem Budget wird kein Recorder erstellt");
        handle.OutputSink.Should().BeOfType<CliOutputLogWriter>(
            "ohne Recorder wird der Protokoll-Writer direkt als Senke übergeben");
    }

    /// <summary>Mit aktivem Budget ist die Senke eine <see cref="CompositeTerminalOutputSink"/> aus
    /// Protokoll-Writer und Recorder.</summary>
    [Fact]
    public async Task StartTerminalSessionAsync_ActiveBudget_SinkIsComposite()
    {
        var sut = TestAiExecutionServiceFactory.Create(
            new Mock<IServiceScopeFactory>().Object,
            launcher: TestTerminalSessionFactory.CreateChunkEmittingLauncher([Encoding.UTF8.GetBytes("x")]));
        var workItemId = Guid.NewGuid();
        var pluginMock = CreatePlugin();

        var handle = await sut.StartTerminalSessionAsync(workItemId, pluginMock.Object, Path.GetTempPath(), ct: TestContext.Current.CancellationToken);

        handle.OutputSink.Should().BeOfType<CompositeTerminalOutputSink>(
            "der an die Session übergebene Sink muss die Composite-Instanz sein (Recorder + Writer)");
    }

    /// <summary>Ein überschrittenes Aufzeichnungsbudget markiert die Aufzeichnung als unvollständig —
    /// das Präfix bleibt erhalten.</summary>
    [Fact]
    public async Task GetCliRecording_BudgetExceeded_IncompleteWithPrefix()
    {
        var chunks = new[]
        {
            Encoding.UTF8.GetBytes("12345678"),
            Encoding.UTF8.GetBytes("9"),
        };
        var sut = TestAiExecutionServiceFactory.Create(
            new Mock<IServiceScopeFactory>().Object,
            launcher: TestTerminalSessionFactory.CreateChunkEmittingLauncher(chunks),
            terminalOptions: new TerminalSessionOptions { RecordingByteBudget = 8 });
        var workItemId = Guid.NewGuid();
        var pluginMock = CreatePlugin();

        await sut.StartTerminalSessionAsync(workItemId, pluginMock.Object, Path.GetTempPath(), ct: TestContext.Current.CancellationToken);

        var recording = sut.GetCliRecording(workItemId);
        recording.Should().NotBeNull();
        recording!.IsComplete.Should().BeFalse();
        recording.Chunks.Should().HaveCount(1);
        recording.Chunks[0].Data.Should().Equal(chunks[0]);
    }

    /// <summary>Die Aufzeichnungs-Registry ist auf die letzten
    /// <see cref="Softwareschmiede.Application.Services.AiExecutionService.MaxRecordingCount"/>
    /// Aufgaben begrenzt — der älteste Mitschnitt wird verworfen, die jüngsten bleiben vorgehalten.</summary>
    [Fact]
    public async Task GetCliRecording_MoreAsMaxRecording_DiscardsOldestEntries()
    {
        var launcher = TestTerminalSessionFactory.CreateChunkEmittingLauncher([Encoding.UTF8.GetBytes("x")]);
        var sut = TestAiExecutionServiceFactory.Create(
            new Mock<IServiceScopeFactory>().Object,
            launcher: launcher);
        var pluginMock = CreatePlugin();
        var workItemIds = Enumerable.Range(0, AiExecutionService.MaxRecordingCount + 1)
            .Select(_ => Guid.NewGuid())
            .ToList();

        foreach (var id in workItemIds)
            await sut.StartTerminalSessionAsync(id, pluginMock.Object, Path.GetTempPath(), ct: TestContext.Current.CancellationToken);

        sut.GetCliRecording(workItemIds[0]).Should().BeNull(
            "der älteste Mitschnitt wird über der Retention-Grenze verworfen");
        foreach (var id in workItemIds.Skip(1))
            sut.GetCliRecording(id).Should().NotBeNull(
                "die letzten {0} Mitschnitte bleiben vorgehalten", AiExecutionService.MaxRecordingCount);
    }

    /// <summary>Der übergebene TimeProvider liefert die Start- und Offset-Zeitstempel der Aufzeichnung.</summary>
    [Fact]
    public async Task GetCliRecording_UsesTimeProviderForTimestamp()
    {
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2025, 5, 5, 10, 0, 0, TimeSpan.Zero));
        var sut = TestAiExecutionServiceFactory.Create(
            new Mock<IServiceScopeFactory>().Object,
            launcher: TestTerminalSessionFactory.CreateChunkEmittingLauncher([Encoding.UTF8.GetBytes("x")]),
            timeProvider: timeProvider);
        var workItemId = Guid.NewGuid();
        var pluginMock = CreatePlugin();

        await sut.StartTerminalSessionAsync(workItemId, pluginMock.Object, Path.GetTempPath(), ct: TestContext.Current.CancellationToken);

        var recording = sut.GetCliRecording(workItemId);
        recording.Should().NotBeNull();
        recording!.StartUtc.Should().Be(timeProvider.Start);
        recording.Chunks[0].Offset.Should().Be(TimeSpan.Zero);
    }
}
