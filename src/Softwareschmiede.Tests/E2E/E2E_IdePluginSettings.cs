using FlaUI.Core.AutomationElements;

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
    private void IdePluginAktivierung_LetztesPluginBlockiertUndPersistiertStatus_E2E(AutomationElement mainWindow)
    {
        NavigateToSettings(mainWindow);
        OpenPluginsTab(mainWindow);

        DeaktiviereIdePlugin(mainWindow, "Softwareschmiede.VisualStudioCode");

        var speichernButton = WaitForElement(mainWindow, cf => cf.ByName("Speichern"), Short);
        speichernButton.AsButton().Click();
        WaitForElement(mainWindow, cf => cf.ByName("Einstellungen gespeichert."), Short);

        var dashboardButton = WaitForElement(mainWindow, cf => cf.ByName("Dashboard"), Short);
        dashboardButton.AsButton().Click();

        NavigateToSettings(mainWindow);
        OpenPluginsTab(mainWindow);

        var vsCodeEintragReloaded = WaitForElement(mainWindow, cf => cf.ByName("Softwareschmiede.VisualStudioCode.Eintrag"), Short);
        vsCodeEintragReloaded.Click();
        var idePluginCheckboxReloaded = WaitForElement(mainWindow, cf => cf.ByName("IdePluginAktiviert"), Short);
        Assert.False(idePluginCheckboxReloaded.AsCheckBox().IsChecked);

        DeaktiviereIdePlugin(mainWindow, "Softwareschmiede.VisualStudio");

        var fehlerMeldung = WaitForElement(mainWindow, cf => cf.ByName("FehlerMeldung"), Short);
        Assert.NotNull(fehlerMeldung);

        var visualStudioEintragNachVersuch = WaitForElement(mainWindow, cf => cf.ByName("Softwareschmiede.VisualStudio.Eintrag"), Short);
        visualStudioEintragNachVersuch.Click();
        var visualStudioCheckboxNachVersuch = WaitForElement(mainWindow, cf => cf.ByName("IdePluginAktiviert"), Short);
        Assert.True(visualStudioCheckboxNachVersuch.AsCheckBox().IsChecked);

        // Visual Studio Code für Phase 2 wieder aktivieren und speichern.
        AktiviereIdePlugin(mainWindow, "Softwareschmiede.VisualStudioCode");

        var speichernButtonPhase1 = WaitForElement(mainWindow, cf => cf.ByName("Speichern"), Short);
        speichernButtonPhase1.AsButton().Click();
        WaitForElement(mainWindow, cf => cf.ByName("Einstellungen gespeichert."), Short);
    }

    /// <summary>
    /// Szenario: Visual Studio Code wird über den "Nach oben"-Button vor Visual Studio in der
    /// IDE-Plugin-Liste einsortiert. Prüft: Die neue Reihenfolge bleibt nach Verlassen und erneutem
    /// Öffnen der Einstellungen erhalten (Persistenz in <c>plugins.ide.order</c>).
    /// </summary>
    /// <param name="mainWindow">Das Hauptfenster, aktuell im Plugins-Register der Einstellungen.</param>
    private void IdePluginReihenfolge_UpDownButtonsAendernUndPersistierenReihenfolge_E2E(AutomationElement mainWindow)
    {
        var idePluginListe = WaitForElement(mainWindow, cf => cf.ByName("IdePluginListe"), Short);
        var ersterEintragVorVerschieben = idePluginListe.FindFirstDescendant(cf => cf.ByName("Softwareschmiede.VisualStudio.Eintrag"));
        Assert.NotNull(ersterEintragVorVerschieben);

        var vsCodeNachObenButton = WaitForElement(mainWindow, cf => cf.ByName("Softwareschmiede.VisualStudioCode.NachOben"), Short);
        vsCodeNachObenButton.AsButton().Click();

        var listenEintraege = idePluginListe.FindAllChildren(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.ListItem));
        Assert.True(listenEintraege.Length >= 2);
        var ersterName = listenEintraege[0].FindFirstDescendant(cf => cf.ByName("Softwareschmiede.VisualStudioCode.Eintrag"));
        Assert.NotNull(ersterName);

        var dashboardButton = WaitForElement(mainWindow, cf => cf.ByName("Dashboard"), Short);
        dashboardButton.AsButton().Click();

        NavigateToSettings(mainWindow);
        OpenPluginsTab(mainWindow);

        var idePluginListeReloaded = WaitForElement(mainWindow, cf => cf.ByName("IdePluginListe"), Short);
        var listenEintraegeReloaded = idePluginListeReloaded.FindAllChildren(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.ListItem));
        Assert.True(listenEintraegeReloaded.Length >= 2);
        var ersterNameReloaded = listenEintraegeReloaded[0].FindFirstDescendant(cf => cf.ByName("Softwareschmiede.VisualStudioCode.Eintrag"));
        Assert.NotNull(ersterNameReloaded);

        var dashboardButtonEnde = WaitForElement(mainWindow, cf => cf.ByName("Dashboard"), Short);
        dashboardButtonEnde.AsButton().Click();
    }

    /// <summary>Wählt den Listeneintrag des IDE-Plugins aus und deaktiviert es über die Checkbox "IdePluginAktiviert" im Inhaltsbereich.</summary>
    /// <param name="mainWindow">Das Hauptfenster, aktuell im Plugins-Register der Einstellungen.</param>
    /// <param name="pluginPrefix">Der Plugin-Prefix, z. B. "Softwareschmiede.VisualStudioCode".</param>
    private static void DeaktiviereIdePlugin(AutomationElement mainWindow, string pluginPrefix) =>
        SetzeIdePluginAktiviert(mainWindow, pluginPrefix, aktiviert: false);

    /// <summary>Wählt den Listeneintrag des IDE-Plugins aus und aktiviert es über die Checkbox "IdePluginAktiviert" im Inhaltsbereich.</summary>
    /// <param name="mainWindow">Das Hauptfenster, aktuell im Plugins-Register der Einstellungen.</param>
    /// <param name="pluginPrefix">Der Plugin-Prefix, z. B. "Softwareschmiede.VisualStudioCode".</param>
    private static void AktiviereIdePlugin(AutomationElement mainWindow, string pluginPrefix) =>
        SetzeIdePluginAktiviert(mainWindow, pluginPrefix, aktiviert: true);

    /// <summary>Klickt den Listeneintrag "{pluginPrefix}.Eintrag" und setzt anschließend die Checkbox "IdePluginAktiviert" im Inhaltsbereich.</summary>
    /// <param name="mainWindow">Das Hauptfenster, aktuell im Plugins-Register der Einstellungen.</param>
    /// <param name="pluginPrefix">Der Plugin-Prefix, z. B. "Softwareschmiede.VisualStudioCode".</param>
    /// <param name="aktiviert">Der gewünschte Aktivierungsstatus.</param>
    private static void SetzeIdePluginAktiviert(AutomationElement mainWindow, string pluginPrefix, bool aktiviert)
    {
        var eintrag = WaitForElement(mainWindow, cf => cf.ByName($"{pluginPrefix}.Eintrag"), Short);
        eintrag.Click();
        var checkbox = WaitForElement(mainWindow, cf => cf.ByName("IdePluginAktiviert"), Short);
        checkbox.AsCheckBox().IsChecked = aktiviert;
    }
}
