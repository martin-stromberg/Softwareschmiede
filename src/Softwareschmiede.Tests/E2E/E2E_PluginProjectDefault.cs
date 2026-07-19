using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

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
[Trait("Category", "E2E")]
[OsInterface]
[Collection("E2E")]
public sealed class E2E_PluginProjectDefault : WpfTestBase
{
    /// <summary>
    /// Führt beide Phasen im selben Projekt aus: Erste Aufgabe speichert den Projekt-Standard über
    /// die Checkbox; zweite, neu angelegte Aufgabe desselben Projekts übernimmt ihn automatisch.
    /// </summary>
    [SkippableFact]
    public void PluginProjectDefault_SpeichernUndAutomatischeUebernahmeInFolgeaufgabe_E2E()
    {
        ConfirmLocalDirectoryGitInitInSourceDirectory();

        var mainWindow = SetupProjectMitNeuerAufgabe(
            "PluginProjectDefault-Repo",
            "PluginProjectDefault-Projekt",
            useInSourceDirectoryMode: false);

        PluginDialogMitProjektCheckbox_SpeichertProjektStandardUndStartetCli_E2E(mainWindow);
        ZweiteAufgabeImProjekt_UebernimmtGespeichertenProjektStandardOhneDialog_E2E(mainWindow);
    }

    /// <summary>
    /// Szenario: Plugin-Dialog wird mit aktivierter Checkbox "Für dieses Projekt verwenden" bestätigt.
    /// Prüft: Start-Ablauf wird mit dem gewählten Plugin fortgesetzt (CLI läuft); der Projekt-Standard
    /// wird gespeichert, sodass eine nachfolgende Aufgabe im selben Projekt den Dialog nicht mehr zeigt
    /// (siehe <see cref="ZweiteAufgabeImProjekt_UebernimmtGespeichertenProjektStandardOhneDialog_E2E"/>).
    /// </summary>
    /// <param name="mainWindow">Das Hauptfenster mit der ersten, neu angelegten Aufgabe im Edit-Panel.</param>
    private void PluginDialogMitProjektCheckbox_SpeichertProjektStandardUndStartetCli_E2E(AutomationElement mainWindow)
    {
        var startenButton = WaitForElement(mainWindow, cf => cf.ByName("Starten"), Short);
        startenButton.AsButton().Click();

        var dialog = WaitForWindow("KI-Plugin auswählen", Medium);

        var pluginAuswahlBox = WaitForElement(dialog, cf => cf.ByName("PluginAuswahl"), Short);
        SelectComboBoxItemByClick(pluginAuswahlBox, "Softwareschmiede.KiSimulator", Short);

        var checkbox = WaitForElement(dialog, cf => cf.ByName("FuerProjektVerwenden"), Short);
        checkbox.AsCheckBox().IsChecked = true;

        var okButton = WaitForElement(dialog, cf => cf.ByName("OK"), Short);
        okButton.AsButton().Click();

        // Start-Ablauf wird fortgesetzt: CLI läuft
        var stoppenButton = WaitForElement(mainWindow, cf => cf.ByName("CliStoppen"), Medium);
        Assert.NotNull(stoppenButton);

        var statusGestartet = WaitForElement(mainWindow, cf => cf.ByName("Gestartet"), Short);
        Assert.NotNull(statusGestartet);

        // Hauptfenster nach Schließen des Dialogs aktivieren, damit die UIA-Elemente
        // wieder einen gültigen Klickpunkt liefern (sonst NoClickablePointException möglich).
        mainWindow.Focus();
        Thread.Sleep(300);

        // Zurück zur Projektdetailansicht, damit die nächste Phase eine neue Aufgabe anlegen kann
        AufgabeDetailZurueck(mainWindow);
    }

    /// <summary>
    /// Szenario: Eine zweite, neu erstellte Aufgabe desselben Projekts wird gestartet, ohne dass der
    /// Plugin-Auswahl-Dialog erscheint; die CLI startet direkt mit dem gespeicherten Plugin.
    /// </summary>
    /// <param name="mainWindow">Das Hauptfenster in der Projektdetailansicht des Projekts mit gespeichertem Standard.</param>
    private void ZweiteAufgabeImProjekt_UebernimmtGespeichertenProjektStandardOhneDialog_E2E(AutomationElement mainWindow)
    {
        // Zweite Aufgabe erstellen und starten
        NeueAufgabeAnlegen(mainWindow);

        var zweiterStartenButton = WaitForElement(mainWindow, cf => cf.ByName("Starten"), Short);
        zweiterStartenButton.AsButton().Click();

        // Kein Plugin-Auswahl-Dialog erscheint: CLI startet direkt mit dem Projekt-Standard
        WaitForElement(mainWindow, cf => cf.ByName("CliStoppen"), Medium);

        var pluginDialogFenster = Automation.GetDesktop().FindFirstChild(cf =>
            cf.ByName("KI-Plugin auswählen").And(cf.ByControlType(ControlType.Window)));
        Assert.Null(pluginDialogFenster);
    }
}
