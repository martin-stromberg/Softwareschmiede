using FluentAssertions;
using Softwareschmiede.Application.Services;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Tests für <see cref="WorkItemRunActivity"/>.</summary>
public sealed class WorkItemRunActivityTests
{
    /// <summary>IsActive liefert true, wenn ActiveRunId gesetzt und der Heartbeat frisch ist.</summary>
    [Fact]
    public void IsActive_ShouldReturnTrue_WhenRunIdSetAndHeartbeatFresh()
    {
        var nowUtc = DateTimeOffset.UtcNow;
        var lastHeartbeatUtc = nowUtc.AddSeconds(-5);

        var result = WorkItemRunActivity.IsActive("run-1", lastHeartbeatUtc, nowUtc);

        result.Should().BeTrue();
    }

    /// <summary>IsActive liefert false, wenn der Heartbeat älter als die Timeout-Schwelle ist.</summary>
    [Fact]
    public void IsActive_ShouldReturnFalse_WhenHeartbeatOlderThanTimeout()
    {
        var nowUtc = DateTimeOffset.UtcNow;
        var lastHeartbeatUtc = nowUtc.AddMinutes(-(WorkItemRecoveryService.HeartbeatTimeoutMinutes + 1));

        var result = WorkItemRunActivity.IsActive("run-1", lastHeartbeatUtc, nowUtc);

        result.Should().BeFalse();
    }

    /// <summary>IsActive liefert false, wenn der Heartbeat exakt die Timeout-Schwelle alt ist (striktes Kleiner-als).</summary>
    [Fact]
    public void IsActive_ShouldReturnFalse_WhenHeartbeatExactlyAtTimeout()
    {
        var nowUtc = DateTimeOffset.UtcNow;
        var lastHeartbeatUtc = nowUtc.AddMinutes(-WorkItemRecoveryService.HeartbeatTimeoutMinutes);

        var result = WorkItemRunActivity.IsActive("run-1", lastHeartbeatUtc, nowUtc);

        result.Should().BeFalse();
    }

    /// <summary>IsActive liefert false, wenn kein Heartbeat vorhanden ist.</summary>
    [Fact]
    public void IsActive_ShouldReturnFalse_WhenHeartbeatNull()
    {
        var result = WorkItemRunActivity.IsActive("run-1", null, DateTimeOffset.UtcNow);

        result.Should().BeFalse();
    }

    /// <summary>IsActive liefert false, wenn keine aktive RunId vorhanden ist, selbst bei frischem Heartbeat.</summary>
    [Fact]
    public void IsActive_ShouldReturnFalse_WhenRunIdNull()
    {
        var nowUtc = DateTimeOffset.UtcNow;
        var lastHeartbeatUtc = nowUtc.AddSeconds(-5);

        var result = WorkItemRunActivity.IsActive(null, lastHeartbeatUtc, nowUtc);

        result.Should().BeFalse();
    }
}
