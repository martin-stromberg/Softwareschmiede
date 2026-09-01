using FlaUI.Core.AutomationElements;
using Softwareschmiede.Tests.E2E.Views;
using Softwareschmiede.Tests.E2E.Views.Dialogs;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// E2E-Tests für das Deaktivieren von Plugins im neuen Plugins-Register der Einstellungen (Issue #174):
/// Validierung "mindestens ein Plugin je Kategorie muss aktiv bleiben", Persistenz des
/// Aktivierungsstatus über ein erneutes Öffnen der Einstellungen, und das Single-Plugin-Verhalten in
/// der Aufgabenbearbeitung (KI-Plugin-Selector/-Auswahl-Dialog entfällt bei genau einem aktiven Plugin).
///
/// Voraussetzungen:
/// - Windows-Desktop-Session (kein Headless-CI)
/// - Softwareschmiede.App muss im Debug-Modus gebaut sein
/// - Im Test-Modus steht ausschließlich das LocalDirectoryPlugin als SCM-Plugin zur Verfügung
///   (Softwareschmiede.Plugin.LocalDirectory); als KI-Plugins sind Softwareschmiede.KiSimulator,
///   Softwareschmiede.ClaudeCli, Softwareschmiede.Codex und Softwareschmiede.GitHubCopilot verfügbar.
///   Weil im Test-Modus nur ein einziges SCM-Plugin geladen wird, kann das Szenario "deaktiviertes
///   SCM-Plugin verschwindet aus der Auswahl" hier nicht mit einer verbleibenden SCM-Auswahl gezeigt
///   werden; stattdessen prüft die erste Phase an der Deaktivierung des einzigen SCM-Plugins die
///   Validierungsregel "mindestens ein Plugin je Kategorie muss aktiv bleiben". Die Filterung
///   deaktivierter SCM-Plugins aus der Auswahl selbst ist durch die Unit-Tests
///   PluginActivationServiceTests.GetEnabledSourceCodeManagementPlugins_FiltertDeaktivierte und
///   RepositoryAssignViewModel abgedeckt.
///
/// Konsolidierung (Issue #174): Validierung, Persistenz und Single-Plugin-Verhalten laufen als drei
/// Phasen an derselben Aufgabe in einem gemeinsamen App-Lifecycle statt dreier eigenständiger App-Starts.
///
/// CI-Regular-Lauf: dotnet test --filter "Category!=OsInterface"
/// </summary>
public partial class End2EndTest
{
    /// <summary>
    /// Führt Validierung (letztes SCM-Plugin), Persistenz des KI-Aktivierungsstatus und das
    /// Single-Plugin-Verhalten beim Aufgabenstart als drei Phasen derselben Aufgabe aus.
    /// </summary>
    protected void PluginAktivierung_ValidierungPersistenzUndSinglePluginVerhalten_E2E(Window mainWindow)
    {
        ConfirmLocalDirectoryGitInitInSourceDirectory();

        SetupProjectMitNeuerAufgabe(mainWindow, "PluginAktivierung-Repo", "PluginAktivierung-Projekt");

        // SetupProjectMitNeuerAufgabe legt die Aufgabe an und öffnet sie direkt im Edit-Panel der
        // TaskDetailView. Für die folgenden Phasen wird zur ProjectDetailView zurückgekehrt, damit
        // "Neue Aufgabe" als ListItem in der Aufgabenliste auffindbar ist (siehe ProjectDetailView.OpenTask).
        new TaskDetailView(mainWindow).GoBack();
        var projectDetail = Assert.IsType<ProjectDetailView>(mainWindow.CurrentView());

        var settings = DeaktivierenDesLetztenScmPlugins_ZeigtValidierungsfehler_E2E(mainWindow, projectDetail);
        DeaktivierenVonDreiKiPlugins_PersistiertUndBlendetAuswahlAus_E2E(mainWindow, settings);
    }

