using FlaUI.Core.AutomationElements;
using Softwareschmiede.Tests.E2E.Views;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// E2E-Test für die Funktionstrennung der Automatisierungs-Einstellungen: Der Tab „Allgemein“
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
    protected void Einstellungen_AllgemeinEnthaeltNurVorhandeneAbschnitte_E2E(Window mainWindow)
    {
        var settings = new SettingsView(mainWindow).ForceShow();
        settings.SwitchTab("Allgemein");

        // „Allgemein“ enthält keine SCM-Automatisierungssteuerung mehr.
        Assert.Null(mainWindow.FindFirstDescendant(cf => cf.ByName("IsScmMonitoringEnabled")));
        Assert.Null(mainWindow.FindFirstDescendant(cf => cf.ByName("IsScmTaskCompletionEnabled")));
        Assert.Null(mainWindow.FindFirstDescendant(cf => cf.ByName("Backmerge-Modus")));
        Assert.Null(mainWindow.FindFirstDescendant(cf => cf.ByName("IsAutonomAufgabenEnabled")));

        // Der „Automatisierung“-Tab bündelt die drei unabhängig schaltbaren Funktionsbereiche.
        settings.SwitchTab("Automatisierung");
        WaitForElement(mainWindow, cf => cf.ByName("AufgabenAusfuehrenExpander"), Short);
        WaitForElement(mainWindow, cf => cf.ByName("AufgabenBeendenExpander"), Short);
        WaitForElement(mainWindow, cf => cf.ByName("ProzessablaeufeExpander"), Short);
        WaitForElement(mainWindow, cf => cf.ByName("AufgabenAusfuehrenStatus"), Short);
        WaitForElement(mainWindow, cf => cf.ByName("AufgabenBeendenStatus"), Short);
        WaitForElement(mainWindow, cf => cf.ByName("ProzessablaeufeStatus"), Short);

        // Aufgaben ausführen: Überwachungsschalter, Überwachungsmodus und Trigger-Keyword.
        // Sichtbarkeits-/Aktivierungsregeln: Standard = Überwachung an + Benutzerkonto
        // → Modus bedienbar, Trigger-Keyword und Prompt-Anhänge sichtbar.
        settings.OpenAufgabenAusfuehren();
        WaitForElement(mainWindow, cf => cf.ByName("IsScmMonitoringEnabled"), Short);
        WaitForElement(mainWindow, cf => cf.ByName("Ueberwachungsmodus"), Short);
        WaitForElement(mainWindow, cf => cf.ByName("ScmTriggerKeyword"), Short);
        Assert.True(settings.IsUeberwachungsmodusEnabled());
        Assert.True(settings.IsTriggerKeywordBereichVisible());
        Assert.True(settings.IsPromptAnhaengeBereichVisible());

        // Überwachungsmodus „Separates Konto" blendet nur das Trigger-Keyword aus.
        settings.SetKontoModus("Separates Konto");
        Assert.False(settings.IsTriggerKeywordBereichVisible());
        Assert.True(settings.IsPromptAnhaengeBereichVisible());
        settings.SetKontoModus("Benutzerkonto");
        Assert.True(settings.IsTriggerKeywordBereichVisible());

        // Schalter aus → Überwachungsmodus deaktiviert, beide Bereiche unsichtbar.
        settings.SetScmMonitoringEnabledChecked(false);
        Assert.False(settings.IsUeberwachungsmodusEnabled());
        Assert.False(settings.IsTriggerKeywordBereichVisible());
        Assert.False(settings.IsPromptAnhaengeBereichVisible());
        settings.SetScmMonitoringEnabledChecked(true);
        Assert.True(settings.IsUeberwachungsmodusEnabled());
        Assert.True(settings.IsTriggerKeywordBereichVisible());
        settings.CollapseAufgabenAusfuehren();
        Assert.Equal("aktiviert", settings.GetAufgabenAusfuehrenStatus());

        // Aufgaben beenden: Schalter, Prüfmodus und Wartezeit. Deaktiviert → beide Felder gesperrt.
        settings.OpenAufgabenBeenden();
        WaitForElement(mainWindow, cf => cf.ByName("IsScmTaskCompletionEnabled"), Short);
        WaitForElement(mainWindow, cf => cf.ByName("Pruefmodus"), Short);
        WaitForElement(mainWindow, cf => cf.ByName("ScmTaskCompletionWaitMinutes"), Short);
        Assert.True(settings.IsPruefmodusEnabled());
        Assert.True(settings.IsScmTaskCompletionWaitEnabled());
        settings.SetScmTaskCompletionEnabledChecked(false);
        Assert.False(settings.IsPruefmodusEnabled());
        Assert.False(settings.IsScmTaskCompletionWaitEnabled());
        settings.SetScmTaskCompletionEnabledChecked(true);
        Assert.True(settings.IsPruefmodusEnabled());
        settings.CollapseAufgabenBeenden();
        Assert.Equal("aktiviert", settings.GetAufgabenBeendenStatus());

        // Prozessabläufe: Backmerge-Modus sowie die Opt-ins Draft-Aktivierung und Admin-Merge.
        // Modus „Deaktiviert" sperrt die Merge-Optionen, „Global aktiviert" gibt sie frei.
        settings.OpenProzessablaeufe();
        WaitForElement(mainWindow, cf => cf.ByName("Backmerge-Modus"), Short);
        WaitForElement(mainWindow, cf => cf.ByName("IsScmBackmergeDraftAktivierung"), Short);
        WaitForElement(mainWindow, cf => cf.ByName("IsScmBackmergeAdminMerge"), Short);
        settings.SetBackmergeModus("Deaktiviert");
        Assert.False(settings.IsBackmergeDraftAktivierungEnabled());
        Assert.False(settings.IsBackmergeAdminMergeEnabled());
        settings.CollapseProzessablaeufe();
        Assert.Equal("deaktiviert", settings.GetProzessablaeufeStatus());
        settings.SetBackmergeModus("Global aktiviert");
        Assert.True(settings.IsBackmergeDraftAktivierungEnabled());
        Assert.True(settings.IsBackmergeAdminMergeEnabled());
        settings.CollapseProzessablaeufe();
        Assert.Equal("aktiviert", settings.GetProzessablaeufeStatus());

        // Persistenz-Stichprobe: Prüfmodus, Backmerge-Modus und Options-Checkboxen speichern,
        // Einstellungen neu laden, Auswahl und Checkbox-Zustände prüfen. Die beiden
        // Funktionsschalter wurden nicht verändert und müssen aktiviert geblieben sein.
        var pruefmodusVorher = settings.GetPruefmodus();
        settings.SetPruefmodus("Alle Aufgaben");
        settings.SetBackmergeModus("Global aktiviert");
        settings.SetBackmergeDraftAktivierungChecked(true);
        settings.SetBackmergeAdminMergeChecked(true);
        settings.SaveSettings();
        var settingsReopened = settings.Menu.NavigateToSettings();
        Assert.Equal("Alle Aufgaben", settingsReopened.GetPruefmodus());
        Assert.Equal("Global aktiviert", settingsReopened.GetBackmergeModus());
        Assert.True(settingsReopened.IsBackmergeDraftAktivierungChecked());
        Assert.True(settingsReopened.IsBackmergeAdminMergeChecked());
        Assert.True(settingsReopened.IsScmMonitoringEnabledChecked());
        Assert.True(settingsReopened.IsScmTaskCompletionEnabledChecked());

        // Ursprungszustand wiederherstellen, damit Folgetests vom Standard ausgehen. Die
        // Options-Checkboxen müssen vor dem Modus zurückgesetzt werden: Bei „Deaktiviert“
        // sind sie gesperrt (IsProzessablaeufeFelderEnabled).
        settingsReopened.SetPruefmodus(pruefmodusVorher);
        settingsReopened.SetBackmergeDraftAktivierungChecked(false);
        settingsReopened.SetBackmergeAdminMergeChecked(false);
        settingsReopened.SetBackmergeModus("Deaktiviert");
        settingsReopened.SaveSettings();

        settingsReopened.Menu.NavigateToDashboard();
    }
}
