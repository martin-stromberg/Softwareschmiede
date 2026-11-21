using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using Microsoft.EntityFrameworkCore;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Tests.E2E.Views;
using Softwareschmiede.Tests.E2E.Views.Dialogs;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// E2E-Test für das manuelle Pausieren und Aufheben einer regulären Aufgabe (Issue 151):
/// Ribbon-Button „Pause einstellen" → modaler Dialog mit Vorbelegung → Kachel-Status
/// „⏸ Pausiert (noch …)" mit Abblendungs-Marker → vorzeitiges Aufheben.
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
    /// Szenario: Eine per Datenbank-Seed auf „Gestartet"/„Aktiv" gesetzte Aufgabe wird über den
    /// Ribbon-Button „Pause einstellen" pausiert (Dialog-Vorbelegung ≈ aktueller Zeitpunkt,
    /// Zielzeitpunkt +2 h). Die Seitenleisten-Kachel zeigt anschließend „⏸ Pausiert (noch …)" und den
    /// Abblendungs-Marker „Paused:True" im HelpText. Ein erneut geöffneter Dialog zeigt die aktive
    /// Pause; „Pause aufheben" stellt den Normalstatus („Paused:False") wieder her.
    /// </summary>
    /// <param name="mainWindow">Das Hauptfenster der Anwendung.</param>
    protected async Task PauseWorkItem_DialogCountdownDimmingAndLift_E2E(Window mainWindow)
    {
        var projectName = $"Pause-Projekt {Guid.NewGuid():N}"[..30];
        var title = $"Pause-Aufgabe {Guid.NewGuid():N}"[..30];

        // Projekt und Aufgabe über die UI anlegen; ein CLI-Start ist für die Pause nicht nötig.
        NavigateToProjects(mainWindow);
        CreateAndOpenProject(mainWindow, projectName);
        var projectDetail = new ProjectDetailView(mainWindow);
        var taskDetail = projectDetail.CreateTask();
        taskDetail.SetTaskTitle(title);
        taskDetail.SaveTask();
        taskDetail.WaitForPersisted();

        // Aufgabe direkt in der Test-DB auf Started/Aktiv setzen, damit sie als aktive Aufgabe
        // in der Seitenleiste erscheint (über die UI nicht abbildbar ohne echten CLI-Start).
        await using (var db = OpenTestDbContext())
        {
            var workItem = await db.WorkItems.SingleAsync(a => a.Title == title);
            workItem.Status = WorkItemStatus.Started;
            workItem.ExecutionStatus = WorkItemExecutionStatus.Active;
            workItem.AiPluginPrefix = "Softwareschmiede.KiSimulator";
            await db.SaveChangesAsync();
        }

        // Die Seitenleiste aktualisiert sich über den 5-s-Timer; auf die Kachel warten.
        // Anker ist 'WorkItemNavigate:{titel}' — seit der Kachel-Umstellung (Issue 288) ist das
        // die gesamte klickbare Kachel (vollflächiger Button). Er existiert in beiden
        // ActiveTasksListControl-Instanzen; die Suche läuft gescoped unter 'ActiveWorkItemsListMenu'.
        WaitForElement(
            mainWindow,
            cf => cf.ByName($"WorkItemNavigate:{title}"),
            Long);

        // Über die Seitenleiste in die (frisch geladene) Detailansicht wechseln. Als
        // Synchronisationssignal auf den „Zurück"-Button der TaskDetailView warten — der Aufgabentitel
        // allein wäre bereits über die Seitenleisten-Kachel im Automation-Baum auffindbar.
        var detail = taskDetail.Menu.NavigateToTask(title);
        WaitForElement(mainWindow, cf => cf.ByName("Back"), Short);

        // Das Ribbon enthält nur die regulären Aufgabensteuerungen (Starten, Pause einstellen).
        WaitForElement(mainWindow, cf => cf.ByName("Start"), Short);
        WaitForElement(mainWindow, cf => cf.ByName("SetPause"), Short);
        Assert.Null(mainWindow.FindFirstDescendant(cf => cf.ByName("AutonomousWorkItemInitialize")));
        Assert.Null(mainWindow.FindFirstDescendant(cf => cf.ByName("Autonome Aufgabe")));

        // Phase 1: Dialog öffnen — Vorbelegung muss ≈ dem aktuellen Zeitpunkt entsprechen.
        var pauseDialog = new WorkItemPauseDialogView(mainWindow).ForceShow();
        var (hour, minute) = pauseDialog.GetPrefilledTime();
        var now = DateTime.Now;
        var prefillMinutes = hour * 60 + minute;
        var nowMinutes = now.Hour * 60 + now.Minute;
        var diffMinutes = Math.Abs(prefillMinutes - nowMinutes);
        Assert.True(
            Math.Min(diffMinutes, 24 * 60 - diffMinutes) <= 1,
            $"Vorbelegung ({hour:00}:{minute:00}) weicht zu stark vom aktuellen Zeitpunkt ({now:HH:mm}) ab.");
        Assert.False(pauseDialog.IsLiftEnabled(), "Ohne bestehende Pause muss 'Pause aufheben' deaktiviert sein.");

        // Phase 2: Zeitpunkt +2 h bestätigen → Pause wird persistiert und in der Kachel sichtbar.
        var target = DateTime.Now.AddHours(2);
        pauseDialog.SetTimestamp(target);
        pauseDialog.Confirm();

        await using (var db = OpenTestDbContext())
        {
            var workItem = await db.WorkItems.SingleAsync(a => a.Title == title);
            Assert.NotNull(workItem.PausedUntilUtc);
            Assert.True(
                Math.Abs((workItem.PausedUntilUtc.Value - new DateTimeOffset(target)).TotalMinutes) <= 1.5,
                $"PausiertBisUtc '{workItem.PausedUntilUtc:O}' weicht vom erwarteten Ziel '{target:O}' ab.");
        }

        // Kachel zeigt „⏸ Pausiert (noch …)" (HelpText des Status-Elements) und „Paused:True"
        // (HelpText des 'WorkItemCard:'-Marker-Elements — E2E-Anker für die Opacity-Abblendung).
        WaitOnTileStatus(mainWindow, title, status => status.StartsWith("⏸ Pausiert", StringComparison.Ordinal), Long);
        WaitOnTilePausedMarker(mainWindow, title, "Paused:True", Long);

        // Phase 3: Dialog erneut öffnen — die bestehende Pause wird angezeigt, „Pause aufheben" ist aktiv.
        var pauseDialog2 = new WorkItemPauseDialogView(mainWindow).ForceShow();
        Assert.True(pauseDialog2.IsLiftEnabled(), "Bei bestehender Pause muss 'Pause aufheben' aktiviert sein.");
        pauseDialog2.WaitForCurrentPauseDisplay();

        // Phase 4: Vorzeitiges Aufheben → Normalstatus und „Paused:False" kehren zurück.
        pauseDialog2.Lift();

        await using (var db = OpenTestDbContext())
        {
            var workItem = await db.WorkItems.SingleAsync(a => a.Title == title);
            Assert.Null(workItem.PausedUntilUtc);
        }

        WaitOnTileStatus(mainWindow, title, status => !status.StartsWith("⏸ Pausiert", StringComparison.Ordinal), Long);
        WaitOnTilePausedMarker(mainWindow, title, "Paused:False", Long);

        // Aufräumen: Die Aufgabe hat keinen laufenden CLI-Prozess; das Projekt kann regulär gelöscht
        // werden. Die Detailansicht wurde über die Seitenleiste geöffnet — „Zurück" führt zum Dashboard.
        detail.ForceClose(recurseToDashboard: false);
        var dashboard = Assert.IsType<DashboardView>(mainWindow.CurrentView());
        var projectList = dashboard.Menu.NavigateToProjects();
        var projectDetailForDelete = projectList.OpenProject(projectName);
        projectDetailForDelete.DeleteProject();
        projectDetailForDelete.Menu.NavigateToDashboard();
    }

    /// <summary>
    /// Wartet, bis der <c>HelpText</c> des Kachel-Status-Elements <c>WorkItemStatus:{titel}</c> das
    /// gegebene Prädikat erfüllt (Polling, da die Seitenleiste sich über den 5-s-Timer bzw. den
    /// Laufdaten-Notifier aktualisiert).
    /// </summary>
    private static void WaitOnTileStatus(Window mainWindow, string title, Func<string, bool> predikat, TimeSpan timeout)
        => WaitOnElementHelpText(mainWindow, $"WorkItemStatus:{title}", predikat, timeout, $"Statuskachel von '{title}'");

    /// <summary>
    /// Wartet, bis der <c>HelpText</c> des Marker-Elements <c>WorkItemCard:{titel}</c> den erwarteten
    /// <c>Paused:…</c>-Marker enthält (E2E-Anker für die Opacity-Abblendung pausierter Kacheln —
    /// die Kachel-Border selbst erzeugt keinen AutomationPeer und ist per UIA unsichtbar).
    /// </summary>
    private static void WaitOnTilePausedMarker(Window mainWindow, string title, string expectedMarker, TimeSpan timeout)
        => WaitOnElementHelpText(
            mainWindow,
            $"WorkItemCard:{title}",
            h => h.Contains(expectedMarker, StringComparison.Ordinal),
            timeout,
            $"Kachel von '{title}' mit Marker '{expectedMarker}'");

    /// <summary>
    /// Wartet per Polling, bis der <c>HelpText</c> des benannten Elements das gegebene Prädikat erfüllt
    /// (die Seitenleiste aktualisiert sich über den 5-s-Timer bzw. den Laufdaten-Notifier).
    /// </summary>
    private static void WaitOnElementHelpText(Window mainWindow, string elementName, Func<string, bool> predikat, TimeSpan timeout, string description)
    {
        var deadline = DateTime.UtcNow + timeout;
        string? lastHelpText = null;
        while (DateTime.UtcNow < deadline)
        {
            var element = mainWindow.FindFirstDescendant(cf => cf.ByName(elementName));
            lastHelpText = element?.HelpText;
            if (lastHelpText is not null && predikat(lastHelpText))
                return;

            Thread.Sleep(200);
        }

        throw new TimeoutException(
            $"{description} erfüllte innerhalb von {timeout.TotalSeconds}s nicht das erwartete Prädikat. Zuletzt gesehen: '{lastHelpText}'.");
    }
}
