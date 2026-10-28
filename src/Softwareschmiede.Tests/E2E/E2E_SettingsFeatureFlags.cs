using FlaUI.Core.AutomationElements;
using Softwareschmiede.Tests.E2E.Views;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// E2E-Test für den Inhalt der Registerkarte "Allgemein" in den Einstellungen.
/// </summary>
public partial class End2EndTest
{
    /// <summary>
    /// Öffnet die Registerkarte "Allgemein" der Einstellungen und prüft, dass der Abschnitt
    /// "SCM-Ereignisse" angezeigt wird und weder ein Abschnitt "Automatisierung" noch eine
    /// Checkbox zur Aktivierung autonomer Aufgaben vorhanden ist.
    /// </summary>
    /// <param name="mainWindow">Das Hauptfenster der Anwendung.</param>
    protected void Einstellungen_AllgemeinEnthaeltNurVorhandeneAbschnitte_E2E(Window mainWindow)
    {
        var settings = new SettingsView(mainWindow).ForceShow();
        settings.SwitchTab("Allgemein");

        WaitForElement(mainWindow, cf => cf.ByName("IsScmMonitoringEnabled"), Short);
        Assert.Null(mainWindow.FindFirstDescendant(cf => cf.ByName("IsAutonomAufgabenEnabled")));
        Assert.Null(mainWindow.FindFirstDescendant(cf => cf.ByName("Automatisierung")));

        settings.Menu.NavigateToDashboard();
    }
}
