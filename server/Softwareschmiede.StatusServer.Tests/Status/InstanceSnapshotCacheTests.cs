using FluentAssertions;
using Softwareschmiede.StatusContracts;
using Softwareschmiede.StatusServer.Status;
using Softwareschmiede.StatusServer.Tests.TestSupport;

namespace Softwareschmiede.StatusServer.Tests.Status;

/// <summary>Tests für <see cref="InstanceSnapshotCache"/>.</summary>
public sealed class InstanceSnapshotCacheTests
{
    private static StatusSnapshot CreateSnapshot(string instanceId = "inst-1", string? name = "Mein PC")
        => new()
        {
            InstanceId = instanceId,
            InstanceName = name,
            CreatedUtc = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero),
            ProtocolVersion = RelayProtocolVersion.Current,
            AppVersion = "2.4.0",
        };

    /// <summary>Eine registrierte Verbindung ohne Snapshot ist verbunden und nicht veraltet.</summary>
    [Fact]
    public void MarkConnected_WithoutSnapshot_IsConnectedAndNotStale()
    {
        var time = new TestTimeProvider();
        var sut = new InstanceSnapshotCache(time, TimeSpan.FromSeconds(300));

        sut.MarkConnected("conn-1", "inst-1", "Mein PC");

        var view = sut.GetAll().Should().ContainSingle().Subject;
        view.Connected.Should().BeTrue();
        view.IsStale.Should().BeFalse();
        view.LastSeenUtc.Should().Be(time.Now);
        view.Snapshot.Should().BeNull();
        view.InstanceName.Should().Be("Mein PC");
    }

    /// <summary><see cref="InstanceSnapshotCache.UpdateSnapshot"/> speichert den Snapshot und schreibt lastSeenUtc fort.</summary>
    [Fact]
    public void UpdateSnapshot_StoresSnapshotAndAdvancesLastSeen()
    {
        var time = new TestTimeProvider();
        var sut = new InstanceSnapshotCache(time, TimeSpan.FromSeconds(300));
        sut.MarkConnected("conn-1", "inst-1", "Mein PC");

        time.Advance(TimeSpan.FromSeconds(30));
        sut.UpdateSnapshot("inst-1", CreateSnapshot());

        var view = sut.GetAll().Single();
        view.Snapshot.Should().NotBeNull();
        view.Snapshot!.AppVersion.Should().Be("2.4.0");
        view.Snapshot.InstanceId.Should().Be("inst-1");
        view.LastSeenUtc.Should().Be(time.Now);
        view.IsStale.Should().BeFalse();
    }

    /// <summary>Bei bestehender Verbindung wird ein zu alter Snapshot als veraltet markiert.</summary>
    [Fact]
    public void GetAll_ConnectedButOldSnapshot_IsStale()
    {
        var time = new TestTimeProvider();
        var sut = new InstanceSnapshotCache(time, TimeSpan.FromSeconds(300));
        sut.MarkConnected("conn-1", "inst-1", null);
        sut.UpdateSnapshot("inst-1", CreateSnapshot());

        time.Advance(TimeSpan.FromSeconds(301));

        var view = sut.GetAll().Single();
        view.Connected.Should().BeTrue();
        view.IsStale.Should().BeTrue();
    }

    /// <summary>Nach dem Trennen der letzten Verbindung bleibt der Snapshot erhalten, die Instanz ist veraltet.</summary>
    [Fact]
    public void MarkDisconnected_LastConnection_InstanceOfflineAndStale()
    {
        var time = new TestTimeProvider();
        var sut = new InstanceSnapshotCache(time, TimeSpan.FromSeconds(300));
        sut.MarkConnected("conn-1", "inst-1", null);
        sut.UpdateSnapshot("inst-1", CreateSnapshot());

        sut.MarkDisconnected("conn-1");

        var view = sut.GetAll().Single();
        view.Connected.Should().BeFalse();
        view.IsStale.Should().BeTrue();
        view.Snapshot.Should().NotBeNull();
    }

    /// <summary>Mehrere Instanzen bleiben getrennt verwaltet; fremde Verbindungen werden korrekt entfernt.</summary>
    [Fact]
    public void GetAll_MultipleInstances_TrackedSeparately()
    {
        var time = new TestTimeProvider();
        var sut = new InstanceSnapshotCache(time, TimeSpan.FromSeconds(300));
        sut.MarkConnected("conn-1", "inst-1", "A");
        sut.MarkConnected("conn-2", "inst-2", "B");

        sut.MarkDisconnected("conn-1");

        var views = sut.GetAll();
        views.Should().HaveCount(2);
        views.Single(v => v.InstanceId == "inst-1").Connected.Should().BeFalse();
        views.Single(v => v.InstanceId == "inst-2").Connected.Should().BeTrue();
    }

    /// <summary>Eine zweite Verbindung derselben Instanz hält die Instanz online, bis beide getrennt sind.</summary>
    [Fact]
    public void MarkDisconnected_OneOfTwoConnections_RemainsConnected()
    {
        var time = new TestTimeProvider();
        var sut = new InstanceSnapshotCache(time, TimeSpan.FromSeconds(300));
        sut.MarkConnected("conn-1", "inst-1", null);
        sut.MarkConnected("conn-2", "inst-1", null);

        sut.MarkDisconnected("conn-1");

        var view = sut.GetAll().Single();
        view.Connected.Should().BeTrue();
        view.IsStale.Should().BeFalse();
    }

    /// <summary>Eine Verbindung, die per RegisterInstance die Instanz wechselt, verlässt die alte Instanz.</summary>
    [Fact]
    public void MarkConnected_SameConnectionIdOnOtherInstance_RemovesFromOldInstance()
    {
        var time = new TestTimeProvider();
        var sut = new InstanceSnapshotCache(time, TimeSpan.FromSeconds(300));
        sut.MarkConnected("conn-1", "inst-1", "A");

        sut.MarkConnected("conn-1", "inst-2", "B");

        var views = sut.GetAll();
        views.Should().HaveCount(2);
        views.Single(v => v.InstanceId == "inst-1").Connected.Should().BeFalse();
        views.Single(v => v.InstanceId == "inst-2").Connected.Should().BeTrue();
    }

    /// <summary>Getrennte Instanzen werden nach Ablauf der Aufbewahrungszeit aus dem Cache entfernt.</summary>
    [Fact]
    public void GetAll_DisconnectedLongerThanRetention_EvictsInstance()
    {
        var time = new TestTimeProvider();
        var sut = new InstanceSnapshotCache(
            time, TimeSpan.FromSeconds(300), disconnectedRetention: TimeSpan.FromHours(1));
        sut.MarkConnected("conn-1", "inst-1", "A");
        sut.UpdateSnapshot("inst-1", CreateSnapshot());
        sut.MarkDisconnected("conn-1");

        time.Advance(TimeSpan.FromHours(1).Add(TimeSpan.FromSeconds(1)));

        sut.GetAll().Should().BeEmpty();
    }

    /// <summary>Verbundene Instanzen werden auch nach der Aufbewahrungszeit nicht entfernt.</summary>
    [Fact]
    public void GetAll_ConnectedLongerThanRetention_KeepsInstance()
    {
        var time = new TestTimeProvider();
        var sut = new InstanceSnapshotCache(
            time, TimeSpan.FromSeconds(300), disconnectedRetention: TimeSpan.FromHours(1));
        sut.MarkConnected("conn-1", "inst-1", "A");
        sut.UpdateSnapshot("inst-1", CreateSnapshot());

        time.Advance(TimeSpan.FromHours(2));

        var view = sut.GetAll().Should().ContainSingle().Subject;
        view.Connected.Should().BeTrue();
        view.IsStale.Should().BeTrue();
    }
}
