using FluentAssertions;
using Softwareschmiede.Application.Services;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Tests für <see cref="AufgabeLaufAktivitaet"/>.</summary>
public sealed class AufgabeLaufAktivitaetTests
{
    /// <summary>IstAktiv liefert true, wenn AktiveRunId gesetzt und der Heartbeat frisch ist.</summary>
    [Fact]
    public void IstAktiv_ShouldReturnTrue_WhenRunIdSetAndHeartbeatFresh()
    {
        var nowUtc = DateTimeOffset.UtcNow;
        var lastHeartbeatUtc = nowUtc.AddSeconds(-5);

        var result = AufgabeLaufAktivitaet.IstAktiv("run-1", lastHeartbeatUtc, nowUtc);

        result.Should().BeTrue();
    }

    /// <summary>IstAktiv liefert false, wenn der Heartbeat älter als die Timeout-Schwelle ist.</summary>
    [Fact]
    public void IstAktiv_ShouldReturnFalse_WhenHeartbeatOlderThanTimeout()
    {
        var nowUtc = DateTimeOffset.UtcNow;
        var lastHeartbeatUtc = nowUtc.AddMinutes(-(AufgabeRecoveryService.HeartbeatTimeoutMinutes + 1));

        var result = AufgabeLaufAktivitaet.IstAktiv("run-1", lastHeartbeatUtc, nowUtc);

        result.Should().BeFalse();
    }

    /// <summary>IstAktiv liefert false, wenn der Heartbeat exakt die Timeout-Schwelle alt ist (striktes Kleiner-als).</summary>
    [Fact]
    public void IstAktiv_ShouldReturnFalse_WhenHeartbeatExactlyAtTimeout()
    {
        var nowUtc = DateTimeOffset.UtcNow;
        var lastHeartbeatUtc = nowUtc.AddMinutes(-AufgabeRecoveryService.HeartbeatTimeoutMinutes);

        var result = AufgabeLaufAktivitaet.IstAktiv("run-1", lastHeartbeatUtc, nowUtc);

        result.Should().BeFalse();
    }

    /// <summary>IstAktiv liefert false, wenn kein Heartbeat vorhanden ist.</summary>
    [Fact]
    public void IstAktiv_ShouldReturnFalse_WhenHeartbeatNull()
    {
        var result = AufgabeLaufAktivitaet.IstAktiv("run-1", null, DateTimeOffset.UtcNow);

        result.Should().BeFalse();
    }

    /// <summary>IstAktiv liefert false, wenn keine aktive RunId vorhanden ist, selbst bei frischem Heartbeat.</summary>
    [Fact]
    public void IstAktiv_ShouldReturnFalse_WhenRunIdNull()
    {
        var nowUtc = DateTimeOffset.UtcNow;
        var lastHeartbeatUtc = nowUtc.AddSeconds(-5);

        var result = AufgabeLaufAktivitaet.IstAktiv(null, lastHeartbeatUtc, nowUtc);

        result.Should().BeFalse();
    }
}
