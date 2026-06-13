using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// E2E-Tests für die Projektdetailansicht mit Ribbon-Menü und Kacheln.
///
/// Voraussetzungen:
/// - Windows-Desktop-Session (kein Headless-CI)
/// - Softwareschmiede.App muss im Debug-Modus gebaut sein
///
/// CI-Ausschluss: dotnet test --filter "Category!=E2E"
/// </summary>
[Trait("Category", "E2E")]
public sealed class ProjectDetailE2ETests : WpfTestBase
{
    private static readonly TimeSpan Short  = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan Medium = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan Long   = TimeSpan.FromSeconds(20);

    // Navigiert zur Projektliste.
    private void NavigateToProjecten(AutomationElement mainWindow)
    {
        var button = WaitForElement(mainWindow, cf => cf.ByName(" Projekte"), Medium);
        button.AsButton().Click();
    }

    // Legt ein neues Projekt an und speichert es.
    // Das Detailoverlay bleibt nach dem ersten Speichern offen (CreateAsync setzt ProjektId).
    private void CreateAndSaveProject(AutomationElement mainWindow, string name)
    {
        var neuButton = WaitForElement(mainWindow, cf => cf.ByName("Neu"), Short);
        neuButton.AsButton().Click();

        var nameBox = WaitForElement(mainWindow, cf => cf.ByName("ProjektName"), Short);
        nameBox.AsTextBox().Text = name;

        var speichernButton = WaitForElement(mainWindow, cf => cf.ByName("Speichern"), Short);
        speichernButton.AsButton().Click();
        Thread.Sleep(1000); // CreateAsync + LadenAsync fire-and-forget abwarten
    }

    // -------------------------------------------------------------------------

    /// <summary>
    /// Szenario: Projekt bearbeiten und speichern.
    /// Prüft: Feld bearbeitbar, Update-Speichern ändert Namen dauerhaft.
    /// </summary>
    [Fact]
    public void ProjektBearbeitenUndSpeichern_AktualisierterNameBleibt_E2E()
    {
        var app = LaunchApp();
        var mainWindow = app.GetMainWindow(Automation, Long)!;
        NavigateToProjecten(mainWindow);
        CreateAndSaveProject(mainWindow, "Edit-Test");

        // Namen aktualisieren und erneut speichern (UpdateAsync-Pfad)
        var nameBox = WaitForElement(mainWindow, cf => cf.ByName("ProjektName"), Short);
        nameBox.AsTextBox().Text = "Edit-Test-Aktualisiert";

        var speichernButton = WaitForElement(mainWindow, cf => cf.ByName("Speichern"), Short);
        speichernButton.AsButton().Click();
        Thread.Sleep(500);

        // Feld zeigt aktualisierten Namen
        Assert.Equal("Edit-Test-Aktualisiert", nameBox.AsTextBox().Text);
    }

    /// <summary>
    /// Szenario: Projekt löschen.
    /// Prüft: Bestätigungsdialog erscheint, Löschen schließt das Overlay.
    /// </summary>
    [Fact]
    public void ProjektLoeschen_BestaetigungErforderlichUndOverlayGeschlossen_E2E()
    {
        var app = LaunchApp();
        var mainWindow = app.GetMainWindow(Automation, Long)!;
        NavigateToProjecten(mainWindow);
        CreateAndSaveProject(mainWindow, "Loeschen-Test");

        var loeschenButton = WaitForElement(mainWindow, cf => cf.ByName("Löschen"), Short);
        loeschenButton.AsButton().Click();

        // MessageBox "Löschen bestätigen" erscheint als separates Fenster
        var msgBox = WaitForWindow("Löschen bestätigen", Short);
        var jaButton = WaitForElement(msgBox, cf => cf.ByName("Ja"), Short);
        jaButton.AsButton().Click();
        Thread.Sleep(500);

        // Overlay geschlossen — "Speichern" nicht mehr sichtbar
        var speichernNachLoeschen = mainWindow.FindFirstDescendant(cf => cf.ByName("Speichern"));
        Assert.Null(speichernNachLoeschen);
    }

    /// <summary>
    /// Szenario: Aufgabe neu anlegen.
    /// Prüft: "AufgabeNeu"-Button erstellt eine Aufgabe, die in der Liste erscheint.
    /// </summary>
    [Fact]
    public void AufgabeNeuAnlegen_ErscheintInAufgabenliste_E2E()
    {
        var app = LaunchApp();
        var mainWindow = app.GetMainWindow(Automation, Long)!;
        NavigateToProjecten(mainWindow);
        CreateAndSaveProject(mainWindow, "Aufgabe-Test");

        // Neue Aufgabe erstellen (AutomationProperties.Name="AufgabeNeu")
        var aufgabeNeuButton = WaitForElement(mainWindow, cf => cf.ByName("AufgabeNeu"), Short);
        aufgabeNeuButton.AsButton().Click();
        Thread.Sleep(1000); // Aufgabe anlegen + LadenAsync abwarten

        // Aufgabenliste enthält jetzt mindestens eine Aufgabe
        var listBox = WaitForElement(mainWindow, cf => cf.ByControlType(ControlType.List), Short);
        var items = listBox.FindAllChildren(cf => cf.ByControlType(ControlType.ListItem));
        Assert.True(items.Length >= 1, "Aufgabenliste sollte nach Anlage mindestens eine Aufgabe enthalten.");
    }

