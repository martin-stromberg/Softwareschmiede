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
[Trait("Category", "E2E")]
[OsInterface]
[Collection("E2E")]
public sealed class E2E_VersionAnzeige : WpfTestBase
{
    /// <summary>
    /// Szenario: App starten mit aufgeklappter Seitenleiste (Standardzustand).
    /// Prüft: Der Versions-TextBlock (AutomationId "AppVersionText") in der Fußzeile der Seitenleiste
    /// zeigt einen nicht-leeren Versionstext an.
    /// </summary>
    [Fact]
    public void AppStarten_ZeigtVersionsTextInFusszeile_E2E()
    {
        var app = LaunchApp();
        var mainWindow = app.GetMainWindow(Automation, Long)!;

        var versionText = WaitForElement(mainWindow, cf => cf.ByAutomationId("AppVersionText"), Short);

        Assert.False(string.IsNullOrWhiteSpace(versionText.Name));
    }
}
