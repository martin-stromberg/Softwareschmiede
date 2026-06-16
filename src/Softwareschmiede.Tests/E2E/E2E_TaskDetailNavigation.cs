using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// E2E-Tests für die separate, fensterumfassende Aufgabendetailansicht (Feature 72).
///
/// Voraussetzungen:
/// - Windows-Desktop-Session (kein Headless-CI)
/// - Softwareschmiede.App muss im Debug-Modus gebaut sein
///
/// CI-Ausschluss: dotnet test --filter "Category!=E2E"
/// </summary>
[Trait("Category", "E2E")]
[Collection("E2E")]
public sealed class E2E_TaskDetailNavigation : WpfTestBase
{
    private static readonly TimeSpan Short = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan Medium = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Szenario: Projekt öffnen, Doppelklick auf Aufgabe in der Aufgabenliste.
    /// Prüft: TaskDetailView wird fensterumfassend angezeigt (ProjectDetailView nicht mehr sichtbar).
    /// </summary>
    [Fact]
    public void AufgabeOeffnen_ZeigtTaskDetailViewFensterumfassend_E2E()
    {
        var app = LaunchApp();
        var mainWindow = app.GetMainWindow(Automation, Medium)!;
        NavigateToProjecten(mainWindow);
        CreateAndOpenProject(mainWindow, "TaskNav-Oeffnen-Test");

        // Aufgabe anlegen, damit ein Listeneintrag existiert
        var aufgabeNeuButton = WaitForElement(mainWindow, cf => cf.ByName("AufgabeNeu"), Short);
        aufgabeNeuButton.AsButton().Click();

        // Neu angelegte Aufgabe öffnet bereits die TaskDetailView; zurück zur Projektansicht navigieren
        var zurueckInTask = WaitForElement(mainWindow, cf => cf.ByName("Zurück"), Short);
        zurueckInTask.AsButton().Click();

        // Aufgabenliste enthält jetzt die Aufgabe; Doppelklick öffnet TaskDetailView erneut
        var listBox = WaitForElement(mainWindow, cf => cf.ByName("AufgabenListe"), Medium);
        var items = listBox.FindAllChildren(cf => cf.ByControlType(ControlType.ListItem));
        Assert.True(items.Length >= 1, "Aufgabenliste sollte mindestens eine Aufgabe enthalten.");
        items[0].DoubleClick();

        // TaskDetailView zeigt eigenes Ribbon mit "Speichern"-Button (Edit-Panel bei Status Neu)
        var speichernInTask = WaitForElement(mainWindow, cf => cf.ByName("Speichern"), Short);
        Assert.NotNull(speichernInTask);

        // ProjektName-TextBox (Teil der ProjectDetailView) ist nicht mehr sichtbar
        var projektNameBox = mainWindow.FindFirstDescendant(cf => cf.ByName("ProjektName"));
        Assert.Null(projektNameBox);
    }

    /// <summary>
    /// Szenario: TaskDetailView zeigt die korrekten Aufgabendaten (Titel) an.
    /// </summary>
    [Fact]
    public void TaskDetailView_ZeigtKorrekteAufgabendaten_E2E()
    {
        var app = LaunchApp();
        var mainWindow = app.GetMainWindow(Automation, Medium)!;
        NavigateToProjecten(mainWindow);
        CreateAndOpenProject(mainWindow, "TaskNav-Daten-Test");

        var aufgabeNeuButton = WaitForElement(mainWindow, cf => cf.ByName("AufgabeNeu"), Short);
        aufgabeNeuButton.AsButton().Click();

        // EditTitel-Feld zeigt den Standardtitel der neu erstellten Aufgabe
        var editTitelBox = WaitForElement(mainWindow, cf => cf.ByName("EditTitel"), Short);
        Assert.Equal("Neue Aufgabe", editTitelBox.AsTextBox().Text);
    }

    /// <summary>
    /// Szenario: Klick auf "Zurück" in der TaskDetailView.
    /// Prüft: Navigation zurück zur ProjectDetailView funktioniert.
    /// </summary>
    [Fact]
    public void ZurueckButtonInTaskDetail_NavigiertZuProjectDetailView_E2E()
    {
        var app = LaunchApp();
        var mainWindow = app.GetMainWindow(Automation, Medium)!;
        NavigateToProjecten(mainWindow);
        CreateAndOpenProject(mainWindow, "TaskNav-Zurueck-Test");

        var aufgabeNeuButton = WaitForElement(mainWindow, cf => cf.ByName("AufgabeNeu"), Short);
        aufgabeNeuButton.AsButton().Click();

        WaitForElement(mainWindow, cf => cf.ByName("EditTitel"), Short);

        var zurueckButton = WaitForElement(mainWindow, cf => cf.ByName("Zurück"), Short);
        zurueckButton.AsButton().Click();

        // ProjectDetailView ist wieder sichtbar (ProjektName-Feld vorhanden)
        var projektNameBox = WaitForElement(mainWindow, cf => cf.ByName("ProjektName"), Short);
        Assert.NotNull(projektNameBox);
    }
}
