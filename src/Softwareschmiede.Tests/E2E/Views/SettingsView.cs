using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
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
    /// ("LocalDirectoryPlugin.Entry"). <see cref="GetActiveTab"/> ist dafür nicht geeignet: Die
    /// TabItems dieser Ansicht haben keinen gebundenen <c>AutomationProperties.Name</c>, wodurch FlaUI
    /// für jeden Tab denselben generischen <c>TabItem.ToString()</c>-Fallback liefert, unabhängig davon,
    /// welcher Tab tatsächlich aktiv ist.
    /// </summary>
    /// <returns><c>true</c>, wenn der "Plugins"-Tab aktiv ist.</returns>
    public bool IsOnPluginsTab() => ElementExists(Window, cf => cf.ByName("LocalDirectoryPlugin.Entry"));

    /// <param name="tabName">Der Name des zu aktivierenden Tabs.</param>
    /// <returns>Diese Instanz.</returns>
    public SettingsView SwitchTab(string tabName)
    {
        WaitForElement(Window, cf => cf.ByName(tabName), Short).ClickInForeground();
        return this;
    }

    /// <summary>Öffnet über den "Konsolentestfenster öffnen"-Button (Tab "Allgemein") das
    /// Konsolentestfenster und gibt dessen Dialog-View zurück.</summary>
    /// <returns>Der Konsolentest-Dialog.</returns>
    public Dialogs.ConsoleTestDialogView OpenConsoleTestDialog()
    {
        SwitchTab("Allgemein");
        WaitForElement(Window, cf => cf.ByName("OpenConsoleTest"), Short).AsButton().ClickInForeground();
        return new Dialogs.ConsoleTestDialogView(Window).ForceShow();
    }

    /// <summary>Klickt den "Speichern"-Button und wartet auf die Bestätigung "Settings gespeichert.".</summary>
    /// <returns>Diese Instanz.</returns>
    public SettingsView SaveSettings()
    {
        WaitForElement(Window, cf => cf.ByName("Save"), Short).AsButton().ClickInForeground();
        WaitForElement(Window, cf => cf.ByName("Settings gespeichert."), Short);

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
        var localDirectoryPluginEntry = WaitForElement(Window, cf => cf.ByName("LocalDirectoryPlugin.Entry"), Short);
        localDirectoryPluginEntry.ClickInForeground();

        var workspaceModeBox = WaitForElement(Window, cf => cf.ByName("WorkspaceMode"), Short);
        var workspaceMode = useInSourceDirectoryMode ? "InSourceDirectory" : "SeparateWorkingDirectory";
        SelectComboBoxItemByClick(workspaceModeBox, workspaceMode, Short);

        var sourceDirectoryBox = WaitForElement(Window, cf => cf.ByName("SourceDirectory"), Short);
        sourceDirectoryBox.AsTextBox().Text = sourceDirectory;

        SaveSettings();

        return Menu.NavigateToDashboard();
    }

    /// <summary>Wählt im "Plugins"-Tab das angegebene KI-Plugin als Standard-Plugin (DefaultAiPlugin) aus.</summary>
    /// <param name="pluginDisplayName">Der Anzeigename des Plugins (z. B. "Codex CLI").</param>
    /// <returns>Diese Instanz.</returns>
    /// <remarks>
    /// Ist der Eintrag bereits selektiert (z. B. weil er als persistierter Standard beim Öffnen
    /// automatisch gesetzt wurde), löst ein erneuter Klick kein SelectionChanged aus - das
    /// Plugin-Einstellungspanel würde nicht geladen. In dem Fall wird vorab ein anderes Plugin
    /// gewählt, um die Selektion anschließend erneut auslösen zu können.
    /// </remarks>
    public SettingsView SelectDefaultAiPlugin(string pluginDisplayName)
    {
        SwitchTab("Plugins");

        var aiPluginBox = WaitForElement(Window, cf => cf.ByName("DefaultAiPlugin"), Short);
        var comboBox = aiPluginBox.AsComboBox();

        if (string.Equals(comboBox.SelectedItem?.Name, pluginDisplayName, StringComparison.Ordinal))
        {
            var otherItemName = comboBox.Items
                .Select(item => item.Name)
                .FirstOrDefault(name => !string.Equals(name, pluginDisplayName, StringComparison.Ordinal));

            if (otherItemName is not null)
                SelectComboBoxItemByClick(aiPluginBox, otherItemName, Short);
        }

        SelectComboBoxItemByClick(aiPluginBox, pluginDisplayName, Short);

        return this;
    }

    /// <returns>Der aktuelle Wert des "CommandLineParameters"-Felds.</returns>
    public string GetCommandLineParameter() => WaitForElement(Window, cf => cf.ByName("CommandLineParameters"), Short).AsTextBox().Text;

    /// <param name="value">Der zu setzende Wert.</param>
    /// <returns>Diese Instanz.</returns>
    public SettingsView SetCommandLineParameter(string value)
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
        WaitForElement(Window, cf => cf.ByName("CliHelpButton"), Short).AsButton().ClickInForeground();

        var dialog = new HelpTextDialogView(Window);
        dialog.ForceShow();
        return dialog;
    }

    /// <summary>Wählt den Listeneintrag des IDE-Plugins aus ("{pluginPrefix}.Entry") und liest den Status der "IdePluginEnabled"-Checkbox.</summary>
    /// <param name="pluginPrefix">Der Plugin-Prefix, z. B. "Softwareschmiede.VisualStudioCode".</param>
    /// <returns><c>true</c>, wenn das Plugin aktuell aktiviert ist.</returns>
    public bool IsIdePluginEnabled(string pluginPrefix)
    {
        WaitForElement(Window, cf => cf.ByName($"{pluginPrefix}.Entry"), Short).ClickInForeground();
        return WaitForElement(Window, cf => cf.ByName("IdePluginEnabled"), Short).AsCheckBox().IsChecked ?? false;
    }

    /// <summary>Wählt den Listeneintrag des IDE-Plugins aus und setzt seinen Aktivierungsstatus über die Checkbox "IdePluginEnabled".</summary>
    /// <param name="pluginPrefix">Der Plugin-Prefix, z. B. "Softwareschmiede.VisualStudioCode".</param>
    /// <param name="enabled">Der gewünschte Aktivierungsstatus.</param>
    /// <returns>Diese Instanz.</returns>
    public SettingsView SetIdePluginEnabled(string pluginPrefix, bool enabled)
    {
        WaitForElement(Window, cf => cf.ByName($"{pluginPrefix}.Entry"), Short).ClickInForeground();
        WaitForElement(Window, cf => cf.ByName("IdePluginEnabled"), Short).AsCheckBox().IsChecked = enabled;
        return this;
    }

    /// <summary>Klickt den "Nach oben"-Button des angegebenen IDE-Plugin-Eintrags in der "IdePluginList".</summary>
    /// <param name="pluginPrefix">Der Plugin-Prefix, z. B. "Softwareschmiede.VisualStudioCode".</param>
    /// <returns>Diese Instanz.</returns>
    public SettingsView MoveIdePluginUp(string pluginPrefix)
    {
        WaitForElement(Window, cf => cf.ByName($"{pluginPrefix}.MoveUp"), Short).AsButton().ClickInForeground();
        return this;
    }

    /// <summary>Prüft, ob der angegebene IDE-Plugin-Eintrag aktuell an erster Stelle der "IdePluginList" steht.</summary>
    /// <param name="pluginPrefix">Der Plugin-Prefix, z. B. "Softwareschmiede.VisualStudioCode".</param>
    /// <returns><c>true</c>, wenn der Eintrag an erster Stelle steht.</returns>
    public bool IsFirstIdePlugin(string pluginPrefix)
    {
        var list = WaitForElement(Window, cf => cf.ByName("IdePluginList"), Short);
        var items = list.FindAllChildren(cf => cf.ByControlType(ControlType.ListItem));
        if (items.Length < 2)
            return false;

        return items[0].FindFirstDescendant(cf => cf.ByName($"{pluginPrefix}.Entry")) is not null;
    }

    /// <summary>Wählt den Listeneintrag eines SCM-/KI-Plugins aus ("{pluginPrefix}.Entry") und liest den Status der "PluginEnabled"-Checkbox.</summary>
    /// <param name="pluginPrefix">Der Plugin-Prefix, z. B. "Softwareschmiede.ClaudeCli".</param>
    /// <returns><c>true</c>, wenn das Plugin aktuell aktiviert ist.</returns>
    public bool IsPluginEnabled(string pluginPrefix)
    {
        WaitForElement(Window, cf => cf.ByName($"{pluginPrefix}.Entry"), Short).ClickInForeground();
        return WaitForElement(Window, cf => cf.ByName("PluginEnabled"), Short).AsCheckBox().IsChecked ?? false;
    }

    /// <summary>Wählt den Listeneintrag eines SCM-/KI-Plugins aus und setzt seinen Aktivierungsstatus über die Checkbox "PluginEnabled".</summary>
    /// <param name="pluginPrefix">Der Plugin-Prefix, z. B. "Softwareschmiede.ClaudeCli".</param>
    /// <param name="enabled">Der gewünschte Aktivierungsstatus.</param>
    /// <returns>Diese Instanz.</returns>
    public SettingsView SetPluginEnabled(string pluginPrefix, bool enabled)
    {
        WaitForElement(Window, cf => cf.ByName($"{pluginPrefix}.Entry"), Short).ClickInForeground();
        WaitForElement(Window, cf => cf.ByName("PluginEnabled"), Short).AsCheckBox().IsChecked = enabled;
        return this;
    }

    /// <returns>Der aktuelle Wert des globalen Felds „Maximale parallele Ausführungen“
    /// (Expander „Aufgaben ausführen“ im „Automatisierung“-Tab).</returns>
    public string GetMaxParallelExecutions()
        => WaitForElement(Window, cf => cf.ByName("MaxParallelExecutions"), Short).AsTextBox().Text;

    /// <summary>Setzt den Wert des globalen Felds „Maximale parallele Ausführungen“.</summary>
    /// <param name="value">Der zu setzende Wert (leer = unbegrenzt).</param>
    /// <returns>Diese Instanz.</returns>
    public SettingsView SetMaxParallelExecutions(string value)
    {
        WaitForElement(Window, cf => cf.ByName("MaxParallelExecutions"), Short).AsTextBox().Text = value;
        return this;
    }

    /// <summary>Wählt im „Plugins“-Tab den Listeneintrag eines SCM-/KI-Plugins aus
    /// („{pluginPrefix}.Entry“), damit dessen Einstellungspanel geladen wird.</summary>
    /// <param name="pluginPrefix">Der Plugin-Prefix, z. B. „Softwareschmiede.ClaudeCli“.</param>
    /// <returns>Diese Instanz.</returns>
    public SettingsView SelectPluginEntry(string pluginPrefix)
    {
        WaitForElement(Window, cf => cf.ByName($"{pluginPrefix}.Entry"), Short).ClickInForeground();
        return this;
    }

    /// <returns>Der aktuelle Wert des hostseitigen „ParallelLimit“-Felds im geöffneten
    /// KI-Plugin-Einstellungspanel (Gruppe „Ausführungsbegrenzung“).</returns>
    public string GetPluginParallelLimit()
        => WaitForElement(Window, cf => cf.ByName("ParallelLimit"), Short).AsTextBox().Text;

    /// <summary>Setzt den Wert des hostseitigen „ParallelLimit“-Felds im geöffneten
    /// KI-Plugin-Einstellungspanel (leer = unbegrenzt).</summary>
    /// <param name="value">Der zu setzende Wert.</param>
    /// <returns>Diese Instanz.</returns>
    public SettingsView SetPluginParallelLimit(string value)
    {
        WaitForElement(Window, cf => cf.ByName("ParallelLimit"), Short).AsTextBox().Text = value;
        return this;
    }

    /// <summary>Klickt „Speichern“ und wartet auf das Fehlerbanner (für Validierungs-Negativpfade).</summary>
    /// <returns>Die angezeigte Fehlermeldung (HelpText des ErrorMessage-Elements).</returns>
    public string SaveSettingsExpectError()
    {
        WaitForElement(Window, cf => cf.ByName("Save"), Short).AsButton().ClickInForeground();
        return GetHelpTextOrName(WaitForElement(Window, cf => cf.ByName("ErrorMessage"), Short));
    }

    /// <summary>Klickt den "Verwerfen"-Button und wartet, bis ein zuvor angezeigtes Fehlerbanner verschwindet.</summary>
    /// <returns>Diese Instanz.</returns>
    public SettingsView DiscardChanges()
    {
        WaitForElement(Window, cf => cf.ByName("Discard"), Short).AsButton().ClickInForeground();
        WaitUntilGone(Window, cf => cf.ByName("ErrorMessage"), Short);
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

    /// <summary>Wechselt in den "Automatisierung"-Tab und klappt den Expander
    /// "Aufgaben ausführen" auf, damit seine Steuerelemente in der UIA-Struktur existieren.</summary>
    /// <returns>Diese Instanz.</returns>
    public SettingsView ExecuteOpenWorkItems() => OpenExpander("WorkItemsExecuteExpander");

    /// <summary>Wechselt in den "Automatisierung"-Tab und klappt den Expander
    /// "Aufgaben beenden" auf, damit seine Steuerelemente in der UIA-Struktur existieren.</summary>
    /// <returns>Diese Instanz.</returns>
    public SettingsView EndOpenWorkItems() => OpenExpander("WorkItemsExitExpander");

    /// <summary>Wechselt in den "Automatisierung"-Tab und klappt den Expander
    /// "Prozessabläufe" auf, damit seine Steuerelemente in der UIA-Struktur existieren.</summary>
    /// <returns>Diese Instanz.</returns>
    public SettingsView OpenProcessflows() => OpenExpander("ProcessRunsExpander");

    /// <summary>Wechselt in den "Automatisierung"-Tab und klappt den Expander
    /// "Issue-Aufbereitung" auf, damit seine Steuerelemente in der UIA-Struktur existieren.</summary>
    /// <returns>Diese Instanz.</returns>
    public SettingsView OpenIssuePreparation() => OpenExpander("IssuePreparationExpander");

    /// <returns>Der Status der Funktion „Issue-Aufbereitung“ (Header-Statustext des Expanders).</returns>
    public string GetIssuePreparationStatus()
        => GetHelpTextOrName(WaitForElement(Window, cf => cf.ByName("IssuePreparationStatus"), Short));

    /// <returns>Der Zustand des Funktionsschalters der Issue-Aufbereitung.</returns>
    public bool IsIssuePreparationEnabled()
        => WaitForElement(Window, cf => cf.ByName("IssuePreparationEnabledCheckBox"), Short).AsCheckBox().IsChecked ?? false;

    /// <summary>Setzt den Funktionsschalter der Issue-Aufbereitung.</summary>
    /// <param name="enabled">Der neue Zustand.</param>
    /// <returns>Diese Instanz.</returns>
    public SettingsView SetIssuePreparationEnabled(bool enabled)
    {
        WaitForElement(Window, cf => cf.ByName("IssuePreparationEnabledCheckBox"), Short).AsCheckBox().IsChecked = enabled;
        return this;
    }

    /// <returns>Der aktuell in der Aufbereitungs-Plugin-ComboBox angezeigte Eintrag (Label).</returns>
    public string GetIssuePreparationPlugin()
        => WaitForElement(Window, cf => cf.ByName("IssuePreparationPluginComboBox"), Short).AsComboBox().SelectedItem?.Name ?? string.Empty;

    /// <summary>Wählt einen Eintrag der Aufbereitungs-Plugin-ComboBox anhand seines Labels aus.</summary>
    /// <param name="label">Das sichtbare Label der Option (z. B. „Globale Standard-CLI verwenden“).</param>
    /// <returns>Diese Instanz.</returns>
    public SettingsView SetIssuePreparationPlugin(string label)
    {
        var box = WaitForElement(Window, cf => cf.ByName("IssuePreparationPluginComboBox"), Short);
        SelectComboBoxItemByClick(box, label, Short);
        ElementWaitHelper.WaitForSelectedComboBoxItem(box, label, Short);
        return this;
    }

    /// <returns>Die sichtbaren Labels aller Aufbereitungs-Plugin-Optionen.</returns>
    public string[] GetIssuePreparationPluginOptions()
    {
        var box = WaitForElement(Window, cf => cf.ByName("IssuePreparationPluginComboBox"), Short).AsComboBox();
        box.Expand();
        // Die Items werden erst beim Öffnen des Dropdowns materialisiert.
        var deadline = DateTime.UtcNow + Short;
        string[] labels;
        while ((labels = box.Items.Select(x => x.Text).ToArray()).Length == 0 && DateTime.UtcNow < deadline)
            Thread.Sleep(50);
        box.Collapse();
        return labels;
    }

    /// <summary>Setzt die Auswahl-Checkbox eines Repositorys in der Aufbereitungs-Liste
    /// anhand seines Anzeigenamens.</summary>
    /// <param name="repositoryName">Der Repository-Anzeigename.</param>
    /// <param name="selected">Der neue Zustand.</param>
    /// <returns>Diese Instanz.</returns>
    public SettingsView SetIssuePreparationRepository(string repositoryName, bool selected)
    {
        WaitForElement(Window, cf => cf.ByName("IssuePreparationRepositoriesList"), Short);
        WaitForElement(Window, cf => cf.ByName(repositoryName), Short).AsCheckBox().IsChecked = selected;
        return this;
    }

    /// <returns>Der Auswahlzustand eines Repositorys in der Aufbereitungs-Liste.</returns>
    /// <param name="repositoryName">Der Repository-Anzeigename.</param>
    public bool IsIssuePreparationRepositorySelected(string repositoryName)
        => WaitForElement(Window, cf => cf.ByName(repositoryName), Short).AsCheckBox().IsChecked ?? false;

    /// <returns>Der Zustand des Kommentar-Schalters der Issue-Aufbereitung.</returns>
    public bool IsIssuePreparationCommentEnabled()
        => WaitForElement(Window, cf => cf.ByName("IssuePreparationCommentCheckBox"), Short).AsCheckBox().IsChecked ?? false;

    /// <summary>Setzt den Kommentar-Schalter der Issue-Aufbereitung.</summary>
    /// <param name="enabled">Der neue Zustand.</param>
    /// <returns>Diese Instanz.</returns>
    public SettingsView SetIssuePreparationCommentEnabled(bool enabled)
    {
        WaitForElement(Window, cf => cf.ByName("IssuePreparationCommentCheckBox"), Short).AsCheckBox().IsChecked = enabled;
        return this;
    }

    /// <summary>Klappt den Expander "Aufgaben ausführen" zu (no-op, wenn er bereits zu ist).</summary>
    /// <returns>Diese Instanz.</returns>
    public SettingsView ExecuteCollapseWorkItems() => CollapseExpander("WorkItemsExecuteExpander");

    /// <summary>Klappt den Expander "Aufgaben beenden" zu (no-op, wenn er bereits zu ist).</summary>
    /// <returns>Diese Instanz.</returns>
    public SettingsView EndCollapseWorkItems() => CollapseExpander("WorkItemsExitExpander");

    /// <summary>Klappt den Expander "Prozessabläufe" zu (no-op, wenn er bereits zu ist).</summary>
    /// <returns>Diese Instanz.</returns>
    public SettingsView CollapseProcessflows() => CollapseExpander("ProcessRunsExpander");

    /// <summary>Wechselt in den "Automatisierung"-Tab und klappt den Expander
    /// "Remote-Übersicht" auf, damit seine Steuerelemente in der UIA-Struktur existieren.</summary>
    /// <returns>Diese Instanz.</returns>
    public SettingsView OpenRemoteStatus() => OpenExpander("RemoteStatusExpander");

    /// <summary>Klappt den Expander "Remote-Übersicht" zu (no-op, wenn er bereits zu ist).</summary>
    /// <returns>Diese Instanz.</returns>
    public SettingsView CollapseRemoteStatus() => CollapseExpander("RemoteStatusExpander");

    /// <summary>Liest den Schaltzustand der Titelleiste "Remote-Übersicht" — auch im
    /// zugeklappten Zustand, da der Status-TextBlock im Expander-Header liegt.</summary>
    /// <returns>Der Status-Text ("aktiviert"/"deaktiviert").</returns>
    public string GetRemoteStatusStatus() => GetExpanderStatus("RemoteStatusStatus");

    /// <returns>Der Zustand des Remote-Übersicht-Funktionsschalters.</returns>
    public bool IsRemoteStatusEnabledChecked()
        => WaitForElement(Window, cf => cf.ByName("IsRemoteStatusEnabled"), Short).AsCheckBox().IsChecked ?? false;

    /// <summary>Setzt den Funktionsschalter der Remote-Übersicht.</summary>
    /// <param name="enabled">Der neue Zustand.</param>
    /// <returns>Diese Instanz.</returns>
    public SettingsView SetRemoteStatusEnabledChecked(bool enabled)
    {
        WaitForElement(Window, cf => cf.ByName("IsRemoteStatusEnabled"), Short).AsCheckBox().IsChecked = enabled;
        return this;
    }

    /// <returns><c>true</c>, wenn die editierbaren Felder der Remote-Übersicht
    /// (Server-URL stellvertretend) bedienbar sind — deaktiviert bleiben sie gesperrt
    /// statt ausgeblendet (Instanz-ID und Verbindungsstatus sind immer sichtbar).</returns>
    public bool IsRemoteStatusUrlEnabled()
        => WaitForElement(Window, cf => cf.ByName("RemoteStatusUrl"), Short).IsEnabled;

    /// <returns><c>true</c>, wenn der Hinweistext „Felder gesperrt" der deaktivierten
    /// Remote-Übersicht sichtbar ist (ausgeblendet, sobald der Schalter an ist).</returns>
    public bool IsRemoteStatusDisabledHintVisible() => IsAreaVisible("RemoteStatusDisabledHint");

    /// <returns><c>true</c>, wenn der „Token bereits hinterlegt"-Indikator neben der
    /// PasswordBox sichtbar ist.</returns>
    public bool IsRemoteStatusTokenStoredHintVisible() => IsAreaVisible("RemoteStatusTokenStoredHint");

    /// <returns>Der aktuelle Wert des Server-URL-Felds.</returns>
    public string GetRemoteStatusUrl()
        => WaitForElement(Window, cf => cf.ByName("RemoteStatusUrl"), Short).AsTextBox().Text;

    /// <summary>Setzt den Wert des Server-URL-Felds.</summary>
    /// <param name="value">Die Basis-Adresse des Status-Servers.</param>
    /// <returns>Diese Instanz.</returns>
    public SettingsView SetRemoteStatusUrl(string value)
    {
        WaitForElement(Window, cf => cf.ByName("RemoteStatusUrl"), Short).AsTextBox().Text = value;
        return this;
    }

    /// <returns>Der aktuelle Wert des Anzeigename-Felds.</returns>
    public string GetRemoteStatusInstanceName()
        => WaitForElement(Window, cf => cf.ByName("RemoteStatusInstanceName"), Short).AsTextBox().Text;

    /// <summary>Setzt den Wert des Anzeigename-Felds.</summary>
    /// <param name="value">Der Anzeigename der Instanz.</param>
    /// <returns>Diese Instanz.</returns>
    public SettingsView SetRemoteStatusInstanceName(string value)
    {
        WaitForElement(Window, cf => cf.ByName("RemoteStatusInstanceName"), Short).AsTextBox().Text = value;
        return this;
    }

    /// <returns>Die angezeigte (schreibgeschützte) Instanz-ID.</returns>
    public string GetRemoteStatusInstanceId()
        => WaitForElement(Window, cf => cf.ByName("RemoteStatusInstanceId"), Short).AsTextBox().Text;

    /// <returns>Der angezeigte Verbindungsstatus-Text.</returns>
    public string GetRemoteStatusConnectionText()
        => WaitForElement(Window, cf => cf.ByName("RemoteStatusConnectionText"), Short).HelpText;

    /// <summary>Klappt den benannten Expander im "Automatisierung"-Tab auf (no-op, wenn er
    /// bereits geöffnet ist) und wartet kurz auf den Aufbau des Inhalts.</summary>
    /// <param name="automationName">Der <c>AutomationProperties.Name</c> des Expanders.</param>
    /// <returns>Diese Instanz.</returns>
    private SettingsView OpenExpander(string automationName)
    {
        SwitchTab("Automatisierung");
        var expander = WaitForElement(Window, cf => cf.ByName(automationName), Short);
        if (expander.Patterns.ExpandCollapse.IsSupported
            && expander.Patterns.ExpandCollapse.Pattern.ExpandCollapseState.Value != ExpandCollapseState.Expanded)
            expander.Patterns.ExpandCollapse.Pattern.Expand();
        return this;
    }

    /// <summary>Klappt den benannten Expander im "Automatisierung"-Tab zu (no-op, wenn er
    /// bereits geschlossen ist).</summary>
    /// <param name="automationName">Der <c>AutomationProperties.Name</c> des Expanders.</param>
    /// <returns>Diese Instanz.</returns>
    private SettingsView CollapseExpander(string automationName)
    {
        SwitchTab("Automatisierung");
        var expander = WaitForElement(Window, cf => cf.ByName(automationName), Short);
        if (expander.Patterns.ExpandCollapse.IsSupported
            && expander.Patterns.ExpandCollapse.Pattern.ExpandCollapseState.Value == ExpandCollapseState.Expanded)
            expander.Patterns.ExpandCollapse.Pattern.Collapse();
        return this;
    }

    /// <summary>Liest den Schaltzustand der Titelleiste "Aufgaben ausführen" — auch im
    /// zugeklappten Zustand, da der Status-TextBlock im Expander-Header liegt.</summary>
    /// <returns>Der Status-Text ("aktiviert"/"deaktiviert").</returns>
    public string GetWorkItemsExecuteStatus() => GetExpanderStatus("WorkItemsExecuteStatus");

    /// <summary>Liest den Schaltzustand der Titelleiste "Aufgaben beenden" — auch im
    /// zugeklappten Zustand, da der Status-TextBlock im Expander-Header liegt.</summary>
    /// <returns>Der Status-Text ("aktiviert"/"deaktiviert").</returns>
    public string GetWorkItemsEndStatus() => GetExpanderStatus("WorkItemsExitStatus");

    /// <summary>Liest den Schaltzustand der Titelleiste "Prozessabläufe" — auch im
    /// zugeklappten Zustand, da der Status-TextBlock im Expander-Header liegt.</summary>
    /// <returns>Der Status-Text ("aktiviert"/"deaktiviert").</returns>
    public string GetProcessflowsStatus() => GetExpanderStatus("ProcessRunsStatus");

    /// <summary>Liest den Status-Text des benannten Expander-Status-TextBlocks über den
    /// HelpText (der UIA-Name trägt den Automatisierungsnamen, nicht den Anzeigetext).</summary>
    /// <param name="automationName">Der <c>AutomationProperties.Name</c> des Status-TextBlocks.</param>
    /// <returns>Der gebundene Status-Text.</returns>
    private string GetExpanderStatus(string automationName)
    {
        SwitchTab("Automatisierung");
        return WaitForElement(Window, cf => cf.ByName(automationName), Short).HelpText;
    }

    /// <summary>Prüft, ob ein Element innerhalb eines per <c>Visibility</c>-Binding gesteuerten
    /// Bereichs aktuell im UIA-Baum enthalten ist. Ein per <c>Visibility=Collapsed</c>
    /// ausgeblendeter Bereich entfernt seinen gesamten Teilbaum aus dem UIA-Baum; ein sichtbarer
    /// Bereich ist dagegen auffindbar, auch wenn er unterhalb des Scroll-Viewports liegt.
    /// <c>IsOffscreen</c> ist dafür kein geeignetes Kriterium: schlichte WPF-Container
    /// implementieren kein <c>ScrollItemPattern</c> und können nicht programmatisch in den
    /// sichtbaren Bereich gerollt werden. Der Bereichs-Container selbst (StackPanel) kann nicht
    /// als Prüfelement dienen — WPF-Panels besitzen keinen AutomationPeer und erscheinen nie im
    /// UIA-Baum; gesucht wird daher ein benanntes Steuerelement innerhalb des Bereichs.
    /// Gepollt wird auf einen stabilen Zustand, da UIA-Baum und Binding-Auswertung kurz nach
    /// einem Umschalten auseinanderlaufen können (zwei aufeinanderfolgende gleiche Abfragen
    /// gelten als stabil).</summary>
    /// <param name="automationName">Der <c>AutomationProperties.Name</c> eines Elements innerhalb des Bereichs.</param>
    /// <returns><c>true</c>, wenn das Element (und damit der Bereich) sichtbar ist.</returns>
    private bool IsAreaVisible(string automationName)
    {
        var existing = Window.FindFirstDescendant(cf => cf.ByName(automationName)) is not null;
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(2);
        while (DateTime.UtcNow < deadline)
        {
            Thread.Sleep(100);
            var current = Window.FindFirstDescendant(cf => cf.ByName(automationName)) is not null;
            if (current == existing)
                return current;
            existing = current;
        }
        return existing;
    }

    /// <summary>Liest, ob die Checkbox "Aufgaben automatisch starten" gesetzt ist.</summary>
    /// <returns><c>true</c>, wenn die Ereignisüberwachung aktiviert ist.</returns>
    public bool IsScmMonitoringEnabledChecked()
    {
        ExecuteOpenWorkItems();
        return WaitForElement(Window, cf => cf.ByName("IsScmMonitoringEnabled"), Short).AsCheckBox().IsChecked ?? false;
    }

    /// <summary>Setzt die Checkbox "Aufgaben automatisch starten".</summary>
    /// <param name="enabled">Der gewünschte Zustand.</param>
    /// <returns>Diese Instanz.</returns>
    public SettingsView SetScmMonitoringEnabledChecked(bool enabled)
    {
        ExecuteOpenWorkItems();
        WaitForElement(Window, cf => cf.ByName("IsScmMonitoringEnabled"), Short).AsCheckBox().IsChecked = enabled;
        return this;
    }

    /// <summary>Liest, ob die "MonitoringMode"-ComboBox bedienbar ist (nur bei aktivierter
    /// Ereignisüberwachung).</summary>
    /// <returns><c>true</c>, wenn die ComboBox aktiviert ist.</returns>
    public bool IsMonitoringModeEnabled()
    {
        ExecuteOpenWorkItems();
        return WaitForElement(Window, cf => cf.ByName("MonitoringMode").And(cf.ByControlType(ControlType.ComboBox)), Short).IsEnabled;
    }

    /// <summary>Prüft, ob der Trigger-Keyword-Bereich sichtbar ist (nur bei aktivierter
    /// Ereignisüberwachung im Benutzerkonto-Modus). Geprüft wird über das enthaltene
    /// "ScmTriggerKeyword"-Textfeld, da der Bereichs-StackPanel kein UIA-Element besitzt.</summary>
    /// <returns><c>true</c>, wenn der Bereich sichtbar ist.</returns>
    public bool IsTriggerKeywordAreaVisible()
    {
        ExecuteOpenWorkItems();
        return IsAreaVisible("ScmTriggerKeyword");
    }

    /// <summary>Prüft, ob der Verzögerungs-Keyword-Bereich sichtbar ist (bei aktivierter
    /// Ereignisüberwachung in beiden Kontomodi). Geprüft wird über das enthaltene
    /// "ScmDelayKeyword"-Textfeld, da der Bereichs-StackPanel kein UIA-Element besitzt.</summary>
    /// <returns><c>true</c>, wenn der Bereich sichtbar ist.</returns>
    public bool IsDelayKeywordAreaVisible()
    {
        ExecuteOpenWorkItems();
        return IsAreaVisible("ScmDelayKeyword");
    }

    /// <summary>Prüft, ob der Prompt-Anhänge-Bereich sichtbar ist (nur bei aktivierter
    /// Ereignisüberwachung). Geprüft wird über das enthaltene "ScmPromptAppendixGeneral"-Textfeld,
    /// da der Bereichs-StackPanel kein UIA-Element besitzt.</summary>
    /// <returns><c>true</c>, wenn der Bereich sichtbar ist.</returns>
    public bool IsPromptAppendAreaVisible()
    {
        ExecuteOpenWorkItems();
        return IsAreaVisible("ScmPromptAppendixGeneral");
    }

    /// <summary>Liest das aktuell ausgewählte Label der "MonitoringMode"-ComboBox
    /// im "Automatisierung"-Tab (Expander "Aufgaben ausführen").</summary>
    /// <returns>Das Anzeige-Label des gewählten SCM-Konto-Modus (siehe <c>ScmKontoModusTexte</c>).</returns>
    public string GetAccountMode()
    {
        ExecuteOpenWorkItems();
        return WaitForElement(Window, cf => cf.ByName("MonitoringMode").And(cf.ByControlType(ControlType.ComboBox)), Short).AsComboBox().SelectedItem?.Name ?? string.Empty;
    }

    /// <summary>Wählt einen Eintrag der "MonitoringMode"-ComboBox und wartet, bis er übernommen wurde.</summary>
    /// <param name="label">Das Anzeige-Label (aus <c>ScmKontoModusTexte</c>: "Benutzerkonto" oder "Separates Konto").</param>
    /// <returns>Diese Instanz.</returns>
    public SettingsView SetAccountMode(string label)
    {
        ExecuteOpenWorkItems();
        // Die ComboBox liegt weit unten im ScrollViewer: ExpandCollapse öffnet sie auch ohne
        // sichtbaren Klickpunkt (das UIA-Element einer WPF-ComboBox ist selbst nicht
        // tastaturfokussierbar). Die Einträge liegen im Popup-Teilbaum der ComboBox; die
        // Auswahl erfolgt patternbasiert, da auch Items keinen eigenen Klickpunkt brauchen.
        var box = WaitForElement(Window, cf => cf.ByName("MonitoringMode").And(cf.ByControlType(ControlType.ComboBox)), Short).AsComboBox();
        box.Expand();
        AutomationElement? item = null;
        var deadline = DateTime.UtcNow + Short;
        while (DateTime.UtcNow < deadline)
        {
            item = box.FindFirstDescendant(cf => cf.ByName(label))
                ?? ElementWaitHelper.FindInProcessWindows(Window, cf => cf.ByName(label));
            if (item is not null)
                break;
            Thread.Sleep(200);
        }
        Assert.NotNull(item);
        item.Patterns.SelectionItem.Pattern.Select();
        WaitForAccountMode(label, Short);
        return this;
    }

    /// <summary>
    /// Wartet, bis die "MonitoringMode"-ComboBox das erwartete Label anzeigt. Dient als echtes
    /// Synchronisationssignal für das Neuladen der persistierten Settings.
    /// </summary>
    /// <param name="label">Das erwartete Anzeige-Label.</param>
    /// <param name="timeout">Maximale Wartezeit.</param>
    /// <returns>Diese Instanz.</returns>
    /// <exception cref="TimeoutException">Das Label wurde nicht rechtzeitig angezeigt.</exception>
    public SettingsView WaitForAccountMode(string label, TimeSpan timeout)
    {
        ExecuteOpenWorkItems();
        ElementWaitHelper.WaitForSelectedComboBoxItem(
            WaitForElement(Window, cf => cf.ByName("MonitoringMode").And(cf.ByControlType(ControlType.ComboBox)), timeout), label, timeout);
        return this;
    }

    /// <summary>Liest, ob die Checkbox "Aufgaben automatisch beenden" gesetzt ist.</summary>
    /// <returns><c>true</c>, wenn die Abschlussüberwachung aktiviert ist.</returns>
    public bool IsScmTaskCompletionEnabledChecked()
    {
        EndOpenWorkItems();
        return WaitForElement(Window, cf => cf.ByName("IsScmTaskCompletionEnabled"), Short).AsCheckBox().IsChecked ?? false;
    }

    /// <summary>Setzt die Checkbox "Aufgaben automatisch beenden".</summary>
    /// <param name="enabled">Der gewünschte Zustand.</param>
    /// <returns>Diese Instanz.</returns>
    public SettingsView SetScmTaskCompletionEnabledChecked(bool enabled)
    {
        EndOpenWorkItems();
        WaitForElement(Window, cf => cf.ByName("IsScmTaskCompletionEnabled"), Short).AsCheckBox().IsChecked = enabled;
        return this;
    }

    /// <summary>Liest, ob die "CheckMode"-ComboBox bedienbar ist (nur bei aktivierter
    /// Abschlussüberwachung).</summary>
    /// <returns><c>true</c>, wenn die ComboBox aktiviert ist.</returns>
    public bool IsCheckmodeEnabled()
    {
        EndOpenWorkItems();
        return WaitForElement(Window, cf => cf.ByName("CheckMode").And(cf.ByControlType(ControlType.ComboBox)), Short).IsEnabled;
    }

    /// <summary>Liest, ob das Wartezeit-Feld "ScmTaskCompletionWaitMinutes" bedienbar ist
    /// (nur bei aktivierter Abschlussüberwachung).</summary>
    /// <returns><c>true</c>, wenn das Feld aktiviert ist.</returns>
    public bool IsScmTaskCompletionWaitEnabled()
    {
        EndOpenWorkItems();
        return WaitForElement(Window, cf => cf.ByName("ScmTaskCompletionWaitMinutes"), Short).IsEnabled;
    }

    /// <summary>Wählt einen Eintrag der "CheckMode"-ComboBox (Expander "Aufgaben beenden")
    /// und wartet, bis er übernommen wurde.</summary>
    /// <param name="label">Das Anzeige-Label (aus <c>ScmTaskCompletionPruefmodusTexte</c>).</param>
    /// <returns>Diese Instanz.</returns>
    public SettingsView SetCheckmode(string label)
    {
        EndOpenWorkItems();
        var box = WaitForElement(Window, cf => cf.ByName("CheckMode").And(cf.ByControlType(ControlType.ComboBox)), Short);
        SelectComboBoxItemByClick(box, label, Short);
        ElementWaitHelper.WaitForSelectedComboBoxItem(box, label, Short);
        return this;
    }

    /// <summary>Liest das aktuell ausgewählte Label der "CheckMode"-ComboBox.</summary>
    /// <returns>Das Anzeige-Label des gewählten Prüfmodus (siehe <c>ScmTaskCompletionPruefmodusTexte</c>).</returns>
    public string GetCheckmode()
    {
        EndOpenWorkItems();
        return WaitForElement(Window, cf => cf.ByName("CheckMode").And(cf.ByControlType(ControlType.ComboBox)), Short).AsComboBox().SelectedItem?.Name ?? string.Empty;
    }

    /// <summary>Wählt einen Eintrag der "Backmerge-Modus"-ComboBox (Expander "Prozessabläufe")
    /// und wartet, bis er übernommen wurde.</summary>
    /// <param name="label">Das Anzeige-Label (aus <c>ScmBackmergeModusTexte</c>).</param>
    /// <returns>Diese Instanz.</returns>
    public SettingsView SetBackmergeMode(string label)
    {
        OpenProcessflows();
        var box = WaitForElement(Window, cf => cf.ByName("Backmerge-Modus").And(cf.ByControlType(ControlType.ComboBox)), Short);
        SelectComboBoxItemByClick(box, label, Short);
        ElementWaitHelper.WaitForSelectedComboBoxItem(box, label, Short);
        return this;
    }

    /// <summary>Liest das aktuell ausgewählte Label der "Backmerge-Modus"-ComboBox.</summary>
    /// <returns>Das Anzeige-Label des gewählten Backmerge-Modus (siehe <c>ScmBackmergeModusTexte</c>).</returns>
    public string GetBackmergeMode()
    {
        OpenProcessflows();
        return WaitForElement(Window, cf => cf.ByName("Backmerge-Modus").And(cf.ByControlType(ControlType.ComboBox)), Short).AsComboBox().SelectedItem?.Name ?? string.Empty;
    }

    /// <summary>Setzt die Checkbox "Draft-Pull-Requests vor dem Merge automatisch aktivieren".</summary>
    /// <param name="enabled">Der gewünschte Zustand.</param>
    /// <returns>Diese Instanz.</returns>
    public SettingsView SetBackmergeDraftActivationChecked(bool enabled)
    {
        OpenProcessflows();
        WaitForElement(Window, cf => cf.ByName("IsScmBackmergeDraftActivation"), Short).AsCheckBox().IsChecked = enabled;
        return this;
    }

    /// <summary>Setzt die Checkbox "Bei blockiertem Merge Admin-Merge erlauben".</summary>
    /// <param name="enabled">Der gewünschte Zustand.</param>
    /// <returns>Diese Instanz.</returns>
    public SettingsView SetBackmergeAdminMergeChecked(bool enabled)
    {
        OpenProcessflows();
        WaitForElement(Window, cf => cf.ByName("IsScmBackmergeAdminMerge"), Short).AsCheckBox().IsChecked = enabled;
        return this;
    }

    /// <summary>Liest, ob die Checkbox "Draft-Pull-Requests vor dem Merge automatisch aktivieren" gesetzt ist.</summary>
    /// <returns><c>true</c>, wenn die Draft-Aktivierung gesetzt ist.</returns>
    public bool IsBackmergeDraftActivationChecked()
    {
        OpenProcessflows();
        return WaitForElement(Window, cf => cf.ByName("IsScmBackmergeDraftActivation"), Short).AsCheckBox().IsChecked ?? false;
    }

    /// <summary>Liest, ob die Checkbox "Bei blockiertem Merge Admin-Merge erlauben" gesetzt ist.</summary>
    /// <returns><c>true</c>, wenn der Admin-Merge gesetzt ist.</returns>
    public bool IsBackmergeAdminMergeChecked()
    {
        OpenProcessflows();
        return WaitForElement(Window, cf => cf.ByName("IsScmBackmergeAdminMerge"), Short).AsCheckBox().IsChecked ?? false;
    }

    /// <summary>Liest, ob die Checkbox "Draft-Pull-Requests vor dem Merge automatisch aktivieren"
    /// bedienbar ist (nur bei Backmerge-Modus ≠ „Deaktiviert").</summary>
    /// <returns><c>true</c>, wenn die Checkbox aktiviert ist.</returns>
    public bool IsBackmergeDraftActivationEnabled()
    {
        OpenProcessflows();
        return WaitForElement(Window, cf => cf.ByName("IsScmBackmergeDraftActivation"), Short).IsEnabled;
    }

    /// <summary>Liest, ob die Checkbox "Bei blockiertem Merge Admin-Merge erlauben" bedienbar
    /// ist (nur bei Backmerge-Modus ≠ „Deaktiviert").</summary>
    /// <returns><c>true</c>, wenn die Checkbox aktiviert ist.</returns>
    public bool IsBackmergeAdminMergeEnabled()
    {
        OpenProcessflows();
        return WaitForElement(Window, cf => cf.ByName("IsScmBackmergeAdminMerge"), Short).IsEnabled;
    }

    /// <summary>
    /// Setzt ein Secret-Pluginfeld (PasswordBox, <c>AutomationProperties.Name</c> = Feld-Key) per
    /// Tastatur. WPF-PasswordBoxen unterstützen über UI-Automation weder ValuePattern noch
    /// TextPattern; Fokus + Unicode-Tastatureingabe ist der einzig verlässliche Weg.
    /// </summary>
    /// <param name="fieldKey">Der Feld-Key aus dem Plugin--Einstellungen-Schema (z. B. "AgentToken").</param>
    /// <param name="value">Der einzugebende Wert.</param>
    /// <returns>Diese Instanz.</returns>
    public SettingsView SetPluginSecretField(string fieldKey, string value)
    {
        var field = WaitForElement(Window, cf => cf.ByName(fieldKey), Short);
        // Fokus scrollt das Feld in den sichtbaren Bereich und setzt den Tastaturfokus —
        // ein vorgängiger Mausklick würde an einem außerhalb liegenden Element fehlschlagen.
        field.Focus();
        Assert.True(field.Properties.HasKeyboardFocus.Value,
            $"Secret-Feld '{fieldKey}' hat vor der Eingabe keinen Tastaturfokus.");
        foreach (var rune in value.EnumerateRunes())
        {
            var runeText = rune.ToString();
            var input = runeText.SelectMany(character => new[]
            {
                INPUT.KeyboardInput(new KEYBDINPUT { wScan = character, dwFlags = KeyEventFlags.KEYEVENTF_UNICODE }),
                INPUT.KeyboardInput(new KEYBDINPUT { wScan = character, dwFlags = KeyEventFlags.KEYEVENTF_UNICODE | KeyEventFlags.KEYEVENTF_KEYUP })
            }).ToArray();
            Assert.Equal((uint)input.Length, User32.SendInput((uint)input.Length, input, INPUT.Size));
        }
        Keyboard.Type(VirtualKeyShort.TAB);
        var deadline = DateTime.UtcNow + Short;
        while (field.Properties.HasKeyboardFocus.Value && DateTime.UtcNow < deadline)
            Thread.Sleep(20);
        return this;
    }

    /// <summary>Liest das aktuell ausgewählte Label der "Update-Modus"-ComboBox im "Allgemein"-Tab.</summary>
    /// <returns>Das Anzeige-Label des gewählten Update-Modus (siehe <c>UpdateModusTexte</c>).</returns>
    public string GetUpdateMode()
        => WaitForElement(Window, cf => cf.ByName("Update-Mode"), Short).AsComboBox().SelectedItem?.Name ?? string.Empty;

    /// <summary>Wählt einen Eintrag der "Update-Modus"-ComboBox und wartet, bis er übernommen wurde.</summary>
    /// <param name="label">Das Anzeige-Label (aus <c>UpdateModusTexte</c>: "Aus", "Nur prüfen" oder "Bei Programmstart prüfen und ausführen").</param>
    /// <returns>Diese Instanz.</returns>
    public SettingsView SetUpdateMode(string label)
    {
        var box = WaitForElement(Window, cf => cf.ByName("Update-Mode"), Short);
        SelectComboBoxItemByClick(box, label, Short);
        WaitForUpdateMode(label, Short);
        return this;
    }

    /// <summary>
    /// Wartet, bis die "Update-Modus"-ComboBox das erwartete Label anzeigt. Dient als echtes
    /// Synchronisationssignal für das Neuladen der persistierten Settings.
    /// </summary>
    /// <param name="label">Das erwartete Anzeige-Label.</param>
    /// <param name="timeout">Maximale Wartezeit.</param>
    /// <returns>Diese Instanz.</returns>
    /// <exception cref="TimeoutException">Das Label wurde nicht rechtzeitig angezeigt.</exception>
    public SettingsView WaitForUpdateMode(string label, TimeSpan timeout)
    {
        ElementWaitHelper.WaitForSelectedComboBoxItem(
            WaitForElement(Window, cf => cf.ByName("Update-Mode"), timeout), label, timeout);
        return this;
    }

    /// <summary>Liest das aktuell ausgewählte Label der "Update-Pfad"-ComboBox im "Allgemein"-Tab.</summary>
    /// <returns>Das Anzeige-Label des gewählten Update-Pfads (siehe <c>UpdatePathText</c>).</returns>
    public string GetUpdatePath()
        => WaitForElement(Window, cf => cf.ByName("Update-Path").And(cf.ByControlType(ControlType.ComboBox)), Short).AsComboBox().SelectedItem?.Name ?? string.Empty;

    /// <summary>Wählt einen Eintrag der "Update-Pfad"-ComboBox und wartet, bis er übernommen wurde.</summary>
    /// <param name="label">Das Anzeige-Label (aus <c>UpdatePathText</c>: "Produktivversionen" oder "Vorabversionen").</param>
    /// <returns>Diese Instanz.</returns>
    public SettingsView SetUpdatePath(string label)
    {
        var box = WaitForElement(Window, cf => cf.ByName("Update-Path").And(cf.ByControlType(ControlType.ComboBox)), Short);
        SelectComboBoxItemByClick(box, label, Short);
        WaitForUpdatePath(label, Short);
        return this;
    }

    /// <summary>Wartet, bis die "Update-Pfad"-ComboBox das erwartete Label anzeigt.</summary>
    /// <param name="label">Das erwartete Anzeige-Label.</param>
    /// <param name="timeout">Maximale Wartezeit.</param>
    /// <returns>Diese Instanz.</returns>
    /// <exception cref="TimeoutException">Das Label wurde nicht rechtzeitig angezeigt.</exception>
    public SettingsView WaitForUpdatePath(string label, TimeSpan timeout)
    {
        ElementWaitHelper.WaitForSelectedComboBoxItem(
            WaitForElement(Window, cf => cf.ByName("Update-Path").And(cf.ByControlType(ControlType.ComboBox)), timeout), label, timeout);
        return this;
    }

    /// <summary>Liest den schreibgeschützten Anzeigetext des abgeleiteten UpdateChannels im "Allgemein"-Tab.</summary>
    /// <returns>Der Kanal-Anzeigetext (siehe <c>UpdatePathText</c>).</returns>
    /// <remarks>
    /// Das Element trägt kein <c>AutomationProperties.Name</c> — der gebundene Kanaltext ist
    /// daher als UIA-Name des TextBlocks lesbar (Muster wie <c>UpdateNotice</c>).
    /// </remarks>
    public string GetDerivedUpdateChannel()
        => WaitForElement(Window, cf => cf.ByAutomationId("UpdateChannel"), Short).Name;

    /// <summary>Wartet auf die Speicher-Bestätigung "Settings gespeichert.".</summary>
    /// <returns>Diese Instanz.</returns>
    /// <exception cref="TimeoutException">Die Bestätigung erschien nicht rechtzeitig.</exception>
    public SettingsView WaitForSettingsSaved()
    {
        WaitForElement(Window, cf => cf.ByName("Settings gespeichert."), Medium);
        return this;
    }
}
