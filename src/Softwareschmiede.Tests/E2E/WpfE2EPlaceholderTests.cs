using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// WPF End-to-End-Tests mit FlaUI. Die Anwendung wird als separater Prozess gestartet.
///
/// Voraussetzungen:
/// - Windows-Desktop-Session (kein Headless-CI)
/// - Softwareschmiede.App muss im Debug-Modus gebaut sein (dotnet build src/Softwareschmiede.App)
///
/// Ausführung (lokal): dotnet test --filter Category=E2E
/// CI-Ausschluss:      dotnet test --filter "Category!=E2E"
/// </summary>
[Trait("Category", "E2E")]
[Collection("E2E")]
public sealed class WpfE2ETests : WpfTestBase
{
    /// <summary>Prüft, dass nach dem Anlegen eines Projekts die Aufgabenliste sichtbar ist.</summary>
    [Fact]
    public void ProjektErstellen_ZeigtAufgabenListe_E2E()
    {
        var app = LaunchApp();
        var mainWindow = app.GetMainWindow(Automation, TimeSpan.FromSeconds(20))!;

        NavigateToProjecten(mainWindow);

        CreateProject(mainWindow, "E2E-Testprojekt");
        OpenProject(mainWindow, "E2E-Testprojekt");

        var aufgabeListe = WaitForElement(mainWindow, cf => cf.ByControlType(ControlType.List), TimeSpan.FromSeconds(10));
        Assert.NotNull(aufgabeListe);
    }

    /// <summary>Prüft, dass nach Projekterstellung eine neue Aufgabe angelegt werden kann und der Status nicht "Gestartet" ist.</summary>
    [Fact]
    public void ProjektErstellen_UndNeueAufgabeAnlegen_E2E()
    {
        var app = LaunchApp();
        var mainWindow = app.GetMainWindow(Automation, TimeSpan.FromSeconds(20))!;

        NavigateToProjecten(mainWindow);
        CreateAndOpenProject(mainWindow, "E2E-Startprojekt");

        var aufgabeNeuButton = WaitForElement(mainWindow, cf => cf.ByName("AufgabeNeu"), TimeSpan.FromSeconds(10));
        aufgabeNeuButton.AsButton().Click();

        var aufgabeListe = WaitForElement(mainWindow, cf => cf.ByControlType(ControlType.List), Short);
        Assert.NotNull(aufgabeListe);

        var statusGestartetText = mainWindow.FindFirstDescendant(cf =>
            cf.ByControlType(ControlType.Text).And(cf.ByName("Gestartet")));
        Assert.Null(statusGestartetText);
    }

    /// <summary>Prüft, dass nach dem Anlegen einer Aufgabe der "Starten"-Button sichtbar ist und das Fenster ein gültiges Handle besitzt.</summary>
    [Fact]
    public void AufgabeAnlegen_ZeigtStartenButton_E2E()
    {
        var app = LaunchApp();
        var mainWindow = app.GetMainWindow(Automation, TimeSpan.FromSeconds(20))!;

        NavigateToProjecten(mainWindow);
        CreateAndOpenProject(mainWindow, "E2E-CLI-Projekt");

        var aufgabeNeuButton = WaitForElement(mainWindow, cf => cf.ByName("AufgabeNeu"), TimeSpan.FromSeconds(10));
        aufgabeNeuButton.AsButton().Click();

        var startenButton = WaitForElement(mainWindow, cf => cf.ByName("Starten"), TimeSpan.FromSeconds(10));
        Assert.NotNull(startenButton);

        var windowHandle = mainWindow.FrameworkAutomationElement.NativeWindowHandle;
        Assert.NotEqual(IntPtr.Zero, windowHandle);
    }

    /// <summary>Prüft, dass der Dark Mode in den Einstellungen umgeschaltet und nach Rückkehr persistiert wird.</summary>
    [Fact]
    public void DarkModeAktivierenUndPersistieren_E2E()
    {
        SetLocalDirectoryWorkspaceMode("SeparateWorkingDirectory");

        var app = LaunchApp();
        var mainWindow = app.GetMainWindow(Automation, TimeSpan.FromSeconds(20))!;

        // Einstellungen öffnen
        var einstellungenButton = WaitForElement(mainWindow, cf => cf.ByName(" Einstellungen"), TimeSpan.FromSeconds(10));
        einstellungenButton.AsButton().Click();

        // Speichern-Button im Ribbon bestätigt, dass die Einstellungsseite geladen ist
        WaitForElement(mainWindow, cf => cf.ByName("Speichern"), Short);

        var designComboBoxElement = WaitForElement(mainWindow, cf => cf.ByName("DesignMode"), Short);
        var designComboBox = designComboBoxElement.AsComboBox();
        var originalValue = designComboBox.SelectedItem?.Name ?? string.Empty;

        var neuerWert = string.Equals(originalValue, "Dark", StringComparison.OrdinalIgnoreCase)
            ? "Light"
            : "Dark";

        designComboBoxElement.Click();
        var neuerEintrag = WaitForElement(Automation.GetDesktop(), cf => cf.ByName(neuerWert), Short);
        neuerEintrag.Click();

        WaitForSelectedComboBoxItem(designComboBoxElement, neuerWert, Short);

        // Einstellungen speichern über Ribbon-Button
        var speichernButton = WaitForElement(mainWindow, cf => cf.ByName("Speichern"), Short);
        speichernButton.AsButton().Click();

        WaitForElement(mainWindow, cf => cf.ByName("Einstellungen gespeichert."), TimeSpan.FromSeconds(10));

        // Einstellungsseite verlassen und zurückkehren
        var dashboardButton = WaitForElement(mainWindow, cf => cf.ByName("Dashboard"), Short);
        dashboardButton.AsButton().Click();

        einstellungenButton.Click();

        // Nach Rückkehr: Design-ComboBox zeigt den gespeicherten Wert. SettingsView.Loaded löst
        // vm.LadenCommand.Execute(null) als Fire-and-Forget aus; DesignMode wird darin erst nach mehreren
        // vorausgehenden awaits (Arbeitsverzeichnis, Standard-KI-Plugin) neu gesetzt. Ein direktes Assert
        // unmittelbar nach dem Auffinden der ComboBox liest daher auf langsameren/kalten CI-Runnern
        // gelegentlich noch den alten Wert, bevor der Reload abgeschlossen ist — deshalb wird hier wie beim
        // ersten Auswählen oben (Zeile 106) auf den erwarteten Wert gepollt statt einmalig geprüft.
        WaitForElement(mainWindow, cf => cf.ByName("Speichern"), Short);
        var designComboBoxNachRueckkehr = WaitForElement(mainWindow, cf => cf.ByName("DesignMode"), Short);

        WaitForSelectedComboBoxItem(designComboBoxNachRueckkehr, neuerWert, Short);
    }

