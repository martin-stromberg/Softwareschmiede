using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Softwareschmiede.App.Services;
using Softwareschmiede.App.Services.Testing;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Application.Services.Updates;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Infrastructure.Data;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.App.ViewModels;

/// <summary>
/// T-09-Tests: GetUpdateSettingsAsync-Lesefehler an allen Grenzen (Startprüfung, vor Vorbereitung,
/// vor Updater-Start) stoppen den Update-Fluss sicher – ohne Fallback auf Defaults, Snapshots
/// oder interne Wiederholungen. Getestet mit echter SQLite-Datenbank und dem
/// <see cref="UpdateSettingsReadFailureInterceptor"/>.
/// </summary>
public sealed class MainWindowViewModelTests_UpdateSettingsReadFailure : MainWindowViewModelUpdateTestBase
{
    /// <summary>Einstiegspunkt, über den ein Updateversuch mit aktivem Lesefehler gestartet wird.</summary>
    public enum UpdateEinstieg
    {
        /// <summary>Einmalige Startautomatik nach Fensterbereitschaft.</summary>
        Startautomatik,
        /// <summary>Manuelle Prüfung über CheckUpdateCommand.</summary>
        CheckManual,
        /// <summary>Manueller Installationsstart über UpdateStartenCommand.</summary>
        InstallManual
    }

    private readonly UpdateSettingsReadFailureInterceptor _interceptor = new();

    /// <summary>Registriert den Fehler-Interceptor am DbContext.</summary>
    protected override void ConfigureDbContext(DbContextOptionsBuilder options)
        => options.AddInterceptors(_interceptor);

