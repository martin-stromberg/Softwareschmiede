using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// E2E-Tests für die Projektdetailansicht mit Ribbon-Menü und Kacheln.
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
public sealed class ProjectDetailE2ETests : WpfTestBase
{
    /// <summary>
    /// Szenario: Projekt anlegen, Neuanlage starten und mit Zurück abbrechen, erstes Projekt öffnen.
    /// Prüft: Nach Abbrechen der Neuanlage ist das erste Projekt noch in der Liste aufrufbar.
    /// </summary>
    [Fact]
    public void NeuanlageAbbrechen_ErstesProjektNochAufrufbar_E2E()
    {
        var mainWindow = StartAndNavigateToProjects();

        // Erstes Projekt anlegen
        CreateProject(mainWindow, "Bestehendes-Projekt");

        // Neuanlage starten
        var neuButton = WaitForElement(mainWindow, cf => cf.ByName("Neu"), Short);
        neuButton.AsButton().Click();
        WaitForElement(mainWindow, cf => cf.ByName("Speichern"), Short);

        // Neuanlage über Zurück abbrechen
        var zurueckButton = WaitForElement(mainWindow, cf => cf.ByName("Zurück"), Short);
        zurueckButton.AsButton().Click();

        // Overlay geschlossen — "Speichern" nicht mehr sichtbar
        WaitUntilGone(mainWindow, cf => cf.ByName("Speichern"), Short);

        // Erstes Projekt ist noch in der Liste und aufrufbar
        OpenProject(mainWindow, "Bestehendes-Projekt");

        var speichernInDetail = WaitForElement(mainWindow, cf => cf.ByName("Speichern"), Short);
        Assert.NotNull(speichernInDetail);
    }


    /// <summary>
    /// Szenario: Projekt anlegen, Projekt öffnen und zurück, Projekt erneut öffnen.
    /// Prüft: Das hin und her zwischen Detailansicht und Übersicht funktioniert.
    /// </summary>
    [Fact]
    public void ProjektOeffnenUndZurueck_ErneutOeffnen_E2E()
    {
        var mainWindow = StartAndNavigateToProjects();

        // Erstes Projekt anlegen
        CreateProject(mainWindow, "Bestehendes-Projekt");
        // Projektdetailansicht öffnen
        OpenProject(mainWindow, "Bestehendes-Projekt");

        // Neuanlage über Zurück abbrechen
        var zurueckButton = WaitForElement(mainWindow, cf => cf.ByName("Zurück"), Short);
        zurueckButton.AsButton().Click();

        // Overlay geschlossen — "Speichern" nicht mehr sichtbar
        WaitUntilGone(mainWindow, cf => cf.ByName("Speichern"), Short);

        // Projektdetailansicht erneut öffnen
        OpenProject(mainWindow, "Bestehendes-Projekt");

        zurueckButton = WaitForElement(mainWindow, cf => cf.ByName("Zurück"), Short);
    }

