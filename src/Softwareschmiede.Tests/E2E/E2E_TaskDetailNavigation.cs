using FlaUI.Core.AutomationElements;
using Softwareschmiede.Tests.E2E.Views;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// E2E-Tests für die separate, fensterumfassende Aufgabendetailansicht (Feature 72).
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
    /// Szenario: Neue Aufgabe anlegen (korrekte Daten prüfen), über "Zurück" zur ProjectDetailView
    /// zurücknavigieren, anschließend die Aufgabe per Doppelklick aus der Aufgabenliste erneut öffnen.
    /// Prüft: Die TaskDetailView zeigt beim Anlegen den korrekten Standardtitel; "Zurück" navigiert zur
    /// ProjectDetailView zurück; das Öffnen aus der Liste zeigt die TaskDetailView fensterumfassend
    /// (ProjectDetailView nicht mehr sichtbar).
    /// </summary>
    /// <param name="mainWindow">Das Hauptfenster der Anwendung.</param>
    protected void TaskDetail_ZeigtDaten_Zurueck_UndOeffnenFensterumfassend_E2E(Window mainWindow)
    {
        var projectList = new ProjectListView(mainWindow).ForceShow();
        projectList.CreateProject("TaskNav-Test");
        var projectDetail = projectList.OpenProject("TaskNav-Test");

        // Korrekte Daten
        var taskDetail = projectDetail.CreateTask();
        Assert.Equal("Neue Aufgabe", taskDetail.GetTaskTitle());

        // Rücknavigation
        taskDetail.GoBack();
        var projectDetailAfterBack = Assert.IsType<ProjectDetailView>(mainWindow.CurrentView());

        // Fensterumfassendes Öffnen aus der Liste
        var items = projectDetailAfterBack.GetTaskElements();
        Assert.True(items.Length >= 1, "Aufgabenliste sollte mindestens eine Aufgabe enthalten.");
        var taskDetailReopened = projectDetailAfterBack.OpenFirstTask();

        // TaskDetailView zeigt eigenes Ribbon mit "Speichern"-Button (Edit-Panel bei Status Neu)
        Assert.True(taskDetailReopened.IsVisible);

        // ProjectDetailView (u. a. "ProjektName") ist nicht mehr sichtbar - fensterumfassend
        Assert.False(new ProjectDetailView(mainWindow).IsVisible);
    }
}
