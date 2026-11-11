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

    /// <summary>Öffnet über den "Konsolentestfenster öffnen"-Button (Tab "Allgemein") das
    /// Konsolentestfenster und gibt dessen Dialog-View zurück.</summary>
    /// <returns>Der Konsolentest-Dialog.</returns>
    public Dialogs.KonsolenTestDialogView OpenKonsolenTestDialog()
    {
        SwitchTab("Allgemein");
        WaitForElement(Window, cf => cf.ByName("KonsolenTestOeffnen"), Short).AsButton().ClickInForeground();
        return new Dialogs.KonsolenTestDialogView(Window).ForceShow();
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

    /// <summary>Wechselt in den "Automatisierung"-Tab und klappt den Expander
    /// "Aufgaben ausführen" auf, damit seine Steuerelemente in der UIA-Struktur existieren.</summary>
    /// <returns>Diese Instanz.</returns>
    public SettingsView OpenAufgabenAusfuehren() => OpenExpander("AufgabenAusfuehrenExpander");

    /// <summary>Wechselt in den "Automatisierung"-Tab und klappt den Expander
    /// "Aufgaben beenden" auf, damit seine Steuerelemente in der UIA-Struktur existieren.</summary>
    /// <returns>Diese Instanz.</returns>
    public SettingsView OpenAufgabenBeenden() => OpenExpander("AufgabenBeendenExpander");

    /// <summary>Wechselt in den "Automatisierung"-Tab und klappt den Expander
    /// "Prozessabläufe" auf, damit seine Steuerelemente in der UIA-Struktur existieren.</summary>
    /// <returns>Diese Instanz.</returns>
    public SettingsView OpenProzessablaeufe() => OpenExpander("ProzessablaeufeExpander");

    /// <summary>Klappt den Expander "Aufgaben ausführen" zu (no-op, wenn er bereits zu ist).</summary>
    /// <returns>Diese Instanz.</returns>
    public SettingsView CollapseAufgabenAusfuehren() => CollapseExpander("AufgabenAusfuehrenExpander");

    /// <summary>Klappt den Expander "Aufgaben beenden" zu (no-op, wenn er bereits zu ist).</summary>
    /// <returns>Diese Instanz.</returns>
    public SettingsView CollapseAufgabenBeenden() => CollapseExpander("AufgabenBeendenExpander");

    /// <summary>Klappt den Expander "Prozessabläufe" zu (no-op, wenn er bereits zu ist).</summary>
    /// <returns>Diese Instanz.</returns>
    public SettingsView CollapseProzessablaeufe() => CollapseExpander("ProzessablaeufeExpander");

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
    public string GetAufgabenAusfuehrenStatus() => GetExpanderStatus("AufgabenAusfuehrenStatus");

    /// <summary>Liest den Schaltzustand der Titelleiste "Aufgaben beenden" — auch im
    /// zugeklappten Zustand, da der Status-TextBlock im Expander-Header liegt.</summary>
    /// <returns>Der Status-Text ("aktiviert"/"deaktiviert").</returns>
    public string GetAufgabenBeendenStatus() => GetExpanderStatus("AufgabenBeendenStatus");

    /// <summary>Liest den Schaltzustand der Titelleiste "Prozessabläufe" — auch im
    /// zugeklappten Zustand, da der Status-TextBlock im Expander-Header liegt.</summary>
    /// <returns>Der Status-Text ("aktiviert"/"deaktiviert").</returns>
    public string GetProzessablaeufeStatus() => GetExpanderStatus("ProzessablaeufeStatus");

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
    private bool IsBereichSichtbar(string automationName)
    {
        var vorhanden = Window.FindFirstDescendant(cf => cf.ByName(automationName)) is not null;
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(2);
        while (DateTime.UtcNow < deadline)
        {
            Thread.Sleep(100);
            var aktuell = Window.FindFirstDescendant(cf => cf.ByName(automationName)) is not null;
            if (aktuell == vorhanden)
                return aktuell;
            vorhanden = aktuell;
        }
        return vorhanden;
    }

    /// <summary>Liest, ob die Checkbox "Aufgaben automatisch starten" gesetzt ist.</summary>
    /// <returns><c>true</c>, wenn die Ereignisüberwachung aktiviert ist.</returns>
    public bool IsScmMonitoringEnabledChecked()
    {
        OpenAufgabenAusfuehren();
        return WaitForElement(Window, cf => cf.ByName("IsScmMonitoringEnabled"), Short).AsCheckBox().IsChecked ?? false;
    }

    /// <summary>Setzt die Checkbox "Aufgaben automatisch starten".</summary>
    /// <param name="enabled">Der gewünschte Zustand.</param>
    /// <returns>Diese Instanz.</returns>
    public SettingsView SetScmMonitoringEnabledChecked(bool enabled)
    {
        OpenAufgabenAusfuehren();
        WaitForElement(Window, cf => cf.ByName("IsScmMonitoringEnabled"), Short).AsCheckBox().IsChecked = enabled;
        return this;
    }

    /// <summary>Liest, ob die "Ueberwachungsmodus"-ComboBox bedienbar ist (nur bei aktivierter
    /// Ereignisüberwachung).</summary>
    /// <returns><c>true</c>, wenn die ComboBox aktiviert ist.</returns>
    public bool IsUeberwachungsmodusEnabled()
    {
        OpenAufgabenAusfuehren();
        return WaitForElement(Window, cf => cf.ByName("Ueberwachungsmodus").And(cf.ByControlType(ControlType.ComboBox)), Short).IsEnabled;
    }

    /// <summary>Prüft, ob der Trigger-Keyword-Bereich sichtbar ist (nur bei aktivierter
    /// Ereignisüberwachung im Benutzerkonto-Modus). Geprüft wird über das enthaltene
    /// "ScmTriggerKeyword"-Textfeld, da der Bereichs-StackPanel kein UIA-Element besitzt.</summary>
    /// <returns><c>true</c>, wenn der Bereich sichtbar ist.</returns>
    public bool IsTriggerKeywordBereichVisible()
    {
        OpenAufgabenAusfuehren();
        return IsBereichSichtbar("ScmTriggerKeyword");
    }

    /// <summary>Prüft, ob der Prompt-Anhänge-Bereich sichtbar ist (nur bei aktivierter
    /// Ereignisüberwachung). Geprüft wird über das enthaltene "ScmPromptAppendixGeneral"-Textfeld,
    /// da der Bereichs-StackPanel kein UIA-Element besitzt.</summary>
    /// <returns><c>true</c>, wenn der Bereich sichtbar ist.</returns>
    public bool IsPromptAnhaengeBereichVisible()
    {
        OpenAufgabenAusfuehren();
        return IsBereichSichtbar("ScmPromptAppendixGeneral");
    }

    /// <summary>Liest das aktuell ausgewählte Label der "Ueberwachungsmodus"-ComboBox
    /// im "Automatisierung"-Tab (Expander "Aufgaben ausführen").</summary>
    /// <returns>Das Anzeige-Label des gewählten SCM-Konto-Modus (siehe <c>ScmKontoModusTexte</c>).</returns>
    public string GetKontoModus()
    {
        OpenAufgabenAusfuehren();
        return WaitForElement(Window, cf => cf.ByName("Ueberwachungsmodus").And(cf.ByControlType(ControlType.ComboBox)), Short).AsComboBox().SelectedItem?.Name ?? string.Empty;
    }

    /// <summary>Wählt einen Eintrag der "Ueberwachungsmodus"-ComboBox und wartet, bis er übernommen wurde.</summary>
    /// <param name="label">Das Anzeige-Label (aus <c>ScmKontoModusTexte</c>: "Benutzerkonto" oder "Separates Konto").</param>
    /// <returns>Diese Instanz.</returns>
    public SettingsView SetKontoModus(string label)
    {
        OpenAufgabenAusfuehren();
        // Die ComboBox liegt weit unten im ScrollViewer: ExpandCollapse öffnet sie auch ohne
        // sichtbaren Klickpunkt (das UIA-Element einer WPF-ComboBox ist selbst nicht
        // tastaturfokussierbar). Die Einträge liegen im Popup-Teilbaum der ComboBox; die
        // Auswahl erfolgt patternbasiert, da auch Items keinen eigenen Klickpunkt brauchen.
        var box = WaitForElement(Window, cf => cf.ByName("Ueberwachungsmodus").And(cf.ByControlType(ControlType.ComboBox)), Short).AsComboBox();
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
        WaitForKontoModus(label, Short);
        return this;
    }

    /// <summary>
    /// Wartet, bis die "Ueberwachungsmodus"-ComboBox das erwartete Label anzeigt. Dient als echtes
    /// Synchronisationssignal für das Neuladen der persistierten Einstellungen.
    /// </summary>
    /// <param name="label">Das erwartete Anzeige-Label.</param>
    /// <param name="timeout">Maximale Wartezeit.</param>
    /// <returns>Diese Instanz.</returns>
    /// <exception cref="TimeoutException">Das Label wurde nicht rechtzeitig angezeigt.</exception>
    public SettingsView WaitForKontoModus(string label, TimeSpan timeout)
    {
        OpenAufgabenAusfuehren();
        ElementWaitHelper.WaitForSelectedComboBoxItem(
            WaitForElement(Window, cf => cf.ByName("Ueberwachungsmodus").And(cf.ByControlType(ControlType.ComboBox)), timeout), label, timeout);
        return this;
    }

    /// <summary>Liest, ob die Checkbox "Aufgaben automatisch beenden" gesetzt ist.</summary>
    /// <returns><c>true</c>, wenn die Abschlussüberwachung aktiviert ist.</returns>
    public bool IsScmTaskCompletionEnabledChecked()
    {
        OpenAufgabenBeenden();
        return WaitForElement(Window, cf => cf.ByName("IsScmTaskCompletionEnabled"), Short).AsCheckBox().IsChecked ?? false;
    }

    /// <summary>Setzt die Checkbox "Aufgaben automatisch beenden".</summary>
    /// <param name="enabled">Der gewünschte Zustand.</param>
    /// <returns>Diese Instanz.</returns>
    public SettingsView SetScmTaskCompletionEnabledChecked(bool enabled)
    {
        OpenAufgabenBeenden();
        WaitForElement(Window, cf => cf.ByName("IsScmTaskCompletionEnabled"), Short).AsCheckBox().IsChecked = enabled;
        return this;
    }

    /// <summary>Liest, ob die "Pruefmodus"-ComboBox bedienbar ist (nur bei aktivierter
    /// Abschlussüberwachung).</summary>
    /// <returns><c>true</c>, wenn die ComboBox aktiviert ist.</returns>
    public bool IsPruefmodusEnabled()
    {
        OpenAufgabenBeenden();
        return WaitForElement(Window, cf => cf.ByName("Pruefmodus").And(cf.ByControlType(ControlType.ComboBox)), Short).IsEnabled;
    }

    /// <summary>Liest, ob das Wartezeit-Feld "ScmTaskCompletionWaitMinutes" bedienbar ist
    /// (nur bei aktivierter Abschlussüberwachung).</summary>
    /// <returns><c>true</c>, wenn das Feld aktiviert ist.</returns>
    public bool IsScmTaskCompletionWaitEnabled()
    {
        OpenAufgabenBeenden();
        return WaitForElement(Window, cf => cf.ByName("ScmTaskCompletionWaitMinutes"), Short).IsEnabled;
    }

    /// <summary>Wählt einen Eintrag der "Pruefmodus"-ComboBox (Expander "Aufgaben beenden")
    /// und wartet, bis er übernommen wurde.</summary>
    /// <param name="label">Das Anzeige-Label (aus <c>ScmTaskCompletionPruefmodusTexte</c>).</param>
    /// <returns>Diese Instanz.</returns>
    public SettingsView SetPruefmodus(string label)
    {
        OpenAufgabenBeenden();
        var box = WaitForElement(Window, cf => cf.ByName("Pruefmodus").And(cf.ByControlType(ControlType.ComboBox)), Short);
        SelectComboBoxItemByClick(box, label, Short);
        ElementWaitHelper.WaitForSelectedComboBoxItem(box, label, Short);
        return this;
    }

    /// <summary>Liest das aktuell ausgewählte Label der "Pruefmodus"-ComboBox.</summary>
    /// <returns>Das Anzeige-Label des gewählten Prüfmodus (siehe <c>ScmTaskCompletionPruefmodusTexte</c>).</returns>
    public string GetPruefmodus()
    {
        OpenAufgabenBeenden();
        return WaitForElement(Window, cf => cf.ByName("Pruefmodus").And(cf.ByControlType(ControlType.ComboBox)), Short).AsComboBox().SelectedItem?.Name ?? string.Empty;
    }

    /// <summary>Wählt einen Eintrag der "Backmerge-Modus"-ComboBox (Expander "Prozessabläufe")
    /// und wartet, bis er übernommen wurde.</summary>
    /// <param name="label">Das Anzeige-Label (aus <c>ScmBackmergeModusTexte</c>).</param>
    /// <returns>Diese Instanz.</returns>
    public SettingsView SetBackmergeModus(string label)
    {
        OpenProzessablaeufe();
        var box = WaitForElement(Window, cf => cf.ByName("Backmerge-Modus").And(cf.ByControlType(ControlType.ComboBox)), Short);
        SelectComboBoxItemByClick(box, label, Short);
        ElementWaitHelper.WaitForSelectedComboBoxItem(box, label, Short);
        return this;
    }

    /// <summary>Liest das aktuell ausgewählte Label der "Backmerge-Modus"-ComboBox.</summary>
    /// <returns>Das Anzeige-Label des gewählten Backmerge-Modus (siehe <c>ScmBackmergeModusTexte</c>).</returns>
    public string GetBackmergeModus()
    {
        OpenProzessablaeufe();
        return WaitForElement(Window, cf => cf.ByName("Backmerge-Modus").And(cf.ByControlType(ControlType.ComboBox)), Short).AsComboBox().SelectedItem?.Name ?? string.Empty;
    }

    /// <summary>Setzt die Checkbox "Draft-Pull-Requests vor dem Merge automatisch aktivieren".</summary>
    /// <param name="enabled">Der gewünschte Zustand.</param>
    /// <returns>Diese Instanz.</returns>
    public SettingsView SetBackmergeDraftAktivierungChecked(bool enabled)
    {
        OpenProzessablaeufe();
        WaitForElement(Window, cf => cf.ByName("IsScmBackmergeDraftAktivierung"), Short).AsCheckBox().IsChecked = enabled;
        return this;
    }

    /// <summary>Setzt die Checkbox "Bei blockiertem Merge Admin-Merge erlauben".</summary>
    /// <param name="enabled">Der gewünschte Zustand.</param>
    /// <returns>Diese Instanz.</returns>
    public SettingsView SetBackmergeAdminMergeChecked(bool enabled)
    {
        OpenProzessablaeufe();
        WaitForElement(Window, cf => cf.ByName("IsScmBackmergeAdminMerge"), Short).AsCheckBox().IsChecked = enabled;
        return this;
    }

    /// <summary>Liest, ob die Checkbox "Draft-Pull-Requests vor dem Merge automatisch aktivieren" gesetzt ist.</summary>
    /// <returns><c>true</c>, wenn die Draft-Aktivierung gesetzt ist.</returns>
    public bool IsBackmergeDraftAktivierungChecked()
    {
        OpenProzessablaeufe();
        return WaitForElement(Window, cf => cf.ByName("IsScmBackmergeDraftAktivierung"), Short).AsCheckBox().IsChecked ?? false;
    }

    /// <summary>Liest, ob die Checkbox "Bei blockiertem Merge Admin-Merge erlauben" gesetzt ist.</summary>
    /// <returns><c>true</c>, wenn der Admin-Merge gesetzt ist.</returns>
    public bool IsBackmergeAdminMergeChecked()
    {
        OpenProzessablaeufe();
        return WaitForElement(Window, cf => cf.ByName("IsScmBackmergeAdminMerge"), Short).AsCheckBox().IsChecked ?? false;
    }

    /// <summary>Liest, ob die Checkbox "Draft-Pull-Requests vor dem Merge automatisch aktivieren"
    /// bedienbar ist (nur bei Backmerge-Modus ≠ „Deaktiviert").</summary>
    /// <returns><c>true</c>, wenn die Checkbox aktiviert ist.</returns>
    public bool IsBackmergeDraftAktivierungEnabled()
    {
        OpenProzessablaeufe();
        return WaitForElement(Window, cf => cf.ByName("IsScmBackmergeDraftAktivierung"), Short).IsEnabled;
    }

    /// <summary>Liest, ob die Checkbox "Bei blockiertem Merge Admin-Merge erlauben" bedienbar
    /// ist (nur bei Backmerge-Modus ≠ „Deaktiviert").</summary>
    /// <returns><c>true</c>, wenn die Checkbox aktiviert ist.</returns>
    public bool IsBackmergeAdminMergeEnabled()
    {
        OpenProzessablaeufe();
        return WaitForElement(Window, cf => cf.ByName("IsScmBackmergeAdminMerge"), Short).IsEnabled;
    }

    /// <summary>
    /// Setzt ein Secret-Pluginfeld (PasswordBox, <c>AutomationProperties.Name</c> = Feld-Key) per
    /// Tastatur. WPF-PasswordBoxen unterstützen über UI-Automation weder ValuePattern noch
    /// TextPattern; Fokus + Unicode-Tastatureingabe ist der einzig verlässliche Weg.
    /// </summary>
    /// <param name="fieldKey">Der Feld-Key aus dem Plugin-Settings-Schema (z. B. "AgentToken").</param>
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

    /// <summary>Liest das aktuell ausgewählte Label der "Update-Pfad"-ComboBox im "Allgemein"-Tab.</summary>
    /// <returns>Das Anzeige-Label des gewählten Update-Pfads (siehe <c>UpdatePfadTexte</c>).</returns>
    public string GetUpdatePfad()
        => WaitForElement(Window, cf => cf.ByName("Update-Pfad").And(cf.ByControlType(ControlType.ComboBox)), Short).AsComboBox().SelectedItem?.Name ?? string.Empty;

    /// <summary>Wählt einen Eintrag der "Update-Pfad"-ComboBox und wartet, bis er übernommen wurde.</summary>
    /// <param name="label">Das Anzeige-Label (aus <c>UpdatePfadTexte</c>: "Produktivversionen" oder "Vorabversionen").</param>
    /// <returns>Diese Instanz.</returns>
    public SettingsView SetUpdatePfad(string label)
    {
        var box = WaitForElement(Window, cf => cf.ByName("Update-Pfad").And(cf.ByControlType(ControlType.ComboBox)), Short);
        SelectComboBoxItemByClick(box, label, Short);
        WaitForUpdatePfad(label, Short);
        return this;
    }

    /// <summary>Wartet, bis die "Update-Pfad"-ComboBox das erwartete Label anzeigt.</summary>
    /// <param name="label">Das erwartete Anzeige-Label.</param>
    /// <param name="timeout">Maximale Wartezeit.</param>
    /// <returns>Diese Instanz.</returns>
    /// <exception cref="TimeoutException">Das Label wurde nicht rechtzeitig angezeigt.</exception>
    public SettingsView WaitForUpdatePfad(string label, TimeSpan timeout)
    {
        ElementWaitHelper.WaitForSelectedComboBoxItem(
            WaitForElement(Window, cf => cf.ByName("Update-Pfad").And(cf.ByControlType(ControlType.ComboBox)), timeout), label, timeout);
        return this;
    }

    /// <summary>Liest den schreibgeschützten Anzeigetext des abgeleiteten Update-Kanals im "Allgemein"-Tab.</summary>
    /// <returns>Der Kanal-Anzeigetext (siehe <c>UpdatePfadTexte</c>).</returns>
    /// <remarks>
    /// Das Element trägt kein <c>AutomationProperties.Name</c> — der gebundene Kanaltext ist
    /// daher als UIA-Name des TextBlocks lesbar (Muster wie <c>UpdateHinweis</c>).
    /// </remarks>
    public string GetAbgeleitetenUpdateKanal()
        => WaitForElement(Window, cf => cf.ByAutomationId("Update-Kanal"), Short).Name;

    /// <summary>Wartet auf die Speicher-Bestätigung "Einstellungen gespeichert.".</summary>
    /// <returns>Diese Instanz.</returns>
    /// <exception cref="TimeoutException">Die Bestätigung erschien nicht rechtzeitig.</exception>
    public SettingsView WaitForSettingsSaved()
    {
        WaitForElement(Window, cf => cf.ByName("Einstellungen gespeichert."), Medium);
        return this;
    }
}
