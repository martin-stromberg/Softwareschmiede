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
/// Symbolen, vollflächiger Kachelklick, verschachtelter To-do-Button ohne Navigation,
/// Moduswechsel (Datum/Repository/KI) in Seitenleiste und Dashboard inkl. Fallback-Gruppen,
/// Persistenz beider AppEinstellung-Schlüssel und Wiederherstellung nach App-Neustart.
/// </summary>
public partial class End2EndTest
{
    /// <param name="mainWindow">Das Hauptfenster der Anwendung.</param>
    /// <returns>Das Hauptfenster nach dem appinternen Neustart - der Uebergebene
    /// <paramref name="mainWindow"/> ist danach stale und darf von Folge-Szenarien
    /// nicht mehr verwendet werden.</returns>
    protected async Task<Window> AufgabenListe_KompakteKachelSymboleUndModuswechsel_E2E(Window mainWindow)
    {
        var projektName = $"Modi-Projekt {Guid.NewGuid():N}"[..30];
        var titelMitRepo = $"Modi-Repo-Aufgabe {Guid.NewGuid():N}"[..30];
        var titelOhneRepo = $"Modi-Frei-Aufgabe {Guid.NewGuid():N}"[..30];

        NavigateToProjects(mainWindow);
        CreateAndOpenProject(mainWindow, projektName);
        var projectDetail = new ProjectDetailView(mainWindow);
        var taskDetail = projectDetail.CreateTask();
        taskDetail.SetTaskTitle(titelMitRepo);
        taskDetail.SaveTask();
        taskDetail.WaitForPersisted();
        taskDetail.GoBack();

        var projectDetail2 = Assert.IsType<ProjectDetailView>(mainWindow.CurrentView());
        var taskDetail2 = projectDetail2.CreateTask();
        taskDetail2.SetTaskTitle(titelOhneRepo);
        taskDetail2.SaveTask();
        taskDetail2.WaitForPersisted();
        taskDetail2.GoBack();
        Assert.IsType<ProjectDetailView>(mainWindow.CurrentView());

        Guid aufgabeMitRepoId;
        Guid aufgabeOhneRepoId;
        await using (var db = OpenTestDbContext())
        {
            var projekt = await db.Projekte.SingleAsync(p => p.Name == projektName);
            var repo = new GitRepository
            {
                Id = Guid.NewGuid(),
                ProjektId = projekt.Id,
                RepositoryName = "modi-repo",
                RepositoryUrl = "https://example.invalid/modi-repo.git",
                PluginTyp = "LocalDirectoryPlugin",
                Aktiv = true
            };
            db.GitRepositories.Add(repo);

            var aufgabeMitRepo = await db.Aufgaben.SingleAsync(a => a.Titel == titelMitRepo);
            aufgabeMitRepo.Status = AufgabeStatus.Gestartet;
            aufgabeMitRepo.AusfuehrungsStatus = AufgabeAusfuehrungsStatus.Aktiv;
            aufgabeMitRepo.KiPluginPrefix = "Softwareschmiede.KiSimulator";
            aufgabeMitRepo.GitRepositoryId = repo.Id;
            aufgabeMitRepoId = aufgabeMitRepo.Id;

            var aufgabeOhneRepo = await db.Aufgaben.SingleAsync(a => a.Titel == titelOhneRepo);
            aufgabeOhneRepo.Status = AufgabeStatus.Gestartet;
            aufgabeOhneRepo.AusfuehrungsStatus = AufgabeAusfuehrungsStatus.Aktiv;
            aufgabeOhneRepoId = aufgabeOhneRepo.Id;

            db.Todos.Add(new Todo { Id = Guid.NewGuid(), AufgabeId = aufgabeMitRepo.Id, Beschreibung = "Offenes Todo", ErstellungsDatum = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        }

        var menu = new MenuView(mainWindow);
        WaitForElement(
            mainWindow.FindFirstDescendant(cf => cf.ByAutomationId("AktiveAufgabenListeMenue"))!,
            cf => cf.ByName($"AufgabeNavigieren:{titelMitRepo}"),
            Long);

        // Kompakte Kachel: KI-Glyph, SCM-Glyph und Status-Glyph lesbar; To-do-Button vorhanden.
        Assert.False(string.IsNullOrWhiteSpace(menu.GetActiveTaskKiPluginSymbol(titelMitRepo)));
        Assert.False(string.IsNullOrWhiteSpace(menu.GetActiveTaskScmPluginSymbol(titelMitRepo)));
        Assert.False(string.IsNullOrWhiteSpace(menu.GetActiveTaskStatusGlyph(titelMitRepo)));
        Assert.Equal("KI Simulator", menu.GetActiveTaskKiPluginName(titelMitRepo));

        // Verschachtelter To-do-Button: öffnet den Dialog, ohne die Kachel-Navigation auszulösen.
        menu.KlickeOffeneTodosButton(titelMitRepo);
        var todosDialog = new OpenTodosDialogView(mainWindow).ForceShow();
        Assert.False(new TaskDetailView(mainWindow).IsVisible,
            "Der To-do-Klick darf nicht die Kachel-Navigation auslösen.");
        todosDialog.ForceClose(false);

        // Kachelklick navigiert zur Aufgabendetailansicht (kein separater Pfeil-Button nötig).
        var detail = menu.NavigateToTask(titelMitRepo);
        WaitForElement(mainWindow, cf => cf.ByName("Zurück"), Short);
        detail.GoBack();

        // Moduswechsel Seitenleiste: Repository- und KI-Gruppierung inkl. Fallback-Gruppen.
        // Der Seitenleisten-Abschnitt "Aktive Aufgaben" ist nur auf Nicht-Dashboard-Ansichten
        // sichtbar (IsDashboardVisible) - daher zuerst in die Projektliste wechseln.
        menu.NavigateToProjects();
        menu.EnsureNavigationExpanded();
        menu.SetzeAufgabenListenModus("Repository");
        menu.WaitForAufgabenGruppe("modi-repo", Medium);
        menu.WaitForAufgabenGruppe("Ohne Repository", Medium);
        menu.SetzeAufgabenListenModus("KI");
        menu.WaitForAufgabenGruppe("KI Simulator", Medium);
        menu.WaitForAufgabenGruppe("Nicht ausgeführt", Medium);
        menu.SetzeAufgabenListenModus("Datum");
        var deadlineFlach = DateTime.UtcNow + Medium;
        while (DateTime.UtcNow < deadlineFlach && menu.GetAufgabenGruppenNamen().Length > 0)
            Thread.Sleep(200);
        Assert.Empty(menu.GetAufgabenGruppenNamen());

        // Moduswechsel Dashboard: eigene RadioButtons, eigener Persistenzschlüssel.
        var dashboard = menu.NavigateToDashboard();
        dashboard.SetzeAufgabenListenModus("Repository");
        dashboard.WaitForAufgabenGruppe("modi-repo", Medium);
        dashboard.WaitForAufgabenGruppe("Ohne Repository", Medium);

        await using (var db = OpenTestDbContext())
        {
            var eintraege = await db.AppEinstellungen
                .Where(e => e.Schluessel == AppEinstellungService.AktiveAufgabenListenModusKey
                            || e.Schluessel == AppEinstellungService.AktiveAufgabenListenModusDashboardKey)
                .ToDictionaryAsync(e => e.Schluessel, e => e.Wert);
            Assert.Equal("Repository", eintraege[AppEinstellungService.AktiveAufgabenListenModusDashboardKey]);
            Assert.True(
                !eintraege.ContainsKey(AppEinstellungService.AktiveAufgabenListenModusKey)
                || eintraege[AppEinstellungService.AktiveAufgabenListenModusKey] == "Datum",
                "Der Dashboard-Moduswechsel darf den Menü-Schlüssel nicht verändern.");
        }

        // Menü-Modus ebenfalls auf einen Nicht-Default setzen (Persistenz beider Schlüssel).
        // Zurück auf die Projektliste, damit der Seitenleisten-Abschnitt sichtbar ist.
        menu.NavigateToProjects();
        menu.EnsureNavigationExpanded();
        menu.SetzeAufgabenListenModus("Repository");
        menu.WaitForAufgabenGruppe("modi-repo", Medium);
        await using (var db = OpenTestDbContext())
        {
            var wert = await db.AppEinstellungen
                .Where(e => e.Schluessel == AppEinstellungService.AktiveAufgabenListenModusKey)
                .Select(e => e.Wert)
                .FirstOrDefaultAsync();
            Assert.Equal("Repository", wert);
        }

        // Neustart mit gleicher DB: beide Ansichten stellen den gespeicherten Modus wieder her.
        // Die App landet auf dem Dashboard - die Seitenleisten-Gruppen sind erst auf einer
        // Nicht-Dashboard-Ansicht sichtbar.
        var restartedWindow = RestartAppPreservingDatabase();
        var menuNachNeustart = new MenuView(restartedWindow);
        menuNachNeustart.NavigateToProjects();
        menuNachNeustart.WaitForAufgabenGruppe("modi-repo", Long);
        menuNachNeustart.WaitForAufgabenGruppe("Ohne Repository", Long);

        var dashboardNachNeustart = menuNachNeustart.NavigateToDashboard();
        dashboardNachNeustart.WaitForAufgabenGruppe("modi-repo", Long);
        dashboardNachNeustart.WaitForAufgabenGruppe("Ohne Repository", Long);

        // Abschluss: beide Modi auf Datum zurücksetzen, damit Folgeschritte die flache Liste sehen.
        // (Dashboard zeigt die Seitenleiste nicht - zur Projektliste wechseln.)
        menuNachNeustart.NavigateToProjects();
        menuNachNeustart.EnsureNavigationExpanded();
        menuNachNeustart.SetzeAufgabenListenModus("Datum");
        // Der Dashboard-Umschalter existiert nur auf der Dashboard-Ansicht - erst zurücknavigieren.
        menuNachNeustart.NavigateToDashboard();
        dashboardNachNeustart.SetzeAufgabenListenModus("Datum");

        // Aufräumen.
        var projectList = menuNachNeustart.NavigateToProjects();
        var projectDetailForDelete = projectList.OpenProject(projektName);
        projectDetailForDelete.DeleteProject();
        projectDetailForDelete.Menu.NavigateToDashboard();

        return restartedWindow;
    }
}
