using FlaUI.Core.AutomationElements;

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
[Trait("Category", "E2E")]
[OsInterface]
[Collection("E2E")]
public sealed class E2E_TaskDetailNavigation : WpfTestBase
{
    /// <summary>
    /// Szenario: Neue Aufgabe anlegen (korrekte Daten prüfen), über "Zurück" zur ProjectDetailView
    /// zurücknavigieren, anschließend die Aufgabe per Doppelklick aus der Aufgabenliste erneut öffnen.
    /// Prüft: Die TaskDetailView zeigt beim Anlegen den korrekten Standardtitel; "Zurück" navigiert zur
    /// ProjectDetailView zurück; das Öffnen aus der Liste zeigt die TaskDetailView fensterumfassend
    /// (ProjectDetailView nicht mehr sichtbar).
    /// </summary>
    [Fact]
    public void TaskDetail_ZeigtDaten_Zurueck_UndOeffnenFensterumfassend_E2E()
    {
        var mainWindow = StartAndNavigateToProjects("TaskNav-Test");

        // Korrekte Daten
        var editTitelBox = NeueAufgabeAnlegen(mainWindow);
        Assert.Equal("Neue Aufgabe", editTitelBox.AsTextBox().Text);

        // Rücknavigation
        AufgabeDetailZurueck(mainWindow);
        WaitForElement(mainWindow, cf => cf.ByName("ProjektName"), Short);

        // Fensterumfassendes Öffnen aus der Liste
        var items = OffeneAufgabenItems(mainWindow);
        Assert.True(items.Length >= 1, "Aufgabenliste sollte mindestens eine Aufgabe enthalten.");
        ErsteOffeneAufgabeOeffnen(items);

        // TaskDetailView zeigt eigenes Ribbon mit "Speichern"-Button (Edit-Panel bei Status Neu)
        WaitForElement(mainWindow, cf => cf.ByName("Speichern"), Short);

        // ProjektName-TextBox (Teil der ProjectDetailView) ist nicht mehr sichtbar
        var projektNameBoxNachOeffnen = mainWindow.FindFirstDescendant(cf => cf.ByName("ProjektName"));
        Assert.Null(projektNameBoxNachOeffnen);
    }
}