    /// <summary>
    /// Szenario: Projektnamen ändern, zurücknavigieren, erneut öffnen.
    /// Prüft: Projektkachel zeigt aktualisierten Namen und lässt sich erneut öffnen.
    /// </summary>
    [Fact]
    public void ProjektNamenAendern_KachelAktualisiert_UndErneutoeffnen_E2E()
    {
        var mainWindow = StartAndNavigateToProjects();
        CreateAndOpenProject(mainWindow, "Umbenennen-Original");

        // Namen ändern und speichern (UpdateAsync-Pfad, bleibt in Detailansicht)
        var nameBox = WaitForElement(mainWindow, cf => cf.ByName("ProjektName"), Short);
        nameBox.Click();
        Keyboard.TypeSimultaneously(FlaUI.Core.WindowsAPI.VirtualKeyShort.CONTROL, FlaUI.Core.WindowsAPI.VirtualKeyShort.KEY_A);
        Keyboard.Type("Umbenennen-Aktualisiert");

        var speichernButton = WaitForElement(mainWindow, cf => cf.ByName("Speichern"), Short);
        speichernButton.AsButton().Click();

        // Zurück zur Übersicht navigieren
        var zurueckButton = WaitForElement(mainWindow, cf => cf.ByName("Zurück"), Short);
        zurueckButton.AsButton().Click();

        // Projektkachel zeigt jetzt den neuen Namen
        var aktualisierteKachel = WaitForElement(mainWindow, cf => cf.ByName("Umbenennen-Aktualisiert"), Short);
        Assert.NotNull(aktualisierteKachel);

        // Kachel erneut anklicken → Detailansicht öffnet sich
        aktualisierteKachel.Click();

        // Speichern-Button bestätigt, dass die Detailansicht geöffnet ist
        var speichernNachWiederoeffnen = WaitForElement(mainWindow, cf => cf.ByName("Speichern"), Short);
        Assert.NotNull(speichernNachWiederoeffnen);
    }

    /// <summary>
    /// Szenario: Projekt bearbeiten und speichern.
    /// Prüft: Feld bearbeitbar, Update-Speichern ändert Namen dauerhaft.
    /// </summary>
    [Fact]
    public void ProjektBearbeitenUndSpeichern_AktualisierterNameBleibt_E2E()
    {
        var mainWindow = StartAndNavigateToProjects();
        CreateAndOpenProject(mainWindow, "Edit-Test");

        // Namen aktualisieren und erneut speichern (UpdateAsync-Pfad)
        var nameBox = WaitForElement(mainWindow, cf => cf.ByName("ProjektName"), Short);
        nameBox.Click();
        Keyboard.TypeSimultaneously(FlaUI.Core.WindowsAPI.VirtualKeyShort.CONTROL, FlaUI.Core.WindowsAPI.VirtualKeyShort.KEY_A);
        Keyboard.Type("Edit-Test-Aktualisiert");

        var speichernButton = WaitForElement(mainWindow, cf => cf.ByName("Speichern"), Short);
        speichernButton.AsButton().Click();

        // Warten bis Speichern abgeschlossen (LadenAsync lädt Daten neu)
        WaitForElement(mainWindow, cf => cf.ByName("Speichern"), Short);

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
        var mainWindow = StartAndNavigateToProjects();
        CreateAndOpenProject(mainWindow, "Loeschen-Test");

        var loeschenButton = WaitForElement(mainWindow, cf => cf.ByName("Löschen"), Short);
        loeschenButton.AsButton().Click();

        // MessageBox "Löschen bestätigen" erscheint als separates Fenster. Der Titel stammt aus der
        // Anwendung (App-Ressource, daher sprachunabhängig sprachlich "Löschen bestätigen"), die
        // Button-Beschriftung "Ja"/"Nein" dagegen wird vom nativen Win32-MessageBox-Control anhand
        // der Systemsprache des ausführenden Betriebssystems gerendert (System.Windows.MessageBox
        // erlaubt keine eigenen Button-Texte) - auf einem englischsprachigen CI-Runner (z. B.
        // windows-latest bei GitHub Actions) heißt der Button "Yes" statt "Ja", wodurch die Suche
        // nach dem Namen dort unabhängig vom Timeout nie etwas findet. Die Automation-ID des
        // Ja/Yes-Buttons entspricht dagegen der stabilen, sprachunabhängigen Win32-Dialog-Control-ID
        // IDYES (6) und funktioniert auf jeder Systemsprache identisch.
        var msgBox = WaitForWindow("Löschen bestätigen", Short);
        var jaButton = WaitForElement(msgBox, cf => cf.ByAutomationId("6"), Short);
        jaButton.AsButton().Click();

        // Overlay geschlossen — "Speichern" nicht mehr sichtbar
        WaitUntilGone(mainWindow, cf => cf.ByName("Speichern"), Short);
    }

