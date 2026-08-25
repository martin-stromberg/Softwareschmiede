using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;

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
    /// Sucht Projekt-Kacheln anhand ihrer Titel-Textelemente. Projektkacheln haben keine eigene
    /// Automation-Id; als Heuristik werden alle Text-Elemente zurückgegeben, deren Name nicht zu den
    /// bekannten statischen Beschriftungen dieser Ansicht gehört (Projekttitel und -beschreibung).
    /// </summary>
    /// <returns>Die gefundenen Projekt-Kachel-Textelemente.</returns>
    public AutomationElement[] GetProjectElements()
        => Window.FindAllDescendants(cf => cf.ByControlType(ControlType.Text))
            .Where(e => !string.IsNullOrWhiteSpace(e.Name) && !StaticLabels.Contains(e.Name))
            .ToArray();

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

        var nameBox = WaitForElement(Window, cf => cf.ByName("ProjektName"), Short);
        nameBox.Click();
        Keyboard.Type(name);

        WaitForElement(Window, cf => cf.ByName("Speichern"), Short).AsButton().Click();
        WaitUntilGone(Window, cf => cf.ByName("Speichern"), Medium);
        WaitForElement(Window, cf => cf.ByName(name), Medium);

        return this;
    }

    /// <summary>
    /// Öffnet ein Projekt aus der Liste anhand seines Namens. Bildet dieselbe fachliche Klick-/
    /// Warte-Sequenz wie <see cref="Softwareschmiede.Tests.E2E.WpfTestBase.OpenProject"/> ab - siehe
    /// Begründung der bewussten Rest-Duplikation in <see cref="CreateProject"/>.
    /// </summary>
    /// <param name="name">Der Projektname.</param>
    /// <returns>Die Projektdetailansicht des geöffneten Projekts.</returns>
    public ProjectDetailView OpenProject(string name)
    {
        WaitForElement(Window, cf => cf.ByName(name), Short).Click();
        WaitForElement(Window, cf => cf.ByName("Speichern"), Medium);

        return new ProjectDetailView(Window);
    }
}
