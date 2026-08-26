using FlaUI.Core.AutomationElements;
using Softwareschmiede.Tests.E2E.Views;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// WPF End-to-End-Tests mit FlaUI. Die Anwendung wird als separater Prozess gestartet.
///
/// Voraussetzungen:
/// - Windows-Desktop-Session (kein Headless-CI)
/// - Softwareschmiede.App muss im Debug-Modus gebaut sein (dotnet build src/Softwareschmiede.App)
///
/// Konsolidierung (Issue #153): Die ursprünglich acht Einzeltests laufen als Phasen in einem
/// gemeinsamen App-Lifecycle, da alle Tests dieser Klasse denselben einfachen Interaktionsmustern
/// folgen und keine sich gegenseitig ausschließenden Vorbedingungen haben.
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
    /// Führt die einfachen WPF-E2E-Szenarien für Projekt/Aufgabe und Einstellungen in einem App-Lifecycle aus.
    /// </summary>
    [Fact]
    public void WpfBasisSzenarien()
    {
        SetLocalDirectoryWorkspaceMode("SeparateWorkingDirectory");

        var app = LaunchApp();
        var mainWindow = app.GetMainWindow(Automation, TimeSpan.FromSeconds(20))!;

        Projekt_ErstellenUndAufgabeAnlegen_ZeigtListeUndStartenButton_E2E(mainWindow);
        Einstellungen_OeffnenAendernUndNavigationBleibtStabil_E2E(mainWindow);
    }

    /// <summary>
    /// Szenario: Projekt anlegen und öffnen (Aufgabenliste sichtbar); neue Aufgabe anlegen (Liste
    /// weiterhin sichtbar, kein Status "Gestartet"); "Starten"-Button sichtbar, Hauptfenster besitzt
    /// ein gültiges Handle.
    /// </summary>
    private void Projekt_ErstellenUndAufgabeAnlegen_ZeigtListeUndStartenButton_E2E(Window mainWindow)
    {
        var projectList = new ProjectListView(mainWindow).ForceShow();
        projectList.CreateProject("E2E-Startprojekt");
        var projectDetail = projectList.OpenProject("E2E-Startprojekt");

        projectDetail.WaitForTaskListVisible();

        var taskDetail = projectDetail.CreateTask();

        Assert.False(taskDetail.IsTaskStarted());

        taskDetail.WaitForStartAvailable();

        var windowHandle = mainWindow.FrameworkAutomationElement.NativeWindowHandle;
        Assert.NotEqual(IntPtr.Zero, windowHandle);

        taskDetail.ForceClose(recurseToDashboard: false);
        var projectDetailAfterTask = Assert.IsType<ProjectDetailView>(mainWindow.CurrentView());
        projectDetailAfterTask.DeleteProject();
        projectDetailAfterTask.Menu.NavigateToDashboard();
    }

    /// <summary>
    /// Szenario: Sauberer Start ohne Recovery-Banner; Einstellungsseite öffnen (Speichern sichtbar);
    /// Dark Mode umschalten, speichern und nach Rückkehr Persistenz prüfen; Arbeitsverzeichnis ändern
    /// und speichern; mehrfache Navigation zwischen Dashboard und Einstellungen bleibt stabil.
    /// </summary>
    private void Einstellungen_OeffnenAendernUndNavigationBleibtStabil_E2E(Window mainWindow)
    {
        // Sauberer Start: kein Recovery-Banner
        var dashboard = Assert.IsType<DashboardView>(mainWindow.CurrentView());
        Assert.True(dashboard.IsVisible);
        Assert.False(dashboard.HasRecoveryBanner());

        // Einstellungen öffnen (Ribbon-"Speichern"-Button bestätigt, dass die Seite geladen ist)
        var settings = dashboard.Menu.NavigateToSettings();

        // Dark Mode umschalten
        var originalValue = settings.GetDesignMode();
        var neuerWert = string.Equals(originalValue, "Dark", StringComparison.OrdinalIgnoreCase)
            ? "Light"
            : "Dark";

        // Statt manuell zu öffnen (Click) und den Eintrag über die gesamte Desktop-Automatisierungsstruktur
        // zu suchen (Automation.GetDesktop() – auf CI-Runnern unzuverlässig), sucht SetDesignMode den
        // Eintrag im Scope der ComboBox selbst und hält definierte Settle-Pausen ein, mit anschließendem
        // Polling auf die tatsächlich übernommene Auswahl.
        settings.SetDesignMode(neuerWert);

        settings.SaveSettings();

        // Einstellungsseite verlassen und zurückkehren
        var dashboardNachDarkMode = settings.Menu.NavigateToDashboard();
        var settingsErneut = dashboardNachDarkMode.Menu.NavigateToSettings();

        // Nach Rückkehr: Design-ComboBox zeigt den gespeicherten Wert. SettingsView.Loaded löst
        // vm.LadenCommand.Execute(null) als Fire-and-Forget aus; DesignMode wird darin erst nach mehreren
        // vorausgehenden awaits (Arbeitsverzeichnis, Standard-KI-Plugin) neu gesetzt. GetDesignMode liest
        // daher nicht einmalig, sondern über SetDesignMode-artiges Polling - hier genügt ein direkter Read,
        // da NavigateToSettings bereits auf die geladenen Plugins-Tabs gewartet hat; falls doch noch ein
        // Zwischenwert gelesen würde, schlägt der folgende Assert.Equal aussagekräftig fehl statt zu flackern.
        Assert.Equal(neuerWert, settingsErneut.GetDesignMode());

        // Arbeitsverzeichnis ändern und speichern
        settingsErneut.SetFirstTextBoxValue(@"C:\TestArbeitsverzeichnis");
        settingsErneut.SaveSettings();

        // Mehrfache Navigation bleibt stabil: Dashboard -> Projekte-Kachel sichtbar -> erneut Einstellungen
        var dashboardNavigation = settingsErneut.Menu.NavigateToDashboard();
        Assert.True(dashboardNavigation.Menu.IsVisible);

        dashboardNavigation.Menu.NavigateToSettings();
    }
}
