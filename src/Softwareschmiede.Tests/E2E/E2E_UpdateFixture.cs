using System.Diagnostics;
using System.Text.Json;
using FlaUI.Core.AutomationElements;
using Softwareschmiede.App.Services.Testing;
using Softwareschmiede.Application.Services.Updates;
using Softwareschmiede.Tests.E2E.Views;
using Softwareschmiede.Tests.E2E.Views.Dialogs;

namespace Softwareschmiede.Tests.E2E;

public partial class End2EndTest
{
    private const string UpdateHinweisLesefehler = "konnten nicht gelesen";

    /// <summary>
    /// Fixture-Smoke 1 (Plan: <c>Fixture_UsesIsolatedRealUpdatePipeline</c>): Echte JSON- und
    /// ZIP-Pipeline über den Fixture-HttpMessageHandler, isolierte Testpfade, Stable-/RC-Auswahl,
    /// kontrollierte Launcher-/Shutdown-Grenze, DB-erhaltender Neustart und der Nachweis, dass
    /// unbekannte Requests ohne Netzwerkfallback scheitern.
    /// </summary>
    private async Task Fixture_UsesIsolatedRealUpdatePipeline()
    {
        using var fixture = new UpdateE2EFixture();
        await fixture.SetzeUpdateEinstellungenAsync(UpdateMode.BeiProgrammstartPruefenUndAusfuehren, includePrereleases: false);

        var app = LaunchApp(ensureDatabaseDeleted: false, fixture.ErzeugeStartUmgebung());
        try
        {
            var mainWindow = WarteAufEchtesHauptfenster(app);
            var menu = new MenuView(mainWindow);

            // Startautomatik bis zur kontrollierten Updater-Prozessgrenze abwarten.
            await fixture.WarteAufEreignisAsync(UpdateE2EEreignisse.StartupUpdateCompleted, Long);
            var protokoll = fixture.LeseProtokoll();

            // Reihenfolge der Basis-Marker: DB-Initialisierung vor WindowReady vor Versuchsbeginn.
            AssertSequenz(protokoll,
                UpdateE2EEreignisse.DatabaseInitializationCompleted,
                UpdateE2EEreignisse.WindowReady,
                UpdateE2EEreignisse.UpdateAttemptStarted);

            // Echte JSON-Pipeline: Release-Abruf traf den Fixture-Handler, Auswahl = Stable 1.3.0.
            Assert.Contains(protokoll, e => e.Ereignis == UpdateE2EEreignisse.HttpRequest
                && DatenText(e, "url") == UpdateE2EFixture.ReleaseApiUrl);
            Assert.Contains(protokoll, e => e.Ereignis == UpdateE2EEreignisse.UpdateCheckCompleted
                && DatenText(e, "status") == "UpdateVerfuegbar"
                && DatenText(e, "version") == UpdateE2EFixture.StableVersion
                && DatenBool(e, "isPrerelease") == false);

            // Echte ZIP-Pipeline: Download/Entpacken/Skripterzeugung liefen real (Phasen protokolliert).
            Assert.True(protokoll.Count(e => e.Ereignis == UpdateE2EEreignisse.PreparationPhase) >= 3,
                "Mindestens drei Vorbereitungsphasen (Download, Entpacken, UpdateVorbereiten) erwartet.");
            var vorbereitung = Assert.Single(protokoll, e => e.Ereignis == UpdateE2EEreignisse.PreparationCompleted);
            var scriptPfad = DatenText(vorbereitung, "scriptPath");
            Assert.NotNull(scriptPfad);
            Assert.StartsWith(Path.GetFullPath(fixture.Testwurzel), Path.GetFullPath(scriptPfad!));

            // Kontrollierte Updater-Prozessgrenze: Launcher-Record statt echtem Prozess.
            var launcher = Assert.Single(protokoll, e => e.Ereignis == UpdateE2EEreignisse.UpdateProcessStartRecorded);
            Assert.Equal(fixture.Testwurzel, DatenText(launcher, "workingDirectory"));
            Assert.Equal(false, DatenBool(launcher, "runElevated"));
            Assert.Equal("Erfolg", DatenText(launcher, "result"));
            Assert.Contains(protokoll, e => e.Ereignis == UpdateE2EEreignisse.UpdateStartSucceeded);
            Assert.Single(protokoll, e => e.Ereignis == UpdateE2EEreignisse.ShutdownRequested);

            // Testpfade: Update-Artefakte liegen ausschließlich unterhalb der Fixture-Wurzel.
            var zipPfad = Path.Combine(fixture.Testwurzel, "updates", "download", "release.zip");
            Assert.True(File.Exists(zipPfad), $"Heruntergeladenes Paket fehlt: {zipPfad}");
            var entpackt = Path.Combine(fixture.Testwurzel, "updates", "extracted", UpdateE2EFixture.StableVersion);
            Assert.Equal("E2E-Paket 1.3.0 (stable-marker)", UpdateE2EFixture.LesePaketExeInhalt(entpackt));
            Assert.True(File.Exists(Path.Combine(entpackt, "version.json")), "Entpacktes version.json fehlt.");
            Assert.True(File.Exists(scriptPfad), $"Update-Skript fehlt: {scriptPfad}");

            // Kein Schreibzugriff auf das App-Programmverzeichnis (Fixture-Isolation).
            var appVerzeichnis = Path.GetDirectoryName(ErmittleAppExePfadDesLaufendenProzesses());
            Assert.NotNull(appVerzeichnis);
            Assert.False(Directory.Exists(Path.Combine(appVerzeichnis!, "updates")),
                "Im App-Programmverzeichnis wurde ein updates/-Ordner erzeugt - Isolation verletzt.");

            // Unbekannter Request scheitert ohne Netzwerkfallback.
            var httpVorher = fixture.ZaehleEreignis(UpdateE2EEreignisse.HttpResponse);
            fixture.AktualisiereSzenario(s => s.Antworten.Clear());
            menu.ClickUpdatePruefen();
            await fixture.WarteAufEreignisAsync(UpdateE2EEreignisse.UnknownHttpRequest, Medium);
            await fixture.WarteAufEreignisAsync(UpdateE2EEreignisse.UpdateCheckCompleted, Medium,
                filter: e => DatenText(e, "status") == "NichtPruefbar");
            Assert.Equal(httpVorher, fixture.ZaehleEreignis(UpdateE2EEreignisse.HttpResponse));

            // RC-Auswahl bei aktivierten Prereleases über echten, DB-erhaltenden Neustart.
            fixture.SetzeStandardAntworten();
            fixture.SpeichereSzenario();
            await fixture.AktualisiereUpdateEinstellungenAsync(UpdateMode.BeiProgrammstartPruefenUndAusfuehren, includePrereleases: true);
            mainWindow = RestartAppPreservingDatabase();
            await fixture.WarteAufEreignisAsync(UpdateE2EEreignisse.StartupUpdateCompleted, Long,
                mindestens: 2);

            var protokollNachRestart = fixture.LeseProtokoll();
            Assert.Contains(protokollNachRestart, e => e.Ereignis == UpdateE2EEreignisse.UpdateCheckCompleted
                && DatenText(e, "version") == UpdateE2EFixture.RcVersion
                && DatenBool(e, "isPrerelease") == true);
            Assert.Equal(2, protokollNachRestart.Count(e => e.Ereignis == UpdateE2EEreignisse.UpdateProcessStartRecorded));
            Assert.Equal(2, protokollNachRestart.Count(e => e.Ereignis == UpdateE2EEreignisse.ShutdownRequested));
            var entpacktRc = Path.Combine(fixture.Testwurzel, "updates", "extracted", UpdateE2EFixture.RcVersion);
            Assert.Equal($"E2E-Paket {UpdateE2EFixture.RcVersion} (rc-marker)", UpdateE2EFixture.LesePaketExeInhalt(entpacktRc));
        }
        finally
        {
            // Die Fixture-App explizit schließen: Der aufgezeichnete (nicht echte) Shutdown lässt
            // den Prozess samt Fortschrittsdialog offen, und LaunchApp schließt keine Vorgänger-
            // instanz - sonst bliebe der "Update vorbereiten"-Dialog als UIA-Geist für Folgetests.
            SchliesseFixtureApp();
        }
    }

