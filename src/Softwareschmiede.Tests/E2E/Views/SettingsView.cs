using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using Softwareschmiede.Tests.E2E;
using Softwareschmiede.Tests.E2E.Views.Dialogs;

namespace Softwareschmiede.Tests.E2E.Views;

/// <summary>View für die Einstellungen-Ansicht.</summary>
public sealed class SettingsView : BaseWindowView
{
    /// <param name="window">Das Hauptfenster der Anwendung.</param>
    public SettingsView(Window window) : base(window)
    {
    }

    /// <inheritdoc/>
    public override bool IsVisible => ElementExists(Window, cf => cf.ByName("Plugins"));

    /// <inheritdoc/>
    public override SettingsView ForceShow()
    {
        if (IsVisible)
            return this;

        Menu.NavigateToSettings();
        return this;
    }

    /// <inheritdoc/>
    public override SettingsView ForceClose(bool recurseToDashboard)
    {
        Menu.NavigateToDashboard();
        return this;
    }

    /// <returns>Der Name des aktuell aktiven Tabs.</returns>
    public string GetActiveTab()
    {
        var tabControl = WaitForElement(Window, cf => cf.ByControlType(ControlType.Tab), Short);
        return tabControl.AsTab().SelectedTabItem?.Name ?? string.Empty;
    }

    /// <summary>
    /// Prüft, ob der "Plugins"-Tab aktuell aktiv ist, anhand eines tab-spezifischen Markers
    /// ("LocalDirectoryPlugin.Eintrag"). <see cref="GetActiveTab"/> ist dafür nicht geeignet: Die
    /// TabItems dieser Ansicht haben keinen gebundenen <c>AutomationProperties.Name</c>, wodurch FlaUI
    /// für jeden Tab denselben generischen <c>TabItem.ToString()</c>-Fallback liefert, unabhängig davon,
    /// welcher Tab tatsächlich aktiv ist.
    /// </summary>
    /// <returns><c>true</c>, wenn der "Plugins"-Tab aktiv ist.</returns>
    public bool IsOnPluginsTab() => ElementExists(Window, cf => cf.ByName("LocalDirectoryPlugin.Eintrag"));

    /// <param name="tabName">Der Name des zu aktivierenden Tabs.</param>
    /// <returns>Diese Instanz.</returns>
    public SettingsView SwitchTab(string tabName)
    {
        WaitForElement(Window, cf => cf.ByName(tabName), Short).Click();
        return this;
    }

    /// <summary>Klickt den "Speichern"-Button und wartet auf die Bestätigung "Einstellungen gespeichert.".</summary>
    /// <returns>Diese Instanz.</returns>
    public SettingsView SaveSettings()
    {
        WaitForElement(Window, cf => cf.ByName("Speichern"), Short).AsButton().Click();
        WaitForElement(Window, cf => cf.ByName("Einstellungen gespeichert."), Short);

        return this;
    }

    /// <summary>
    /// Wählt im "Plugins"-Tab den Eintrag des LocalDirectoryPlugin aus, setzt WorkspaceMode und
    /// Quellverzeichnis und speichert. Navigiert anschließend zurück zum Dashboard. Bildet dieselbe
    /// fachliche Klick-/Warte-Sequenz wie <see cref="Softwareschmiede.Tests.E2E.WpfTestBase.ConfigureLocalDirectoryPlugin"/> ab.
    /// </summary>
    /// <param name="sourceDirectory">Das lokale Quellverzeichnis, das dem Plugin zugewiesen wird.</param>
    /// <param name="useInSourceDirectoryMode">Ob der WorkspaceMode auf "InSourceDirectory" (Standard) statt "SeparateWorkingDirectory" gesetzt wird.</param>
    /// <returns>Die Dashboard-Ansicht, zu der am Ende navigiert wird.</returns>
    public DashboardView ConfigureLocalDirectoryPlugin(string sourceDirectory, bool useInSourceDirectoryMode = true)
    {
        SwitchTab("Plugins");

        // Klickt gezielt auf das Namens-Label (nicht die Aktivierungs-CheckBox selbst), damit nur
        // der Listeneintrag ausgewählt wird, ohne den Aktivierungsstatus des Plugins zu verändern.
        var localDirectoryPluginEntry = WaitForElement(Window, cf => cf.ByName("LocalDirectoryPlugin.Eintrag"), Short);
        localDirectoryPluginEntry.Click();

        var workspaceModeBox = WaitForElement(Window, cf => cf.ByName("WorkspaceMode"), Short);
        var workspaceMode = useInSourceDirectoryMode ? "InSourceDirectory" : "SeparateWorkingDirectory";
        SelectComboBoxItemByClick(workspaceModeBox, workspaceMode, Short);

        var sourceDirectoryBox = WaitForElement(Window, cf => cf.ByName("SourceDirectory"), Short);
        sourceDirectoryBox.AsTextBox().Text = sourceDirectory;

        SaveSettings();

        return Menu.NavigateToDashboard();
    }

