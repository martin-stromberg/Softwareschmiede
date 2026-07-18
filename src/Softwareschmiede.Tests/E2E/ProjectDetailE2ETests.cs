using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
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
    /// Szenario: Projekt anlegen; Neuanlage starten und über "Zurück" abbrechen; erstes Projekt
    /// öffnen und wieder verlassen; erneut öffnen; zuletzt zur Übersicht zurücknavigieren.
    /// Prüft: Nach Abbrechen der Neuanlage ist das erste Projekt noch in der Liste aufrufbar; das
    /// wiederholte Öffnen/Verlassen der Detailansicht funktioniert; die Übersicht zeigt die
    /// Projektkachel nach dem finalen "Zurück".
    /// </summary>
    [Fact]
    public void ProjektNavigation_NeuanlageAbbrechenUndOeffnenUndSchliessen_E2E()
    {
        var mainWindow = StartAndNavigateToProjects();

        // Erstes Projekt anlegen
        CreateProject(mainWindow, "Bestehendes-Projekt");

        // Neuanlage starten und über Zurück abbrechen
        var neuButton = WaitForElement(mainWindow, cf => cf.ByName("Neu"), Short);
        neuButton.AsButton().Click();
        WaitForElement(mainWindow, cf => cf.ByName("Speichern"), Short);

        ZurueckZurProjektuebersicht(mainWindow);

        // Erstes Projekt ist noch in der Liste und aufrufbar
        OpenProject(mainWindow, "Bestehendes-Projekt");
        var speichernInDetail = WaitForElement(mainWindow, cf => cf.ByName("Speichern"), Short);
        Assert.NotNull(speichernInDetail);

        // Zurück zur Übersicht, Projekt erneut öffnen
        ZurueckZurProjektuebersicht(mainWindow);

        OpenProject(mainWindow, "Bestehendes-Projekt");
        WaitForElement(mainWindow, cf => cf.ByName("Speichern"), Short);

        // Zurück zur Übersicht
        ZurueckZurProjektuebersicht(mainWindow);

        var projektTile = WaitForElement(mainWindow, cf => cf.ByName("Bestehendes-Projekt"), Short);
        Assert.NotNull(projektTile);
    }

    /// <summary>
    /// Szenario: Projekt anlegen und öffnen, Namen ändern und speichern, zur Übersicht
    /// zurücknavigieren, Projektkachel erneut öffnen, erneut umbenennen und speichern.
    /// Prüft: Die Projektkachel zeigt nach dem ersten Speichern den aktualisierten Namen; die
    /// erneute Bearbeitung (UpdateAsync-Pfad) hält den aktualisierten Namen im Textfeld.
    /// </summary>
    [Fact]
    public void ProjektBearbeiten_NamenAendernSpeichernZurueckUndErneutBearbeiten_E2E()
    {
        var mainWindow = StartAndNavigateToProjects();
        CreateAndOpenProject(mainWindow, "Umbenennen-Test");

        ProjektNamenAendernUndSpeichern(mainWindow, "Umbenennen-Test-Aktualisiert");

        // Zurück zur Übersicht navigieren
        var zurueckButton = WaitForElement(mainWindow, cf => cf.ByName("Zurück"), Short);
        zurueckButton.AsButton().Click();

        // Projektkachel zeigt jetzt den neuen Namen
        var aktualisierteKachel = WaitForElement(mainWindow, cf => cf.ByName("Umbenennen-Test-Aktualisiert"), Short);
        Assert.NotNull(aktualisierteKachel);

        // Kachel erneut anklicken → Detailansicht öffnet sich
        aktualisierteKachel.Click();
        var nameBox = WaitForElement(mainWindow, cf => cf.ByName("ProjektName"), Short);
        Assert.NotNull(nameBox);

        // Erneut bearbeiten und speichern (UpdateAsync-Pfad); Name bleibt aktualisiert
        ProjektNamenAendernUndSpeichern(mainWindow, "Umbenennen-Test-Aktualisiert-Erneut");
        var nameBoxNachReload = WaitForElement(mainWindow, cf => cf.ByName("ProjektName"), Short);
        Assert.Equal("Umbenennen-Test-Aktualisiert-Erneut", nameBoxNachReload.AsTextBox().Text);
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
    /// Szenario: Aufgabe neu anlegen und in die Aufgabenliste zurückkehren; anschließend das
    /// Filter-Overlay öffnen, den RadioButton "Aktiv" wählen und das Overlay wieder schließen.
    /// Prüft: "AufgabeNeu" erstellt eine Aufgabe und navigiert zur separaten TaskDetailView; nach
    /// Zurück-Navigation erscheint die neue Aufgabe in der Aufgabenliste; das Filter-Overlay öffnet
    /// und schließt sich korrekt.
    /// </summary>
    [Fact]
    public void AufgabenInProjektdetail_NeuAnlegenUndFiltern_E2E()
    {
        var mainWindow = StartAndNavigateToProjects();
        CreateAndOpenProject(mainWindow, "Aufgabe-Test");

        // Neue Aufgabe erstellen; Navigation zur separaten TaskDetailView (Edit-Panel, da Status == Neu)
        var editTitelBox = NeueAufgabeAnlegen(mainWindow);
        Assert.NotNull(editTitelBox);

        // Zurück zur Projektdetailansicht navigieren
        AufgabeDetailZurueck(mainWindow);

        // Aufgabenliste enthält jetzt mindestens eine Aufgabe
        var items = OffeneAufgabenItems(mainWindow);
        Assert.True(items.Length >= 1, "Aufgabenliste sollte nach Anlage mindestens eine Aufgabe enthalten.");

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
    /// Szenario: "Öffnen"-Button in der Detailansicht prüfen; anschließend den
    /// Repository-Zuweisungs-Dialog öffnen und die SCM-Plugin- sowie die
    /// Arbeitsverzeichnis-ComboBox prüfen; Dialog über "Abbrechen" schließen.
    /// Prüft: Der "Öffnen"-Button existiert; der Dialog enthält mindestens eine ComboBox für die
    /// Plugin-Auswahl sowie das Label und eine zweite ComboBox für die Arbeitsverzeichnis-Auswahl;
    /// nach "Abbrechen" bleibt das Hauptfenster-Overlay ("Speichern") weiterhin sichtbar.
    /// </summary>
    [Fact]
    public void RepositoryDialog_OeffnenButtonZuweisenPluginUndArbeitsverzeichnis_E2E()
    {
        var mainWindow = StartAndNavigateToProjects();
        CreateAndOpenProject(mainWindow, "Repository-Dialog-Test");

        var oeffnenButton = WaitForElement(mainWindow, cf => cf.ByName("Öffnen"), Short);
        Assert.NotNull(oeffnenButton);

        // "Zuweisen"-Button im Ribbon klicken
        var zuweisenButton = WaitForElement(mainWindow, cf => cf.ByName("Zuweisen"), Short);
        zuweisenButton.AsButton().Click();

        // RepositoryAssignDialog erscheint als separates Fenster
        var dialog = WaitForWindow("Repository zuweisen", Short);
        Assert.NotNull(dialog);

        // ComboBox für SCM-Plugin-Auswahl muss vorhanden sein
        var comboBoxen = dialog.FindAllDescendants(cf => cf.ByControlType(ControlType.ComboBox));
        Assert.True(comboBoxen.Length >= 1, "RepositoryAssignDialog muss mindestens eine ComboBox für die Plugin-Auswahl enthalten.");

        // Label "Arbeitsverzeichnis im Repository" ist vorhanden
        var label = WaitForElement(dialog, cf => cf.ByName("Arbeitsverzeichnis im Repository"), Short);
        Assert.NotNull(label);

        // Zweite ComboBox (Arbeitsverzeichnis-Auswahl) ist zusätzlich zur Plugin-Auswahl vorhanden
        Assert.True(comboBoxen.Length >= 2, "RepositoryAssignDialog muss zusätzlich zur Plugin-Auswahl eine ComboBox für die Arbeitsverzeichnis-Auswahl enthalten.");

        // Dialog über "Abbrechen" schließen
        var abbrechenButton = WaitForElement(dialog, cf => cf.ByName("Abbrechen"), Short);
        abbrechenButton.AsButton().Click();

        // Hauptfenster-Overlay noch offen (Speichern-Button sichtbar)
        var speichernNachAbbrechen = WaitForElement(mainWindow, cf => cf.ByName("Speichern"), Short);
        Assert.NotNull(speichernNachAbbrechen);
    }

    /// <summary>
    /// Klickt den "Zurück"-Button in der Projektdetailansicht und wartet auf das Verschwinden des
    /// "Speichern"-Buttons (Bestätigung, dass das Overlay geschlossen und die Übersicht wieder sichtbar ist).
    /// </summary>
    /// <param name="mainWindow">Das Hauptfenster der Anwendung.</param>
    private void ZurueckZurProjektuebersicht(AutomationElement mainWindow)
    {
        var zurueckButton = WaitForElement(mainWindow, cf => cf.ByName("Zurück"), Short);
        zurueckButton.AsButton().Click();

        WaitUntilGone(mainWindow, cf => cf.ByName("Speichern"), Short);
    }
}
