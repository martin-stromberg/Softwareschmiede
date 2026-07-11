using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// E2E-Regressionstest für den Kundenbug: "Ist eine Aufgabe geöffnet und wählt der Anwender eine
/// andere Aufgabe aus der Aufgabenliste im Menü aus, so bleibt die geöffnete Aufgabe geöffnet und die
/// neue Aufgabe wird nicht angezeigt. Lediglich in der Fußzeile ändert die Ansicht auf den Namen der
/// zweiten Aufgabe. Die geöffnete CLI ist aber weiterhin diejenige der zuvor geöffneten Aufgabe."
///
/// Ursache: TaskDetailView wird über eine DataType-DataTemplate-Zuordnung in MainWindow.xaml
/// eingebunden. Wechselt MainWindowViewModel.CurrentView zwischen zwei TaskDetailViewModel-Instanzen
/// (gleicher Typ), erzeugt WPF keine neue TaskDetailView-Instanz und feuert daher weder Loaded noch
/// Unloaded — nur die XAML-Bindings (Titel, Fußzeile) aktualisieren sich reaktiv. Die im Code-Behind
/// nur in den Loaded/Unloaded-Handlern gesetzte TerminalControl.Session blieb dadurch auf der
/// vorherigen Aufgabe stehen.
///
/// Testbarkeit: Die tatsächlich im TerminalControl eingebettete Prozess-ID wird über
/// AutomationProperties.HelpText offengelegt (siehe TaskDetailView.xaml.cs), damit der Test
/// unabhängig von der (custom-gezeichneten) Terminal-Darstellung verifizieren kann, welcher
/// CLI-Prozess tatsächlich angezeigt wird.
///
/// Voraussetzungen:
/// - Windows-Desktop-Session (kein Headless-CI)
/// - Softwareschmiede.App muss im Debug-Modus gebaut sein
/// - Im Test-Modus steht ausschließlich das LocalDirectoryPlugin als SCM-Plugin zur Verfügung.
///
/// CI-Ausschluss: dotnet test --filter "Category!=E2E"
/// </summary>
[Trait("Category", "E2E")]
[Collection("E2E")]
public sealed class E2E_TaskWechselUeberMenue : WpfTestBase
{
    private const string TitelA = "Aufgabe-A-Wechseltest";
    private const string TitelB = "Aufgabe-B-Wechseltest";

    /// <summary>
    /// Szenario: Aufgabe A ist geöffnet und ihre CLI läuft. Ohne über "Zurück" zu navigieren, wählt
    /// der Anwender über die Aufgabenliste in der Seitenleiste ("Aktive Aufgaben") Aufgabe B aus.
    /// Prüft: Danach wird tatsächlich Aufgabe B angezeigt — inklusive der zu Aufgabe B gehörenden CLI
    /// (eigene Prozess-ID), nicht mehr die CLI von Aufgabe A.
    /// </summary>
    [SkippableFact]
    public void AufgabeWechselUeberSeitenleiste_ZeigtNeueAufgabeMitEigenerCli_E2E()
    {
        SkipWennConPtyNichtVerfuegbar();
        ConfirmLocalDirectoryGitInitInSourceDirectory();

        var sourceDirectory = CreateLocalSourceDirectory("Wechsel-Repo");
        var app = LaunchApp();
        var mainWindow = app.GetMainWindow(Automation, Long)!;
        ConfigureLocalDirectoryPlugin(mainWindow, sourceDirectory, useInSourceDirectoryMode: false);

        NavigateToProjecten(mainWindow);
        CreateAndOpenProject(mainWindow, "Wechsel-Projekt");
        AssignLocalDirectoryRepository(mainWindow);

        // Aufgabe A anlegen, öffnen und CLI starten
        ErstelleUndStarteAufgabe(mainWindow, TitelA);
        var pidA = WaitForTerminalProzessId(mainWindow, Medium);

        // Zurück zum Projekt, um Aufgabe B anzulegen
        var zurueckNachA = WaitForElement(mainWindow, cf => cf.ByName("Zurück"), Short);
        zurueckNachA.AsButton().Click();
        WaitForElement(mainWindow, cf => cf.ByName("ProjektName"), Medium);

        // Aufgabe B anlegen, öffnen und CLI starten (eigener Prozess, andere PID als Aufgabe A)
        ErstelleUndStarteAufgabe(mainWindow, TitelB);
        var pidB = WaitForTerminalProzessId(mainWindow, Medium);
        Assert.NotEqual(pidA, pidB);

        // Zurück zum Projekt und Aufgabe A erneut öffnen — Aufgabe A ist nun die "geöffnete" Aufgabe
        var zurueckNachB = WaitForElement(mainWindow, cf => cf.ByName("Zurück"), Short);
        zurueckNachB.AsButton().Click();
        WaitForElement(mainWindow, cf => cf.ByName("ProjektName"), Medium);

        OeffneAufgabeAusListe(mainWindow, TitelA);
        WaitForElement(mainWindow, cf => cf.ByName("CliStoppen"), Medium);
        var pidAErneutGeoeffnet = WaitForTerminalProzessId(mainWindow, Medium);
        Assert.Equal(pidA, pidAErneutGeoeffnet);

        // Über die Aufgabenliste in der Seitenleiste ("Aktive Aufgaben") zu Aufgabe B wechseln,
        // OHNE über "Zurück" zu navigieren — genau das im Bug-Report beschriebene Szenario.
        var navigateZuB = WaitForElement(mainWindow, cf => cf.ByName($"AufgabeNavigieren:{TitelB}"), Medium);
        navigateZuB.AsButton().Click();

        // Die eingebettete CLI muss jetzt tatsächlich zu Aufgabe B gehören (nicht mehr zu Aufgabe A).
        var pidNachWechsel = WaitForTerminalProzessId(mainWindow, Medium);
        Assert.NotEqual(pidA, pidNachWechsel);
        Assert.Equal(pidB, pidNachWechsel);

        // Zusätzlich (nicht nur Titel/Fußzeile): Das Info-Panel zeigt den Titel von Aufgabe B.
        var infoToggle = WaitForElement(mainWindow, cf => cf.ByName("InfoCliToggle"), Medium);
        infoToggle.Click();
        var titelBImInfoPanel = WaitForElement(mainWindow, cf => cf.ByName(TitelB), Short);
        Assert.NotNull(titelBImInfoPanel);
    }

