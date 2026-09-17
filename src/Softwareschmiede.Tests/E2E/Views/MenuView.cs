using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace Softwareschmiede.Tests.E2E.Views;

/// <summary>View für das persistente Navigationsmenü der Anwendung (Dashboard/Projekte/Einstellungen).</summary>
public sealed class MenuView : BaseWindowView
{
    private static readonly string[] ProjekteButtonNamen = [" Projekte", "Projekte"];
    private static readonly string[] EinstellungenButtonNamen = [" Einstellungen", "Einstellungen"];

    /// <param name="window">Das Hauptfenster der Anwendung.</param>
    public MenuView(Window window) : base(window)
    {
    }

    /// <inheritdoc/>
    public override bool IsVisible
        => ElementExists(Window, cf => cf.ByName("Dashboard"))
           && TryFindNavigationButton(ProjekteButtonNamen) is not null
           && TryFindNavigationButton(EinstellungenButtonNamen) is not null;

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
        WaitForNavigationButton(ProjekteButtonNamen, Short).AsButton().Click();
        WaitForElement(Window, cf => cf.ByName("Neu"), Medium);
        var projectList = new ProjectListView(Window);
        Assert.True(projectList.IsVisible, "Projektliste sollte nach Klick auf 'Projekte' sichtbar sein.");
        return projectList;
    }

    /// <summary>Klickt den "Einstellungen"-Button und wartet auf die Einstellungs-Tabs.</summary>
    /// <returns>Die Einstellungen-Ansicht.</returns>
    public SettingsView NavigateToSettings()
    {
        WaitForNavigationButton(EinstellungenButtonNamen, Short).AsButton().Click();
        WaitForElement(Window, cf => cf.ByName("Plugins"), Medium);

        var settings = new SettingsView(Window);
        Assert.True(settings.IsVisible, "Einstellungen sollten nach Klick auf 'Einstellungen' sichtbar sein.");
        return settings;
    }

    /// <returns>Der in der Fußzeile der Navigations-Seitenleiste angezeigte Versionstext ("AppVersionText").</returns>
    public string GetVersionText() => WaitForElement(Window, cf => cf.ByAutomationId("AppVersionText"), Short).Name;

    /// <summary>Klickt den "Programmupdate prüfen"-Button der Seitenleiste.</summary>
    /// <returns>Diese Instanz.</returns>
    public MenuView ClickUpdatePruefen()
    {
        WaitForElement(Window, cf => cf.ByName("Programmupdate prüfen"), Short).AsButton().Click();
        return this;
    }

    /// <summary>Klickt den "Programmupdate starten"-Button der Seitenleiste (nur bei Updateangebot sichtbar).</summary>
    /// <returns>Diese Instanz.</returns>
    /// <exception cref="TimeoutException">Der Button ist nicht sichtbar, weil kein Update angeboten wird.</exception>
    public MenuView ClickUpdateStarten()
    {
        WaitForElement(Window, cf => cf.ByName("Programmupdate starten"), Medium).AsButton().Click();
        return this;
    }

    /// <summary>Gibt an, ob der "Programmupdate starten"-Button aktuell sichtbar ist (Updateangebot liegt an).</summary>
    /// <returns><c>true</c>, wenn der Button im Automation-Baum vorhanden und nicht ausgeblendet ist.</returns>
    public bool IsUpdateStartButtonVisible()
    {
        var button = Window.FindFirstDescendant(
            cf => cf.ByName("Programmupdate starten").And(cf.ByControlType(ControlType.Button)));
        return button is not null && !button.IsOffscreen;
    }

    /// <summary>Gibt an, ob der "Programmupdate starten"-Button sichtbar und aktiviert ist.</summary>
    /// <returns><c>true</c>, wenn der Button sichtbar und klickbar ist.</returns>
    public bool IsUpdateStartButtonEnabled()
    {
        var button = Window.FindFirstDescendant(
            cf => cf.ByName("Programmupdate starten").And(cf.ByControlType(ControlType.Button)));
        return button is not null && !button.IsOffscreen && button.IsEnabled;
    }

    /// <summary>Gibt an, ob der "Programmupdate prüfen"-Button aktuell aktiviert ist.</summary>
    /// <returns><c>true</c>, wenn der Button klickbar ist.</returns>
    public bool IsUpdatePruefenButtonEnabled()
    {
        var button = Window.FindFirstDescendant(
            cf => cf.ByName("Programmupdate prüfen").And(cf.ByControlType(ControlType.Button)));
        return button is not null && button.IsEnabled;
    }

    /// <summary>
    /// Liest die angebotene Update-Version aus dem ToolTip des "Programmupdate starten"-Buttons
    /// ("Update auf Version {0} vorbereiten"). WPF bildet <c>ToolTip</c> auf die UIA-Eigenschaft
    /// <c>HelpText</c> ab.
    /// </summary>
    /// <returns>Die angebotene Versionszeichenkette oder <c>null</c>, wenn kein Update angeboten wird.</returns>
    public string? GetOfferedUpdateVersion()
    {
        var button = Window.FindFirstDescendant(
            cf => cf.ByName("Programmupdate starten").And(cf.ByControlType(ControlType.Button)));
        if (button is null || button.IsOffscreen)
            return null;

        var helpText = button.HelpText;
        const string prefix = "Update auf Version ";
        const string suffix = " vorbereiten";
        if (helpText is not null
            && helpText.StartsWith(prefix, StringComparison.Ordinal)
            && helpText.EndsWith(suffix, StringComparison.Ordinal))
        {
            return helpText[prefix.Length..^suffix.Length];
        }

        return helpText;
    }

    /// <summary>
    /// Wartet, bis der "Programmupdate starten"-Button erscheint, und liefert die angebotene Version.
    /// </summary>
    /// <param name="timeout">Maximale Wartezeit.</param>
    /// <returns>Die angebotene Versionszeichenkette.</returns>
    /// <exception cref="TimeoutException">Es wurde kein Update angeboten.</exception>
    public string WaitForOfferedUpdateVersion(TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var version = GetOfferedUpdateVersion();
            if (version is not null)
                return version;

            Thread.Sleep(200);
        }

        throw new TimeoutException($"Innerhalb von {timeout.TotalSeconds}s wurde kein Updateangebot angezeigt.");
    }

    /// <summary>Liest den aktuellen "UpdateHinweis"-Text der Seitenleiste.</summary>
    /// <returns>Der Hinweistext oder <c>null</c>, wenn kein Hinweis angezeigt wird.</returns>
    /// <remarks>
    /// Das Element trägt kein <c>AutomationProperties.Name</c> - der gebundene Hinweistext ist
    /// daher als UIA-Name des TextBlocks lesbar.
    /// </remarks>
    public string? GetUpdateHinweis()
    {
        var element = Window.FindFirstDescendant(cf => cf.ByAutomationId("UpdateHinweis"));
        if (element is null || element.IsOffscreen)
            return null;

        return element.Name;
    }

    /// <summary>Wartet, bis der "UpdateHinweis" einen Text enthält, der <paramref name="teilText"/> umfasst.</summary>
    /// <param name="teilText">Der erwartete Teiltext.</param>
    /// <param name="timeout">Maximale Wartezeit.</param>
    /// <returns>Der vollständige Hinweistext.</returns>
    /// <exception cref="TimeoutException">Der Hinweis erschien nicht rechtzeitig.</exception>
    public string WaitForUpdateHinweis(string teilText, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        string? zuletzt = null;
        while (DateTime.UtcNow < deadline)
        {
            zuletzt = GetUpdateHinweis();
            if (zuletzt is not null && zuletzt.Contains(teilText, StringComparison.Ordinal))
                return zuletzt;

            Thread.Sleep(200);
        }

        throw new TimeoutException(
            $"UpdateHinweis enthielt nicht innerhalb von {timeout.TotalSeconds}s '{teilText}'. Zuletzt gesehen: '{zuletzt}'.");
    }

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
    /// Liest den in der Seitenleiste für die angegebene aktive Aufgabe angezeigten KI-Plugin-Namen
    /// (Text "KI: {Name}").
    /// </summary>
    /// <param name="taskTitle">Der Titel der Aufgabe.</param>
    /// <returns>Der reine Plugin-Name ohne das "KI: "-Präfix.</returns>
    public string GetActiveTaskKiPluginName(string taskTitle)
    {
        var kiPluginText = WaitForElement(Window, cf => cf.ByAutomationId($"AktiveAufgabeKiPlugin_{taskTitle}"), Short);
        var name = kiPluginText.Name;
        if (name is not null && name.StartsWith("KI: ", StringComparison.Ordinal))
        {
            return name.Substring(4);
        }

        throw new InvalidOperationException($"Für Aufgabe '{taskTitle}' wurde kein 'KI: '-Eintrag in der Seitenleiste gefunden. Gesehen: '{name}'.");
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

    private AutomationElement WaitForNavigationButton(IReadOnlyList<string> names, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var button = TryFindNavigationButton(names);
            if (button is not null)
                return button;

            Thread.Sleep(200);
        }

        throw new TimeoutException($"Navigations-Button wurde nicht innerhalb von {timeout.TotalSeconds}s gefunden. Gesucht: {string.Join(", ", names)}");
    }

    private AutomationElement? TryFindNavigationButton(IReadOnlyList<string> names)
    {
        foreach (var name in names)
        {
            var button = Window.FindFirstDescendant(cf => cf.ByName(name).And(cf.ByControlType(ControlType.Button)));
            if (button is not null)
                return button;
        }

        return null;
    }
}
