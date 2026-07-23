using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

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
[Trait("Category", "E2E")]
[OsInterface]
[Collection("E2E")]
public sealed class E2E_PluginAktivierung : WpfTestBase
{
    /// <summary>
    /// Führt Validierung (letztes SCM-Plugin), Persistenz des KI-Aktivierungsstatus und das
    /// Single-Plugin-Verhalten beim Aufgabenstart als drei Phasen derselben Aufgabe aus.
    /// </summary>
    [SkippableFact]
    public void PluginAktivierung_ValidierungPersistenzUndSinglePluginVerhalten_E2E()
    {
        ConfirmLocalDirectoryGitInitInSourceDirectory();

        var mainWindow = SetupProjectMitNeuerAufgabe("PluginAktivierung-Repo", "PluginAktivierung-Projekt");

        // SetupProjectMitNeuerAufgabe legt die Aufgabe an und öffnet sie direkt im Edit-Panel der
        // TaskDetailView. Für die folgenden Phasen wird zur ProjectDetailView zurückgekehrt, damit
        // "Neue Aufgabe" als ListItem in der Aufgabenliste auffindbar ist (siehe AufgabeAusListeOeffnen).
        AufgabeDetailZurueck(mainWindow);

        DeaktivierenDesLetztenScmPlugins_ZeigtValidierungsfehler_E2E(mainWindow);
        DeaktivierenVonDreiKiPlugins_PersistiertUndBlendetAuswahlAus_E2E(mainWindow);
    }

    /// <summary>
    /// Szenario: Das einzige verfügbare SCM-Plugin (LocalDirectoryPlugin) wird im Plugins-Register
    /// deaktiviert und gespeichert. Prüft: Die Validierungsregel "mindestens ein Plugin je Kategorie
    /// muss aktiv bleiben" verhindert das Speichern und zeigt eine Fehlermeldung. Setzt den
    /// Aktivierungsstatus anschließend über "Verwerfen" zurück, damit Phase 2 mit einem sauberen,
    /// weiterhin gültigen Zustand startet.
    /// </summary>
    /// <param name="mainWindow">Das Hauptfenster mit der neu angelegten Aufgabe im Edit-Panel.</param>
    private void DeaktivierenDesLetztenScmPlugins_ZeigtValidierungsfehler_E2E(AutomationElement mainWindow)
    {
        NavigateToSettings(mainWindow);
        OpenPluginsTab(mainWindow);

        var scmEintrag = WaitForElement(mainWindow, cf => cf.ByName("LocalDirectoryPlugin.Eintrag"), Short);
        scmEintrag.Click();
        var scmAktiviertCheckbox = WaitForElement(mainWindow, cf => cf.ByName("PluginAktiviert"), Short);
        scmAktiviertCheckbox.AsCheckBox().IsChecked = false;

        var speichernButton = WaitForElement(mainWindow, cf => cf.ByName("Speichern"), Short);
        speichernButton.AsButton().Click();

        var fehlerMeldung = WaitForElement(mainWindow, cf => cf.ByName("FehlerMeldung"), Short);
        Assert.NotNull(fehlerMeldung);

        // Zustand zurücksetzen (Aktivierungsstatus wurde ohnehin nicht persistiert), Plugins-Tab
        // bleibt dabei ausgewählt, da die Tab-Auswahl reines UI-Zustand ist und vom Reload nicht berührt wird.
        var verwerfenButton = WaitForElement(mainWindow, cf => cf.ByName("Verwerfen"), Short);
        verwerfenButton.AsButton().Click();
        WaitUntilGone(mainWindow, cf => cf.ByName("FehlerMeldung"), Short);
    }

