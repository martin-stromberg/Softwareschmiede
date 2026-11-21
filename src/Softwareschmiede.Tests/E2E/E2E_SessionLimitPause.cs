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
/// <c>plugins.sessionlimit.Softwareschmiede.KiSimulator</c>, PausedUntilUtc auf allen laufenden
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
    private const string LimitTitleA = "Limit-Aufgabe-A";
    private const string LimitTitleB = "Limit-Aufgabe-B";
    private const string MarkerTemplatesName = "SessionLimit-Marker";
    private const string SimulatorPrefix = "Softwareschmiede.KiSimulator";
    private const string ForeignPrefix = "E2E.Anderes.KiPlugin";

    /// <summary>
    /// Szenario: Zwei Aufgaben laufen mit dem KiSimulator; über die UI wird an Aufgabe B ein
    /// zeitgesteuerter Prompt mit vergangener Zielzeit (→ Sofortversand) gesendet, der den
    /// Rate-Limit-Marker per <c>echo</c> auf die CLI-Ausgabe schreibt. Anschließend werden
    /// Protokolleintrag, App-Einstellung, PausedUntilUtc beider Aufgaben und die pausierten
    /// Seitenleisten-Kacheln geprüft — bei weiterlaufender CLI von B (kein Unterbrechen). Danach
    /// wird die Update-Sicherheitsprüfung verifiziert: Limit-Aufgaben trotz frischem Heartbeat
    /// nicht riskant, Fremd-Prefix-Aufgabe riskant, nach Ablauf des Limits wieder riskant.
    /// </summary>
    /// <param name="mainWindow">Das Hauptfenster der Anwendung.</param>
    protected async Task SessionLimit_MarkerPauseLogAndUpdateSafe_E2E(Window mainWindow)
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
        // der ComboBox „PromptTemplateSelection" der jeweils geladenen Detailansicht enthalten ist.
        var resetUtc = DateTimeOffset.UtcNow.AddHours(2);
        var markerLine = $"echo [[SOFTWARESCHMIEDE_RATE_LIMIT:{resetUtc:O}]]";
        await using (var db = OpenTestDbContext())
        {
            db.PromptTemplates.Add(new PromptTemplate
            {
                Name = MarkerTemplatesName,
                PromptText = markerLine,
                SortOrder = 0
            });
            await db.SaveChangesAsync();
        }

        // Aufgabe A anlegen und CLI starten, dann zurück zum Projekt für Aufgabe B.
        var taskA = CreateAndStartLimitWorkItem(projectDetail, LimitTitleA);
        taskA.GoBack();
        var projectDetailForB = Assert.IsType<ProjectDetailView>(mainWindow.CurrentView());

        // Aufgabe B anlegen, CLI starten und geöffnet lassen — über sie wird der Marker gesendet.
        var taskB = CreateAndStartLimitWorkItem(projectDetailForB, LimitTitleB);

        Guid idA;
        Guid idB;
        string runIdB;
        await WaitUntilAsync(async () =>
        {
            await using var db = OpenTestDbContext();
            var a = await db.WorkItems.SingleOrDefaultAsync(x => x.Title == LimitTitleA);
            var b = await db.WorkItems.SingleOrDefaultAsync(x => x.Title == LimitTitleB);
            return a?.ActiveRunId is not null && b?.ActiveRunId is not null;
        });
        await using (var db = OpenTestDbContext())
        {
            idA = (await db.WorkItems.SingleAsync(x => x.Title == LimitTitleA)).Id;
            var b = await db.WorkItems.SingleAsync(x => x.Title == LimitTitleB);
            idB = b.Id;
            runIdB = b.ActiveRunId!;
        }

        // Zeitgesteuerter Versand mit bereits vergangener Zielzeit → der Service versendet sofort.
        // Skip-Guard analog ZeitgesteuerterPrompt_NachPlanen_..._E2E: Zwischen 00:00 und 00:10 läge
        // "jetzt - 10 min" noch am Vortag; die App interpretiert die eingegebene Uhrzeit aber immer
        // als heutiges Datum — eine Zukunftszeit würde geplant statt gesendet.
        var now = DateTime.Now;
        Assert.SkipWhen(now.Hour == 0 && now.Minute < 10, "Kurz nach Mitternacht: 'jetzt - 10 min' wäre als heutige Uhrzeit eine Zukunftszeit.");
        var vergangen = now.AddMinutes(-10);

        taskB.SetScheduledPromptTime(vergangen.Hour, vergangen.Minute);
        taskB.SelectPromptTemplate(MarkerTemplatesName);
        WaitForElement(mainWindow, cf => cf.ByName("ScheduledSend"), Short).AsButton().Click();

        // Warten, bis der Marker auf dem echten Ausgabepfad verarbeitet wurde:
        // RateLimit-Protokolleintrag an Aufgabe B + PausedUntilUtc an beiden Aufgaben.
        // Die interaktive /k-Shell des KiSimulators (Issue #271) führt den echo-Befehl sofort aus —
        // kein 30-s-Blocker durch ein Einmal-Kommando mehr.
        await WaitUntilAsync(async () =>
        {
            await using var db = OpenTestDbContext();
            var hasRateLimitEntry = await db.LogEntries
                .AnyAsync(p => p.WorkItemId == idB && p.Type == LogEntryType.RateLimit);
            var a = await db.WorkItems.SingleAsync(x => x.Id == idA);
            var b = await db.WorkItems.SingleAsync(x => x.Id == idB);
            return hasRateLimitEntry && a.PausedUntilUtc is not null && b.PausedUntilUtc is not null;
        }, maxAttempts: 250);

        await using (var db = OpenTestDbContext())
        {
            // Plugin-weit persistiertes Session-Limit.
            var limitEntry = await db.AppSettings
                .SingleOrDefaultAsync(e => e.Key == AiPluginLimitService.SessionLimitKeyPrefix + SimulatorPrefix);
            Assert.NotNull(limitEntry);
            Assert.True(
                DateTimeOffset.TryParse(limitEntry!.Value, out var persistedLimit),
                $"Persistierter Limit-Wert '{limitEntry.Value}' ist kein gültiger Zeitstempel.");
            Assert.True(
                Math.Abs((persistedLimit - resetUtc).TotalSeconds) < 2,
                $"Persistiertes Limit '{persistedLimit:O}' weicht vom Marker '{resetUtc:O}' ab.");

            // Beide Aufgaben desselben Prefix sind pausiert.
            var a = await db.WorkItems.SingleAsync(x => x.Id == idA);
            var b = await db.WorkItems.SingleAsync(x => x.Id == idB);
            AssertPausedUntilUtc(a.PausedUntilUtc, resetUtc, LimitTitleA);
            AssertPausedUntilUtc(b.PausedUntilUtc, resetUtc, LimitTitleB);

            // Kein Unterbrechen: ActiveRunId von B ist unverändert, Status weiterhin Gestartet/Aktiv.
            Assert.Equal(runIdB, b.ActiveRunId);
            Assert.Equal(WorkItemExecutionStatus.Active, b.ExecutionStatus);
        }

        // Die CLI von B läuft sichtbar weiter (Stoppen-Button).
        Assert.True(taskB.IsCliRunning(), "Der 'CliStoppen'-Button von Aufgabe B muss trotz Pause sichtbar bleiben — der laufende Prozess wird nicht unterbrochen.");

        // Beide Seitenleisten-Kacheln zeigen den Pausiert-Status mit Countdown und Abblendungs-Marker.
        WaitOnTileStatus(mainWindow, LimitTitleA, s => s.StartsWith("⏸ Pausiert", StringComparison.Ordinal), Long);
        WaitOnTileStatus(mainWindow, LimitTitleB, s => s.StartsWith("⏸ Pausiert", StringComparison.Ordinal), Long);
        WaitOnTilePausedMarker(mainWindow, LimitTitleA, "Paused:True", Long);
        WaitOnTilePausedMarker(mainWindow, LimitTitleB, "Paused:True", Long);

        // Dritte Aufgabe mit Fremd-Prefix und frischem Heartbeat seeden (kein echter CLI-Start nötig —
        // CliUpdateSafetyService bewertet ausschließlich persistierte Lauf-Indikatoren).
        await using (var db = OpenTestDbContext())
        {
            var project = await db.Projects.FirstAsync();
            db.WorkItems.Add(new WorkItem
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                Title = "Limit-FremdPrefix-Aufgabe",
                Status = WorkItemStatus.Started,
                ExecutionStatus = WorkItemExecutionStatus.Active,
                AiPluginPrefix = ForeignPrefix,
                ActiveRunId = Guid.NewGuid().ToString("N"),
                LastHeartbeatUtc = DateTimeOffset.UtcNow,
                CreatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        // Update-Sicherheitsprüfung gegen die laufende Test-DB: pausierte Limit-Aufgaben sind trotz
        // frischem Heartbeat nicht riskant (Heartbeat-Toleranz entfällt), die Fremd-Prefix-Aufgabe bleibt riskant.
        await using (var db = OpenTestDbContext())
        {
            var result = await CreateSafetyService(db).CheckAsync();
            Assert.DoesNotContain(result.RiskyTasks, t => t.Contains(LimitTitleA, StringComparison.Ordinal));
            Assert.DoesNotContain(result.RiskyTasks, t => t.Contains(LimitTitleB, StringComparison.Ordinal));
            Assert.Contains(result.RiskyTasks, t => t.Contains("Limit-FremdPrefix-Aufgabe", StringComparison.Ordinal));
        }

        // Nach Ablauf des Limits (abgelaufener Zeitstempel persistiert) greift die normale
        // Heartbeat-Bewertung wieder: die laufenden Simulator-Aufgaben sind wieder riskant.
        await using (var db = OpenTestDbContext())
        {
            var entry = await db.AppSettings
                .SingleAsync(e => e.Key == AiPluginLimitService.SessionLimitKeyPrefix + SimulatorPrefix);
            entry.Value = DateTimeOffset.UtcNow.AddMinutes(-5).ToString("O", System.Globalization.CultureInfo.InvariantCulture);
            entry.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
        }

        await using (var db = OpenTestDbContext())
        {
            var result = await CreateSafetyService(db).CheckAsync();
            Assert.Contains(result.RiskyTasks, t => t.Contains(LimitTitleA, StringComparison.Ordinal));
            Assert.Contains(result.RiskyTasks, t => t.Contains(LimitTitleB, StringComparison.Ordinal));
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
    private static TaskDetailView CreateAndStartLimitWorkItem(ProjectDetailView projectDetail, string title)
    {
        var task = projectDetail.CreateTask();
        task.SetTaskTitle(title);
        task.SaveTask();
        task.GoBack();

        var projectDetailAfterSave = Assert.IsType<ProjectDetailView>(task.Window.CurrentView());
        var taskReopened = projectDetailAfterSave.OpenTask(title);
        taskReopened.Start("Softwareschmiede.KiSimulator", forProjectUse: false);
        taskReopened.WaitForCliRunning();

        return taskReopened;
    }

    private static CliUpdateSafetyService CreateSafetyService(Softwareschmiede.Infrastructure.Data.SoftwareschmiedeDbContext db)
    {
        var workItemService = new WorkItemService(
            db,
            NullLogger<WorkItemService>.Instance,
            new TodoService(db, NullLogger<TodoService>.Instance));
        var appSettingService = new AppSettingService(db, NullLogger<AppSettingService>.Instance);
        var limitService = new AiPluginLimitService(
            db,
            appSettingService,
            new WorkItemRunDataChangedNotifier(),
            NullLogger<AiPluginLimitService>.Instance);
        return new CliUpdateSafetyService(workItemService, limitService, NullLogger<CliUpdateSafetyService>.Instance);
    }

    private static void AssertPausedUntilUtc(DateTimeOffset? pausedUntilUtc, DateTimeOffset expected, string title)
    {
        Assert.NotNull(pausedUntilUtc);
        Assert.True(
            Math.Abs((pausedUntilUtc!.Value - expected).TotalSeconds) < 2,
            $"PausiertBisUtc von '{title}' ('{pausedUntilUtc:O}') weicht vom Marker-Zeitpunkt '{expected:O}' ab.");
    }
}