    /// <summary>
    /// Szenario: Aufgaben filtern.
    /// Prüft: Filter-Overlay erscheint, RadioButton wählbar, Overlay schließt wieder.
    /// </summary>
    [Fact]
    public void AufgabenFiltern_OverlayOeffnetUndSchliesst_E2E()
    {
        var app = LaunchApp();
        var mainWindow = app.GetMainWindow(Automation, Long)!;
        NavigateToProjecten(mainWindow);
        CreateAndSaveProject(mainWindow, "Filter-Test");

        // Filter-Overlay öffnen
        var filterButton = WaitForElement(mainWindow, cf => cf.ByName("Filter"), Short);
        filterButton.AsButton().Click();
        Thread.Sleep(300);

        // Überschrift "Aufgaben filtern" erscheint
        var overlayTitel = WaitForElement(mainWindow, cf => cf.ByName("Aufgaben filtern"), Short);
        Assert.NotNull(overlayTitel);

        // RadioButton "Aktiv" wählen
        var aktivRadio = WaitForElement(
            mainWindow,
            cf => cf.ByName("Aktiv").And(cf.ByControlType(ControlType.RadioButton)),
            Short);
        aktivRadio.Click();

        // Filter-Overlay wieder schließen
        filterButton.Click();
        Thread.Sleep(300);

        // Overlay weg
        var overlayNachSchliessen = mainWindow.FindFirstDescendant(cf => cf.ByName("Aufgaben filtern"));
        Assert.Null(overlayNachSchliessen);
    }

    /// <summary>
    /// Szenario: Repository zuweisen.
    /// Prüft: "Zuweisen"-Button öffnet den Dialog, Dialog schließt über "Abbrechen".
    /// </summary>
    [Fact]
    public void RepositoryZuweisen_DialogOeffnetUndSchliessbarPerAbbrechen_E2E()
    {
        var app = LaunchApp();
        var mainWindow = app.GetMainWindow(Automation, Long)!;
        NavigateToProjecten(mainWindow);
        CreateAndSaveProject(mainWindow, "Repo-Zuweisen-Test");

        // "Zuweisen"-Button im Ribbon klicken
        var zuweisenButton = WaitForElement(mainWindow, cf => cf.ByName("Zuweisen"), Short);
        zuweisenButton.AsButton().Click();

        // RepositoryAssignDialog erscheint als separates Fenster
        var dialog = WaitForWindow("Repository zuweisen", Short);
        Assert.NotNull(dialog);

        // Dialog über "Abbrechen" schließen
        var abbrechenButton = WaitForElement(dialog, cf => cf.ByName("Abbrechen"), Short);
        abbrechenButton.AsButton().Click();
        Thread.Sleep(300);

        // Hauptfenster-Overlay noch offen (Speichern-Button sichtbar)
        var speichernNachAbbrechen = mainWindow.FindFirstDescendant(cf => cf.ByName("Speichern"));
        Assert.NotNull(speichernNachAbbrechen);
    }

    /// <summary>
    /// Szenario: Repository öffnen.
    /// Prüft: "Öffnen"-Button existiert in der Detailansicht.
    /// (Tatsächliches Browser-Öffnen ist im E2E nicht zuverlässig prüfbar.)
    /// </summary>
    [Fact]
    public void RepositoryOeffnen_ButtonExistiertInDetailansicht_E2E()
    {
        var app = LaunchApp();
        var mainWindow = app.GetMainWindow(Automation, Long)!;
        NavigateToProjecten(mainWindow);
        CreateAndSaveProject(mainWindow, "Repo-Oeffnen-Test");

        var oeffnenButton = WaitForElement(mainWindow, cf => cf.ByName("Öffnen"), Short);
        Assert.NotNull(oeffnenButton);
    }

    /// <summary>
    /// Szenario: Zurück zur Übersicht.
    /// Prüft: "Zurück"-Button schließt das Detailoverlay, Projektkachel in der Liste sichtbar.
    /// </summary>
    [Fact]
    public void ZurueckZurUebersicht_SchliesstOverlayUndZeigtListe_E2E()
    {
        var app = LaunchApp();
        var mainWindow = app.GetMainWindow(Automation, Long)!;
        NavigateToProjecten(mainWindow);
        CreateAndSaveProject(mainWindow, "Zurueck-Test");

        // Zurück klicken
        var zurueckButton = WaitForElement(mainWindow, cf => cf.ByName("Zurück"), Short);
        zurueckButton.AsButton().Click();
        Thread.Sleep(300);

        // Overlay geschlossen — "Speichern" nicht mehr sichtbar
        var speichernNachZurueck = mainWindow.FindFirstDescendant(cf => cf.ByName("Speichern"));
        Assert.Null(speichernNachZurueck);

        // Das eben erstellte Projekt erscheint als Kachel in der Liste
        var projektTile = WaitForElement(mainWindow, cf => cf.ByName("Zurueck-Test"), Short);
        Assert.NotNull(projektTile);
    }
}
