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
/// Konsolidierung (Issue #153): <see cref="ProjektDetailSzenarien"/> führt alle sechs Szenarien
/// (Navigation, Bearbeiten, Aufgaben/Filtern, Repository-Dialog, Offene/beendete Aufgaben-Trennung,
/// Löschen) als aufeinanderfolgende Phasen in einem gemeinsamen App-Lifecycle aus. Jede Phase räumt ihr
/// Projekt über <see cref="WpfTestBase.DeleteCurrentProject"/> auf und kehrt über
/// <see cref="WpfTestBase.NavigateBackToDashboard"/> zum Dashboard zurück, bevor die nächste Phase mit
/// <see cref="WpfTestBase.NavigateToProjects"/> neu beginnt - ein erneuter Klick auf " Projekte" direkt
/// aus einer bereits geöffneten Projektdetailansicht heraus navigiert nicht zuverlässig zur Übersicht,
/// sondern bleibt in der zuletzt geöffneten Projektansicht (daher immer zuerst zurück zum Dashboard).
///
/// CI-Regular-Lauf: dotnet test --filter "Category!=OsInterface"
/// </summary>
[Trait("Category", "E2E")]
[OsInterface]
[Collection("E2E")]
public sealed class ProjectDetailE2ETests : WpfTestBase
{
    /// <summary>
    /// Führt sechs Projektdetail-Szenarien nacheinander in einem gemeinsamen App-Lifecycle aus:
    /// Navigation (Neuanlage abbrechen, öffnen/schließen), Bearbeiten (umbenennen), Aufgaben anlegen
    /// und filtern, Repository-Dialog prüfen, Trennung offener/beendeter Aufgaben, sowie Löschen.
    /// Jede Phase räumt ihr Projekt auf, bevor die nächste beginnt.
    /// </summary>
    [Fact]
    public async Task ProjektDetailSzenarien()
    {
        var mainWindow = LaunchAppAndGetMainWindow();

        ProjektNavigation_NeuanlageAbbrechenUndOeffnenUndSchliessen_E2E(mainWindow);
        ProjektBearbeiten_NamenAendernSpeichernZurueckUndErneutBearbeiten_E2E(mainWindow);
        AufgabenInProjektdetail_NeuAnlegenUndFiltern_E2E(mainWindow);
        RepositoryDialog_OeffnenButtonZuweisenPluginUndArbeitsverzeichnis_E2E(mainWindow);
        await Projektdetailansicht_TrenntOffeneUndBeendeteAufgaben_E2E(mainWindow);
        ProjektLoeschen_BestaetigungErforderlichUndOverlayGeschlossen_E2E(mainWindow);
    }

    /// <summary>
    /// Szenario: Projekt anlegen; Neuanlage starten und über "Zurück" abbrechen; erstes Projekt
    /// öffnen und wieder verlassen; erneut öffnen; zuletzt zur Übersicht zurücknavigieren.
    /// Prüft: Nach Abbrechen der Neuanlage ist das erste Projekt noch in der Liste aufrufbar; das
    /// wiederholte Öffnen/Verlassen der Detailansicht funktioniert; die Übersicht zeigt die
    /// Projektkachel nach dem finalen "Zurück".
    /// </summary>
    /// <param name="mainWindow">Das bereits laufende Hauptfenster, in dem diese Phase ausgeführt wird.</param>
    private void ProjektNavigation_NeuanlageAbbrechenUndOeffnenUndSchliessen_E2E(AutomationElement mainWindow)
    {
        NavigateToProjects(mainWindow);

        // Erstes Projekt anlegen
        CreateProject(mainWindow, "Bestehendes-Projekt");

        // Neuanlage starten und über Zurück abbrechen
        var neuButton = WaitForElement(mainWindow, cf => cf.ByName("Neu"), Short);
        neuButton.AsButton().Click();
        WaitForElement(mainWindow, cf => cf.ByName("Speichern"), Short);

        ZurueckZurProjektuebersicht(mainWindow);

        // Erstes Projekt ist noch in der Liste und aufrufbar
        OpenProject(mainWindow, "Bestehendes-Projekt");
        WaitForElement(mainWindow, cf => cf.ByName("Speichern"), Short);

        // Zurück zur Übersicht, Projekt erneut öffnen
        ZurueckZurProjektuebersicht(mainWindow);

        OpenProject(mainWindow, "Bestehendes-Projekt");
        WaitForElement(mainWindow, cf => cf.ByName("Speichern"), Short);

        // Zurück zur Übersicht
        ZurueckZurProjektuebersicht(mainWindow);

        WaitForElement(mainWindow, cf => cf.ByName("Bestehendes-Projekt"), Short);

        OpenProject(mainWindow, "Bestehendes-Projekt");
        DeleteCurrentProject(mainWindow);
        NavigateBackToDashboard(mainWindow);
    }

