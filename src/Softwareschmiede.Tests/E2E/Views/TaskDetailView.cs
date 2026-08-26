using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using Softwareschmiede.Tests.E2E.Views.Dialogs;

namespace Softwareschmiede.Tests.E2E.Views;

/// <summary>View für die Aufgabendetail-Ansicht.</summary>
public sealed class TaskDetailView : BaseWindowView
{
    /// <param name="window">Das Hauptfenster der Anwendung.</param>
    public TaskDetailView(Window window) : base(window)
    {
    }

    /// <inheritdoc/>
    public override bool IsVisible
        => ElementExists(Window, cf => cf.ByName("EditTitel"))
           && (ElementExists(Window, cf => cf.ByName("Speichern")) || ElementExists(Window, cf => cf.ByName("Zurück")));

    /// <returns>Diese Instanz, wenn die Ansicht bereits sichtbar ist.</returns>
    /// <exception cref="InvalidOperationException">Wird geworfen, wenn die Ansicht noch nicht sichtbar ist.</exception>
    public override TaskDetailView ForceShow()
    {
        if (IsVisible)
            return this;

        throw new InvalidOperationException(
            "TaskDetailView.ForceShow() kann ohne bekannten Aufgabentitel keine Ansicht öffnen. " +
            "Nutze ProjectDetailView.CreateTask() oder eine Navigation über die Aufgabenliste.");
    }

    /// <inheritdoc/>
    public override TaskDetailView ForceClose(bool recurseToDashboard)
    {
        GoBack();

        if (recurseToDashboard)
            RecurseToDashboard();

        return this;
    }

    /// <returns>Der aktuelle Aufgabentitel.</returns>
    public string GetTaskTitle() => WaitForElement(Window, cf => cf.ByName("EditTitel"), Short).AsTextBox().Text;

    /// <param name="title">Der neue Aufgabentitel.</param>
    /// <returns>Diese Instanz.</returns>
    public TaskDetailView SetTaskTitle(string title)
    {
        var box = WaitForElement(Window, cf => cf.ByName("EditTitel"), Short);
        box.Click();
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
        Keyboard.Type(title);

        return this;
    }

    /// <summary>Klickt den "Speichern"-Button.</summary>
    /// <returns>Diese Instanz.</returns>
    public TaskDetailView SaveTask()
    {
        WaitForElement(Window, cf => cf.ByName("Speichern"), Short).AsButton().Click();
        return this;
    }

    /// <summary>
    /// Wartet, bis der "Starten"-Button sichtbar ist. Dient als Signal dafür, dass die Aufgabe
    /// persistiert wurde (der Button ist erst nach dem ersten Speichern verfügbar) - die TaskDetailView
    /// bleibt nach dem Speichern geöffnet, statt zur Projektdetailansicht zurückzufallen.
    /// </summary>
    /// <returns>Diese Instanz.</returns>
    public TaskDetailView WaitForPersisted()
    {
        WaitForElement(Window, cf => cf.ByName("Starten"), Medium);
        return this;
    }

    /// <summary>Löscht die aktuelle Aufgabe über den "Löschen"-Button und bestätigt den nativen Löschdialog.</summary>
    /// <returns>Die Projektdetailansicht, die nach dem Löschen sichtbar wird.</returns>
    public ProjectDetailView DeleteTask()
    {
        WaitForElement(Window, cf => cf.ByName("Starten"), Short);

        WaitForElement(Window, cf => cf.ByName("Löschen"), Short).AsButton().Click();
        new DeleteConfirmationDialogView(Window).Confirm();

        WaitUntilGone(Window, cf => cf.ByName("Starten"), Short);

        return new ProjectDetailView(Window);
    }

