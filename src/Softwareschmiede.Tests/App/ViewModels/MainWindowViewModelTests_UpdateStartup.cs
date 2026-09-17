using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Softwareschmiede.App.Services;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Application.Services.Updates;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Infrastructure.Data;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.App.ViewModels;

/// <summary>
/// Unit-Tests für den Update-Startfluss des <see cref="MainWindowViewModel"/>:
/// Konstruktor ohne Seiteneffekte, einmalige Startprüfung nach Fensterbereitschaft,
/// Modus-Aus, Einstellungsänderungen, Sicherheitsdialog und Gleichzeitigkeitsschutz.
/// </summary>
public sealed class MainWindowViewModelTests_UpdateStartup : MainWindowViewModelUpdateTestBase
{
    private static readonly UpdateInfo ReleaseCandidateUpdate = new(
        "1.2.3-rc.1", "v1.2.3-rc.1", "release.zip",
        new Uri("https://example.invalid/release.zip"), null, IsPrerelease: true);

    /// <summary>Der Konstruktor darf keine Update-Prüfung starten; beide Update-Commands bleiben gesperrt.</summary>
    [Fact]
    public async Task Constructor_DoesNotCheckUpdates()
    {
        var sut = CreateSut();

        await Task.Delay(300);

        _updateServiceMock.Verify(
            s => s.CheckForUpdateAsync(It.IsAny<UpdateCheckOptions>(), It.IsAny<CancellationToken>()),
            Times.Never);
        sut.UpdateVerfuegbar.Should().BeFalse();
        sut.UpdatePruefenCommand.CanExecute(null).Should()
            .BeFalse("vor dem erfolgreichen Laden der Update-Einstellungen ist die Prüfung gesperrt");
        sut.UpdateStartenCommand.CanExecute(null).Should().BeFalse();
    }

