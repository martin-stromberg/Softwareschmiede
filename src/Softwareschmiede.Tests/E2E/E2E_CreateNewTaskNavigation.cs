using FlaUI.Core.AutomationElements;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// E2E-Tests für die Neuanlage von Aufgaben über die separate Aufgabendetailansicht (Feature 72).
///
/// Voraussetzungen:
/// - Windows-Desktop-Session (kein Headless-CI)
/// - Softwareschmiede.App muss im Debug-Modus gebaut sein
///
/// CI-Regular-Lauf: dotnet test --filter "Category!=OsInterface"
/// </summary>
public partial class End2EndTest
{
    /// <summary>
    /// Szenario: Neue Aufgabe erstellen, Titel ausfüllen, speichern (Phase Speichern); anschließend
    /// explizit zur Projektansicht zurückkehren und eine weitere Aufgabe über "Zurück" abbrechen
    /// (Phase Abbrechen). Prüft: Die gespeicherte Aufgabe bleibt nach dem Speichern in der
    /// TaskDetailView geöffnet, wird mit Status "Neu" persistiert und erscheint nach expliziter
    /// Rücknavigation in der Liste. Die im Abbrechen-Pfad eingegebene Titeländerung wird nicht
    /// persistiert, die zuvor angelegte Aufgabe (Status "Neu") bleibt jedoch weiterhin vorhanden.
    /// </summary>
    protected void AufgabeAnlegen_SpeichernPersistiert_UndAbbrechenVerwirftTitel_E2E(Window mainWindow)
    {
        NavigateToProjectsAndCreateProject(mainWindow, "NeueAufgabe-Test");

        // Phase Speichern
        NeueAufgabeAnlegen(mainWindow);
        AufgabeTitelSetzen(mainWindow, "Persistierte Neue Aufgabe");
        AufgabeDetailSpeichern(mainWindow, false);

        // Die TaskDetailView bleibt geöffnet; der Anwender kann direkt starten statt zur Liste zurückzufallen.
        WaitForElement(mainWindow, cf => cf.ByName("Starten"), Medium);
        WaitForElement(mainWindow, cf => cf.ByName("EditTitel"), Short);

        // Erst explizite Rücknavigation zeigt wieder die Projektliste.
        AufgabeDetailZurueck(mainWindow);
        WaitForElement(mainWindow, cf => cf.ByName("ProjektName"), Medium);

        // Neue Aufgabe erscheint mit aktualisiertem Titel in der Aufgabenliste.
        WaitForElement(mainWindow, cf => cf.ByName("Persistierte Neue Aufgabe"), Short);

        // Phase Abbrechen
        NeueAufgabeAnlegen(mainWindow);
        AufgabeTitelSetzen(mainWindow, "Nicht gespeicherter Titel");
        AufgabeDetailZurueck(mainWindow);

        // Der nicht gespeicherte Titel erscheint nicht in der Aufgabenliste
        var nichtGespeicherterTitel = mainWindow.FindFirstDescendant(cf => cf.ByName("Nicht gespeicherter Titel"));
        Assert.Null(nichtGespeicherterTitel);

        // Die Aufgabenliste enthält beide zuvor angelegten Aufgaben (Status "Neu")
        var items = OffeneAufgabenItems(mainWindow);
        Assert.True(items.Length >= 2, "Aufgabenliste sollte beide angelegten Aufgaben weiterhin enthalten.");
        DeleteCurrentProject(mainWindow);
    }
}
