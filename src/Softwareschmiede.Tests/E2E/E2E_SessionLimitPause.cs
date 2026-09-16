using FlaUI.Core.AutomationElements;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Application.Services.Updates;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Tests.E2E.Views;
using Softwareschmiede.Tests.E2E.Views.Dialogs;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// E2E-Test für die automatische Pausierung bei Session-Limit-Marker-Erkennung (Issue 151):
/// Der Marker <c>[[SOFTWARESCHMIEDE_RATE_LIMIT:&lt;ISO8601&gt;]]</c> wird über eine Promptvorlage
/// (<c>echo [[...]]</c>) auf dem echten Ausgabepfad <c>PseudoConsoleSession → CliOutputProtokollWriter</c>
/// einer laufenden KiSimulator-Aufgabe emittiert. Erwartet: RateLimit-Protokolleintrag an der
/// auslösenden Aufgabe, persistiertes Plugin-Limit unter
/// <c>plugins.sessionlimit.Softwareschmiede.KiSimulator</c>, PausiertBisUtc auf allen laufenden
/// Aufgaben desselben Prefix, pausierte Kacheln — ohne Unterbrechung der laufenden CLI.
/// Zusätzlich wird die Update-Sicherheitsprüfung (<see cref="CliUpdateSafetyService"/>) mit einem
/// in-Test instanziierten Service gegen die laufende Test-DB geprüft.
///
/// Voraussetzungen:
/// - Windows-Desktop-Session (kein Headless-CI)
/// - Softwareschmiede.App muss im Debug-Modus gebaut sein
/// - Im Test-Modus steht ausschließlich das LocalDirectoryPlugin als SCM-Plugin zur Verfügung;
///   der CLI-Prozess läuft als simuliertes interaktives cmd.exe (<see cref="Softwareschmiede.Infrastructure.Terminal.SimulatedPseudoConsoleProcessLauncher"/>),
///   sodass der per Prompt gesendete <c>echo</c>-Befehl den Marker deterministisch auf STDOUT ausgibt.
///
/// CI-Regular-Lauf: dotnet test --filter "Category!=OsInterface"
/// </summary>
public partial class End2EndTest
{
    private const string LimitTitelA = "Limit-Aufgabe-A";
    private const string LimitTitelB = "Limit-Aufgabe-B";
    private const string MarkerVorlagenName = "SessionLimit-Marker";
    private const string SimulatorPrefix = "Softwareschmiede.KiSimulator";
    private const string FremdPrefix = "E2E.Anderes.KiPlugin";

