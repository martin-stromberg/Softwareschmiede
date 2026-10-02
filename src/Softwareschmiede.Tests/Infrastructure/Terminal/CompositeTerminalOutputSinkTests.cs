using System.Text;
using FluentAssertions;
using Softwareschmiede.Infrastructure.Terminal;

namespace Softwareschmiede.Tests.Infrastructure.Terminal;

/// <summary>Unit-Tests für <see cref="CompositeTerminalOutputSink"/>.</summary>
public sealed class CompositeTerminalOutputSinkTests
{
    /// <summary>Normale Output-Chunks gehen an alle inneren Senken in Reihenfolge.</summary>
    [Fact]
    public void OnOutputChunk_FaechertAufAlleSenken()
    {
        var sinkA = new CollectingSink();
        var sinkB = new CollectingDiagnoseSink();
        var sut = new CompositeTerminalOutputSink(sinkA, sinkB);

        var chunk = Encoding.UTF8.GetBytes("abc");
        sut.OnOutputChunk(chunk);

        sinkA.Chunks.Should().Equal("abc");
        sinkB.Chunks.Should().Equal("abc");
    }

    /// <summary>Diagnose-Chunks erreichen nur innere Senken, die <see cref="ITerminalDiagnoseSink"/>
    /// implementieren — byte-exakte Mitschnitt-Senken bleiben frei von Markerzeilen.</summary>
    [Fact]
    public void OnDiagnoseChunk_GehtNurAnDiagnoseSenken()
    {
        var recorderAehnlich = new CollectingSink();           // kein ITerminalDiagnoseSink
        var protokollAehnlich = new CollectingDiagnoseSink();  // implementiert ITerminalDiagnoseSink
        var sut = new CompositeTerminalOutputSink(recorderAehnlich, protokollAehnlich);

        sut.OnDiagnoseChunk(Encoding.UTF8.GetBytes("[Terminal-Diagnose] test"));

        protokollAehnlich.DiagnoseChunks.Should().Equal("[Terminal-Diagnose] test");
        protokollAehnlich.Chunks.Should().BeEmpty(
            "der Diagnose-Kanal darf nicht als normaler Output-Chunk in die Senke laufen");
        recorderAehnlich.Chunks.Should().BeEmpty(
            "Senken ohne ITerminalDiagnoseSink erhalten keine Diagnose-Marker");
    }

    /// <summary><see cref="ITerminalOutputSink.Complete"/> und
    /// <see cref="ITerminalOutputSink.CompleteAsync"/> schließen alle inneren Senken ab.</summary>
    [Fact]
    public async Task Complete_SchliesstAlleInnerenSenkenAb()
    {
        var sinkA = new CollectingSink();
        var sinkB = new CollectingDiagnoseSink();
        var sut = new CompositeTerminalOutputSink(sinkA, sinkB);

        sut.Complete();
        sinkA.IsCompleted.Should().BeTrue();
        sinkB.IsCompleted.Should().BeTrue();

        var sinkC = new CollectingSink();
        var sinkD = new CollectingDiagnoseSink();
        var sut2 = new CompositeTerminalOutputSink(sinkC, sinkD);
        await sut2.CompleteAsync(TimeSpan.FromSeconds(1));
        sinkC.IsCompleted.Should().BeTrue();
        sinkD.IsCompleted.Should().BeTrue();
    }

    private class CollectingSink : ITerminalOutputSink
    {
        private readonly List<string> _chunks = [];
        public IReadOnlyList<string> Chunks => _chunks;
        public bool IsCompleted { get; private set; }

        public void OnOutputChunk(ReadOnlySpan<byte> bytes)
            => _chunks.Add(Encoding.UTF8.GetString(bytes));

        public void Complete() => IsCompleted = true;

        public virtual Task CompleteAsync(TimeSpan timeout, CancellationToken ct = default)
        {
            IsCompleted = true;
            return Task.CompletedTask;
        }
    }

    private sealed class CollectingDiagnoseSink : CollectingSink, ITerminalDiagnoseSink
    {
        private readonly List<string> _diagnoseChunks = [];
        public IReadOnlyList<string> DiagnoseChunks => _diagnoseChunks;

        public void OnDiagnoseChunk(ReadOnlySpan<byte> bytes)
            => _diagnoseChunks.Add(Encoding.UTF8.GetString(bytes));
    }
}
