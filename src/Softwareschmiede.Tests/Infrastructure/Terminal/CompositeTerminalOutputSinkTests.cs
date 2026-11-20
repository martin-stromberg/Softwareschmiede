using System.Text;
using FluentAssertions;
using Softwareschmiede.Infrastructure.Terminal;

namespace Softwareschmiede.Tests.Infrastructure.Terminal;

/// <summary>Unit-Tests für <see cref="CompositeTerminalOutputSink"/>.</summary>
public sealed class CompositeTerminalOutputSinkTests
{
    /// <summary>Normale Output-Chunks gehen an alle inneren Senken in Reihenfolge.</summary>
    [Fact]
    public void OnOutputChunk_FansOnAllSink()
    {
        var sinkA = new CollectingSink();
        var sinkB = new CollectingDiagnosticsSink();
        var sut = new CompositeTerminalOutputSink(sinkA, sinkB);

        var chunk = Encoding.UTF8.GetBytes("abc");
        sut.OnOutputChunk(chunk);

        sinkA.Chunks.Should().Equal("abc");
        sinkB.Chunks.Should().Equal("abc");
    }

    /// <summary>Diagnose-Chunks erreichen nur innere Senken, die <see cref="ITerminalDiagnosticsSink"/>
    /// implementieren — byte-exakte Mitschnitt-Senken bleiben frei von Markerzeilen.</summary>
    [Fact]
    public void OnDiagnosticsChunk_GoesOnlyAnDiagnosticsSink()
    {
        var recorderSimilar = new CollectingSink();           // kein ITerminalDiagnoseSink
        var logSimilar = new CollectingDiagnosticsSink();  // implementiert ITerminalDiagnoseSink
        var sut = new CompositeTerminalOutputSink(recorderSimilar, logSimilar);

        sut.OnDiagnosticsChunk(Encoding.UTF8.GetBytes("[Terminal-Diagnose] test"));

        logSimilar.DiagnosticsChunks.Should().Equal("[Terminal-Diagnose] test");
        logSimilar.Chunks.Should().BeEmpty(
            "der Diagnose-Kanal darf nicht als normaler Output-Chunk in die Senke laufen");
        recorderSimilar.Chunks.Should().BeEmpty(
            "Senken ohne ITerminalDiagnoseSink erhalten keine Diagnose-Marker");
    }

    /// <summary><see cref="ITerminalOutputSink.Complete"/> und
    /// <see cref="ITerminalOutputSink.CompleteAsync"/> schließen alle inneren Senken ab.</summary>
    [Fact]
    public async Task Complete_ClosesAllInnerSinkFrom()
    {
        var sinkA = new CollectingSink();
        var sinkB = new CollectingDiagnosticsSink();
        var sut = new CompositeTerminalOutputSink(sinkA, sinkB);

        sut.Complete();
        sinkA.IsCompleted.Should().BeTrue();
        sinkB.IsCompleted.Should().BeTrue();

        var sinkC = new CollectingSink();
        var sinkD = new CollectingDiagnosticsSink();
        var sut2 = new CompositeTerminalOutputSink(sinkC, sinkD);
        await sut2.CompleteAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
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

    private sealed class CollectingDiagnosticsSink : CollectingSink, ITerminalDiagnosticsSink
    {
        private readonly List<string> _diagnosticsChunks = [];
        public IReadOnlyList<string> DiagnosticsChunks => _diagnosticsChunks;

        public void OnDiagnosticsChunk(ReadOnlySpan<byte> bytes)
            => _diagnosticsChunks.Add(Encoding.UTF8.GetString(bytes));
    }
}