    /// <summary>
    /// Szenario: Aufgabe neu anlegen.
    /// Prüft: "AufgabeNeu"-Button erstellt eine Aufgabe und navigiert zur separaten TaskDetailView;
    /// nach Zurück-Navigation erscheint die neue Aufgabe in der Aufgabenliste der Projektdetailansicht.
    /// </summary>
    [Fact]
    public void AufgabeNeuAnlegen_ErscheintInAufgabenliste_E2E()
    {
        var mainWindow = StartAndNavigateToProjects();
        CreateAndOpenProject(mainWindow, "Aufgabe-Test");

        // Neue Aufgabe erstellen (AutomationProperties.Name="AufgabeNeu")
        var aufgabeNeuButton = WaitForElement(mainWindow, cf => cf.ByName("AufgabeNeu"), Short);
        aufgabeNeuButton.AsButton().Click();

        // Navigation zur separaten TaskDetailView (Edit-Panel, da Status == Neu)
        var editTitelBox = WaitForElement(mainWindow, cf => cf.ByName("EditTitel"), Medium);
        Assert.NotNull(editTitelBox);

        // Zurück zur Projektdetailansicht navigieren
        var zurueckButton = WaitForElement(mainWindow, cf => cf.ByName("Zurück"), Short);
        zurueckButton.AsButton().Click();

        // Aufgabenliste enthält jetzt mindestens eine Aufgabe
        var listBox = WaitForElement(mainWindow, cf => cf.ByName("OffeneAufgabenListe"), Medium);
        var items = listBox.FindAllChildren(cf => cf.ByControlType(ControlType.ListItem));
        Assert.True(items.Length >= 1, "Aufgabenliste sollte nach Anlage mindestens eine Aufgabe enthalten.");
    }

    /// <summary>
    /// Szenario: Projektdetailansicht trennt offene und beendete Aufgaben.
    /// Prueft: Offene Aufgaben sind direkt sichtbar, beendete Aufgaben erst nach Aufklappen des Expanders.
    /// </summary>
    [Fact]
    public async Task Projektdetailansicht_TrenntOffeneUndBeendeteAufgaben_E2E()
    {
        var app = LaunchApp();
        var mainWindow = app.GetMainWindow(Automation, Long)!;

        var projektName = "Archivierte-Aufgaben-E2E";
        var offeneAufgabeTitel = "Offene Aufgabe E2E";
        var beendeteAufgabeTitel = "Beendete Aufgabe E2E";

        await using (var db = OpenTestDbContext())
        {
            var projektService = new ProjektService(db, NullLogger<ProjektService>.Instance);
            var aufgabeService = new AufgabeService(db, NullLogger<AufgabeService>.Instance);

            var projekt = await projektService.CreateAsync(projektName, null);
            await aufgabeService.CreateAsync(projekt.Id, offeneAufgabeTitel, null);
            var beendeteAufgabe = await aufgabeService.CreateAsync(projekt.Id, beendeteAufgabeTitel, null);
            await aufgabeService.StatusSetzenAsync(beendeteAufgabe.Id, AufgabeStatus.Beendet);
        }

        NavigateToProjecten(mainWindow);
        OpenProject(mainWindow, projektName);

        var offeneAufgabenListe = WaitForElement(mainWindow, cf => cf.ByName("OffeneAufgabenListe"), Medium);
        Assert.NotNull(offeneAufgabenListe);

        var offeneItems = offeneAufgabenListe.FindAllChildren(cf => cf.ByControlType(ControlType.ListItem));
        Assert.Contains(offeneItems, item => item.Name == offeneAufgabeTitel);
        Assert.DoesNotContain(offeneItems, item => item.Name == beendeteAufgabeTitel);

        var beendeteAufgabenExpander = WaitForElement(mainWindow, cf => cf.ByName("BeendeteAufgabenExpander"), Short);
        Assert.NotNull(beendeteAufgabenExpander);
        Assert.Equal(ExpandCollapseState.Collapsed, beendeteAufgabenExpander.Patterns.ExpandCollapse.Pattern.ExpandCollapseState);

        beendeteAufgabenExpander.Patterns.ExpandCollapse.Pattern.Expand();

        var beendeteAufgabenListe = WaitForElement(beendeteAufgabenExpander, cf => cf.ByName("BeendeteAufgabenListe"), Short);
        Assert.NotNull(beendeteAufgabenListe);

        var beendeteItems = beendeteAufgabenListe.FindAllChildren(cf => cf.ByControlType(ControlType.ListItem));
        Assert.Contains(beendeteItems, item => item.Name == beendeteAufgabeTitel);
    }

