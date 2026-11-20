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
    private const string UpdateNoticeReadfailure = "konnten nicht gelesen";

    /// <summary>
    /// Fixture-Smoke 1 (Plan: <c>Fixture_UsesIsolatedRealUpdatePipeline</c>): Echte JSON- und
    /// ZIP-Pipeline über den Fixture-HttpMessageHandler inklusive Manifest-Abruf und
    /// SHA-256-Verifikation, isolierte Testpfade, Stable-/RC-Auswahl,
    /// kontrollierte Launcher-/Shutdown-Grenze, DB-erhaltender Neustart und der Nachweis, dass
    /// unbekannte Requests ohne Netzwerkfallback scheitern.
    /// </summary>
    private async Task Fixture_UsesIsolatedRealUpdatePipeline()
    {
        using var fixture = new UpdateE2EFixture();
        await fixture.SetUpdateSettingsAsync(UpdateMode.CheckAndRunAtStartup, UpdatePathChannelResolver.StableVersionsPath);

        var app = LaunchApp(ensureDatabaseDeleted: false, fixture.CreateStartEnvironment());
        try
        {
            var mainWindow = WaitOnRealMainwindow(app);
            var menu = new MenuView(mainWindow);

            // Startautomatik bis zur kontrollierten Updater-Prozessgrenze abwarten.
            await fixture.WaitOnEventAsync(UpdateE2EEvents.StartupUpdateCompleted, Long);
            var log = fixture.ReadLog();

            // Reihenfolge der Basis-Marker: DB-Initialisierung vor WindowReady vor Versuchsbeginn.
            AssertSequence(log,
                UpdateE2EEvents.DatabaseInitializationCompleted,
                UpdateE2EEvents.WindowReady,
                UpdateE2EEvents.UpdateAttemptStarted);

            // Echte JSON-Pipeline: Release-Abruf traf den Fixture-Handler, Auswahl = Stable 1.3.0.
            Assert.Contains(log, e => e.Event == UpdateE2EEvents.HttpRequest
                && DataText(e, "url") == UpdateE2EFixture.ReleaseApiUrl);
            Assert.Contains(log, e => e.Event == UpdateE2EEvents.UpdateCheckCompleted
                && DataText(e, "status") == "UpdateVerfuegbar"
                && DataText(e, "version") == UpdateE2EFixture.StableVersion
                && DataBool(e, "isPrerelease") == false);

            // Echte ZIP-Pipeline: Download/Verifizieren/Entpacken/Skripterzeugung liefen real
            // (Phasen protokolliert); der Manifest-Abruf lief über den Fixture-Handler.
            Assert.True(log.Count(e => e.Event == UpdateE2EEvents.PreparationPhase) >= 4,
                "Mindestens vier Vorbereitungsphasen (Download, Verifizieren, Entpacken, UpdateVorbereiten) erwartet.");
            Assert.Contains(log, e => e.Event == UpdateE2EEvents.PreparationPhase
                && DataText(e, "phase") == "Verifizieren");
            Assert.Contains(log, e => e.Event == UpdateE2EEvents.HttpRequest
                && DataText(e, "art") == "manifest"
                && DataText(e, "url") == fixture.StableManifestDownloadUrl);
            var preparation = Assert.Single(log, e => e.Event == UpdateE2EEvents.PreparationCompleted);
            var scriptPath = DataText(preparation, "scriptPath");
            Assert.NotNull(scriptPath);
            Assert.StartsWith(Path.GetFullPath(fixture.TestRoot), Path.GetFullPath(scriptPath!));

            // Kontrollierte Updater-Prozessgrenze: Launcher-Record statt echtem Prozess.
            var launcher = Assert.Single(log, e => e.Event == UpdateE2EEvents.UpdateProcessStartRecorded);
            Assert.Equal(fixture.TestRoot, DataText(launcher, "workingDirectory"));
            Assert.Equal(false, DataBool(launcher, "runElevated"));
            Assert.Equal("Erfolg", DataText(launcher, "result"));
            Assert.Contains(log, e => e.Event == UpdateE2EEvents.UpdateStartSucceeded);
            Assert.Single(log, e => e.Event == UpdateE2EEvents.ShutdownRequested);

            // Testpfade: Update-Artefakte liegen ausschließlich unterhalb der Fixture-Wurzel.
            var zipPath = Path.Combine(fixture.TestRoot, "updates", "download", "release.zip");
            Assert.True(File.Exists(zipPath), $"Heruntergeladenes Paket fehlt: {zipPath}");
            var entpackt = Path.Combine(fixture.TestRoot, "updates", "extracted", UpdateE2EFixture.StableVersion);
            Assert.Equal("E2E-Paket 1.3.0 (stable-marker)", UpdateE2EFixture.ReadPackageExeContent(entpackt));
            Assert.True(File.Exists(Path.Combine(entpackt, "version.json")), "Entpacktes version.json fehlt.");
            Assert.True(File.Exists(scriptPath), $"Update-Skript fehlt: {scriptPath}");

            // Kein Schreibzugriff auf das App-Programmverzeichnis (Fixture-Isolation).
            var appDirectory = Path.GetDirectoryName(ResolveAppExePathOfRunningProcess());
            Assert.NotNull(appDirectory);
            Assert.False(Directory.Exists(Path.Combine(appDirectory!, "updates")),
                "Im App-Programmverzeichnis wurde ein updates/-Ordner erzeugt - Isolation verletzt.");

            // Unbekannter Request scheitert ohne Netzwerkfallback.
            var httpBefore = fixture.CountEvent(UpdateE2EEvents.HttpResponse);
            fixture.UpdateScenario(s => s.Answer.Clear());
            menu.CheckClickUpdate();
            await fixture.WaitOnEventAsync(UpdateE2EEvents.UnknownHttpRequest, Medium);
            await fixture.WaitOnEventAsync(UpdateE2EEvents.UpdateCheckCompleted, Medium,
                filter: e => DataText(e, "status") == "NichtPruefbar");
            Assert.Equal(httpBefore, fixture.CountEvent(UpdateE2EEvents.HttpResponse));

            // RC-Auswahl über den Vorabversionen-Update-Pfad nach echtem, DB-erhaltendem Neustart.
            fixture.SetDefaultResponses();
            fixture.SaveScenario();
            await fixture.UpdateUpdateSettingsAsync(UpdateMode.CheckAndRunAtStartup, UpdatePathChannelResolver.PrereleaseVersionsPath);
            mainWindow = RestartAppPreservingDatabase();
            await fixture.WaitOnEventAsync(UpdateE2EEvents.StartupUpdateCompleted, Long,
                mindestens: 2);

            var logAfterRestart = fixture.ReadLog();
            Assert.Contains(logAfterRestart, e => e.Event == UpdateE2EEvents.UpdateCheckCompleted
                && DataText(e, "version") == UpdateE2EFixture.RcVersion
                && DataBool(e, "isPrerelease") == true);
            Assert.Equal(2, logAfterRestart.Count(e => e.Event == UpdateE2EEvents.UpdateProcessStartRecorded));
            Assert.Equal(2, logAfterRestart.Count(e => e.Event == UpdateE2EEvents.ShutdownRequested));
            var entpacktRc = Path.Combine(fixture.TestRoot, "updates", "extracted", UpdateE2EFixture.RcVersion);
            Assert.Equal($"E2E-Paket {UpdateE2EFixture.RcVersion} (rc-marker)", UpdateE2EFixture.ReadPackageExeContent(entpacktRc));
        }
        finally
        {
            // Die Fixture-App explizit schließen: Der aufgezeichnete (nicht echte) Shutdown lässt
            // den Prozess samt Fortschrittsdialog offen, und LaunchApp schließt keine Vorgänger-
            // instanz - sonst bliebe der "Update vorbereiten"-Dialog als UIA-Geist für Folgetests.
            CloseFixtureApp();
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
        await fixture.SetUpdateSettingsAsync(UpdateMode.CheckOnly, UpdatePathChannelResolver.StableVersionsPath);

        // Der Lesefehler wird vor dem Start aktiviert; markierte Reads kommen frühestens nach
        // DatabaseInitializationCompleted im ersten Updateversuch vor (der erste getaggte Read
        // ist der des Startversuchs - Migration und ungetaggte Settings bleiben unberührt).
        fixture.ActivateReadfailure("Initial", attempt: "startup");

        var app = LaunchApp(ensureDatabaseDeleted: false, fixture.CreateStartEnvironment());
        var mainWindow = WaitOnRealMainwindow(app);
        var menu = new MenuView(mainWindow);

        try
        {
            // Grenze Initial (Startversuch): markierter Read blockiert bis zur Freigabe.
            var reaches = await fixture.WaitOnEventAsync(
                UpdateE2EEvents.UpdateSettingsReadReached, Medium,
                filter: e => DataText(e, "versuch") == "startup" && DataNumber(e, "ordinal") == 1);
            Assert.Equal("Initial", DataText(reaches[0], "grenze"));
            fixture.OpenGate(UpdateE2ETestContext.ReadFailureGateName);

            await fixture.WaitOnEventAsync(UpdateE2EEvents.UpdateSettingsReadFailed, Medium,
                filter: e => DataText(e, "versuch") == "startup");
            await fixture.WaitOnEventAsync(UpdateE2EEvents.StartupUpdateCompleted, Medium);
            menu.WaitForUpdateNotice(UpdateNoticeReadfailure, Medium);

            // Kein unerlaubter Folgeschritt: Der Releaseabruf durfte nicht stattfinden.
            Assert.Equal(0, fixture.CountEvent(UpdateE2EEvents.HttpRequest));
            fixture.SetGateBack(UpdateE2ETestContext.ReadFailureGateName);
            fixture.DeactivateReadfailure();
            Assert.True(fixture.CountEvent(UpdateE2EEvents.UpdateSettingsReadFailureDisabled) >= 1);

            // Wiederlesen nach Deaktivierung gelingt: Das Speichern der Einstellungen über die
            // echte UI füllt den Einstellungs-Snapshot des Hauptfensters (danach ist "Programmupdate
            // prüfen" wieder aktiviert) und beweist, dass Settings-Reads/Schreiben funktionieren.
            var settingsView = menu.NavigateToSettings();
            settingsView.SaveSettings();
            menu.NavigateToDashboard();

            menu.CheckClickUpdate();
            Assert.Equal(UpdateE2EFixture.StableVersion, menu.WaitForOfferedUpdateVersion(Medium));

            // Grenze BeforePreparation (manueller Startversuch): erster Read und Releaseabruf
            // gelingen, der zweite markierte Read vor dem Download schlägt kontrolliert fehl.
            fixture.ActivateReadfailure("BeforePreparation", attempt: "starten");
            menu.StartClickUpdate();
            await fixture.WaitOnEventAsync(
                UpdateE2EEvents.UpdateSettingsReadReached, Medium,
                filter: e => DataText(e, "versuch") == "starten" && DataNumber(e, "ordinal") == 2);
            fixture.OpenGate(UpdateE2ETestContext.ReadFailureGateName);
            await fixture.WaitOnEventAsync(UpdateE2EEvents.UpdateSettingsReadFailed, Medium,
                filter: e => DataText(e, "versuch") == "starten" && DataText(e, "grenze") == "BeforePreparation");

            var progress = new UpdateProgressDialogView(mainWindow);
            Assert.Contains(UpdateNoticeReadfailure, progress.WaitForErrorState(Medium));
            progress.Close();
            Assert.Equal(0, fixture.CountEvent(UpdateE2EEvents.PreparationCompleted));
            fixture.SetGateBack(UpdateE2ETestContext.ReadFailureGateName);
            fixture.DeactivateReadfailure();

            // Grenze BeforeUpdaterStart: Reads 1+2 und die komplette Vorbereitung gelingen;
            // erst der letzte markierte Read nach PreparationCompleted schlägt fehl.
            fixture.ActivateReadfailure("BeforeUpdaterStart", attempt: "starten");
            menu.CheckClickUpdate();
            menu.WaitForOfferedUpdateVersion(Medium);
            menu.StartClickUpdate();
            var lastRead = (await fixture.WaitOnEventAsync(
                UpdateE2EEvents.UpdateSettingsReadReached, Medium,
                filter: e => DataText(e, "versuch") == "starten" && DataNumber(e, "ordinal") == 3))[0];
            var preparationCompleted = Assert.Single(
                fixture.ReadLog(), e => e.Event == UpdateE2EEvents.PreparationCompleted);
            Assert.True(preparationCompleted.Seq < lastRead.Seq,
                "Der BeforeUpdaterStart-Read muss erst nach PreparationCompleted erfolgen.");
            fixture.OpenGate(UpdateE2ETestContext.ReadFailureGateName);
            await fixture.WaitOnEventAsync(UpdateE2EEvents.UpdateSettingsReadFailed, Medium,
                filter: e => DataText(e, "grenze") == "BeforeUpdaterStart");
            Assert.Contains(UpdateNoticeReadfailure, progress.WaitForErrorState(Medium));
            progress.Close();

            // Bis zur Installation durfte es nicht kommen: kein Updater-Start, kein Shutdown.
            Assert.Equal(0, fixture.CountEvent(UpdateE2EEvents.UpdateStartAttempt));
            Assert.Equal(0, fixture.CountEvent(UpdateE2EEvents.ShutdownRequested));
            fixture.SetGateBack(UpdateE2ETestContext.ReadFailureGateName);

            // Einmal aktiviert, bleibt der Fehler bis zur expliziten Deaktivierung aktiv
            // (zwei aufeinanderfolgende markierte Reads desselben Versuchstyps scheitern).
            fixture.ActivateReadfailure("Initial", attempt: "pruefen");
            var errorBefore = fixture.CountEvent(UpdateE2EEvents.UpdateSettingsReadFailed);
            for (var i = 0; i < 2; i++)
            {
                var reachedCheckBefore = fixture.ReadLog().Count(e =>
                    e.Event == UpdateE2EEvents.UpdateSettingsReadReached
                    && DataText(e, "versuch") == "pruefen" && DataNumber(e, "ordinal") == 1);
                menu.CheckClickUpdate();
                await fixture.WaitOnEventAsync(
                    UpdateE2EEvents.UpdateSettingsReadReached, Medium,
                    mindestens: reachedCheckBefore + 1,
                    filter: e => DataText(e, "versuch") == "pruefen" && DataNumber(e, "ordinal") == 1);
                if (i == 0)
                    fixture.CancelGateFrom(UpdateE2ETestContext.ReadFailureGateName); // Abbruch-Pfad
                else
                    fixture.OpenGate(UpdateE2ETestContext.ReadFailureGateName); // Freigabe-Pfad
                await fixture.WaitOnEventAsync(UpdateE2EEvents.UpdateSettingsReadFailed, Medium,
                    mindestens: errorBefore + i + 1);
                fixture.SetGateBack(UpdateE2ETestContext.ReadFailureGateName);
            }
            var cancel = fixture.ReadLog()
                .Where(e => e.Event == UpdateE2EEvents.UpdateSettingsReadFailed)
                .ToList();
            Assert.Contains(cancel, e => DataText(e, "grund") == "abgebrochen");
            Assert.Contains(cancel, e => DataText(e, "grund") == "freigegeben");

            // Timeout-Cleanup: Ohne Gate-Datei schlägt die begrenzte Wartezeit fehl.
            fixture.ActivateReadfailure("Initial", attempt: "pruefen", waittimeSeconds: 1);
            menu.CheckClickUpdate();
            await fixture.WaitOnEventAsync(UpdateE2EEvents.UpdateSettingsReadFailed, Medium,
                filter: e => DataText(e, "grund") == "timeout");
            fixture.DeactivateReadfailure();

            // Nach der Deaktivierung läuft die reguläre Prüfung wieder vollständig.
            menu.CheckClickUpdate();
            await fixture.WaitOnEventAsync(UpdateE2EEvents.UpdateCheckCompleted, Medium,
                filter: e => DataText(e, "status") == "UpdateVerfuegbar");
        }
        finally
        {
            // Fehlersteuerung und Gates auch bei Testabbruch zurücksetzen - jeder Schritt
            // einzeln geschützt, damit ein Fehler nicht die Original-Assertion maskiert.
            try { fixture.DeactivateReadfailure(); } catch { /* TestRoot evtl. schon entfernt. */ }
            try { fixture.SetGateBack(UpdateE2ETestContext.ReadFailureGateName); } catch { /* TestRoot evtl. schon entfernt. */ }
            CloseFixtureApp();
        }
    }

    /// <summary>
    /// Schließt die zuletzt gestartete Fixture-App und wartet auf den Prozess-Exit.
    /// <c>LaunchApp</c> beendet keine Vorgängerinstanz; ohne explizites Schließen bliebe ein
    /// Fenster (z. B. der Fortschrittsdialog) als UIA-Geist für Desktop-weite Suchläufe offen.
    /// </summary>
    private void CloseFixtureApp()
    {
        int? processId;
        try { processId = FlaUiApp.ProcessId; }
        catch (InvalidOperationException) { return; }

        try { FlaUiApp.Close(); }
        catch (Exception) { /* Die App ist evtl. bereits beendet. */ }

        try
        {
            using var process = Process.GetProcessById(processId.Value);
            process.WaitForExit((int)TimeSpan.FromSeconds(30).TotalMilliseconds);
        }
        catch (Exception) { /* Prozess bereits beendet. */ }
    }

    private static void AssertSequence(IReadOnlyList<UpdateE2ELogEntry> log, params string[] events)
    {
        var position = 0L;
        foreach (var evt in events)
        {
            var entry = log.FirstOrDefault(e => e.Event == evt && e.Seq > position);
            Assert.True(entry is not null,
                $"Protokollereignis '{evt}' fehlt oder liegt nicht in der erwarteten Reihenfolge.");
            position = entry!.Seq;
        }
    }

    private static string? DataText(UpdateE2ELogEntry entry, string name)
        => UpdateE2EFixture.Data(entry) is { } data
            && data.TryGetProperty(name, out var value)
            ? value.GetString()
            : null;

    private static bool? DataBool(UpdateE2ELogEntry entry, string name)
        => UpdateE2EFixture.Data(entry) is { } data
            && data.TryGetProperty(name, out var value)
            && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : null;

    private static long? DataNumber(UpdateE2ELogEntry entry, string name)
        => UpdateE2EFixture.Data(entry) is { } data
            && data.TryGetProperty(name, out var value)
            && value.ValueKind == JsonValueKind.Number
            ? value.GetInt64()
            : null;

    private string? ResolveAppExePathOfRunningProcess()
    {
        try
        {
            using var process = Process.GetProcessById(FlaUiApp.ProcessId);
            return process.MainModule?.FileName;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