    /// <summary>Legt eine neue Aufgabe im aktuell geöffneten Projekt an, benennt sie um, öffnet sie erneut und startet die CLI mit dem KI-Simulator-Plugin.</summary>
    /// <param name="mainWindow">Das Hauptfenster der Anwendung.</param>
    /// <param name="titel">Der Titel, auf den die neue Aufgabe umbenannt werden soll.</param>
    private void ErstelleUndStarteAufgabe(AutomationElement mainWindow, string titel)
    {
        var aufgabeNeuButton = WaitForElement(mainWindow, cf => cf.ByName("AufgabeNeu"), Short);
        aufgabeNeuButton.AsButton().Click();

        var editTitelBox = WaitForElement(mainWindow, cf => cf.ByName("EditTitel"), Short);
        editTitelBox.Click();
        Keyboard.TypeSimultaneously(FlaUI.Core.WindowsAPI.VirtualKeyShort.CONTROL, FlaUI.Core.WindowsAPI.VirtualKeyShort.KEY_A);
        Keyboard.Type(titel);

        var speichernButton = WaitForElement(mainWindow, cf => cf.ByName("Speichern"), Short);
        speichernButton.AsButton().Click();

        WaitForElement(mainWindow, cf => cf.ByName("ProjektName"), Medium);

        OeffneAufgabeAusListe(mainWindow, titel);

        StartenUndPluginWaehlen(mainWindow, "Softwareschmiede.KiSimulator");
        WaitForElement(mainWindow, cf => cf.ByName("CliStoppen"), Medium);
    }

    /// <summary>Öffnet eine Aufgabe mit dem angegebenen Titel per Doppelklick aus der Aufgabenliste der Projektdetailansicht.</summary>
    /// <param name="mainWindow">Das Hauptfenster der Anwendung.</param>
    /// <param name="titel">Der Titel der zu öffnenden Aufgabe.</param>
    private static void OeffneAufgabeAusListe(AutomationElement mainWindow, string titel)
    {
        var listenEintrag = WaitForElement(
            mainWindow,
            cf => cf.ByName(titel).And(cf.ByControlType(ControlType.ListItem)),
            Medium);
        listenEintrag.DoubleClick();
        WaitForElement(mainWindow, cf => cf.ByName("Zurück"), Short);
    }

    /// <summary>
    /// Wartet, bis das TerminalConsole-Element eine nicht-leere Prozess-ID (AutomationProperties.HelpText,
    /// siehe TaskDetailView.xaml.cs) anzeigt, und gibt diese zurück.
    /// </summary>
    /// <param name="mainWindow">Das Hauptfenster der Anwendung.</param>
    /// <param name="timeout">Maximale Wartezeit.</param>
    /// <returns>Die als HelpText hinterlegte Prozess-ID des aktuell eingebetteten CLI-Prozesses.</returns>
    private static string WaitForTerminalProzessId(AutomationElement mainWindow, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var terminal = mainWindow.FindFirstDescendant(cf => cf.ByName("TerminalConsole"));
            var pid = terminal?.HelpText;
            if (!string.IsNullOrWhiteSpace(pid))
                return pid;
            Thread.Sleep(200);
        }
        throw new TimeoutException(
            "TerminalConsole zeigte innerhalb des Timeouts keine Prozess-ID (HelpText) an. "
            + $"Vorhandene Descendants von mainWindow: {BeschreibeDescendants(mainWindow)}");
    }

    /// <summary>Listet zu Diagnosezwecken ControlType und Name aller Nachfahren eines Elements auf
    /// (z. B. für die Fehlermeldung eines TimeoutException, um zu sehen, welche Elemente statt des
    /// erwarteten tatsächlich im Automation-Baum vorhanden sind). Einzelne Elemente, deren Eigenschaften
    /// nicht mehr abrufbar sind (z. B. bereits entfernt), werden übersprungen statt die Diagnose selbst
    /// scheitern zu lassen.</summary>
    /// <param name="parent">Das Element, dessen Nachfahren aufgelistet werden.</param>
    /// <returns>Kommagetrennte Liste aus "ControlType:'Name'" je Nachfahre.</returns>
    private static string BeschreibeDescendants(AutomationElement parent)
    {
        try
        {
            var descendants = parent.FindAllDescendants();
            var beschreibungen = new List<string>(descendants.Length);
            foreach (var element in descendants)
            {
                try
                {
                    beschreibungen.Add($"{element.ControlType}:'{element.Name}'");
                }
                catch
                {
                    // Element nicht mehr abrufbar (z. B. bereits aus dem Baum entfernt) — überspringen.
                }
            }
            return beschreibungen.Count == 0
                ? "(keine)"
                : $"[{beschreibungen.Count}] {string.Join(", ", beschreibungen)}";
        }
        catch (Exception ex)
        {
            return $"(Descendants-Abfrage fehlgeschlagen: {ex.Message})";
        }
    }
}
