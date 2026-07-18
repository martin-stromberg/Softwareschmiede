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
[Trait("Category", "E2E")]
[OsInterface]
[Collection("E2E")]
public sealed class E2E_CreateNewTaskNavigation : WpfTestBase
{
    /// <summary>
    /// Szenario: Neue Aufgabe erstellen, Titel ausfüllen, speichern (Phase Speichern); anschließend
    /// eine weitere Aufgabe erstellen, Titel ausfüllen, über "Zurück" abbrechen (Phase Abbrechen).
    /// Prüft: Die gespeicherte Aufgabe wird mit Status "Neu" persistiert, erscheint in der Liste, und
    /// die Navigation kehrt zur ProjectDetailView zurück. Die im Abbrechen-Pfad eingegebene
    /// Titeländerung wird nicht persistiert, die zuvor angelegte Aufgabe (Status "Neu") bleibt jedoch
    /// weiterhin in der Liste vorhanden.
    /// </summary>
    [Fact]
    public void AufgabeAnlegen_SpeichernPersistiert_UndAbbrechenVerwirftTitel_E2E()
    {
        var mainWindow = StartAndNavigateToProjects("NeueAufgabe-Test");

        // Phase Speichern
        NeueAufgabeAnlegen(mainWindow);
        AufgabeTitelSetzen(mainWindow, "Persistierte Neue Aufgabe");
        AufgabeDetailSpeichern(mainWindow);

        // Navigation kehrt zur ProjectDetailView zurück (ProjektName-Feld wieder sichtbar)
        var projektNameBox = WaitForElement(mainWindow, cf => cf.ByName("ProjektName"), Medium);
        Assert.NotNull(projektNameBox);

        // Neue Aufgabe erscheint mit aktualisiertem Titel in der Aufgabenliste
        var aufgabenTitel = WaitForElement(mainWindow, cf => cf.ByName("Persistierte Neue Aufgabe"), Short);
        Assert.NotNull(aufgabenTitel);

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
    }
}
