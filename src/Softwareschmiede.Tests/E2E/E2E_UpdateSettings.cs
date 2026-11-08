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
    private const string UpdateModusAusLabel = UpdateModusTexte.Aus;
    private const string UpdateModusNurPruefenLabel = UpdateModusTexte.NurPruefen;
    private const string UpdateModusStartLabel = UpdateModusTexte.BeiProgrammstartPruefenUndAusfuehren;
    private const string UpdatePfadProduktivLabel = UpdatePfadTexte.Produktivversionen;
    private const string UpdatePfadVorabLabel = UpdatePfadTexte.Vorabversionen;
    private const string UpdateHinweisKanalKonfliktTeil = "passt nicht";
    private const string UpdateDownloadGate = "download-block";
    private const string UpdateReleaseGate = "release-block";
    private const string UpdateHinweisNichtPruefbarTeil = "nicht prüfbar";
    private const uint GwOwner = 4;

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

    /// <summary>
    /// E-01: Alle drei exakten Update-Modus-Labels und beide Update-Pfade
    /// werden über die echte Settings-UI gesetzt, gespeichert und nach Weg-/Zurücknavigation
    /// sowie nach einem echten, DB-erhaltenden Neustart wieder angezeigt. Eine ungespeicherte
    /// Änderung wird über "Verwerfen" zurückgesetzt. Der lange Auswahltext bleibt bei der
    /// minimalen Fenstergröße lesbar (ComboBox ist breit genug).
    /// </summary>
    private async Task Settings_AllModesAndUpdatePfadPersist()
    {
        using var fixture = new UpdateE2EFixture();
        await fixture.SetzeUpdateEinstellungenAsync(UpdateMode.Aus, UpdatePfadKanalResolver.VorabversionenPfad);

        // Die Protokoll-Basis muss VOR dem Start gezogen werden: Der komplette Startlauf
        // (inkl. StartupUpdateCompleted) kann bereits während des Fenster-Wartens
        // protokolliert sein, bevor GetMainWindow zurückkehrt.
        var basis = ProtokollBasis(fixture);
        var app = LaunchApp(ensureDatabaseDeleted: false, fixture.ErzeugeStartUmgebung());
        Window? mainWindow = null;
        try
        {
            mainWindow = WarteAufEchtesHauptfenster(app);
            var menu = new MenuView(mainWindow);
            await WarteAufEreignisSeitAsync(fixture, basis, UpdateE2EEreignisse.StartupUpdateCompleted, Long);

            var settings = await OeffneSettingsGeladenAsync(fixture, menu);
            Assert.Equal(UpdateModusAusLabel, settings.GetUpdateMode());
            Assert.Equal(UpdatePfadVorabLabel, settings.GetUpdatePfad());
            Assert.Equal(UpdatePfadTexte.KanalReleaseCandidate, settings.GetAbgeleitetenUpdateKanal());

            // Alle drei exakten Labels nacheinander setzen und speichern.
            settings.SetUpdateMode(UpdateModusNurPruefenLabel).SaveSettings();
            WarteAufSpeichernAktiviert(settings);
            Assert.Equal(UpdateModusNurPruefenLabel, settings.GetUpdateMode());

            settings.SetUpdateMode(UpdateModusStartLabel).SaveSettings();
            WarteAufSpeichernAktiviert(settings);
            Assert.Equal(UpdateModusStartLabel, settings.GetUpdateMode());

            // Minimale Fenstergröße (MinWidth/MinHeight 900x600 aus MainWindow.xaml):
            // Der lange Auswahltext muss im aufgeklappten Dropdown vollständig im
            // Fenster lesbar bleiben (TextWrapping statt horizontalem Abschneiden).
            settings.Window.Patterns.Transform.Pattern.Resize(900, 600);
            Thread.Sleep(300);
            var updateCombo = WaitForElement(settings.Window, cf => cf.ByName("Update-Modus"), Short);
            updateCombo.Patterns.ExpandCollapse.Pattern.Expand();
            var langerEintrag = WaitForElement(
                updateCombo,
                cf => cf.ByName(UpdateModusStartLabel).And(cf.ByControlType(ControlType.ListItem)),
                Medium);
            var fensterRect = settings.Window.BoundingRectangle;
            var eintragRect = langerEintrag.BoundingRectangle;
            Assert.True(
                eintragRect.Left >= fensterRect.Left
                && eintragRect.Right <= fensterRect.Right
                && eintragRect.Top >= fensterRect.Top
                && eintragRect.Bottom <= fensterRect.Bottom
                && eintragRect.Width >= 200,
                $"Der lange Update-Modus-Text ist bei minimaler Fenstergröße nicht vollständig " +
                $"lesbar (Eintrag {eintragRect}, Fenster {fensterRect}).");
            updateCombo.Patterns.ExpandCollapse.Pattern.Collapse();

            settings.SetUpdateMode(UpdateModusAusLabel).SaveSettings();
            WarteAufSpeichernAktiviert(settings);
            Assert.Equal(UpdateModusAusLabel, settings.GetUpdateMode());

            // Beide Update-Pfade setzen und speichern; der abgeleitete Kanal folgt der Auswahl.
            settings.SetUpdatePfad(UpdatePfadProduktivLabel).SaveSettings();
            WarteAufSpeichernAktiviert(settings);
            Assert.Equal(UpdatePfadProduktivLabel, settings.GetUpdatePfad());
            Assert.Equal(UpdatePfadTexte.KanalProduktiv, settings.GetAbgeleitetenUpdateKanal());
            settings.SetUpdatePfad(UpdatePfadVorabLabel).SaveSettings();
            WarteAufSpeichernAktiviert(settings);
            Assert.Equal(UpdatePfadVorabLabel, settings.GetUpdatePfad());
            Assert.Equal(UpdatePfadTexte.KanalReleaseCandidate, settings.GetAbgeleitetenUpdateKanal());

            // "Nur prüfen" als Restart-Ausgangslage (kein Auto-Install beim Neustart).
            settings.SetUpdateMode(UpdateModusNurPruefenLabel).SaveSettings();
            WarteAufSpeichernAktiviert(settings);

            // Weg- und zurücknavigieren: die persistierten Werte werden erneut angezeigt.
            menu.NavigateToDashboard();
            settings = await OeffneSettingsGeladenAsync(fixture, menu);
            Assert.Equal(UpdateModusNurPruefenLabel, settings.GetUpdateMode());
            Assert.Equal(UpdatePfadVorabLabel, settings.GetUpdatePfad());

            // Ungespeicherte Änderung wird über "Verwerfen" zurückgesetzt.
            settings.SetUpdateMode(UpdateModusAusLabel);
            Assert.Equal(UpdateModusAusLabel, settings.GetUpdateMode());
            settings.SetUpdatePfad(UpdatePfadProduktivLabel);
            Assert.Equal(UpdatePfadProduktivLabel, settings.GetUpdatePfad());
            var readsVorher = fixture.ZaehleEreignis(UpdateE2EEreignisse.UpdateSettingsReadReached);
            settings.DiscardChanges();
            await fixture.WarteAufEreignisAsync(UpdateE2EEreignisse.UpdateSettingsReadReached, Medium,
                mindestens: readsVorher + 1);
            settings.WaitForUpdateMode(UpdateModusNurPruefenLabel, Medium);
            settings.WaitForUpdatePfad(UpdatePfadVorabLabel, Medium);
            menu.NavigateToDashboard();

            // Echter Neustart mit derselben DB: beide Werte sind persistiert und steuern den
            // Startlauf sichtbar (Nur prüfen + Vorabversionen -> RC-Angebot mit
            // nicht-blockierender Konfliktwarnung, ohne Installation).
            basis = ProtokollBasis(fixture);
            mainWindow = RestartAppPreservingDatabase();
            menu = new MenuView(mainWindow);
            await WarteAufEreignisSeitAsync(fixture, basis, UpdateE2EEreignisse.StartupUpdateCompleted, Long);

            Assert.Equal(UpdateE2EFixture.RcVersion, menu.WaitForOfferedUpdateVersion(Medium));
            Assert.Contains(EintraegeSeit(fixture, basis), e => e.Ereignis == UpdateE2EEreignisse.UpdateCheckStarted
                && DatenText(e, "updatePfad") == UpdatePfadKanalResolver.VorabversionenPfad
                && DatenText(e, "kanal") == nameof(UpdateKanal.ReleaseCandidate));
            Assert.Contains(EintraegeSeit(fixture, basis), e => e.Ereignis == UpdateE2EEreignisse.UpdateCheckCompleted
                && DatenText(e, "version") == UpdateE2EFixture.RcVersion
                && DatenBool(e, "isPrerelease") == true);
            menu.WaitForUpdateHinweis(UpdateHinweisKanalKonfliktTeil, Medium);
            Assert.Equal(0, ZaehleEreignisSeit(fixture, basis, UpdateE2EEreignisse.UpdateProcessStartRecorded));
            Assert.Equal(0, ZaehleEreignisSeit(fixture, basis, UpdateE2EEreignisse.ShutdownRequested));

            settings = await OeffneSettingsGeladenAsync(fixture, menu);
            Assert.Equal(UpdateModusNurPruefenLabel, settings.GetUpdateMode());
            Assert.Equal(UpdatePfadVorabLabel, settings.GetUpdatePfad());
            Assert.Equal(UpdatePfadTexte.KanalReleaseCandidate, settings.GetAbgeleitetenUpdateKanal());
            menu.NavigateToDashboard();
        }
        finally
        {
            await SetzeUpdateSteuerungZurueckAsync(fixture, mainWindow);
            SchliesseFixtureApp();
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
        await fixture.SetzeUpdateEinstellungenAsync(UpdateMode.NurPruefen, UpdatePfadKanalResolver.ProduktivversionenPfad);

        // Die Protokoll-Basis muss VOR dem Start gezogen werden: Der komplette Startlauf
        // (inkl. StartupUpdateCompleted) kann bereits während des Fenster-Wartens
        // protokolliert sein, bevor GetMainWindow zurückkehrt.
        var basis = ProtokollBasis(fixture);
        var app = LaunchApp(ensureDatabaseDeleted: false, fixture.ErzeugeStartUmgebung());
        Window? mainWindow = null;
        try
        {
            mainWindow = WarteAufEchtesHauptfenster(app);
            var menu = new MenuView(mainWindow);
            await WarteAufEreignisSeitAsync(fixture, basis, UpdateE2EEreignisse.StartupUpdateCompleted, Long);

            // Modus "Aus" über die UI speichern, regulär schließen, gleiche DB neu starten.
            await SpeichereUpdateEinstellungenUeberUiAsync(fixture, menu, UpdateModusAusLabel, UpdatePfadProduktivLabel);
            basis = ProtokollBasis(fixture);
            mainWindow = RestartAppPreservingDatabase();
            menu = new MenuView(mainWindow);
            await WarteAufEreignisSeitAsync(fixture, basis, UpdateE2EEreignisse.StartupUpdateCompleted, Long);

            WarteAufUiZustand(() => !menu.IsUpdatePruefenButtonEnabled(),
                "Prüfen-Button im Modus Aus deaktiviert", Medium);
            Assert.False(menu.IsUpdateStartButtonVisible());
            Assert.Equal(0, ZaehleEreignisSeit(fixture, basis, UpdateE2EEreignisse.HttpRequest));
            Assert.Equal(0, ZaehleEreignisSeit(fixture, basis, UpdateE2EEreignisse.UpdateCheckStarted));
            AssertKeinUpdateNebenwirkungSeit(fixture, basis);

            // Modus "Nur prüfen" über die UI speichern, regulär schließen, neu starten.
            await SpeichereUpdateEinstellungenUeberUiAsync(fixture, menu, UpdateModusNurPruefenLabel, UpdatePfadProduktivLabel);
            basis = ProtokollBasis(fixture);
            mainWindow = RestartAppPreservingDatabase();
            menu = new MenuView(mainWindow);
            await WarteAufEreignisSeitAsync(fixture, basis, UpdateE2EEreignisse.StartupUpdateCompleted, Long);

            Assert.Contains(EintraegeSeit(fixture, basis), e => e.Ereignis == UpdateE2EEreignisse.HttpRequest
                && DatenText(e, "url") == UpdateE2EFixture.ReleaseApiUrl);
            Assert.Contains(EintraegeSeit(fixture, basis), e => e.Ereignis == UpdateE2EEreignisse.UpdateCheckCompleted
                && DatenText(e, "status") == "UpdateVerfuegbar"
                && DatenText(e, "version") == UpdateE2EFixture.StableVersion
                && DatenBool(e, "isPrerelease") == false);
            Assert.Equal(UpdateE2EFixture.StableVersion, menu.WaitForOfferedUpdateVersion(Medium));
            Assert.Equal(0, ZaehleEreignisSeit(fixture, basis, UpdateE2EEreignisse.HttpRequest,
                e => DatenText(e, "art") == "asset"));
            AssertKeinUpdateNebenwirkungSeit(fixture, basis);

            // Startmodus über die UI speichern; der ZIP-Stream wird per Gate blockiert.
            await SpeichereUpdateEinstellungenUeberUiAsync(fixture, menu, UpdateModusStartLabel, UpdatePfadProduktivLabel);
            fixture.AktualisiereSzenario(s =>
            {
                var paket = s.Antworten.Single(a => a.Url == fixture.StableDownloadUrl);
                paket.StreamGate = UpdateDownloadGate;
                paket.StreamGateBytes = 128;
            });
            basis = ProtokollBasis(fixture);
            mainWindow = RestartAppPreservingDatabase();
            menu = new MenuView(mainWindow);

            // Ohne Updateklick erscheint der echte Fortschrittsdialog; der Download blockiert
            // kontrolliert im Stream-Gate, nachdem die Releaseantwort sofort ankam.
            var dialogElement = WaitForWindow("Update vorbereiten", Long);
            await WarteAufEreignisSeitAsync(fixture, basis, UpdateE2EEreignisse.HttpRequestBlocked, Long,
                filter: e => DatenText(e, "gate") == UpdateDownloadGate);
            Assert.Contains(EintraegeSeit(fixture, basis), e => e.Ereignis == UpdateE2EEreignisse.HttpResponse
                && DatenText(e, "url") == UpdateE2EFixture.ReleaseApiUrl);

            // Korrekter Owner: Der Fortschrittsdialog gehört dem Hauptfenster.
            Assert.Equal(
                mainWindow.FrameworkAutomationElement.NativeWindowHandle,
                GetWindow(dialogElement.FrameworkAutomationElement.NativeWindowHandle, GwOwner));

            var fortschritt = new UpdateProgressDialogView(mainWindow);
            Assert.True(fortschritt.IsVisible);
            WarteAufUiZustand(() => fortschritt.GetPhase() == "Download", "Download-Phase", Medium);
            Assert.True(fortschritt.CanCancel());
            WarteAufUiZustand(() => fortschritt.GetProgress() is > 0 and < 100,
                "Teilfortschritt des blockierten Downloads", Medium);

            // Während des blockierten Downloads bleiben beide Update-Commands gesperrt.
            WarteAufUiZustand(() => !menu.IsUpdatePruefenButtonEnabled(), "Prüfen während Download gesperrt", Medium);
            Assert.False(menu.IsUpdateStartButtonEnabled());

            // Freigabe: reale Vorbereitung bis zur kontrollierten Launcher-/Shutdown-Grenze.
            fixture.OeffneGate(UpdateDownloadGate);
            await WarteAufEreignisSeitAsync(fixture, basis, UpdateE2EEreignisse.StartupUpdateCompleted, Long);
            AssertiereInstallationsNachweis(fixture, basis,
                UpdateE2EFixture.StableVersion, fixture.StableDownloadUrl, "stable-marker");
            fortschritt.Close();
            fixture.SetzeGateZurueck(UpdateDownloadGate);
        }
        finally
        {
            await SetzeUpdateSteuerungZurueckAsync(fixture, mainWindow);
            SchliesseFixtureApp();
        }
    }

    /// <summary>
    /// E-03: Der Update-Pfad wählt exklusiv den Kanal. In "Nur prüfen" werden beide Pfade
    /// per UI gespeichert und der Prüfbutton geklickt: der Produktiv-Pfad bietet exakt
    /// Stable an, der Vorab-Pfad exakt RC - eine semantisch höhere stabile Version wird im
    /// RC-Kanal nicht gewählt. Der Installationsbutton führt die echte Vorbereitung bis zur
    /// aufgezeichneten Übergabe aus; Download-URL, entpackte <c>version.json</c> und
    /// Skriptziel gehören exakt zur gewählten Version. Der Kanalkonflikt (produktive
    /// Installation + Vorab-Pfad) zeigt die nicht-blockierende Warnung im UpdateHinweis
    /// und im Versuchsprotokoll. Beide Pfade werden zusätzlich mit UI-gespeichertem
    /// Startmodus und je echtem Neustart ohne Updateklick geprüft.
    /// </summary>
    private async Task UpdatePfad_SelectsExclusiveChannelAsset()
    {
        using var fixture = new UpdateE2EFixture();
        await fixture.SetzeUpdateEinstellungenAsync(UpdateMode.Aus, UpdatePfadKanalResolver.ProduktivversionenPfad);

        // Die Protokoll-Basis muss VOR dem Start gezogen werden: Der komplette Startlauf
        // (inkl. StartupUpdateCompleted) kann bereits während des Fenster-Wartens
        // protokolliert sein, bevor GetMainWindow zurückkehrt.
        var basis = ProtokollBasis(fixture);
        var app = LaunchApp(ensureDatabaseDeleted: false, fixture.ErzeugeStartUmgebung());
        Window? mainWindow = null;
        try
        {
            mainWindow = WarteAufEchtesHauptfenster(app);
            var menu = new MenuView(mainWindow);
            await WarteAufEreignisSeitAsync(fixture, basis, UpdateE2EEreignisse.StartupUpdateCompleted, Long);

            // Produktiv-Pfad: exakt Stable wird angeboten und installiert.
            await SpeichereUpdateEinstellungenUeberUiAsync(fixture, menu, UpdateModusNurPruefenLabel, UpdatePfadProduktivLabel);
            await PruefeUpdateUeberUiAsync(fixture, menu, basis = ProtokollBasis(fixture),
                UpdateE2EFixture.StableVersion, isPrerelease: false, UpdatePfadProduktivLabel);

            basis = ProtokollBasis(fixture);
            menu.ClickUpdateStarten();
            await WarteAufEreignisSeitAsync(fixture, basis, UpdateE2EEreignisse.UpdateAttemptCompleted, Long,
                filter: e => DatenText(e, "versuch") == "starten");
            AssertiereInstallationsNachweis(fixture, basis,
                UpdateE2EFixture.StableVersion, fixture.StableDownloadUrl, "stable-marker");
            Assert.Equal(0, ZaehleEreignisSeit(fixture, basis, UpdateE2EEreignisse.HttpRequest,
                e => DatenText(e, "url") == fixture.RcDownloadUrl));
            new UpdateProgressDialogView(mainWindow).Close();

            // Vorab-Pfad mit semantisch höherer stabiler Konkurrenz: exakt RC - die höhere
            // stabile Version ist kanalfremd. Produktive Installation + Vorab-Pfad ergibt
            // den Kanalkonflikt: Angebot bleibt, die Warnung ist nicht blockierend.
            fixture.AktualisiereSzenario(s =>
                s.Antworten.Single(a => a.Url == UpdateE2EFixture.ReleaseApiUrl).Inhalt =
                    ErzeugeRcUndHoeheresStableReleaseJson(fixture));
            await SpeichereUpdateEinstellungenUeberUiAsync(fixture, menu, UpdateModusNurPruefenLabel, UpdatePfadVorabLabel);
            await PruefeUpdateUeberUiAsync(fixture, menu, basis = ProtokollBasis(fixture),
                UpdateE2EFixture.RcVersion, isPrerelease: true, UpdatePfadVorabLabel);
            menu.WaitForUpdateHinweis(UpdateHinweisKanalKonfliktTeil, Medium);
            Assert.Contains(EintraegeSeit(fixture, basis), e => e.Ereignis == UpdateE2EEreignisse.UpdateAttemptCompleted
                && DatenText(e, "versuch") == "pruefen"
                && (DatenText(e, "hinweis") ?? string.Empty).Contains(UpdateHinweisKanalKonfliktTeil));

            basis = ProtokollBasis(fixture);
            menu.ClickUpdateStarten();
            await WarteAufEreignisSeitAsync(fixture, basis, UpdateE2EEreignisse.UpdateAttemptCompleted, Long,
                filter: e => DatenText(e, "versuch") == "starten");
            AssertiereInstallationsNachweis(fixture, basis,
                UpdateE2EFixture.RcVersion, fixture.RcDownloadUrl, "rc-marker");
            Assert.Equal(0, ZaehleEreignisSeit(fixture, basis, UpdateE2EEreignisse.HttpRequest,
                e => DatenText(e, "url") == fixture.StableDownloadUrl));
            new UpdateProgressDialogView(mainWindow).Close();

            // Startmodus + Produktiv-Pfad über die UI speichern: echter Neustart installiert
            // Stable ohne Updateklick.
            fixture.SetzeStandardAntworten();
            fixture.SpeichereSzenario();
            await SpeichereUpdateEinstellungenUeberUiAsync(fixture, menu, UpdateModusStartLabel, UpdatePfadProduktivLabel);
            basis = ProtokollBasis(fixture);
            mainWindow = RestartAppPreservingDatabase();
            menu = new MenuView(mainWindow);
            await WarteAufEreignisSeitAsync(fixture, basis, UpdateE2EEreignisse.StartupUpdateCompleted, Long);
            AssertiereInstallationsNachweis(fixture, basis,
                UpdateE2EFixture.StableVersion, fixture.StableDownloadUrl, "stable-marker");
            new UpdateProgressDialogView(mainWindow).Close();

            // Startmodus + Vorab-Pfad: echter Neustart installiert RC ohne Updateklick;
            // die Konfliktwarnung erscheint erneut, blockiert die Installation aber nicht.
            await SpeichereUpdateEinstellungenUeberUiAsync(fixture, menu, UpdateModusStartLabel, UpdatePfadVorabLabel);
            basis = ProtokollBasis(fixture);
            mainWindow = RestartAppPreservingDatabase();
            menu = new MenuView(mainWindow);
            await WarteAufEreignisSeitAsync(fixture, basis, UpdateE2EEreignisse.StartupUpdateCompleted, Long);
            AssertiereInstallationsNachweis(fixture, basis,
                UpdateE2EFixture.RcVersion, fixture.RcDownloadUrl, "rc-marker");
            menu.WaitForUpdateHinweis(UpdateHinweisKanalKonfliktTeil, Medium);
            Assert.Contains(EintraegeSeit(fixture, basis), e => e.Ereignis == UpdateE2EEreignisse.StartupUpdateCompleted
                && (DatenText(e, "hinweis") ?? string.Empty).Contains(UpdateHinweisKanalKonfliktTeil));
            new UpdateProgressDialogView(mainWindow).Close();
        }
        finally
        {
            await SetzeUpdateSteuerungZurueckAsync(fixture, mainWindow);
            SchliesseFixtureApp();
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
        await fixture.SetzeUpdateEinstellungenAsync(UpdateMode.NurPruefen, UpdatePfadKanalResolver.VorabversionenPfad);

        // Die Protokoll-Basis muss VOR dem Start gezogen werden: Der komplette Startlauf
        // (inkl. StartupUpdateCompleted) kann bereits während des Fenster-Wartens
        // protokolliert sein, bevor GetMainWindow zurückkehrt.
        var basis = ProtokollBasis(fixture);
        var app = LaunchApp(ensureDatabaseDeleted: false, fixture.ErzeugeStartUmgebung());
        Window? mainWindow = null;
        try
        {
            mainWindow = WarteAufEchtesHauptfenster(app);
            var menu = new MenuView(mainWindow);
            await WarteAufEreignisSeitAsync(fixture, basis, UpdateE2EEreignisse.StartupUpdateCompleted, Long);
            Assert.Equal(UpdateE2EFixture.RcVersion, menu.WaitForOfferedUpdateVersion(Medium));

            // Update-Pfad auf Produktivversionen + speichern: das alte RC-Angebot verschwindet sofort, ohne dass
            // das Speichern selbst einen Update-Request auslöst.
            basis = ProtokollBasis(fixture);
            await SpeichereUpdateEinstellungenUeberUiAsync(fixture, menu, UpdateModusNurPruefenLabel, UpdatePfadProduktivLabel);
            WarteAufUiZustand(() => !menu.IsUpdateStartButtonVisible(),
                "Updateangebot nach Speichern entfernt", Medium);
            Assert.Null(menu.GetOfferedUpdateVersion());
            Assert.False(menu.IsUpdateStartButtonEnabled());
            Assert.Equal(0, ZaehleEreignisSeit(fixture, basis, UpdateE2EEreignisse.HttpRequest));

            // Erneut prüfen: Stable-Angebot; Installation fordert nur das Stable-Asset an.
            basis = ProtokollBasis(fixture);
            menu.ClickUpdatePruefen();
            await WarteAufEreignisSeitAsync(fixture, basis, UpdateE2EEreignisse.UpdateAttemptCompleted, Long,
                filter: e => DatenText(e, "versuch") == "pruefen");
            Assert.Equal(UpdateE2EFixture.StableVersion, menu.WaitForOfferedUpdateVersion(Medium));

            menu.ClickUpdateStarten();
            await WarteAufEreignisSeitAsync(fixture, basis, UpdateE2EEreignisse.UpdateAttemptCompleted, Long,
                filter: e => DatenText(e, "versuch") == "starten");
            AssertiereInstallationsNachweis(fixture, basis,
                UpdateE2EFixture.StableVersion, fixture.StableDownloadUrl, "stable-marker");
            new UpdateProgressDialogView(mainWindow).Close();

            // Erneut RC anbieten (Update-Pfad wieder auf Vorabversionen).
            await SpeichereUpdateEinstellungenUeberUiAsync(fixture, menu, UpdateModusNurPruefenLabel, UpdatePfadVorabLabel);
            basis = ProtokollBasis(fixture);
            menu.ClickUpdatePruefen();
            await WarteAufEreignisSeitAsync(fixture, basis, UpdateE2EEreignisse.UpdateAttemptCompleted, Long,
                filter: e => DatenText(e, "versuch") == "pruefen");
            Assert.Equal(UpdateE2EFixture.RcVersion, menu.WaitForOfferedUpdateVersion(Medium));

            // Laufenden Prüfrequest blockieren; währenddessen "Aus" speichern.
            fixture.AktualisiereSzenario(s =>
                s.Antworten.Single(a => a.Url == UpdateE2EFixture.ReleaseApiUrl).Gate = UpdateReleaseGate);
            basis = ProtokollBasis(fixture);
            menu.ClickUpdatePruefen();
            await WarteAufEreignisSeitAsync(fixture, basis, UpdateE2EEreignisse.HttpRequestBlocked, Long,
                filter: e => DatenText(e, "url") == UpdateE2EFixture.ReleaseApiUrl
                    && DatenText(e, "gate") == UpdateReleaseGate);

            await SpeichereUpdateEinstellungenUeberUiAsync(fixture, menu, UpdateModusAusLabel, UpdatePfadVorabLabel);
            WarteAufUiZustand(() => !menu.IsUpdateStartButtonVisible(),
                "Updateangebot nach Aus-Speichern entfernt", Medium);
            WarteAufUiZustand(() => !menu.IsUpdatePruefenButtonEnabled(),
                "Prüfen im Modus Aus deaktiviert", Medium);

            // Antwort freigeben: das späte Ergebnis wird verworfen.
            fixture.OeffneGate(UpdateReleaseGate);
            await WarteAufEreignisSeitAsync(fixture, basis, UpdateE2EEreignisse.UpdateAttemptCompleted, Long,
                filter: e => DatenText(e, "versuch") == "pruefen");

            var phaseEintraege = EintraegeSeit(fixture, basis);
            Assert.Contains(phaseEintraege, e => e.Ereignis == UpdateE2EEreignisse.UpdateCheckCompleted
                && DatenText(e, "version") == UpdateE2EFixture.RcVersion);
            Assert.Null(menu.GetOfferedUpdateVersion());
            Assert.False(menu.IsUpdateStartButtonVisible());
            Assert.DoesNotContain(phaseEintraege, e => e.Ereignis == UpdateE2EEreignisse.HttpRequest
                && DatenText(e, "art") == "asset");
            AssertKeinUpdateNebenwirkungSeit(fixture, basis);
            fixture.AktualisiereSzenario(s =>
                s.Antworten.Single(a => a.Url == UpdateE2EFixture.ReleaseApiUrl).Gate = null);
            fixture.SetzeGateZurueck(UpdateReleaseGate);

            // Gleiche DB neu starten: der Modus Aus erzeugt den Nullabruf.
            basis = ProtokollBasis(fixture);
            mainWindow = RestartAppPreservingDatabase();
            menu = new MenuView(mainWindow);
            await WarteAufEreignisSeitAsync(fixture, basis, UpdateE2EEreignisse.StartupUpdateCompleted, Long);
            Assert.Equal(1, ZaehleEreignisSeit(fixture, basis, UpdateE2EEreignisse.UpdateAttemptStarted));
            Assert.Equal(0, ZaehleEreignisSeit(fixture, basis, UpdateE2EEreignisse.HttpRequest));
            Assert.Equal(0, ZaehleEreignisSeit(fixture, basis, UpdateE2EEreignisse.UpdateCheckStarted));
            AssertKeinUpdateNebenwirkungSeit(fixture, basis);
            WarteAufUiZustand(() => !menu.IsUpdatePruefenButtonEnabled(),
                "Prüfen nach Aus-Restart deaktiviert", Medium);
            Assert.False(menu.IsUpdateStartButtonVisible());
        }
        finally
        {
            await SetzeUpdateSteuerungZurueckAsync(fixture, mainWindow);
            SchliesseFixtureApp();
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
        await fixture.SetzeUpdateEinstellungenAsync(UpdateMode.Aus, UpdatePfadKanalResolver.ProduktivversionenPfad);

        // Die Protokoll-Basis muss VOR dem Start gezogen werden: Der komplette Startlauf
        // (inkl. StartupUpdateCompleted) kann bereits während des Fenster-Wartens
        // protokolliert sein, bevor GetMainWindow zurückkehrt.
        var basis = ProtokollBasis(fixture);
        var app = LaunchApp(ensureDatabaseDeleted: false, fixture.ErzeugeStartUmgebung());
        Window? mainWindow = null;
        try
        {
            mainWindow = WarteAufEchtesHauptfenster(app);
            var menu = new MenuView(mainWindow);
            await WarteAufEreignisSeitAsync(fixture, basis, UpdateE2EEreignisse.StartupUpdateCompleted, Long);
            await SpeichereUpdateEinstellungenUeberUiAsync(fixture, menu, UpdateModusStartLabel, UpdatePfadProduktivLabel);

            var versionsDatei = Path.Combine(fixture.InstalliertesVerzeichnis, "version.json");

            // Variante: gleiche/ältere Release-Version -> kein Update, keine Nebenwirkungen.
            fixture.AktualisiereSzenario(s =>
                s.Antworten.Single(a => a.Url == UpdateE2EFixture.ReleaseApiUrl).Inhalt =
                    ErzeugeReleaseListenJson(fixture, "1.2.0", "1.1.9"));
            basis = ProtokollBasis(fixture);
            mainWindow = RestartAppPreservingDatabase();
            menu = new MenuView(mainWindow);
            await WarteAufEreignisSeitAsync(fixture, basis, UpdateE2EEreignisse.StartupUpdateCompleted, Long);
            Assert.Contains(EintraegeSeit(fixture, basis), e => e.Ereignis == UpdateE2EEreignisse.UpdateCheckCompleted
                && DatenText(e, "status") == "KeinUpdate");
            Assert.Null(menu.GetUpdateHinweis());
            Assert.False(menu.IsUpdateStartButtonVisible());
            Assert.False(new UpdateProgressDialogView(mainWindow).IsVisible);
            Assert.Equal(0, ZaehleEreignisSeit(fixture, basis, UpdateE2EEreignisse.HttpRequest,
                e => DatenText(e, "art") == "asset"));
            AssertKeinUpdateNebenwirkungSeit(fixture, basis);
            await PruefeSettingsBedienbarAsync(fixture, menu);
            Assert.Equal(1, ZaehleEreignisSeit(fixture, basis, UpdateE2EEreignisse.UpdateAttemptStarted));

            // Variante: ungültige lokale version.json -> NichtPruefbar ohne Releaseabruf.
            File.WriteAllText(versionsDatei, """{"version":"keine-gueltige-version"}""");
            basis = ProtokollBasis(fixture);
            mainWindow = RestartAppPreservingDatabase();
            menu = new MenuView(mainWindow);
            await WarteAufEreignisSeitAsync(fixture, basis, UpdateE2EEreignisse.StartupUpdateCompleted, Long);
            Assert.Contains(EintraegeSeit(fixture, basis), e => e.Ereignis == UpdateE2EEreignisse.UpdateCheckCompleted
                && DatenText(e, "status") == "NichtPruefbar");
            Assert.Equal(0, ZaehleEreignisSeit(fixture, basis, UpdateE2EEreignisse.HttpRequest));
            menu.WaitForUpdateHinweis("Lokale Version", Medium);
            Assert.False(menu.IsUpdateStartButtonVisible());
            Assert.False(new UpdateProgressDialogView(mainWindow).IsVisible);
            AssertKeinUpdateNebenwirkungSeit(fixture, basis);
            await PruefeSettingsBedienbarAsync(fixture, menu);

            // Variante: fehlende lokale version.json in derselben Session (siehe Hinweis im
            // Methodenkommentar - als Start-Fixture scheitert bereits die Konfigurationsvalidierung).
            File.Delete(versionsDatei);
            basis = ProtokollBasis(fixture);
            WarteAufUiZustand(() => menu.IsUpdatePruefenButtonEnabled(), "Prüfen-Button aktiviert", Medium);
            menu.ClickUpdatePruefen();
            await WarteAufEreignisSeitAsync(fixture, basis, UpdateE2EEreignisse.UpdateAttemptCompleted, Long,
                filter: e => DatenText(e, "versuch") == "pruefen");
            Assert.Equal(0, ZaehleEreignisSeit(fixture, basis, UpdateE2EEreignisse.HttpRequest));
            menu.WaitForUpdateHinweis("Lokale Version", Medium);
            Assert.False(menu.IsUpdateStartButtonVisible());
            AssertKeinUpdateNebenwirkungSeit(fixture, basis);
            File.WriteAllText(versionsDatei, $$"""{"version":"{{UpdateE2EFixture.InstallierteVersion}}","tagName":"v{{UpdateE2EFixture.InstallierteVersion}}"}""");

            // Variante: Release-HTTP-Fehler -> NichtPruefbar, Hinweis, keine Nebenwirkungen.
            fixture.AktualisiereSzenario(s =>
            {
                var antwort = s.Antworten.Single(a => a.Url == UpdateE2EFixture.ReleaseApiUrl);
                antwort.Status = 500;
                antwort.Inhalt = "e2e-fehler";
            });
            basis = ProtokollBasis(fixture);
            mainWindow = RestartAppPreservingDatabase();
            menu = new MenuView(mainWindow);
            await WarteAufEreignisSeitAsync(fixture, basis, UpdateE2EEreignisse.StartupUpdateCompleted, Long);
            Assert.Equal(1, ZaehleEreignisSeit(fixture, basis, UpdateE2EEreignisse.HttpRequest,
                e => DatenText(e, "url") == UpdateE2EFixture.ReleaseApiUrl));
            Assert.Contains(EintraegeSeit(fixture, basis), e => e.Ereignis == UpdateE2EEreignisse.UpdateCheckCompleted
                && DatenText(e, "status") == "NichtPruefbar");
            menu.WaitForUpdateHinweis(UpdateHinweisNichtPruefbarTeil, Medium);
            Assert.False(menu.IsUpdateStartButtonVisible());
            Assert.False(new UpdateProgressDialogView(mainWindow).IsVisible);
            AssertKeinUpdateNebenwirkungSeit(fixture, basis);
            await PruefeSettingsBedienbarAsync(fixture, menu);

            // Variante: ungültiges Release-JSON -> NichtPruefbar, Hinweis, keine Nebenwirkungen.
            fixture.AktualisiereSzenario(s =>
            {
                var antwort = s.Antworten.Single(a => a.Url == UpdateE2EFixture.ReleaseApiUrl);
                antwort.Status = 200;
                antwort.Inhalt = "definitiv kein gueltiges json {{{";
            });
            basis = ProtokollBasis(fixture);
            mainWindow = RestartAppPreservingDatabase();
            menu = new MenuView(mainWindow);
            await WarteAufEreignisSeitAsync(fixture, basis, UpdateE2EEreignisse.StartupUpdateCompleted, Long);
            Assert.Equal(1, ZaehleEreignisSeit(fixture, basis, UpdateE2EEreignisse.HttpRequest,
                e => DatenText(e, "url") == UpdateE2EFixture.ReleaseApiUrl));
            Assert.Contains(EintraegeSeit(fixture, basis), e => e.Ereignis == UpdateE2EEreignisse.UpdateCheckCompleted
                && DatenText(e, "status") == "NichtPruefbar");
            menu.WaitForUpdateHinweis(UpdateHinweisNichtPruefbarTeil, Medium);
            Assert.False(menu.IsUpdateStartButtonVisible());
            AssertKeinUpdateNebenwirkungSeit(fixture, basis);
            await PruefeSettingsBedienbarAsync(fixture, menu);
        }
        finally
        {
            await SetzeUpdateSteuerungZurueckAsync(fixture, mainWindow);
            SchliesseFixtureApp();
        }
    }

    /// <summary>
    /// E-06: Startmodus wird über die UI gespeichert; für jeden automatischen Versuch wird
    /// neu gestartet. (a) Riskante CLI-Fixture: echter Ja/Nein-Dialog mit Nein - keine
    /// Vorbereitung/Asset/Start/Shutdown. (b) Ja bestätigt, echter blockierter
    /// Downloadfortschritt, Abbrechen - Abbruchzustand ohne Start/Shutdown. (c) Asset-HTTP-
    /// Fehler (automatisch) und defektes ZIP (manuell): sichtbarer Vorbereitungsfehler ohne
    /// Start/Shutdown. (c3) Primärer Asset-Abruf liefert 5xx: einmaliger Fallback auf die
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
        await fixture.SetzeUpdateEinstellungenAsync(UpdateMode.Aus, UpdatePfadKanalResolver.ProduktivversionenPfad);

        // Die Protokoll-Basis muss VOR dem Start gezogen werden: Der komplette Startlauf
        // (inkl. StartupUpdateCompleted) kann bereits während des Fenster-Wartens
        // protokolliert sein, bevor GetMainWindow zurückkehrt.
        var basis = ProtokollBasis(fixture);
        var app = LaunchApp(ensureDatabaseDeleted: false, fixture.ErzeugeStartUmgebung());
        Window? mainWindow = null;
        try
        {
            mainWindow = WarteAufEchtesHauptfenster(app);
            var menu = new MenuView(mainWindow);
            await WarteAufEreignisSeitAsync(fixture, basis, UpdateE2EEreignisse.StartupUpdateCompleted, Long);
            await SpeichereUpdateEinstellungenUeberUiAsync(fixture, menu, UpdateModusStartLabel, UpdatePfadProduktivLabel);

            // (a) Riskante CLI-Fixture: der echte Sicherheitsdialog wird mit Nein beantwortet.
            (mainWindow, menu) = await SafetyDialogAbgelehntPhaseAsync(fixture);

            // (b) Ja bestätigt: echter Downloadfortschritt, Abbrechen im blockierten Stream.
            (mainWindow, menu) = await DownloadAbbruchPhaseAsync(fixture);

            // (c1) Asset-HTTP-Fehler beim automatischen Versuch: sichtbarer Vorbereitungsfehler.
            (mainWindow, menu) = await AssetHttpFehlerPhaseAsync(fixture);

            // (c2) Defektes ZIP beim manuellen Startversuch in derselben Session.
            await DefektesZipPhaseAsync(fixture, menu);

            // (d) Gültige Vorbereitung, Launcherfehler: ein Versuch, kein erfolgreicher Start,
            // kein Shutdown.
            (mainWindow, menu) = await LauncherFehlerPhaseAsync(fixture);
            fixture.AktualisiereSzenario(s => s.ProzessStart = new UpdateE2EProzessStart { Ergebnis = "Erfolg" });

            // (c3) Primärer Asset-Abruf 5xx, einmaliger Fallback auf die API-Asset-URL:
            // die Installation läuft vollständig durch.
            (mainWindow, menu) = await AssetFallbackErfolgPhaseAsync(fixture);

            // T-09 automatisch: Lesefehler an allen drei Grenzen (Initial, BeforePreparation,
            // BeforeUpdaterStart) stoppen den Startversuch sicher.
            (mainWindow, menu) = await T09StartupPhaseAsync(fixture, "Initial", ordinal: 1,
                erwarteReleaseAbruf: false, erwarteVorbereitung: false, erwarteDialogFehler: false);
            (mainWindow, menu) = await T09StartupPhaseAsync(fixture, "BeforePreparation", ordinal: 2,
                erwarteReleaseAbruf: true, erwarteVorbereitung: false, erwarteDialogFehler: true);
            (mainWindow, menu) = await T09StartupPhaseAsync(fixture, "BeforeUpdaterStart", ordinal: 3,
                erwarteReleaseAbruf: true, erwarteVorbereitung: true, erwarteDialogFehler: true);

            // T-09 manuell: Grenze Initial beim Prüfversuch (kein Releaseabruf), dann die
            // Installationsgrenzen BeforePreparation/BeforeUpdaterStart beim Startversuch.
            menu = await T09ManuellInitialPhaseAsync(fixture, menu);
            await T09ManuellStartenPhaseAsync(fixture, menu, "BeforePreparation", ordinal: 2,
                erwarteVorbereitung: false);
            await T09ManuellStartenPhaseAsync(fixture, menu, "BeforeUpdaterStart", ordinal: 3,
                erwarteVorbereitung: true);
            Assert.True(fixture.ZaehleEreignis(UpdateE2EEreignisse.UpdateSettingsReadFailureDisabled) >= 1);

            // Erfolgreiche manuelle Folgeprüfung nach Deaktivierung - ohne automatische
            // Installation.
            menu = await NachLesefehlerFortsetzenAsync(fixture, menu);
        }
        finally
        {
            await SetzeUpdateSteuerungZurueckAsync(fixture, mainWindow);
            SchliesseFixtureApp();
        }
    }

    /// <summary>
    /// E-06 (a): Riskante CLI-Fixture aktivieren, neu starten, den echten Sicherheitsdialog
    /// mit Nein beantworten - keine Vorbereitung, kein Asset-Abruf, keine Nebenwirkungen.
    /// </summary>
    private async Task<(Window MainWindow, MenuView Menu)> SafetyDialogAbgelehntPhaseAsync(
        UpdateE2EFixture fixture)
    {
        fixture.AktualisiereSzenario(s => s.CliSicherheit = new UpdateE2ECliSicherheit
        {
            Aktiv = true,
            RiskanteAufgaben = ["E2E-Risikoaufgabe"]
        });
        var basis = ProtokollBasis(fixture);
        var mainWindow = RestartAppPreservingDatabase();
        var menu = new MenuView(mainWindow);
        var fortschritt = new UpdateProgressDialogView(mainWindow);
        var sicherheit = new UpdateSafetyDialogView(mainWindow);
        await WarteAufEreignisSeitAsync(fixture, basis, UpdateE2EEreignisse.CliSafetyChecked, Long,
            filter: e => DatenZahl(e, "riskyTaskCount") >= 1);
        sicherheit.ForceShow();
        sicherheit.Cancel();
        await WarteAufEreignisSeitAsync(fixture, basis, UpdateE2EEreignisse.StartupUpdateCompleted, Long);
        Assert.False(fortschritt.IsVisible);
        Assert.Equal(0, ZaehleEreignisSeit(fixture, basis, UpdateE2EEreignisse.HttpRequest,
            e => DatenText(e, "art") == "asset"));
        AssertKeinUpdateNebenwirkungSeit(fixture, basis);
        Assert.Equal(1, ZaehleEreignisSeit(fixture, basis, UpdateE2EEreignisse.UpdateAttemptStarted));
        await PruefeSettingsBedienbarAsync(fixture, menu);
        return (mainWindow, menu);
    }

    /// <summary>
    /// E-06 (b): Sicherheitsdialog mit Ja bestätigen, echten blockierten Download abbrechen -
    /// Abbruchzustand ohne Vorbereitung, Start oder Shutdown.
    /// </summary>
    private async Task<(Window MainWindow, MenuView Menu)> DownloadAbbruchPhaseAsync(
        UpdateE2EFixture fixture)
    {
        fixture.AktualisiereSzenario(s =>
        {
            var paket = s.Antworten.Single(a => a.Url == fixture.StableDownloadUrl);
            paket.StreamGate = UpdateDownloadGate;
            paket.StreamGateBytes = 128;
        });
        var basis = ProtokollBasis(fixture);
        var mainWindow = RestartAppPreservingDatabase();
        var menu = new MenuView(mainWindow);
        var fortschritt = new UpdateProgressDialogView(mainWindow);
        var sicherheit = new UpdateSafetyDialogView(mainWindow);
        await WarteAufEreignisSeitAsync(fixture, basis, UpdateE2EEreignisse.CliSafetyChecked, Long,
            filter: e => DatenZahl(e, "riskyTaskCount") >= 1);
        sicherheit.ForceShow();
        sicherheit.Confirm();
        await WarteAufEreignisSeitAsync(fixture, basis, UpdateE2EEreignisse.HttpRequestBlocked, Long,
            filter: e => DatenText(e, "gate") == UpdateDownloadGate);
        Assert.True(fortschritt.IsVisible);
        WarteAufUiZustand(() => fortschritt.GetPhase() == "Download", "Download-Phase", Medium);
        fortschritt.Cancel();
        var abbruchMeldung = fortschritt.WaitForErrorState(Medium);
        Assert.Contains("abgebr", abbruchMeldung);
        await WarteAufEreignisSeitAsync(fixture, basis, UpdateE2EEreignisse.StartupUpdateCompleted, Long);
        Assert.Equal(1, ZaehleEreignisSeit(fixture, basis, UpdateE2EEreignisse.PreparationFailed));
        Assert.Equal(0, ZaehleEreignisSeit(fixture, basis, UpdateE2EEreignisse.PreparationCompleted));
        Assert.Equal(0, ZaehleEreignisSeit(fixture, basis, UpdateE2EEreignisse.UpdateStartAttempt));
        Assert.Equal(0, ZaehleEreignisSeit(fixture, basis, UpdateE2EEreignisse.UpdateProcessStartRecorded));
        Assert.Equal(0, ZaehleEreignisSeit(fixture, basis, UpdateE2EEreignisse.ShutdownRequested));
        fortschritt.Close();
        fixture.SetzeGateZurueck(UpdateDownloadGate);
        fixture.AktualisiereSzenario(s =>
        {
            s.CliSicherheit = new UpdateE2ECliSicherheit { Aktiv = false };
            s.Antworten.Single(a => a.Url == fixture.StableDownloadUrl).StreamGate = null;
        });
        await PruefeSettingsBedienbarAsync(fixture, menu);
        Assert.Equal(1, ZaehleEreignisSeit(fixture, basis, UpdateE2EEreignisse.UpdateAttemptStarted));
        return (mainWindow, menu);
    }

    /// <summary>
    /// E-06 (c1): Asset-HTTP-Fehler beim automatischen Versuch - primäre Download-URL und
    /// API-Asset-Fallback-URL liefern beide 500, sichtbarer Vorbereitungsfehler ohne
    /// Start/Shutdown.
    /// </summary>
    private async Task<(Window MainWindow, MenuView Menu)> AssetHttpFehlerPhaseAsync(
        UpdateE2EFixture fixture)
    {
        fixture.AktualisiereSzenario(s =>
        {
            s.Antworten.Single(a => a.Url == fixture.StableDownloadUrl).Status = 500;
            s.Antworten.Single(a => a.Url == UpdateE2EFixture.StableAssetApiUrl).Status = 500;
        });
        var basis = ProtokollBasis(fixture);
        var mainWindow = RestartAppPreservingDatabase();
        var menu = new MenuView(mainWindow);
        var fortschritt = new UpdateProgressDialogView(mainWindow);
        await WarteAufEreignisSeitAsync(fixture, basis, UpdateE2EEreignisse.PreparationFailed, Long);
        Assert.Contains("konnte nicht vorbereitet", fortschritt.WaitForErrorState(Medium));
        await WarteAufEreignisSeitAsync(fixture, basis, UpdateE2EEreignisse.StartupUpdateCompleted, Long);
        Assert.Equal(1, ZaehleEreignisSeit(fixture, basis, UpdateE2EEreignisse.HttpRequest,
            e => DatenText(e, "url") == fixture.StableDownloadUrl));
        Assert.Equal(1, ZaehleEreignisSeit(fixture, basis, UpdateE2EEreignisse.HttpRequest,
            e => DatenText(e, "url") == UpdateE2EFixture.StableAssetApiUrl));
        Assert.Equal(0, ZaehleEreignisSeit(fixture, basis, UpdateE2EEreignisse.UpdateStartAttempt));
        Assert.Equal(0, ZaehleEreignisSeit(fixture, basis, UpdateE2EEreignisse.UpdateProcessStartRecorded));
        Assert.Equal(0, ZaehleEreignisSeit(fixture, basis, UpdateE2EEreignisse.ShutdownRequested));
        fortschritt.Close();
        await PruefeSettingsBedienbarAsync(fixture, menu);
        Assert.Equal(1, ZaehleEreignisSeit(fixture, basis, UpdateE2EEreignisse.UpdateAttemptStarted));
        return (mainWindow, menu);
    }

    /// <summary>
    /// E-06 (c2): Defektes ZIP beim manuellen Startversuch in derselben Session -
    /// sichtbarer Vorbereitungsfehler ohne Start/Shutdown.
    /// </summary>
    private async Task DefektesZipPhaseAsync(UpdateE2EFixture fixture, MenuView menu)
    {
        var defektesPaket = Path.Combine(fixture.PaketVerzeichnis, "defekt.zip");
        File.WriteAllText(defektesPaket, "E2E: kein gueltiges ZIP");
        fixture.AktualisiereSzenario(s =>
        {
            var antwort = s.Antworten.Single(a => a.Url == fixture.StableDownloadUrl);
            antwort.Status = 200;
            antwort.BodyDatei = Path.GetRelativePath(fixture.Testwurzel, defektesPaket);
        });
        var basis = ProtokollBasis(fixture);
        menu.ClickUpdateStarten();
        await WarteAufEreignisSeitAsync(fixture, basis, UpdateE2EEreignisse.UpdateAttemptCompleted, Long,
            filter: e => DatenText(e, "versuch") == "starten");
        var fortschritt = new UpdateProgressDialogView(menu.Window);
        Assert.Equal(1, ZaehleEreignisSeit(fixture, basis, UpdateE2EEreignisse.PreparationFailed));
        Assert.Contains("konnte nicht vorbereitet", fortschritt.WaitForErrorState(Medium));
        Assert.Equal(0, ZaehleEreignisSeit(fixture, basis, UpdateE2EEreignisse.UpdateStartAttempt));
        Assert.Equal(0, ZaehleEreignisSeit(fixture, basis, UpdateE2EEreignisse.ShutdownRequested));
        fortschritt.Close();
        await PruefeSettingsBedienbarAsync(fixture, menu);
    }

    /// <summary>
    /// E-06 (d): Gültige Vorbereitung mit Launcherfehler - genau ein Versuch, kein
    /// erfolgreicher Start, kein Shutdown, sichtbarer Fehler.
    /// </summary>
    private async Task<(Window MainWindow, MenuView Menu)> LauncherFehlerPhaseAsync(
        UpdateE2EFixture fixture)
    {
        fixture.AktualisiereSzenario(s =>
        {
            var antwort = s.Antworten.Single(a => a.Url == fixture.StableDownloadUrl);
            antwort.BodyDatei = Path.GetRelativePath(fixture.Testwurzel, fixture.StableZipPfad);
            s.ProzessStart = new UpdateE2EProzessStart { Ergebnis = "Fehler" };
        });
        var basis = ProtokollBasis(fixture);
        var mainWindow = RestartAppPreservingDatabase();
        var menu = new MenuView(mainWindow);
        var fortschritt = new UpdateProgressDialogView(mainWindow);
        await WarteAufEreignisSeitAsync(fixture, basis, UpdateE2EEreignisse.UpdateProcessStartRecorded, Long);
        await WarteAufEreignisSeitAsync(fixture, basis, UpdateE2EEreignisse.StartupUpdateCompleted, Long);
        Assert.Equal(1, ZaehleEreignisSeit(fixture, basis, UpdateE2EEreignisse.UpdateStartAttempt));
        var launcherFehler = Assert.Single(EintraegeSeit(fixture, basis),
            e => e.Ereignis == UpdateE2EEreignisse.UpdateProcessStartRecorded);
        Assert.Equal("Fehler", DatenText(launcherFehler, "result"));
        Assert.Equal(1, ZaehleEreignisSeit(fixture, basis, UpdateE2EEreignisse.UpdateStartFailed));
        Assert.Equal(0, ZaehleEreignisSeit(fixture, basis, UpdateE2EEreignisse.UpdateStartSucceeded));
        Assert.Equal(0, ZaehleEreignisSeit(fixture, basis, UpdateE2EEreignisse.ShutdownRequested));
        Assert.Contains("konnte nicht vorbereitet", fortschritt.WaitForErrorState(Medium));
        fortschritt.Close();
        await PruefeSettingsBedienbarAsync(fixture, menu);
        Assert.Equal(1, ZaehleEreignisSeit(fixture, basis, UpdateE2EEreignisse.UpdateAttemptStarted));
        return (mainWindow, menu);
    }

    /// <summary>
    /// E-06 (c3): Primärer Asset-Abruf liefert 5xx - der einmalige Fallback auf die
    /// GitHub-API-Asset-URL rettet den Download; die Installation läuft vollständig durch.
    /// Genau ein Request je Download-Route; der vollständige Installationsnachweis läuft
    /// über <c>AssertiereInstallationsNachweis</c> mit beiden erwarteten Asset-URLs.
    /// </summary>
    private async Task<(Window MainWindow, MenuView Menu)> AssetFallbackErfolgPhaseAsync(
        UpdateE2EFixture fixture)
    {
        fixture.AktualisiereSzenario(s =>
        {
            s.Antworten.Single(a => a.Url == fixture.StableDownloadUrl).Status = 500;
            s.Antworten.Single(a => a.Url == UpdateE2EFixture.StableAssetApiUrl).Status = 200;
        });
        var basis = ProtokollBasis(fixture);
        var mainWindow = RestartAppPreservingDatabase();
        var menu = new MenuView(mainWindow);
        var fortschritt = new UpdateProgressDialogView(mainWindow);
        await WarteAufEreignisSeitAsync(fixture, basis, UpdateE2EEreignisse.StartupUpdateCompleted, Long);

        AssertiereInstallationsNachweis(fixture, basis,
            UpdateE2EFixture.StableVersion, fixture.StableDownloadUrl, "stable-marker",
            [UpdateE2EFixture.StableAssetApiUrl]);
        fortschritt.Close();
        fixture.AktualisiereSzenario(s =>
            s.Antworten.Single(a => a.Url == fixture.StableDownloadUrl).Status = 200);
        return (mainWindow, menu);
    }

    /// <summary>
    /// E-06 T-09 automatisch: Einen Startversuch mit aktivem Lesefehler an der Grenze
    /// <paramref name="grenze"/> durchlaufen und die grenzabhängigen Nachweise prüfen.
    /// </summary>
    /// <param name="fixture">Die Update-Fixture.</param>
    /// <param name="grenze">Die Lesegrenze (<c>Initial</c>, <c>BeforePreparation</c>, <c>BeforeUpdaterStart</c>).</param>
    /// <param name="ordinal">Die erwartete Ordnungszahl des blockierten Reads.</param>
    /// <param name="erwarteReleaseAbruf">Ob der Releaseabruf vor dem Lesefehler gelaufen ist.</param>
    /// <param name="erwarteVorbereitung">Ob die Vorbereitung vor dem Lesefehler abgeschlossen war.</param>
    /// <param name="erwarteDialogFehler">Ob der Fehler im Fortschrittsdialog statt im Hinweistext erscheint.</param>
    /// <returns>Das neue Hauptfenster und das Navigationsmenü.</returns>
    private async Task<(Window MainWindow, MenuView Menu)> T09StartupPhaseAsync(
        UpdateE2EFixture fixture, string grenze, int ordinal,
        bool erwarteReleaseAbruf, bool erwarteVorbereitung, bool erwarteDialogFehler)
    {
        var (mainWindow, basis) = await LesefehlerStartupVarianteStartenAsync(fixture, grenze, ordinal);
        var menu = new MenuView(mainWindow);

        if (erwarteVorbereitung)
        {
            var letzterRead = EintraegeSeit(fixture, basis)
                .Last(e => e.Ereignis == UpdateE2EEreignisse.UpdateSettingsReadReached
                    && DatenText(e, "versuch") == "startup" && DatenZahl(e, "ordinal") == ordinal);
            Assert.True(EintraegeSeit(fixture, basis)
                    .Single(e => e.Ereignis == UpdateE2EEreignisse.PreparationCompleted).Seq < letzterRead.Seq,
                "Der Read an der Grenze muss erst nach PreparationCompleted erfolgen.");
        }

        await WarteAufEreignisSeitAsync(fixture, basis, UpdateE2EEreignisse.StartupUpdateCompleted, Long);

        if (erwarteDialogFehler)
        {
            var fortschritt = new UpdateProgressDialogView(mainWindow);
            Assert.Contains(UpdateHinweisLesefehler, fortschritt.WaitForErrorState(Medium));
            fortschritt.Close();
        }
        else
        {
            menu.WaitForUpdateHinweis(UpdateHinweisLesefehler, Medium);
        }

        if (!erwarteReleaseAbruf)
        {
            Assert.Equal(0, ZaehleEreignisSeit(fixture, basis, UpdateE2EEreignisse.HttpRequest));
            Assert.Equal(0, ZaehleEreignisSeit(fixture, basis, UpdateE2EEreignisse.UpdateCheckStarted));
        }
        else
        {
            Assert.Equal(1, ZaehleEreignisSeit(fixture, basis, UpdateE2EEreignisse.HttpRequest,
                e => DatenText(e, "url") == UpdateE2EFixture.ReleaseApiUrl));
            // BeforeUpdaterStart: die Vorbereitung inkl. Asset-Abruf läuft vollständig,
            // erst der letzte Read vor dem Updater-Start schlägt fehl.
            Assert.Equal(erwarteVorbereitung ? 1 : 0,
                ZaehleEreignisSeit(fixture, basis, UpdateE2EEreignisse.HttpRequest,
                    e => DatenText(e, "art") == "asset"));
        }

        AssertKeinUpdateNebenwirkungSeit(fixture, basis, erlaubeVorbereitung: erwarteVorbereitung);
        await NachLesefehlerFortsetzenAsync(fixture, menu);
        return (mainWindow, menu);
    }

    /// <summary>
    /// E-06 T-09 manuell, Grenze Initial beim Prüfversuch: Der markierte Read wird blockiert,
    /// freigegeben und schlägt fehl - kein Releaseabruf, Hinweis sichtbar, danach
    /// Wiederherstellung über die Settings-UI.
    /// </summary>
    private async Task<MenuView> T09ManuellInitialPhaseAsync(UpdateE2EFixture fixture, MenuView menu)
    {
        fixture.AktiviereLesefehler("Initial", versuch: "pruefen");
        var basis = ProtokollBasis(fixture);
        menu.ClickUpdatePruefen();
        await WarteAufEreignisSeitAsync(fixture, basis, UpdateE2EEreignisse.UpdateSettingsReadReached, Long,
            filter: e => DatenText(e, "versuch") == "pruefen" && DatenZahl(e, "ordinal") == 1);
        fixture.OeffneGate(UpdateE2ETestKontext.LesefehlerGateName);
        await WarteAufEreignisSeitAsync(fixture, basis, UpdateE2EEreignisse.UpdateSettingsReadFailed, Long,
            filter: e => DatenText(e, "versuch") == "pruefen" && DatenText(e, "grenze") == "Initial");
        await WarteAufEreignisSeitAsync(fixture, basis, UpdateE2EEreignisse.UpdateAttemptCompleted, Long,
            filter: e => DatenText(e, "versuch") == "pruefen");
        menu.WaitForUpdateHinweis(UpdateHinweisLesefehler, Medium);
        Assert.Equal(0, ZaehleEreignisSeit(fixture, basis, UpdateE2EEreignisse.HttpRequest));
        AssertKeinUpdateNebenwirkungSeit(fixture, basis);
        fixture.SetzeGateZurueck(UpdateE2ETestKontext.LesefehlerGateName);
        fixture.DeaktiviereLesefehler();
        return await NachLesefehlerFortsetzenAsync(fixture, menu);
    }

    /// <summary>
    /// E-06 T-09 manuell beim Startversuch: Nach erfolgreicher Prüfung wird der Startversuch
    /// an der Lesegrenze <paramref name="grenze"/> blockiert und schlägt fehl - der
    /// Fortschrittsdialog zeigt den Lesefehler, ohne unerlaubte Folgeschritte.
    /// </summary>
    /// <param name="fixture">Die Update-Fixture.</param>
    /// <param name="menu">Das Navigationsmenü.</param>
    /// <param name="grenze">Die Lesegrenze (<c>BeforePreparation</c>, <c>BeforeUpdaterStart</c>).</param>
    /// <param name="ordinal">Die erwartete Ordnungszahl des blockierten Reads.</param>
    /// <param name="erwarteVorbereitung">Ob die Vorbereitung vor dem Lesefehler abgeschlossen war.</param>
    private async Task T09ManuellStartenPhaseAsync(
        UpdateE2EFixture fixture, MenuView menu, string grenze, int ordinal, bool erwarteVorbereitung)
    {
        fixture.AktiviereLesefehler(grenze, versuch: "starten");
        var basis = ProtokollBasis(fixture);
        menu.ClickUpdatePruefen();
        await WarteAufEreignisSeitAsync(fixture, basis, UpdateE2EEreignisse.UpdateAttemptCompleted, Long,
            filter: e => DatenText(e, "versuch") == "pruefen");
        menu.WaitForOfferedUpdateVersion(Medium);
        menu.ClickUpdateStarten();
        var letzterRead = (await WarteAufEreignisSeitAsync(fixture, basis,
            UpdateE2EEreignisse.UpdateSettingsReadReached, Long,
            filter: e => DatenText(e, "versuch") == "starten" && DatenZahl(e, "ordinal") == ordinal))[0];
        if (erwarteVorbereitung)
        {
            Assert.True(EintraegeSeit(fixture, basis)
                    .Single(e => e.Ereignis == UpdateE2EEreignisse.PreparationCompleted).Seq < letzterRead.Seq,
                "Der BeforeUpdaterStart-Read muss erst nach PreparationCompleted erfolgen.");
        }

        fixture.OeffneGate(UpdateE2ETestKontext.LesefehlerGateName);
        await WarteAufEreignisSeitAsync(fixture, basis, UpdateE2EEreignisse.UpdateSettingsReadFailed, Long,
            filter: e => DatenText(e, "versuch") == "starten" && DatenText(e, "grenze") == grenze);
        await WarteAufEreignisSeitAsync(fixture, basis, UpdateE2EEreignisse.UpdateAttemptCompleted, Long,
            filter: e => DatenText(e, "versuch") == "starten");
        var fortschritt = new UpdateProgressDialogView(menu.Window);
        Assert.Contains(UpdateHinweisLesefehler, fortschritt.WaitForErrorState(Medium));
        fortschritt.Close();
        if (!erwarteVorbereitung)
            Assert.Equal(0, ZaehleEreignisSeit(fixture, basis, UpdateE2EEreignisse.HttpRequest,
                e => DatenText(e, "art") == "asset"));

        Assert.Equal(erwarteVorbereitung ? 1 : 0,
            ZaehleEreignisSeit(fixture, basis, UpdateE2EEreignisse.PreparationCompleted));
        AssertKeinUpdateNebenwirkungSeit(fixture, basis, erlaubeVorbereitung: erwarteVorbereitung);
        fixture.SetzeGateZurueck(UpdateE2ETestKontext.LesefehlerGateName);
        fixture.DeaktiviereLesefehler();
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
        await fixture.SetzeUpdateEinstellungenAsync(UpdateMode.Aus, UpdatePfadKanalResolver.ProduktivversionenPfad);

        // Die Protokoll-Basis muss VOR dem Start gezogen werden: Der komplette Startlauf
        // (inkl. StartupUpdateCompleted) kann bereits während des Fenster-Wartens
        // protokolliert sein, bevor GetMainWindow zurückkehrt.
        var basis = ProtokollBasis(fixture);
        var app = LaunchApp(ensureDatabaseDeleted: false, fixture.ErzeugeStartUmgebung());
        Window? mainWindow = null;
        try
        {
            mainWindow = WarteAufEchtesHauptfenster(app);
            var menu = new MenuView(mainWindow);
            await WarteAufEreignisSeitAsync(fixture, basis, UpdateE2EEreignisse.StartupUpdateCompleted, Long);
            await SpeichereUpdateEinstellungenUeberUiAsync(fixture, menu, UpdateModusStartLabel, UpdatePfadProduktivLabel);

            fixture.AktualisiereSzenario(s =>
            {
                var paket = s.Antworten.Single(a => a.Url == fixture.StableDownloadUrl);
                paket.StreamGate = UpdateDownloadGate;
                paket.StreamGateBytes = 128;
            });
            basis = ProtokollBasis(fixture);
            mainWindow = RestartAppPreservingDatabase();
            menu = new MenuView(mainWindow);
            await WarteAufEreignisSeitAsync(fixture, basis, UpdateE2EEreignisse.HttpRequestBlocked, Long,
                filter: e => DatenText(e, "gate") == UpdateDownloadGate);

            var fortschritt = new UpdateProgressDialogView(mainWindow);
            Assert.True(fortschritt.IsVisible);
            WarteAufUiZustand(() => fortschritt.GetPhase() == "Download", "Download-Phase", Medium);

            // Gesperrte Commands: Prüfen deaktiviert, Installieren sichtbar aber deaktiviert.
            WarteAufUiZustand(() => !menu.IsUpdatePruefenButtonEnabled(),
                "Prüfen während blockierter Startautomatik gesperrt", Medium);
            Assert.True(menu.IsUpdateStartButtonVisible());
            Assert.False(menu.IsUpdateStartButtonEnabled());

            // Versuchte UI-Aktivierung erzeugt keinen zweiten Ablauf und keinen Shutdown.
            var pruefenButton = mainWindow.FindFirstDescendant(
                cf => cf.ByName("Programmupdate prüfen").And(cf.ByControlType(ControlType.Button)));
            pruefenButton?.AsButton().ClickInForeground();
            var startenButton = mainWindow.FindFirstDescendant(
                cf => cf.ByName("Programmupdate starten").And(cf.ByControlType(ControlType.Button)));
            startenButton?.AsButton().ClickInForeground();
            Assert.Equal(1, ZaehleEreignisSeit(fixture, basis, UpdateE2EEreignisse.UpdateAttemptStarted));
            Assert.Equal(0, ZaehleEreignisSeit(fixture, basis, UpdateE2EEreignisse.ShutdownRequested));

            // Freigabe: genau eine Vorbereitung, eine Übergabe, ein Shutdown.
            fixture.OeffneGate(UpdateDownloadGate);
            await WarteAufEreignisSeitAsync(fixture, basis, UpdateE2EEreignisse.StartupUpdateCompleted, Long);
            var eintraege = EintraegeSeit(fixture, basis);
            Assert.Single(eintraege, e => e.Ereignis == UpdateE2EEreignisse.UpdateAttemptStarted);
            Assert.Single(eintraege, e => e.Ereignis == UpdateE2EEreignisse.UpdateAttemptCompleted);
            AssertiereInstallationsNachweis(fixture, basis,
                UpdateE2EFixture.StableVersion, fixture.StableDownloadUrl, "stable-marker");
            fortschritt.Close();
            fixture.SetzeGateZurueck(UpdateDownloadGate);

            // Offen gehaltenes Fenster: Navigation und erneutes Rendern ändern die Zähler nicht.
            var nachBasis = ProtokollBasis(fixture);
            var settings = await OeffneSettingsGeladenAsync(fixture, menu);
            Assert.Equal(UpdateModusStartLabel, settings.GetUpdateMode());
            menu.NavigateToDashboard();
            menu.NavigateToProjects();
            menu.NavigateToDashboard();
            Assert.Equal(0, ZaehleEreignisSeit(fixture, nachBasis, UpdateE2EEreignisse.UpdateAttemptStarted));
            Assert.Equal(0, ZaehleEreignisSeit(fixture, nachBasis, UpdateE2EEreignisse.HttpRequest));
            Assert.Equal(0, ZaehleEreignisSeit(fixture, nachBasis, UpdateE2EEreignisse.PreparationCompleted));
            Assert.Equal(0, ZaehleEreignisSeit(fixture, nachBasis, UpdateE2EEreignisse.ShutdownRequested));
        }
        finally
        {
            await SetzeUpdateSteuerungZurueckAsync(fixture, mainWindow);
            SchliesseFixtureApp();
        }
    }

    /// <summary>
    /// Startet einen T-09-Startup-Lesefehler-Fall: Fehler aktivieren, mit gleicher DB neu
    /// starten, auf den blockierten markierten Read warten und das Gate freigeben.
    /// </summary>
    /// <param name="fixture">Die Update-Fixture.</param>
    /// <param name="grenze">Die Lesegrenze (<c>Initial</c>, <c>BeforePreparation</c>).</param>
    /// <param name="ordinal">Die erwartete Ordnungszahl des blockierten Reads.</param>
    /// <returns>Das neue Hauptfenster und die Protokoll-Basis der Phase.</returns>
    private async Task<(Window MainWindow, int Basis)> LesefehlerStartupVarianteStartenAsync(
        UpdateE2EFixture fixture, string grenze, int ordinal)
    {
        fixture.AktiviereLesefehler(grenze, versuch: "startup");
        var basis = ProtokollBasis(fixture);
        var mainWindow = RestartAppPreservingDatabase();
        await WarteAufEreignisSeitAsync(fixture, basis, UpdateE2EEreignisse.UpdateSettingsReadReached, Long,
            filter: e => DatenText(e, "versuch") == "startup" && DatenZahl(e, "ordinal") == ordinal);
        fixture.OeffneGate(UpdateE2ETestKontext.LesefehlerGateName);
        await WarteAufEreignisSeitAsync(fixture, basis, UpdateE2EEreignisse.UpdateSettingsReadFailed, Long,
            filter: e => DatenText(e, "versuch") == "startup" && DatenText(e, "grenze") == grenze);
        return (mainWindow, basis);
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
    private async Task<MenuView> NachLesefehlerFortsetzenAsync(UpdateE2EFixture fixture, MenuView menu)
    {
        fixture.SetzeGateZurueck(UpdateE2ETestKontext.LesefehlerGateName);
        fixture.DeaktiviereLesefehler();
        Assert.True(fixture.ZaehleEreignis(UpdateE2EEreignisse.UpdateSettingsReadFailureDisabled) >= 1);

        var settings = await OeffneSettingsGeladenAsync(fixture, menu);
        Assert.Equal(UpdateModusStartLabel, settings.GetUpdateMode());
        settings.SaveSettings();
        WarteAufSpeichernAktiviert(settings);
        menu.NavigateToDashboard();

        var basis = ProtokollBasis(fixture);
        WarteAufUiZustand(() => menu.IsUpdatePruefenButtonEnabled(),
            "Prüfen nach Einstellungs-Speichern aktiviert", Medium);
        menu.ClickUpdatePruefen();
        await WarteAufEreignisSeitAsync(fixture, basis, UpdateE2EEreignisse.UpdateAttemptCompleted, Long,
            filter: e => DatenText(e, "versuch") == "pruefen");
        Assert.Equal(UpdateE2EFixture.StableVersion, menu.WaitForOfferedUpdateVersion(Medium));
        AssertKeinUpdateNebenwirkungSeit(fixture, basis);
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
    private async Task<SettingsView> OeffneSettingsGeladenAsync(UpdateE2EFixture fixture, MenuView menu)
    {
        var readsVorher = fixture.ZaehleEreignis(UpdateE2EEreignisse.UpdateSettingsReadReached);
        var settings = menu.NavigateToSettings();
        await fixture.WarteAufEreignisAsync(UpdateE2EEreignisse.UpdateSettingsReadReached, Medium,
            mindestens: readsVorher + 1);
        WarteAufSpeichernAktiviert(settings);
        return settings;
    }

    /// <summary>
    /// Setzt Update-Modus und Update-Pfad über die echte Settings-UI, speichert und
    /// navigiert zurück zum Dashboard.
    /// </summary>
    /// <param name="fixture">Die Update-Fixture.</param>
    /// <param name="menu">Das Navigationsmenü.</param>
    /// <param name="modusLabel">Das exakte Update-Modus-Label.</param>
    /// <param name="pfadLabel">Das zu setzende Update-Pfad-Label.</param>
    private async Task SpeichereUpdateEinstellungenUeberUiAsync(
        UpdateE2EFixture fixture, MenuView menu, string modusLabel, string pfadLabel)
    {
        var settings = await OeffneSettingsGeladenAsync(fixture, menu);
        settings.SetUpdateMode(modusLabel);
        settings.SetUpdatePfad(pfadLabel);
        settings.SaveSettings();
        WarteAufSpeichernAktiviert(settings);
        menu.NavigateToDashboard();
    }

    /// <summary>
    /// Klickt "Programmupdate prüfen" und wartet auf den Abschluss des Prüfversuchs; prüft
    /// anschließend Angebot und CheckCompleted-Protokolleintrag für die erwartete Version.
    /// </summary>
    private async Task PruefeUpdateUeberUiAsync(
        UpdateE2EFixture fixture, MenuView menu, int basis, string erwarteteVersion,
        bool isPrerelease, string erwarteterPfad)
    {
        WarteAufUiZustand(() => menu.IsUpdatePruefenButtonEnabled(), "Prüfen-Button aktiviert", Medium);
        menu.ClickUpdatePruefen();
        await WarteAufEreignisSeitAsync(fixture, basis, UpdateE2EEreignisse.UpdateAttemptCompleted, Long,
            filter: e => DatenText(e, "versuch") == "pruefen");
        Assert.Equal(erwarteteVersion, menu.WaitForOfferedUpdateVersion(Medium));
        var erwarteterKanal = erwarteterPfad == UpdatePfadVorabLabel
            ? nameof(UpdateKanal.ReleaseCandidate)
            : nameof(UpdateKanal.Produktiv);
        Assert.Contains(EintraegeSeit(fixture, basis), e => e.Ereignis == UpdateE2EEreignisse.UpdateCheckStarted
            && DatenText(e, "updatePfad") == erwarteterPfad
            && DatenText(e, "kanal") == erwarteterKanal);
        Assert.Contains(EintraegeSeit(fixture, basis), e => e.Ereignis == UpdateE2EEreignisse.UpdateCheckCompleted
            && DatenText(e, "status") == "UpdateVerfuegbar"
            && DatenText(e, "version") == erwarteteVersion
            && DatenBool(e, "isPrerelease") == isPrerelease);
    }

    /// <summary>
    /// Prüft, dass die Einstellungen bedienbar bleiben: öffnen, Update-Modus (Startmodus)
    /// wird angezeigt, zurück zum Dashboard.
    /// </summary>
    private async Task PruefeSettingsBedienbarAsync(UpdateE2EFixture fixture, MenuView menu)
    {
        var settings = await OeffneSettingsGeladenAsync(fixture, menu);
        Assert.Equal(UpdateModusStartLabel, settings.GetUpdateMode());
        menu.NavigateToDashboard();
    }

    /// <summary>
    /// Nachweis einer echten Installation bis zur kontrollierten Launcher-/Shutdown-Grenze:
    /// Asset-Abrufe genau auf den erwarteten URLs (primäre Download-URL, optional weitere
    /// wie die API-Asset-Fallback-URL), entpacktes Paket mit passender <c>version.json</c>
    /// und Markerinhalt, Skript mit <c>-ExtractedDirectory</c> auf das entpackte Verzeichnis
    /// der Version, genau ein Launcher-Erfolg vor genau einem Shutdown.
    /// </summary>
    private static void AssertiereInstallationsNachweis(
        UpdateE2EFixture fixture, int basis, string erwarteteVersion, string erwarteteDownloadUrl,
        string erwarteterMarker, IReadOnlyList<string>? weitereErwarteteAssetUrls = null)
    {
        var eintraege = EintraegeSeit(fixture, basis);

        var erwarteteAssetUrls = new List<string?> { erwarteteDownloadUrl };
        if (weitereErwarteteAssetUrls is not null)
            erwarteteAssetUrls.AddRange(weitereErwarteteAssetUrls);
        Assert.Equal(erwarteteAssetUrls, eintraege
            .Where(e => e.Ereignis == UpdateE2EEreignisse.HttpRequest && DatenText(e, "art") == "asset")
            .Select(e => DatenText(e, "url"))
            .ToList());

        var vorbereitung = Assert.Single(eintraege,
            e => e.Ereignis == UpdateE2EEreignisse.PreparationCompleted);
        var extractedDirectory = DatenText(vorbereitung, "extractedDirectory");
        Assert.NotNull(extractedDirectory);
        Assert.Equal(erwarteteVersion,
            Path.GetFileName(extractedDirectory!.TrimEnd(Path.DirectorySeparatorChar)));
        var versionJson = File.ReadAllText(Path.Combine(extractedDirectory, "version.json"));
        Assert.Contains($"\"version\":\"{erwarteteVersion}\"", versionJson);
        Assert.Equal($"E2E-Paket {erwarteteVersion} ({erwarteterMarker})",
            UpdateE2EFixture.LesePaketExeInhalt(extractedDirectory));
        var scriptPfad = DatenText(vorbereitung, "scriptPath");
        Assert.NotNull(scriptPfad);
        Assert.True(File.Exists(scriptPfad), $"Update-Skript fehlt: {scriptPfad}");

        var launcher = Assert.Single(eintraege,
            e => e.Ereignis == UpdateE2EEreignisse.UpdateProcessStartRecorded);
        Assert.Equal("Erfolg", DatenText(launcher, "result"));
        Assert.Equal(fixture.Testwurzel, DatenText(launcher, "workingDirectory"));
        var argumente = UpdateE2EFixture.Daten(launcher) is { } launcherDaten
            && launcherDaten.TryGetProperty("arguments", out var argsElement)
                ? argsElement.EnumerateArray().Select(a => a.GetString()).ToList()
                : [];
        var dirIndex = argumente.IndexOf("-ExtractedDirectory");
        Assert.True(dirIndex >= 0 && dirIndex + 1 < argumente.Count,
            "Launcher-Argumente enthalten kein -ExtractedDirectory.");
        Assert.Equal(extractedDirectory, argumente[dirIndex + 1]);

        Assert.Single(eintraege, e => e.Ereignis == UpdateE2EEreignisse.UpdateStartAttempt);
        Assert.Single(eintraege, e => e.Ereignis == UpdateE2EEreignisse.UpdateStartSucceeded);
        var shutdown = Assert.Single(eintraege, e => e.Ereignis == UpdateE2EEreignisse.ShutdownRequested);
        Assert.True(shutdown.Seq > launcher.Seq,
            "ShutdownRequested muss nach dem aufgezeichneten Launcher-Start protokolliert sein.");
    }

    /// <summary>
    /// Prüft, dass seit <paramref name="basis"/> keine Update-Nebenwirkungen auftraten.
    /// Mit <paramref name="erlaubeVorbereitung"/> wird ein abgeschlossenes Paket-
    /// PreparationCompleted toleriert (BeforeUpdaterStart-Grenze: die Vorbereitung
    /// läuft, nur der Updater-Start selbst wird geblockt).
    /// </summary>
    private static void AssertKeinUpdateNebenwirkungSeit(
        UpdateE2EFixture fixture, int basis, bool erlaubeVorbereitung = false)
    {
        var eintraege = EintraegeSeit(fixture, basis);
        if (!erlaubeVorbereitung)
            Assert.DoesNotContain(eintraege, e => e.Ereignis == UpdateE2EEreignisse.PreparationCompleted);
        Assert.DoesNotContain(eintraege, e => e.Ereignis == UpdateE2EEreignisse.UpdateStartAttempt);
        Assert.DoesNotContain(eintraege, e => e.Ereignis == UpdateE2EEreignisse.UpdateProcessStartRecorded);
        Assert.DoesNotContain(eintraege, e => e.Ereignis == UpdateE2EEreignisse.ShutdownRequested);
    }

    /// <summary>Anzahl bisher geschriebener Protokolleinträge (Phasengrenze im JSONL).</summary>
    private static int ProtokollBasis(UpdateE2EFixture fixture)
        => fixture.LeseProtokoll().Count;

    /// <summary>Protokolleinträge seit der Phasengrenze <paramref name="basis"/>.</summary>
    private static List<UpdateE2EProtokollEintrag> EintraegeSeit(UpdateE2EFixture fixture, int basis)
        => fixture.LeseProtokoll().Skip(basis).ToList();

    /// <summary>Anzahl der Protokolleinträge eines Ereignisnamens seit der Phasengrenze.</summary>
    private static int ZaehleEreignisSeit(
        UpdateE2EFixture fixture, int basis, string ereignis,
        Func<UpdateE2EProtokollEintrag, bool>? filter = null)
        => EintraegeSeit(fixture, basis).Count(e => e.Ereignis == ereignis && (filter?.Invoke(e) ?? true));

    /// <summary>Wartet auf ein Protokollereignis seit der Phasengrenze.</summary>
    private async Task<IReadOnlyList<UpdateE2EProtokollEintrag>> WarteAufEreignisSeitAsync(
        UpdateE2EFixture fixture, int basis, string ereignis, TimeSpan timeout,
        int mindestens = 1, Func<UpdateE2EProtokollEintrag, bool>? filter = null)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var treffer = EintraegeSeit(fixture, basis)
                .Where(e => e.Ereignis == ereignis && (filter?.Invoke(e) ?? true))
                .ToList();
            if (treffer.Count >= mindestens)
                return treffer;

            await Task.Delay(100);
        }

        throw new TimeoutException(
            $"Protokollereignis '{ereignis}' (mindestens {mindestens}) seit Basis {basis} erschien nicht " +
            $"innerhalb von {timeout.TotalSeconds}s. Ereignisse seit Basis: " +
            $"{string.Join(", ", EintraegeSeit(fixture, basis).Select(e => e.Ereignis).Distinct())}");
    }

    /// <summary>Wartet begrenzt auf einen UI-Zustand (gebundene Waits statt fester Sleeps).</summary>
    private static void WarteAufUiZustand(Func<bool> bedingung, string beschreibung, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (bedingung())
                return;

            Thread.Sleep(150);
        }

        throw new TimeoutException($"UI-Zustand '{beschreibung}' nicht innerhalb von {timeout.TotalSeconds}s erreicht.");
    }

    /// <summary>
    /// Wartet, bis der "Speichern"-Ribbon-Button wieder aktiviert ist. Während
    /// <c>LadenAsync</c>/<c>SpeichernAsync</c> ist er über das AsyncRelayCommand deaktiviert.
    /// </summary>
    private void WarteAufSpeichernAktiviert(SettingsView settings)
        => WarteAufUiZustand(
            () => settings.Window.FindFirstDescendant(cf => cf.ByName("Speichern"))?.IsEnabled == true,
            "Speichern-Button aktiviert", Medium);

    /// <summary>
    /// Führt eine Aufräumaktion aus und schluckt jeden Fehler: Im Cleanup-Pfad darf ein
    /// bereits teilweise entsorgtes Fixture oder eine beendete App die restliche
    /// Aufräumsequenz nicht maskieren.
    /// </summary>
    private static void BestEffort(Action aktion)
    {
        try
        {
            aktion();
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
    private async Task SetzeUpdateSteuerungZurueckAsync(UpdateE2EFixture fixture, Window? mainWindow)
    {
        BestEffort(() => fixture.OeffneGate(UpdateDownloadGate));
        BestEffort(() => fixture.OeffneGate(UpdateReleaseGate));
        BestEffort(() => fixture.OeffneGate(UpdateE2ETestKontext.LesefehlerGateName));
        BestEffort(fixture.DeaktiviereLesefehler);

        if (mainWindow is not null)
        {
            BestEffort(() =>
            {
                var sicherheit = new UpdateSafetyDialogView(mainWindow);
                if (sicherheit.IsVisible)
                    sicherheit.Cancel();
            });
        }

        // Auf den Abschluss eines ggf. freigegebenen laufenden Versuchs warten.
        var deadline = DateTime.UtcNow + Medium;
        while (DateTime.UtcNow < deadline)
        {
            var eintraege = fixture.LeseProtokoll();
            if (eintraege.Count(e => e.Ereignis == UpdateE2EEreignisse.UpdateAttemptStarted)
                <= eintraege.Count(e => e.Ereignis == UpdateE2EEreignisse.UpdateAttemptCompleted))
                break;

            await Task.Delay(200);
        }

        if (mainWindow is not null)
        {
            BestEffort(() =>
            {
                var fortschritt = new UpdateProgressDialogView(mainWindow);
                if (fortschritt.IsVisible)
                    fortschritt.Close();
            });
        }

        BestEffort(() => fixture.SetzeGateZurueck(UpdateDownloadGate));
        BestEffort(() => fixture.SetzeGateZurueck(UpdateReleaseGate));
        BestEffort(() => fixture.SetzeGateZurueck(UpdateE2ETestKontext.LesefehlerGateName));
    }

    /// <summary>Erzeugt eine Release-Liste mit dem RC-Paket und einer semantisch höheren stabilen Version.</summary>
    private static string ErzeugeRcUndHoeheresStableReleaseJson(UpdateE2EFixture fixture)
    {
        var releases = new[]
        {
            UpdateE2EFixture.ErstelleReleaseJson(
                UpdateE2EFixture.RcVersion,
                prerelease: true,
                DateTimeOffset.UtcNow,
                UpdateE2EFixture.ErstelleAssetJson(
                    UpdateE2EFixture.RcAssetApiUrl, UpdateE2EFixture.RcAssetId, fixture.RcDownloadUrl)),
            UpdateE2EFixture.ErstelleReleaseJson(
                "9.9.9",
                prerelease: false,
                DateTimeOffset.UtcNow,
                UpdateE2EFixture.ErstelleAssetJson(
                    UpdateE2EFixture.AssetApiUrl(91999), 91999L, "https://e2e.invalid/unused-stable.zip"))
        };
        return JsonSerializer.Serialize(releases);
    }

    /// <summary>Erzeugt eine Release-Listen-JSON mit den angegebenen Stable-Versionen.</summary>
    private static string ErzeugeReleaseListenJson(UpdateE2EFixture fixture, params string[] versionen)
    {
        var releases = versionen.Select(version => UpdateE2EFixture.ErstelleReleaseJson(
            version,
            prerelease: false,
            DateTimeOffset.UtcNow.AddDays(-7),
            UpdateE2EFixture.ErstelleAssetJson(
                UpdateE2EFixture.StableAssetApiUrl, UpdateE2EFixture.StableAssetId, fixture.StableDownloadUrl)));
        return JsonSerializer.Serialize(releases);
    }
}