    /// <summary>Wählt im "Plugins"-Tab das angegebene KI-Plugin als Standard-Plugin (DefaultKiPlugin) aus.</summary>
    /// <param name="pluginDisplayName">Der Anzeigename des Plugins (z. B. "Codex CLI").</param>
    /// <returns>Diese Instanz.</returns>
    public SettingsView SelectDefaultKiPlugin(string pluginDisplayName)
    {
        SwitchTab("Plugins");

        var kiPluginBox = WaitForElement(Window, cf => cf.ByName("DefaultKiPlugin"), Short);
        SelectComboBoxItemByClick(kiPluginBox, pluginDisplayName, Short);

        return this;
    }

    /// <returns>Der aktuelle Wert des "CommandLineParameters"-Felds.</returns>
    public string GetCommandLineParameters() => WaitForElement(Window, cf => cf.ByName("CommandLineParameters"), Short).AsTextBox().Text;

    /// <param name="value">Der zu setzende Wert.</param>
    /// <returns>Diese Instanz.</returns>
    public SettingsView SetCommandLineParameters(string value)
    {
        WaitForElement(Window, cf => cf.ByName("CommandLineParameters"), Short).AsTextBox().Text = value;
        return this;
    }

    /// <returns>Der aktuelle Wert des "ExecutablePath"-Felds.</returns>
    public string GetExecutablePath() => WaitForElement(Window, cf => cf.ByName("ExecutablePath"), Short).AsTextBox().Text;

    /// <param name="value">Der zu setzende Pfad.</param>
    /// <returns>Diese Instanz.</returns>
    public SettingsView SetExecutablePath(string value)
    {
        WaitForElement(Window, cf => cf.ByName("ExecutablePath"), Short).AsTextBox().Text = value;
        return this;
    }

    /// <summary>Klickt den Hilfe-Button (?) neben den CommandLineParameters und öffnet den Hilfetext-Dialog.</summary>
    /// <returns>Der geöffnete Hilfetext-Dialog.</returns>
    public HelpTextDialogView OpenCliHelp()
    {
        WaitForElement(Window, cf => cf.ByName("CliHilfeButton"), Short).AsButton().Click();

        var dialog = new HelpTextDialogView(Window);
        dialog.ForceShow();
        return dialog;
    }

    /// <summary>Wählt den Listeneintrag des IDE-Plugins aus ("{pluginPrefix}.Eintrag") und liest den Status der "IdePluginAktiviert"-Checkbox.</summary>
    /// <param name="pluginPrefix">Der Plugin-Prefix, z. B. "Softwareschmiede.VisualStudioCode".</param>
    /// <returns><c>true</c>, wenn das Plugin aktuell aktiviert ist.</returns>
    public bool IsIdePluginEnabled(string pluginPrefix)
    {
        WaitForElement(Window, cf => cf.ByName($"{pluginPrefix}.Eintrag"), Short).Click();
        return WaitForElement(Window, cf => cf.ByName("IdePluginAktiviert"), Short).AsCheckBox().IsChecked ?? false;
    }

    /// <summary>Wählt den Listeneintrag des IDE-Plugins aus und setzt seinen Aktivierungsstatus über die Checkbox "IdePluginAktiviert".</summary>
    /// <param name="pluginPrefix">Der Plugin-Prefix, z. B. "Softwareschmiede.VisualStudioCode".</param>
    /// <param name="enabled">Der gewünschte Aktivierungsstatus.</param>
    /// <returns>Diese Instanz.</returns>
    public SettingsView SetIdePluginEnabled(string pluginPrefix, bool enabled)
    {
        WaitForElement(Window, cf => cf.ByName($"{pluginPrefix}.Eintrag"), Short).Click();
        WaitForElement(Window, cf => cf.ByName("IdePluginAktiviert"), Short).AsCheckBox().IsChecked = enabled;
        return this;
    }

    /// <summary>Klickt den "Nach oben"-Button des angegebenen IDE-Plugin-Eintrags in der "IdePluginListe".</summary>
    /// <param name="pluginPrefix">Der Plugin-Prefix, z. B. "Softwareschmiede.VisualStudioCode".</param>
    /// <returns>Diese Instanz.</returns>
    public SettingsView MoveIdePluginUp(string pluginPrefix)
    {
        WaitForElement(Window, cf => cf.ByName($"{pluginPrefix}.NachOben"), Short).AsButton().Click();
        return this;
    }

