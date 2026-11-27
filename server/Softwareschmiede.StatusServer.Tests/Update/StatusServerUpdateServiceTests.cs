using System.Collections.Concurrent;
using FluentAssertions;
using Moq;
using msTools.Updater;
using Softwareschmiede.StatusServer.Update;

namespace Softwareschmiede.StatusServer.Tests.Update;

/// <summary>Tests für <see cref="StatusServerUpdateService"/> mit gemockten
/// Bibliotheksschnittstellen (<see cref="IAutoUpdateOrchestrator"/>,
/// <see cref="IAutoUpdatePackageStore"/>, <see cref="IAutoUpdateStateStore"/>).</summary>
public sealed class StatusServerUpdateServiceTests
{
    private static readonly CancellationToken s_ct = TestContext.Current.CancellationToken;

    private static readonly StatusServerUpdateSettings s_checkAndInstall =
        new(StatusServerUpdateChannel.ReleaseCandidate,
            StatusServerAutomationMode.CheckAndInstall, new TimeOnly(3, 30));

    private readonly Mock<IAutoUpdateOrchestrator> _orchestrator = new();
    private readonly Mock<IAutoUpdateStateStore> _stateStore = new();
    private readonly Mock<IInstalledVersionProvider> _installedVersionProvider = new();
    private readonly Mock<IAutoUpdatePackageStore> _packageStore = new();
    private readonly AutoUpdateOptions _autoUpdateOptions = new();
    private readonly AutoUpdateStatusService _statusService;
    private readonly StatusServerUpdateService _sut;

