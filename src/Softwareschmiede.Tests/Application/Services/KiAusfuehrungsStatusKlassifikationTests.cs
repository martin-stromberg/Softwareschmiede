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
        var jetzt = DateTimeOffset.UtcNow;
        DateTimeOffset? pausedUntilUtc = jetzt.AddMinutes(10);

        var result = pausedUntilUtc.IsCurrentlyPaused(jetzt);

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
        var jetzt = DateTimeOffset.UtcNow;
        DateTimeOffset? pausedUntilUtc = jetzt.AddMinutes(-10);

        var result = pausedUntilUtc.IsCurrentlyPaused(jetzt);

        result.Should().BeFalse();
    }

    /// <summary>IstAktuellPausiert liefert false, wenn der Pausenzeitpunkt exakt jetzt ist (striktes Größer-als).</summary>
    [Fact]
    public void IstAktuellPausiert_ShouldReturnFalse_WhenPausiertBisGenauJetzt()
    {
        var jetzt = DateTimeOffset.UtcNow;
        DateTimeOffset? pausedUntilUtc = jetzt;

        var result = pausedUntilUtc.IsCurrentlyPaused(jetzt);

        result.Should().BeFalse();
    }
}