    /// <summary>
    /// Klickt den "Zurück"-Button und wartet, bis die Aufgabendetailansicht verlassen wurde. Wartet
    /// bewusst nicht fest auf "ProjektName" (Projektdetailansicht): Wurde die Aufgabe zuvor über die
    /// Aufgabenliste in der Seitenleiste geöffnet (<see cref="MenuView.NavigateToTask"/>), führt "Zurück"
    /// stattdessen zum Dashboard (siehe MainWindowViewModel.NavigateZuAufgabe). Aufrufer ermitteln die
    /// tatsächliche Zielansicht danach selbst über <see cref="WindowExtensions.CurrentView"/>.
    /// </summary>
    /// <returns>Diese Instanz.</returns>
    public TaskDetailView GoBack()
    {
        WaitForElement(Window, cf => cf.ByName("Zurück"), Short).AsButton().Click();

        var deadline = DateTime.UtcNow + Medium;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                if (Window.CurrentView() is not TaskDetailView)
                    return this;
            }
            catch (InvalidOperationException)
            {
                // Übergangszustand zwischen den Ansichten - weiter warten.
            }

            Thread.Sleep(200);
        }

        throw new TimeoutException(
            $"Nach Klick auf 'Zurück' wurde innerhalb von {Medium.TotalSeconds}s keine neue Ansicht sichtbar.");
    }

    /// <summary>
    /// Startet die Ausführung der Aufgabe über den "Starten"-Button und bedient den anschließend
    /// erscheinenden Plugin-Auswahl-Dialog (<see cref="PluginSelectionDialogView"/>): wählt das
    /// angegebene KI-Plugin aus, setzt optional "FuerProjektVerwenden" und bestätigt mit "OK". Wartet
    /// nicht auf das Ergebnis (Erfolg oder Fehlermeldung) - Aufrufer prüfen danach den erwarteten
    /// Zustand selbst, z. B. über <see cref="WaitForCliRunning"/> oder
    /// <c>Assert.IsType&lt;ErrorView&gt;(mainWindow.CurrentView())</c>.
    /// </summary>
    /// <param name="pluginName">Der Name des im Dialog auszuwählenden KI-Plugins.</param>
    /// <param name="fuerProjektVerwenden">Wenn <c>true</c>, wird die "FuerProjektVerwenden"-Checkbox gesetzt, damit das gewählte Plugin als Projekt-Standard gespeichert wird.</param>
    /// <returns>Diese Instanz.</returns>
    public TaskDetailView Start(string pluginName, bool fuerProjektVerwenden)
    {
        new PluginSelectionDialogView(Window)
            .ForceShow()
            .SelectPlugin(pluginName, fuerProjektVerwenden)
            .Confirm();

        return this;
    }

    /// <summary>Wartet, bis die CLI gestartet ist (Stoppen-Button sichtbar und Status "Gestartet" angezeigt).</summary>
    /// <returns>Diese Instanz.</returns>
    public TaskDetailView WaitForCliRunning()
    {
        WaitForElement(Window, cf => cf.ByName("CliStoppen"), Medium);
        WaitForElement(Window, cf => cf.ByName("Gestartet"), Short);

        return this;
    }

    /// <returns><c>true</c>, wenn der "CliStoppen"-Button aktuell sichtbar ist (CLI läuft).</returns>
    public bool IsCliRunning() => ElementExists(Window, cf => cf.ByName("CliStoppen"));

    /// <summary>Klickt den "CliStoppen"-Button, um die laufende CLI manuell zu beenden, und wartet, bis er verschwindet.</summary>
    /// <returns>Diese Instanz.</returns>
    public TaskDetailView StopCli()
    {
        WaitForElement(Window, cf => cf.ByName("CliStoppen"), Short).AsButton().Click();
        WaitUntilGone(Window, cf => cf.ByName("CliStoppen"), Medium);

        return this;
    }

    /// <returns><c>true</c>, wenn das CLI-Panel ("CliViewButton") sichtbar ist.</returns>
    public bool HasCliPanel() => ElementExists(Window, cf => cf.ByName("CliViewButton"));

    /// <returns><c>true</c>, wenn die letzte CLI-Ausgabe ("TerminalConsole") sichtbar ist.</returns>
    public bool HasTerminalOutput() => ElementExists(Window, cf => cf.ByName("TerminalConsole"));

    /// <returns><c>true</c>, wenn der "CliNeustarten"-Button (manueller CLI-Neustart nach Beendigung) sichtbar ist.</returns>
    public bool CanRestartCli() => ElementExists(Window, cf => cf.ByName("CliNeustarten"));

    /// <returns><c>true</c>, wenn die Statusleiste den Aufgabenstatus "Gestartet" anzeigt.</returns>
    public bool IsTaskStarted() => ElementExists(Window, cf => cf.ByName("Gestartet"));

    /// <summary>Klickt den "Beenden"-Button.</summary>
    /// <returns>Diese Instanz.</returns>
    public TaskDetailView Finish()
    {
        WaitForElement(Window, cf => cf.ByName("Beenden"), Short).AsButton().Click();
        return this;
    }

    /// <summary>Wartet, bis die Statusleiste den Aufgabenstatus "Beendet" anzeigt.</summary>
    /// <returns>Diese Instanz.</returns>
    public TaskDetailView WaitForFinished()
    {
        WaitForElement(Window, cf => cf.ByName("Beendet"), Short);
        return this;
    }

    /// <summary>Klickt einen View-Umschalt-Button innerhalb der Aufgabendetailansicht (z. B. "InfoCliToggle", "CliViewButton").</summary>
    /// <param name="viewButtonName">Der Automation-Name des Umschalt-Buttons.</param>
    /// <returns>Diese Instanz.</returns>
    public TaskDetailView SwitchPanel(string viewButtonName)
    {
        WaitForElement(Window, cf => cf.ByName(viewButtonName), Short).AsButton().Click();
        return this;
    }

    /// <summary>Wartet, bis ein Protokolleintrag des angegebenen Typs ("ProtokollTyp-{Typ}") sichtbar ist.</summary>
    /// <param name="typ">Der Protokolltyp (z. B. "GitAktion").</param>
    /// <returns>Diese Instanz.</returns>
    public TaskDetailView WaitForLogEntry(string typ)
    {
        WaitForElement(Window, cf => cf.ByName($"ProtokollTyp-{typ}"), Medium);
        return this;
    }

    /// <summary>Wartet, bis der "CliStoppen"-Button verschwunden ist (CLI nicht mehr aktiv).</summary>
    /// <returns>Diese Instanz.</returns>
    public TaskDetailView WaitForCliStopped()
    {
        WaitUntilGone(Window, cf => cf.ByName("CliStoppen"), Short);
        return this;
    }

    /// <summary>Wartet, bis ein Element mit dem angegebenen Namen sichtbar ist (z. B. der Aufgabentitel im Info-Panel).</summary>
    /// <param name="text">Der erwartete Automation-Name.</param>
    /// <returns>Diese Instanz.</returns>
    public TaskDetailView WaitForText(string text)
    {
        WaitForElement(Window, cf => cf.ByName(text), Short);
        return this;
    }

    /// <summary>Wartet, bis der "Starten"-Button verfügbar ist.</summary>
    /// <returns>Diese Instanz.</returns>
    public TaskDetailView WaitForStartAvailable()
    {
        WaitForElement(Window, cf => cf.ByName("Starten"), Short);
        return this;
    }

    /// <summary>
    /// Klickt den "Starten"-Button erneut, ohne den Plugin-Auswahl-Dialog zu bedienen - für den manuellen
    /// Neustart einer bereits zuvor mit einem Plugin gestarteten Aufgabe (der Dialog erscheint nur beim
    /// ersten Start, danach wird das zuletzt gewählte Plugin wiederverwendet).
    /// </summary>
    /// <returns>Diese Instanz.</returns>
    public TaskDetailView Restart()
    {
        WaitForElement(Window, cf => cf.ByName("Starten"), Short).AsButton().Click();
        return this;
    }

    /// <summary>Trägt Stunde und Minute für den zeitgesteuerten Prompt-Versand ein.</summary>
    /// <param name="hour">Die Zielstunde (0-23).</param>
    /// <param name="minute">Die Zielminute (0-59).</param>
    /// <returns>Diese Instanz.</returns>
    public TaskDetailView SetScheduledPromptTime(int hour, int minute)
    {
        var stundeBox = WaitForElement(Window, cf => cf.ByName("ScheduledPromptStunde"), Short);
        stundeBox.Click();
        Keyboard.Type(hour.ToString("00"));

        var minuteBox = WaitForElement(Window, cf => cf.ByName("ScheduledPromptMinute"), Short);
        minuteBox.Click();
        Keyboard.Type(minute.ToString("00"));

        return this;
    }

    /// <summary>Wählt eine Promptvorlage in der "PromptVorlagenAuswahl"-ComboBox aus.</summary>
    /// <param name="name">Der Name der Promptvorlage.</param>
    /// <returns>Diese Instanz.</returns>
    public TaskDetailView SelectPromptTemplate(string name)
    {
        var box = WaitForElement(Window, cf => cf.ByName("PromptVorlagenAuswahl"), Short);
        SelectComboBoxItemByClick(box, name, Short);
        return this;
    }

    /// <summary>Klickt "Zeitgesteuert senden" und wartet auf die Statusanzeige "Prompt in Wartestellung".</summary>
    /// <returns>Diese Instanz.</returns>
    public TaskDetailView SendScheduledPrompt()
    {
        WaitForElement(Window, cf => cf.ByName("ZeitgesteuertSenden"), Short).AsButton().Click();
        WaitForElement(Window, cf => cf.ByName("ScheduledPromptStatus"), Medium);
        return this;
    }

    /// <summary>Klickt den Haupt-Button "IdeOeffnen" des IDE-Öffnen-Split-Buttons.</summary>
    /// <returns>Diese Instanz.</returns>
    public TaskDetailView OpenIde()
    {
        WaitForElement(Window, cf => cf.ByName("IdeOeffnen"), Short).AsButton().Click();
        return this;
    }

    /// <returns><c>true</c>, wenn der Dropdown-Button "IdeOeffnenDropdown" sichtbar ist (mindestens zwei Einstiegspunkte gefunden).</returns>
    public bool HasIdeDropdown() => ElementExists(Window, cf => cf.ByName("IdeOeffnenDropdown"));

    /// <returns><c>true</c>, wenn der Haupt-Button "IdeOeffnen" aktuell aktiviert ist.</returns>
    public bool IsIdeButtonEnabled() => WaitForElement(Window, cf => cf.ByName("IdeOeffnen"), Short).Properties.IsEnabled.Value;

    /// <summary>Klickt den "ArbeitsverzeichnisOeffnen"-Button im Ribbon.</summary>
    /// <returns>Diese Instanz.</returns>
    public TaskDetailView OpenWorkingDirectory()
    {
        WaitForElement(Window, cf => cf.ByName("ArbeitsverzeichnisOeffnen"), Short).AsButton().Click();
        return this;
    }

    /// <returns><c>true</c>, wenn der "PluginAendern"-Button (Plugin-Wechsel-Selector, nur bei mehreren aktiven KI-Plugins sichtbar) angezeigt wird.</returns>
    public bool HasPluginChangeButton() => ElementExists(Window, cf => cf.ByName("PluginAendern"));

    /// <summary>Wartet, bis der "PluginAendern"-Button (CLI-Ribbon-Gruppe) sichtbar ist.</summary>
    /// <returns>Diese Instanz.</returns>
    public TaskDetailView WaitForPluginChangeButton()
    {
        WaitForElement(Window, cf => cf.ByName("PluginAendern"), Short);
        return this;
    }

    /// <summary>Wartet, bis der "PluginAendern"-Button (CLI-Ribbon-Gruppe) verschwindet.</summary>
    /// <returns>Diese Instanz.</returns>
    public TaskDetailView WaitUntilPluginChangeButtonGone()
    {
        WaitUntilGone(Window, cf => cf.ByName("PluginAendern"), Short);
        return this;
    }

    /// <summary>
    /// Klickt den "PluginAendern"-Button, um bei laufender CLI ein anderes KI-Plugin zu wählen. Ruft
    /// bewusst nicht <see cref="Dialogs.PluginSelectionDialogView.ForceShow"/> auf, da dessen
    /// Navigationslogik über den "Starten"-Button ausgelöst wird, der hier (CLI läuft bereits) nicht
    /// sichtbar ist - die zurückgegebene Instanz wartet stattdessen passiv (wie
    /// <see cref="DialogView.ForceShow"/>), sobald eine ihrer Methoden aufgerufen wird.
    /// </summary>
    /// <returns>Der KI-Plugin-Auswahl-Dialog, mit dem aktuellen Plugin vorselektiert.</returns>
    public Dialogs.PluginSelectionDialogView OpenPluginChangeDialog()
    {
        WaitForElement(Window, cf => cf.ByName("PluginAendern"), Short).AsButton().Click();
        return new Dialogs.PluginSelectionDialogView(Window);
    }

    /// <summary>Wartet, bis die letzte CLI-Ausgabe ("TerminalConsole") sichtbar ist.</summary>
    /// <returns>Diese Instanz.</returns>
    public TaskDetailView WaitForTerminalOutput()
    {
        WaitForElement(Window, cf => cf.ByName("TerminalConsole"), Short);
        return this;
    }

    /// <summary>
    /// Wartet, bis das TerminalConsole-Element eine nicht-leere Prozess-ID (AutomationProperties.HelpText,
    /// siehe TaskDetailView.xaml.cs) anzeigt, und gibt diese zurück.
    /// </summary>
    /// <param name="timeout">Maximale Wartezeit.</param>
    /// <returns>Die als HelpText hinterlegte Prozess-ID des aktuell eingebetteten CLI-Prozesses.</returns>
    /// <exception cref="TimeoutException">Wird geworfen, wenn innerhalb des Timeouts keine Prozess-ID angezeigt wird.</exception>
    public string WaitForTerminalProcessId(TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var terminal = Window.FindFirstDescendant(cf => cf.ByName("TerminalConsole"));
            var pid = terminal?.HelpText;
            if (!string.IsNullOrWhiteSpace(pid))
                return pid;

            Thread.Sleep(200);
        }

        throw new TimeoutException(
            "TerminalConsole zeigte innerhalb des Timeouts keine Prozess-ID (HelpText) an. "
            + $"Vorhandene Descendants von Window: {DescribeDescendants(Window)}");
    }

    /// <summary>
    /// Listet zu Diagnosezwecken ControlType und Name aller Nachfahren eines Elements auf (z. B. für die
    /// Fehlermeldung einer TimeoutException, um zu sehen, welche Elemente statt des erwarteten tatsächlich
    /// im Automation-Baum vorhanden sind). Einzelne Elemente, deren Eigenschaften nicht mehr abrufbar sind
    /// (z. B. bereits entfernt), werden übersprungen statt die Diagnose selbst scheitern zu lassen.
    /// </summary>
    /// <param name="parent">Das Element, dessen Nachfahren aufgelistet werden.</param>
    /// <returns>Kommagetrennte Liste aus "ControlType:'Name'" je Nachfahre.</returns>
    private static string DescribeDescendants(AutomationElement parent)
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

    /// <summary>Klickt den Dropdown-Button "IdeOeffnenDropdown" und öffnet den Solution-Auswahl-Dialog.</summary>
    /// <returns>Der geöffnete Solution-Auswahl-Dialog.</returns>
    public SolutionSelectionDialogView OpenIdeDropdown()
    {
        WaitForElement(Window, cf => cf.ByName("IdeOeffnenDropdown"), Short).AsButton().Click();

        var dialog = new SolutionSelectionDialogView(Window);
        dialog.ForceShow();
        return dialog;
    }

    /// <summary>
    /// Verlässt die Aufgabendetailansicht und öffnet dieselbe (einzige) Aufgabe des Projekts erneut, um
    /// einen frischen ViewModel-Initialisierungsaufruf zu erzwingen, der extern (im Testprozess)
    /// vorgenommene Änderungen (z. B. neu angelegte Dateien im Arbeitsverzeichnis) erfasst.
    /// </summary>
    /// <returns>Die neu geöffnete Aufgabendetailansicht.</returns>
    public TaskDetailView Reload()
    {
        GoBack();
        var projectDetail = new ProjectDetailView(Window);
        return projectDetail.OpenFirstTask();
    }
}