    /// <summary>
    /// Szenario: Projekt anlegen und öffnen, Namen ändern und speichern, zur Übersicht
    /// zurücknavigieren, Projektkachel erneut öffnen, erneut umbenennen und speichern.
    /// Prüft: Die Projektkachel zeigt nach dem ersten Speichern den aktualisierten Namen; die
    /// erneute Bearbeitung (UpdateAsync-Pfad) hält den aktualisierten Namen im Textfeld.
    /// </summary>
    /// <param name="mainWindow">Das bereits laufende Hauptfenster, in dem diese Phase ausgeführt wird.</param>
    private void ProjektBearbeiten_NamenAendernSpeichernZurueckUndErneutBearbeiten_E2E(AutomationElement mainWindow)
    {
        NavigateToProjects(mainWindow);
        CreateAndOpenProject(mainWindow, "Umbenennen-Test");

        ProjektNamenAendernUndSpeichern(mainWindow, "Umbenennen-Test-Aktualisiert");

        // Zurück zur Übersicht navigieren
        var zurueckButton = WaitForElement(mainWindow, cf => cf.ByName("Zurück"), Short);
        zurueckButton.AsButton().Click();

        // Projektkachel zeigt jetzt den neuen Namen
        var aktualisierteKachel = WaitForElement(mainWindow, cf => cf.ByName("Umbenennen-Test-Aktualisiert"), Short);

        // Kachel erneut anklicken → Detailansicht öffnet sich
        aktualisierteKachel.Click();
        WaitForElement(mainWindow, cf => cf.ByName("ProjektName"), Short);

        // Erneut bearbeiten und speichern (UpdateAsync-Pfad); Name bleibt aktualisiert
        ProjektNamenAendernUndSpeichern(mainWindow, "Umbenennen-Test-Aktualisiert-Erneut");
        var nameBoxNachReload = WaitForElement(mainWindow, cf => cf.ByName("ProjektName"), Short);
        Assert.Equal("Umbenennen-Test-Aktualisiert-Erneut", nameBoxNachReload.AsTextBox().Text);

        DeleteCurrentProject(mainWindow);
        NavigateBackToDashboard(mainWindow);
    }

    /// <summary>
    /// Szenario: Projekt löschen.
    /// Prüft: Bestätigungsdialog erscheint, Löschen schließt das Overlay.
    /// </summary>
    /// <param name="mainWindow">Das bereits laufende Hauptfenster, in dem diese Phase ausgeführt wird.</param>
    private void ProjektLoeschen_BestaetigungErforderlichUndOverlayGeschlossen_E2E(AutomationElement mainWindow)
    {
        NavigateToProjects(mainWindow);
        CreateAndOpenProject(mainWindow, "Loeschen-Test");

        DeleteCurrentProject(mainWindow);
    }

    /// <summary>
    /// Szenario: Projektdetailansicht trennt offene und beendete Aufgaben.
    /// Prueft: Offene Aufgaben sind direkt sichtbar, beendete Aufgaben erst nach Aufklappen des Expanders.
    /// </summary>
    /// <param name="mainWindow">Das bereits laufende Hauptfenster, in dem diese Phase ausgeführt wird.</param>
    private async Task Projektdetailansicht_TrenntOffeneUndBeendeteAufgaben_E2E(AutomationElement mainWindow)
    {
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

        NavigateToProjects(mainWindow);
        OpenProject(mainWindow, projektName);

        var offeneItems = OffeneAufgabenItems(mainWindow);
        Assert.Contains(offeneItems, item => item.Name == offeneAufgabeTitel);
        Assert.DoesNotContain(offeneItems, item => item.Name == beendeteAufgabeTitel);

        var beendeteAufgabenExpander = WaitForElement(mainWindow, cf => cf.ByName("BeendeteAufgabenExpander"), Short);
        Assert.Equal(ExpandCollapseState.Collapsed, beendeteAufgabenExpander.Patterns.ExpandCollapse.Pattern.ExpandCollapseState);

        beendeteAufgabenExpander.Patterns.ExpandCollapse.Pattern.Expand();

        var beendeteAufgabenListe = WaitForElement(beendeteAufgabenExpander, cf => cf.ByName("BeendeteAufgabenListe"), Short);

        var beendeteItems = beendeteAufgabenListe.FindAllChildren(cf => cf.ByControlType(ControlType.ListItem));
        Assert.Contains(beendeteItems, item => item.Name == beendeteAufgabeTitel);

        DeleteCurrentProject(mainWindow);
        NavigateBackToDashboard(mainWindow);
    }

