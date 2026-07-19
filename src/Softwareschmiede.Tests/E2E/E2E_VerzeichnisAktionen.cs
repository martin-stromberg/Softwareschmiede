using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FluentAssertions;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// E2E-Tests für die ins Ribbon-Menü der Aufgabendetailansicht überführten Verzeichnis-Aktionen
/// „Arbeitsverzeichnis öffnen" und „IDE öffnen".
///
/// Voraussetzungen:
/// - Windows-Desktop-Session (kein Headless-CI)
/// - Softwareschmiede.App muss im Debug-Modus gebaut sein
/// - Im Testmodus (SOFTWARESCHMIEDE_TEST_DB_PATH gesetzt) zeichnet IProzessStarter
///   (AufzeichnenderProzessStarter) jeden Prozessstart in einer Logdatei auf, statt einen echten
///   Prozess zu starten - siehe WpfTestBase.WaitForProzessStartEintragAsync.
///
/// CI-Regular-Lauf: dotnet test --filter "Category!=OsInterface"
/// </summary>
[Trait("Category", "E2E")]
[OsInterface]
[Collection("E2E")]
public sealed class E2E_VerzeichnisAktionen : WpfTestBase
{
    /// <summary>
    /// Szenario: Repository klonen (Aufgabe starten), dann nacheinander „Arbeitsverzeichnis öffnen"
    /// und „IDE öffnen" über das Ribbon prüfen - zunächst ohne, dann mit einer und mit mehreren
    /// „*.sln"-Dateien im Arbeitsverzeichnis.
    /// </summary>
    [SkippableFact]
    public async Task VerzeichnisAktionen_ArbeitsverzeichnisUndIdeOeffnen_E2E()
    {
        var mainWindow = SetupProjectMitNeuerAufgabe("VerzeichnisAktionen-Repo", "VerzeichnisAktionen-Projekt");

        // Das Ribbon kann bei sichtbarer CLI-Gruppe (Status Gestartet/Wartend) breiter als das Standard-
        // Fenster werden (Dateien- und Werkzeuge-Gruppe kommen neu hinzu). Ein Button einfacher WPF-Buttons
        // implementiert kein UIA-ScrollItemPattern, kann also nicht programmatisch in den sichtbaren Bereich
        // gescrollt werden - das Fenster wird deshalb maximiert, damit die Ribbon-Buttons tatsächlich im
        // klickbaren Bereich liegen.
        mainWindow.AsWindow().Patterns.Window.Pattern.SetWindowVisualState(WindowVisualState.Maximized);

        // git init im Quellverzeichnis vorab bestätigen, damit "Starten" im ersten Versuch gelingt.
        ConfirmLocalDirectoryGitInitInSourceDirectory();
        StartenUndPluginWaehlen(mainWindow, "Softwareschmiede.KiSimulator");

        // Nach erfolgreichem Start ist das Repository geklont (LokalerKlonPfad gesetzt).
        WaitForElement(mainWindow, cf => cf.ByName("CliStoppen"), Medium);

        var lokalerKlonPfad = await GetLokalerKlonPfadAsync();

        // Phase 1: "Arbeitsverzeichnis öffnen" zeichnet den OS-Dateiexplorer-Start mit dem LokalerKlonPfad auf.
        var arbeitsverzeichnisButton = WaitForElement(mainWindow, cf => cf.ByName("ArbeitsverzeichnisOeffnen"), Short);
        arbeitsverzeichnisButton.AsButton().Click();
        await WaitForProzessStartEintragAsync(lokalerKlonPfad);

        // Phase 2: Ohne "*.sln" ist "IDE öffnen" deaktiviert.
        var ideButtonOhneSln = WaitForElement(mainWindow, cf => cf.ByName("IdeOeffnen"), Short);
        ideButtonOhneSln.Properties.IsEnabled.Value.Should().BeFalse("im Arbeitsverzeichnis liegt noch keine .sln-Datei");

        // Genau eine "*.sln" anlegen und die Aufgabe neu laden (Ribbon-Button-CanExecute wird beim Laden gecacht).
        var ersteSolution = Path.Combine(lokalerKlonPfad, "Erste.sln");
        File.WriteAllText(ersteSolution, string.Empty);
        ReloadTaskDetail(mainWindow);

        // Phase 3: Bei genau einer "*.sln" öffnet "IDE öffnen" diese direkt, ohne Auswahl-Dialog.
        var ideButtonMitEinerSln = WaitForElement(mainWindow, cf => cf.ByName("IdeOeffnen"), Short);
        ideButtonMitEinerSln.Properties.IsEnabled.Value.Should().BeTrue("es existiert jetzt genau eine .sln-Datei");
        ideButtonMitEinerSln.AsButton().Click();
        await WaitForProzessStartEintragAsync(ersteSolution);

        var dialogNachEinerSln = mainWindow.FindFirstDescendant(cf => cf.ByName("Solution auswählen"));
        dialogNachEinerSln.Should().BeNull("bei genau einer Solution darf kein Auswahl-Dialog erscheinen");

        // Eine zweite "*.sln" anlegen und die Aufgabe erneut neu laden.
        var zweiteSolution = Path.Combine(lokalerKlonPfad, "Zweite.sln");
        File.WriteAllText(zweiteSolution, string.Empty);
        ReloadTaskDetail(mainWindow);

        // Phase 4: Bei mehreren "*.sln"-Dateien erscheint der Auswahl-Dialog; die gewählte Solution wird geöffnet.
        var ideButtonMitZweiSln = WaitForElement(mainWindow, cf => cf.ByName("IdeOeffnen"), Short);
        ideButtonMitZweiSln.AsButton().Click();

        var dialog = WaitForWindow("Solution auswählen", Short);
        var solutionListe = WaitForElement(dialog, cf => cf.ByName("SolutionAuswahl"), Short);
        var zweiterEintrag = WaitForElement(solutionListe, cf => cf.ByName(zweiteSolution), Short);
        zweiterEintrag.Click();

        var okButton = WaitForElement(dialog, cf => cf.ByName("OK"), Short);
        okButton.AsButton().Click();

        await WaitForProzessStartEintragAsync(zweiteSolution);
    }

    private async Task<string> GetLokalerKlonPfadAsync()
    {
        await using var db = OpenTestDbContext();
        var aufgabe = db.Aufgaben.Single();
        return aufgabe.LokalerKlonPfad
            ?? throw new InvalidOperationException("LokalerKlonPfad wurde nach dem Starten der Aufgabe nicht gesetzt.");
    }

    private void ReloadTaskDetail(AutomationElement mainWindow)
    {
        AufgabeDetailZurueck(mainWindow);
        var items = OffeneAufgabenItems(mainWindow);
        ErsteOffeneAufgabeOeffnen(items);
        WaitForElement(mainWindow, cf => cf.ByName("Zurück"), Short);
    }
}