    /// <summary>
    /// Fixture-Smoke 2 (Plan: <c>Fixture_SettingsReadFailureTargetsPhaseAfterDatabaseInitialization</c>):
    /// Alle drei Settings-Lesefehler-Grenzen (Initial, BeforePreparation, BeforeUpdaterStart) mit
    /// markierten Reads, begrenzter Gate-Freigabe/-Abbruch, Fehlerpersistenz bis zur Deaktivierung
    /// und Timeout-Cleanup. Die DB-Initialisierung und spätere erfolgreiche Reads bleiben funktionsfähig.
    /// </summary>
    private async Task Fixture_SettingsReadFailureTargetsPhaseAfterDatabaseInitialization()
    {
        using var fixture = new UpdateE2EFixture();
        await fixture.SetzeUpdateEinstellungenAsync(UpdateMode.NurPruefen, includePrereleases: false);

        // Der Lesefehler wird vor dem Start aktiviert; markierte Reads kommen frühestens nach
        // DatabaseInitializationCompleted im ersten Updateversuch vor (der erste getaggte Read
        // ist der des Startversuchs - Migration und ungetaggte Settings bleiben unberührt).
        fixture.AktiviereLesefehler("Initial", versuch: "startup");

        var app = LaunchApp(ensureDatabaseDeleted: false, fixture.ErzeugeStartUmgebung());
        var mainWindow = WarteAufEchtesHauptfenster(app);
        var menu = new MenuView(mainWindow);

        try
        {
            // Grenze Initial (Startversuch): markierter Read blockiert bis zur Freigabe.
            var erreicht = await fixture.WarteAufEreignisAsync(
                UpdateE2EEreignisse.UpdateSettingsReadReached, Medium,
                filter: e => DatenText(e, "versuch") == "startup" && DatenZahl(e, "ordinal") == 1);
            Assert.Equal("Initial", DatenText(erreicht[0], "grenze"));
            fixture.OeffneGate(UpdateE2ETestKontext.LesefehlerGateName);

            await fixture.WarteAufEreignisAsync(UpdateE2EEreignisse.UpdateSettingsReadFailed, Medium,
                filter: e => DatenText(e, "versuch") == "startup");
            await fixture.WarteAufEreignisAsync(UpdateE2EEreignisse.StartupUpdateCompleted, Medium);
            menu.WaitForUpdateHinweis(UpdateHinweisLesefehler, Medium);

            // Kein unerlaubter Folgeschritt: Der Releaseabruf durfte nicht stattfinden.
            Assert.Equal(0, fixture.ZaehleEreignis(UpdateE2EEreignisse.HttpRequest));
            fixture.SetzeGateZurueck(UpdateE2ETestKontext.LesefehlerGateName);
            fixture.DeaktiviereLesefehler();
            Assert.True(fixture.ZaehleEreignis(UpdateE2EEreignisse.UpdateSettingsReadFailureDisabled) >= 1);

            // Wiederlesen nach Deaktivierung gelingt: Das Speichern der Einstellungen über die
            // echte UI füllt den Einstellungs-Snapshot des Hauptfensters (danach ist "Programmupdate
            // prüfen" wieder aktiviert) und beweist, dass Settings-Reads/Schreiben funktionieren.
            var settingsView = menu.NavigateToSettings();
            settingsView.SaveSettings();
            menu.NavigateToDashboard();

            menu.ClickUpdatePruefen();
            Assert.Equal(UpdateE2EFixture.StableVersion, menu.WaitForOfferedUpdateVersion(Medium));

            // Grenze BeforePreparation (manueller Startversuch): erster Read und Releaseabruf
            // gelingen, der zweite markierte Read vor dem Download schlägt kontrolliert fehl.
            fixture.AktiviereLesefehler("BeforePreparation", versuch: "starten");
            menu.ClickUpdateStarten();
            await fixture.WarteAufEreignisAsync(
                UpdateE2EEreignisse.UpdateSettingsReadReached, Medium,
                filter: e => DatenText(e, "versuch") == "starten" && DatenZahl(e, "ordinal") == 2);
            fixture.OeffneGate(UpdateE2ETestKontext.LesefehlerGateName);
            await fixture.WarteAufEreignisAsync(UpdateE2EEreignisse.UpdateSettingsReadFailed, Medium,
                filter: e => DatenText(e, "versuch") == "starten" && DatenText(e, "grenze") == "BeforePreparation");

            var fortschritt = new UpdateProgressDialogView(mainWindow);
            Assert.Contains(UpdateHinweisLesefehler, fortschritt.WaitForErrorState(Medium));
            fortschritt.Close();
            Assert.Equal(0, fixture.ZaehleEreignis(UpdateE2EEreignisse.PreparationCompleted));
            fixture.SetzeGateZurueck(UpdateE2ETestKontext.LesefehlerGateName);
            fixture.DeaktiviereLesefehler();

            // Grenze BeforeUpdaterStart: Reads 1+2 und die komplette Vorbereitung gelingen;
            // erst der letzte markierte Read nach PreparationCompleted schlägt fehl.
            fixture.AktiviereLesefehler("BeforeUpdaterStart", versuch: "starten");
            menu.ClickUpdatePruefen();
            menu.WaitForOfferedUpdateVersion(Medium);
            menu.ClickUpdateStarten();
            var letzterRead = (await fixture.WarteAufEreignisAsync(
                UpdateE2EEreignisse.UpdateSettingsReadReached, Medium,
                filter: e => DatenText(e, "versuch") == "starten" && DatenZahl(e, "ordinal") == 3))[0];
            var vorbereitungAbgeschlossen = Assert.Single(
                fixture.LeseProtokoll(), e => e.Ereignis == UpdateE2EEreignisse.PreparationCompleted);
            Assert.True(vorbereitungAbgeschlossen.Seq < letzterRead.Seq,
                "Der BeforeUpdaterStart-Read muss erst nach PreparationCompleted erfolgen.");
            fixture.OeffneGate(UpdateE2ETestKontext.LesefehlerGateName);
            await fixture.WarteAufEreignisAsync(UpdateE2EEreignisse.UpdateSettingsReadFailed, Medium,
                filter: e => DatenText(e, "grenze") == "BeforeUpdaterStart");
            Assert.Contains(UpdateHinweisLesefehler, fortschritt.WaitForErrorState(Medium));
            fortschritt.Close();

            // Bis zur Installation durfte es nicht kommen: kein Updater-Start, kein Shutdown.
            Assert.Equal(0, fixture.ZaehleEreignis(UpdateE2EEreignisse.UpdateStartAttempt));
            Assert.Equal(0, fixture.ZaehleEreignis(UpdateE2EEreignisse.ShutdownRequested));
            fixture.SetzeGateZurueck(UpdateE2ETestKontext.LesefehlerGateName);

            // Einmal aktiviert, bleibt der Fehler bis zur expliziten Deaktivierung aktiv
            // (zwei aufeinanderfolgende markierte Reads desselben Versuchstyps scheitern).
            fixture.AktiviereLesefehler("Initial", versuch: "pruefen");
            var fehlerVorher = fixture.ZaehleEreignis(UpdateE2EEreignisse.UpdateSettingsReadFailed);
            for (var i = 0; i < 2; i++)
            {
                var reachedPruefenVorher = fixture.LeseProtokoll().Count(e =>
                    e.Ereignis == UpdateE2EEreignisse.UpdateSettingsReadReached
                    && DatenText(e, "versuch") == "pruefen" && DatenZahl(e, "ordinal") == 1);
                menu.ClickUpdatePruefen();
                await fixture.WarteAufEreignisAsync(
                    UpdateE2EEreignisse.UpdateSettingsReadReached, Medium,
                    mindestens: reachedPruefenVorher + 1,
                    filter: e => DatenText(e, "versuch") == "pruefen" && DatenZahl(e, "ordinal") == 1);
                if (i == 0)
                    fixture.BrecheGateAb(UpdateE2ETestKontext.LesefehlerGateName); // Abbruch-Pfad
                else
                    fixture.OeffneGate(UpdateE2ETestKontext.LesefehlerGateName); // Freigabe-Pfad
                await fixture.WarteAufEreignisAsync(UpdateE2EEreignisse.UpdateSettingsReadFailed, Medium,
                    mindestens: fehlerVorher + i + 1);
                fixture.SetzeGateZurueck(UpdateE2ETestKontext.LesefehlerGateName);
            }
            var abbruch = fixture.LeseProtokoll()
                .Where(e => e.Ereignis == UpdateE2EEreignisse.UpdateSettingsReadFailed)
                .ToList();
            Assert.Contains(abbruch, e => DatenText(e, "grund") == "abgebrochen");
            Assert.Contains(abbruch, e => DatenText(e, "grund") == "freigegeben");

            // Timeout-Cleanup: Ohne Gate-Datei schlägt die begrenzte Wartezeit fehl.
            fixture.AktiviereLesefehler("Initial", versuch: "pruefen", wartezeitSekunden: 1);
            menu.ClickUpdatePruefen();
            await fixture.WarteAufEreignisAsync(UpdateE2EEreignisse.UpdateSettingsReadFailed, Medium,
                filter: e => DatenText(e, "grund") == "timeout");
            fixture.DeaktiviereLesefehler();

            // Nach der Deaktivierung läuft die reguläre Prüfung wieder vollständig.
            menu.ClickUpdatePruefen();
            await fixture.WarteAufEreignisAsync(UpdateE2EEreignisse.UpdateCheckCompleted, Medium,
                filter: e => DatenText(e, "status") == "UpdateVerfuegbar");
        }
        finally
        {
            // Fehlersteuerung und Gates auch bei Testabbruch zurücksetzen - jeder Schritt
            // einzeln geschützt, damit ein Fehler nicht die Original-Assertion maskiert.
            try { fixture.DeaktiviereLesefehler(); } catch { /* Testwurzel evtl. schon entfernt. */ }
            try { fixture.SetzeGateZurueck(UpdateE2ETestKontext.LesefehlerGateName); } catch { /* Testwurzel evtl. schon entfernt. */ }
            SchliesseFixtureApp();
        }
    }