    /// <summary>
    /// Szenario: Aufgabe neu anlegen und in die Aufgabenliste zurückkehren; anschließend das
    /// Filter-Overlay öffnen, den RadioButton "Aktiv" wählen und das Overlay wieder schließen.
    /// Prüft: "AufgabeNeu" erstellt eine Aufgabe und navigiert zur separaten TaskDetailView; nach
    /// Zurück-Navigation erscheint die neue Aufgabe in der Aufgabenliste; das Filter-Overlay öffnet
    /// und schließt sich korrekt.
    /// </summary>
    /// <param name="mainWindow">Das bereits laufende Hauptfenster, in dem diese Phase ausgeführt wird.</param>
    private void AufgabenInProjektdetail_NeuAnlegenUndFiltern_E2E(AutomationElement mainWindow)
    {
        NavigateToProjects(mainWindow);
        CreateAndOpenProject(mainWindow, "Aufgabe-Test");

        // Neue Aufgabe erstellen; Navigation zur separaten TaskDetailView (Edit-Panel, da Status == Neu)
        NeueAufgabeAnlegen(mainWindow);

        // Zurück zur Projektdetailansicht navigieren
        AufgabeDetailZurueck(mainWindow);

        // Aufgabenliste enthält jetzt mindestens eine Aufgabe
        var items = OffeneAufgabenItems(mainWindow);
        Assert.True(items.Length >= 1, "Aufgabenliste sollte nach Anlage mindestens eine Aufgabe enthalten.");

        // Filter-Overlay öffnen
        var filterButton = WaitForElement(mainWindow, cf => cf.ByName("Filter"), Short);
        filterButton.AsButton().Click();

        // Überschrift "Aufgaben filtern" erscheint
        WaitForElement(mainWindow, cf => cf.ByName("Aufgaben filtern"), Short);

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

        DeleteCurrentProject(mainWindow);
        NavigateBackToDashboard(mainWindow);
    }

    /// <summary>
    /// Szenario: "Öffnen"-Button in der Detailansicht prüfen; anschließend den
    /// Repository-Zuweisungs-Dialog öffnen und die SCM-Plugin- sowie die
    /// Arbeitsverzeichnis-ComboBox prüfen; Dialog über "Abbrechen" schließen.
    /// Prüft: Der "Öffnen"-Button existiert; der Dialog enthält mindestens eine ComboBox für die
    /// Plugin-Auswahl sowie das Label und eine zweite ComboBox für die Arbeitsverzeichnis-Auswahl;
    /// nach "Abbrechen" bleibt das Hauptfenster-Overlay ("Speichern") weiterhin sichtbar.
    /// </summary>
    /// <param name="mainWindow">Das bereits laufende Hauptfenster, in dem diese Phase ausgeführt wird.</param>
    private void RepositoryDialog_OeffnenButtonZuweisenPluginUndArbeitsverzeichnis_E2E(AutomationElement mainWindow)
    {
        NavigateToProjects(mainWindow);
        CreateAndOpenProject(mainWindow, "Repository-Dialog-Test");

        WaitForElement(mainWindow, cf => cf.ByName("Öffnen"), Short);

        // "Zuweisen"-Button im Ribbon klicken
        var zuweisenButton = WaitForElement(mainWindow, cf => cf.ByName("Zuweisen"), Short);
        zuweisenButton.AsButton().Click();

        // RepositoryAssignDialog erscheint als separates Fenster
        var dialog = WaitForWindow("Repository zuweisen", Short);

        // ComboBox für SCM-Plugin-Auswahl muss vorhanden sein
        var comboBoxen = dialog.FindAllDescendants(cf => cf.ByControlType(ControlType.ComboBox));
        Assert.True(comboBoxen.Length >= 1, "RepositoryAssignDialog muss mindestens eine ComboBox für die Plugin-Auswahl enthalten.");

        // Label "Arbeitsverzeichnis im Repository" ist vorhanden
        WaitForElement(dialog, cf => cf.ByName("Arbeitsverzeichnis im Repository"), Short);

        // Zweite ComboBox (Arbeitsverzeichnis-Auswahl) ist zusätzlich zur Plugin-Auswahl vorhanden
        Assert.True(comboBoxen.Length >= 2, "RepositoryAssignDialog muss zusätzlich zur Plugin-Auswahl eine ComboBox für die Arbeitsverzeichnis-Auswahl enthalten.");

        // Dialog über "Abbrechen" schließen
        var abbrechenButton = WaitForElement(dialog, cf => cf.ByName("Abbrechen"), Short);
        abbrechenButton.AsButton().Click();

        // Hauptfenster-Overlay noch offen (Speichern-Button sichtbar)
        WaitForElement(mainWindow, cf => cf.ByName("Speichern"), Short);

        DeleteCurrentProject(mainWindow);
        NavigateBackToDashboard(mainWindow);
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
