using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// E2E-Tests für die To-Do-Liste der Aufgabendetailansicht (Issue 103).
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
    /// Szenario (auf derselben Aufgabeninstanz durchlaufen, um die Laufzeit der FlaUI-Suite gering zu
    /// halten): Todo-Tab öffnen (leer), drei To-Dos erstellen (Badge zeigt "3"), eines löschen (Badge zeigt
    /// "2", Eintrag verschwindet aus der Liste), eines abhaken (visuelle Änderung + Badge zeigt "1"),
    /// Aufgabenabschluss mit dem verbleibenden offenen To-Do wird blockiert (Fehlermeldung mit Anzahl),
    /// danach auch das letzte To-Do abhaken — danach ist der Abschluss erlaubt (Badge verschwindet, Status
    /// wechselt auf "Beendet").
    /// </summary>
    /// <param name="mainWindow">Das Hauptfenster der Anwendung.</param>
    protected void Todo_ErstellenAbhakenLoeschenUndAbschlussValidierung_E2E(Window mainWindow)
    {
        NavigateToProjectsAndCreateProject(mainWindow, "Todo-Test");

        NeueAufgabeAnlegen(mainWindow);
        AufgabeTitelSetzen(mainWindow, "Todo-Testaufgabe");
        AufgabeDetailSpeichern(mainWindow, true);
        AufgabeAusListeOeffnen(mainWindow, "Todo-Testaufgabe");

        // Todo-Tab öffnen: Todo-Liste ist leer, Badge nicht sichtbar
        var todoTabButton = WaitForElement(mainWindow, cf => cf.ByName("TodoViewButton"), Short);
        todoTabButton.AsButton().Click();
        var eingabeFeld = WaitForElement(mainWindow, cf => cf.ByName("NeuesTodoBeschreibung"), Short);
        Assert.Null(mainWindow.FindFirstDescendant(cf => cf.ByName("OffeneTodoCountBadge")));

        // Drei To-Dos erstellen
        ErstelleTodoUeberUi(mainWindow, eingabeFeld, "Erstes Todo");
        ErstelleTodoUeberUi(mainWindow, eingabeFeld, "Zweites Todo");
        ErstelleTodoUeberUi(mainWindow, eingabeFeld, "Zu löschendes Todo");
        WaitForElement(mainWindow, cf => cf.ByName("Erstes Todo"), Short);
        WaitForElement(mainWindow, cf => cf.ByName("Zweites Todo"), Short);
        WaitForElement(mainWindow, cf => cf.ByName("Zu löschendes Todo"), Short);

        // Badge zeigt 3 offene To-Dos
        var badge = WaitForElement(mainWindow, cf => cf.ByName("OffeneTodoCountBadge"), Short);
        Assert.Equal("3", GetHelpTextOrName(badge));

        // Drittes Todo löschen → verschwindet aus der Liste, Badge zeigt 2
        LoescheTodoUeberUi(mainWindow, "Zu löschendes Todo");
        var badgeNachLoeschen = WaitForElement(mainWindow, cf => cf.ByName("OffeneTodoCountBadge"), Short);
        Assert.Equal("2", GetHelpTextOrName(badgeNachLoeschen));
        Assert.Null(mainWindow.FindFirstDescendant(cf => cf.ByName("Zu löschendes Todo")));

        // Erstes Todo abhaken
        AbhakenTodoUeberCheckbox(mainWindow, "Erstes Todo");

        // Badge zeigt nur noch 1 offenes Todo
        var badgeNachErstemAbhaken = WaitForElement(mainWindow, cf => cf.ByName("OffeneTodoCountBadge"), Short);
        Assert.Equal("1", GetHelpTextOrName(badgeNachErstemAbhaken));

        // Aufgabe direkt in der Test-Datenbank auf Status "Gestartet" setzen, um "Beenden" zu ermöglichen,
        // ohne einen echten CLI-/Klon-Vorgang durchführen zu müssen (nicht Gegenstand dieses Szenarios).
        SetzeAufgabeStatusGestartet("Todo-Testaufgabe");
        AufgabeAusListeUeberSeitenleisteErneutLaden(mainWindow);

        todoTabButton = WaitForElement(mainWindow, cf => cf.ByName("TodoViewButton"), Short);
        todoTabButton.AsButton().Click();

        // Abschluss mit noch einem offenen Todo wird blockiert
        var beendenButton = WaitForElement(mainWindow, cf => cf.ByName("Beenden"), Short);
        beendenButton.AsButton().Click();
        var fehlerBanner = WaitForElement(mainWindow, cf => cf.ByName("FehlerMeldung"), Short);
        Assert.Contains("1 offene To-Do(s)", GetHelpTextOrName(fehlerBanner));
        WaitForElement(mainWindow, cf => cf.ByName("Gestartet"), Short);

        // Zweites Todo abhaken → keine offenen To-Dos mehr, Badge verschwindet
        AbhakenTodoUeberCheckbox(mainWindow, "Zweites Todo");
        WaitUntilGone(mainWindow, cf => cf.ByName("OffeneTodoCountBadge"), Short);

        // Abschluss ist nun erlaubt
        beendenButton = WaitForElement(mainWindow, cf => cf.ByName("Beenden"), Short);
        beendenButton.AsButton().Click();
        WaitForElement(mainWindow, cf => cf.ByName("Beendet"), Short);

        NavigateBackFromTaskToProject(mainWindow);
        DeleteCurrentProject(mainWindow);
    }

    private void ErstelleTodoUeberUi(AutomationElement mainWindow, AutomationElement eingabeFeld, string beschreibung)
    {
        eingabeFeld.Click();
        Keyboard.TypeSimultaneously(FlaUI.Core.WindowsAPI.VirtualKeyShort.CONTROL, FlaUI.Core.WindowsAPI.VirtualKeyShort.KEY_A);
        Keyboard.Type(beschreibung);
        var hinzufuegenButton = WaitForElement(mainWindow, cf => cf.ByName("TodoHinzufuegen"), Short);
        hinzufuegenButton.AsButton().Click();
    }

    private void AbhakenTodoUeberCheckbox(AutomationElement mainWindow, string beschreibung)
    {
        var eintrag = WaitForElement(mainWindow, cf => cf.ByName(beschreibung), Short);
        var container = eintrag.Parent;
        var checkbox = container?.FindFirstDescendant(cf => cf.ByName("TodoErledigtCheckbox"))
            ?? throw new InvalidOperationException($"Checkbox für To-Do '{beschreibung}' nicht gefunden.");
        checkbox.AsCheckBox().Click();
    }

    private void LoescheTodoUeberUi(AutomationElement mainWindow, string beschreibung)
    {
        var eintrag = WaitForElement(mainWindow, cf => cf.ByName(beschreibung), Short);
        var container = eintrag.Parent;
        var loeschenButton = container?.FindFirstDescendant(cf => cf.ByName("TodoLoeschen"))
            ?? throw new InvalidOperationException($"Löschen-Button für To-Do '{beschreibung}' nicht gefunden.");
        loeschenButton.AsButton().Click();
    }

    private void SetzeAufgabeStatusGestartet(string aufgabeTitel)
    {
        using var db = OpenTestDbContext();
        var aufgabe = db.Aufgaben.Single(a => a.Titel == aufgabeTitel);
        aufgabe.Status = AufgabeStatus.Gestartet;
        db.SaveChanges();
    }

    private void AufgabeAusListeUeberSeitenleisteErneutLaden(Window mainWindow)
    {
        NavigateBackFromTaskToProject(mainWindow);
        AufgabeAusListeOeffnen(mainWindow, "Todo-Testaufgabe");
    }
}
