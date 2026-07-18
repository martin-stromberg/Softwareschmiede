using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// E2E-Test: Nachdem für ein Projekt ein KI-Plugin-Standard gespeichert wurde, verwendet eine
/// nachfolgende Aufgabe desselben Projekts dieses Plugin automatisch und der Auswahl-Dialog
/// wird beim Starten nicht mehr angezeigt (Feature 72).
///
/// Voraussetzungen:
/// - Windows-Desktop-Session (kein Headless-CI)
/// - Softwareschmiede.App muss im Debug-Modus gebaut sein
/// - Im Test-Modus steht ausschließlich das LocalDirectoryPlugin als SCM-Plugin zur Verfügung.
///
/// CI-Regular-Lauf: dotnet test --filter "Category!=OsInterface"
/// </summary>
[Trait("Category", "E2E")]
[OsInterface]
[Collection("E2E")]
public sealed class E2E_PluginProjectDefault_NextTask : WpfTestBase
{
    /// <summary>
    /// Szenario: Erste Aufgabe speichert den Projekt-Standard über die Checkbox. Eine zweite,
    /// neu erstellte Aufgabe desselben Projekts wird gestartet, ohne dass der Plugin-Auswahl-Dialog
    /// erscheint; die CLI startet direkt mit dem gespeicherten Plugin.
    /// </summary>
    [SkippableFact]
    public void ZweiteAufgabeImProjekt_UebernimmtGespeichertenProjektStandardOhneDialog_E2E()
    {
        var mainWindow = SetupProjectMitNeuerAufgabe(
            "PluginProjectDefaultNext-Repo",
            "PluginProjectDefaultNext-Projekt",
            useInSourceDirectoryMode: false);

        // Erste Aufgabe: Plugin auswählen, Projekt-Standard speichern
        StartenUndPluginWaehlen(mainWindow, "Softwareschmiede.KiSimulator", fuerProjektVerwenden: true);

        WaitForElement(mainWindow, cf => cf.ByName("CliStoppen"), Medium);

        // Hauptfenster nach Schließen des Dialogs aktivieren, damit die UIA-Elemente
        // wieder einen gültigen Klickpunkt liefern (sonst NoClickablePointException möglich).
        mainWindow.Focus();
        Thread.Sleep(300);

        // Zurück zur Projektdetailansicht
        AufgabeDetailZurueck(mainWindow);

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
