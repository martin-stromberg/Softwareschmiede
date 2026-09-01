using FlaUI.Core.AutomationElements;

namespace Softwareschmiede.Tests.E2E.Views.Dialogs;

/// <summary>View für den KI-Plugin-Auswahl-Dialog.</summary>
public sealed class PluginSelectionDialogView : DialogView
{
    /// <param name="window">Das Hauptfenster der Anwendung.</param>
    public PluginSelectionDialogView(Window window) : base(window)
    {
    }

    /// <inheritdoc/>
    protected override string DialogTitle => "KI-Plugin auswählen";

    /// <summary>Klickt den "Starten"-Button im Hauptfenster (TaskDetailView), falls der Dialog noch nicht sichtbar ist.</summary>
    /// <returns>Diese Instanz.</returns>
    public override PluginSelectionDialogView ForceShow()
    {
        if (IsVisible)
            return this;

        WaitForElement(Window, cf => cf.ByName("Starten"), Short).AsButton().Click();
        GetDialogWindow();

        return this;
    }

    /// <summary>
    /// Wählt das angegebene KI-Plugin in der "PluginAuswahl"-ComboBox aus und setzt optional die
    /// "FuerProjektVerwenden"-Checkbox (Projekt-Standard speichern).
    /// </summary>
    /// <param name="pluginName">Der Name des im Dialog auszuwählenden KI-Plugins.</param>
    /// <param name="fuerProjektVerwenden">Wenn <c>true</c>, wird die "FuerProjektVerwenden"-Checkbox gesetzt.</param>
    /// <returns>Diese Instanz.</returns>
    public PluginSelectionDialogView SelectPlugin(string pluginName, bool fuerProjektVerwenden = false)
    {
        var dialog = GetDialogWindow();
        var pluginAuswahlBox = WaitForElement(dialog, cf => cf.ByName("PluginAuswahl"), Short);
        SelectComboBoxItemByClick(pluginAuswahlBox, pluginName, Short);

        if (fuerProjektVerwenden)
        {
            var checkbox = WaitForElement(dialog, cf => cf.ByName("FuerProjektVerwenden"), Short);
            checkbox.AsCheckBox().IsChecked = true;
        }

        return this;
    }

    /// <summary>Bestätigt den Dialog über den "OK"-Button.</summary>
    public void Confirm()
    {
        var dialog = GetDialogWindow();
        WaitForElement(dialog, cf => cf.ByName("OK"), Short).AsButton().Click();
    }

    /// <summary>Bricht den Dialog über den "Abbrechen"-Button ab, ohne ein Plugin zu übernehmen.</summary>
    public void Cancel()
    {
        var dialog = GetDialogWindow();
        WaitForElement(dialog, cf => cf.ByName("Abbrechen"), Short).AsButton().Click();
    }
}
