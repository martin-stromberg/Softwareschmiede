using System.Runtime.InteropServices;
using System.Text.Json;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using Softwareschmiede.App.Services.Testing;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.Application.Services.Updates;
using Softwareschmiede.Tests.E2E.Views;
using Softwareschmiede.Tests.E2E.Views.Dialogs;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// Update-E2E-Szenarien E-01 bis E-07 (Plan U-07) gegen die isolierte
/// <see cref="UpdateE2EFixture"/>-Pipeline: Persistenz aller Update-Modi und der
/// Update-Pfad-Auswahl über die echte Settings-UI inklusive echtem Neustart,
/// modusabhängige Startautomatik, Asset-Auswahl je Update-Pfad (manuell und
/// automatisch), Invalidierung zuvor angebotener Updates durch gespeicherte Änderungen,
/// Nicht-prüfbar-/Fehler-/Abbruch-Robustheit, kontrollierte Settings-Lesefehler an allen
/// drei Grenzen (T-09, automatisch und manuell) sowie die Einmaligkeit des Startversuchs.
/// Jede Phase wird über einen eigenen Protokoll-Index abgegrenzt; negative Aussagen stehen
/// ausschließlich hinter abgeschlossenen Versuchs-/Phasenmarkierungen, nie hinter Timeouts.
/// </summary>
public partial class End2EndTest
{
    private const string UpdateModeOutLabel = UpdateModeText.Off;
    private const string UpdateModeOnlyCheckLabel = UpdateModeText.CheckOnly;
    private const string UpdateModeStartLabel = UpdateModeText.CheckAndRunAtStartup;
    private const string UpdatePathProductionLabel = UpdatePathText.StableVersions;
    private const string UpdatePathUpfrontLabel = UpdatePathText.PrereleaseVersions;
    private const string UpdateNoticeChannelConflictPart = "passt nicht";
    private const string UpdateDownloadGate = "download-block";
    private const string UpdateReleaseGate = "release-block";
    private const string UpdateNoticeNotCheckablePart = "nicht prüfbar";
    private const uint GwOwner = 4;

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr hWnd, uint andCmd);

    /// <summary>
    /// E-01: Alle drei exakten Update-Modus-Labels und beide Update-Pfade
    /// werden über die echte Settings-UI gesetzt, gespeichert und nach Weg-/Zurücknavigation
    /// sowie nach einem echten, DB-erhaltenden Neustart wieder angezeigt. Eine ungespeicherte
    /// Änderung wird über "Verwerfen" zurückgesetzt. Der lange Auswahltext bleibt bei der
    /// minimalen Fenstergröße lesbar (ComboBox ist breit genug).
    /// </summary>
    private async Task Settings_AllModesAndUpdatePathPersist()
    {
        using var fixture = new UpdateE2EFixture();
        await fixture.SetUpdateSettingsAsync(UpdateMode.Off, UpdatePathChannelResolver.PrereleaseVersionsPath);

        // Die Protokoll-Basis muss VOR dem Start gezogen werden: Der komplette Startlauf
        // (inkl. StartupUpdateCompleted) kann bereits während des Fenster-Wartens
        // protokolliert sein, bevor GetMainWindow zurückkehrt.
        var baseline = LogBase(fixture);
        var app = LaunchApp(ensureDatabaseDeleted: false, fixture.CreateStartEnvironment());
        Window? mainWindow = null;
        try
        {
            mainWindow = WaitOnRealMainwindow(app);
            var menu = new MenuView(mainWindow);
            await WaitOnEventSinceAsync(fixture, baseline, UpdateE2EEvents.StartupUpdateCompleted, Long);

            var settings = await OpenSettingsLoadedAsync(fixture, menu);
            Assert.Equal(UpdateModeOutLabel, settings.GetUpdateMode());
            Assert.Equal(UpdatePathUpfrontLabel, settings.GetUpdatePath());
            Assert.Equal(UpdatePathText.ChannelReleaseCandidate, settings.GetDerivedUpdateChannel());

            // Alle drei exakten Labels nacheinander setzen und speichern.
            settings.SetUpdateMode(UpdateModeOnlyCheckLabel).SaveSettings();
            WaitOnSaveEnabled(settings);
            Assert.Equal(UpdateModeOnlyCheckLabel, settings.GetUpdateMode());

            settings.SetUpdateMode(UpdateModeStartLabel).SaveSettings();
            WaitOnSaveEnabled(settings);
            Assert.Equal(UpdateModeStartLabel, settings.GetUpdateMode());

            // Minimale Fenstergröße (MinWidth/MinHeight 900x600 aus MainWindow.xaml):
            // Der lange Auswahltext muss im aufgeklappten Dropdown vollständig im
            // Fenster lesbar bleiben (TextWrapping statt horizontalem Abschneiden).
            settings.Window.Patterns.Transform.Pattern.Resize(900, 600);
            Thread.Sleep(300);
            var updateCombo = WaitForElement(settings.Window, cf => cf.ByName("Update-Mode"), Short);
            updateCombo.Patterns.ExpandCollapse.Pattern.Expand();
            var longerEntry = WaitForElement(
                updateCombo,
                cf => cf.ByName(UpdateModeStartLabel).And(cf.ByControlType(ControlType.ListItem)),
                Medium);
            var windowRect = settings.Window.BoundingRectangle;
            var entryRect = longerEntry.BoundingRectangle;
            Assert.True(
                entryRect.Left >= windowRect.Left
                && entryRect.Right <= windowRect.Right
                && entryRect.Top >= windowRect.Top
                && entryRect.Bottom <= windowRect.Bottom
                && entryRect.Width >= 200,
                $"Der lange Update-Modus-Text ist bei minimaler Fenstergröße nicht vollständig " +
                $"lesbar (Eintrag {entryRect}, Fenster {windowRect}).");
            updateCombo.Patterns.ExpandCollapse.Pattern.Collapse();

            settings.SetUpdateMode(UpdateModeOutLabel).SaveSettings();
            WaitOnSaveEnabled(settings);
            Assert.Equal(UpdateModeOutLabel, settings.GetUpdateMode());

            // Beide Update-Pfade setzen und speichern; der abgeleitete Kanal folgt der Auswahl.
            settings.SetUpdatePath(UpdatePathProductionLabel).SaveSettings();
            WaitOnSaveEnabled(settings);
            Assert.Equal(UpdatePathProductionLabel, settings.GetUpdatePath());
            Assert.Equal(UpdatePathText.ChannelStable, settings.GetDerivedUpdateChannel());
            settings.SetUpdatePath(UpdatePathUpfrontLabel).SaveSettings();
            WaitOnSaveEnabled(settings);
            Assert.Equal(UpdatePathUpfrontLabel, settings.GetUpdatePath());
            Assert.Equal(UpdatePathText.ChannelReleaseCandidate, settings.GetDerivedUpdateChannel());

            // "Nur prüfen" als Restart-Ausgangslage (kein Auto-Install beim Neustart).
            settings.SetUpdateMode(UpdateModeOnlyCheckLabel).SaveSettings();
            WaitOnSaveEnabled(settings);

            // Weg- und zurücknavigieren: die persistierten Werte werden erneut angezeigt.
            menu.NavigateToDashboard();
            settings = await OpenSettingsLoadedAsync(fixture, menu);
            Assert.Equal(UpdateModeOnlyCheckLabel, settings.GetUpdateMode());
            Assert.Equal(UpdatePathUpfrontLabel, settings.GetUpdatePath());

            // Ungespeicherte Änderung wird über "Verwerfen" zurückgesetzt.
            settings.SetUpdateMode(UpdateModeOutLabel);
            Assert.Equal(UpdateModeOutLabel, settings.GetUpdateMode());
            settings.SetUpdatePath(UpdatePathProductionLabel);
            Assert.Equal(UpdatePathProductionLabel, settings.GetUpdatePath());
            var readsBefore = fixture.CountEvent(UpdateE2EEvents.UpdateSettingsReadReached);
            settings.DiscardChanges();
            await fixture.WaitOnEventAsync(UpdateE2EEvents.UpdateSettingsReadReached, Medium,
                mindestens: readsBefore + 1);
            settings.WaitForUpdateMode(UpdateModeOnlyCheckLabel, Medium);
            settings.WaitForUpdatePath(UpdatePathUpfrontLabel, Medium);
            menu.NavigateToDashboard();

            // Echter Neustart mit derselben DB: beide Werte sind persistiert und steuern den
            // Startlauf sichtbar (Nur prüfen + Vorabversionen -> RC-Angebot mit
            // nicht-blockierender Konfliktwarnung, ohne Installation).
            baseline = LogBase(fixture);
            mainWindow = RestartAppPreservingDatabase();
            menu = new MenuView(mainWindow);
            await WaitOnEventSinceAsync(fixture, baseline, UpdateE2EEvents.StartupUpdateCompleted, Long);

            Assert.Equal(UpdateE2EFixture.RcVersion, menu.WaitForOfferedUpdateVersion(Medium));
            Assert.Contains(EntriesSince(fixture, baseline), e => e.Event == UpdateE2EEvents.UpdateCheckStarted
                && DataText(e, "updatePfad") == UpdatePathChannelResolver.PrereleaseVersionsPath
                && DataText(e, "kanal") == nameof(UpdateChannel.ReleaseCandidate));
            Assert.Contains(EntriesSince(fixture, baseline), e => e.Event == UpdateE2EEvents.UpdateCheckCompleted
                && DataText(e, "version") == UpdateE2EFixture.RcVersion
                && DataBool(e, "isPrerelease") == true);
            menu.WaitForUpdateNotice(UpdateNoticeChannelConflictPart, Medium);
            Assert.Equal(0, CountEventSince(fixture, baseline, UpdateE2EEvents.UpdateProcessStartRecorded));
            Assert.Equal(0, CountEventSince(fixture, baseline, UpdateE2EEvents.ShutdownRequested));

            settings = await OpenSettingsLoadedAsync(fixture, menu);
            Assert.Equal(UpdateModeOnlyCheckLabel, settings.GetUpdateMode());
            Assert.Equal(UpdatePathUpfrontLabel, settings.GetUpdatePath());
            Assert.Equal(UpdatePathText.ChannelReleaseCandidate, settings.GetDerivedUpdateChannel());
            menu.NavigateToDashboard();
        }
        finally
        {
            await SetUpdateControlBackAsync(fixture, mainWindow);
            CloseFixtureApp();
        }
    }

    /// <summary>
    /// E-02: Jeder Modus wird über die echte Settings-UI gespeichert, das eigene Fenster
    /// regulär geschlossen und mit gleicher DB neu gestartet. "Aus": kein Release-/Asset-
    /// Abruf, kein Launcher, kein Shutdown, Prüfen deaktiviert, Installieren verborgen.
    /// "Nur prüfen": Releaseabruf und sichtbares Stable-Angebot im Tooltip, kein
    /// Asset/Start/Shutdown. Startmodus: sofortige Releaseantwort, ohne Updateklick echter
    /// Fortschrittsdialog mit korrektem Owner, blockierter Download, freigegebene reale
    /// Vorbereitung mit Paket-/Skriptnachweis und genau einem Launcher-Erfolg vor genau
    /// einem Shutdown.
    /// </summary>
    private async Task Startup_ModesDriveUpdatePipeline()
    {
        using var fixture = new UpdateE2EFixture();
        await fixture.SetUpdateSettingsAsync(UpdateMode.CheckOnly, UpdatePathChannelResolver.StableVersionsPath);

        // Die Protokoll-Basis muss VOR dem Start gezogen werden: Der komplette Startlauf
        // (inkl. StartupUpdateCompleted) kann bereits während des Fenster-Wartens
        // protokolliert sein, bevor GetMainWindow zurückkehrt.
        var baseline = LogBase(fixture);
        var app = LaunchApp(ensureDatabaseDeleted: false, fixture.CreateStartEnvironment());
        Window? mainWindow = null;
        try
        {
            mainWindow = WaitOnRealMainwindow(app);
            var menu = new MenuView(mainWindow);
            await WaitOnEventSinceAsync(fixture, baseline, UpdateE2EEvents.StartupUpdateCompleted, Long);

            // Modus "Aus" über die UI speichern, regulär schließen, gleiche DB neu starten.
            await SaveUpdateSettingsOverUiAsync(fixture, menu, UpdateModeOutLabel, UpdatePathProductionLabel);
            baseline = LogBase(fixture);
            mainWindow = RestartAppPreservingDatabase();
            menu = new MenuView(mainWindow);
            await WaitOnEventSinceAsync(fixture, baseline, UpdateE2EEvents.StartupUpdateCompleted, Long);

            WaitOnUiState(() => !menu.IsUpdateCheckButtonEnabled(),
                "Prüfen-Button im Modus Aus deaktiviert", Medium);
            Assert.False(menu.IsUpdateStartButtonVisible());
            Assert.Equal(0, CountEventSince(fixture, baseline, UpdateE2EEvents.HttpRequest));
            Assert.Equal(0, CountEventSince(fixture, baseline, UpdateE2EEvents.UpdateCheckStarted));
            AssertNoUpdateSideeffectSince(fixture, baseline);

            // Modus "Nur prüfen" über die UI speichern, regulär schließen, neu starten.
            await SaveUpdateSettingsOverUiAsync(fixture, menu, UpdateModeOnlyCheckLabel, UpdatePathProductionLabel);
            baseline = LogBase(fixture);
            mainWindow = RestartAppPreservingDatabase();
            menu = new MenuView(mainWindow);
            await WaitOnEventSinceAsync(fixture, baseline, UpdateE2EEvents.StartupUpdateCompleted, Long);

            Assert.Contains(EntriesSince(fixture, baseline), e => e.Event == UpdateE2EEvents.HttpRequest
                && DataText(e, "url") == UpdateE2EFixture.ReleaseApiUrl);
            Assert.Contains(EntriesSince(fixture, baseline), e => e.Event == UpdateE2EEvents.UpdateCheckCompleted
                && DataText(e, "status") == "UpdateVerfuegbar"
                && DataText(e, "version") == UpdateE2EFixture.StableVersion
                && DataBool(e, "isPrerelease") == false);
            Assert.Equal(UpdateE2EFixture.StableVersion, menu.WaitForOfferedUpdateVersion(Medium));
            Assert.Equal(0, CountEventSince(fixture, baseline, UpdateE2EEvents.HttpRequest,
                e => DataText(e, "art") == "asset"));
            AssertNoUpdateSideeffectSince(fixture, baseline);

            // Startmodus über die UI speichern; der ZIP-Stream wird per Gate blockiert.
            await SaveUpdateSettingsOverUiAsync(fixture, menu, UpdateModeStartLabel, UpdatePathProductionLabel);
            fixture.UpdateScenario(s =>
            {
                var package = s.Answer.Single(a => a.Url == fixture.StableDownloadUrl);
                package.StreamGate = UpdateDownloadGate;
                package.StreamGateBytes = 128;
            });
            baseline = LogBase(fixture);
            mainWindow = RestartAppPreservingDatabase();
            menu = new MenuView(mainWindow);

            // Ohne Updateklick erscheint der echte Fortschrittsdialog; der Download blockiert
            // kontrolliert im Stream-Gate, nachdem die Releaseantwort sofort ankam.
            var dialogElement = WaitForWindow("Update vorbereiten", Long);
            await WaitOnEventSinceAsync(fixture, baseline, UpdateE2EEvents.HttpRequestBlocked, Long,
                filter: e => DataText(e, "gate") == UpdateDownloadGate);
            Assert.Contains(EntriesSince(fixture, baseline), e => e.Event == UpdateE2EEvents.HttpResponse
                && DataText(e, "url") == UpdateE2EFixture.ReleaseApiUrl);

            // Korrekter Owner: Der Fortschrittsdialog gehört dem Hauptfenster.
            Assert.Equal(
                mainWindow.FrameworkAutomationElement.NativeWindowHandle,
                GetWindow(dialogElement.FrameworkAutomationElement.NativeWindowHandle, GwOwner));

            var progress = new UpdateProgressDialogView(mainWindow);
            Assert.True(progress.IsVisible);
            WaitOnUiState(() => progress.GetPhase() == "Download", "Download-Phase", Medium);
            Assert.True(progress.CanCancel());
            WaitOnUiState(() => progress.GetProgress() is > 0 and < 100,
                "Teilfortschritt des blockierten Downloads", Medium);

            // Während des blockierten Downloads bleiben beide Update-Commands gesperrt.
            WaitOnUiState(() => !menu.IsUpdateCheckButtonEnabled(), "Prüfen während Download gesperrt", Medium);
            Assert.False(menu.IsUpdateStartButtonEnabled());

            // Freigabe: reale Vorbereitung bis zur kontrollierten Launcher-/Shutdown-Grenze.
            fixture.OpenGate(UpdateDownloadGate);
            await WaitOnEventSinceAsync(fixture, baseline, UpdateE2EEvents.StartupUpdateCompleted, Long);
            AssertInstallationProof(fixture, baseline,
                UpdateE2EFixture.StableVersion, fixture.StableDownloadUrl, "stable-marker");
            progress.Close();
            fixture.SetGateBack(UpdateDownloadGate);
        }
        finally
        {
            await SetUpdateControlBackAsync(fixture, mainWindow);
            CloseFixtureApp();
        }
    }

    /// <summary>
    /// E-03: Der Update-Pfad wählt exklusiv den Kanal. In "Nur prüfen" werden beide Pfade
    /// per UI gespeichert und der Prüfbutton geklickt: der Produktiv-Pfad bietet exakt
    /// Stable an, der Vorab-Pfad exakt RC - eine semantisch höhere stabile Version wird im
    /// RC-Kanal nicht gewählt. Der Installationsbutton führt die echte Vorbereitung bis zur
    /// aufgezeichneten Übergabe aus; Download-URL, entpackte <c>version.json</c> und
    /// Skriptziel gehören exakt zur gewählten Version. Der Kanalkonflikt (produktive
    /// Installation + Vorab-Pfad) zeigt die nicht-blockierende Warnung im UpdateNotice
    /// und im Versuchsprotokoll. Beide Pfade werden zusätzlich mit UI-gespeichertem
    /// Startmodus und je echtem Neustart ohne Updateklick geprüft.
    /// </summary>
    private async Task UpdatePath_SelectsExclusiveChannelAsset()
    {
        using var fixture = new UpdateE2EFixture();
        await fixture.SetUpdateSettingsAsync(UpdateMode.Off, UpdatePathChannelResolver.StableVersionsPath);

        // Die Protokoll-Basis muss VOR dem Start gezogen werden: Der komplette Startlauf
        // (inkl. StartupUpdateCompleted) kann bereits während des Fenster-Wartens
        // protokolliert sein, bevor GetMainWindow zurückkehrt.
        var baseline = LogBase(fixture);
        var app = LaunchApp(ensureDatabaseDeleted: false, fixture.CreateStartEnvironment());
        Window? mainWindow = null;
        try
        {
            mainWindow = WaitOnRealMainwindow(app);
            var menu = new MenuView(mainWindow);
            await WaitOnEventSinceAsync(fixture, baseline, UpdateE2EEvents.StartupUpdateCompleted, Long);

            // Produktiv-Pfad: exakt Stable wird angeboten und installiert.
            await SaveUpdateSettingsOverUiAsync(fixture, menu, UpdateModeOnlyCheckLabel, UpdatePathProductionLabel);
            await CheckUpdateOverUiAsync(fixture, menu, baseline = LogBase(fixture),
                UpdateE2EFixture.StableVersion, isPrerelease: false, UpdatePathProductionLabel);

            baseline = LogBase(fixture);
            menu.StartClickUpdate();
            await WaitOnEventSinceAsync(fixture, baseline, UpdateE2EEvents.UpdateAttemptCompleted, Long,
                filter: e => DataText(e, "versuch") == "starten");
            AssertInstallationProof(fixture, baseline,
                UpdateE2EFixture.StableVersion, fixture.StableDownloadUrl, "stable-marker");
            Assert.Equal(0, CountEventSince(fixture, baseline, UpdateE2EEvents.HttpRequest,
                e => DataText(e, "url") == fixture.RcDownloadUrl));
            new UpdateProgressDialogView(mainWindow).Close();

            // Vorab-Pfad mit semantisch höherer stabiler Konkurrenz: exakt RC - die höhere
            // stabile Version ist kanalfremd. Produktive Installation + Vorab-Pfad ergibt
            // den Kanalkonflikt: Angebot bleibt, die Warnung ist nicht blockierend.
            fixture.UpdateScenario(s =>
                s.Answer.Single(a => a.Url == UpdateE2EFixture.ReleaseApiUrl).Content =
                    CreateRcAndHigherStableReleaseJson(fixture));
            await SaveUpdateSettingsOverUiAsync(fixture, menu, UpdateModeOnlyCheckLabel, UpdatePathUpfrontLabel);
            await CheckUpdateOverUiAsync(fixture, menu, baseline = LogBase(fixture),
                UpdateE2EFixture.RcVersion, isPrerelease: true, UpdatePathUpfrontLabel);
            menu.WaitForUpdateNotice(UpdateNoticeChannelConflictPart, Medium);
            Assert.Contains(EntriesSince(fixture, baseline), e => e.Event == UpdateE2EEvents.UpdateAttemptCompleted
                && DataText(e, "versuch") == "pruefen"
                && (DataText(e, "hinweis") ?? string.Empty).Contains(UpdateNoticeChannelConflictPart));

            baseline = LogBase(fixture);
            menu.StartClickUpdate();
            await WaitOnEventSinceAsync(fixture, baseline, UpdateE2EEvents.UpdateAttemptCompleted, Long,
                filter: e => DataText(e, "versuch") == "starten");
            AssertInstallationProof(fixture, baseline,
                UpdateE2EFixture.RcVersion, fixture.RcDownloadUrl, "rc-marker");
            Assert.Equal(0, CountEventSince(fixture, baseline, UpdateE2EEvents.HttpRequest,
                e => DataText(e, "url") == fixture.StableDownloadUrl));
            new UpdateProgressDialogView(mainWindow).Close();

            // Startmodus + Produktiv-Pfad über die UI speichern: echter Neustart installiert
            // Stable ohne Updateklick.
            fixture.SetDefaultResponses();
            fixture.SaveScenario();
            await SaveUpdateSettingsOverUiAsync(fixture, menu, UpdateModeStartLabel, UpdatePathProductionLabel);
            baseline = LogBase(fixture);
            mainWindow = RestartAppPreservingDatabase();
            menu = new MenuView(mainWindow);
            await WaitOnEventSinceAsync(fixture, baseline, UpdateE2EEvents.StartupUpdateCompleted, Long);
            AssertInstallationProof(fixture, baseline,
                UpdateE2EFixture.StableVersion, fixture.StableDownloadUrl, "stable-marker");
            new UpdateProgressDialogView(mainWindow).Close();

            // Startmodus + Vorab-Pfad: echter Neustart installiert RC ohne Updateklick;
            // die Konfliktwarnung erscheint erneut, blockiert die Installation aber nicht.
            await SaveUpdateSettingsOverUiAsync(fixture, menu, UpdateModeStartLabel, UpdatePathUpfrontLabel);
            baseline = LogBase(fixture);
            mainWindow = RestartAppPreservingDatabase();
            menu = new MenuView(mainWindow);
            await WaitOnEventSinceAsync(fixture, baseline, UpdateE2EEvents.StartupUpdateCompleted, Long);
            AssertInstallationProof(fixture, baseline,
                UpdateE2EFixture.RcVersion, fixture.RcDownloadUrl, "rc-marker");
            menu.WaitForUpdateNotice(UpdateNoticeChannelConflictPart, Medium);
            Assert.Contains(EntriesSince(fixture, baseline), e => e.Event == UpdateE2EEvents.StartupUpdateCompleted
                && (DataText(e, "hinweis") ?? string.Empty).Contains(UpdateNoticeChannelConflictPart));
            new UpdateProgressDialogView(mainWindow).Close();
        }
        finally
        {
            await SetUpdateControlBackAsync(fixture, mainWindow);
            CloseFixtureApp();
        }
    }

    /// <summary>
    /// E-04: Gespeicherte Änderungen invalidieren das zuvor angebotene Update sofort.
    /// RC-Angebot -&gt; Update-Pfad auf Produktivversionen + speichern: Angebot weg, Installieren nicht
    /// aktivierbar, kein neuer Request. Erneute Prüfung bietet Stable an; die Installation
    /// fordert nur das Stable-Asset an. Erneutes RC-Angebot -&gt; "Aus" speichern: Prüfen
    /// deaktiviert, Angebot verborgen, keine neuen Requests/Starts; gleiche DB neu gestartet
    /// bestätigt den Nullabruf. Ein laufender Prüfrequest wird blockiert, währenddessen "Aus"
    /// gespeichert und die Antwort danach freigegeben: kein spätes Angebot, kein Download.
    /// Der direkte stale-Installationsaufruf ist ergänzend in
    /// <c>MainWindowViewModelTests_UpdateStartup</c> abgedeckt.
    /// </summary>
    private async Task SavedChangesInvalidatePreviousOffer()
    {
        using var fixture = new UpdateE2EFixture();
        await fixture.SetUpdateSettingsAsync(UpdateMode.CheckOnly, UpdatePathChannelResolver.PrereleaseVersionsPath);

        // Die Protokoll-Basis muss VOR dem Start gezogen werden: Der komplette Startlauf
        // (inkl. StartupUpdateCompleted) kann bereits während des Fenster-Wartens
        // protokolliert sein, bevor GetMainWindow zurückkehrt.
        var baseline = LogBase(fixture);
        var app = LaunchApp(ensureDatabaseDeleted: false, fixture.CreateStartEnvironment());
        Window? mainWindow = null;
        try
        {
            mainWindow = WaitOnRealMainwindow(app);
            var menu = new MenuView(mainWindow);
            await WaitOnEventSinceAsync(fixture, baseline, UpdateE2EEvents.StartupUpdateCompleted, Long);
            Assert.Equal(UpdateE2EFixture.RcVersion, menu.WaitForOfferedUpdateVersion(Medium));

            // Update-Pfad auf Produktivversionen + speichern: das alte RC-Angebot verschwindet sofort, ohne dass
            // das Speichern selbst einen Update-Request auslöst.
            baseline = LogBase(fixture);
            await SaveUpdateSettingsOverUiAsync(fixture, menu, UpdateModeOnlyCheckLabel, UpdatePathProductionLabel);
            WaitOnUiState(() => !menu.IsUpdateStartButtonVisible(),
                "Updateangebot nach Speichern entfernt", Medium);
            Assert.Null(menu.GetOfferedUpdateVersion());
            Assert.False(menu.IsUpdateStartButtonEnabled());
            Assert.Equal(0, CountEventSince(fixture, baseline, UpdateE2EEvents.HttpRequest));

            // Erneut prüfen: Stable-Angebot; Installation fordert nur das Stable-Asset an.
            baseline = LogBase(fixture);
            menu.CheckClickUpdate();
            await WaitOnEventSinceAsync(fixture, baseline, UpdateE2EEvents.UpdateAttemptCompleted, Long,
                filter: e => DataText(e, "versuch") == "pruefen");
            Assert.Equal(UpdateE2EFixture.StableVersion, menu.WaitForOfferedUpdateVersion(Medium));

            menu.StartClickUpdate();
            await WaitOnEventSinceAsync(fixture, baseline, UpdateE2EEvents.UpdateAttemptCompleted, Long,
                filter: e => DataText(e, "versuch") == "starten");
            AssertInstallationProof(fixture, baseline,
                UpdateE2EFixture.StableVersion, fixture.StableDownloadUrl, "stable-marker");
            new UpdateProgressDialogView(mainWindow).Close();

            // Erneut RC anbieten (Update-Pfad wieder auf Vorabversionen).
            await SaveUpdateSettingsOverUiAsync(fixture, menu, UpdateModeOnlyCheckLabel, UpdatePathUpfrontLabel);
            baseline = LogBase(fixture);
            menu.CheckClickUpdate();
            await WaitOnEventSinceAsync(fixture, baseline, UpdateE2EEvents.UpdateAttemptCompleted, Long,
                filter: e => DataText(e, "versuch") == "pruefen");
            Assert.Equal(UpdateE2EFixture.RcVersion, menu.WaitForOfferedUpdateVersion(Medium));

            // Laufenden Prüfrequest blockieren; währenddessen "Aus" speichern.
            fixture.UpdateScenario(s =>
                s.Answer.Single(a => a.Url == UpdateE2EFixture.ReleaseApiUrl).Gate = UpdateReleaseGate);
            baseline = LogBase(fixture);
            menu.CheckClickUpdate();
            await WaitOnEventSinceAsync(fixture, baseline, UpdateE2EEvents.HttpRequestBlocked, Long,
                filter: e => DataText(e, "url") == UpdateE2EFixture.ReleaseApiUrl
                    && DataText(e, "gate") == UpdateReleaseGate);

            await SaveUpdateSettingsOverUiAsync(fixture, menu, UpdateModeOutLabel, UpdatePathUpfrontLabel);
            WaitOnUiState(() => !menu.IsUpdateStartButtonVisible(),
                "Updateangebot nach Aus-Speichern entfernt", Medium);
            WaitOnUiState(() => !menu.IsUpdateCheckButtonEnabled(),
                "Prüfen im Modus Aus deaktiviert", Medium);

            // Antwort freigeben: das späte Ergebnis wird verworfen.
            fixture.OpenGate(UpdateReleaseGate);
            await WaitOnEventSinceAsync(fixture, baseline, UpdateE2EEvents.UpdateAttemptCompleted, Long,
                filter: e => DataText(e, "versuch") == "pruefen");

            var phaseEntries = EntriesSince(fixture, baseline);
            Assert.Contains(phaseEntries, e => e.Event == UpdateE2EEvents.UpdateCheckCompleted
                && DataText(e, "version") == UpdateE2EFixture.RcVersion);
            Assert.Null(menu.GetOfferedUpdateVersion());
            Assert.False(menu.IsUpdateStartButtonVisible());
            Assert.DoesNotContain(phaseEntries, e => e.Event == UpdateE2EEvents.HttpRequest
                && DataText(e, "art") == "asset");
            AssertNoUpdateSideeffectSince(fixture, baseline);
            fixture.UpdateScenario(s =>
                s.Answer.Single(a => a.Url == UpdateE2EFixture.ReleaseApiUrl).Gate = null);
            fixture.SetGateBack(UpdateReleaseGate);

            // Gleiche DB neu starten: der Modus Aus erzeugt den Nullabruf.
            baseline = LogBase(fixture);
            mainWindow = RestartAppPreservingDatabase();
            menu = new MenuView(mainWindow);
            await WaitOnEventSinceAsync(fixture, baseline, UpdateE2EEvents.StartupUpdateCompleted, Long);
            Assert.Equal(1, CountEventSince(fixture, baseline, UpdateE2EEvents.UpdateAttemptStarted));
            Assert.Equal(0, CountEventSince(fixture, baseline, UpdateE2EEvents.HttpRequest));
            Assert.Equal(0, CountEventSince(fixture, baseline, UpdateE2EEvents.UpdateCheckStarted));
            AssertNoUpdateSideeffectSince(fixture, baseline);
            WaitOnUiState(() => !menu.IsUpdateCheckButtonEnabled(),
                "Prüfen nach Aus-Restart deaktiviert", Medium);
            Assert.False(menu.IsUpdateStartButtonVisible());
        }
        finally
        {
            await SetUpdateControlBackAsync(fixture, mainWindow);
            CloseFixtureApp();
        }
    }

    /// <summary>
    /// E-05: Der Startmodus wird zuerst über die UI gespeichert; danach werden je Neustart
    /// die nicht prüfbaren bzw. updatefreien Fälle abgedeckt: gleiche/ältere Version,
    /// ungültige lokale <c>version.json</c>, Release-HTTP-Fehler und Release-JSON-Fehler.
    /// Die fehlende lokale Datei wird in derselben Session über eine manuelle Prüfung
    /// nachgewiesen (eine fehlende <c>version.json</c> kann kein Start-Fixture sein, weil die
    /// Testkonfigurations-Validierung ihre Existenz beim App-Start verlangt). Nach dem
    /// Startabschluss bleibt jeweils: kein Angebot, kein Fortschrittsdialog, kein Asset/
    /// Launcher/Shutdown; bei ungültiger/fehlender lokaler Version zusätzlich kein
    /// Releaseabruf. Der Nicht-prüfbar-Hinweis wird kontrolliert und die Einstellungen
    /// bleiben bedienbar.
    /// </summary>
    private async Task Startup_NoUpdateOrUncheckableRemainsUsable()
    {
        using var fixture = new UpdateE2EFixture();
        await fixture.SetUpdateSettingsAsync(UpdateMode.Off, UpdatePathChannelResolver.StableVersionsPath);

        // Die Protokoll-Basis muss VOR dem Start gezogen werden: Der komplette Startlauf
        // (inkl. StartupUpdateCompleted) kann bereits während des Fenster-Wartens
        // protokolliert sein, bevor GetMainWindow zurückkehrt.
        var baseline = LogBase(fixture);
        var app = LaunchApp(ensureDatabaseDeleted: false, fixture.CreateStartEnvironment());
        Window? mainWindow = null;
        try
        {
            mainWindow = WaitOnRealMainwindow(app);
            var menu = new MenuView(mainWindow);
            await WaitOnEventSinceAsync(fixture, baseline, UpdateE2EEvents.StartupUpdateCompleted, Long);
            await SaveUpdateSettingsOverUiAsync(fixture, menu, UpdateModeStartLabel, UpdatePathProductionLabel);

            var versionsFile = Path.Combine(fixture.InstalledDirectory, "version.json");

            // Variante: gleiche/ältere Release-Version -> kein Update, keine Nebenwirkungen.
            fixture.UpdateScenario(s =>
                s.Answer.Single(a => a.Url == UpdateE2EFixture.ReleaseApiUrl).Content =
                    CreateReleaseListsJson(fixture, "1.2.0", "1.1.9"));
            baseline = LogBase(fixture);
            mainWindow = RestartAppPreservingDatabase();
            menu = new MenuView(mainWindow);
            await WaitOnEventSinceAsync(fixture, baseline, UpdateE2EEvents.StartupUpdateCompleted, Long);
            Assert.Contains(EntriesSince(fixture, baseline), e => e.Event == UpdateE2EEvents.UpdateCheckCompleted
                && DataText(e, "status") == "KeinUpdate");
            Assert.Null(menu.GetUpdateNotice());
            Assert.False(menu.IsUpdateStartButtonVisible());
            Assert.False(new UpdateProgressDialogView(mainWindow).IsVisible);
            Assert.Equal(0, CountEventSince(fixture, baseline, UpdateE2EEvents.HttpRequest,
                e => DataText(e, "art") == "asset"));
            AssertNoUpdateSideeffectSince(fixture, baseline);
            await CheckSettingsOperableAsync(fixture, menu);
            Assert.Equal(1, CountEventSince(fixture, baseline, UpdateE2EEvents.UpdateAttemptStarted));

            // Variante: ungültige lokale version.json -> NichtPruefbar ohne Releaseabruf.
            File.WriteAllText(versionsFile, """{"version":"keine-gueltige-version"}""");
            baseline = LogBase(fixture);
            mainWindow = RestartAppPreservingDatabase();
            menu = new MenuView(mainWindow);
            await WaitOnEventSinceAsync(fixture, baseline, UpdateE2EEvents.StartupUpdateCompleted, Long);
            Assert.Contains(EntriesSince(fixture, baseline), e => e.Event == UpdateE2EEvents.UpdateCheckCompleted
                && DataText(e, "status") == "NichtPruefbar");
            Assert.Equal(0, CountEventSince(fixture, baseline, UpdateE2EEvents.HttpRequest));
            menu.WaitForUpdateNotice("Lokale Version", Medium);
            Assert.False(menu.IsUpdateStartButtonVisible());
            Assert.False(new UpdateProgressDialogView(mainWindow).IsVisible);
            AssertNoUpdateSideeffectSince(fixture, baseline);
            await CheckSettingsOperableAsync(fixture, menu);

            // Variante: fehlende lokale version.json in derselben Session (siehe Hinweis im
            // Methodenkommentar - als Start-Fixture scheitert bereits die Konfigurationsvalidierung).
            File.Delete(versionsFile);
            baseline = LogBase(fixture);
            WaitOnUiState(() => menu.IsUpdateCheckButtonEnabled(), "Prüfen-Button aktiviert", Medium);
            menu.CheckClickUpdate();
            await WaitOnEventSinceAsync(fixture, baseline, UpdateE2EEvents.UpdateAttemptCompleted, Long,
                filter: e => DataText(e, "versuch") == "pruefen");
            Assert.Equal(0, CountEventSince(fixture, baseline, UpdateE2EEvents.HttpRequest));
            menu.WaitForUpdateNotice("Lokale Version", Medium);
            Assert.False(menu.IsUpdateStartButtonVisible());
            AssertNoUpdateSideeffectSince(fixture, baseline);
            File.WriteAllText(versionsFile, $$"""{"version":"{{UpdateE2EFixture.InstalledVersion}}","tagName":"v{{UpdateE2EFixture.InstalledVersion}}"}""");

            // Variante: Release-HTTP-Fehler -> NichtPruefbar, Hinweis, keine Nebenwirkungen.
            fixture.UpdateScenario(s =>
            {
                var response = s.Answer.Single(a => a.Url == UpdateE2EFixture.ReleaseApiUrl);
                response.Status = 500;
                response.Content = "e2e-fehler";
            });
            baseline = LogBase(fixture);
            mainWindow = RestartAppPreservingDatabase();
            menu = new MenuView(mainWindow);
            await WaitOnEventSinceAsync(fixture, baseline, UpdateE2EEvents.StartupUpdateCompleted, Long);
            Assert.Equal(1, CountEventSince(fixture, baseline, UpdateE2EEvents.HttpRequest,
                e => DataText(e, "url") == UpdateE2EFixture.ReleaseApiUrl));
            Assert.Contains(EntriesSince(fixture, baseline), e => e.Event == UpdateE2EEvents.UpdateCheckCompleted
                && DataText(e, "status") == "NichtPruefbar");
            menu.WaitForUpdateNotice(UpdateNoticeNotCheckablePart, Medium);
            Assert.False(menu.IsUpdateStartButtonVisible());
            Assert.False(new UpdateProgressDialogView(mainWindow).IsVisible);
            AssertNoUpdateSideeffectSince(fixture, baseline);
            await CheckSettingsOperableAsync(fixture, menu);

            // Variante: ungültiges Release-JSON -> NichtPruefbar, Hinweis, keine Nebenwirkungen.
            fixture.UpdateScenario(s =>
            {
                var response = s.Answer.Single(a => a.Url == UpdateE2EFixture.ReleaseApiUrl);
                response.Status = 200;
                response.Content = "definitiv kein gueltiges json {{{";
            });
            baseline = LogBase(fixture);
            mainWindow = RestartAppPreservingDatabase();
            menu = new MenuView(mainWindow);
            await WaitOnEventSinceAsync(fixture, baseline, UpdateE2EEvents.StartupUpdateCompleted, Long);
            Assert.Equal(1, CountEventSince(fixture, baseline, UpdateE2EEvents.HttpRequest,
                e => DataText(e, "url") == UpdateE2EFixture.ReleaseApiUrl));
            Assert.Contains(EntriesSince(fixture, baseline), e => e.Event == UpdateE2EEvents.UpdateCheckCompleted
                && DataText(e, "status") == "NichtPruefbar");
            menu.WaitForUpdateNotice(UpdateNoticeNotCheckablePart, Medium);
            Assert.False(menu.IsUpdateStartButtonVisible());
            AssertNoUpdateSideeffectSince(fixture, baseline);
            await CheckSettingsOperableAsync(fixture, menu);
        }
        finally
        {
            await SetUpdateControlBackAsync(fixture, mainWindow);
            CloseFixtureApp();
        }
    }

    /// <summary>
    /// E-06: Startmodus wird über die UI gespeichert; für jeden automatischen Versuch wird
    /// neu gestartet. (a) Riskante CLI-Fixture: echter Ja/Nein-Dialog mit Nein - keine
    /// Vorbereitung/Asset/Start/Shutdown. (b) Ja bestätigt, echter blockierter
    /// Downloadfortschritt, Cancel - Abbruchzustand ohne Start/Shutdown. (c) Asset-HTTP-
    /// Fehler (automatisch), defektes ZIP und manipuliertes Paket mit Hash-Mismatch
    /// (manuell): sichtbarer Vorbereitungsfehler ohne Start/Shutdown. (c4) Primärer
    /// Asset-Abruf liefert 5xx: einmaliger Fallback auf die
    /// GitHub-API-Asset-URL - die Installation läuft vollständig durch. (d) Gültige
    /// Vorbereitung mit Launcherfehler: genau ein Versuch, kein
    /// erfolgreicher Start, kein Shutdown, sichtbarer Fehler. Anschließend alle T-09-
    /// Lesefehler-Grenzen (Initial, BeforePreparation, BeforeUpdaterStart) je einmal
    /// automatisch (Start) und manuell (Prüfen/Starten) mit Gate-Freigabe, ohne
    /// unerlaubte Folgeschritte und ohne automatische Wiederholung; nach Deaktivierung läuft
    /// eine erfolgreiche manuelle Folgeprüfung ohne automatische Installation.
    /// </summary>
    private async Task Startup_SafetyCancelAndErrorsRemainUsable()
    {
        using var fixture = new UpdateE2EFixture();
        await fixture.SetUpdateSettingsAsync(UpdateMode.Off, UpdatePathChannelResolver.StableVersionsPath);

        // Die Protokoll-Basis muss VOR dem Start gezogen werden: Der komplette Startlauf
        // (inkl. StartupUpdateCompleted) kann bereits während des Fenster-Wartens
        // protokolliert sein, bevor GetMainWindow zurückkehrt.
        var baseline = LogBase(fixture);
        var app = LaunchApp(ensureDatabaseDeleted: false, fixture.CreateStartEnvironment());
        Window? mainWindow = null;
        try
        {
            mainWindow = WaitOnRealMainwindow(app);
            var menu = new MenuView(mainWindow);
            await WaitOnEventSinceAsync(fixture, baseline, UpdateE2EEvents.StartupUpdateCompleted, Long);
            await SaveUpdateSettingsOverUiAsync(fixture, menu, UpdateModeStartLabel, UpdatePathProductionLabel);

            // (a) Riskante CLI-Fixture: der echte Sicherheitsdialog wird mit Nein beantwortet.
            (mainWindow, menu) = await SafetyDialogRejectedPhaseAsync(fixture);

            // (b) Ja bestätigt: echter Downloadfortschritt, Cancel im blockierten Stream.
            (mainWindow, menu) = await DownloadCancelPhaseAsync(fixture);

            // (c1) Asset-HTTP-Fehler beim automatischen Versuch: sichtbarer Vorbereitungsfehler.
            (mainWindow, menu) = await AssetHttpErrorPhaseAsync(fixture);

            // (c2) Defektes ZIP beim manuellen Startversuch in derselben Session.
            await DefectZipPhaseAsync(fixture, menu);

            // (c3) Gültiges ZIP mit fremdem Manifest-Hash beim manuellen Startversuch:
            // die Verifikation bricht vor dem Entpacken kontrolliert ab.
            await TamperedPackagePhaseAsync(fixture, menu);

            // (d) Gültige Vorbereitung, Launcherfehler: ein Versuch, kein erfolgreicher Start,
            // kein Shutdown.
            (mainWindow, menu) = await LauncherErrorPhaseAsync(fixture);
            fixture.UpdateScenario(s => s.ProcessStart = new UpdateE2EProcessStart { Result = "Erfolg" });

            // (c4) Primärer Asset-Abruf 5xx, einmaliger Fallback auf die API-Asset-URL:
            // die Installation läuft vollständig durch.
            (mainWindow, menu) = await AssetFallbackSuccessPhaseAsync(fixture);

            // T-09 automatisch: Lesefehler an allen drei Grenzen (Initial, BeforePreparation,
            // BeforeUpdaterStart) stoppen den Startversuch sicher.
            (mainWindow, menu) = await T09StartupPhaseAsync(fixture, "Initial", ordinal: 1,
                expectReleaseQuery: false, expectPreparation: false, expectDialogError: false);
            (mainWindow, menu) = await T09StartupPhaseAsync(fixture, "BeforePreparation", ordinal: 2,
                expectReleaseQuery: true, expectPreparation: false, expectDialogError: true);
            (mainWindow, menu) = await T09StartupPhaseAsync(fixture, "BeforeUpdaterStart", ordinal: 3,
                expectReleaseQuery: true, expectPreparation: true, expectDialogError: true);

            // T-09 manuell: Grenze Initial beim Prüfversuch (kein Releaseabruf), dann die
            // Installationsgrenzen BeforePreparation/BeforeUpdaterStart beim Startversuch.
            menu = await T09ManualInitialPhaseAsync(fixture, menu);
            await T09ManualStartPhaseAsync(fixture, menu, "BeforePreparation", ordinal: 2,
                expectPreparation: false);
            await T09ManualStartPhaseAsync(fixture, menu, "BeforeUpdaterStart", ordinal: 3,
                expectPreparation: true);
            Assert.True(fixture.CountEvent(UpdateE2EEvents.UpdateSettingsReadFailureDisabled) >= 1);

            // Erfolgreiche manuelle Folgeprüfung nach Deaktivierung - ohne automatische
            // Installation.
            menu = await ResumeAfterReadfailureAsync(fixture, menu);
        }
        finally
        {
            await SetUpdateControlBackAsync(fixture, mainWindow);
            CloseFixtureApp();
        }
    }

    /// <summary>
    /// E-06 (a): Riskante CLI-Fixture aktivieren, neu starten, den echten Sicherheitsdialog
    /// mit Nein beantworten - keine Vorbereitung, kein Asset-Abruf, keine Nebenwirkungen.
    /// </summary>
    private async Task<(Window MainWindow, MenuView Menu)> SafetyDialogRejectedPhaseAsync(
        UpdateE2EFixture fixture)
    {
        fixture.UpdateScenario(s => s.CliSafe = new UpdateE2ECliSafety
        {
            Active = true,
            RiskyWorkItems = ["E2E-Risikoaufgabe"]
        });
        var baseline = LogBase(fixture);
        var mainWindow = RestartAppPreservingDatabase();
        var menu = new MenuView(mainWindow);
        var progress = new UpdateProgressDialogView(mainWindow);
        var safe = new UpdateSafetyDialogView(mainWindow);
        await WaitOnEventSinceAsync(fixture, baseline, UpdateE2EEvents.CliSafetyChecked, Long,
            filter: e => DataNumber(e, "riskyTaskCount") >= 1);
        safe.ForceShow();
        safe.Cancel();
        await WaitOnEventSinceAsync(fixture, baseline, UpdateE2EEvents.StartupUpdateCompleted, Long);
        Assert.False(progress.IsVisible);
        Assert.Equal(0, CountEventSince(fixture, baseline, UpdateE2EEvents.HttpRequest,
            e => DataText(e, "art") == "asset"));
        AssertNoUpdateSideeffectSince(fixture, baseline);
        Assert.Equal(1, CountEventSince(fixture, baseline, UpdateE2EEvents.UpdateAttemptStarted));
        await CheckSettingsOperableAsync(fixture, menu);
        return (mainWindow, menu);
    }

    /// <summary>
    /// E-06 (b): Sicherheitsdialog mit Ja bestätigen, echten blockierten Download abbrechen -
    /// Abbruchzustand ohne Vorbereitung, Start oder Shutdown.
    /// </summary>
    private async Task<(Window MainWindow, MenuView Menu)> DownloadCancelPhaseAsync(
        UpdateE2EFixture fixture)
    {
        fixture.UpdateScenario(s =>
        {
            var package = s.Answer.Single(a => a.Url == fixture.StableDownloadUrl);
            package.StreamGate = UpdateDownloadGate;
            package.StreamGateBytes = 128;
        });
        var baseline = LogBase(fixture);
        var mainWindow = RestartAppPreservingDatabase();
        var menu = new MenuView(mainWindow);
        var progress = new UpdateProgressDialogView(mainWindow);
        var safe = new UpdateSafetyDialogView(mainWindow);
        await WaitOnEventSinceAsync(fixture, baseline, UpdateE2EEvents.CliSafetyChecked, Long,
            filter: e => DataNumber(e, "riskyTaskCount") >= 1);
        safe.ForceShow();
        safe.Confirm();
        await WaitOnEventSinceAsync(fixture, baseline, UpdateE2EEvents.HttpRequestBlocked, Long,
            filter: e => DataText(e, "gate") == UpdateDownloadGate);
        Assert.True(progress.IsVisible);
        WaitOnUiState(() => progress.GetPhase() == "Download", "Download-Phase", Medium);
        progress.Cancel();
        var cancelMessage = progress.WaitForErrorState(Medium);
        Assert.Contains("abgebr", cancelMessage);
        await WaitOnEventSinceAsync(fixture, baseline, UpdateE2EEvents.StartupUpdateCompleted, Long);
        Assert.Equal(1, CountEventSince(fixture, baseline, UpdateE2EEvents.PreparationFailed));
        Assert.Equal(0, CountEventSince(fixture, baseline, UpdateE2EEvents.PreparationCompleted));
        Assert.Equal(0, CountEventSince(fixture, baseline, UpdateE2EEvents.UpdateStartAttempt));
        Assert.Equal(0, CountEventSince(fixture, baseline, UpdateE2EEvents.UpdateProcessStartRecorded));
        Assert.Equal(0, CountEventSince(fixture, baseline, UpdateE2EEvents.ShutdownRequested));
        progress.Close();
        fixture.SetGateBack(UpdateDownloadGate);
        fixture.UpdateScenario(s =>
        {
            s.CliSafe = new UpdateE2ECliSafety { Active = false };
            s.Answer.Single(a => a.Url == fixture.StableDownloadUrl).StreamGate = null;
        });
        await CheckSettingsOperableAsync(fixture, menu);
        Assert.Equal(1, CountEventSince(fixture, baseline, UpdateE2EEvents.UpdateAttemptStarted));
        return (mainWindow, menu);
    }

    /// <summary>
    /// E-06 (c1): Asset-HTTP-Fehler beim automatischen Versuch - primäre Download-URL und
    /// API-Asset-Fallback-URL liefern beide 500, sichtbarer Vorbereitungsfehler ohne
    /// Start/Shutdown.
    /// </summary>
    private async Task<(Window MainWindow, MenuView Menu)> AssetHttpErrorPhaseAsync(
        UpdateE2EFixture fixture)
    {
        fixture.UpdateScenario(s =>
        {
            s.Answer.Single(a => a.Url == fixture.StableDownloadUrl).Status = 500;
            s.Answer.Single(a => a.Url == UpdateE2EFixture.StableAssetApiUrl).Status = 500;
        });
        var baseline = LogBase(fixture);
        var mainWindow = RestartAppPreservingDatabase();
        var menu = new MenuView(mainWindow);
        var progress = new UpdateProgressDialogView(mainWindow);
        await WaitOnEventSinceAsync(fixture, baseline, UpdateE2EEvents.PreparationFailed, Long);
        Assert.Contains("konnte nicht vorbereitet", progress.WaitForErrorState(Medium));
        await WaitOnEventSinceAsync(fixture, baseline, UpdateE2EEvents.StartupUpdateCompleted, Long);
        Assert.Equal(1, CountEventSince(fixture, baseline, UpdateE2EEvents.HttpRequest,
            e => DataText(e, "url") == fixture.StableDownloadUrl));
        Assert.Equal(1, CountEventSince(fixture, baseline, UpdateE2EEvents.HttpRequest,
            e => DataText(e, "url") == UpdateE2EFixture.StableAssetApiUrl));
        Assert.Equal(0, CountEventSince(fixture, baseline, UpdateE2EEvents.UpdateStartAttempt));
        Assert.Equal(0, CountEventSince(fixture, baseline, UpdateE2EEvents.UpdateProcessStartRecorded));
        Assert.Equal(0, CountEventSince(fixture, baseline, UpdateE2EEvents.ShutdownRequested));
        progress.Close();
        await CheckSettingsOperableAsync(fixture, menu);
        Assert.Equal(1, CountEventSince(fixture, baseline, UpdateE2EEvents.UpdateAttemptStarted));
        return (mainWindow, menu);
    }

    /// <summary>
    /// E-06 (c2): Defektes ZIP beim manuellen Startversuch in derselben Session -
    /// sichtbarer Vorbereitungsfehler ohne Start/Shutdown. Das Manifest trägt den Hash der
    /// servierten defekten Datei, damit weiterhin der ZIP-Parse-Fehler (nicht der
    /// Hash-Mismatch) den Abbruch auslöst.
    /// </summary>
    private async Task DefectZipPhaseAsync(UpdateE2EFixture fixture, MenuView menu)
    {
        var defectPackage = Path.Combine(fixture.PackageDirectory, "defekt.zip");
        File.WriteAllText(defectPackage, "E2E: kein gueltiges ZIP");
        fixture.UpdateScenario(s =>
        {
            var response = s.Answer.Single(a => a.Url == fixture.StableDownloadUrl);
            response.Status = 200;
            response.BodyFile = Path.GetRelativePath(fixture.TestRoot, defectPackage);
            s.Answer.Single(a => a.Url == fixture.StableManifestDownloadUrl).Content =
                UpdateE2EFixture.CreateManifestJson(UpdateE2EFixture.StableVersion, fixture.StableDownloadUrl, defectPackage);
        });
        var baseline = LogBase(fixture);
        menu.StartClickUpdate();
        await WaitOnEventSinceAsync(fixture, baseline, UpdateE2EEvents.UpdateAttemptCompleted, Long,
            filter: e => DataText(e, "versuch") == "starten");
        var progress = new UpdateProgressDialogView(menu.Window);
        Assert.Equal(1, CountEventSince(fixture, baseline, UpdateE2EEvents.PreparationFailed));
        Assert.Contains("konnte nicht vorbereitet", progress.WaitForErrorState(Medium));
        Assert.Equal(0, CountEventSince(fixture, baseline, UpdateE2EEvents.UpdateStartAttempt));
        Assert.Equal(0, CountEventSince(fixture, baseline, UpdateE2EEvents.ShutdownRequested));
        progress.Close();
        await CheckSettingsOperableAsync(fixture, menu);
    }

    /// <summary>
    /// E-06 (c3): Manipuliertes Paket beim manuellen Startversuch in derselben Session - eine
    /// gültige ZIP mit anderem Inhalt als vom Manifest verzeichnet scheitert an der
    /// SHA-256-Verifikation vor dem Entpacken: sichtbarer Vorbereitungsfehler ohne
    /// Entpacken, Start oder Shutdown.
    /// </summary>
    private async Task TamperedPackagePhaseAsync(UpdateE2EFixture fixture, MenuView menu)
    {
        var tamperedPackage = Path.Combine(fixture.PackageDirectory, "manipuliert.zip");
        UpdateE2EFixture.CreatePackageZip(tamperedPackage, UpdateE2EFixture.StableVersion, "manipulierter-marker");
        fixture.UpdateScenario(s =>
        {
            var response = s.Answer.Single(a => a.Url == fixture.StableDownloadUrl);
            response.Status = 200;
            response.BodyFile = Path.GetRelativePath(fixture.TestRoot, tamperedPackage);
            s.Answer.Single(a => a.Url == fixture.StableManifestDownloadUrl).Content =
                UpdateE2EFixture.CreateManifestJson(UpdateE2EFixture.StableVersion, fixture.StableDownloadUrl, fixture.StableZipPath);
        });
        var baseline = LogBase(fixture);
        menu.StartClickUpdate();
        await WaitOnEventSinceAsync(fixture, baseline, UpdateE2EEvents.UpdateAttemptCompleted, Long,
            filter: e => DataText(e, "versuch") == "starten");
        var progress = new UpdateProgressDialogView(menu.Window);
        Assert.Equal(1, CountEventSince(fixture, baseline, UpdateE2EEvents.HttpRequest,
            e => DataText(e, "art") == "manifest" && DataText(e, "url") == fixture.StableManifestDownloadUrl));
        Assert.Equal(1, CountEventSince(fixture, baseline, UpdateE2EEvents.PreparationFailed));
        Assert.Contains("konnte nicht vorbereitet", progress.WaitForErrorState(Medium));
        Assert.Equal(0, CountEventSince(fixture, baseline, UpdateE2EEvents.PreparationPhase,
            e => DataText(e, "phase") == "Entpacken"));
        Assert.Equal(0, CountEventSince(fixture, baseline, UpdateE2EEvents.UpdateStartAttempt));
        Assert.Equal(0, CountEventSince(fixture, baseline, UpdateE2EEvents.UpdateProcessStartRecorded));
        Assert.Equal(0, CountEventSince(fixture, baseline, UpdateE2EEvents.ShutdownRequested));
        progress.Close();
        await CheckSettingsOperableAsync(fixture, menu);
    }

    /// <summary>
    /// E-06 (d): Gültige Vorbereitung mit Launcherfehler - genau ein Versuch, kein
    /// erfolgreicher Start, kein Shutdown, sichtbarer Fehler.
    /// </summary>
    private async Task<(Window MainWindow, MenuView Menu)> LauncherErrorPhaseAsync(
        UpdateE2EFixture fixture)
    {
        fixture.UpdateScenario(s =>
        {
            var response = s.Answer.Single(a => a.Url == fixture.StableDownloadUrl);
            response.BodyFile = Path.GetRelativePath(fixture.TestRoot, fixture.StableZipPath);
            s.ProcessStart = new UpdateE2EProcessStart { Result = "Fehler" };
        });
        var baseline = LogBase(fixture);
        var mainWindow = RestartAppPreservingDatabase();
        var menu = new MenuView(mainWindow);
        var progress = new UpdateProgressDialogView(mainWindow);
        await WaitOnEventSinceAsync(fixture, baseline, UpdateE2EEvents.UpdateProcessStartRecorded, Long);
        await WaitOnEventSinceAsync(fixture, baseline, UpdateE2EEvents.StartupUpdateCompleted, Long);
        Assert.Equal(1, CountEventSince(fixture, baseline, UpdateE2EEvents.UpdateStartAttempt));
        var launcherError = Assert.Single(EntriesSince(fixture, baseline),
            e => e.Event == UpdateE2EEvents.UpdateProcessStartRecorded);
        Assert.Equal("Fehler", DataText(launcherError, "result"));
        Assert.Equal(1, CountEventSince(fixture, baseline, UpdateE2EEvents.UpdateStartFailed));
        Assert.Equal(0, CountEventSince(fixture, baseline, UpdateE2EEvents.UpdateStartSucceeded));
        Assert.Equal(0, CountEventSince(fixture, baseline, UpdateE2EEvents.ShutdownRequested));
        Assert.Contains("konnte nicht vorbereitet", progress.WaitForErrorState(Medium));
        progress.Close();
        await CheckSettingsOperableAsync(fixture, menu);
        Assert.Equal(1, CountEventSince(fixture, baseline, UpdateE2EEvents.UpdateAttemptStarted));
        return (mainWindow, menu);
    }

    /// <summary>
    /// E-06 (c4): Primärer Asset-Abruf liefert 5xx - der einmalige Fallback auf die
    /// GitHub-API-Asset-URL rettet den Download; die Installation läuft vollständig durch.
    /// Genau ein Request je Download-Route; der vollständige Installationsnachweis läuft
    /// über <c>AssertiereInstallationsNachweis</c> mit beiden erwarteten Asset-URLs.
    /// </summary>
    private async Task<(Window MainWindow, MenuView Menu)> AssetFallbackSuccessPhaseAsync(
        UpdateE2EFixture fixture)
    {
        fixture.UpdateScenario(s =>
        {
            s.Answer.Single(a => a.Url == fixture.StableDownloadUrl).Status = 500;
            s.Answer.Single(a => a.Url == UpdateE2EFixture.StableAssetApiUrl).Status = 200;
        });
        var baseline = LogBase(fixture);
        var mainWindow = RestartAppPreservingDatabase();
        var menu = new MenuView(mainWindow);
        var progress = new UpdateProgressDialogView(mainWindow);
        await WaitOnEventSinceAsync(fixture, baseline, UpdateE2EEvents.StartupUpdateCompleted, Long);

        AssertInstallationProof(fixture, baseline,
            UpdateE2EFixture.StableVersion, fixture.StableDownloadUrl, "stable-marker",
            [UpdateE2EFixture.StableAssetApiUrl]);
        progress.Close();
        fixture.UpdateScenario(s =>
            s.Answer.Single(a => a.Url == fixture.StableDownloadUrl).Status = 200);
        return (mainWindow, menu);
    }

    /// <summary>
    /// E-06 T-09 automatisch: Einen Startversuch mit aktivem Lesefehler an der Grenze
    /// <paramref name="limit"/> durchlaufen und die grenzabhängigen Nachweise prüfen.
    /// </summary>
    /// <param name="fixture">Die Update-Fixture.</param>
    /// <param name="limit">Die Lesegrenze (<c>Initial</c>, <c>BeforePreparation</c>, <c>BeforeUpdaterStart</c>).</param>
    /// <param name="ordinal">Die erwartete Ordnungszahl des blockierten Reads.</param>
    /// <param name="expectReleaseQuery">Ob der Releaseabruf vor dem Lesefehler gelaufen ist.</param>
    /// <param name="expectPreparation">Ob die Vorbereitung vor dem Lesefehler abgeschlossen war.</param>
    /// <param name="expectDialogError">Ob der Fehler im Fortschrittsdialog statt im Hinweistext erscheint.</param>
    /// <returns>Das neue Hauptfenster und das Navigationsmenü.</returns>
    private async Task<(Window MainWindow, MenuView Menu)> T09StartupPhaseAsync(
        UpdateE2EFixture fixture, string limit, int ordinal,
        bool expectReleaseQuery, bool expectPreparation, bool expectDialogError)
    {
        var (mainWindow, baseline) = await StartReadfailureStartupVariantAsync(fixture, limit, ordinal);
        var menu = new MenuView(mainWindow);

        if (expectPreparation)
        {
            var lastRead = EntriesSince(fixture, baseline)
                .Last(e => e.Event == UpdateE2EEvents.UpdateSettingsReadReached
                    && DataText(e, "versuch") == "startup" && DataNumber(e, "ordinal") == ordinal);
            Assert.True(EntriesSince(fixture, baseline)
                    .Single(e => e.Event == UpdateE2EEvents.PreparationCompleted).Seq < lastRead.Seq,
                "Der Read an der Grenze muss erst nach PreparationCompleted erfolgen.");
        }

        await WaitOnEventSinceAsync(fixture, baseline, UpdateE2EEvents.StartupUpdateCompleted, Long);

        if (expectDialogError)
        {
            var progress = new UpdateProgressDialogView(mainWindow);
            Assert.Contains(UpdateNoticeReadfailure, progress.WaitForErrorState(Medium));
            progress.Close();
        }
        else
        {
            menu.WaitForUpdateNotice(UpdateNoticeReadfailure, Medium);
        }

        if (!expectReleaseQuery)
        {
            Assert.Equal(0, CountEventSince(fixture, baseline, UpdateE2EEvents.HttpRequest));
            Assert.Equal(0, CountEventSince(fixture, baseline, UpdateE2EEvents.UpdateCheckStarted));
        }
        else
        {
            Assert.Equal(1, CountEventSince(fixture, baseline, UpdateE2EEvents.HttpRequest,
                e => DataText(e, "url") == UpdateE2EFixture.ReleaseApiUrl));
            // BeforeUpdaterStart: die Vorbereitung inkl. Asset-Abruf läuft vollständig,
            // erst der letzte Read vor dem Updater-Start schlägt fehl.
            Assert.Equal(expectPreparation ? 1 : 0,
                CountEventSince(fixture, baseline, UpdateE2EEvents.HttpRequest,
                    e => DataText(e, "art") == "asset"));
        }

        AssertNoUpdateSideeffectSince(fixture, baseline, allowPreparation: expectPreparation);
        await ResumeAfterReadfailureAsync(fixture, menu);
        return (mainWindow, menu);
    }

    /// <summary>
    /// E-06 T-09 manuell, Grenze Initial beim Prüfversuch: Der markierte Read wird blockiert,
    /// freigegeben und schlägt fehl - kein Releaseabruf, Hinweis sichtbar, danach
    /// Wiederherstellung über die Einstellungen-UI.
    /// </summary>
    private async Task<MenuView> T09ManualInitialPhaseAsync(UpdateE2EFixture fixture, MenuView menu)
    {
        fixture.ActivateReadfailure("Initial", attempt: "pruefen");
        var baseline = LogBase(fixture);
        menu.CheckClickUpdate();
        await WaitOnEventSinceAsync(fixture, baseline, UpdateE2EEvents.UpdateSettingsReadReached, Long,
            filter: e => DataText(e, "versuch") == "pruefen" && DataNumber(e, "ordinal") == 1);
        fixture.OpenGate(UpdateE2ETestContext.ReadFailureGateName);
        await WaitOnEventSinceAsync(fixture, baseline, UpdateE2EEvents.UpdateSettingsReadFailed, Long,
            filter: e => DataText(e, "versuch") == "pruefen" && DataText(e, "grenze") == "Initial");
        await WaitOnEventSinceAsync(fixture, baseline, UpdateE2EEvents.UpdateAttemptCompleted, Long,
            filter: e => DataText(e, "versuch") == "pruefen");
        menu.WaitForUpdateNotice(UpdateNoticeReadfailure, Medium);
        Assert.Equal(0, CountEventSince(fixture, baseline, UpdateE2EEvents.HttpRequest));
        AssertNoUpdateSideeffectSince(fixture, baseline);
        fixture.SetGateBack(UpdateE2ETestContext.ReadFailureGateName);
        fixture.DeactivateReadfailure();
        return await ResumeAfterReadfailureAsync(fixture, menu);
    }

    /// <summary>
    /// E-06 T-09 manuell beim Startversuch: Nach erfolgreicher Prüfung wird der Startversuch
    /// an der Lesegrenze <paramref name="limit"/> blockiert und schlägt fehl - der
    /// Fortschrittsdialog zeigt den Lesefehler, ohne unerlaubte Folgeschritte.
    /// </summary>
    /// <param name="fixture">Die Update-Fixture.</param>
    /// <param name="menu">Das Navigationsmenü.</param>
    /// <param name="limit">Die Lesegrenze (<c>BeforePreparation</c>, <c>BeforeUpdaterStart</c>).</param>
    /// <param name="ordinal">Die erwartete Ordnungszahl des blockierten Reads.</param>
    /// <param name="expectPreparation">Ob die Vorbereitung vor dem Lesefehler abgeschlossen war.</param>
    private async Task T09ManualStartPhaseAsync(
        UpdateE2EFixture fixture, MenuView menu, string limit, int ordinal, bool expectPreparation)
    {
        fixture.ActivateReadfailure(limit, attempt: "starten");
        var baseline = LogBase(fixture);
        menu.CheckClickUpdate();
        await WaitOnEventSinceAsync(fixture, baseline, UpdateE2EEvents.UpdateAttemptCompleted, Long,
            filter: e => DataText(e, "versuch") == "pruefen");
        menu.WaitForOfferedUpdateVersion(Medium);
        menu.StartClickUpdate();
        var lastRead = (await WaitOnEventSinceAsync(fixture, baseline,
            UpdateE2EEvents.UpdateSettingsReadReached, Long,
            filter: e => DataText(e, "versuch") == "starten" && DataNumber(e, "ordinal") == ordinal))[0];
        if (expectPreparation)
        {
            Assert.True(EntriesSince(fixture, baseline)
                    .Single(e => e.Event == UpdateE2EEvents.PreparationCompleted).Seq < lastRead.Seq,
                "Der BeforeUpdaterStart-Read muss erst nach PreparationCompleted erfolgen.");
        }

        fixture.OpenGate(UpdateE2ETestContext.ReadFailureGateName);
        await WaitOnEventSinceAsync(fixture, baseline, UpdateE2EEvents.UpdateSettingsReadFailed, Long,
            filter: e => DataText(e, "versuch") == "starten" && DataText(e, "grenze") == limit);
        await WaitOnEventSinceAsync(fixture, baseline, UpdateE2EEvents.UpdateAttemptCompleted, Long,
            filter: e => DataText(e, "versuch") == "starten");
        var progress = new UpdateProgressDialogView(menu.Window);
        Assert.Contains(UpdateNoticeReadfailure, progress.WaitForErrorState(Medium));
        progress.Close();
        if (!expectPreparation)
            Assert.Equal(0, CountEventSince(fixture, baseline, UpdateE2EEvents.HttpRequest,
                e => DataText(e, "art") == "asset"));

        Assert.Equal(expectPreparation ? 1 : 0,
            CountEventSince(fixture, baseline, UpdateE2EEvents.PreparationCompleted));
        AssertNoUpdateSideeffectSince(fixture, baseline, allowPreparation: expectPreparation);
        fixture.SetGateBack(UpdateE2ETestContext.ReadFailureGateName);
        fixture.DeactivateReadfailure();
    }

    /// <summary>
    /// E-07: Der Startmodus wird über die UI gespeichert und neu gestartet; die
    /// Releaseantwort kommt sofort, der ZIP-Stream blockiert. Währenddessen sind Prüfen und
    /// Installieren gesperrt bzw. deaktiviert; eine versuchte UI-Aktivierung erzeugt keinen
    /// zweiten Ablauf und keinen Shutdown vor der Freigabe. Nach der Freigabe existiert
    /// genau eine Vorbereitung, eine Launcher-Übergabe und ein Shutdown-Eintrag. Im offen
    /// gehaltenen Fenster bleiben die Zähler nach Navigation und erneutem Rendern
    /// unverändert. Der wiederholte direkte Bereitschaftsaufruf ist ergänzend in
    /// <c>MainWindowViewModelTests_UpdateStartup</c> abgedeckt.
    /// </summary>
    private async Task Startup_IsOnceAndCommandsStayBlocked()
    {
        using var fixture = new UpdateE2EFixture();
        await fixture.SetUpdateSettingsAsync(UpdateMode.Off, UpdatePathChannelResolver.StableVersionsPath);

        // Die Protokoll-Basis muss VOR dem Start gezogen werden: Der komplette Startlauf
        // (inkl. StartupUpdateCompleted) kann bereits während des Fenster-Wartens
        // protokolliert sein, bevor GetMainWindow zurückkehrt.
        var baseline = LogBase(fixture);
        var app = LaunchApp(ensureDatabaseDeleted: false, fixture.CreateStartEnvironment());
        Window? mainWindow = null;
        try
        {
            mainWindow = WaitOnRealMainwindow(app);
            var menu = new MenuView(mainWindow);
            await WaitOnEventSinceAsync(fixture, baseline, UpdateE2EEvents.StartupUpdateCompleted, Long);
            await SaveUpdateSettingsOverUiAsync(fixture, menu, UpdateModeStartLabel, UpdatePathProductionLabel);

            fixture.UpdateScenario(s =>
            {
                var package = s.Answer.Single(a => a.Url == fixture.StableDownloadUrl);
                package.StreamGate = UpdateDownloadGate;
                package.StreamGateBytes = 128;
            });
            baseline = LogBase(fixture);
            mainWindow = RestartAppPreservingDatabase();
            menu = new MenuView(mainWindow);
            await WaitOnEventSinceAsync(fixture, baseline, UpdateE2EEvents.HttpRequestBlocked, Long,
                filter: e => DataText(e, "gate") == UpdateDownloadGate);

            var progress = new UpdateProgressDialogView(mainWindow);
            Assert.True(progress.IsVisible);
            WaitOnUiState(() => progress.GetPhase() == "Download", "Download-Phase", Medium);

            // Gesperrte Commands: Prüfen deaktiviert, Installieren sichtbar aber deaktiviert.
            WaitOnUiState(() => !menu.IsUpdateCheckButtonEnabled(),
                "Prüfen während blockierter Startautomatik gesperrt", Medium);
            Assert.True(menu.IsUpdateStartButtonVisible());
            Assert.False(menu.IsUpdateStartButtonEnabled());

            // Versuchte UI-Aktivierung erzeugt keinen zweiten Ablauf und keinen Shutdown.
            var checkButton = mainWindow.FindFirstDescendant(
                cf => cf.ByName("Programmupdate prüfen").And(cf.ByControlType(ControlType.Button)));
            checkButton?.AsButton().ClickInForeground();
            var startButton = mainWindow.FindFirstDescendant(
                cf => cf.ByName("Programmupdate starten").And(cf.ByControlType(ControlType.Button)));
            startButton?.AsButton().ClickInForeground();
            Assert.Equal(1, CountEventSince(fixture, baseline, UpdateE2EEvents.UpdateAttemptStarted));
            Assert.Equal(0, CountEventSince(fixture, baseline, UpdateE2EEvents.ShutdownRequested));

            // Freigabe: genau eine Vorbereitung, eine Übergabe, ein Shutdown.
            fixture.OpenGate(UpdateDownloadGate);
            await WaitOnEventSinceAsync(fixture, baseline, UpdateE2EEvents.StartupUpdateCompleted, Long);
            var entries = EntriesSince(fixture, baseline);
            Assert.Single(entries, e => e.Event == UpdateE2EEvents.UpdateAttemptStarted);
            Assert.Single(entries, e => e.Event == UpdateE2EEvents.UpdateAttemptCompleted);
            AssertInstallationProof(fixture, baseline,
                UpdateE2EFixture.StableVersion, fixture.StableDownloadUrl, "stable-marker");
            progress.Close();
            fixture.SetGateBack(UpdateDownloadGate);

            // Offen gehaltenes Fenster: Navigation und erneutes Rendern ändern die Zähler nicht.
            var afterBase = LogBase(fixture);
            var settings = await OpenSettingsLoadedAsync(fixture, menu);
            Assert.Equal(UpdateModeStartLabel, settings.GetUpdateMode());
            menu.NavigateToDashboard();
            menu.NavigateToProjects();
            menu.NavigateToDashboard();
            Assert.Equal(0, CountEventSince(fixture, afterBase, UpdateE2EEvents.UpdateAttemptStarted));
            Assert.Equal(0, CountEventSince(fixture, afterBase, UpdateE2EEvents.HttpRequest));
            Assert.Equal(0, CountEventSince(fixture, afterBase, UpdateE2EEvents.PreparationCompleted));
            Assert.Equal(0, CountEventSince(fixture, afterBase, UpdateE2EEvents.ShutdownRequested));
        }
        finally
        {
            await SetUpdateControlBackAsync(fixture, mainWindow);
            CloseFixtureApp();
        }
    }

    /// <summary>
    /// Startet einen T-09-Startup-Lesefehler-Fall: Fehler aktivieren, mit gleicher DB neu
    /// starten, auf den blockierten markierten Read warten und das Gate freigeben.
    /// </summary>
    /// <param name="fixture">Die Update-Fixture.</param>
    /// <param name="limit">Die Lesegrenze (<c>Initial</c>, <c>BeforePreparation</c>).</param>
    /// <param name="ordinal">Die erwartete Ordnungszahl des blockierten Reads.</param>
    /// <returns>Das neue Hauptfenster und die Protokoll-Basis der Phase.</returns>
    private async Task<(Window MainWindow, int Base)> StartReadfailureStartupVariantAsync(
        UpdateE2EFixture fixture, string limit, int ordinal)
    {
        fixture.ActivateReadfailure(limit, attempt: "startup");
        var baseline = LogBase(fixture);
        var mainWindow = RestartAppPreservingDatabase();
        await WaitOnEventSinceAsync(fixture, baseline, UpdateE2EEvents.UpdateSettingsReadReached, Long,
            filter: e => DataText(e, "versuch") == "startup" && DataNumber(e, "ordinal") == ordinal);
        fixture.OpenGate(UpdateE2ETestContext.ReadFailureGateName);
        await WaitOnEventSinceAsync(fixture, baseline, UpdateE2EEvents.UpdateSettingsReadFailed, Long,
            filter: e => DataText(e, "versuch") == "startup" && DataText(e, "grenze") == limit);
        return (mainWindow, baseline);
    }

    /// <summary>
    /// Fährt nach einem T-09-Lesefehler fort: Gate zurücksetzen, Fehler deaktivieren, über
    /// die echte Settings-UI speichern (stellt den Einstellungs-Snapshot des Hauptfensters
    /// wieder her) und eine erfolgreiche manuelle Prüfung ohne automatische Installation
    /// nachweisen. Kein automatischer Wiederholungsversuch.
    /// </summary>
    /// <param name="fixture">Die Update-Fixture.</param>
    /// <param name="menu">Das Navigationsmenü.</param>
    /// <returns>Das Navigationsmenü.</returns>
    private async Task<MenuView> ResumeAfterReadfailureAsync(UpdateE2EFixture fixture, MenuView menu)
    {
        fixture.SetGateBack(UpdateE2ETestContext.ReadFailureGateName);
        fixture.DeactivateReadfailure();
        Assert.True(fixture.CountEvent(UpdateE2EEvents.UpdateSettingsReadFailureDisabled) >= 1);

        var settings = await OpenSettingsLoadedAsync(fixture, menu);
        Assert.Equal(UpdateModeStartLabel, settings.GetUpdateMode());
        settings.SaveSettings();
        WaitOnSaveEnabled(settings);
        menu.NavigateToDashboard();

        var baseline = LogBase(fixture);
        WaitOnUiState(() => menu.IsUpdateCheckButtonEnabled(),
            "Prüfen nach Einstellungs-Speichern aktiviert", Medium);
        menu.CheckClickUpdate();
        await WaitOnEventSinceAsync(fixture, baseline, UpdateE2EEvents.UpdateAttemptCompleted, Long,
            filter: e => DataText(e, "versuch") == "pruefen");
        Assert.Equal(UpdateE2EFixture.StableVersion, menu.WaitForOfferedUpdateVersion(Medium));
        AssertNoUpdateSideeffectSince(fixture, baseline);
        return menu;
    }

    /// <summary>
    /// Öffnet die Einstellungen und wartet deterministisch, bis der Ladevorgang der
    /// Update-Werte abgeschlossen ist: ein neuer markierter Settings-Read ist gelaufen und
    /// der "Speichern"-Button ist wieder aktiviert (AsyncRelayCommand ist während
    /// <c>LadenAsync</c> deaktiviert).
    /// </summary>
    /// <param name="fixture">Die Update-Fixture.</param>
    /// <param name="menu">Das Navigationsmenü.</param>
    /// <returns>Die geladene Einstellungs-Ansicht.</returns>
    private async Task<SettingsView> OpenSettingsLoadedAsync(UpdateE2EFixture fixture, MenuView menu)
    {
        var readsBefore = fixture.CountEvent(UpdateE2EEvents.UpdateSettingsReadReached);
        var settings = menu.NavigateToSettings();
        await fixture.WaitOnEventAsync(UpdateE2EEvents.UpdateSettingsReadReached, Medium,
            mindestens: readsBefore + 1);
        WaitOnSaveEnabled(settings);
        return settings;
    }

    /// <summary>
    /// Setzt Update-Modus und Update-Pfad über die echte Settings-UI, speichert und
    /// navigiert zurück zum Dashboard.
    /// </summary>
    /// <param name="fixture">Die Update-Fixture.</param>
    /// <param name="menu">Das Navigationsmenü.</param>
    /// <param name="modeLabel">Das exakte Update-Modus-Label.</param>
    /// <param name="pathLabel">Das zu setzende Update-Pfad-Label.</param>
    private async Task SaveUpdateSettingsOverUiAsync(
        UpdateE2EFixture fixture, MenuView menu, string modeLabel, string pathLabel)
    {
        var settings = await OpenSettingsLoadedAsync(fixture, menu);
        settings.SetUpdateMode(modeLabel);
        settings.SetUpdatePath(pathLabel);
        settings.SaveSettings();
        WaitOnSaveEnabled(settings);
        menu.NavigateToDashboard();
    }

    /// <summary>
    /// Klickt "Programmupdate prüfen" und wartet auf den Abschluss des Prüfversuchs; prüft
    /// anschließend Angebot und CheckCompleted-Protokolleintrag für die erwartete Version.
    /// </summary>
    private async Task CheckUpdateOverUiAsync(
        UpdateE2EFixture fixture, MenuView menu, int baseline, string expectedVersion,
        bool isPrerelease, string expectedPath)
    {
        WaitOnUiState(() => menu.IsUpdateCheckButtonEnabled(), "Prüfen-Button aktiviert", Medium);
        menu.CheckClickUpdate();
        await WaitOnEventSinceAsync(fixture, baseline, UpdateE2EEvents.UpdateAttemptCompleted, Long,
            filter: e => DataText(e, "versuch") == "pruefen");
        Assert.Equal(expectedVersion, menu.WaitForOfferedUpdateVersion(Medium));
        var expectedChannel = expectedPath == UpdatePathUpfrontLabel
            ? nameof(UpdateChannel.ReleaseCandidate)
            : nameof(UpdateChannel.Stable);
        Assert.Contains(EntriesSince(fixture, baseline), e => e.Event == UpdateE2EEvents.UpdateCheckStarted
            && DataText(e, "updatePfad") == expectedPath
            && DataText(e, "kanal") == expectedChannel);
        Assert.Contains(EntriesSince(fixture, baseline), e => e.Event == UpdateE2EEvents.UpdateCheckCompleted
            && DataText(e, "status") == "UpdateVerfuegbar"
            && DataText(e, "version") == expectedVersion
            && DataBool(e, "isPrerelease") == isPrerelease);
    }

    /// <summary>
    /// Prüft, dass die Einstellungen bedienbar bleiben: öffnen, Update-Modus (Startmodus)
    /// wird angezeigt, zurück zum Dashboard.
    /// </summary>
    private async Task CheckSettingsOperableAsync(UpdateE2EFixture fixture, MenuView menu)
    {
        var settings = await OpenSettingsLoadedAsync(fixture, menu);
        Assert.Equal(UpdateModeStartLabel, settings.GetUpdateMode());
        menu.NavigateToDashboard();
    }

    /// <summary>
    /// Nachweis einer echten Installation bis zur kontrollierten Launcher-/Shutdown-Grenze:
    /// Asset-Abrufe genau auf den erwarteten URLs (primäre Download-URL, optional weitere
    /// wie die API-Asset-Fallback-URL), entpacktes Paket mit passender <c>version.json</c>
    /// und Markerinhalt, Skript mit <c>-ExtractedDirectory</c> auf das entpackte Verzeichnis
    /// der Version, genau ein Launcher-Erfolg vor genau einem Shutdown.
    /// </summary>
    private static void AssertInstallationProof(
        UpdateE2EFixture fixture, int baseline, string expectedVersion, string expectedDownloadUrl,
        string expectedMarker, IReadOnlyList<string>? furtherExpectedAssetUrl = null)
    {
        var entries = EntriesSince(fixture, baseline);

        var expectedAssetUrl = new List<string?> { expectedDownloadUrl };
        if (furtherExpectedAssetUrl is not null)
            expectedAssetUrl.AddRange(furtherExpectedAssetUrl);
        Assert.Equal(expectedAssetUrl, entries
            .Where(e => e.Event == UpdateE2EEvents.HttpRequest && DataText(e, "art") == "asset")
            .Select(e => DataText(e, "url"))
            .ToList());

        var preparation = Assert.Single(entries,
            e => e.Event == UpdateE2EEvents.PreparationCompleted);
        var extractedDirectory = DataText(preparation, "extractedDirectory");
        Assert.NotNull(extractedDirectory);
        Assert.Equal(expectedVersion,
            Path.GetFileName(extractedDirectory!.TrimEnd(Path.DirectorySeparatorChar)));
        var versionJson = File.ReadAllText(Path.Combine(extractedDirectory, "version.json"));
        Assert.Contains($"\"version\":\"{expectedVersion}\"", versionJson);
        Assert.Equal($"E2E-Paket {expectedVersion} ({expectedMarker})",
            UpdateE2EFixture.ReadPackageExeContent(extractedDirectory));
        var scriptPath = DataText(preparation, "scriptPath");
        Assert.NotNull(scriptPath);
        Assert.True(File.Exists(scriptPath), $"Update-Skript fehlt: {scriptPath}");

        var launcher = Assert.Single(entries,
            e => e.Event == UpdateE2EEvents.UpdateProcessStartRecorded);
        Assert.Equal("Erfolg", DataText(launcher, "result"));
        Assert.Equal(fixture.TestRoot, DataText(launcher, "workingDirectory"));
        var arguments = UpdateE2EFixture.Data(launcher) is { } launcherData
            && launcherData.TryGetProperty("arguments", out var argsElement)
                ? argsElement.EnumerateArray().Select(a => a.GetString()).ToList()
                : [];
        var dirIndex = arguments.IndexOf("-ExtractedDirectory");
        Assert.True(dirIndex >= 0 && dirIndex + 1 < arguments.Count,
            "Launcher-Argumente enthalten kein -ExtractedDirectory.");
        Assert.Equal(extractedDirectory, arguments[dirIndex + 1]);

        Assert.Single(entries, e => e.Event == UpdateE2EEvents.UpdateStartAttempt);
        Assert.Single(entries, e => e.Event == UpdateE2EEvents.UpdateStartSucceeded);
        var shutdown = Assert.Single(entries, e => e.Event == UpdateE2EEvents.ShutdownRequested);
        Assert.True(shutdown.Seq > launcher.Seq,
            "ShutdownRequested muss nach dem aufgezeichneten Launcher-Start protokolliert sein.");
    }

    /// <summary>
    /// Prüft, dass seit <paramref name="baseline"/> keine Update-Nebenwirkungen auftraten.
    /// Mit <paramref name="allowPreparation"/> wird ein abgeschlossenes Paket-
    /// PreparationCompleted toleriert (BeforeUpdaterStart-Grenze: die Vorbereitung
    /// läuft, nur der Updater-Start selbst wird geblockt).
    /// </summary>
    private static void AssertNoUpdateSideeffectSince(
        UpdateE2EFixture fixture, int baseline, bool allowPreparation = false)
    {
        var entries = EntriesSince(fixture, baseline);
        if (!allowPreparation)
            Assert.DoesNotContain(entries, e => e.Event == UpdateE2EEvents.PreparationCompleted);
        Assert.DoesNotContain(entries, e => e.Event == UpdateE2EEvents.UpdateStartAttempt);
        Assert.DoesNotContain(entries, e => e.Event == UpdateE2EEvents.UpdateProcessStartRecorded);
        Assert.DoesNotContain(entries, e => e.Event == UpdateE2EEvents.ShutdownRequested);
    }

    /// <summary>Anzahl bisher geschriebener Protokolleinträge (Phasengrenze im JSONL).</summary>
    private static int LogBase(UpdateE2EFixture fixture)
        => fixture.ReadLog().Count;

    /// <summary>Protokolleinträge seit der Phasengrenze <paramref name="baseline"/>.</summary>
    private static List<UpdateE2ELogEntry> EntriesSince(UpdateE2EFixture fixture, int baseline)
        => fixture.ReadLog().Skip(baseline).ToList();

    /// <summary>Anzahl der Protokolleinträge eines Ereignisnamens seit der Phasengrenze.</summary>
    private static int CountEventSince(
        UpdateE2EFixture fixture, int baseline, string evt,
        Func<UpdateE2ELogEntry, bool>? filter = null)
        => EntriesSince(fixture, baseline).Count(e => e.Event == evt && (filter?.Invoke(e) ?? true));

    /// <summary>Wartet auf ein Protokollereignis seit der Phasengrenze.</summary>
    private async Task<IReadOnlyList<UpdateE2ELogEntry>> WaitOnEventSinceAsync(
        UpdateE2EFixture fixture, int baseline, string evt, TimeSpan timeout,
        int mindestens = 1, Func<UpdateE2ELogEntry, bool>? filter = null)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var match = EntriesSince(fixture, baseline)
                .Where(e => e.Event == evt && (filter?.Invoke(e) ?? true))
                .ToList();
            if (match.Count >= mindestens)
                return match;

            await Task.Delay(100);
        }

        throw new TimeoutException(
            $"Protokollereignis '{evt}' (mindestens {mindestens}) seit Basis {baseline} erschien nicht " +
            $"innerhalb von {timeout.TotalSeconds}s. Ereignisse seit Basis: " +
            $"{string.Join(", ", EntriesSince(fixture, baseline).Select(e => e.Event).Distinct())}");
    }

    /// <summary>Wartet begrenzt auf einen UI-Zustand (gebundene Waits statt fester Sleeps).</summary>
    private static void WaitOnUiState(Func<bool> condition, string description, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
                return;

            Thread.Sleep(150);
        }

        throw new TimeoutException($"UI-Zustand '{description}' nicht innerhalb von {timeout.TotalSeconds}s erreicht.");
    }

    /// <summary>
    /// Wartet, bis der "Speichern"-Ribbon-Button wieder aktiviert ist. Während
    /// <c>LadenAsync</c>/<c>SpeichernAsync</c> ist er über das AsyncRelayCommand deaktiviert.
    /// </summary>
    private void WaitOnSaveEnabled(SettingsView settings)
        => WaitOnUiState(
            () => settings.Window.FindFirstDescendant(cf => cf.ByName("Save"))?.IsEnabled == true,
            "Speichern-Button aktiviert", Medium);

    /// <summary>
    /// Führt eine Aufräumaktion aus und schluckt jeden Fehler: Im Cleanup-Pfad darf ein
    /// bereits teilweise entsorgtes Fixture oder eine beendete App die restliche
    /// Aufräumsequenz nicht maskieren.
    /// </summary>
    private static void BestEffort(Action action)
    {
        try
        {
            action();
        }
        catch
        {
            // Cleanup ist best-effort: Die Fixture kann bereits entsorgt sein oder die
            // App wurde bereits beendet - Fehler hier sind erwartbar und irrelevant.
        }
    }

    /// <summary>
    /// Gibt blockierte Gates frei, deaktiviert die Fehlersteuerung und beantwortet ggf.
    /// offene Dialoge, damit die Fixture-App auch auf einem Fehlerpfad regulär schließt.
    /// </summary>
    private async Task SetUpdateControlBackAsync(UpdateE2EFixture fixture, Window? mainWindow)
    {
        BestEffort(() => fixture.OpenGate(UpdateDownloadGate));
        BestEffort(() => fixture.OpenGate(UpdateReleaseGate));
        BestEffort(() => fixture.OpenGate(UpdateE2ETestContext.ReadFailureGateName));
        BestEffort(fixture.DeactivateReadfailure);

        if (mainWindow is not null)
        {
            BestEffort(() =>
            {
                var safe = new UpdateSafetyDialogView(mainWindow);
                if (safe.IsVisible)
                    safe.Cancel();
            });
        }

        // Auf den Abschluss eines ggf. freigegebenen laufenden Versuchs warten.
        var deadline = DateTime.UtcNow + Medium;
        while (DateTime.UtcNow < deadline)
        {
            var entries = fixture.ReadLog();
            if (entries.Count(e => e.Event == UpdateE2EEvents.UpdateAttemptStarted)
                <= entries.Count(e => e.Event == UpdateE2EEvents.UpdateAttemptCompleted))
                break;

            await Task.Delay(200);
        }

        if (mainWindow is not null)
        {
            BestEffort(() =>
            {
                var progress = new UpdateProgressDialogView(mainWindow);
                if (progress.IsVisible)
                    progress.Close();
            });
        }

        BestEffort(() => fixture.SetGateBack(UpdateDownloadGate));
        BestEffort(() => fixture.SetGateBack(UpdateReleaseGate));
        BestEffort(() => fixture.SetGateBack(UpdateE2ETestContext.ReadFailureGateName));
    }

    /// <summary>Erzeugt eine Release-Liste mit dem RC-Paket und einer semantisch höheren stabilen Version.</summary>
    private static string CreateRcAndHigherStableReleaseJson(UpdateE2EFixture fixture)
    {
        var releases = new[]
        {
            UpdateE2EFixture.CreateReleaseJson(
                UpdateE2EFixture.RcVersion,
                prerelease: true,
                DateTimeOffset.UtcNow,
                UpdateE2EFixture.CreateAssetJson(
                    UpdateE2EFixture.RcAssetApiUrl, UpdateE2EFixture.RcAssetId, fixture.RcDownloadUrl),
                UpdateE2EFixture.CreateManifestAssetJson(
                    UpdateE2EFixture.RcManifestAssetApiUrl, UpdateE2EFixture.RcManifestAssetId, fixture.RcManifestDownloadUrl)),
            UpdateE2EFixture.CreateReleaseJson(
                "9.9.9",
                prerelease: false,
                DateTimeOffset.UtcNow,
                UpdateE2EFixture.CreateAssetJson(
                    UpdateE2EFixture.AssetApiUrl(91999), 91999L, "https://e2e.invalid/unused-stable.zip"))
        };
        return JsonSerializer.Serialize(releases);
    }

    /// <summary>Erzeugt eine Release-Listen-JSON mit den angegebenen Stable-Versionen.</summary>
    private static string CreateReleaseListsJson(UpdateE2EFixture fixture, params string[] versionen)
    {
        var releases = versionen.Select(version => UpdateE2EFixture.CreateReleaseJson(
            version,
            prerelease: false,
            DateTimeOffset.UtcNow.AddDays(-7),
            UpdateE2EFixture.CreateAssetJson(
                UpdateE2EFixture.StableAssetApiUrl, UpdateE2EFixture.StableAssetId, fixture.StableDownloadUrl)));
        return JsonSerializer.Serialize(releases);
    }
}
