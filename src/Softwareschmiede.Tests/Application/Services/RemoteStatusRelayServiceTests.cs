using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Application.Services.Updates;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Infrastructure.Data;
using Softwareschmiede.StatusContracts;
using Softwareschmiede.Tests.App.ViewModels;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>
/// Tests des <see cref="RemoteStatusRelayService"/> (Issue 478): Konfigurationslesen
/// (deaktiviert, fehlende URL/Token, vollständig) sowie der Verbindungsautomat
/// (Disabled/ConfigurationError, Connect → Register → Initial-Snapshot, Ablehnung,
/// Connect-Fehler → Reconnecting, Re-Registrierung nach Auto-Reconnect, Backoff und
/// gedrosselter Change-Push) mit gemocktem Relay-Client.
/// </summary>
public sealed class RemoteStatusRelayServiceTests : IDisposable
{
    private readonly SoftwareschmiedeDbContext _db;
    private readonly AppSettingService _appSettings;
    private readonly ServiceProvider _provider;
    private readonly Mock<IRemoteStatusRelayClient> _client = new();
    private readonly RemoteStatusConnectionInfo _info = new();
    private readonly InMemoryCredentialStoreForSettings _credentialStore = new();
    private readonly ScmEventStartupGate _startupGate = new();
    private readonly WorkItemRunDataChangedNotifier _runDataNotifier = new();

    private bool _clientConnected;

