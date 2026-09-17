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
        WaitForElement(Window, cf => cf.ByName(tabName), Short).ClickInForeground();
        return this;
    }

    /// <summary>Klickt den "Speichern"-Button und wartet auf die Bestätigung "Einstellungen gespeichert.".</summary>
    /// <returns>Diese Instanz.</returns>
    public SettingsView SaveSettings()
    {
        WaitForElement(Window, cf => cf.ByName("Speichern"), Short).AsButton().ClickInForeground();
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
        localDirectoryPluginEntry.ClickInForeground();

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
    /// <remarks>
    /// Ist der Eintrag bereits selektiert (z. B. weil er als persistierter Standard beim Öffnen
    /// automatisch gesetzt wurde), löst ein erneuter Klick kein SelectionChanged aus - das
    /// Plugin-Einstellungspanel würde nicht geladen. In dem Fall wird vorab ein anderes Plugin
    /// gewählt, um die Selektion anschließend erneut auslösen zu können.
    /// </remarks>
    public SettingsView SelectDefaultKiPlugin(string pluginDisplayName)
    {
        SwitchTab("Plugins");

        var kiPluginBox = WaitForElement(Window, cf => cf.ByName("DefaultKiPlugin"), Short);
        var comboBox = kiPluginBox.AsComboBox();

        if (string.Equals(comboBox.SelectedItem?.Name, pluginDisplayName, StringComparison.Ordinal))
        {
            var otherItemName = comboBox.Items
                .Select(item => item.Name)
                .FirstOrDefault(name => !string.Equals(name, pluginDisplayName, StringComparison.Ordinal));

            if (otherItemName is not null)
                SelectComboBoxItemByClick(kiPluginBox, otherItemName, Short);
        }

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
        WaitForElement(Window, cf => cf.ByName("CliHilfeButton"), Short).AsButton().ClickInForeground();

        var dialog = new HelpTextDialogView(Window);
        dialog.ForceShow();
        return dialog;
    }

    /// <summary>Wählt den Listeneintrag des IDE-Plugins aus ("{pluginPrefix}.Eintrag") und liest den Status der "IdePluginAktiviert"-Checkbox.</summary>
    /// <param name="pluginPrefix">Der Plugin-Prefix, z. B. "Softwareschmiede.VisualStudioCode".</param>
    /// <returns><c>true</c>, wenn das Plugin aktuell aktiviert ist.</returns>
    public bool IsIdePluginEnabled(string pluginPrefix)
    {
        WaitForElement(Window, cf => cf.ByName($"{pluginPrefix}.Eintrag"), Short).ClickInForeground();
        return WaitForElement(Window, cf => cf.ByName("IdePluginAktiviert"), Short).AsCheckBox().IsChecked ?? false;
    }

    /// <summary>Wählt den Listeneintrag des IDE-Plugins aus und setzt seinen Aktivierungsstatus über die Checkbox "IdePluginAktiviert".</summary>
    /// <param name="pluginPrefix">Der Plugin-Prefix, z. B. "Softwareschmiede.VisualStudioCode".</param>
    /// <param name="enabled">Der gewünschte Aktivierungsstatus.</param>
    /// <returns>Diese Instanz.</returns>
    public SettingsView SetIdePluginEnabled(string pluginPrefix, bool enabled)
    {
        WaitForElement(Window, cf => cf.ByName($"{pluginPrefix}.Eintrag"), Short).ClickInForeground();
        WaitForElement(Window, cf => cf.ByName("IdePluginAktiviert"), Short).AsCheckBox().IsChecked = enabled;
        return this;
    }

    /// <summary>Klickt den "Nach oben"-Button des angegebenen IDE-Plugin-Eintrags in der "IdePluginListe".</summary>
    /// <param name="pluginPrefix">Der Plugin-Prefix, z. B. "Softwareschmiede.VisualStudioCode".</param>
    /// <returns>Diese Instanz.</returns>
    public SettingsView MoveIdePluginUp(string pluginPrefix)
    {
        WaitForElement(Window, cf => cf.ByName($"{pluginPrefix}.NachOben"), Short).AsButton().ClickInForeground();
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
        WaitForElement(Window, cf => cf.ByName($"{pluginPrefix}.Eintrag"), Short).ClickInForeground();
        return WaitForElement(Window, cf => cf.ByName("PluginAktiviert"), Short).AsCheckBox().IsChecked ?? false;
    }

    /// <summary>Wählt den Listeneintrag eines SCM-/KI-Plugins aus und setzt seinen Aktivierungsstatus über die Checkbox "PluginAktiviert".</summary>
    /// <param name="pluginPrefix">Der Plugin-Prefix, z. B. "Softwareschmiede.ClaudeCli".</param>
    /// <param name="enabled">Der gewünschte Aktivierungsstatus.</param>
    /// <returns>Diese Instanz.</returns>
    public SettingsView SetPluginEnabled(string pluginPrefix, bool enabled)
    {
        WaitForElement(Window, cf => cf.ByName($"{pluginPrefix}.Eintrag"), Short).ClickInForeground();
        WaitForElement(Window, cf => cf.ByName("PluginAktiviert"), Short).AsCheckBox().IsChecked = enabled;
        return this;
    }

    /// <summary>Klickt den "Verwerfen"-Button und wartet, bis ein zuvor angezeigtes Fehlerbanner verschwindet.</summary>
    /// <returns>Diese Instanz.</returns>
    public SettingsView DiscardChanges()
    {
        WaitForElement(Window, cf => cf.ByName("Verwerfen"), Short).AsButton().ClickInForeground();
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

    /// <summary>Liest den Status der "Autonome Aufgaben aktivieren"-CheckBox im "Allgemein"-Tab (Issue 205).</summary>
    /// <returns><c>true</c>, wenn das Feature-Flag "Autonome Aufgaben aktivieren" aktuell aktiviert ist.</returns>
    public bool IsAutonomAufgabenEnabled()
        => WaitForElement(Window, cf => cf.ByName("IsAutonomAufgabenEnabled"), Short).AsCheckBox().IsChecked ?? false;

    /// <summary>Setzt den Status der "Autonome Aufgaben aktivieren"-CheckBox im "Allgemein"-Tab (Issue 205).</summary>
    /// <param name="enabled">Der gewünschte Aktivierungsstatus.</param>
    /// <returns>Diese Instanz.</returns>
    public SettingsView SetAutonomAufgabenEnabled(bool enabled)
    {
        WaitForElement(Window, cf => cf.ByName("IsAutonomAufgabenEnabled"), Short).AsCheckBox().IsChecked = enabled;
        return this;
    }

    /// <summary>Liest das aktuell ausgewählte Label der "Update-Modus"-ComboBox im "Allgemein"-Tab.</summary>
    /// <returns>Das Anzeige-Label des gewählten Update-Modus (siehe <c>UpdateModusTexte</c>).</returns>
    public string GetUpdateMode()
        => WaitForElement(Window, cf => cf.ByName("Update-Modus"), Short).AsComboBox().SelectedItem?.Name ?? string.Empty;

    /// <summary>Wählt einen Eintrag der "Update-Modus"-ComboBox und wartet, bis er übernommen wurde.</summary>
    /// <param name="label">Das Anzeige-Label (aus <c>UpdateModusTexte</c>: "Aus", "Nur prüfen" oder "Bei Programmstart prüfen und ausführen").</param>
    /// <returns>Diese Instanz.</returns>
    public SettingsView SetUpdateMode(string label)
    {
        var box = WaitForElement(Window, cf => cf.ByName("Update-Modus"), Short);
        SelectComboBoxItemByClick(box, label, Short);
        WaitForUpdateMode(label, Short);
        return this;
    }

    /// <summary>
    /// Wartet, bis die "Update-Modus"-ComboBox das erwartete Label anzeigt. Dient als echtes
    /// Synchronisationssignal für das Neuladen der persistierten Einstellungen.
    /// </summary>
    /// <param name="label">Das erwartete Anzeige-Label.</param>
    /// <param name="timeout">Maximale Wartezeit.</param>
    /// <returns>Diese Instanz.</returns>
    /// <exception cref="TimeoutException">Das Label wurde nicht rechtzeitig angezeigt.</exception>
    public SettingsView WaitForUpdateMode(string label, TimeSpan timeout)
    {
        ElementWaitHelper.WaitForSelectedComboBoxItem(
            WaitForElement(Window, cf => cf.ByName("Update-Modus"), timeout), label, timeout);
        return this;
    }

    /// <summary>Liest den Status der "Prerelease-Versionen laden"-CheckBox im "Allgemein"-Tab.</summary>
    /// <returns><c>true</c>, wenn Prerelease-Versionen aktuell geladen werden.</returns>
    public bool GetIncludePrereleases()
        => WaitForElement(Window, cf => cf.ByName("Prerelease-Versionen laden"), Short).AsCheckBox().IsChecked ?? false;

    /// <summary>Setzt den Status der "Prerelease-Versionen laden"-CheckBox im "Allgemein"-Tab.</summary>
    /// <param name="enabled">Der gewünschte Status.</param>
    /// <returns>Diese Instanz.</returns>
    public SettingsView SetIncludePrereleases(bool enabled)
    {
        WaitForElement(Window, cf => cf.ByName("Prerelease-Versionen laden"), Short).AsCheckBox().IsChecked = enabled;
        return this;
    }

    /// <summary>Wartet auf die Speicher-Bestätigung "Einstellungen gespeichert.".</summary>
    /// <returns>Diese Instanz.</returns>
    /// <exception cref="TimeoutException">Die Bestätigung erschien nicht rechtzeitig.</exception>
    public SettingsView WaitForSettingsSaved()
    {
        WaitForElement(Window, cf => cf.ByName("Einstellungen gespeichert."), Medium);
        return this;
    }
}