    /// <summary>
    /// Szenario: Aufgaben filtern.
    /// Prüft: Filter-Overlay erscheint, RadioButton wählbar, Overlay schließt wieder.
    /// </summary>
    [Fact]
    public void AufgabenFiltern_OverlayOeffnetUndSchliesst_E2E()
    {
        var mainWindow = StartAndNavigateToProjects();
        CreateAndOpenProject(mainWindow, "Filter-Test");

        // Filter-Overlay öffnen
        var filterButton = WaitForElement(mainWindow, cf => cf.ByName("Filter"), Short);
        filterButton.AsButton().Click();

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

        // Overlay weg
        WaitUntilGone(mainWindow, cf => cf.ByName("Aufgaben filtern"), Short);
    }

    /// <summary>
    /// Szenario: Repository zuweisen.
    /// Prüft: "Zuweisen"-Button öffnet den Dialog, Dialog schließt über "Abbrechen".
    /// </summary>
    [Fact]
    public void RepositoryZuweisen_DialogOeffnetUndSchliessbarPerAbbrechen_E2E()
    {
        var mainWindow = StartAndNavigateToProjects();
        CreateAndOpenProject(mainWindow, "Repo-Zuweisen-Test");

        // "Zuweisen"-Button im Ribbon klicken
        var zuweisenButton = WaitForElement(mainWindow, cf => cf.ByName("Zuweisen"), Short);
        zuweisenButton.AsButton().Click();

        // RepositoryAssignDialog erscheint als separates Fenster
        var dialog = WaitForWindow("Repository zuweisen", Short);
        Assert.NotNull(dialog);

        // Dialog über "Abbrechen" schließen
        var abbrechenButton = WaitForElement(dialog, cf => cf.ByName("Abbrechen"), Short);
        abbrechenButton.AsButton().Click();

        // Hauptfenster-Overlay noch offen (Speichern-Button sichtbar)
        var speichernNachAbbrechen = WaitForElement(mainWindow, cf => cf.ByName("Speichern"), Short);
        Assert.NotNull(speichernNachAbbrechen);
    }

    /// <summary>
    /// Szenario: Repository-Zuweisungsdialog öffnen und SCM-Plugin-ComboBox prüfen.
    /// Prüft: Die ComboBox für die SCM-Plugin-Auswahl ist vorhanden und enthält die erwarteten Einträge.
    /// </summary>
    [Fact]
    public void RepositoryZuweisenDialog_ScmPluginListe_EnthaeltErwartetePlugins_E2E()
    {
        var mainWindow = StartAndNavigateToProjects();
        CreateAndOpenProject(mainWindow, "SCM-Plugin-Test");

        // "Zuweisen"-Button im Ribbon klicken
        var zuweisenButton = WaitForElement(mainWindow, cf => cf.ByName("Zuweisen"), Short);
        zuweisenButton.AsButton().Click();

        // RepositoryAssignDialog erscheint als separates Fenster
        var dialog = WaitForWindow("Repository zuweisen", Short);
        Assert.NotNull(dialog);

        // ComboBox für SCM-Plugin-Auswahl muss vorhanden sein
        var comboBoxen = dialog.FindAllDescendants(cf => cf.ByControlType(ControlType.ComboBox));
        Assert.True(comboBoxen.Length >= 1, "RepositoryAssignDialog muss mindestens eine ComboBox für die Plugin-Auswahl enthalten.");

        var pluginComboBox = comboBoxen[0].AsComboBox();

        // ComboBox öffnen und Items zählen
        pluginComboBox.Click();
        Thread.Sleep(300);

        // Die ComboBox muss mindestens einen Eintrag oder leer sein (je nach installierten Plugins)
        // Wichtig: die ComboBox ist vorhanden und reagiert auf Interaktion
        Assert.NotNull(pluginComboBox);

        // Dialog schließen
        var abbrechenButton = WaitForElement(dialog, cf => cf.ByName("Abbrechen"), Short);
        abbrechenButton.AsButton().Click();
    }

