using FluentAssertions;
using Softwareschmiede.Infrastructure.Terminal;

namespace Softwareschmiede.Tests.Infrastructure.Terminal;

/// <summary>Unit-Tests für <see cref="TerminalReplayBuffer"/>.</summary>
public sealed class TerminalReplayBufferTests
{
    /// <summary>Ein Budget von 0 oder negativ ist ungültig.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ReplayBuffer_BudgetKleinerGleichNull_WirftArgumentOutOfRange(int budget)
    {
        var act = () => new TerminalReplayBuffer(budget);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    /// <summary>Angehängte Chunks werden in Eingangsreihenfolge zurückgegeben.</summary>
    [Fact]
    public void Append_UndGetChunks_LiefernChunksInReihenfolge()
    {
        var sut = new TerminalReplayBuffer(1024);

        sut.Append([1, 2, 3]);
        sut.Append([4, 5]);

        var chunks = sut.GetChunks();
        chunks.Should().HaveCount(2);
        chunks[0].Should().Equal([1, 2, 3]);
        chunks[1].Should().Equal([4, 5]);
    }

    /// <summary>Bei Budget-Überschreitung werden die ältesten Chunks verworfen.</summary>
    [Fact]
    public void Append_BudgetUeberschreitung_VerwirftAeltesteChunks()
    {
        var sut = new TerminalReplayBuffer(4);

        sut.Append([1, 2]);
        sut.Append([3, 4]);
        sut.Append([5, 6]);

        var chunks = sut.GetChunks();
        chunks.Should().HaveCount(2);
        chunks.SelectMany(c => c).Should().Equal([3, 4, 5, 6]);
    }

    /// <summary>Ein einzelner Chunk größer als das Budget wird auf die letzten Budget-Bytes gekürzt.</summary>
    [Fact]
    public void Append_ChunkGroesserAlsBudget_BehaeltNurLetzteBytes()
    {
        var sut = new TerminalReplayBuffer(4);

        sut.Append([1, 2, 3, 4, 5, 6]);

        var chunks = sut.GetChunks();
        chunks.Should().HaveCount(1);
        chunks[0].Should().Equal([3, 4, 5, 6]);
    }

    /// <summary>Leere Chunks werden ignoriert.</summary>
    [Fact]
    public void Append_LeererChunk_WirdIgnoriert()
    {
        var sut = new TerminalReplayBuffer(8);

        sut.Append(ReadOnlySpan<byte>.Empty);

        sut.GetChunks().Should().BeEmpty();
    }
}
