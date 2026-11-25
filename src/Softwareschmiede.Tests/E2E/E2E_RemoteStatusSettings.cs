using FlaUI.Core.AutomationElements;
using Softwareschmiede.Tests.E2E.Views;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// E2E-Test für den Einstellungsabschnitt „Remote-Übersicht“ (Issue 478) im
/// „Automatisierung“-Tab: Expander mit Funktionsschalter, per <c>IsEnabled</c> gesperrter
/// Feldbereich (URL, Anzeigename, Token) mit Hinweistext, dauerhaft sichtbarer
/// Instanz-ID/Verbindungsstatus und Speichern-und-Neuladen-Persistenz.
/// </summary>
public partial class End2EndTest
{
    /// <summary>
    /// Prüft den Abschnitt „Remote-Übersicht“: Deaktiviert sind die Editierfelder gesperrt
    /// (mit Hinweistext), Instanz-ID und Verbindungsstatus bleiben sichtbar und der
    /// Header-Status zeigt „deaktiviert“; aktiviert werden die Felder bedienbar und der
    /// Header meldet „aktiviert“. Ein Speichern-Neuladen-Zyklus beweist Persistenz von
    /// Schalter, URL, Name und Instanz-ID; abschließend wird der Ursprungszustand
    /// (deaktiviert) wiederhergestellt.
    /// </summary>
    /// <param name="mainWindow">Das Hauptfenster der Anwendung.</param>
    protected void Settings_RemoteStatusSection_TogglesAndPersists_E2E(Window mainWindow)
    {
        var settings = new SettingsView(mainWindow).ForceShow();
        settings.OpenRemoteStatus();

        // Deaktiviert: Header-Status „deaktiviert“, Editierfelder gesperrt mit Hinweistext;
        // Instanz-ID und Verbindungsstatus bleiben sichtbar.
        WaitForElement(mainWindow, cf => cf.ByName("IsRemoteStatusEnabled"), Short);
        Assert.False(settings.IsRemoteStatusEnabledChecked());
        WaitForElement(mainWindow, cf => cf.ByName("RemoteStatusUrl"), Short);
        Assert.False(settings.IsRemoteStatusUrlEnabled());
        Assert.True(settings.IsRemoteStatusDisabledHintVisible());
        WaitForElement(mainWindow, cf => cf.ByName("RemoteStatusInstanceId"), Short);
        WaitForElement(mainWindow, cf => cf.ByName("RemoteStatusConnectionText"), Short);
        Assert.False(string.IsNullOrWhiteSpace(settings.GetRemoteStatusConnectionText()));
        settings.CollapseRemoteStatus();
        Assert.Equal("deaktiviert", settings.GetRemoteStatusStatus());

        // Aktivieren: Felder werden bedienbar, Hinweis verschwindet; die Instanz-ID wird
        // mit dem Speichern vergeben (Load-/Save-Pfad, nicht beim Anhaken).
        settings.OpenRemoteStatus();
        settings.SetRemoteStatusEnabledChecked(true);
        Assert.True(settings.IsRemoteStatusUrlEnabled());
        Assert.False(settings.IsRemoteStatusDisabledHintVisible());
        settings.SetRemoteStatusUrl("http://localhost:5055");
        settings.SetRemoteStatusInstanceName("E2E-Instanz");
        settings.SaveSettings();
        var instanceId = settings.GetRemoteStatusInstanceId();
        Assert.False(string.IsNullOrWhiteSpace(instanceId));
        settings.CollapseRemoteStatus();
        Assert.Equal("aktiviert", settings.GetRemoteStatusStatus());

        // Einstellungen neu laden: Schalter, URL, Name und Instanz-ID bleiben erhalten;
        // die Felder sind weiterhin bedienbar.
        var settingsReopened = settings.Menu.NavigateToSettings();
        settingsReopened.OpenRemoteStatus();
        Assert.True(settingsReopened.IsRemoteStatusEnabledChecked());
        Assert.True(settingsReopened.IsRemoteStatusUrlEnabled());
        Assert.Equal("http://localhost:5055", settingsReopened.GetRemoteStatusUrl());
        Assert.Equal("E2E-Instanz", settingsReopened.GetRemoteStatusInstanceName());
        Assert.Equal(instanceId, settingsReopened.GetRemoteStatusInstanceId());

        // Ursprungszustand wiederherstellen: deaktivieren, Felder sperren, speichern.
        settingsReopened.SetRemoteStatusEnabledChecked(false);
        Assert.False(settingsReopened.IsRemoteStatusUrlEnabled());
        settingsReopened.SaveSettings();
        var settingsRestored = settingsReopened.Menu.NavigateToSettings();
        settingsRestored.OpenRemoteStatus();
        Assert.False(settingsRestored.IsRemoteStatusEnabledChecked());

        settingsRestored.Menu.NavigateToDashboard();
    }
}