    /// <summary>
    /// Schließt die zuletzt gestartete Fixture-App und wartet auf den Prozess-Exit.
    /// <c>LaunchApp</c> beendet keine Vorgängerinstanz; ohne explizites Schließen bliebe ein
    /// Fenster (z. B. der Fortschrittsdialog) als UIA-Geist für Desktop-weite Suchläufe offen.
    /// </summary>
    private void SchliesseFixtureApp()
    {
        int? processId;
        try { processId = FlaUiApp.ProcessId; }
        catch (InvalidOperationException) { return; }

        try { FlaUiApp.Close(); }
        catch (Exception) { /* Die App ist evtl. bereits beendet. */ }

        try
        {
            using var prozess = Process.GetProcessById(processId.Value);
            prozess.WaitForExit((int)TimeSpan.FromSeconds(30).TotalMilliseconds);
        }
        catch (Exception) { /* Prozess bereits beendet. */ }
    }

    private static void AssertSequenz(IReadOnlyList<UpdateE2EProtokollEintrag> protokoll, params string[] ereignisse)
    {
        var position = 0L;
        foreach (var ereignis in ereignisse)
        {
            var eintrag = protokoll.FirstOrDefault(e => e.Ereignis == ereignis && e.Seq > position);
            Assert.True(eintrag is not null,
                $"Protokollereignis '{ereignis}' fehlt oder liegt nicht in der erwarteten Reihenfolge.");
            position = eintrag!.Seq;
        }
    }

    private static string? DatenText(UpdateE2EProtokollEintrag eintrag, string name)
        => UpdateE2EFixture.Daten(eintrag) is { } daten
            && daten.TryGetProperty(name, out var wert)
            ? wert.GetString()
            : null;

    private static bool? DatenBool(UpdateE2EProtokollEintrag eintrag, string name)
        => UpdateE2EFixture.Daten(eintrag) is { } daten
            && daten.TryGetProperty(name, out var wert)
            && wert.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? wert.GetBoolean()
            : null;

    private static long? DatenZahl(UpdateE2EProtokollEintrag eintrag, string name)
        => UpdateE2EFixture.Daten(eintrag) is { } daten
            && daten.TryGetProperty(name, out var wert)
            && wert.ValueKind == JsonValueKind.Number
            ? wert.GetInt64()
            : null;

    private string? ErmittleAppExePfadDesLaufendenProzesses()
    {
        try
        {
            using var prozess = Process.GetProcessById(FlaUiApp.ProcessId);
            return prozess.MainModule?.FileName;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