    /// <summary>
    /// Szenario: Drei der vier verfügbaren KI-Plugins werden im Plugins-Register deaktiviert, sodass nur
    /// Softwareschmiede.KiSimulator aktiv bleibt; gespeichert. Prüft: Der Aktivierungsstatus bleibt nach
    /// Verlassen und erneutem Öffnen der Einstellungen erhalten (Persistenz). Anschließend wird die
    /// Aufgabe erneut geöffnet und gestartet: weil nur ein KI-Plugin aktiv ist, entfällt sowohl der
    /// "Plugin ändern"-Selector als auch der Plugin-Auswahl-Dialog; die CLI startet direkt.
    /// </summary>
    /// <param name="mainWindow">Das Hauptfenster, aktuell im Plugins-Register der Einstellungen.</param>
    private void DeaktivierenVonDreiKiPlugins_PersistiertUndBlendetAuswahlAus_E2E(AutomationElement mainWindow)
    {
        DeaktivierePlugin(mainWindow, "Softwareschmiede.ClaudeCli");
        DeaktivierePlugin(mainWindow, "Softwareschmiede.Codex");
        DeaktivierePlugin(mainWindow, "Softwareschmiede.GitHubCopilot");

        var speichernButton = WaitForElement(mainWindow, cf => cf.ByName("Speichern"), Short);
        speichernButton.AsButton().Click();
        WaitForElement(mainWindow, cf => cf.ByName("Einstellungen gespeichert."), Short);

        // Einstellungen verlassen und erneut öffnen: Aktivierungsstatus bleibt erhalten (Persistenz)
        var dashboardButton = WaitForElement(mainWindow, cf => cf.ByName("Dashboard"), Short);
        dashboardButton.AsButton().Click();

        NavigateToSettings(mainWindow);
        OpenPluginsTab(mainWindow);

        var claudeEintragReloaded = WaitForElement(mainWindow, cf => cf.ByName("Softwareschmiede.ClaudeCli.Eintrag"), Short);
        claudeEintragReloaded.Click();
        var claudeCheckboxReloaded = WaitForElement(mainWindow, cf => cf.ByName("PluginAktiviert"), Short);
        Assert.False(claudeCheckboxReloaded.AsCheckBox().IsChecked);

        // Zurück zur Aufgabe: bei genau einem aktiven KI-Plugin entfällt Selector und Auswahl-Dialog
        var dashboardButtonErneut = WaitForElement(mainWindow, cf => cf.ByName("Dashboard"), Short);
        dashboardButtonErneut.AsButton().Click();

        NavigateToProjects(mainWindow);
        OpenProject(mainWindow, "PluginAktivierung-Projekt");
        AufgabeAusListeOeffnen(mainWindow, "Neue Aufgabe");

        var startenButton = WaitForElement(mainWindow, cf => cf.ByName("Starten"), Short);
        startenButton.AsButton().Click();

        // Kein Plugin-Auswahl-Dialog erscheint: CLI startet direkt mit dem einzigen aktiven Plugin
        WaitForElement(mainWindow, cf => cf.ByName("CliStoppen"), Medium);

        var pluginDialogFenster = Automation.GetDesktop().FindFirstChild(cf =>
            cf.ByName("KI-Plugin auswählen").And(cf.ByControlType(ControlType.Window)));
        Assert.Null(pluginDialogFenster);

        var pluginAendernButton = mainWindow.FindFirstDescendant(cf => cf.ByName("PluginAendern"));
        Assert.Null(pluginAendernButton);
    }

    private static void OpenPluginsTab(AutomationElement mainWindow)
    {
        var pluginsTab = WaitForElement(mainWindow, cf => cf.ByName("Plugins"), Short);
        pluginsTab.Click();
    }

    private static void DeaktivierePlugin(AutomationElement mainWindow, string pluginPrefix)
    {
        var eintrag = WaitForElement(mainWindow, cf => cf.ByName($"{pluginPrefix}.Eintrag"), Short);
        eintrag.Click();
        var checkbox = WaitForElement(mainWindow, cf => cf.ByName("PluginAktiviert"), Short);
        checkbox.AsCheckBox().IsChecked = false;
    }
}