    /// <summary>
    /// Szenario: Das einzige verfügbare SCM-Plugin (LocalDirectoryPlugin) wird im Plugins-Register
    /// deaktiviert und gespeichert. Prüft: Die Validierungsregel "mindestens ein Plugin je Kategorie
    /// muss aktiv bleiben" verhindert das Speichern und zeigt eine Fehlermeldung. Setzt den
    /// Aktivierungsstatus anschließend über "Verwerfen" zurück, damit Phase 2 mit einem sauberen,
    /// weiterhin gültigen Zustand startet.
    /// </summary>
    /// <param name="mainWindow">Das Hauptfenster der Anwendung.</param>
    /// <param name="projectDetail">Die Projektdetailansicht mit der neu angelegten Aufgabe in der Liste.</param>
    /// <returns>Die Einstellungen-Ansicht (Plugins-Tab), bereit für Phase 2.</returns>
    private SettingsView DeaktivierenDesLetztenScmPlugins_ZeigtValidierungsfehler_E2E(Window mainWindow, ProjectDetailView projectDetail)
    {
        var settings = projectDetail.Menu.NavigateToSettings();
        settings.SwitchTab("Plugins");

        settings.SetPluginEnabled("LocalDirectoryPlugin", false);

        Assert.Throws<InvalidOperationException>(() => settings.SaveSettings());

        // Zustand zurücksetzen (Aktivierungsstatus wurde ohnehin nicht persistiert), Plugins-Tab
        // bleibt dabei ausgewählt, da die Tab-Auswahl reines UI-Zustand ist und vom Reload nicht berührt wird.
        settings.DiscardChanges();

        return settings;
    }

    /// <summary>
    /// Szenario: Drei der vier verfügbaren KI-Plugins werden im Plugins-Register deaktiviert, sodass nur
    /// Softwareschmiede.KiSimulator aktiv bleibt; gespeichert. Prüft: Der Aktivierungsstatus bleibt nach
    /// Verlassen und erneutem Öffnen der Einstellungen erhalten (Persistenz). Anschließend wird die
    /// Aufgabe erneut geöffnet und gestartet: weil nur ein KI-Plugin aktiv ist, entfällt sowohl der
    /// "Plugin ändern"-Selector als auch der Plugin-Auswahl-Dialog; die CLI startet direkt.
    /// </summary>
    /// <param name="mainWindow">Das Hauptfenster der Anwendung.</param>
    /// <param name="settings">Die Einstellungen-Ansicht, aktuell im Plugins-Register.</param>
    private void DeaktivierenVonDreiKiPlugins_PersistiertUndBlendetAuswahlAus_E2E(Window mainWindow, SettingsView settings)
    {
        settings.SetPluginEnabled("Softwareschmiede.ClaudeCli", false);
        settings.SetPluginEnabled("Softwareschmiede.Codex", false);
        settings.SetPluginEnabled("Softwareschmiede.Devin", false);
        settings.SetPluginEnabled("Softwareschmiede.GitHubCopilot", false);
        settings.SaveSettings();

        // Einstellungen verlassen und erneut öffnen: Aktivierungsstatus bleibt erhalten (Persistenz)
        var dashboard = settings.Menu.NavigateToDashboard();

        var settingsReloaded = dashboard.Menu.NavigateToSettings();
        settingsReloaded.SwitchTab("Plugins");
        Assert.False(settingsReloaded.IsPluginEnabled("Softwareschmiede.ClaudeCli"));

        // Zurück zur Aufgabe: bei genau einem aktiven KI-Plugin entfällt Selector und Auswahl-Dialog
        var dashboardErneut = settingsReloaded.Menu.NavigateToDashboard();
        var projectList = dashboardErneut.Menu.NavigateToProjects();
        var projectDetail = projectList.OpenProject("PluginAktivierung-Projekt");
        var taskDetail = projectDetail.OpenTask("Neue Aufgabe");

        // Kein Plugin-Auswahl-Dialog erscheint: CLI startet direkt mit dem einzigen aktiven Plugin
        taskDetail.Restart();
        taskDetail.WaitForCliRunning();

        Assert.False(new PluginSelectionDialogView(mainWindow).IsVisible);
        Assert.False(taskDetail.HasPluginChangeButton());

        taskDetail.ForceClose(recurseToDashboard: false);
        var projectDetailFinal = Assert.IsType<ProjectDetailView>(mainWindow.CurrentView());
        projectDetailFinal.DeleteProject();
    }
}