    /// <summary>Baut den Service mit echtem <see cref="AutoUpdateStatusService"/>
    /// über dem gemockten State-Store (damit der vom Service gelesene Snapshot
    /// steuerbar ist) und echter <see cref="StatusServerUpdateSourceFactory"/>.</summary>
    public StatusServerUpdateServiceTests()
    {
        _stateStore.Setup(s => s.ReadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((AutoUpdateStatusSnapshot?)null);
        _stateStore.Setup(s => s.WriteAsync(It.IsAny<AutoUpdateStatusSnapshot>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _installedVersionProvider.Setup(p => p.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstalledReleaseInfo("1.0.0", null, null, null, "win-x64"));

        _statusService = new AutoUpdateStatusService(_stateStore.Object, _installedVersionProvider.Object);
        _sut = CreateService();
    }

    /// <summary>Erzeugt eine weitere Service-Instanz auf denselben Abhängigkeiten —
    /// entspricht der Produktion, in der jeder Request-Scope eine eigene Instanz
    /// auf dem gemeinsamen <see cref="AutoUpdateOptions"/>-Singleton bekommt.</summary>
    private StatusServerUpdateService CreateService()
    {
        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient());
        var sourceFactory = new StatusServerUpdateSourceFactory(
            httpClientFactory.Object, new AutoUpdatePlatformResolver(), new StatusServerUpdateOptions());
        var statusMapper = new UpdateStatusMapper(_installedVersionProvider.Object, _packageStore.Object);
        return new StatusServerUpdateService(
            _orchestrator.Object, _statusService, _packageStore.Object,
            _autoUpdateOptions, sourceFactory, statusMapper);
    }

    private static AutoUpdateResult Result(AutoUpdateOutcome outcome, AutoUpdateState state, AutoUpdateResultCode code)
        => new(outcome, state, code, "test", null);

    /// <summary>Ein manuell ausgelöster Check läuft auch bei deaktivierter Automation;
    /// <see cref="AutoUpdateOptions.Enabled"/> wird für die Dauer erzwungen und danach
    /// wiederhergestellt. Bei gefundenem Update folgt automatisch der Download.</summary>
    [Fact]
    public async Task CheckAndDownload_Disabled_ForcesEnabledAndRestores()
    {
        _autoUpdateOptions.Enabled = false;
        _orchestrator.Setup(o => o.CheckForUpdateAsync(s_ct)).ReturnsAsync(
            Result(AutoUpdateOutcome.Success, AutoUpdateState.UpdateAvailable, AutoUpdateResultCode.UpdateAvailable));
        _orchestrator.Setup(o => o.DownloadAsync(s_ct)).ReturnsAsync(
            Result(AutoUpdateOutcome.Success, AutoUpdateState.ReadyToInstall, AutoUpdateResultCode.DownloadCompleted));

        var result = await _sut.CheckAndDownloadAsync(s_ct);

        result.State.Should().Be(AutoUpdateState.ReadyToInstall);
        _orchestrator.Verify(o => o.DownloadAsync(s_ct), Times.Once);
        _autoUpdateOptions.Enabled.Should().BeFalse("der Ausgangswert wird nach dem manuellen Lauf wiederhergestellt");
    }

    /// <summary>Findet die Prüfung kein Update, wird kein Download gestartet und
    /// das Prüfergebnis direkt zurückgegeben.</summary>
    [Fact]
    public async Task CheckAndDownload_NoUpdate_SkipsDownload()
    {
        _autoUpdateOptions.Enabled = true;
        _orchestrator.Setup(o => o.CheckForUpdateAsync(s_ct)).ReturnsAsync(
            Result(AutoUpdateOutcome.NoUpdate, AutoUpdateState.Idle, AutoUpdateResultCode.NoNewerUpdateAvailable));

        var result = await _sut.CheckAndDownloadAsync(s_ct);

        result.Outcome.Should().Be(AutoUpdateOutcome.NoUpdate);
        _orchestrator.Verify(o => o.DownloadAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Zwei parallele manuelle Läufe über zwei Service-Instanzen auf
    /// demselben <see cref="AutoUpdateOptions"/>-Singleton (wie in der Produktion
    /// je Request-Scope) werden vom Override-Lock serialisiert: Die Prüfung des
    /// zweiten Laufs startet erst nach dem Ende des ersten, jeder Lauf sieht
    /// <c>Enabled=true</c> und der Ausgangswert wird am Ende wiederhergestellt —
    /// ohne Lock könnte der erste finishende Lauf <c>Enabled=false</c>
    /// wiederherstellen, während der zweite noch läuft.</summary>
    [Fact]
    public async Task CheckAndDownload_ParallelRuns_AreSerializedAndKeepEnabled()
    {
        _autoUpdateOptions.Enabled = false;
        var second = CreateService();
        var firstInsideCheck = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseCheck = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var enabledAtCheck = new ConcurrentQueue<bool>();
        var checkCalls = 0;
        _orchestrator.Setup(o => o.CheckForUpdateAsync(It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                Interlocked.Increment(ref checkCalls);
                enabledAtCheck.Enqueue(_autoUpdateOptions.Enabled);
                firstInsideCheck.TrySetResult();
                await releaseCheck.Task;
                return Result(AutoUpdateOutcome.NoUpdate, AutoUpdateState.Idle,
                    AutoUpdateResultCode.NoNewerUpdateAvailable);
            });

        var firstRun = _sut.CheckAndDownloadAsync(s_ct);
        await firstInsideCheck.Task;
        var secondRun = second.CheckAndDownloadAsync(s_ct);

        // Dem zweiten Lauf Gelegenheit geben — ohne Lock läge seine Prüfung
        // bereits beim Orchestrator, statt auf den Override des ersten zu warten.
        await Task.Delay(200, s_ct);
        checkCalls.Should().Be(1, "der zweite manuelle Lauf wartet auf den Override-Lock des ersten");

        releaseCheck.SetResult();
        await Task.WhenAll(firstRun, secondRun);

        checkCalls.Should().Be(2);
        enabledAtCheck.Should().OnlyContain(e => e,
            "jeder Lauf erzwingt Enabled für die Dauer seines Overrides");
        _autoUpdateOptions.Enabled.Should().BeFalse(
            "der Ausgangswert wird erst wiederhergestellt, wenn kein Override mehr aktiv ist");
    }

    /// <summary>Ein während eines manuellen Laufs gespeicherter Einstellungswert
    /// wird vom <see langword="finally"/>-Restore des Enabled-Overrides nicht
    /// überschrieben: <see cref="StatusServerUpdateService.ApplySettingsAsync"/>
    /// wartet auf denselben Override-Lock; der zuletzt angewendete (persistierte)
    /// Wert gewinnt — ohne die Sperre stünde <c>Enabled</c> am Ende wieder auf dem
    /// alten <see langword="false"/>, obwohl „Prüfen und installieren" gespeichert
    /// wurde.</summary>
    [Fact]
    public async Task ApplySettings_DuringManualRun_WaitsAndKeepsPersistedValue()
    {
        _autoUpdateOptions.Enabled = false;
        var second = CreateService();
        var insideCheck = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseCheck = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _orchestrator.Setup(o => o.CheckForUpdateAsync(It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                insideCheck.TrySetResult();
                await releaseCheck.Task;
                return Result(AutoUpdateOutcome.NoUpdate, AutoUpdateState.Idle,
                    AutoUpdateResultCode.NoNewerUpdateAvailable);
            });

        var manualRun = _sut.CheckAndDownloadAsync(s_ct);
        await insideCheck.Task;

        var applyTask = second.ApplySettingsAsync(s_checkAndInstall);
        // Gelegenheit geben: Ohne gemeinsame Sperre wäre die Mutation längst
        // durchgelaufen (und würde später vom finally-Restore überschrieben).
        await Task.Delay(200, s_ct);
        applyTask.IsCompleted.Should().BeFalse(
            "ApplySettingsAsync mutiert die Optionen unter demselben Lock wie der Override");

        releaseCheck.SetResult();
        await Task.WhenAll(manualRun, applyTask);

        _autoUpdateOptions.Enabled.Should().BeTrue(
            "der gespeicherte Modus wird nicht vom finally-Restore des Overrides zurückgesetzt");
        (_autoUpdateOptions.Source as AutoUpdateGithubSource)?.Dispose();
    }

    /// <summary><see cref="AutoUpdateOptions.Enabled"/> wird auch dann
    /// wiederhergestellt, wenn der Orchestrator wirft.</summary>
    [Fact]
    public async Task CheckAndDownload_OrchestratorThrows_StillRestoresEnabled()
    {
        _autoUpdateOptions.Enabled = false;
        _orchestrator.Setup(o => o.CheckForUpdateAsync(s_ct)).ThrowsAsync(new InvalidOperationException("boom"));

        var act = () => _sut.CheckAndDownloadAsync(s_ct);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _autoUpdateOptions.Enabled.Should().BeFalse();
    }

    /// <summary>Die Downtime-Bestätigung wird unverändert an
    /// <see cref="IAutoUpdateOrchestrator.InstallAsync"/> weitergereicht.</summary>
    [Fact]
    public async Task StartInstall_PassesConfirmDowntime()
    {
        _orchestrator.Setup(o => o.InstallAsync(true, false, s_ct)).ReturnsAsync(
            Result(AutoUpdateOutcome.Success, AutoUpdateState.Installing, AutoUpdateResultCode.InstallationStarted));

        var result = await _sut.StartInstallAsync(confirmDowntime: true, s_ct);

        result.Outcome.Should().Be(AutoUpdateOutcome.Success);
        _orchestrator.Verify(o => o.InstallAsync(true, false, s_ct), Times.Once);
    }

    /// <summary>Liegt ein gefundenes, noch nicht geladenes Paket vor
    /// (<c>UpdateAvailable</c> ohne Download-Ergebnis), lädt der Service erst nach
    /// und startet danach die Installation.</summary>
    [Fact]
    public async Task StartInstall_UpdateAvailableWithoutDownload_DownloadsFirst()
    {
        var persisted = new AutoUpdateStatusSnapshot(
            AutoUpdateState.UpdateAvailable, "1.0.0", "9.9.9",
            DateTimeOffset.UtcNow, new AutoUpdateCheckResult("9.9.9", null, null, null, false),
            null, null, null, null, false, null, null, null);
        _stateStore.Setup(s => s.ReadAsync(It.IsAny<CancellationToken>())).ReturnsAsync(persisted);
        await _statusService.EnsureLoadedAsync(s_ct);

        _orchestrator.Setup(o => o.DownloadAsync(s_ct)).ReturnsAsync(
            Result(AutoUpdateOutcome.Success, AutoUpdateState.ReadyToInstall, AutoUpdateResultCode.DownloadCompleted));
        _orchestrator.Setup(o => o.InstallAsync(true, false, s_ct)).ReturnsAsync(
            Result(AutoUpdateOutcome.Success, AutoUpdateState.Installing, AutoUpdateResultCode.InstallationStarted));

        var result = await _sut.StartInstallAsync(confirmDowntime: true, s_ct);

        result.Outcome.Should().Be(AutoUpdateOutcome.Success);
        _orchestrator.Verify(o => o.DownloadAsync(s_ct), Times.Once);
        _orchestrator.Verify(o => o.InstallAsync(true, false, s_ct), Times.Once);
    }

    /// <summary>Ohne Lock gibt <see cref="StatusServerUpdateService.ResetLockAsync"/>
    /// <see langword="false"/> zurück und löscht nichts.</summary>
    [Fact]
    public async Task ResetLock_NoLock_ReturnsFalse()
    {
        _packageStore.Setup(s => s.GetLockCreatedAtAsync(s_ct))
            .ReturnsAsync((DateTimeOffset?)null);

        (await _sut.ResetLockAsync(s_ct)).Should().BeFalse();

        _packageStore.Verify(s => s.DeleteLockAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Ein vorhandener, aber nicht abgelaufener Lock wird nicht gelöscht.</summary>
    [Fact]
    public async Task ResetLock_NotStale_ReturnsFalse()
    {
        var createdAt = DateTimeOffset.UtcNow;
        _packageStore.Setup(s => s.GetLockCreatedAtAsync(s_ct)).ReturnsAsync(createdAt);
        _packageStore.Setup(s => s.IsLockStale(createdAt)).Returns(false);

        (await _sut.ResetLockAsync(s_ct)).Should().BeFalse();

        _packageStore.Verify(s => s.DeleteLockAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Ein abgelaufener Lock wird gelöscht und der persistierte Status
    /// (IsLocked/LockCreatedAt) zurückgesetzt.</summary>
    [Fact]
    public async Task ResetLock_Stale_DeletesAndCorrectsStatus()
    {
        var createdAt = DateTimeOffset.UtcNow.AddHours(-3);
        _packageStore.Setup(s => s.GetLockCreatedAtAsync(s_ct)).ReturnsAsync(createdAt);
        _packageStore.Setup(s => s.IsLockStale(createdAt)).Returns(true);
        _packageStore.Setup(s => s.DeleteLockAsync(s_ct)).ReturnsAsync(true);

        (await _sut.ResetLockAsync(s_ct)).Should().BeTrue();

        _packageStore.Verify(s => s.DeleteLockAsync(s_ct), Times.Once);
        _stateStore.Verify(s => s.WriteAsync(
            It.Is<AutoUpdateStatusSnapshot>(snap => !snap.IsLocked && snap.LockCreatedAt == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary><see cref="StatusServerUpdateService.ApplySettingsAsync"/> mapped die
    /// Einstellungen auf die Laufzeitoptionen und ersetzt
    /// <see cref="AutoUpdateOptions.Source"/> sofort (Kanalwechsel wirkt ohne Neustart).</summary>
    [Fact]
    public async Task ApplySettings_AppliesFlagsAndReplacesSource()
    {
        _autoUpdateOptions.Enabled = false;
        _autoUpdateOptions.AllowPrereleaseUpdates = false;
        var oldSource = new Mock<IAutoUpdateSource>().Object;
        _autoUpdateOptions.Source = oldSource;

        await _sut.ApplySettingsAsync(s_checkAndInstall);

        _autoUpdateOptions.Enabled.Should().BeTrue();
        _autoUpdateOptions.AllowPrereleaseUpdates.Should().BeTrue();
        _autoUpdateOptions.EnableAutomaticInstallation.Should().BeTrue();
        _autoUpdateOptions.ScheduledInstallTime.Should().Be(new TimeOnly(3, 30));
        _autoUpdateOptions.Source.Should().BeOfType<AutoUpdateGithubSource>()
            .And.NotBeSameAs(oldSource);
        (_autoUpdateOptions.Source as AutoUpdateGithubSource)?.Dispose();
    }
}