    /// <summary>
    /// Szenario: Zwei Aufgaben laufen mit dem KiSimulator; über die UI wird an Aufgabe B ein
    /// zeitgesteuerter Prompt mit vergangener Zielzeit (→ Sofortversand) gesendet, der den
    /// Rate-Limit-Marker per <c>echo</c> auf die CLI-Ausgabe schreibt. Anschließend werden
    /// Protokolleintrag, App-Einstellung, PausiertBisUtc beider Aufgaben und die pausierten
    /// Seitenleisten-Kacheln geprüft — bei weiterlaufender CLI von B (kein Unterbrechen). Danach
    /// wird die Update-Sicherheitsprüfung verifiziert: Limit-Aufgaben trotz frischem Heartbeat
    /// nicht riskant, Fremd-Prefix-Aufgabe riskant, nach Ablauf des Limits wieder riskant.
    /// </summary>
    /// <param name="mainWindow">Das Hauptfenster der Anwendung.</param>
    protected async Task SessionLimit_MarkerPauseProtokollUndUpdateSicherheit_E2E(Window mainWindow)
    {
        ConfirmLocalDirectoryGitInitInSourceDirectory();

        var sourceDirectory = CreateLocalSourceDirectory("SessionLimit-Repo");
        var settings = new SettingsView(mainWindow).ForceShow();
        var dashboard = settings.ConfigureLocalDirectoryPlugin(sourceDirectory, useInSourceDirectoryMode: false);

        var projectList = dashboard.Menu.NavigateToProjects();
        projectList.CreateProject("SessionLimit-Projekt");
        var projectDetail = projectList.OpenProject("SessionLimit-Projekt");

        var repoDialog = new RepositoryAssignDialogView(mainWindow).ForceShow();
        repoDialog.SelectFirstRepository();
        projectDetail = repoDialog.Confirm();

        // Promptvorlage mit Marker-Echo VOR dem Öffnen der Detailansichten seeden, damit sie in
        // der ComboBox „PromptVorlagenAuswahl" der jeweils geladenen Detailansicht enthalten ist.
        var resetUtc = DateTimeOffset.UtcNow.AddHours(2);
        var markerZeile = $"echo [[SOFTWARESCHMIEDE_RATE_LIMIT:{resetUtc:O}]]";
        await using (var db = OpenTestDbContext())
        {
            db.PromptVorlagen.Add(new PromptVorlage
            {
                Name = MarkerVorlagenName,
                Prompttext = markerZeile,
                Sortierung = 0
            });
            await db.SaveChangesAsync();
        }

        // Aufgabe A anlegen und CLI starten, dann zurück zum Projekt für Aufgabe B.
        var taskA = ErstelleUndStarteLimitAufgabe(projectDetail, LimitTitelA);
        taskA.GoBack();
        var projectDetailForB = Assert.IsType<ProjectDetailView>(mainWindow.CurrentView());

        // Aufgabe B anlegen, CLI starten und geöffnet lassen — über sie wird der Marker gesendet.
        var taskB = ErstelleUndStarteLimitAufgabe(projectDetailForB, LimitTitelB);

        Guid idA;
        Guid idB;
        string runIdB;
        await WartenBisAsync(async () =>
        {
            await using var db = OpenTestDbContext();
            var a = await db.Aufgaben.SingleOrDefaultAsync(x => x.Titel == LimitTitelA);
            var b = await db.Aufgaben.SingleOrDefaultAsync(x => x.Titel == LimitTitelB);
            return a?.AktiveRunId is not null && b?.AktiveRunId is not null;
        });
        await using (var db = OpenTestDbContext())
        {
            idA = (await db.Aufgaben.SingleAsync(x => x.Titel == LimitTitelA)).Id;
            var b = await db.Aufgaben.SingleAsync(x => x.Titel == LimitTitelB);
            idB = b.Id;
            runIdB = b.AktiveRunId!;
        }

        // Zeitgesteuerter Versand mit bereits vergangener Zielzeit → der Service versendet sofort.
        // Skip-Guard analog ZeitgesteuerterPrompt_NachPlanen_..._E2E: Zwischen 00:00 und 00:10 läge
        // "jetzt - 10 min" noch am Vortag; die App interpretiert die eingegebene Uhrzeit aber immer
        // als heutiges Datum — eine Zukunftszeit würde geplant statt gesendet.
        var jetzt = DateTime.Now;
        Skip.If(jetzt.Hour == 0 && jetzt.Minute < 10, "Kurz nach Mitternacht: 'jetzt - 10 min' wäre als heutige Uhrzeit eine Zukunftszeit.");
        var vergangen = jetzt.AddMinutes(-10);

        taskB.SelectPromptTemplate(MarkerVorlagenName);
        taskB.SetScheduledPromptTime(vergangen.Hour, vergangen.Minute);
        WaitForElement(mainWindow, cf => cf.ByName("ZeitgesteuertSenden"), Short).AsButton().Click();

        // Warten, bis der Marker auf dem echten Ausgabepfad verarbeitet wurde:
        // RateLimit-Protokolleintrag an Aufgabe B + PausiertBisUtc an beiden Aufgaben.
        // Erhöhtes Zeitlimit: Der gesendete echo-Befehl liegt in der STDIN-Pipe, solange das
        // simulierte Plugin-Kommando (ping -n 31 ≈ 30 s) die cmd.exe blockiert — die
        // Marker-Ausgabe erscheint erst nach dessen Ende.
        await WartenBisAsync(async () =>
        {
            await using var db = OpenTestDbContext();
            var hatRateLimitEintrag = await db.Protokolleintraege
                .AnyAsync(p => p.AufgabeId == idB && p.Typ == ProtokollTyp.RateLimit);
            var a = await db.Aufgaben.SingleAsync(x => x.Id == idA);
            var b = await db.Aufgaben.SingleAsync(x => x.Id == idB);
            return hatRateLimitEintrag && a.PausiertBisUtc is not null && b.PausiertBisUtc is not null;
        }, maxVersuche: 250);

        await using (var db = OpenTestDbContext())
        {
            // Plugin-weit persistiertes Session-Limit.
            var limitEintrag = await db.AppEinstellungen
                .SingleOrDefaultAsync(e => e.Schluessel == KiPluginLimitService.SessionLimitKeyPrefix + SimulatorPrefix);
            Assert.NotNull(limitEintrag);
            Assert.True(
                DateTimeOffset.TryParse(limitEintrag!.Wert, out var persistiertesLimit),
                $"Persistierter Limit-Wert '{limitEintrag.Wert}' ist kein gültiger Zeitstempel.");
            Assert.True(
                Math.Abs((persistiertesLimit - resetUtc).TotalSeconds) < 2,
                $"Persistiertes Limit '{persistiertesLimit:O}' weicht vom Marker '{resetUtc:O}' ab.");

            // Beide Aufgaben desselben Prefix sind pausiert.
            var a = await db.Aufgaben.SingleAsync(x => x.Id == idA);
            var b = await db.Aufgaben.SingleAsync(x => x.Id == idB);
            AssertPausiertBisUtc(a.PausiertBisUtc, resetUtc, LimitTitelA);
            AssertPausiertBisUtc(b.PausiertBisUtc, resetUtc, LimitTitelB);

            // Kein Unterbrechen: AktiveRunId von B ist unverändert, Status weiterhin Gestartet/Aktiv.
            Assert.Equal(runIdB, b.AktiveRunId);
            Assert.Equal(AufgabeAusfuehrungsStatus.Aktiv, b.AusfuehrungsStatus);
        }

        // Die CLI von B läuft sichtbar weiter (Stoppen-Button).
        Assert.True(taskB.IsCliRunning(), "Der 'CliStoppen'-Button von Aufgabe B muss trotz Pause sichtbar bleiben — der laufende Prozess wird nicht unterbrochen.");

        // Beide Seitenleisten-Kacheln zeigen den Pausiert-Status mit Countdown und Abblendungs-Marker.
        WarteAufKachelStatus(mainWindow, LimitTitelA, s => s.StartsWith("⏸ Pausiert", StringComparison.Ordinal), Long);
        WarteAufKachelStatus(mainWindow, LimitTitelB, s => s.StartsWith("⏸ Pausiert", StringComparison.Ordinal), Long);
        WarteAufKachelPausiertMarker(mainWindow, LimitTitelA, "Pausiert:True", Long);
        WarteAufKachelPausiertMarker(mainWindow, LimitTitelB, "Pausiert:True", Long);

        // Dritte Aufgabe mit Fremd-Prefix und frischem Heartbeat seeden (kein echter CLI-Start nötig —
        // CliUpdateSafetyService bewertet ausschließlich persistierte Lauf-Indikatoren).
        await using (var db = OpenTestDbContext())
        {
            var projekt = await db.Projekte.FirstAsync();
            db.Aufgaben.Add(new Aufgabe
            {
                Id = Guid.NewGuid(),
                ProjektId = projekt.Id,
                Titel = "Limit-FremdPrefix-Aufgabe",
                Status = AufgabeStatus.Gestartet,
                AusfuehrungsStatus = AufgabeAusfuehrungsStatus.Aktiv,
                KiPluginPrefix = FremdPrefix,
                AktiveRunId = Guid.NewGuid().ToString("N"),
                LastHeartbeatUtc = DateTimeOffset.UtcNow,
                ErstellungsDatum = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        // Update-Sicherheitsprüfung gegen die laufende Test-DB: pausierte Limit-Aufgaben sind trotz
        // frischem Heartbeat nicht riskant (Heartbeat-Toleranz entfällt), die Fremd-Prefix-Aufgabe bleibt riskant.
        await using (var db = OpenTestDbContext())
        {
            var result = await ErstelleSafetyService(db).CheckAsync();
            Assert.DoesNotContain(result.RiskyTasks, t => t.Contains(LimitTitelA, StringComparison.Ordinal));
            Assert.DoesNotContain(result.RiskyTasks, t => t.Contains(LimitTitelB, StringComparison.Ordinal));
            Assert.Contains(result.RiskyTasks, t => t.Contains("Limit-FremdPrefix-Aufgabe", StringComparison.Ordinal));
        }

        // Nach Ablauf des Limits (abgelaufener Zeitstempel persistiert) greift die normale
        // Heartbeat-Bewertung wieder: die laufenden Simulator-Aufgaben sind wieder riskant.
        await using (var db = OpenTestDbContext())
        {
            var eintrag = await db.AppEinstellungen
                .SingleAsync(e => e.Schluessel == KiPluginLimitService.SessionLimitKeyPrefix + SimulatorPrefix);
            eintrag.Wert = DateTimeOffset.UtcNow.AddMinutes(-5).ToString("O", System.Globalization.CultureInfo.InvariantCulture);
            eintrag.AktualisiertAm = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
        }

        await using (var db = OpenTestDbContext())
        {
            var result = await ErstelleSafetyService(db).CheckAsync();
            Assert.Contains(result.RiskyTasks, t => t.Contains(LimitTitelA, StringComparison.Ordinal));
            Assert.Contains(result.RiskyTasks, t => t.Contains(LimitTitelB, StringComparison.Ordinal));
        }

        // Aufräumen: laufende CLI von B stoppen und das Projekt löschen. Task B wurde über die
        // Projektdetailansicht geöffnet — „Zurück" führt dorthin, nicht zum Dashboard.
        taskB.StopCli();
        taskB.ForceClose(recurseToDashboard: false);
        var projectDetailFinal = Assert.IsType<ProjectDetailView>(mainWindow.CurrentView());
        projectDetailFinal.DeleteProject();
        projectDetailFinal.Menu.NavigateToDashboard();
    }

    /// <summary>
    /// Legt eine neue Aufgabe im übergebenen Projekt an, benennt sie um, öffnet sie erneut und startet
    /// die CLI mit dem KI-Simulator-Plugin (gleiches Muster wie
    /// <c>E2E_TaskWechselUeberMenue.ErstelleUndStarteAufgabe</c>).
    /// </summary>
    private static TaskDetailView ErstelleUndStarteLimitAufgabe(ProjectDetailView projectDetail, string titel)
    {
        var task = projectDetail.CreateTask();
        task.SetTaskTitle(titel);
        task.SaveTask();
        task.GoBack();

        var projectDetailAfterSave = Assert.IsType<ProjectDetailView>(task.Window.CurrentView());
        var taskReopened = projectDetailAfterSave.OpenTask(titel);
        taskReopened.Start("Softwareschmiede.KiSimulator", fuerProjektVerwenden: false);
        taskReopened.WaitForCliRunning();

        return taskReopened;
    }

    private static CliUpdateSafetyService ErstelleSafetyService(Softwareschmiede.Infrastructure.Data.SoftwareschmiededDbContext db)
    {
        var aufgabeService = new AufgabeService(
            db,
            NullLogger<AufgabeService>.Instance,
            new TodoService(db, NullLogger<TodoService>.Instance));
        var appEinstellungService = new AppEinstellungService(db, NullLogger<AppEinstellungService>.Instance);
        var limitService = new KiPluginLimitService(
            db,
            appEinstellungService,
            new AufgabeLaufdatenChangedNotifier(),
            NullLogger<KiPluginLimitService>.Instance);
        return new CliUpdateSafetyService(aufgabeService, limitService, NullLogger<CliUpdateSafetyService>.Instance);
    }

    private static void AssertPausiertBisUtc(DateTimeOffset? pausiertBisUtc, DateTimeOffset erwartet, string titel)
    {
        Assert.NotNull(pausiertBisUtc);
        Assert.True(
            Math.Abs((pausiertBisUtc!.Value - erwartet).TotalSeconds) < 2,
            $"PausiertBisUtc von '{titel}' ('{pausiertBisUtc:O}') weicht vom Marker-Zeitpunkt '{erwartet:O}' ab.");
    }
}
