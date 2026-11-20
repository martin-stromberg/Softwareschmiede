using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Softwareschmiede.Infrastructure.Terminal;

namespace Softwareschmiede.Tests.Infrastructure.Terminal;

/// <summary>Unit-Tests für <see cref="CliOutputRecorder"/> (Rohbyte-Mitschnitt für das
/// Konsolentestfenster).</summary>
public sealed class CliOutputRecorderTests
{
    /// <summary>Der Recorder zeichnet jeden Chunk mit Offset und kopiert die Bytes sofort.</summary>
    [Fact]
    public void OnOutputChunk_ZeichnetChunksMitOffsetUndBytekopie()
    {
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var workItemId = Guid.NewGuid();
        var sut = new CliOutputRecorder(workItemId, "TestPlugin", 120, 30, 1024, timeProvider);

        var chunk1 = Encoding.UTF8.GetBytes("erster");
        sut.OnOutputChunk(chunk1);
        chunk1[0] = (byte)'X'; // nachträgliche Änderung des Ausgangspuffers
        timeProvider.Advance(TimeSpan.FromMilliseconds(500));
        sut.OnOutputChunk(Encoding.UTF8.GetBytes("zweiter"));

        var recording = sut.GetRecording();
        recording.WorkItemId.Should().Be(workItemId);
        recording.PluginName.Should().Be("TestPlugin");
        recording.StartUtc.Should().Be(timeProvider.Start);
        recording.Cols.Should().Be(120);
        recording.Rows.Should().Be(30);
        recording.IstVollstaendig.Should().BeTrue();
        recording.EndeUtc.Should().BeNull("Complete() wurde noch nicht aufgerufen");
        recording.Chunks.Should().HaveCount(2);
        recording.Chunks[0].Offset.Should().Be(TimeSpan.Zero);
        recording.Chunks[0].Data.Should().Equal(Encoding.UTF8.GetBytes("erster"),
            "der Recorder muss die Bytes sofort kopieren — nachträgliche Puffer-Änderungen bleiben wirkungslos");
        recording.Chunks[1].Offset.Should().Be(TimeSpan.FromMilliseconds(500));
        recording.Chunks[1].Data.Should().Equal(Encoding.UTF8.GetBytes("zweiter"));
    }

    /// <summary>Bei Budget-Überschreitung bleibt das intakte Präfix erhalten und die Aufzeichnung
    /// wird als unvollständig markiert; weitere Chunks werden ignoriert.</summary>
    [Fact]
    public void OnOutputChunk_BudgetUeberschritten_BehaeltIntaktesPraefixUndStoppt()
    {
        var sut = new CliOutputRecorder(Guid.NewGuid(), "P", 80, 24, 5, new FakeTimeProvider());

        sut.OnOutputChunk(Encoding.UTF8.GetBytes("abcde")); // füllt das Budget exakt
        sut.OnOutputChunk(Encoding.UTF8.GetBytes("f"));     // würde überschreiten → Stop
        sut.OnOutputChunk(Encoding.UTF8.GetBytes("xy"));    // wird nicht mehr angenommen

        var recording = sut.GetRecording();
        recording.IstVollstaendig.Should().BeFalse();
        recording.Chunks.Should().HaveCount(1);
        recording.Chunks[0].Data.Should().Equal(Encoding.UTF8.GetBytes("abcde"));
    }

    /// <summary><see cref="CliOutputRecorder.Complete"/> setzt das Ende-Zeitstempel-Feld einmalig
    /// und ist idempotent; <see cref="ITerminalOutputSink.CompleteAsync"/> verhält sich gleich.</summary>
    [Fact]
    public async Task Complete_SetztEndeUtc_Idempotent()
    {
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2025, 6, 1, 12, 0, 0, TimeSpan.Zero));
        var sut = new CliOutputRecorder(Guid.NewGuid(), "P", 80, 24, 1024, timeProvider);

        sut.OnOutputChunk(Encoding.UTF8.GetBytes("x"));
        timeProvider.Advance(TimeSpan.FromSeconds(2));
        sut.Complete();
        timeProvider.Advance(TimeSpan.FromSeconds(5));
        await sut.CompleteAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

        var recording = sut.GetRecording();
        recording.EndeUtc.Should().Be(timeProvider.Start + TimeSpan.FromSeconds(2),
            "das erste Complete() muss den Endzeitpunkt festhalten; spätere Aufrufe ändern ihn nicht");
    }

    /// <summary><see cref="CliOutputRecorder.GetRecording"/> liefert einen Snapshot: später
    /// aufgezeichnete Chunks ändern eine bereits geholte Aufzeichnung nicht.</summary>
    [Fact]
    public void GetAufzeichnung_LiefertUnabhaengigenSnapshot()
    {
        var sut = new CliOutputRecorder(Guid.NewGuid(), "P", 80, 24, 1024, new FakeTimeProvider());

        sut.OnOutputChunk(Encoding.UTF8.GetBytes("a"));
        var snapshot = sut.GetRecording();
        sut.OnOutputChunk(Encoding.UTF8.GetBytes("b"));
        sut.Complete();

        snapshot.Chunks.Should().HaveCount(1);
        snapshot.EndeUtc.Should().BeNull();
        sut.GetRecording().Chunks.Should().HaveCount(2);
    }

    /// <summary>Leere Chunks werden nicht aufgezeichnet (kein Rauschen im Mitschnitt).</summary>
    [Fact]
    public void OnOutputChunk_LeererChunk_WirdIgnoriert()
    {
        var sut = new CliOutputRecorder(Guid.NewGuid(), "P", 80, 24, 1024, new FakeTimeProvider());

        sut.OnOutputChunk(ReadOnlySpan<byte>.Empty);

        sut.GetRecording().Chunks.Should().BeEmpty();
    }

    /// <summary>Der Recorder implementiert bewusst nicht <see cref="ITerminalDiagnoseSink"/>, damit
    /// Diagnose-Markerzeilen nicht in den byte-exakten Mitschnitt gelangen.</summary>
    [Fact]
    public void Recorder_ImplementiertNichtITerminalDiagnoseSink()
    {
        ITerminalOutputSink sut = new CliOutputRecorder(Guid.NewGuid(), "P", 80, 24, 1024, new FakeTimeProvider());

        (sut is ITerminalDiagnoseSink).Should().BeFalse();
    }
}
