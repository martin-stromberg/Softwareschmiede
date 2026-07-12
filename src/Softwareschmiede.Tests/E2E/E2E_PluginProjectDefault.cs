using FlaUI.Core.AutomationElements;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// E2E-Test für das Speichern eines Projekt-Standard-KI-Plugins über die Checkbox
/// "Für dieses Projekt verwenden" im Plugin-Auswahl-Dialog (Feature 72).
///
/// Voraussetzungen:
/// - Windows-Desktop-Session (kein Headless-CI)
/// - Softwareschmiede.App muss im Debug-Modus gebaut sein
/// - Im Test-Modus steht ausschließlich das LocalDirectoryPlugin als SCM-Plugin zur Verfügung.
///
/// CI-Ausschluss: dotnet test --filter "Category!=E2E"
/// </summary>
[Trait("Category", "E2E")]
[Collection("E2E")]
public sealed class E2E_PluginProjectDefault : WpfTestBase
{
    /// <summary>
    /// Szenario: Plugin-Dialog wird mit aktivierter Checkbox "Für dieses Projekt verwenden" bestätigt.
    /// Prüft: Start-Ablauf wird mit dem gewählten Plugin fortgesetzt (CLI läuft); der Projekt-Standard
    /// wird gespeichert, sodass eine nachfolgende Aufgabe im selben Projekt den Dialog nicht mehr zeigt
    /// (siehe <see cref="E2E_PluginProjectDefault_NextTask"/>).
    /// </summary>
    [SkippableFact]
    public void PluginDialogMitProjektCheckbox_SpeichertProjektStandardUndStartetCli_E2E()
    {
        ConfirmLocalDirectoryGitInitInSourceDirectory();

        var mainWindow = SetupProjectMitNeuerAufgabe("PluginProjectDefault-Repo", "PluginProjectDefault-Projekt");

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
    }
}
