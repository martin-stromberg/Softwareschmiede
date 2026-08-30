using FlaUI.Core.AutomationElements;
using Softwareschmiede.Tests.E2E.Views;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// E2E-Test für den Feature-Flag-Schalter "Autonome Aufgaben aktivieren" in den Einstellungen (Issue 205).
/// </summary>
public partial class End2EndTest
{
    /// <summary>
    /// Öffnet die Einstellungsseite, prüft den Default-Wert der CheckBox "Autonome Aufgaben aktivieren"
    /// (aktiviert, siehe <see cref="Softwareschmiede.Application.Services.AutonomAufgabenOptions.Enabled"/>-Default),
    /// deaktiviert sie, speichert und prüft nach erneutem Öffnen der Einstellungen, dass der geänderte Wert
    /// über <see cref="Softwareschmiede.Application.Services.AppEinstellungService"/> persistiert wurde.
    /// </summary>
    /// <param name="mainWindow">Das Hauptfenster der Anwendung.</param>
    protected void Einstellungen_AutonomAufgabenFeatureFlagToggle_PersistiertWert_E2E(Window mainWindow)
    {
        var settings = new SettingsView(mainWindow).ForceShow();
        Assert.True(settings.IsAutonomAufgabenEnabled(), "Autonome Aufgaben sollten standardmäßig aktiviert sein.");

        settings.SetAutonomAufgabenEnabled(false);
        settings.SaveSettings();
        settings.Menu.NavigateToDashboard();

        var settingsReopened = new SettingsView(mainWindow).ForceShow();
        Assert.False(settingsReopened.IsAutonomAufgabenEnabled(), "Der deaktivierte Zustand muss nach erneutem Öffnen der Einstellungen erhalten bleiben.");

        // Wert zurücksetzen, damit nachfolgende Testphasen im selben App-Lifecycle (z. B. weitere autonome
        // Aufgaben-Szenarien) von der Standardkonfiguration unbeeinflusst bleiben.
        settingsReopened.SetAutonomAufgabenEnabled(true);
        settingsReopened.SaveSettings();
        settingsReopened.Menu.NavigateToDashboard();
    }
}
