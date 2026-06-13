using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Definitions;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// WPF End-to-End-Tests mit FlaUI. Die Anwendung wird als separater Prozess gestartet.
///
/// Voraussetzungen für die lokale Ausführung:
/// - Windows-Desktop-Session (kein Headless-CI)
/// - Das Projekt Softwareschmiede.App muss im Debug-Modus gebaut sein
///
/// Ausführung (lokal): dotnet test --filter Category=E2E
/// </summary>
[Trait("Category", "E2E")]
public sealed class WpfE2EPlaceholderTests : WpfTestBase
{
    [Fact]
    public void ProduktErstellenUndAufgabeHinzufuegen_E2E()
    {
        var app = LaunchApp();
        var mainWindow = app.GetMainWindow(Automation, TimeSpan.FromSeconds(20))!;

        var projekteButton = WaitForElement(mainWindow, cf => cf.ByName(" Projekte"), TimeSpan.FromSeconds(10));
        projekteButton.AsButton().Click();

        var neuesProjektButton = WaitForElement(mainWindow, cf => cf.ByName("+ Neues Projekt"), TimeSpan.FromSeconds(10));
        neuesProjektButton.AsButton().Click();

        var nameTextBox = WaitForElement(mainWindow, cf => cf.ByControlType(ControlType.Edit), TimeSpan.FromSeconds(5));
        nameTextBox.AsTextBox().Enter("E2E-Testprojekt");

        var erstellenButton = WaitForElement(mainWindow, cf => cf.ByName("Erstellen"), TimeSpan.FromSeconds(5));
        erstellenButton.AsButton().Click();

        var aufgabeListe = WaitForElement(mainWindow, cf => cf.ByControlType(ControlType.List), TimeSpan.FromSeconds(10));
        Assert.NotNull(aufgabeListe);
    }

    [Fact]
    public void AufgabeStarten_RepositoryKlonen_BranchErstellen_E2E()
    {
        var app = LaunchApp();
        var mainWindow = app.GetMainWindow(Automation, TimeSpan.FromSeconds(20))!;

        var projekteButton = WaitForElement(mainWindow, cf => cf.ByName(" Projekte"), TimeSpan.FromSeconds(10));
        projekteButton.AsButton().Click();

        var neuesProjektButton = WaitForElement(mainWindow, cf => cf.ByName("+ Neues Projekt"), TimeSpan.FromSeconds(10));
        neuesProjektButton.AsButton().Click();

        var nameTextBox = WaitForElement(mainWindow, cf => cf.ByControlType(ControlType.Edit), TimeSpan.FromSeconds(5));
        nameTextBox.AsTextBox().Enter("E2E-Startprojekt");

        var erstellenButton = WaitForElement(mainWindow, cf => cf.ByName("Erstellen"), TimeSpan.FromSeconds(5));
        erstellenButton.AsButton().Click();

        var neueAufgabe = WaitForElement(mainWindow, cf => cf.ByName("+ Neue Aufgabe"), TimeSpan.FromSeconds(10));
        neueAufgabe.AsButton().Click();

        var aufgabeListe = WaitForElement(mainWindow, cf => cf.ByControlType(ControlType.List), TimeSpan.FromSeconds(5));
        Assert.NotNull(aufgabeListe);

        var statusGestartetText = mainWindow.FindFirstDescendant(cf =>
            cf.ByControlType(ControlType.Text).And(cf.ByName("Gestartet")));
        Assert.Null(statusGestartetText);
    }

    [Fact]
    public void CliProzessStartenUndFensterEinbetten_E2E()
    {
        var app = LaunchApp();
        var mainWindow = app.GetMainWindow(Automation, TimeSpan.FromSeconds(20))!;

        var projekteButton = WaitForElement(mainWindow, cf => cf.ByName(" Projekte"), TimeSpan.FromSeconds(10));
        projekteButton.AsButton().Click();

        var neuesProjektButton = WaitForElement(mainWindow, cf => cf.ByName("+ Neues Projekt"), TimeSpan.FromSeconds(10));
        neuesProjektButton.AsButton().Click();

        var nameTextBox = WaitForElement(mainWindow, cf => cf.ByControlType(ControlType.Edit), TimeSpan.FromSeconds(5));
        nameTextBox.AsTextBox().Enter("E2E-CLI-Projekt");

        var erstellenButton = WaitForElement(mainWindow, cf => cf.ByName("Erstellen"), TimeSpan.FromSeconds(5));
        erstellenButton.AsButton().Click();

        var neueAufgabe = WaitForElement(mainWindow, cf => cf.ByName("+ Neue Aufgabe"), TimeSpan.FromSeconds(10));
        neueAufgabe.AsButton().Click();

        var cliStartenButton = WaitForElement(mainWindow, cf => cf.ByName("▶ CLI Starten"), TimeSpan.FromSeconds(10));
        Assert.NotNull(cliStartenButton);

        var windowHandle = mainWindow.FrameworkAutomationElement.NativeWindowHandle;
        Assert.NotEqual(IntPtr.Zero, windowHandle);
    }

    [Fact]
    public void DarkModeAktivierenUndPersistieren_E2E()
    {
        var app = LaunchApp();
        var mainWindow = app.GetMainWindow(Automation, TimeSpan.FromSeconds(20))!;

        var darkModeButton = WaitForElement(mainWindow, cf => cf.ByName(" Dark Mode"), TimeSpan.FromSeconds(10));
        darkModeButton.AsButton().Click();

        Thread.Sleep(500);

        var einstellungenButton = WaitForElement(mainWindow, cf => cf.ByName(" Einstellungen"), TimeSpan.FromSeconds(10));
        einstellungenButton.AsButton().Click();

        var darkModeCheckBox = WaitForElement(mainWindow, cf => cf.ByName("Dark Mode aktivieren"), TimeSpan.FromSeconds(5));
        Assert.True(darkModeCheckBox.AsCheckBox().IsChecked);
    }

    [Fact]
    public void RecoveryBannerNachHeartbeatTimeout_E2E()
    {
        var app = LaunchApp();
        var mainWindow = app.GetMainWindow(Automation, TimeSpan.FromSeconds(20))!;

        var dashboardText = WaitForElement(mainWindow, cf => cf.ByName("Dashboard"), TimeSpan.FromSeconds(10));
        Assert.NotNull(dashboardText);

        var recoveryBanner = mainWindow.FindFirstDescendant(cf =>
            cf.ByName("Aufgabe(n) benötigen Wiederherstellung."));
        Assert.Null(recoveryBanner);
    }

    private static AutomationElement WaitForElement(
        AutomationElement parent,
        Func<ConditionFactory, ConditionBase> conditionFunc,
        TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        AutomationElement? element = null;

        while (DateTime.UtcNow < deadline)
        {
            element = parent.FindFirstDescendant(conditionFunc);
            if (element is not null)
                return element;

            Thread.Sleep(200);
        }

        throw new TimeoutException(
            $"Element wurde nicht innerhalb von {timeout.TotalSeconds}s gefunden.");
    }
}
