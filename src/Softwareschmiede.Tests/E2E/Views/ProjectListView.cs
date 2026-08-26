using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace Softwareschmiede.Tests.E2E.Views;

/// <summary>View für die Projektlisten-Ansicht.</summary>
public sealed class ProjectListView : BaseWindowView
{
    private static readonly string[] StaticLabels = ["Neu", "Dashboard", " Projekte", " Einstellungen", "FehlerMeldung"];

    /// <param name="window">Das Hauptfenster der Anwendung.</param>
    public ProjectListView(Window window) : base(window)
    {
    }

    /// <inheritdoc/>
    public override bool IsVisible => ElementExists(Window, cf => cf.ByName("Neu"));

    /// <inheritdoc/>
    public override ProjectListView ForceShow()
    {
        if (IsVisible)
            return this;

        Menu.NavigateToProjects();
        return this;
    }

    /// <inheritdoc/>
    public override ProjectListView ForceClose(bool recurseToDashboard)
    {
        Menu.NavigateToDashboard();
        return this;
    }

    /// <summary>
    /// Sucht Projekt-Kacheln anhand ihrer Titel-Textelemente, beschränkt auf den Projektkacheln-Container
    /// ("ProjektKachelnListe"). Eine ungezielte Suche über das gesamte Fenster würde auch gleichnamige
    /// Text-Elemente aus der Seitenleiste treffen - z. B. den Aufgabentitel einer aktiven Aufgabe in
    /// "Aktive Aufgaben", der standardmäßig mit dem Projektnamen vorbelegt ist (siehe
    /// <see cref="OpenProject"/>).
    /// </summary>
    /// <returns>Die gefundenen Projekt-Kachel-Textelemente.</returns>
    public AutomationElement[] GetProjectElements()
        => GetProjectTilesContainer()
            .FindAllDescendants(cf => cf.ByControlType(ControlType.Text))
            .Where(e => !string.IsNullOrWhiteSpace(e.Name) && !StaticLabels.Contains(e.Name))
            .ToArray();

    /// <returns>Der Container-Element der Projektkacheln-Liste (AutomationId "ProjektKachelnListe").</returns>
    private AutomationElement GetProjectTilesContainer()
        => WaitForElement(Window, cf => cf.ByAutomationId("ProjektKachelnListe"), Short);

    /// <summary>
    /// Erstellt ein neues Projekt über den "Neu"-Button und speichert es. Bildet dieselbe fachliche
    /// Klick-/Warte-Sequenz wie <see cref="Softwareschmiede.Tests.E2E.WpfTestBase.CreateProject"/> ab.
    /// Das ist eine bewusst in Kauf genommene Rest-Duplikation: die gemeinsame Polling-/Timeout-Logik
    /// (<see cref="BaseWindowView.WaitForElement"/>, <see cref="BaseWindowView.WaitUntilGone"/>) ist
    /// bereits über <see cref="ElementWaitHelper"/> geteilt (siehe <see cref="BaseWindowView"/>); die
    /// verbleibende Duplikation betrifft nur die Reihenfolge der fachlichen UI-Schritte (Button- und
    /// Feldnamen wie "Neu"/"ProjektName"/"Speichern"). Eine weitere Extraktion in
    /// <see cref="ElementWaitHelper"/> wäre unpassend, da dieser Helper bewusst generisch (ohne
    /// Kenntnis fachlicher Button-/Feldnamen) bleiben soll; eine Abhängigkeit auf
    /// <see cref="Softwareschmiede.Tests.E2E.WpfTestBase"/> scheidet aus, da <see cref="BaseWindowView"/>
    /// bewusst nicht von dieser Basisklasse erbt (siehe Klassendoc).
    /// </summary>
    /// <param name="name">Der Projektname.</param>
    /// <returns>Diese Instanz.</returns>
    public ProjectListView CreateProject(string name)
    {
        WaitForElement(Window, cf => cf.ByName("Neu"), Short).AsButton().Click();

        var projectView = new ProjectDetailView(Window);
        Assert.True(projectView.IsVisible, "Projekt-Detailansicht sollte nach Klick auf 'Neu' sichtbar sein.");
        projectView.SetProjectName(name);
        projectView.SaveChanges();

        Assert.True(IsVisible, "Projektlisten-Ansicht sollte nach Speichern des neuen Projekts sichtbar sein.");
        Assert.Contains(name, GetProjectNames());
        return this;
    }

    /// <summary>
    /// Ruft die Namen aller Projekte in der Projektliste ab.
    /// </summary>
    /// <returns>Ein Array der Projektnamen.</returns>
    public string[] GetProjectNames()
    {
        return GetProjectElements().Select(e => e.Name).ToArray();
    }
    /// <summary>
    /// Überprüft, ob ein Projekt mit dem angegebenen Namen in der Projektliste existiert.
    /// </summary>
    /// <param name="name">Der Projektname.</param>
    /// <returns>True, wenn das Projekt existiert, andernfalls False.</returns>
    public bool ProjectExists(string name)
    {
        return GetProjectNames().Contains(name);
    }

    /// <summary>
    /// Öffnet ein Projekt aus der Liste anhand seines Namens. Bildet dieselbe fachliche Klick-/
    /// Warte-Sequenz wie <see cref="Softwareschmiede.Tests.E2E.WpfTestBase.OpenProject"/> ab - siehe
    /// Begründung der bewussten Rest-Duplikation in <see cref="CreateProject"/>.
    /// </summary>
    /// <remarks>
    /// Die Suche ist bewusst auf den Projektkacheln-Container beschränkt (statt das gesamte Fenster zu
    /// durchsuchen): Eine ungezielte Namenssuche würde bei gleichlautendem Projekt- und Aufgabentitel
    /// auch den entsprechenden Eintrag in der Seitenleiste ("Aktive Aufgaben") treffen - der Aufgabentitel
    /// ist standardmäßig mit dem Projektnamen vorbelegt, solange er nicht explizit geändert wurde.
    /// </remarks>
    /// <param name="name">Der Projektname.</param>
    /// <returns>Die Projektdetailansicht des geöffneten Projekts.</returns>
    public ProjectDetailView OpenProject(string name)
    {
        WaitForElement(GetProjectTilesContainer(), cf => cf.ByName(name), Short).Click();
        WaitForElement(Window, cf => cf.ByName("Speichern"), Medium);

        return new ProjectDetailView(Window);
    }
}
