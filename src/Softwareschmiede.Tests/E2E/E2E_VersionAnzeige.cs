namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// E2E-Test für die Anzeige der installierten Programmversion in der Fußzeile der Navigations-Seitenleiste (Issue 147).
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
    /// Szenario: App starten mit aufgeklappter Seitenleiste (Standardzustand).
    /// Prüft: Der Versions-TextBlock (AutomationId "AppVersionText") in der Fußzeile der Seitenleiste
    /// zeigt einen nicht-leeren Versionstext an.
    /// </summary>
    protected void AppStarten_ZeigtVersionsTextInFusszeile_E2E(FlaUI.Core.AutomationElements.Window mainWindow)
    {        
        var versionText = WaitForElement(mainWindow, cf => cf.ByAutomationId("AppVersionText"), Short);
        Assert.False(string.IsNullOrWhiteSpace(versionText.Name));
    }
}
