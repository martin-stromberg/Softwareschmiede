using FlaUI.Core.AutomationElements;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Tests.E2E.Views;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// E2E-Tests für die To-Do-Liste der Aufgabendetailansicht (Issue 103).
///
/// Voraussetzungen:
/// - Windows-Desktop-Session (kein Headless-CI)
/// - Softwareschmiede.App muss im Debug-Modus gebaut sein
///
/// CI-Regular-Lauf: dotnet test --filter "Category!=OsInterface"
/// </summary>
public partial class End2EndTest
{
    /// <summary>
    /// Szenario (auf derselben Aufgabeninstanz durchlaufen, um die Laufzeit der FlaUI-Suite gering zu
    /// halten): Todo-Tab öffnen (leer), drei To-Dos erstellen (Badge zeigt "3"), eines löschen (Badge zeigt
    /// "2", Eintrag verschwindet aus der Liste), eines abhaken (visuelle Änderung + Badge zeigt "1"),
    /// Aufgabenabschluss mit dem verbleibenden offenen To-Do wird blockiert (Fehlermeldung mit Anzahl),
    /// danach auch das letzte To-Do abhaken — danach ist der Abschluss erlaubt (Badge verschwindet, Status
    /// wechselt auf "Beendet").
    /// </summary>
    /// <param name="mainWindow">Das Hauptfenster der Anwendung.</param>
    protected void Todo_ErstellenAbhakenLoeschenUndAbschlussValidierung_E2E(Window mainWindow)
    {
        var projectList = new ProjectListView(mainWindow).ForceShow();
        projectList.CreateProject("Todo-Test");
        var projectDetail = projectList.OpenProject("Todo-Test");

        var taskDetail = projectDetail.CreateTask();
        taskDetail.SetTaskTitle("Todo-Testaufgabe");
        taskDetail.SaveTask();
        taskDetail.GoBack();
        var projectDetailAfterSave = Assert.IsType<ProjectDetailView>(mainWindow.CurrentView());
        taskDetail = projectDetailAfterSave.OpenTask("Todo-Testaufgabe");

        // Todo-Tab öffnen: Todo-Liste ist leer, Badge nicht sichtbar
        var todoList = new TodoListView(mainWindow).ForceShow();
        Assert.False(todoList.HasOpenCountBadge());

        // Drei To-Dos erstellen
        todoList.CreateTodo("Erstes Todo");
        todoList.CreateTodo("Zweites Todo");
        todoList.CreateTodo("Zu löschendes Todo");
        todoList.WaitForTodo("Erstes Todo");
        todoList.WaitForTodo("Zweites Todo");
        todoList.WaitForTodo("Zu löschendes Todo");

        // Badge zeigt 3 offene To-Dos
        Assert.Equal("3", todoList.GetOpenCount());

        // Drittes Todo löschen → verschwindet aus der Liste, Badge zeigt 2
        todoList.DeleteTodo("Zu löschendes Todo");
        Assert.Equal("2", todoList.GetOpenCount());
        Assert.False(todoList.HasTodo("Zu löschendes Todo"));

        // Erstes Todo abhaken
        todoList.CheckOff("Erstes Todo");

        // Badge zeigt nur noch 1 offenes Todo
        Assert.Equal("1", todoList.GetOpenCount());

        // Aufgabe direkt in der Test-Datenbank auf Status "Gestartet" setzen, um "Beenden" zu ermöglichen,
        // ohne einen echten CLI-/Klon-Vorgang durchführen zu müssen (nicht Gegenstand dieses Szenarios).
        SetzeAufgabeStatusGestartet("Todo-Testaufgabe");

        taskDetail.GoBack();
        var projectDetailReloaded = Assert.IsType<ProjectDetailView>(mainWindow.CurrentView());
        taskDetail = projectDetailReloaded.OpenTask("Todo-Testaufgabe");

        todoList = new TodoListView(mainWindow).ForceShow();

        // Abschluss mit noch einem offenen Todo wird blockiert
        taskDetail.Finish();
        Assert.Contains("1 offene To-Do(s)", new ErrorView(mainWindow).GetErrorMessage());
        Assert.True(taskDetail.IsTaskStarted());

        // Zweites Todo abhaken → keine offenen To-Dos mehr, Badge verschwindet
        todoList.CheckOff("Zweites Todo");
        todoList.WaitUntilBadgeGone();

        // Abschluss ist nun erlaubt
        taskDetail.Finish();
        taskDetail.WaitForFinished();

        taskDetail.ForceClose(recurseToDashboard: false);
        var projectDetailFinal = Assert.IsType<ProjectDetailView>(mainWindow.CurrentView());
        projectDetailFinal.DeleteProject();
    }

    private void SetzeAufgabeStatusGestartet(string aufgabeTitel)
    {
        using var db = OpenTestDbContext();
        var aufgabe = db.Aufgaben.Single(a => a.Titel == aufgabeTitel);
        aufgabe.Status = AufgabeStatus.Gestartet;
        db.SaveChanges();
    }
}