    /// <summary>RemoteStatusRelayServiceTests.</summary>
    public RemoteStatusRelayServiceTests()
    {
        _db = TestDbContextFactory.Create();
        _appSettings = new AppSettingService(_db, NullLogger<AppSettingService>.Instance);
        _provider = new ServiceCollection()
            .AddSingleton(_db)
            .AddSingleton(_appSettings)
            .BuildServiceProvider();
        _startupGate.Ready();
        _client.SetupGet(c => c.IsConnected).Returns(() => _clientConnected);
        _client
            .Setup(c => c.ConnectAsync(It.IsAny<Uri>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => _clientConnected = true)
            .Returns(Task.CompletedTask);
        _client
            .Setup(c => c.RegisterInstanceAsync(It.IsAny<RegisterInstanceRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RegisterInstanceResult.Ok);
        _client
            .Setup(c => c.PushSnapshotAsync(It.IsAny<StatusSnapshot>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _client
            .Setup(c => c.DisconnectAsync(It.IsAny<CancellationToken>()))
            .Callback(() => _clientConnected = false)
            .Returns(Task.CompletedTask);
    }

    /// <summary>Dispose.</summary>
    public void Dispose()
    {
        _provider.Dispose();
        _db.Dispose();
    }

    /// <summary>Deaktivierte Remote-Übersicht liefert eine Konfiguration mit Fehlertext und Enabled = false.</summary>
    [Fact]
    public async Task ReadConfigurationAsync_Disabled_ReturnsError()
    {
        var sut = CreateSut();

        var config = await sut.ReadConfigurationAsync(TestContext.Current.CancellationToken);

        config.Enabled.Should().BeFalse();
        config.Error.Should().NotBeNullOrEmpty();
        config.HubUrl.Should().BeNull();
        config.Token.Should().BeNull();
    }

    /// <summary>Aktiviert ohne URL liefert einen Konfigurationsfehler.</summary>
    [Fact]
    public async Task ReadConfigurationAsync_MissingUrl_ReturnsError()
    {
        await _appSettings.SetRemoteStatusEnabledAsync(true, TestContext.Current.CancellationToken);
        var sut = CreateSut();

        var config = await sut.ReadConfigurationAsync(TestContext.Current.CancellationToken);

        config.Enabled.Should().BeTrue();
        config.Error.Should().NotBeNullOrEmpty();
        config.HubUrl.Should().BeNull();
    }

    /// <summary>Aktiviert mit URL, aber ohne hinterlegtes Relay-Token liefert einen Konfigurationsfehler.</summary>
    [Fact]
    public async Task ReadConfigurationAsync_MissingToken_ReturnsError()
    {
        await ConfigureEnabledAsync();
        var sut = CreateSut();

        var config = await sut.ReadConfigurationAsync(TestContext.Current.CancellationToken);

        config.Enabled.Should().BeTrue();
        config.Error.Should().Contain("Token");
        config.Token.Should().BeNull();
    }

    /// <summary>Vollständige Konfiguration liefert Hub-URL mit /relay-Pfad, Token, Instanz-ID und Intervall.</summary>
    [Fact]
    public async Task ReadConfigurationAsync_Complete_ReturnsResolvedConfiguration()
    {
        await ConfigureEnabledAsync();
        _credentialStore.SetCredential(AppSettingService.RemoteStatusRelayTokenCredentialTarget, "geheim");
        var sut = CreateSut();

        var config = await sut.ReadConfigurationAsync(TestContext.Current.CancellationToken);

        config.Error.Should().BeNull();
        config.HubUrl.Should().Be(new Uri("http://localhost:5055/relay"));
        config.Token.Should().Be("geheim");
        config.InstanceId.Should().Be(
            await _appSettings.GetOrCreateRemoteStatusInstanceIdAsync(TestContext.Current.CancellationToken));
        config.InstanceName.Should().Be("Martin-PC");
        config.SnapshotIntervalSeconds.Should().Be(AppSettingService.RemoteStatusDefaultSnapshotIntervalSeconds);
    }

    /// <summary>Deaktiviert bleibt der Dienst im Zustand Disabled und verbindet nicht.</summary>
    [Fact]
    public async Task ExecuteAsync_Disabled_StaysDisabledAndDoesNotConnect()
    {
        var sut = CreateSut();
        await sut.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            await WaitForStateAsync(RemoteStatusConnectionState.Disabled);
            _client.Verify(
                c => c.ConnectAsync(It.IsAny<Uri>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }
        finally
        {
            await sut.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    /// <summary>Aktiviert ohne Token meldet der Dienst einen Konfigurationsfehler.</summary>
    [Fact]
    public async Task ExecuteAsync_MissingToken_SetsConfigurationError()
    {
        await ConfigureEnabledAsync();
        var sut = CreateSut();
        await sut.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            await WaitForStateAsync(RemoteStatusConnectionState.ConfigurationError);
            _client.Verify(
                c => c.ConnectAsync(It.IsAny<Uri>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }
        finally
        {
            await sut.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    /// <summary>Vollständig konfiguriert verbindet der Dienst, registriert die Instanz und pusht
    /// sofort einen ersten Snapshot; der Zustand steht auf Connected.</summary>
    [Fact]
    public async Task ExecuteAsync_Configured_ConnectsRegistersAndPushesInitialSnapshot()
    {
        await ConfigureEnabledAsync();
        _credentialStore.SetCredential(AppSettingService.RemoteStatusRelayTokenCredentialTarget, "geheim");
        var sut = CreateSut();
        await sut.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            await WaitForStateAsync(RemoteStatusConnectionState.Connected);
            // Der Initial-Push folgt dem Statuswechsel asynchron in derselben Schleifeniteration.
            var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(10);
            while (_info.LastSnapshotUtc is null && DateTimeOffset.UtcNow < deadline)
                await Task.Delay(50, TestContext.Current.CancellationToken);
            _client.Verify(
                c => c.ConnectAsync(new Uri("http://localhost:5055/relay"), "geheim", It.IsAny<CancellationToken>()),
                Times.Once);
            _client.Verify(
                c => c.RegisterInstanceAsync(
                    It.Is<RegisterInstanceRequest>(r =>
                        r.ProtocolVersion == RelayProtocolVersion.Current && r.InstanceName == "Martin-PC"),
                    It.IsAny<CancellationToken>()),
                Times.Once);
            _client.Verify(
                c => c.PushSnapshotAsync(It.IsAny<StatusSnapshot>(), It.IsAny<CancellationToken>()),
                Times.AtLeastOnce);
            _info.LastSnapshotUtc.Should().NotBeNull();
        }
        finally
        {
            await sut.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    /// <summary>Eine abgelehnte Registrierung meldet der Dienst als Error mit dem Ablehnungsgrund
    /// und trennt die Verbindung — die Pump-Schleife pusht keine unregistrierten Snapshots.</summary>
    [Fact]
    public async Task ExecuteAsync_RejectedRegistration_DisconnectsAndSetsErrorState()
    {
        await ConfigureEnabledAsync();
        _credentialStore.SetCredential(AppSettingService.RemoteStatusRelayTokenCredentialTarget, "geheim");
        _client
            .Setup(c => c.RegisterInstanceAsync(It.IsAny<RegisterInstanceRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RegisterInstanceResult.Rejected("Protokollversion inkompatibel"));
        var sut = CreateSut();
        await sut.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            await WaitForStateAsync(RemoteStatusConnectionState.Error);
            _info.LastError.Should().Contain("Protokollversion");
            _client.Verify(
                c => c.DisconnectAsync(It.IsAny<CancellationToken>()),
                Times.AtLeastOnce);
            // Der Dienst versucht den Zyklus mit Backoff erneut, pusht aber nie unregistriert.
            _client.Verify(
                c => c.PushSnapshotAsync(It.IsAny<StatusSnapshot>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }
        finally
        {
            await sut.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    /// <summary>Nach einem SignalR-Auto-Reconnect registriert der Dienst die Instanz erneut
    /// und stößt einen sofortigen Snapshot-Push an (die neue Verbindung ist serverseitig
    /// unregistriert, ohne Re-Register würde jeder Push mit HubException scheitern).</summary>
    [Fact]
    public async Task ExecuteAsync_AutoReconnect_ReregistersInstanceAndPushes()
    {
        await ConfigureEnabledAsync();
        _credentialStore.SetCredential(AppSettingService.RemoteStatusRelayTokenCredentialTarget, "geheim");
        var sut = CreateSut();
        await sut.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            await WaitForStateAsync(RemoteStatusConnectionState.Connected);
            var lastPush = await WaitForLastSnapshotAsync();
            _client.Verify(
                c => c.RegisterInstanceAsync(It.IsAny<RegisterInstanceRequest>(), It.IsAny<CancellationToken>()),
                Times.Once);

            // Simulierter Auto-Reconnect: neue Verbindungs-ID, noch verbunden.
            _client.Raise(c => c.Reconnected += null, "neue-verbindungs-id");

            var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(20);
            while (_info.LastSnapshotUtc <= lastPush && DateTimeOffset.UtcNow < deadline)
                await Task.Delay(50, TestContext.Current.CancellationToken);
            _client.Verify(
                c => c.RegisterInstanceAsync(
                    It.Is<RegisterInstanceRequest>(r => r.InstanceName == "Martin-PC"),
                    It.IsAny<CancellationToken>()),
                Times.Exactly(2));
            _client.Verify(
                c => c.PushSnapshotAsync(It.IsAny<StatusSnapshot>(), It.IsAny<CancellationToken>()),
                Times.AtLeast(2));
            _info.State.Should().Be(RemoteStatusConnectionState.Connected);
        }
        finally
        {
            await sut.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    /// <summary>Bei abgelehnter Re-Registrierung nach Auto-Reconnect trennt der Dienst die
    /// Verbindung und meldet Error, statt weiter unregistriert zu pumpen.</summary>
    [Fact]
    public async Task ExecuteAsync_RejectedReregister_DisconnectsAndSetsErrorState()
    {
        await ConfigureEnabledAsync();
        _credentialStore.SetCredential(AppSettingService.RemoteStatusRelayTokenCredentialTarget, "geheim");
        var registrations = 0;
        _client
            .Setup(c => c.RegisterInstanceAsync(It.IsAny<RegisterInstanceRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
                ++registrations == 1
                    ? RegisterInstanceResult.Ok
                    : RegisterInstanceResult.Rejected("Instanz-ID unbekannt"));
        var sut = CreateSut();
        await sut.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            await WaitForStateAsync(RemoteStatusConnectionState.Connected);
            await WaitForLastSnapshotAsync();

            _client.Raise(c => c.Reconnected += null, "neue-verbindungs-id");

            await WaitForStateAsync(RemoteStatusConnectionState.Error);
            _client.Verify(
                c => c.DisconnectAsync(It.IsAny<CancellationToken>()),
                Times.AtLeastOnce);
        }
        finally
        {
            await sut.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    /// <summary>Nach einem Connect-Fehler versucht der Dienst es mit exponentiellem Backoff
    /// erneut (5 s → 10 s …), deterministisch über FakeTimeProvider geprüft.</summary>
    [Fact]
    public async Task ExecuteAsync_ConnectFailure_RetriesWithExponentialBackoff()
    {
        await ConfigureEnabledAsync();
        _credentialStore.SetCredential(AppSettingService.RemoteStatusRelayTokenCredentialTarget, "geheim");
        var connectAttempts = 0;
        _client
            .Setup(c => c.ConnectAsync(It.IsAny<Uri>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => connectAttempts++)
            .ThrowsAsync(new InvalidOperationException("Server nicht erreichbar"));
        var time = new FakeTimeProvider();
        var sut = CreateSut(time);
        await sut.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            await WaitForStateAsync(RemoteStatusConnectionState.Reconnecting);
            connectAttempts.Should().Be(1);

            // Vor Ablauf des 5-s-Backoffs kein erneuter Versuch.
            time.Advance(RemoteStatusRelayService.InitialBackoff - TimeSpan.FromSeconds(1));
            await Task.Delay(300, TestContext.Current.CancellationToken);
            connectAttempts.Should().Be(1);

            // Nach 5 s der zweite Versuch; der nächste folgt erst nach 10 s (Verdopplung).
            time.Advance(TimeSpan.FromSeconds(1));
            await WaitForConnectAttemptsAsync(2);
            time.Advance(RemoteStatusRelayService.InitialBackoff * 2 - TimeSpan.FromSeconds(1));
            await Task.Delay(300, TestContext.Current.CancellationToken);
            connectAttempts.Should().Be(2);
            time.Advance(TimeSpan.FromSeconds(1));
            await WaitForConnectAttemptsAsync(3);
        }
        finally
        {
            await sut.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    /// <summary>Ein Change-Signal kurz nach dem letzten Push wird gedrosselt: Es gibt keinen
    /// sofortigen Push, aber spätestens nach dem Mindestabstand (10 s) — das Dirty-Flag bleibt
    /// bis dahin erhalten, statt mit der Drossel-Prüfung verloren zu gehen.</summary>
    [Fact]
    public async Task ExecuteAsync_ChangeSignal_PushesAfterMinPushInterval()
    {
        await ConfigureEnabledAsync();
        _credentialStore.SetCredential(AppSettingService.RemoteStatusRelayTokenCredentialTarget, "geheim");
        var time = new FakeTimeProvider();
        var sut = CreateSut(time);
        await sut.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            await WaitForStateAsync(RemoteStatusConnectionState.Connected);
            await WaitForLastSnapshotAsync();
            var pushesBefore = CountPushes();

            // Change-Signal innerhalb der Drossel: kein Sofort-Push, Flag bleibt gesetzt.
            _runDataNotifier.NotifyRunDataChanged(Guid.NewGuid());
            await Task.Delay(300, TestContext.Current.CancellationToken);
            CountPushes().Should().Be(pushesBefore);

            // Nach dem Mindestabstand wird der aufgestaute Change-Push nachgeholt.
            time.Advance(RemoteStatusRelayService.MinPushInterval + TimeSpan.FromSeconds(1));
            await WaitForPushCountAsync(pushesBefore + 1);
        }
        finally
        {
            await sut.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    /// <summary>Ein Connect-Fehler versetzt den Dienst in Reconnecting statt abzustürzen.</summary>
    [Fact]
    public async Task ExecuteAsync_ConnectFailure_SetsReconnecting()
    {
        await ConfigureEnabledAsync();
        _credentialStore.SetCredential(AppSettingService.RemoteStatusRelayTokenCredentialTarget, "geheim");
        _client
            .Setup(c => c.ConnectAsync(It.IsAny<Uri>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Server nicht erreichbar"));
        var sut = CreateSut();
        await sut.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            await WaitForStateAsync(RemoteStatusConnectionState.Reconnecting);
            _client.Verify(
                c => c.PushSnapshotAsync(It.IsAny<StatusSnapshot>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }
        finally
        {
            await sut.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    private async Task ConfigureEnabledAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        await _appSettings.SetRemoteStatusEnabledAsync(true, ct);
        await _appSettings.SetRemoteStatusUrlAsync("http://localhost:5055", ct);
        await _appSettings.SetRemoteStatusInstanceNameAsync("Martin-PC", ct);
    }

    private async Task WaitForStateAsync(RemoteStatusConnectionState expected)
    {
        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(10);
        while (_info.State != expected)
        {
            if (DateTimeOffset.UtcNow > deadline)
                _info.State.Should().Be(expected);
            await Task.Delay(50, TestContext.Current.CancellationToken);
        }
    }

    private async Task<DateTimeOffset> WaitForLastSnapshotAsync()
    {
        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(10);
        while (_info.LastSnapshotUtc is null)
        {
            if (DateTimeOffset.UtcNow > deadline)
                _info.LastSnapshotUtc.Should().NotBeNull();
            await Task.Delay(50, TestContext.Current.CancellationToken);
        }

        return _info.LastSnapshotUtc.Value;
    }

    private int CountPushes()
        => _client.Invocations.Count(i => i.Method.Name == nameof(IRemoteStatusRelayClient.PushSnapshotAsync));

    private async Task WaitForPushCountAsync(int expected)
    {
        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(10);
        while (CountPushes() < expected)
        {
            if (DateTimeOffset.UtcNow > deadline)
                CountPushes().Should().BeGreaterThanOrEqualTo(expected);
            await Task.Delay(50, TestContext.Current.CancellationToken);
        }
    }

    private async Task WaitForConnectAttemptsAsync(int expected)
    {
        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(10);
        while (_client.Invocations.Count(i => i.Method.Name == nameof(IRemoteStatusRelayClient.ConnectAsync)) < expected)
        {
            if (DateTimeOffset.UtcNow > deadline)
                _client.Invocations.Count(i => i.Method.Name == nameof(IRemoteStatusRelayClient.ConnectAsync))
                    .Should().BeGreaterThanOrEqualTo(expected);
            await Task.Delay(50, TestContext.Current.CancellationToken);
        }
    }

    private RemoteStatusRelayService CreateSut(TimeProvider? timeProvider = null)
    {
        var scopeFactory = _provider.GetRequiredService<IServiceScopeFactory>();
        var automationsCount = new Mock<IRunningAutomationsCount>();
        automationsCount.Setup(c => c.CountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(0);
        var versionProvider = new Mock<IApplicationVersionProvider>();
        versionProvider
            .Setup(v => v.GetInstalledVersionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((InstalledVersionInfo?)null);
        var projector = new StatusSnapshotProjector(
            scopeFactory,
            new Mock<IRunningAutomationStatusSource>().Object,
            automationsCount.Object,
            versionProvider.Object,
            TimeProvider.System,
            NullLogger<StatusSnapshotProjector>.Instance);
        return new RemoteStatusRelayService(
            scopeFactory,
            _client.Object,
            _info,
            projector,
            _runDataNotifier,
            new ScmEventChangeNotifier(),
            _startupGate,
            _credentialStore,
            timeProvider ?? TimeProvider.System,
            NullLogger<RemoteStatusRelayService>.Instance);
    }
}
