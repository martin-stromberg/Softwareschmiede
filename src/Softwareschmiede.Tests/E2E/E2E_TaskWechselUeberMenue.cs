using FlaUI.Core.AutomationElements;
using Softwareschmiede.Tests.E2E.Views;
using Softwareschmiede.Tests.E2E.Views.Dialogs;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// E2E-Regressionstest für den Kundenbug: "Ist eine Aufgabe geöffnet und wählt der Anwender eine
/// andere Aufgabe aus der Aufgabenliste im Menü aus, so bleibt die geöffnete Aufgabe geöffnet und die
/// neue Aufgabe wird nicht angezeigt. Lediglich in der Fußzeile ändert die Ansicht auf den Namen der
/// zweiten Aufgabe. Die geöffnete CLI ist aber weiterhin diejenige der zuvor geöffneten Aufgabe."
///
/// Ursache: TaskDetailView wird über eine DataType-DataTemplate-Zuordnung in MainWindow.xaml
/// eingebunden. Wechselt MainWindowViewModel.CurrentView zwischen zwei TaskDetailViewModel-Instanzen
/// (gleicher Typ), erzeugt WPF keine neue TaskDetailView-Instanz und feuert daher weder Loaded noch
/// Unloaded — nur die XAML-Bindings (Titel, Fußzeile) aktualisieren sich reaktiv. Die im Code-Behind
/// nur in den Loaded/Unloaded-Handlern gesetzte TerminalControl.Session blieb dadurch auf der
/// vorherigen Aufgabe stehen.
///
/// Testbarkeit: Die tatsächlich im TerminalControl eingebettete Prozess-ID wird über
/// AutomationProperties.HelpText offengelegt (siehe TaskDetailView.xaml.cs), damit der Test
/// unabhängig von der (custom-gezeichneten) Terminal-Darstellung verifizieren kann, welcher
/// CLI-Prozess tatsächlich angezeigt wird.
///
/// Voraussetzungen:
/// - Windows-Desktop-Session (kein Headless-CI)
/// - Softwareschmiede.App muss im Debug-Modus gebaut sein
/// - Im Test-Modus steht ausschließlich das LocalDirectoryPlugin als SCM-Plugin zur Verfügung.
///
/// CI-Regular-Lauf: dotnet test --filter "Category!=OsInterface"
/// </summary>
public partial class End2EndTest
{
    private const string TitelA = "Aufgabe-A-Wechseltest";
    private const string TitelB = "Aufgabe-B-Wechseltest";