    /// <summary>
    /// Nach Fensterbereitschaft läuft der Startfluss exakt einmal; Aus führt keine Prüfung aus,
    /// NurPruefen meldet nur, der Startmodus installiert das gefundene Update sofort.
    /// </summary>
    [Theory]
    [InlineData(UpdateMode.Aus)]
    [InlineData(UpdateMode.NurPruefen)]
    [InlineData(UpdateMode.BeiProgrammstartPruefenUndAusfuehren)]
    public async Task WindowReady_StartsOnceWithImmediateResult(UpdateMode modus)
    {
        await SetUpdateSettingsAsync(new UpdateSettings(modus, false));
        _updateServiceMock
            .Setup(s => s.CheckForUpdateAsync(It.IsAny<UpdateCheckOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UpdateCheckResult.UpdateVerfuegbar(NeuesUpdate));
        _updateServiceMock
            .Setup(s => s.PrepareUpdateAsync(NeuesUpdate, It.IsAny<IProgress<UpdatePreparationProgress>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(FertigeVorbereitung);
        _safetyServiceMock.Setup(s => s.CheckAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliUpdateSafetyResult(0, []));

        var sut = CreateSut();

        await sut.InitializeUpdatesAfterWindowReadyAsync();
        await sut.InitializeUpdatesAfterWindowReadyAsync();

        var erwartetePruefungen = modus == UpdateMode.Aus ? Times.Never() : Times.Once();
        _updateServiceMock.Verify(
            s => s.CheckForUpdateAsync(It.IsAny<UpdateCheckOptions>(), It.IsAny<CancellationToken>()),
            erwartetePruefungen);

        var erwarteteInstallation = modus == UpdateMode.BeiProgrammstartPruefenUndAusfuehren
            ? Times.Once() : Times.Never();
        _safetyServiceMock.Verify(s => s.CheckAsync(It.IsAny<CancellationToken>()), erwarteteInstallation);
        _updateServiceMock.Verify(
            s => s.PrepareUpdateAsync(It.IsAny<UpdateInfo>(), It.IsAny<IProgress<UpdatePreparationProgress>>(), It.IsAny<CancellationToken>()),
            erwarteteInstallation);
        _updateServiceMock.Verify(
            s => s.StartPreparedUpdateAsync(It.IsAny<UpdatePreparationResult>(), It.IsAny<CancellationToken>()),
            erwarteteInstallation);

        sut.UpdateVerfuegbar.Should().Be(modus != UpdateMode.Aus);
        sut.UpdateCheckLaeuft.Should().BeFalse();
        sut.UpdateWirdVorbereitet.Should().BeFalse();
    }

    /// <summary>
    /// UpdateMode.Aus sperrt beide Commands und blockiert auch direkte ExecuteAsync-Aufrufe;
    /// ein Wechsel aus einem aktiven Modus verbirgt ein zuvor sichtbares Angebot.
    /// </summary>
    [Fact]
    public async Task Aus_BlocksBothCommandsAndDirectInvocation()
    {
        _updateServiceMock
            .Setup(s => s.CheckForUpdateAsync(It.IsAny<UpdateCheckOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UpdateCheckResult.UpdateVerfuegbar(NeuesUpdate));
        _safetyServiceMock.Setup(s => s.CheckAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliUpdateSafetyResult(0, []));

        // Phase 1: Aus ist bereits persistiert -> keine Prüfung, beide Commands gesperrt
        await SetUpdateSettingsAsync(new UpdateSettings(UpdateMode.Aus, false));
        var sut = CreateSut();

        await sut.InitializeUpdatesAfterWindowReadyAsync();

        sut.UpdatePruefenCommand.CanExecute(null).Should().BeFalse();
        sut.UpdateStartenCommand.CanExecute(null).Should().BeFalse();

        await ((AsyncRelayCommand)sut.UpdatePruefenCommand).ExecuteAsync();
        await ((AsyncRelayCommand)sut.UpdateStartenCommand).ExecuteAsync();

        _updateServiceMock.Verify(
            s => s.CheckForUpdateAsync(It.IsAny<UpdateCheckOptions>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _safetyServiceMock.Verify(s => s.CheckAsync(It.IsAny<CancellationToken>()), Times.Never);
        _updateServiceMock.Verify(
            s => s.PrepareUpdateAsync(It.IsAny<UpdateInfo>(), It.IsAny<IProgress<UpdatePreparationProgress>>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _updateServiceMock.Verify(
            s => s.StartPreparedUpdateAsync(It.IsAny<UpdatePreparationResult>(), It.IsAny<CancellationToken>()),
            Times.Never);

        // Phase 2: Wechsel aus aktivem Modus -> Angebot verschwindet, Commands gesperrt, kein weiterer Check
        await SetUpdateSettingsAsync(new UpdateSettings(UpdateMode.NurPruefen, false));
        var sut2 = CreateSut();
        await sut2.InitializeUpdatesAfterWindowReadyAsync();

        sut2.UpdateVerfuegbar.Should().BeTrue();
        sut2.UpdatePruefenCommand.CanExecute(null).Should().BeTrue();
        sut2.UpdateStartenCommand.CanExecute(null).Should().BeTrue();

        await SpeichereUpdateEinstellungenUeberUiAsync(sut2, "Aus", false);

        sut2.UpdateVerfuegbar.Should().BeFalse("ein Moduswechsel auf Aus muss das Angebot verwerfen");
        sut2.VerfuegbaresUpdate.Should().BeNull();
        sut2.UpdatePruefenCommand.CanExecute(null).Should().BeFalse();
        sut2.UpdateStartenCommand.CanExecute(null).Should().BeFalse();

        await ((AsyncRelayCommand)sut2.UpdatePruefenCommand).ExecuteAsync();
        await ((AsyncRelayCommand)sut2.UpdateStartenCommand).ExecuteAsync();

        _updateServiceMock.Verify(
            s => s.CheckForUpdateAsync(It.IsAny<UpdateCheckOptions>(), It.IsAny<CancellationToken>()),
            Times.Once, "nach dem Wechsel auf Aus darf keine weitere Prüfung mehr starten");
        _safetyServiceMock.Verify(s => s.CheckAsync(It.IsAny<CancellationToken>()), Times.Never);
        _updateServiceMock.Verify(
            s => s.PrepareUpdateAsync(It.IsAny<UpdateInfo>(), It.IsAny<IProgress<UpdatePreparationProgress>>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _updateServiceMock.Verify(
            s => s.StartPreparedUpdateAsync(It.IsAny<UpdatePreparationResult>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Eine Einstellungsänderung während einer laufenden Prüfung verwirft das verzögerte Ergebnis
    /// und übergibt beim Folgeaufruf die aktuelle Checkbox-Einstellung; eine Änderung während
    /// der Vorbereitung stoppt die Installation vor dem Updater-Start.
    /// </summary>
    [Fact]
    public async Task SettingsChange_DiscardsDelayedResultAndStopsPreparation()
    {
        await SetUpdateSettingsAsync(new UpdateSettings(UpdateMode.NurPruefen, true));

        var eingecheckteOptionen = new List<UpdateCheckOptions>();
        var verzoegertesErgebnis = new TaskCompletionSource<UpdateCheckResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var checkCalls = 0;
        _updateServiceMock
            .Setup(s => s.CheckForUpdateAsync(It.IsAny<UpdateCheckOptions>(), It.IsAny<CancellationToken>()))
            .Returns<UpdateCheckOptions, CancellationToken>((o, _) =>
            {
                checkCalls++;
                eingecheckteOptionen.Add(o);
                return checkCalls == 1
                    ? verzoegertesErgebnis.Task
                    : Task.FromResult(UpdateCheckResult.KeinUpdate());
            });
        _safetyServiceMock.Setup(s => s.CheckAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliUpdateSafetyResult(0, []));

        var sut = CreateSut();

        // a) Verzögertes Check-Ergebnis wird nach Kanalwechsel verworfen
        var laufendePruefung = ((AsyncRelayCommand)sut.UpdatePruefenCommand).ExecuteAsync();
        await SpeichereUpdateEinstellungenUeberUiAsync(sut, "Nur Pruefen", false);

        verzoegertesErgebnis.SetResult(UpdateCheckResult.UpdateVerfuegbar(ReleaseCandidateUpdate));
        await laufendePruefung.WaitAsync(TimeSpan.FromSeconds(10));

        sut.VerfuegbaresUpdate.Should().BeNull("das verzögerte Ergebnis stammt aus der alten Einstellungs-Generation");
        sut.UpdateVerfuegbar.Should().BeFalse();
        eingecheckteOptionen[0].IncludePrereleases.Should().BeTrue();

        // Folgeprüfung verwendet die aktuelle Einstellung (Checkbox jetzt aus)
        await ((AsyncRelayCommand)sut.UpdatePruefenCommand).ExecuteAsync();
        eingecheckteOptionen.Should().HaveCount(2);
        eingecheckteOptionen[1].IncludePrereleases.Should().BeFalse();
        sut.UpdateVerfuegbar.Should().BeFalse();

        // b) Einstellungsänderung während der Vorbereitung stoppt vor dem Updater-Start
        await SetUpdateSettingsAsync(new UpdateSettings(
            UpdateMode.BeiProgrammstartPruefenUndAusfuehren, false));
        var vorbereitungGestartet = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var vorbereitungFreigabe = new TaskCompletionSource<UpdatePreparationResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        _updateServiceMock
            .Setup(s => s.CheckForUpdateAsync(It.IsAny<UpdateCheckOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UpdateCheckResult.UpdateVerfuegbar(NeuesUpdate));
        _updateServiceMock
            .Setup(s => s.PrepareUpdateAsync(NeuesUpdate, It.IsAny<IProgress<UpdatePreparationProgress>>(), It.IsAny<CancellationToken>()))
            .Returns<UpdateInfo, IProgress<UpdatePreparationProgress>?, CancellationToken>((_, _, _) =>
            {
                vorbereitungGestartet.TrySetResult();
                return vorbereitungFreigabe.Task;
            });

        var sut2 = CreateSut();
        var startTask = sut2.InitializeUpdatesAfterWindowReadyAsync();
        await vorbereitungGestartet.Task.WaitAsync(TimeSpan.FromSeconds(10));

        await SpeichereUpdateEinstellungenUeberUiAsync(sut2, "Aus", false);
        vorbereitungFreigabe.SetResult(FertigeVorbereitung);
        await startTask.WaitAsync(TimeSpan.FromSeconds(10));

        _updateServiceMock.Verify(
            s => s.StartPreparedUpdateAsync(It.IsAny<UpdatePreparationResult>(), It.IsAny<CancellationToken>()),
            Times.Never, "eine Einstellungsänderung während der Vorbereitung muss den Updater-Start verhindern");
        sut2.UpdateVerfuegbar.Should().BeFalse();
        sut2.UpdateWirdVorbereitet.Should().BeFalse();
    }

    /// <summary>
    /// Ohne gefundenes Update oder bei nicht prüfbarer Release-Quelle startet die
    /// Startautomatik keine Installation und hinterlässt einen sauberen Zustand.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Startup_NoUpdateOrNotCheckable_DoesNotInstall(bool nichtPruefbar)
    {
        await SetUpdateSettingsAsync(new UpdateSettings(
            UpdateMode.BeiProgrammstartPruefenUndAusfuehren, false));
        _updateServiceMock
            .Setup(s => s.CheckForUpdateAsync(It.IsAny<UpdateCheckOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(nichtPruefbar
                ? UpdateCheckResult.NichtPruefbar("Release-Quelle nicht erreichbar")
                : UpdateCheckResult.KeinUpdate());

        var sut = CreateSut();
        await sut.InitializeUpdatesAfterWindowReadyAsync();

        _safetyServiceMock.Verify(s => s.CheckAsync(It.IsAny<CancellationToken>()), Times.Never);
        _updateServiceMock.Verify(
            s => s.PrepareUpdateAsync(It.IsAny<UpdateInfo>(), It.IsAny<IProgress<UpdatePreparationProgress>>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _updateServiceMock.Verify(
            s => s.StartPreparedUpdateAsync(It.IsAny<UpdatePreparationResult>(), It.IsAny<CancellationToken>()),
            Times.Never);
        sut.UpdateVerfuegbar.Should().BeFalse();
        sut.UpdateCheckLaeuft.Should().BeFalse();
        sut.UpdateWirdVorbereitet.Should().BeFalse();
        if (nichtPruefbar)
            sut.UpdateHinweis.Should().Contain("Release-Quelle nicht erreichbar");
        else
            sut.UpdateHinweis.Should().BeNull();
    }

    /// <summary>
    /// Ein abgelehnter Sicherheitsdialog stoppt den Update-Fluss vor Fortschrittsdialog,
    /// Download, Vorbereitung und Updater-Start – für Startautomatik und manuellen Start gleichermaßen.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Startup_SafetyDeclined_DoesNotContinue(bool automatisch)
    {
        await SetUpdateSettingsAsync(new UpdateSettings(
            automatisch ? UpdateMode.BeiProgrammstartPruefenUndAusfuehren : UpdateMode.NurPruefen, false));
        _updateServiceMock
            .Setup(s => s.CheckForUpdateAsync(It.IsAny<UpdateCheckOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UpdateCheckResult.UpdateVerfuegbar(NeuesUpdate));
        _safetyServiceMock.Setup(s => s.CheckAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliUpdateSafetyResult(1, ["Riskante Aufgabe"]));
        _dialogServiceMock.Setup(d => d.BestaetigenDialog(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(false);

        var sut = CreateSut();
        await sut.InitializeUpdatesAfterWindowReadyAsync();
        if (!automatisch)
            await ((AsyncRelayCommand)sut.UpdateStartenCommand).ExecuteAsync();

        _progressDialogMock.Verify(d => d.Show(It.IsAny<UpdateProgressViewModel>()), Times.Never,
            "der Fortschrittsdialog darf erst nach bestätigter Sicherheitsprüfung erscheinen");
        _updateServiceMock.Verify(
            s => s.PrepareUpdateAsync(It.IsAny<UpdateInfo>(), It.IsAny<IProgress<UpdatePreparationProgress>>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _updateServiceMock.Verify(
            s => s.StartPreparedUpdateAsync(It.IsAny<UpdatePreparationResult>(), It.IsAny<CancellationToken>()),
            Times.Never);
        sut.UpdateWirdVorbereitet.Should().BeFalse();
    }

    /// <summary>
    /// Abbruch über den Fortschrittsdialog durchläuft denselben CancellationToken bis in
    /// die Vorbereitung; der Dialog wird schließbar und kein Updater wird gestartet.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Startup_SafetyCancelled_DoesNotContinue(bool automatisch)
    {
        await SetUpdateSettingsAsync(new UpdateSettings(
            automatisch ? UpdateMode.BeiProgrammstartPruefenUndAusfuehren : UpdateMode.NurPruefen, false));
        _updateServiceMock
            .Setup(s => s.CheckForUpdateAsync(It.IsAny<UpdateCheckOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UpdateCheckResult.UpdateVerfuegbar(NeuesUpdate));
        // Falls die Vorbereitung doch erreicht wird, hängt sie ohne Abbruch unendlich –
        // der Test beweist so, dass der Dialog-Abbruch denselben CancellationToken auslöst.
        _updateServiceMock
            .Setup(s => s.PrepareUpdateAsync(It.IsAny<UpdateInfo>(), It.IsAny<IProgress<UpdatePreparationProgress>>(), It.IsAny<CancellationToken>()))
            .Returns<UpdateInfo, IProgress<UpdatePreparationProgress>?, CancellationToken>(async (_, _, token) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return FertigeVorbereitung;
            });
        _safetyServiceMock.Setup(s => s.CheckAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliUpdateSafetyResult(0, []));
        UpdateProgressViewModel? progressViewModel = null;
        _progressDialogMock.Setup(d => d.Show(It.IsAny<UpdateProgressViewModel>()))
            .Callback<UpdateProgressViewModel>(vm =>
            {
                progressViewModel = vm;
                vm.CancelCommand.Execute(null);
            });

        var sut = CreateSut();
        await sut.InitializeUpdatesAfterWindowReadyAsync();
        if (!automatisch)
            await ((AsyncRelayCommand)sut.UpdateStartenCommand).ExecuteAsync();

        // Ohne propagierten Abbruch würde die unendliche Vorbereitung den Aufruf nie beenden.
        progressViewModel.Should().NotBeNull();
        progressViewModel!.CanClose.Should().BeTrue();
        progressViewModel.CanCancel.Should().BeFalse();
        progressViewModel.Message.Should().Contain("abgebrochen");
        _updateServiceMock.Verify(
            s => s.StartPreparedUpdateAsync(It.IsAny<UpdatePreparationResult>(), It.IsAny<CancellationToken>()),
            Times.Never);
        sut.UpdateWirdVorbereitet.Should().BeFalse();
        sut.UpdateHinweis.Should().NotBeNullOrEmpty();

        // Der Vorgang ist vollständig freigegeben: Eine neue manuelle Prüfung ist möglich.
        await ((AsyncRelayCommand)sut.UpdatePruefenCommand).ExecuteAsync();
        _updateServiceMock.Verify(
            s => s.CheckForUpdateAsync(It.IsAny<UpdateCheckOptions>(), It.IsAny<CancellationToken>()),
            Times.Exactly(automatisch ? 2 : 3));
    }

    /// <summary>
    /// Fehler bei Vorbereitung oder Updater-Start führen zu einem sichtbaren Fehlerzustand
    /// im Fortschrittsdialog; kein separater Shutdown wird ausgelöst und das Gate wird freigegeben.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Startup_PrepareOrStartFailure_DoesNotShutdown(bool fehlerBeimStart)
    {
        await SetUpdateSettingsAsync(new UpdateSettings(
            UpdateMode.BeiProgrammstartPruefenUndAusfuehren, false));
        _updateServiceMock
            .Setup(s => s.CheckForUpdateAsync(It.IsAny<UpdateCheckOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UpdateCheckResult.UpdateVerfuegbar(NeuesUpdate));
        if (fehlerBeimStart)
        {
            _updateServiceMock
                .Setup(s => s.PrepareUpdateAsync(NeuesUpdate, It.IsAny<IProgress<UpdatePreparationProgress>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(FertigeVorbereitung);
            _updateServiceMock
                .Setup(s => s.StartPreparedUpdateAsync(FertigeVorbereitung, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Launcher konnte nicht gestartet werden"));
        }
        else
        {
            _updateServiceMock
                .Setup(s => s.PrepareUpdateAsync(NeuesUpdate, It.IsAny<IProgress<UpdatePreparationProgress>>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Asset-Download fehlgeschlagen"));
        }

        _safetyServiceMock.Setup(s => s.CheckAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliUpdateSafetyResult(0, []));
        UpdateProgressViewModel? progressViewModel = null;
        _progressDialogMock.Setup(d => d.Show(It.IsAny<UpdateProgressViewModel>()))
            .Callback<UpdateProgressViewModel>(vm => progressViewModel = vm);

        var sut = CreateSut();
        await sut.InitializeUpdatesAfterWindowReadyAsync();

        progressViewModel.Should().NotBeNull();
        progressViewModel!.HasError.Should().BeTrue("Fehler müssen im geöffneten Fortschrittsdialog sichtbar sein");
        progressViewModel.CanClose.Should().BeTrue();
        _updateServiceMock.Verify(
            s => s.StartPreparedUpdateAsync(It.IsAny<UpdatePreparationResult>(), It.IsAny<CancellationToken>()),
            fehlerBeimStart ? Times.Once() : Times.Never());
        sut.UpdateWirdVorbereitet.Should().BeFalse();
        sut.UpdateHinweis.Should().NotBeNullOrEmpty();

        // Das Gate ist freigegeben: Eine neue manuelle Prüfung läuft durch.
        await ((AsyncRelayCommand)sut.UpdatePruefenCommand).ExecuteAsync();
        _updateServiceMock.Verify(
            s => s.CheckForUpdateAsync(It.IsAny<UpdateCheckOptions>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    /// <summary>
    /// Parallele Prüf-, Start- und erneute Bereitschaftsaufrufe laufen nicht doppelt:
    /// Prüfung, Vorbereitung und Updater-Start bleiben exakt einmalig.
    /// </summary>
    [Fact]
    public async Task Startup_ConcurrentCommandsCannotDuplicateInstall()
    {
        await SetUpdateSettingsAsync(new UpdateSettings(
            UpdateMode.BeiProgrammstartPruefenUndAusfuehren, false));
        var pruefungGestartet = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var pruefungFreigabe = new TaskCompletionSource<UpdateCheckResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var vorbereitungGestartet = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var vorbereitungFreigabe = new TaskCompletionSource<UpdatePreparationResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        _updateServiceMock
            .Setup(s => s.CheckForUpdateAsync(It.IsAny<UpdateCheckOptions>(), It.IsAny<CancellationToken>()))
            .Returns<UpdateCheckOptions, CancellationToken>((_, _) =>
            {
                pruefungGestartet.TrySetResult();
                return pruefungFreigabe.Task;
            });
        _updateServiceMock
            .Setup(s => s.PrepareUpdateAsync(NeuesUpdate, It.IsAny<IProgress<UpdatePreparationProgress>>(), It.IsAny<CancellationToken>()))
            .Returns<UpdateInfo, IProgress<UpdatePreparationProgress>?, CancellationToken>((_, _, _) =>
            {
                vorbereitungGestartet.TrySetResult();
                return vorbereitungFreigabe.Task;
            });
        _safetyServiceMock.Setup(s => s.CheckAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliUpdateSafetyResult(0, []));

        var sut = CreateSut();
        var startTask = sut.InitializeUpdatesAfterWindowReadyAsync();
        await pruefungGestartet.Task.WaitAsync(TimeSpan.FromSeconds(10));

        // Parallele Aufrufe während die Prüfung läuft -> werden am Gate verworfen
        await ((AsyncRelayCommand)sut.UpdatePruefenCommand).ExecuteAsync();
        await ((AsyncRelayCommand)sut.UpdateStartenCommand).ExecuteAsync();
        await sut.InitializeUpdatesAfterWindowReadyAsync();

        pruefungFreigabe.SetResult(UpdateCheckResult.UpdateVerfuegbar(NeuesUpdate));
        await vorbereitungGestartet.Task.WaitAsync(TimeSpan.FromSeconds(10));

        // Auch während der laufenden Vorbereitung darf nichts doppelt starten
        await ((AsyncRelayCommand)sut.UpdatePruefenCommand).ExecuteAsync();
        await ((AsyncRelayCommand)sut.UpdateStartenCommand).ExecuteAsync();

        vorbereitungFreigabe.SetResult(FertigeVorbereitung);
        await startTask.WaitAsync(TimeSpan.FromSeconds(10));

        _updateServiceMock.Verify(
            s => s.CheckForUpdateAsync(It.IsAny<UpdateCheckOptions>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _updateServiceMock.Verify(
            s => s.PrepareUpdateAsync(It.IsAny<UpdateInfo>(), It.IsAny<IProgress<UpdatePreparationProgress>>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _updateServiceMock.Verify(
            s => s.StartPreparedUpdateAsync(FertigeVorbereitung, It.IsAny<CancellationToken>()),
            Times.Once);

        // Erneuter Bereitschaftsaufruf nach Abschluss startet keinen zweiten Ablauf
        await sut.InitializeUpdatesAfterWindowReadyAsync();
        _updateServiceMock.Verify(
            s => s.CheckForUpdateAsync(It.IsAny<UpdateCheckOptions>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
