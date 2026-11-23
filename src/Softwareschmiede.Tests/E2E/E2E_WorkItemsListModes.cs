using FlaUI.Core.AutomationElements;
using Microsoft.EntityFrameworkCore;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Tests.E2E.Views;
using Softwareschmiede.Tests.E2E.Views.Dialogs;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// Konsolidierter E2E-Test für die kompakte Aufgabenliste (Issue 288): dreizeilige Kachel mit
/// Symbolen und Auftragsart-Badge (Symbol + Klartext), vollflächiger Kachelklick,
/// verschachtelter To-do-Button ohne Navigation, Moduswechsel (Datum/Repository/KI/Art)
/// in Seitenleiste und Dashboard inkl. Fallback-Gruppen, Persistenz beider
/// AppEinstellung-Schlüssel und Wiederherstellung nach App-Neustart.
/// </summary>
public partial class End2EndTest
{
    /// <param name="mainWindow">Das Hauptfenster der Anwendung.</param>
    /// <returns>Das Hauptfenster nach dem appinternen Neustart - der Uebergebene
    /// <paramref name="mainWindow"/> ist danach stale und darf von Folge-Szenarien
    /// nicht mehr verwendet werden.</returns>
    protected async Task<Window> WorkItemsList_CompactTileSymbolAndModeswitch_E2E(Window mainWindow)
    {
        var projectName = $"Modi-Projekt {Guid.NewGuid():N}"[..30];
        var titleWithRepo = $"Modi-Repo-Aufgabe {Guid.NewGuid():N}"[..30];
        var titleWithoutRepo = $"Modi-Frei-Aufgabe {Guid.NewGuid():N}"[..30];

        NavigateToProjects(mainWindow);
        CreateAndOpenProject(mainWindow, projectName);
        var projectDetail = new ProjectDetailView(mainWindow);
        var taskDetail = projectDetail.CreateTask();
        taskDetail.SetTaskTitle(titleWithRepo);
        taskDetail.SaveTask();
        taskDetail.WaitForPersisted();
        taskDetail.GoBack();

        var projectDetail2 = Assert.IsType<ProjectDetailView>(mainWindow.CurrentView());
        var taskDetail2 = projectDetail2.CreateTask();
        taskDetail2.SetTaskTitle(titleWithoutRepo);
        taskDetail2.SaveTask();
        taskDetail2.WaitForPersisted();
        taskDetail2.GoBack();
        Assert.IsType<ProjectDetailView>(mainWindow.CurrentView());

        Guid workItemWithRepoId;
        Guid workItemWithoutRepoId;
        await using (var db = OpenTestDbContext())
        {
            var project = await db.Projects.SingleAsync(p => p.Name == projectName);
            var repo = new GitRepository
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                RepositoryName = "modi-repo",
                RepositoryUrl = "https://example.invalid/modi-repo.git",
                PluginType = "LocalDirectoryPlugin",
                Active = true
            };
            db.GitRepositories.Add(repo);

            var workItemWithRepo = await db.WorkItems.SingleAsync(a => a.Title == titleWithRepo);
            workItemWithRepo.Status = WorkItemStatus.Started;
            workItemWithRepo.ExecutionStatus = WorkItemExecutionStatus.Active;
            workItemWithRepo.AiPluginPrefix = "Softwareschmiede.KiSimulator";
            workItemWithRepo.GitRepositoryId = repo.Id;
            workItemWithRepoId = workItemWithRepo.Id;

            var workItemWithoutRepo = await db.WorkItems.SingleAsync(a => a.Title == titleWithoutRepo);
            workItemWithoutRepo.Status = WorkItemStatus.Started;
            workItemWithoutRepo.ExecutionStatus = WorkItemExecutionStatus.Active;
            workItemWithoutRepo.Kind = WorkItemKind.Alert;
            workItemWithoutRepoId = workItemWithoutRepo.Id;

            db.Todos.Add(new Todo { Id = Guid.NewGuid(), WorkItemId = workItemWithRepo.Id, Description = "Offenes Todo", CreatedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        }

        var menu = new MenuView(mainWindow);
        WaitForElement(
            mainWindow.FindFirstDescendant(cf => cf.ByAutomationId("ActiveWorkItemsListMenu"))!,
            cf => cf.ByName($"WorkItemNavigate:{titleWithRepo}"),
            Long);

        // Kompakte Kachel: KI-Glyph, SCM-Glyph und Status-Glyph lesbar; To-do-Button vorhanden.
        Assert.False(string.IsNullOrWhiteSpace(menu.GetActiveTaskAiPluginSymbol(titleWithRepo)));
        Assert.False(string.IsNullOrWhiteSpace(menu.GetActiveTaskScmPluginSymbol(titleWithRepo)));
        Assert.False(string.IsNullOrWhiteSpace(menu.GetActiveTaskStatusGlyph(titleWithRepo)));
        Assert.Equal("KI Simulator", menu.GetActiveTaskAiPluginName(titleWithRepo));

        // Auftragsart-Badge: Symbol + Klartext je Kachel (manuell angelegt → "✋ Manuell",
        // per DB auf Alert gesetzt → "⚠ Alert").
        Assert.Equal("✋", menu.GetActiveTaskKindSymbol(titleWithRepo));
        Assert.Equal("Manuell", menu.GetActiveTaskKindLabel(titleWithRepo));
        Assert.Equal("⚠", menu.GetActiveTaskKindSymbol(titleWithoutRepo));
        Assert.Equal("Alert", menu.GetActiveTaskKindLabel(titleWithoutRepo));

        // Verschachtelter To-do-Button: öffnet den Dialog, ohne die Kachel-Navigation auszulösen.
        menu.ClickOpenTodosButton(titleWithRepo);
        var todosDialog = new OpenTodosDialogView(mainWindow).ForceShow();
        Assert.False(new TaskDetailView(mainWindow).IsVisible,
            "Der To-do-Klick darf nicht die Kachel-Navigation auslösen.");
        todosDialog.ForceClose(false);

        // Kachelklick navigiert zur Aufgabendetailansicht (kein separater Pfeil-Button nötig).
        var detail = menu.NavigateToTask(titleWithRepo);
        WaitForElement(mainWindow, cf => cf.ByName("Back"), Short);
        detail.GoBack();

        // Moduswechsel Seitenleiste: Repository- und KI-Gruppierung inkl. Fallback-Gruppen.
        // Der Seitenleisten-Abschnitt "Aktive Aufgaben" ist nur auf Nicht-Dashboard-Ansichten
        // sichtbar (IsDashboardVisible) - daher zuerst in die Projektliste wechseln.
        menu.NavigateToProjects();
        menu.EnsureNavigationExpanded();
        menu.SetWorkItemsListsMode("Repository");
        menu.WaitForWorkItemsGroup("modi-repo", Medium);
        menu.WaitForWorkItemsGroup("Ohne Repository", Medium);
        menu.SetWorkItemsListsMode("AI");
        menu.WaitForWorkItemsGroup("KI Simulator", Medium);
        menu.WaitForWorkItemsGroup("Nicht ausgeführt", Medium);
        // Art-Gruppierung: Klartext-Gruppenköpfe ohne Symbol.
        menu.SetWorkItemsListsMode("Kind");
        menu.WaitForWorkItemsGroup("Manuell", Medium);
        menu.WaitForWorkItemsGroup("Alert", Medium);
        menu.SetWorkItemsListsMode("Date");
        var deadlineFlach = DateTime.UtcNow + Medium;
        while (DateTime.UtcNow < deadlineFlach && menu.GetWorkItemsGroupsNames().Length > 0)
            Thread.Sleep(200);
        Assert.Empty(menu.GetWorkItemsGroupsNames());

        // Moduswechsel Dashboard: eigene RadioButtons, eigener Persistenzschlüssel.
        var dashboard = menu.NavigateToDashboard();
        dashboard.SetWorkItemsListsMode("Repository");
        dashboard.WaitForWorkItemsGroup("modi-repo", Medium);
        dashboard.WaitForWorkItemsGroup("Ohne Repository", Medium);

        await using (var db = OpenTestDbContext())
        {
            var entries = await db.AppSettings
                .Where(e => e.Key == AppSettingService.ActiveWorkItemsListModeKey
                            || e.Key == AppSettingService.ActiveWorkItemsListModeDashboardKey)
                .ToDictionaryAsync(e => e.Key, e => e.Value);
            Assert.Equal("Repository", entries[AppSettingService.ActiveWorkItemsListModeDashboardKey]);
            Assert.True(
                !entries.ContainsKey(AppSettingService.ActiveWorkItemsListModeKey)
                || entries[AppSettingService.ActiveWorkItemsListModeKey] == "Datum",
                "Der Dashboard-Moduswechsel darf den Menü-Schlüssel nicht verändern.");
        }

        // Menü-Modus ebenfalls auf einen Nicht-Default setzen (Persistenz beider Schlüssel).
        // Zurück auf die Projektliste, damit der Seitenleisten-Abschnitt sichtbar ist.
        menu.NavigateToProjects();
        menu.EnsureNavigationExpanded();
        menu.SetWorkItemsListsMode("Repository");
        menu.WaitForWorkItemsGroup("modi-repo", Medium);
        await using (var db = OpenTestDbContext())
        {
            var value = await db.AppSettings
                .Where(e => e.Key == AppSettingService.ActiveWorkItemsListModeKey)
                .Select(e => e.Value)
                .FirstOrDefaultAsync();
            Assert.Equal("Repository", value);
        }

        // Neustart mit gleicher DB: beide Ansichten stellen den gespeicherten Modus wieder her.
        // Die App landet auf dem Dashboard - die Seitenleisten-Gruppen sind erst auf einer
        // Nicht-Dashboard-Ansicht sichtbar.
        var restartedWindow = RestartAppPreservingDatabase();
        var menuAfterRestart = new MenuView(restartedWindow);
        menuAfterRestart.NavigateToProjects();
        menuAfterRestart.WaitForWorkItemsGroup("modi-repo", Long);
        menuAfterRestart.WaitForWorkItemsGroup("Ohne Repository", Long);

        var dashboardAfterRestart = menuAfterRestart.NavigateToDashboard();
        dashboardAfterRestart.WaitForWorkItemsGroup("modi-repo", Long);
        dashboardAfterRestart.WaitForWorkItemsGroup("Ohne Repository", Long);

        // Abschluss: beide Modi auf Datum zurücksetzen, damit Folgeschritte die flache Liste sehen.
        // (Dashboard zeigt die Seitenleiste nicht - zur Projektliste wechseln.)
        menuAfterRestart.NavigateToProjects();
        menuAfterRestart.EnsureNavigationExpanded();
        menuAfterRestart.SetWorkItemsListsMode("Date");
        // Der Dashboard-Umschalter existiert nur auf der Dashboard-Ansicht - erst zurücknavigieren.
        menuAfterRestart.NavigateToDashboard();
        dashboardAfterRestart.SetWorkItemsListsMode("Date");

        // Aufräumen.
        var projectList = menuAfterRestart.NavigateToProjects();
        var projectDetailForDelete = projectList.OpenProject(projectName);
        projectDetailForDelete.DeleteProject();
        projectDetailForDelete.Menu.NavigateToDashboard();

        return restartedWindow;
    }
}
