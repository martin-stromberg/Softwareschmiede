using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;

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
    /// Szenario: Neue Aufgabe erstellen, Titel ausfüllen, speichern.
    /// Prüft: Neue Aufgabe mit Status "Neu" wird persistiert, erscheint in der Liste,
    /// und die Navigation kehrt zur ProjectDetailView zurück.
    /// </summary>
    [Fact]
    public void NeueAufgabeErstellenUndSpeichern_ErscheintInListeUndNavigiertZurueck_E2E()
    {
        var mainWindow = StartAndNavigateToProjects("NeueAufgabe-Speichern-Test");

        var aufgabeNeuButton = WaitForElement(mainWindow, cf => cf.ByName("AufgabeNeu"), Short);
        aufgabeNeuButton.AsButton().Click();

        var titelBox = WaitForElement(mainWindow, cf => cf.ByName("EditTitel"), Short);
        titelBox.Click();
        Keyboard.TypeSimultaneously(FlaUI.Core.WindowsAPI.VirtualKeyShort.CONTROL, FlaUI.Core.WindowsAPI.VirtualKeyShort.KEY_A);
        Keyboard.Type("Persistierte Neue Aufgabe");

        var speichernButton = WaitForElement(mainWindow, cf => cf.ByName("Speichern"), Short);
        speichernButton.AsButton().Click();

        // Navigation kehrt zur ProjectDetailView zurück (ProjektName-Feld wieder sichtbar)
        var projektNameBox = WaitForElement(mainWindow, cf => cf.ByName("ProjektName"), Medium);
        Assert.NotNull(projektNameBox);

        // Neue Aufgabe erscheint mit aktualisiertem Titel in der Aufgabenliste
        var aufgabenTitel = WaitForElement(mainWindow, cf => cf.ByName("Persistierte Neue Aufgabe"), Short);
        Assert.NotNull(aufgabenTitel);
    }

    /// <summary>
    /// Szenario: Neue Aufgabe erstellen, dann über "Zurück" abbrechen ohne zu speichern.
    /// Prüft: Navigation kehrt zur ProjectDetailView zurück, die Aufgabe wurde dennoch beim
    /// Anlegen mit Status "Neu" persistiert (AufgabeErstellenCommand persistiert sofort),
    /// jedoch ohne die nachträgliche Titel-Änderung.
    /// </summary>
    [Fact]
    public void NeueAufgabeAbbrechen_NavigiertZurueckOhneTitelAenderungZuSpeichern_E2E()
    {
        var mainWindow = StartAndNavigateToProjects("NeueAufgabe-Abbrechen-Test");

        var aufgabeNeuButton = WaitForElement(mainWindow, cf => cf.ByName("AufgabeNeu"), Short);
        aufgabeNeuButton.AsButton().Click();

        var titelBox = WaitForElement(mainWindow, cf => cf.ByName("EditTitel"), Short);
        titelBox.Click();
        Keyboard.TypeSimultaneously(FlaUI.Core.WindowsAPI.VirtualKeyShort.CONTROL, FlaUI.Core.WindowsAPI.VirtualKeyShort.KEY_A);
        Keyboard.Type("Nicht gespeicherter Titel");

        // Abbrechen über "Zurück" statt zu speichern
        var zurueckButton = WaitForElement(mainWindow, cf => cf.ByName("Zurück"), Short);
        zurueckButton.AsButton().Click();

        // Navigation kehrt zur ProjectDetailView zurück
        var projektNameBox = WaitForElement(mainWindow, cf => cf.ByName("ProjektName"), Medium);
        Assert.NotNull(projektNameBox);

        // Der nicht gespeicherte Titel erscheint nicht in der Aufgabenliste
        var nichtGespeicherterTitel = mainWindow.FindFirstDescendant(cf => cf.ByName("Nicht gespeicherter Titel"));
        Assert.Null(nichtGespeicherterTitel);

        // Die ursprünglich angelegte Aufgabe (Status "Neu") ist weiterhin in der Liste vorhanden
        var listBox = WaitForElement(mainWindow, cf => cf.ByName("OffeneAufgabenListe"), Short);
        var items = listBox.FindAllChildren(cf => cf.ByControlType(ControlType.ListItem));
        Assert.True(items.Length >= 1, "Aufgabenliste sollte die angelegte Aufgabe weiterhin enthalten.");
    }
}