    /// <summary>Prüft, dass beim sauberen Start kein Recovery-Banner angezeigt wird.</summary>
    [Fact]
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

    /// <summary>Prüft, dass die Einstellungsseite geöffnet werden kann und der Speichern-Button sichtbar ist.</summary>
    [Fact]
    public void EinstellungenOeffnen_ZeigtEinstellungsSeite_E2E()
    {
        var app = LaunchApp();
        var mainWindow = app.GetMainWindow(Automation, TimeSpan.FromSeconds(20))!;

        var einstellungenButton = WaitForElement(mainWindow, cf => cf.ByName(" Einstellungen"), TimeSpan.FromSeconds(10));
        einstellungenButton.AsButton().Click();

        // Ribbon-Speichern-Button bestätigt, dass die Einstellungsseite geladen ist
        var speichernButton = WaitForElement(mainWindow, cf => cf.ByName("Speichern"), TimeSpan.FromSeconds(10));
        Assert.NotNull(speichernButton);
    }

    /// <summary>Prüft, dass das Arbeitsverzeichnis in den Einstellungen geändert und gespeichert werden kann.</summary>
    [Fact]
    public void EinstellungenArbeitsverzeichnis_Aendern_UndSpeichern_E2E()
    {
        SetLocalDirectoryWorkspaceMode("SeparateWorkingDirectory");

        var app = LaunchApp();
        var mainWindow = app.GetMainWindow(Automation, TimeSpan.FromSeconds(20))!;

        var einstellungenButton = WaitForElement(mainWindow, cf => cf.ByName(" Einstellungen"), TimeSpan.FromSeconds(10));
        einstellungenButton.AsButton().Click();

        // Warten bis Einstellungsseite geladen ist
        WaitForElement(mainWindow, cf => cf.ByName("Speichern"), Short);

        var textBoxen = mainWindow.FindAllDescendants(cf => cf.ByControlType(ControlType.Edit));
        Assert.True(textBoxen.Length > 0, "Kein Textfeld auf der Einstellungsseite gefunden.");

        var arbeitsverzeichnisBox = textBoxen[0].AsTextBox();
        arbeitsverzeichnisBox.Text = @"C:\TestArbeitsverzeichnis";

        // Speichern über Ribbon-Button
        var speichernButton = WaitForElement(mainWindow, cf => cf.ByName("Speichern"), Short);
        speichernButton.AsButton().Click();

        WaitForElement(mainWindow, cf => cf.ByName("Einstellungen gespeichert."), TimeSpan.FromSeconds(10));
    }

    /// <summary>Prüft, dass mehrfache Navigation zur Einstellungsseite stabil bleibt und der Speichern-Button nach Rückkehr wieder erscheint.</summary>
    [Fact]
    public void EinstellungenNavigation_BleibtNachMehrerenKlicks_Stabil_E2E()
    {
        var app = LaunchApp();
        var mainWindow = app.GetMainWindow(Automation, TimeSpan.FromSeconds(20))!;

        var einstellungenButton = WaitForElement(mainWindow, cf => cf.ByName(" Einstellungen"), TimeSpan.FromSeconds(10));
        einstellungenButton.AsButton().Click();

        // Ribbon-Speichern-Button bestätigt, dass die Einstellungsseite geladen ist
        var speichernButton1 = WaitForElement(mainWindow, cf => cf.ByName("Speichern"), Short);
        Assert.NotNull(speichernButton1);

        var dashboardButton = WaitForElement(mainWindow, cf => cf.ByName("Dashboard"), Short);
        dashboardButton.AsButton().Click();

        var projekteKachel = WaitForElement(mainWindow, cf => cf.ByName("Projekte"), Short);
        Assert.NotNull(projekteKachel);

        einstellungenButton.Click();

        var speichernButton2 = WaitForElement(mainWindow, cf => cf.ByName("Speichern"), Short);
        Assert.NotNull(speichernButton2);
    }
}
