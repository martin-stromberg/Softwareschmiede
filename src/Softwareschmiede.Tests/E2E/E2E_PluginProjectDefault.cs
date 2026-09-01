using FlaUI.Core.AutomationElements;
using Softwareschmiede.Tests.E2E.Views;
using Softwareschmiede.Tests.E2E.Views.Dialogs;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// E2E-Tests für das Speichern eines Projekt-Standard-KI-Plugins über die Checkbox
/// "Für dieses Projekt verwenden" im Plugin-Auswahl-Dialog, und dafür, dass eine nachfolgende
/// Aufgabe desselben Projekts diesen Standard automatisch übernimmt (Feature 72).
///
/// Voraussetzungen:
/// - Windows-Desktop-Session (kein Headless-CI)
/// - Softwareschmiede.App muss im Debug-Modus gebaut sein
/// - Im Test-Modus steht ausschließlich das LocalDirectoryPlugin als SCM-Plugin zur Verfügung.
///
/// Konsolidierung (Issue #153): Die zweite Aufgabe, die den gespeicherten Projekt-Standard prüft,
/// gehört zwingend zum selben Projekt wie die erste - beide laufen daher als Phasen in einem
/// gemeinsamen App-Lifecycle statt zwei eigenständiger App-Starts.
///
/// CI-Regular-Lauf: dotnet test --filter "Category!=OsInterface"
/// </summary>
public partial class End2EndTest
{
    /// <summary>
    /// Führt beide Phasen im selben Projekt aus: Erste Aufgabe speichert den Projekt-Standard über
    /// die Checkbox "Für dieses Projekt verwenden"; zweite, neu angelegte Aufgabe desselben Projekts
    /// übernimmt ihn automatisch (kein Plugin-Auswahl-Dialog erscheint mehr).
    /// </summary>
    protected void PluginProjectDefault_SpeichernUndAutomatischeUebernahmeInFolgeaufgabe_E2E(Window mainWindow)
    {
        ConfirmLocalDirectoryGitInitInSourceDirectory();

        SetupProjectMitNeuerAufgabe(mainWindow,
            "PluginProjectDefault-Repo",
            "PluginProjectDefault-Projekt",
            useInSourceDirectoryMode: false);

        // Phase 1: Plugin-Dialog mit aktivierter Checkbox "Für dieses Projekt verwenden" bestätigen.
        var ersteAufgabe = new TaskDetailView(mainWindow).Start("Softwareschmiede.KiSimulator", fuerProjektVerwenden: true);
        ersteAufgabe.WaitForCliRunning();

        // Hauptfenster nach Schließen des Dialogs aktivieren, damit die UIA-Elemente
        // wieder einen gültigen Klickpunkt liefern (sonst NoClickablePointException möglich).
        mainWindow.Focus();
        Thread.Sleep(300);

        // Zurück zur Projektdetailansicht, damit die nächste Phase eine neue Aufgabe anlegen kann
        ersteAufgabe.GoBack();
        var projectDetail = Assert.IsType<ProjectDetailView>(mainWindow.CurrentView());

        // Phase 2: Zweite, neu erstellte Aufgabe desselben Projekts wird gestartet, ohne dass der
        // Plugin-Auswahl-Dialog erscheint; die CLI startet direkt mit dem gespeicherten Plugin.
        var zweiteAufgabe = projectDetail.CreateTask();
        zweiteAufgabe.Restart();
        zweiteAufgabe.WaitForCliRunning();

        Assert.False(new PluginSelectionDialogView(mainWindow).IsVisible);

        zweiteAufgabe.ForceClose(recurseToDashboard: false);
        var projectDetailFinal = Assert.IsType<ProjectDetailView>(mainWindow.CurrentView());
        projectDetailFinal.DeleteProject();
    }
}