    /// <summary>Prüft, ob der angegebene IDE-Plugin-Eintrag aktuell an erster Stelle der "IdePluginListe" steht.</summary>
    /// <param name="pluginPrefix">Der Plugin-Prefix, z. B. "Softwareschmiede.VisualStudioCode".</param>
    /// <returns><c>true</c>, wenn der Eintrag an erster Stelle steht.</returns>
    public bool IsFirstIdePlugin(string pluginPrefix)
    {
        var liste = WaitForElement(Window, cf => cf.ByName("IdePluginListe"), Short);
        var items = liste.FindAllChildren(cf => cf.ByControlType(ControlType.ListItem));
        if (items.Length < 2)
            return false;

        return items[0].FindFirstDescendant(cf => cf.ByName($"{pluginPrefix}.Eintrag")) is not null;
    }

    /// <summary>Wählt den Listeneintrag eines SCM-/KI-Plugins aus ("{pluginPrefix}.Eintrag") und liest den Status der "PluginAktiviert"-Checkbox.</summary>
    /// <param name="pluginPrefix">Der Plugin-Prefix, z. B. "Softwareschmiede.ClaudeCli".</param>
    /// <returns><c>true</c>, wenn das Plugin aktuell aktiviert ist.</returns>
    public bool IsPluginEnabled(string pluginPrefix)
    {
        WaitForElement(Window, cf => cf.ByName($"{pluginPrefix}.Eintrag"), Short).Click();
        return WaitForElement(Window, cf => cf.ByName("PluginAktiviert"), Short).AsCheckBox().IsChecked ?? false;
    }

    /// <summary>Wählt den Listeneintrag eines SCM-/KI-Plugins aus und setzt seinen Aktivierungsstatus über die Checkbox "PluginAktiviert".</summary>
    /// <param name="pluginPrefix">Der Plugin-Prefix, z. B. "Softwareschmiede.ClaudeCli".</param>
    /// <param name="enabled">Der gewünschte Aktivierungsstatus.</param>
    /// <returns>Diese Instanz.</returns>
    public SettingsView SetPluginEnabled(string pluginPrefix, bool enabled)
    {
        WaitForElement(Window, cf => cf.ByName($"{pluginPrefix}.Eintrag"), Short).Click();
        WaitForElement(Window, cf => cf.ByName("PluginAktiviert"), Short).AsCheckBox().IsChecked = enabled;
        return this;
    }

    /// <summary>Klickt den "Verwerfen"-Button und wartet, bis ein zuvor angezeigtes Fehlerbanner verschwindet.</summary>
    /// <returns>Diese Instanz.</returns>
    public SettingsView DiscardChanges()
    {
        WaitForElement(Window, cf => cf.ByName("Verwerfen"), Short).AsButton().Click();
        WaitUntilGone(Window, cf => cf.ByName("FehlerMeldung"), Short);
        return this;
    }

    /// <returns>Der aktuell in der "DesignMode"-ComboBox ausgewählte Wert.</returns>
    public string GetDesignMode() => WaitForElement(Window, cf => cf.ByName("DesignMode"), Short).AsComboBox().SelectedItem?.Name ?? string.Empty;

    /// <summary>Wählt einen Wert in der "DesignMode"-ComboBox aus und wartet, bis er tatsächlich übernommen wurde.</summary>
    /// <param name="value">Der zu wählende Wert (z. B. "Dark"/"Light").</param>
    /// <returns>Diese Instanz.</returns>
    public SettingsView SetDesignMode(string value)
    {
        var box = WaitForElement(Window, cf => cf.ByName("DesignMode"), Short);
        SelectComboBoxItemByClick(box, value, Short);
        ElementWaitHelper.WaitForSelectedComboBoxItem(box, value, Short);
        return this;
    }

    /// <summary>Setzt den Text des ersten TextBox-Feldes auf der Einstellungsseite (z. B. das Arbeitsverzeichnis-Feld auf der Standard-Ansicht).</summary>
    /// <param name="value">Der zu setzende Text.</param>
    /// <returns>Diese Instanz.</returns>
    /// <exception cref="InvalidOperationException">Wird geworfen, wenn kein Textfeld gefunden wird.</exception>
    public SettingsView SetFirstTextBoxValue(string value)
    {
        var textBoxen = Window.FindAllDescendants(cf => cf.ByControlType(ControlType.Edit));
        if (textBoxen.Length == 0)
            throw new InvalidOperationException("Kein Textfeld auf der Einstellungsseite gefunden.");

        textBoxen[0].AsTextBox().Text = value;
        return this;
    }
}
