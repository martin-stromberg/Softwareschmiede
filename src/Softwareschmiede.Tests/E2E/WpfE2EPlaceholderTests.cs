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
public sealed class WpfE2ETests : WpfTestBase
{
    private const string SkipReason =
        "Erfordert eine Windows-Desktop-Session und ein vorab gebautes Softwareschmiede.App.exe. " +
        "Nicht in Headless-CI ausführbar.";

    [Fact(Skip = SkipReason)]
    public void ProjektErstellen_ZeigtAufgabenListe_E2E()
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

    [Fact(Skip = SkipReason)]
    public void ProjektErstellen_UndNeueAufgabeAnlegen_E2E()
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

    [Fact(Skip = SkipReason)]
    public void AufgabeAnlegen_ZeigtCliStartenButton_E2E()
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

    [Fact(Skip = SkipReason)]
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

    [Fact(Skip = SkipReason)]
    public void Dashboard_KeineRecoveryBanner_BeiSauberemStart_E2E()
    {
        var app = LaunchApp();
        var mainWindow = app.GetMainWindow(Automation, TimeSpan.FromSeconds(20))!;

        var dashboardText = WaitForElement(mainWindow, cf => cf.ByName("Dashboard"), TimeSpan.FromSeconds(10));
        Assert.NotNull(dashboardText);

        var recoveryBanner = mainWindow.FindFirstDescendant(cf =>
            cf.ByName("Aufgabe(n) benötigen Wiederherstellung."));
        Assert.Null(recoveryBanner);
    }

    [Fact(Skip = SkipReason)]
    public void EinstellungenOeffnen_ZeigtEinstellungsSeite_E2E()
    {
        var app = LaunchApp();
        var mainWindow = app.GetMainWindow(Automation, TimeSpan.FromSeconds(20))!;

        var einstellungenButton = WaitForElement(mainWindow, cf => cf.ByName(" Einstellungen"), TimeSpan.FromSeconds(10));
        einstellungenButton.AsButton().Click();

        var einstellungenTitel = WaitForElement(mainWindow, cf => cf.ByName("Einstellungen"), TimeSpan.FromSeconds(10));
        Assert.NotNull(einstellungenTitel);

        var speichernButton = WaitForElement(mainWindow, cf => cf.ByName("Einstellungen speichern"), TimeSpan.FromSeconds(5));
        Assert.NotNull(speichernButton);
    }

    [Fact(Skip = SkipReason)]
    public void EinstellungenArbeitsverzeichnis_Aendern_UndSpeichern_E2E()
    {
        var app = LaunchApp();
        var mainWindow = app.GetMainWindow(Automation, TimeSpan.FromSeconds(20))!;

        var einstellungenButton = WaitForElement(mainWindow, cf => cf.ByName(" Einstellungen"), TimeSpan.FromSeconds(10));
        einstellungenButton.AsButton().Click();

        var textBoxen = mainWindow.FindAllDescendants(cf => cf.ByControlType(ControlType.Edit));
        Assert.True(textBoxen.Length > 0, "Kein Textfeld auf der Einstellungsseite gefunden.");

        var arbeitsverzeichnisBox = textBoxen[0].AsTextBox();
        arbeitsverzeichnisBox.Enter(@"C:\TestArbeitsverzeichnis");

        var speichernButton = WaitForElement(mainWindow, cf => cf.ByName("Einstellungen speichern"), TimeSpan.FromSeconds(5));
        speichernButton.AsButton().Click();

        Thread.Sleep(500);

        var erfolgsMeldung = mainWindow.FindFirstDescendant(cf =>
            cf.ByName("Einstellungen gespeichert."));
        Assert.NotNull(erfolgsMeldung);
    }

    [Fact(Skip = SkipReason)]
    public void EinstellungenNavigation_BleibtNachMehrerenKlicks_Stabil_E2E()
    {
        var app = LaunchApp();
        var mainWindow = app.GetMainWindow(Automation, TimeSpan.FromSeconds(20))!;

        var einstellungenButton = WaitForElement(mainWindow, cf => cf.ByName(" Einstellungen"), TimeSpan.FromSeconds(10));
        einstellungenButton.AsButton().Click();

        var speichernButton1 = WaitForElement(mainWindow, cf => cf.ByName("Einstellungen speichern"), TimeSpan.FromSeconds(5));
        Assert.NotNull(speichernButton1);

        var dashboardButton = WaitForElement(mainWindow, cf => cf.ByName("Dashboard"), TimeSpan.FromSeconds(5));
        dashboardButton.AsButton().Click();

        var projekteKachel = WaitForElement(mainWindow, cf => cf.ByName("Projekte"), TimeSpan.FromSeconds(5));
        Assert.NotNull(projekteKachel);

        einstellungenButton.Click();

        var speichernButton2 = WaitForElement(mainWindow, cf => cf.ByName("Einstellungen speichern"), TimeSpan.FromSeconds(5));
        Assert.NotNull(speichernButton2);
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
