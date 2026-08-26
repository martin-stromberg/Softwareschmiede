using FlaUI.Core.AutomationElements;
using Softwareschmiede.Tests.E2E.Views;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// E2E-Tests für die neue IDE-Plugins-Sektion im Plugins-Register der Einstellungen (Issue #204):
/// Aktivierungsstatus-Persistenz, Validierung "mindestens ein IDE-Plugin muss aktiv bleiben" und
/// Reihenfolge-Verwaltung über die Up/Down-Buttons.
///
/// Voraussetzungen:
/// - Windows-Desktop-Session (kein Headless-CI)
/// - Softwareschmiede.App muss im Debug-Modus gebaut sein
/// - Die eingebauten IDE-Plugins (Visual Studio, Visual Studio Code) werden unabhängig vom Testmodus-
///   DLL-Filter registriert (siehe PluginManager.RegisterBuiltInIdePlugins) und sind daher auch im
///   Testmodus über die Plugins-Sektion sichtbar.
///
/// Konsolidierung: Aktivierung/Validierung und Reihenfolge-Verwaltung laufen als zwei Phasen in einem
/// gemeinsamen App-Lifecycle statt zweier eigenständiger App-Starts.
///
/// CI-Regular-Lauf: dotnet test --filter "Category!=OsInterface"
/// </summary>
public partial class End2EndTest
{
    /// <summary>
    /// Führt Aktivierungs-/Validierungs- und Reihenfolge-Verwaltung der IDE-Plugins-Sektion als zwei
    /// Phasen im selben App-Lifecycle aus.
    /// </summary>
    /// <param name="mainWindow">Das Hauptfenster der Anwendung.</param>
    protected void IdePluginSettings_AktivierungValidierungUndReihenfolge_E2E(Window mainWindow)
    {
        IdePluginAktivierung_LetztesPluginBlockiertUndPersistiertStatus_E2E(mainWindow);
        IdePluginReihenfolge_UpDownButtonsAendernUndPersistierenReihenfolge_E2E(mainWindow);
    }

    /// <summary>
    /// Szenario: Visual Studio Code wird über den Listeneintrag ausgewählt und im Inhaltsbereich über die
    /// Checkbox "IdePluginAktiviert" deaktiviert, dann gespeichert - Persistenz wird über ein erneutes
    /// Öffnen der Einstellungen geprüft. Anschließend wird versucht, auch Visual Studio (das letzte noch
    /// aktive IDE-Plugin) zu deaktivieren: die Checkbox springt zurück auf aktiviert und eine
    /// Fehlermeldung erscheint. Am Ende wird Visual Studio Code wieder aktiviert, damit Phase 2 mit
    /// beiden Plugins aktiv startet.
    /// </summary>
    /// <param name="mainWindow">Das Hauptfenster der Anwendung.</param>
    private void IdePluginAktivierung_LetztesPluginBlockiertUndPersistiertStatus_E2E(Window mainWindow)
    {
        var settings = new SettingsView(mainWindow).ForceShow();
        settings.SwitchTab("Plugins");

        settings.SetIdePluginEnabled("Softwareschmiede.VisualStudioCode", false);
        settings.SaveSettings();
        settings.Menu.NavigateToDashboard();

        var settingsReloaded = new SettingsView(mainWindow).ForceShow();
        settingsReloaded.SwitchTab("Plugins");
        Assert.False(settingsReloaded.IsIdePluginEnabled("Softwareschmiede.VisualStudioCode"));

        settingsReloaded.SetIdePluginEnabled("Softwareschmiede.VisualStudio", false);
        Assert.False(string.IsNullOrWhiteSpace(new ErrorView(mainWindow).GetErrorMessage()));
        Assert.True(settingsReloaded.IsIdePluginEnabled("Softwareschmiede.VisualStudio"));

        // Visual Studio Code für Phase 2 wieder aktivieren und speichern.
        settingsReloaded.SetIdePluginEnabled("Softwareschmiede.VisualStudioCode", true);
        settingsReloaded.SaveSettings();
    }

    /// <summary>
    /// Szenario: Visual Studio Code wird über den "Nach oben"-Button vor Visual Studio in der
    /// IDE-Plugin-Liste einsortiert. Prüft: Die neue Reihenfolge bleibt nach Verlassen und erneutem
    /// Öffnen der Einstellungen erhalten (Persistenz in <c>plugins.ide.order</c>).
    /// </summary>
    /// <param name="mainWindow">Das Hauptfenster, aktuell im Plugins-Register der Einstellungen.</param>
    private void IdePluginReihenfolge_UpDownButtonsAendernUndPersistierenReihenfolge_E2E(Window mainWindow)
    {
        var settings = Assert.IsType<SettingsView>(mainWindow.CurrentView());

        settings.MoveIdePluginUp("Softwareschmiede.VisualStudioCode");
        Assert.True(settings.IsFirstIdePlugin("Softwareschmiede.VisualStudioCode"));

        settings.Menu.NavigateToDashboard();

        var settingsReloaded = new SettingsView(mainWindow).ForceShow();
        settingsReloaded.SwitchTab("Plugins");
        Assert.True(settingsReloaded.IsFirstIdePlugin("Softwareschmiede.VisualStudioCode"));

        settingsReloaded.Menu.NavigateToDashboard();
    }
}
