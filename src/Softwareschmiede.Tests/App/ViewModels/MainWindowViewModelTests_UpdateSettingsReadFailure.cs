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
    private readonly UpdateSettingsReadFailureInterceptor _interceptor = new();

    /// <summary>Registriert den Fehler-Interceptor am DbContext.</summary>
    protected override void ConfigureDbContext(DbContextOptionsBuilder options)
        => options.AddInterceptors(_interceptor);

    /// <summary>
    /// Ein Lesefehler beim ersten Settings-Zugriff stoppt vor jedem Release-Request –
    /// für Startautomatik, manuelle Prüfung und direkten Installationsaufruf gleichermaßen.
    /// Es gibt keinen internen Retry; über die Settings-UI ist eine Wiederherstellung möglich.
    /// </summary>
    [Theory]
    [InlineData("startautomatik")]
    [InlineData("manuellPruefen")]
    [InlineData("manuellInstallieren")]
    public async Task UpdateSettingsReadFailure_InitialStopsBeforeReleaseRequest(string einstieg)
    {
        await SetUpdateSettingsAsync(new UpdateSettings(UpdateMode.NurPruefen, false));
        _updateServiceMock
            .Setup(s => s.CheckForUpdateAsync(It.IsAny<UpdateCheckOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UpdateCheckResult.UpdateVerfuegbar(NeuesUpdate));
        _safetyServiceMock.Setup(s => s.CheckAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliUpdateSafetyResult(0, []));

        var sut = CreateSut();

        var erwartetePruefungen = 0;
        if (einstieg == "manuellInstallieren")
        {
            // Zuvor ein sichtbares Angebot herstellen, damit der Installationspfad getestet wird
            await sut.InitializeUpdatesAfterWindowReadyAsync();
            sut.UpdateVerfuegbar.Should().BeTrue();
            erwartetePruefungen = 1;
        }

        _interceptor.ActivateFailure();
        var readsBefore = _interceptor.SettingsReadCount;

        switch (einstieg)
        {
            case "startautomatik":
                await sut.InitializeUpdatesAfterWindowReadyAsync();
                break;
            case "manuellPruefen":
                await ((AsyncRelayCommand)sut.UpdatePruefenCommand).ExecuteAsync();
                break;
            case "manuellInstallieren":
                await ((AsyncRelayCommand)sut.UpdateStartenCommand).ExecuteAsync();
                break;
        }

        _interceptor.SettingsReadCount.Should().Be(readsBefore + 1,
            "ein Lesefehler darf nicht intern wiederholt werden");
        _updateServiceMock.Verify(
            s => s.CheckForUpdateAsync(It.IsAny<UpdateCheckOptions>(), It.IsAny<CancellationToken>()),
            Times.Exactly(erwartetePruefungen),
            "nach einem Lesefehler darf kein Release-Request mehr gesendet werden");
        _safetyServiceMock.Verify(s => s.CheckAsync(It.IsAny<CancellationToken>()), Times.Never);
        _progressDialogMock.Verify(d => d.Show(It.IsAny<UpdateProgressViewModel>()), Times.Never);
        _updateServiceMock.Verify(
            s => s.PrepareUpdateAsync(It.IsAny<UpdateInfo>(), It.IsAny<IProgress<UpdatePreparationProgress>>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _updateServiceMock.Verify(
            s => s.StartPreparedUpdateAsync(It.IsAny<UpdatePreparationResult>(), It.IsAny<CancellationToken>()),
            Times.Never);
        sut.UpdateHinweis.Should().NotBeNullOrEmpty(
            "ein Lesefehler muss einen sichtbaren Hinweis setzen");
        sut.UpdateVerfuegbar.Should().BeFalse();
        sut.VerfuegbaresUpdate.Should().BeNull();
        sut.UpdateCheckLaeuft.Should().BeFalse();
        sut.UpdateWirdVorbereitet.Should().BeFalse();

        // Wiederherstellung: Fehler deaktivieren, Einstellungen über die UI neu laden und speichern
        _interceptor.DeactivateFailure();
        await SpeichereUpdateEinstellungenUeberUiAsync(
            sut, "Bei Programmstart pruefen und ausfuehren", true);

        sut.UpdatePruefenCommand.CanExecute(null).Should().BeTrue(
            "nach erfolgreichem Speichern muss die Prüfung wieder verfügbar sein");
        await ((AsyncRelayCommand)sut.UpdatePruefenCommand).ExecuteAsync();

        _updateServiceMock.Verify(
            s => s.CheckForUpdateAsync(
                It.Is<UpdateCheckOptions>(o => o.IncludePrereleases), It.IsAny<CancellationToken>()),
            Times.Once, "die Folgeprüfung muss die aktuelle Checkbox-Einstellung übergeben");
        _updateServiceMock.Verify(
            s => s.CheckForUpdateAsync(It.IsAny<UpdateCheckOptions>(), It.IsAny<CancellationToken>()),
            Times.Exactly(erwartetePruefungen + 1));
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
    public async Task UpdateSettingsReadFailure_BeforePreparationStopsInstall(bool automatisch)
    {
        await SetUpdateSettingsAsync(new UpdateSettings(
            automatisch ? UpdateMode.BeiProgrammstartPruefenUndAusfuehren : UpdateMode.NurPruefen, false));
        var aktivierenBeiAufruf = automatisch ? 1 : 2;
        var checkCalls = 0;
        _updateServiceMock
            .Setup(s => s.CheckForUpdateAsync(It.IsAny<UpdateCheckOptions>(), It.IsAny<CancellationToken>()))
            .Returns<UpdateCheckOptions, CancellationToken>((_, _) =>
            {
                checkCalls++;
                if (checkCalls == aktivierenBeiAufruf)
                    _interceptor.ActivateFailure();
                return Task.FromResult(UpdateCheckResult.UpdateVerfuegbar(NeuesUpdate));
            });
        _safetyServiceMock.Setup(s => s.CheckAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliUpdateSafetyResult(0, []));
        UpdateProgressViewModel? progressViewModel = null;
        _progressDialogMock.Setup(d => d.Show(It.IsAny<UpdateProgressViewModel>()))
            .Callback<UpdateProgressViewModel>(vm => progressViewModel = vm);

        var sut = CreateSut();
        await sut.InitializeUpdatesAfterWindowReadyAsync();
        if (!automatisch)
        {
            sut.UpdateVerfuegbar.Should().BeTrue();
            await ((AsyncRelayCommand)sut.UpdateStartenCommand).ExecuteAsync();
        }

        _updateServiceMock.Verify(
            s => s.PrepareUpdateAsync(It.IsAny<UpdateInfo>(), It.IsAny<IProgress<UpdatePreparationProgress>>(), It.IsAny<CancellationToken>()),
            Times.Never, "ein Lesefehler vor der Vorbereitung muss die Installation stoppen");
        _updateServiceMock.Verify(
            s => s.StartPreparedUpdateAsync(It.IsAny<UpdatePreparationResult>(), It.IsAny<CancellationToken>()),
            Times.Never);
        progressViewModel.Should().NotBeNull("der Fortschrittsdialog war bereits geöffnet");
        progressViewModel!.HasError.Should().BeTrue("späte Lesefehler müssen im Dialog sichtbar sein");
        progressViewModel.Message.Should().Contain("Update-Einstellungen");
        progressViewModel.CanClose.Should().BeTrue();
        sut.UpdateHinweis.Should().NotBeNullOrEmpty();
        sut.UpdateVerfuegbar.Should().BeFalse();
        sut.VerfuegbaresUpdate.Should().BeNull();
        sut.UpdateCheckLaeuft.Should().BeFalse();
        sut.UpdateWirdVorbereitet.Should().BeFalse();

        // Wiederherstellung über die Settings-UI: Neue Prüfung findet das Update erneut.
        var checksVorWiederherstellung = checkCalls;
        _interceptor.DeactivateFailure();
        await SpeichereUpdateEinstellungenUeberUiAsync(sut, "Nur Pruefen", false);
        await ((AsyncRelayCommand)sut.UpdatePruefenCommand).ExecuteAsync();

        _updateServiceMock.Verify(
            s => s.CheckForUpdateAsync(It.IsAny<UpdateCheckOptions>(), It.IsAny<CancellationToken>()),
            Times.Exactly(checksVorWiederherstellung + 1));
        sut.UpdateVerfuegbar.Should().BeTrue();
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
    public async Task UpdateSettingsReadFailure_BeforeUpdaterStartStopsInstall(bool automatisch)
    {
        await SetUpdateSettingsAsync(new UpdateSettings(
            automatisch ? UpdateMode.BeiProgrammstartPruefenUndAusfuehren : UpdateMode.NurPruefen, false));
        _updateServiceMock
            .Setup(s => s.CheckForUpdateAsync(It.IsAny<UpdateCheckOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UpdateCheckResult.UpdateVerfuegbar(NeuesUpdate));
        var prepareCalls = 0;
        _updateServiceMock
            .Setup(s => s.PrepareUpdateAsync(NeuesUpdate, It.IsAny<IProgress<UpdatePreparationProgress>>(), It.IsAny<CancellationToken>()))
            .Returns<UpdateInfo, IProgress<UpdatePreparationProgress>?, CancellationToken>((_, _, _) =>
            {
                prepareCalls++;
                _interceptor.ActivateFailure();
                return Task.FromResult(FertigeVorbereitung);
            });
        _safetyServiceMock.Setup(s => s.CheckAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliUpdateSafetyResult(0, []));
        UpdateProgressViewModel? progressViewModel = null;
        _progressDialogMock.Setup(d => d.Show(It.IsAny<UpdateProgressViewModel>()))
            .Callback<UpdateProgressViewModel>(vm => progressViewModel = vm);

        var sut = CreateSut();
        await sut.InitializeUpdatesAfterWindowReadyAsync();
        if (!automatisch)
        {
            sut.UpdateVerfuegbar.Should().BeTrue();
            await ((AsyncRelayCommand)sut.UpdateStartenCommand).ExecuteAsync();
        }

        prepareCalls.Should().Be(1);
        _updateServiceMock.Verify(
            s => s.StartPreparedUpdateAsync(It.IsAny<UpdatePreparationResult>(), It.IsAny<CancellationToken>()),
            Times.Never, "ein Lesefehler vor dem Updater-Start muss den Start verhindern");
        progressViewModel.Should().NotBeNull();
        progressViewModel!.HasError.Should().BeTrue();
        progressViewModel.Message.Should().Contain("Update-Einstellungen",
            "der Fehlerzustand darf nicht als erfolgreicher Start erscheinen");
        progressViewModel.CanClose.Should().BeTrue();
        sut.UpdateHinweis.Should().NotBeNullOrEmpty();
        sut.UpdateVerfuegbar.Should().BeFalse();
        sut.VerfuegbaresUpdate.Should().BeNull();
        sut.UpdateWirdVorbereitet.Should().BeFalse();

        // Wiederherstellung über die Settings-UI: Neue Prüfung findet das Update erneut.
        _interceptor.DeactivateFailure();
        await SpeichereUpdateEinstellungenUeberUiAsync(sut, "Nur Pruefen", false);
        await ((AsyncRelayCommand)sut.UpdatePruefenCommand).ExecuteAsync();

        sut.UpdateVerfuegbar.Should().BeTrue();
        _updateServiceMock.Verify(
            s => s.StartPreparedUpdateAsync(It.IsAny<UpdatePreparationResult>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
