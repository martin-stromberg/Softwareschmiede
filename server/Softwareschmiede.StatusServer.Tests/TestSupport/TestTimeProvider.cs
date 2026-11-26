namespace Softwareschmiede.StatusServer.Tests.TestSupport;

/// <summary>Deterministische Zeitquelle für Tests: Die aktuelle Zeit ist direkt setzbar und lässt
/// sich über <see cref="Advance"/> ohne echtes Warten vorstellen.</summary>
internal sealed class TestTimeProvider : TimeProvider
{
    /// <summary>Aktuelle Zeit der Testquelle; direkt setzbar für Zeitvorgaben.</summary>
    internal DateTimeOffset Now { get; set; } = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    /// <inheritdoc/>
    public override DateTimeOffset GetUtcNow() => Now;

    /// <summary>Stellt die Testquelle um <paramref name="span"/> vor.</summary>
    /// <param name="span">Die Zeitspanne.</param>
    internal void Advance(TimeSpan span) => Now += span;
}
