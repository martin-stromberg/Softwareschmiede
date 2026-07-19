using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// WPF End-to-End-Tests mit FlaUI. Die Anwendung wird als separater Prozess gestartet.
///
/// Voraussetzungen:
/// - Windows-Desktop-Session (kein Headless-CI)
/// - Softwareschmiede.App muss im Debug-Modus gebaut sein (dotnet build src/Softwareschmiede.App)
///
/// Konsolidierung (Issue #153): Die ursprünglich acht Einzeltests laufen als Phasen in zwei
/// gemeinsamen App-Lifecycles - ein Projekt-/Aufgaben-Flow und ein Einstellungen-Flow -, da alle
/// Tests dieser Klasse denselben einfachen Interaktionsmustern folgen und keine sich gegenseitig
/// ausschließenden Vorbedingungen haben.
///
/// Ausführung (lokal): dotnet test --filter Category=E2E
/// CI-Regular-Lauf:    dotnet test --filter "Category!=OsInterface"
/// </summary>
[Trait("Category", "E2E")]
[OsInterface]
[Collection("E2E")]
public sealed class WpfE2ETests : WpfTestBase
{
    /// <summary>
    /// Szenario: Projekt anlegen und öffnen (Aufgabenliste sichtbar); neue Aufgabe anlegen (Liste
    /// weiterhin sichtbar, kein Status "Gestartet"); "Starten"-Button sichtbar, Hauptfenster besitzt
    /// ein gültiges Handle.
    /// </summary>
    [Fact]
    public void Projekt_ErstellenUndAufgabeAnlegen_ZeigtListeUndStartenButton_E2E()
    {
        var app = LaunchApp();
        var mainWindow = app.GetMainWindow(Automation, TimeSpan.FromSeconds(20))!;

        NavigateToProjects(mainWindow);
        CreateAndOpenProject(mainWindow, "E2E-Startprojekt");

        WaitForElement(mainWindow, cf => cf.ByControlType(ControlType.List), TimeSpan.FromSeconds(10));

        var aufgabeNeuButton = WaitForElement(mainWindow, cf => cf.ByName("AufgabeNeu"), TimeSpan.FromSeconds(10));
        aufgabeNeuButton.AsButton().Click();

        WaitForElement(mainWindow, cf => cf.ByControlType(ControlType.List), Short);

        var statusGestartetText = mainWindow.FindFirstDescendant(cf =>
            cf.ByControlType(ControlType.Text).And(cf.ByName("Gestartet")));
        Assert.Null(statusGestartetText);

        WaitForElement(mainWindow, cf => cf.ByName("Starten"), TimeSpan.FromSeconds(10));

        var windowHandle = mainWindow.FrameworkAutomationElement.NativeWindowHandle;
        Assert.NotEqual(IntPtr.Zero, windowHandle);
    }