    /// <summary>
    /// Ein Lesefehler beim ersten Settings-Zugriff stoppt vor jedem Release-Request –
    /// für Startautomatik, manuelle Prüfung und direkten Installationsaufruf gleichermaßen.
    /// Es gibt keinen internen Retry; über die Einstellungen-UI ist eine Wiederherstellung möglich.
    /// </summary>
    [Theory]
    [InlineData(UpdateEinstieg.Startautomatik)]
    [InlineData(UpdateEinstieg.CheckManual)]
    [InlineData(UpdateEinstieg.InstallManual)]
    public async Task UpdateSettingsReadFailure_InitialStopsBeforeReleaseRequest(UpdateEinstieg einstieg)
    {
        await SetUpdateSettingsAsync(new UpdateSettings(UpdateMode.CheckOnly, UpdatePathChannelResolver.StableVersionsPath));
        _updateServiceMock
            .Setup(s => s.CheckForUpdateAsync(It.IsAny<UpdateCheckOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UpdateCheckResult.UpdateAvailable(NewUpdate));
        _safetyServiceMock.Setup(s => s.CheckAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliUpdateSafetyResult(0, []));

        var sut = CreateSut();

        var expectedCheck = 0;
        if (einstieg == UpdateEinstieg.InstallManual)
        {
            // Zuvor ein sichtbares Angebot herstellen, damit der Installationspfad getestet wird
            await sut.InitializeUpdatesAfterWindowReadyAsync(TestContext.Current.CancellationToken);
            sut.UpdateAvailable.Should().BeTrue();
            expectedCheck = 1;
        }

        _interceptor.ActivateFailure();
        var readsBefore = _interceptor.SettingsReadCount;

        switch (einstieg)
        {
            case UpdateEinstieg.Startautomatik:
                await sut.InitializeUpdatesAfterWindowReadyAsync(TestContext.Current.CancellationToken);
                break;
            case UpdateEinstieg.CheckManual:
                await ((AsyncRelayCommand)sut.CheckUpdateCommand).ExecuteAsync();
                break;
            case UpdateEinstieg.InstallManual:
                await ((AsyncRelayCommand)sut.StartUpdateCommand).ExecuteAsync();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(einstieg), einstieg, "Unbekannter Update-Einstieg.");
        }

        _interceptor.SettingsReadCount.Should().Be(readsBefore + 1,
            "ein Lesefehler darf nicht intern wiederholt werden");
        _updateServiceMock.Verify(
            s => s.CheckForUpdateAsync(It.IsAny<UpdateCheckOptions>(), It.IsAny<CancellationToken>()),
            Times.Exactly(expectedCheck),
            "nach einem Lesefehler darf kein Release-Request mehr gesendet werden");
        _safetyServiceMock.Verify(s => s.CheckAsync(It.IsAny<CancellationToken>()), Times.Never);
        _progressDialogMock.Verify(d => d.Show(It.IsAny<UpdateProgressViewModel>()), Times.Never);
        _updateServiceMock.Verify(
            s => s.PrepareUpdateAsync(It.IsAny<UpdateInfo>(), It.IsAny<IProgress<UpdatePreparationProgress>>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _updateServiceMock.Verify(
            s => s.StartPreparedUpdateAsync(It.IsAny<UpdatePreparationResult>(), It.IsAny<CancellationToken>()),
            Times.Never);
        sut.UpdateNotice.Should().NotBeNullOrEmpty(
            "ein Lesefehler muss einen sichtbaren Hinweis setzen");
        sut.UpdateAvailable.Should().BeFalse();
        sut.AvailableUpdate.Should().BeNull();
        sut.UpdateCheckRunning.Should().BeFalse();
        sut.UpdateBeingPrepared.Should().BeFalse();

        // Wiederherstellung: Fehler deaktivieren, Settings über die UI neu laden und speichern
        _interceptor.DeactivateFailure();
        await SaveUpdateSettingsOverUiAsync(
            sut, UpdateModeOption.ForMode(UpdateMode.CheckAndRunAtStartup), UpdatePathChannelResolver.PrereleaseVersionsPath);

        sut.CheckUpdateCommand.CanExecute(null).Should().BeTrue(
            "nach erfolgreichem Speichern muss die Prüfung wieder verfügbar sein");
        await ((AsyncRelayCommand)sut.CheckUpdateCommand).ExecuteAsync();

        _updateServiceMock.Verify(
            s => s.CheckForUpdateAsync(
                It.Is<UpdateCheckOptions>(o => o.UpdatePath == UpdatePathChannelResolver.PrereleaseVersionsPath), It.IsAny<CancellationToken>()),
            Times.Once, "die Folgeprüfung muss die aktuelle Update-Pfad-Einstellung übergeben");
        _updateServiceMock.Verify(
            s => s.CheckForUpdateAsync(It.IsAny<UpdateCheckOptions>(), It.IsAny<CancellationToken>()),
            Times.Exactly(expectedCheck + 1));
        _updateServiceMock.Verify(
            s => s.PrepareUpdateAsync(It.IsAny<UpdateInfo>(), It.IsAny<IProgress<UpdatePreparationProgress>>(), It.IsAny<CancellationToken>()),
            Times.Never, "eine manuelle Prüfung darf keine automatische Installation auslösen");
        _updateServiceMock.Verify(
            s => s.StartPreparedUpdateAsync(It.IsAny<UpdatePreparationResult>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Ein Lesefehler beim erneuten Settings-Read nach der Sicherheitsbestätigung stoppt
    /// die Installation vor der Vorbereitung; der Fortschrittsdialog zeigt den Fehler.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UpdateSettingsReadFailure_BeforePreparationStopsInstall(bool automatic)
    {
        await SetUpdateSettingsAsync(new UpdateSettings(
            automatic ? UpdateMode.CheckAndRunAtStartup : UpdateMode.CheckOnly, UpdatePathChannelResolver.StableVersionsPath));
        var activateAtCall = automatic ? 1 : 2;
        var checkCalls = 0;
        _updateServiceMock
            .Setup(s => s.CheckForUpdateAsync(It.IsAny<UpdateCheckOptions>(), It.IsAny<CancellationToken>()))
            .Returns<UpdateCheckOptions, CancellationToken>((_, _) =>
            {
                checkCalls++;
                if (checkCalls == activateAtCall)
                    _interceptor.ActivateFailure();
                return Task.FromResult(UpdateCheckResult.UpdateAvailable(NewUpdate));
            });
        _safetyServiceMock.Setup(s => s.CheckAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliUpdateSafetyResult(0, []));
        UpdateProgressViewModel? progressViewModel = null;
        _progressDialogMock.Setup(d => d.Show(It.IsAny<UpdateProgressViewModel>()))
            .Callback<UpdateProgressViewModel>(vm => progressViewModel = vm);

        var sut = CreateSut();
        await sut.InitializeUpdatesAfterWindowReadyAsync(TestContext.Current.CancellationToken);
        if (!automatic)
        {
            sut.UpdateAvailable.Should().BeTrue();
            await ((AsyncRelayCommand)sut.StartUpdateCommand).ExecuteAsync();
        }

        _updateServiceMock.Verify(
            s => s.PrepareUpdateAsync(It.IsAny<UpdateInfo>(), It.IsAny<IProgress<UpdatePreparationProgress>>(), It.IsAny<CancellationToken>()),
            Times.Never, "ein Lesefehler vor der Vorbereitung muss die Installation stoppen");
        _updateServiceMock.Verify(
            s => s.StartPreparedUpdateAsync(It.IsAny<UpdatePreparationResult>(), It.IsAny<CancellationToken>()),
            Times.Never);
        progressViewModel.Should().NotBeNull("der Fortschrittsdialog war bereits geöffnet");
        progressViewModel!.HasError.Should().BeTrue("späte Lesefehler müssen im Dialog sichtbar sein");
        progressViewModel.Message.Should().Contain("Update--Einstellungen");
        progressViewModel.CanClose.Should().BeTrue();
        sut.UpdateNotice.Should().NotBeNullOrEmpty();
        sut.UpdateAvailable.Should().BeFalse();
        sut.AvailableUpdate.Should().BeNull();
        sut.UpdateCheckRunning.Should().BeFalse();
        sut.UpdateBeingPrepared.Should().BeFalse();

        // Wiederherstellung über die Einstellungen-UI: Neue Prüfung findet das Update erneut.
        var checksBeforeRestore = checkCalls;
        _interceptor.DeactivateFailure();
        await SaveUpdateSettingsOverUiAsync(sut, UpdateModeOption.ForMode(UpdateMode.CheckOnly), UpdatePathChannelResolver.StableVersionsPath);
        await ((AsyncRelayCommand)sut.CheckUpdateCommand).ExecuteAsync();

        _updateServiceMock.Verify(
            s => s.CheckForUpdateAsync(It.IsAny<UpdateCheckOptions>(), It.IsAny<CancellationToken>()),
            Times.Exactly(checksBeforeRestore + 1));
        sut.UpdateAvailable.Should().BeTrue();
        _updateServiceMock.Verify(
            s => s.StartPreparedUpdateAsync(It.IsAny<UpdatePreparationResult>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Ein Lesefehler beim finalen Settings-Read nach erfolgreicher Vorbereitung verhindert
    /// den Updater-Start vollständig – kein MarkUpdaterStarting, kein Launcher, kein Shutdown.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UpdateSettingsReadFailure_BeforeUpdaterStartStopsInstall(bool automatic)
    {
        await SetUpdateSettingsAsync(new UpdateSettings(
            automatic ? UpdateMode.CheckAndRunAtStartup : UpdateMode.CheckOnly, UpdatePathChannelResolver.StableVersionsPath));
        _updateServiceMock
            .Setup(s => s.CheckForUpdateAsync(It.IsAny<UpdateCheckOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UpdateCheckResult.UpdateAvailable(NewUpdate));
        var prepareCalls = 0;
        _updateServiceMock
            .Setup(s => s.PrepareUpdateAsync(NewUpdate, It.IsAny<IProgress<UpdatePreparationProgress>>(), It.IsAny<CancellationToken>()))
            .Returns<UpdateInfo, IProgress<UpdatePreparationProgress>?, CancellationToken>((_, _, _) =>
            {
                prepareCalls++;
                _interceptor.ActivateFailure();
                return Task.FromResult(DonePreparation);
            });
        _safetyServiceMock.Setup(s => s.CheckAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliUpdateSafetyResult(0, []));
        UpdateProgressViewModel? progressViewModel = null;
        _progressDialogMock.Setup(d => d.Show(It.IsAny<UpdateProgressViewModel>()))
            .Callback<UpdateProgressViewModel>(vm => progressViewModel = vm);

        var sut = CreateSut();
        await sut.InitializeUpdatesAfterWindowReadyAsync(TestContext.Current.CancellationToken);
        if (!automatic)
        {
            sut.UpdateAvailable.Should().BeTrue();
            await ((AsyncRelayCommand)sut.StartUpdateCommand).ExecuteAsync();
        }

        prepareCalls.Should().Be(1);
        _updateServiceMock.Verify(
            s => s.StartPreparedUpdateAsync(It.IsAny<UpdatePreparationResult>(), It.IsAny<CancellationToken>()),
            Times.Never, "ein Lesefehler vor dem Updater-Start muss den Start verhindern");
        progressViewModel.Should().NotBeNull();
        progressViewModel!.HasError.Should().BeTrue();
        progressViewModel.Message.Should().Contain("Update--Einstellungen",
            "der Fehlerzustand darf nicht als erfolgreicher Start erscheinen");
        progressViewModel.CanClose.Should().BeTrue();
        sut.UpdateNotice.Should().NotBeNullOrEmpty();
        sut.UpdateAvailable.Should().BeFalse();
        sut.AvailableUpdate.Should().BeNull();
        sut.UpdateBeingPrepared.Should().BeFalse();

        // Wiederherstellung über die Einstellungen-UI: Neue Prüfung findet das Update erneut.
        _interceptor.DeactivateFailure();
        await SaveUpdateSettingsOverUiAsync(sut, UpdateModeOption.ForMode(UpdateMode.CheckOnly), UpdatePathChannelResolver.StableVersionsPath);
        await ((AsyncRelayCommand)sut.CheckUpdateCommand).ExecuteAsync();

        sut.UpdateAvailable.Should().BeTrue();
        _updateServiceMock.Verify(
            s => s.StartPreparedUpdateAsync(It.IsAny<UpdatePreparationResult>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
