using FluentAssertions;
using Softwareschmiede.Application.Services;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Tests für <see cref="AiExecutionStatusClassification"/>.</summary>
public sealed class KiAusfuehrungsStatusKlassifikationTests
{
    /// <summary>IstAktuellPausiert liefert true, wenn der Pausenzeitpunkt gesetzt ist und in der Zukunft liegt.</summary>
    [Fact]
    public void IstAktuellPausiert_ShouldReturnTrue_WhenPausiertBisInZukunft()
    {
        var now = DateTimeOffset.UtcNow;
        DateTimeOffset? pausedUntilUtc = now.AddMinutes(10);

        var result = pausedUntilUtc.IsCurrentlyPaused(now);

        result.Should().BeTrue();
    }

    /// <summary>IstAktuellPausiert liefert false, wenn kein Pausenzeitpunkt gesetzt ist.</summary>
    [Fact]
    public void IstAktuellPausiert_ShouldReturnFalse_WhenPausiertBisNull()
    {
        DateTimeOffset? pausedUntilUtc = null;

        var result = pausedUntilUtc.IsCurrentlyPaused(DateTimeOffset.UtcNow);

        result.Should().BeFalse();
    }

    /// <summary>IstAktuellPausiert liefert false, wenn der Pausenzeitpunkt in der Vergangenheit liegt (abgelaufene Pause zählt wieder als nicht pausiert).</summary>
    [Fact]
    public void IstAktuellPausiert_ShouldReturnFalse_WhenPausiertBisAbgelaufen()
    {
        var now = DateTimeOffset.UtcNow;
        DateTimeOffset? pausedUntilUtc = now.AddMinutes(-10);

        var result = pausedUntilUtc.IsCurrentlyPaused(now);

        result.Should().BeFalse();
    }

    /// <summary>IstAktuellPausiert liefert false, wenn der Pausenzeitpunkt exakt jetzt ist (striktes Größer-als).</summary>
    [Fact]
    public void IstAktuellPausiert_ShouldReturnFalse_WhenPausiertBisGenauJetzt()
    {
        var now = DateTimeOffset.UtcNow;
        DateTimeOffset? pausedUntilUtc = now;

        var result = pausedUntilUtc.IsCurrentlyPaused(now);

        result.Should().BeFalse();
    }
}
