using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace Softwareschmiede.Tests.E2E.Views;

/// <summary>View für das persistente Navigationsmenü der Anwendung (Dashboard/Projekte/Einstellungen).</summary>
public sealed class MenuView : BaseWindowView
{
    /// <param name="window">Das Hauptfenster der Anwendung.</param>
    public MenuView(Window window) : base(window)
    {
    }

    /// <inheritdoc/>
    public override bool IsVisible
        => ElementExists(Window, cf => cf.ByName("Dashboard"))
           && ElementExists(Window, cf => cf.ByName(" Projekte"))
           && ElementExists(Window, cf => cf.ByName(" Einstellungen"));

    /// <inheritdoc/>
    public override MenuView ForceShow() => this;

    /// <inheritdoc/>
    public override MenuView ForceClose(bool recurseToDashboard) => this;

    /// <summary>Klickt den "Dashboard"-Button und wartet auf den Dashboard-Seitentitel.</summary>
    /// <returns>Die Dashboard-Ansicht.</returns>
    public DashboardView NavigateToDashboard()
    {
        WaitForElement(Window, cf => cf.ByName("Dashboard"), Short).AsButton().Click();
        WaitForElement(Window, cf => cf.ByName("Dashboard").And(cf.ByControlType(ControlType.Text)), Medium);

        var dashboard = new DashboardView(Window);
        Assert.True(dashboard.IsVisible, "Dashboard sollte nach Klick auf 'Dashboard' sichtbar sein.");
        return dashboard;
    }

    /// <summary>Klickt den "Projekte"-Button und wartet auf die Projektliste.</summary>
    /// <returns>Die Projektlisten-Ansicht.</returns>
    public ProjectListView NavigateToProjects()
    {
        WaitForElement(Window, cf => cf.ByName(" Projekte"), Short).AsButton().Click();
        WaitForElement(Window, cf => cf.ByName("Neu"), Medium);
        var projectList = new ProjectListView(Window);
        Assert.True(projectList.IsVisible, "Projektliste sollte nach Klick auf 'Projekte' sichtbar sein.");
        return projectList;
    }

    /// <summary>Klickt den "Einstellungen"-Button und wartet auf die Einstellungs-Tabs.</summary>
    /// <returns>Die Einstellungen-Ansicht.</returns>
    public SettingsView NavigateToSettings()
    {
        WaitForElement(Window, cf => cf.ByName(" Einstellungen"), Short).AsButton().Click();
        WaitForElement(Window, cf => cf.ByName("Plugins"), Medium);

        var settings = new SettingsView(Window);
        Assert.True(settings.IsVisible, "Einstellungen sollten nach Klick auf 'Einstellungen' sichtbar sein.");
        return settings;
    }

    /// <returns>Der in der Fußzeile der Navigations-Seitenleiste angezeigte Versionstext ("AppVersionText").</returns>
    public string GetVersionText() => WaitForElement(Window, cf => cf.ByAutomationId("AppVersionText"), Short).Name;

    /// <summary>
    /// Wechselt über die Aufgabenliste in der Seitenleiste ("Aktive Aufgaben") direkt zur angegebenen
    /// Aufgabe, ohne über "Zurück" zu navigieren.
    /// </summary>
    /// <param name="taskTitle">Der Titel der Aufgabe.</param>
    /// <returns>Die Aufgabendetailansicht der Zielaufgabe.</returns>
    public TaskDetailView NavigateToTask(string taskTitle)
    {
        WaitForElement(Window, cf => cf.ByName($"AufgabeNavigieren:{taskTitle}"), Medium).AsButton().Click();
        return new TaskDetailView(Window);
    }

    /// <summary>
    /// Wartet, bis die Status-Kachel der Aufgabe in der Seitenleiste den erwarteten Status-Text als
    /// <c>AutomationProperties.HelpText</c> anzeigt (siehe ActiveTasksListControl.xaml).
    /// </summary>
    /// <param name="taskTitle">Der Titel der Aufgabe.</param>
    /// <param name="expectedStatus">Der erwartete Status-Text (z. B. "▶ Läuft").</param>
    /// <param name="timeout">Maximale Wartezeit.</param>
    /// <returns>Diese Instanz.</returns>
    /// <exception cref="TimeoutException">Wird geworfen, wenn der erwartete Status nicht rechtzeitig erscheint.</exception>
    public MenuView WaitForTaskStatus(string taskTitle, string expectedStatus, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        string? lastStatus = null;
        while (DateTime.UtcNow < deadline)
        {
            var statusElement = Window.FindFirstDescendant(cf => cf.ByName($"AufgabeStatus:{taskTitle}"));
            lastStatus = statusElement?.HelpText;
            if (lastStatus == expectedStatus)
                return this;

            Thread.Sleep(200);
        }

        throw new TimeoutException(
            $"Statuskachel zeigte innerhalb von {timeout.TotalSeconds}s nicht den erwarteten Status '{expectedStatus}' an. Zuletzt gesehen: '{lastStatus}'.");
    }
}
