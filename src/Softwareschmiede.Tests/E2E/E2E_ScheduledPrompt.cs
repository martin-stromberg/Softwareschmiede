using FlaUI.Core.AutomationElements;
using Microsoft.EntityFrameworkCore;
using Softwareschmiede.Tests.E2E.Views;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// E2E-Test: Zeitgesteuerter Prompt-Versand über das CLI-Ribbon der Aufgabendetailansicht.
///
/// Voraussetzungen:
/// - Windows-Desktop-Session (kein Headless-CI), Windows 10 Build 17763 oder neuer
/// - Softwareschmiede.App muss im Debug-Modus gebaut sein
///
/// CI-Regular-Lauf: dotnet test --filter "Category!=OsInterface"
/// </summary>
public partial class End2EndTest
{
    /// <summary>
    /// Szenario: Nutzer startet die CLI, trägt eine in der Zukunft liegende Zielzeit ein, wählt eine
    /// Promptvorlage und klickt "Zeitgesteuert senden". Die Statusanzeige "Prompt in Wartestellung"
    /// muss erscheinen, ohne dass ein Fehlerbanner sichtbar wird. Der Plan überlebt das Verlassen
    /// der Detailansicht (persistierter Datensatz), ist beim Wiederöffnen weiterhin sichtbar und
    /// lässt sich über den Button "Wartestellung aufheben" stornieren (Anzeige weg, Datensatz gelöscht).
    /// </summary>
    protected async Task ScheduledPrompt_AfterPlan_ShowsQueueStatus_E2E(Window mainWindow)
    {
        SkipWhenConPtyNotAvailable();
        ConfirmLocalDirectoryGitInitInSourceDirectory();

        SetupProjectWithNewWorkItem(mainWindow, "ZeitgesteuertPrompt-Repo", "ZeitgesteuertPrompt-Projekt");
        var taskDetail = new TaskDetailView(mainWindow).Start("Softwareschmiede.KiSimulator", forProjectUse: false);
        taskDetail.WaitForCliRunning();

        // Die App berechnet die Zielzeit stets aus dem heutigen Datum. Läge "jetzt + 5 Minuten" bereits am
        // nächsten Tag (kurz vor Mitternacht), wäre die daraus resultierende heutige Uhrzeit bereits vergangen,
        // wodurch die App sofort statt zeitgesteuert versendet und die erwartete Wartestellung-Anzeige ausbleibt.
        // Da dieser E2E-Test die reale Systemuhr nutzt (kein injizierbarer TimeProvider in der App-UI-Schicht),
        // wird die Zielzeit in diesem Fall auf den letztmöglichen Zeitpunkt des heutigen Tages begrenzt.
        // In der letzten Tagesminute (23:59:00-23:59:59) läge auch diese Begrenzung bereits in der
        // Vergangenheit, weshalb der Test in diesem terminalen 1-Minuten-Fenster übersprungen wird,
        // statt eine unerreichbare Zielzeit zu wählen.
        var now = DateTime.Now;
        Assert.SkipWhen(now.Hour == 23 && now.Minute >= 59, "Letzte Tagesminute: Mitternachts-Guard würde eine bereits vergangene Zielzeit liefern.");

        var targettimeCandidate = now.AddMinutes(5);
        var targettime = targettimeCandidate.Date == now.Date
            ? targettimeCandidate
            : new DateTime(now.Year, now.Month, now.Day, 23, 59, 0);

        taskDetail.SetScheduledPromptTime(targettime.Hour, targettime.Minute);
        taskDetail.SelectPromptTemplate("Weitermachen");

        // Erscheint erst, sobald ScheduledPromptStatus gesetzt ist (NullOrEmptyToVisibilityConverter).
        taskDetail.SendScheduledPrompt();

        Assert.False(new ErrorView(mainWindow).IsVisible);

        // Detailansicht verlassen — der persistierte Plan darf dadurch nicht verloren gehen.
        taskDetail.GoBack();
        var projectDetail = Assert.IsType<ProjectDetailView>(mainWindow.CurrentView());

        await using (var db = OpenTestDbContext())
        {
            var workItem = await db.WorkItems.SingleAsync();
            var record = await db.ScheduledPrompts.SingleOrDefaultAsync(z => z.WorkItemId == workItem.Id);
            Assert.NotNull(record);
        }

        // Wiederöffnen: Wartestellung und Storno-Button müssen weiterhin sichtbar sein.
        var taskDetailReopened = projectDetail.OpenFirstTask();
        taskDetailReopened.WaitForScheduledPromptStatus();

        // Manueller Storno: Anzeige verschwindet und der Datensatz wird gelöscht.
        taskDetailReopened.CancelScheduledPrompt();

        await using (var db = OpenTestDbContext())
        {
            Assert.False(await db.ScheduledPrompts.AnyAsync());
        }

        taskDetailReopened.ForceClose(recurseToDashboard: false);
        var projectDetailAfterCancellation = Assert.IsType<ProjectDetailView>(mainWindow.CurrentView());
        projectDetailAfterCancellation.DeleteProject();
        projectDetailAfterCancellation.Menu.NavigateToDashboard();
    }
}