    /// <summary>
    /// Szenario: Sauberer Start ohne Recovery-Banner; Einstellungsseite öffnen (Speichern sichtbar);
    /// Dark Mode umschalten, speichern und nach Rückkehr Persistenz prüfen; Arbeitsverzeichnis ändern
    /// und speichern; mehrfache Navigation zwischen Dashboard und Einstellungen bleibt stabil.
    /// </summary>
    [Fact]
    public void Einstellungen_OeffnenAendernUndNavigationBleibtStabil_E2E()
    {
        SetLocalDirectoryWorkspaceMode("SeparateWorkingDirectory");

        var app = LaunchApp();
        var mainWindow = app.GetMainWindow(Automation, TimeSpan.FromSeconds(20))!;

        // Sauberer Start: kein Recovery-Banner
        WaitForElement(mainWindow, cf => cf.ByName("Dashboard"), TimeSpan.FromSeconds(10));

        var recoveryBanner = mainWindow.FindFirstDescendant(cf =>
            cf.ByName("Aufgabe(n) benötigen Wiederherstellung."));
        Assert.Null(recoveryBanner);

        // Einstellungen öffnen
        var einstellungenButton = WaitForElement(mainWindow, cf => cf.ByName(" Einstellungen"), TimeSpan.FromSeconds(10));
        einstellungenButton.AsButton().Click();

        // Ribbon-Speichern-Button bestätigt, dass die Einstellungsseite geladen ist
        WaitForElement(mainWindow, cf => cf.ByName("Speichern"), TimeSpan.FromSeconds(10));

        // Dark Mode umschalten
        var designComboBoxElement = WaitForElement(mainWindow, cf => cf.ByName("DesignMode"), Short);
        var designComboBox = designComboBoxElement.AsComboBox();
        var originalValue = designComboBox.SelectedItem?.Name ?? string.Empty;

        var neuerWert = string.Equals(originalValue, "Dark", StringComparison.OrdinalIgnoreCase)
            ? "Light"
            : "Dark";

        // Statt manuell zu öffnen (Click) und den Eintrag über die gesamte Desktop-Automatisierungsstruktur
        // zu suchen (Automation.GetDesktop() – auf CI-Runnern unzuverlässig, siehe SelectComboBoxItemByClick),
        // wird hier dieselbe bereits andernorts (z. B. E2E_SettingsKiPluginPersistence) erprobte Hilfsmethode
        // verwendet, die den Eintrag im Scope der ComboBox selbst sucht und definierte Settle-Pausen einhält.
        SelectComboBoxItemByClick(designComboBoxElement, neuerWert, Short);
        WaitForSelectedComboBoxItem(designComboBoxElement, neuerWert, Short);

        // Einstellungen speichern über Ribbon-Button
        var speichernButtonDarkMode = WaitForElement(mainWindow, cf => cf.ByName("Speichern"), Short);
        speichernButtonDarkMode.AsButton().Click();

        WaitForElement(mainWindow, cf => cf.ByName("Einstellungen gespeichert."), TimeSpan.FromSeconds(10));

        // Einstellungsseite verlassen und zurückkehren
        var dashboardButtonNachDarkMode = WaitForElement(mainWindow, cf => cf.ByName("Dashboard"), Short);
        dashboardButtonNachDarkMode.AsButton().Click();

        einstellungenButton.Click();

        // Nach Rückkehr: Design-ComboBox zeigt den gespeicherten Wert. SettingsView.Loaded löst
        // vm.LadenCommand.Execute(null) als Fire-and-Forget aus; DesignMode wird darin erst nach mehreren
        // vorausgehenden awaits (Arbeitsverzeichnis, Standard-KI-Plugin) neu gesetzt. Ein direktes Assert
        // unmittelbar nach dem Auffinden der ComboBox liest daher auf langsameren/kalten CI-Runnern
        // gelegentlich noch den alten Wert, bevor der Reload abgeschlossen ist — deshalb wird hier wie beim
        // ersten Auswählen oben auf den erwarteten Wert gepollt statt einmalig geprüft.
        WaitForElement(mainWindow, cf => cf.ByName("Speichern"), Short);
        var designComboBoxNachRueckkehr = WaitForElement(mainWindow, cf => cf.ByName("DesignMode"), Short);

        WaitForSelectedComboBoxItem(designComboBoxNachRueckkehr, neuerWert, Short);

        // Arbeitsverzeichnis ändern und speichern
        var textBoxen = mainWindow.FindAllDescendants(cf => cf.ByControlType(ControlType.Edit));
        Assert.True(textBoxen.Length > 0, "Kein Textfeld auf der Einstellungsseite gefunden.");

        var arbeitsverzeichnisBox = textBoxen[0].AsTextBox();
        arbeitsverzeichnisBox.Text = @"C:\TestArbeitsverzeichnis";

        var speichernButtonArbeitsverzeichnis = WaitForElement(mainWindow, cf => cf.ByName("Speichern"), Short);
        speichernButtonArbeitsverzeichnis.AsButton().Click();

        WaitForElement(mainWindow, cf => cf.ByName("Einstellungen gespeichert."), TimeSpan.FromSeconds(10));

        // Mehrfache Navigation bleibt stabil: Dashboard -> Projekte-Kachel sichtbar -> erneut Einstellungen
        var dashboardButtonNavigation = WaitForElement(mainWindow, cf => cf.ByName("Dashboard"), Short);
        dashboardButtonNavigation.AsButton().Click();

        WaitForElement(mainWindow, cf => cf.ByName("Projekte"), Short);

        einstellungenButton.Click();

        WaitForElement(mainWindow, cf => cf.ByName("Speichern"), Short);
    }
}