    /// <summary>
    /// Szenario: Aufgabe A ist geöffnet und ihre CLI läuft. Ohne über "Zurück" zu navigieren, wählt
    /// der Anwender über die Aufgabenliste in der Seitenleiste ("Aktive Aufgaben") Aufgabe B aus.
    /// Prüft: Danach wird tatsächlich Aufgabe B angezeigt — inklusive der zu Aufgabe B gehörenden CLI
    /// (eigene Prozess-ID), nicht mehr die CLI von Aufgabe A.
    /// </summary>
    protected void AufgabeWechselUeberSeitenleiste_ZeigtNeueAufgabeMitEigenerCli_E2E(Window mainWindow)
    {
        ConfirmLocalDirectoryGitInitInSourceDirectory();

        var sourceDirectory = CreateLocalSourceDirectory("Wechsel-Repo");
        var settings = new SettingsView(mainWindow).ForceShow();
        var dashboard = settings.ConfigureLocalDirectoryPlugin(sourceDirectory, useInSourceDirectoryMode: false);

        var projectList = dashboard.Menu.NavigateToProjects();
        projectList.CreateProject("Wechsel-Projekt");
        var projectDetail = projectList.OpenProject("Wechsel-Projekt");

        var dialog = new RepositoryAssignDialogView(mainWindow).ForceShow();
        dialog.SelectFirstRepository();
        projectDetail = dialog.Confirm();

        // Aufgabe A anlegen, öffnen und CLI starten
        var taskA = ErstelleUndStarteAufgabe(projectDetail, TitelA);
        var pidA = taskA.WaitForTerminalProcessId(Medium);

        // Zurück zum Projekt, um Aufgabe B anzulegen
        taskA.GoBack();
        var projectDetailForB = Assert.IsType<ProjectDetailView>(mainWindow.CurrentView());

        // Aufgabe B anlegen, öffnen und CLI starten (eigener Prozess, andere PID als Aufgabe A)
        var taskB = ErstelleUndStarteAufgabe(projectDetailForB, TitelB);
        var pidB = taskB.WaitForTerminalProcessId(Medium);
        Assert.NotEqual(pidA, pidB);

        // Zurück zum Projekt und Aufgabe A erneut öffnen — Aufgabe A ist nun die "geöffnete" Aufgabe
        taskB.GoBack();
        var projectDetailForReopen = Assert.IsType<ProjectDetailView>(mainWindow.CurrentView());

        var taskAReopened = projectDetailForReopen.OpenTask(TitelA);
        taskAReopened.WaitForCliRunning();
        var pidAErneutGeoeffnet = taskAReopened.WaitForTerminalProcessId(Medium);
        Assert.Equal(pidA, pidAErneutGeoeffnet);

        // Über die Aufgabenliste in der Seitenleiste ("Aktive Aufgaben") zu Aufgabe B wechseln,
        // OHNE über "Zurück" zu navigieren — genau das im Bug-Report beschriebene Szenario.
        var taskAfterSwitchToB = taskAReopened.Menu.NavigateToTask(TitelB);

        // Die eingebettete CLI muss jetzt tatsächlich zu Aufgabe B gehören (nicht mehr zu Aufgabe A).
        var pidNachWechsel = taskAfterSwitchToB.WaitForTerminalProcessId(Medium);
        Assert.NotEqual(pidA, pidNachWechsel);
        Assert.Equal(pidB, pidNachWechsel);

        // Zusätzlich (nicht nur Titel/Fußzeile): Das Info-Panel zeigt den Titel von Aufgabe B.
        taskAfterSwitchToB.SwitchPanel("InfoCliToggle");
        taskAfterSwitchToB.WaitForText(TitelB);

        // Über die Aufgabenliste in der Seitenleiste("Aktive Aufgaben") zu Aufgabe A wechseln,
        // OHNE über "Zurück" zu navigieren — genau das im Bug-Report beschriebene Szenario.
        var taskAfterSwitchToA = taskAfterSwitchToB.Menu.NavigateToTask(TitelA);

        // Die eingebettete CLI muss jetzt tatsächlich zu Aufgabe A gehören (nicht mehr zu Aufgabe B).
        pidNachWechsel = taskAfterSwitchToA.WaitForTerminalProcessId(Medium);
        Assert.NotEqual(pidB, pidNachWechsel);
        Assert.Equal(pidA, pidNachWechsel);

        // Zusätzlich (nicht nur Titel/Fußzeile): Das Info-Panel zeigt den Titel von Aufgabe A.
        taskAfterSwitchToA.SwitchPanel("InfoCliToggle");
        taskAfterSwitchToA.WaitForText(TitelA);

        // taskAfterSwitchToA wurde zuletzt über das Menü (Menu.NavigateToTask) erreicht, nicht über die
        // Projektdetailansicht. MainWindowViewModel.NavigateZuAufgabe setzt für diesen Navigationsweg
        // ZurueckAction bewusst auf NavigateToDashboard (siehe dortiger Kommentar) - "Zurück" führt daher
        // unabhängig vom vorherigen Navigationsverlauf zum Dashboard, nicht zur Projektdetailansicht.
        taskAfterSwitchToA.ForceClose(recurseToDashboard: false);
        var dashboardFinal = Assert.IsType<DashboardView>(mainWindow.CurrentView());
        var projectListFinal = dashboardFinal.Menu.NavigateToProjects();
        var projectDetailFinal = projectListFinal.OpenProject("Wechsel-Projekt");
        projectDetailFinal.DeleteProject();
    }

    /// <summary>Legt eine neue Aufgabe im übergebenen Projekt an, benennt sie um, öffnet sie erneut und startet die CLI mit dem KI-Simulator-Plugin.</summary>
    /// <param name="projectDetail">Die Projektdetailansicht, in der die Aufgabe angelegt wird.</param>
    /// <param name="titel">Der Titel, auf den die neue Aufgabe umbenannt werden soll.</param>
    /// <returns>Die Aufgabendetailansicht der gestarteten Aufgabe.</returns>
    private TaskDetailView ErstelleUndStarteAufgabe(ProjectDetailView projectDetail, string titel)
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
}