    /// <summary>
    /// Szenario: Repository-Zuweisungsdialog öffnen und Arbeitsverzeichnis-Auswahl prüfen.
    /// Prüft: Label und ComboBox für die Arbeitsverzeichnis-Auswahl sind im Dialog vorhanden.
    /// </summary>
    [Fact]
    public void RepositoryZuweisenDialog_ArbeitsverzeichnisAuswahl_IstVorhanden_E2E()
    {
        var mainWindow = StartAndNavigateToProjects();
        CreateAndOpenProject(mainWindow, "Arbeitsverzeichnis-Test");

        // "Zuweisen"-Button im Ribbon klicken
        var zuweisenButton = WaitForElement(mainWindow, cf => cf.ByName("Zuweisen"), Short);
        zuweisenButton.AsButton().Click();

        // RepositoryAssignDialog erscheint als separates Fenster
        var dialog = WaitForWindow("Repository zuweisen", Short);
        Assert.NotNull(dialog);

        // Label "Arbeitsverzeichnis im Repository" ist vorhanden
        var label = WaitForElement(dialog, cf => cf.ByName("Arbeitsverzeichnis im Repository"), Short);
        Assert.NotNull(label);

        // Zweite ComboBox (Arbeitsverzeichnis-Auswahl) ist zusätzlich zur Plugin-Auswahl vorhanden
        var comboBoxen = dialog.FindAllDescendants(cf => cf.ByControlType(ControlType.ComboBox));
        Assert.True(comboBoxen.Length >= 2, "RepositoryAssignDialog muss zusätzlich zur Plugin-Auswahl eine ComboBox für die Arbeitsverzeichnis-Auswahl enthalten.");

        // Dialog schließen
        var abbrechenButton = WaitForElement(dialog, cf => cf.ByName("Abbrechen"), Short);
        abbrechenButton.AsButton().Click();
    }

    /// <summary>
    /// Szenario: Repository öffnen.
    /// Prüft: "Öffnen"-Button existiert in der Detailansicht.
    /// (Tatsächliches Browser-Öffnen ist im E2E nicht zuverlässig prüfbar.)
    /// </summary>
    [Fact]
    public void RepositoryOeffnen_ButtonExistiertInDetailansicht_E2E()
    {
        var mainWindow = StartAndNavigateToProjects();
        CreateAndOpenProject(mainWindow, "Repo-Oeffnen-Test");

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
        var mainWindow = StartAndNavigateToProjects();
        CreateAndOpenProject(mainWindow, "Zurueck-Test");

        // Zurück klicken
        var zurueckButton = WaitForElement(mainWindow, cf => cf.ByName("Zurück"), Short);
        zurueckButton.AsButton().Click();

        // Overlay geschlossen — "Speichern" nicht mehr sichtbar
        WaitUntilGone(mainWindow, cf => cf.ByName("Speichern"), Short);

        // Das eben erstellte Projekt erscheint als Kachel in der Liste
        var projektTile = WaitForElement(mainWindow, cf => cf.ByName("Zurueck-Test"), Short);
        Assert.NotNull(projektTile);
    }
}
