using FlaUI.Core.AutomationElements;
using Softwareschmiede.Tests.E2E.Views;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// E2E-Test für die Funktionstrennung der Automatisierungs--Einstellungen: Der Tab „Allgemein“
/// enthält keine SCM-Automatisierungssteuerung mehr; der neue Tab „Automatisierung“ bündelt die
/// drei unabhängig schaltbaren Funktionsbereiche in eigenen Expandern.
/// </summary>
public partial class End2EndTest
{
    /// <summary>
    /// Prüft die Tab-Struktur der Einstellungen: „Allgemein“ ohne SCM-Steuerung, „Automatisierung“
    /// mit den Expandern „Aufgaben ausführen“, „Aufgaben beenden“ und „Prozessabläufe“ samt ihren
    /// Steuerelementen und Statusanzeigen; abschließend ein Speichern-und-Neuladen-Zyklus des
    /// Prüfmodus als Stichprobe der Persistenz.
    /// </summary>
    /// <param name="mainWindow">Das Hauptfenster der Anwendung.</param>
    protected void Settings_GeneralContainsOnlyExistingSection_E2E(Window mainWindow)
    {
        var settings = new SettingsView(mainWindow).ForceShow();
        settings.SwitchTab("Allgemein");

        // „Allgemein“ enthält keine SCM-Automatisierungssteuerung mehr.
        Assert.Null(mainWindow.FindFirstDescendant(cf => cf.ByName("IsScmMonitoringEnabled")));
        Assert.Null(mainWindow.FindFirstDescendant(cf => cf.ByName("IsScmTaskCompletionEnabled")));
        Assert.Null(mainWindow.FindFirstDescendant(cf => cf.ByName("Backmerge-Modus")));
        Assert.Null(mainWindow.FindFirstDescendant(cf => cf.ByName("IsAutonomousWorkItemsEnabled")));

        // Der „Automatisierung“-Tab bündelt die drei unabhängig schaltbaren Funktionsbereiche.
        settings.SwitchTab("Automatisierung");
        WaitForElement(mainWindow, cf => cf.ByName("WorkItemsExecuteExpander"), Short);
        WaitForElement(mainWindow, cf => cf.ByName("WorkItemsExitExpander"), Short);
        WaitForElement(mainWindow, cf => cf.ByName("ProcessRunsExpander"), Short);
        WaitForElement(mainWindow, cf => cf.ByName("WorkItemsExecuteStatus"), Short);
        WaitForElement(mainWindow, cf => cf.ByName("WorkItemsExitStatus"), Short);
        WaitForElement(mainWindow, cf => cf.ByName("ProcessRunsStatus"), Short);

        // Aufgaben ausführen: Überwachungsschalter, Überwachungsmodus und Trigger-Keyword.
        // Sichtbarkeits-/Aktivierungsregeln: Standard = Überwachung an + Benutzerkonto
        // → Modus bedienbar, Trigger-Keyword und Prompt-Anhänge sichtbar.
        settings.ExecuteOpenWorkItems();
        WaitForElement(mainWindow, cf => cf.ByName("IsScmMonitoringEnabled"), Short);
        WaitForElement(mainWindow, cf => cf.ByName("MonitoringMode"), Short);
        WaitForElement(mainWindow, cf => cf.ByName("ScmTriggerKeyword"), Short);
        Assert.True(settings.IsMonitoringModeEnabled());
        Assert.True(settings.IsTriggerKeywordAreaVisible());
        Assert.True(settings.IsDelayKeywordAreaVisible());
        Assert.True(settings.IsPromptAppendAreaVisible());

        // Überwachungsmodus „Separates Konto" blendet nur das Trigger-Keyword aus;
        // das Verzögerungs-Keyword bleibt in beiden Kontomodi sichtbar.
        settings.SetAccountMode("Separates Konto");
        Assert.False(settings.IsTriggerKeywordAreaVisible());
        Assert.True(settings.IsDelayKeywordAreaVisible());
        Assert.True(settings.IsPromptAppendAreaVisible());
        settings.SetAccountMode("Benutzerkonto");
        Assert.True(settings.IsTriggerKeywordAreaVisible());

        // Schalter aus → Überwachungsmodus deaktiviert, alle Bereiche unsichtbar.
        settings.SetScmMonitoringEnabledChecked(false);
        Assert.False(settings.IsMonitoringModeEnabled());
        Assert.False(settings.IsTriggerKeywordAreaVisible());
        Assert.False(settings.IsDelayKeywordAreaVisible());
        Assert.False(settings.IsPromptAppendAreaVisible());
        settings.SetScmMonitoringEnabledChecked(true);
        Assert.True(settings.IsMonitoringModeEnabled());
        Assert.True(settings.IsTriggerKeywordAreaVisible());
        Assert.True(settings.IsDelayKeywordAreaVisible());
        settings.ExecuteCollapseWorkItems();
        Assert.Equal("aktiviert", settings.GetWorkItemsExecuteStatus());

        // Aufgaben beenden: Schalter, Prüfmodus und Wartezeit. Deaktiviert → beide Felder gesperrt.
        settings.EndOpenWorkItems();
        WaitForElement(mainWindow, cf => cf.ByName("IsScmTaskCompletionEnabled"), Short);
        WaitForElement(mainWindow, cf => cf.ByName("CheckMode"), Short);
        WaitForElement(mainWindow, cf => cf.ByName("ScmTaskCompletionWaitMinutes"), Short);
        Assert.True(settings.IsCheckmodeEnabled());
        Assert.True(settings.IsScmTaskCompletionWaitEnabled());
        settings.SetScmTaskCompletionEnabledChecked(false);
        Assert.False(settings.IsCheckmodeEnabled());
        Assert.False(settings.IsScmTaskCompletionWaitEnabled());
        settings.SetScmTaskCompletionEnabledChecked(true);
        Assert.True(settings.IsCheckmodeEnabled());
        settings.EndCollapseWorkItems();
        Assert.Equal("aktiviert", settings.GetWorkItemsEndStatus());

        // Prozessabläufe: Backmerge-Modus sowie die Opt-ins Draft-Aktivierung und Admin-Merge.
        // Modus „Deaktiviert" sperrt die Merge-Optionen, „Global aktiviert" gibt sie frei.
        settings.OpenProcessflows();
        WaitForElement(mainWindow, cf => cf.ByName("Backmerge-Modus"), Short);
        WaitForElement(mainWindow, cf => cf.ByName("IsScmBackmergeDraftActivation"), Short);
        WaitForElement(mainWindow, cf => cf.ByName("IsScmBackmergeAdminMerge"), Short);
        settings.SetBackmergeMode("Deaktiviert");
        Assert.False(settings.IsBackmergeDraftActivationEnabled());
        Assert.False(settings.IsBackmergeAdminMergeEnabled());
        settings.CollapseProcessflows();
        Assert.Equal("deaktiviert", settings.GetProcessflowsStatus());
        settings.SetBackmergeMode("Global aktiviert");
        Assert.True(settings.IsBackmergeDraftActivationEnabled());
        Assert.True(settings.IsBackmergeAdminMergeEnabled());
        settings.CollapseProcessflows();
        Assert.Equal("aktiviert", settings.GetProcessflowsStatus());

        // Persistenz-Stichprobe: Prüfmodus, Backmerge-Modus und Options-Checkboxen speichern,
        // Settings neu laden, Auswahl und Checkbox-Zustände prüfen. Die beiden
        // Funktionsschalter wurden nicht verändert und müssen aktiviert geblieben sein.
        var checkmodeBefore = settings.GetCheckmode();
        settings.SetCheckmode("Alle Aufgaben");
        settings.SetBackmergeMode("Global aktiviert");
        settings.SetBackmergeDraftActivationChecked(true);
        settings.SetBackmergeAdminMergeChecked(true);
        settings.SaveSettings();
        var settingsReopened = settings.Menu.NavigateToSettings();
        Assert.Equal("Alle Aufgaben", settingsReopened.GetCheckmode());
        Assert.Equal("Global aktiviert", settingsReopened.GetBackmergeMode());
        Assert.True(settingsReopened.IsBackmergeDraftActivationChecked());
        Assert.True(settingsReopened.IsBackmergeAdminMergeChecked());
        Assert.True(settingsReopened.IsScmMonitoringEnabledChecked());
        Assert.True(settingsReopened.IsScmTaskCompletionEnabledChecked());

        // Ursprungszustand wiederherstellen, damit Folgetests vom Standard ausgehen. Die
        // Options-Checkboxen müssen vor dem Modus zurückgesetzt werden: Bei „Deaktiviert“
        // sind sie gesperrt (IsProzessablaeufeFelderEnabled).
        settingsReopened.SetCheckmode(checkmodeBefore);
        settingsReopened.SetBackmergeDraftActivationChecked(false);
        settingsReopened.SetBackmergeAdminMergeChecked(false);
        settingsReopened.SetBackmergeMode("Deaktiviert");
        settingsReopened.SaveSettings();

        settingsReopened.Menu.NavigateToDashboard();
    }
}
