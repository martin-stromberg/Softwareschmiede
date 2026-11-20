using FlaUI.Core.AutomationElements;
using Softwareschmiede.Tests.E2E;

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

        WaitForElement(Window, cf => cf.ByName("Start"), Short).AsButton().ClickInForeground();
        GetDialogWindow();

        return this;
    }

    /// <summary>
    /// Wählt das angegebene KI-Plugin in der "PluginSelection"-ComboBox aus und setzt optional die
    /// "UseForProject"-Checkbox (Projekt-Standard speichern).
    /// </summary>
    /// <param name="pluginName">Der Name des im Dialog auszuwählenden KI-Plugins.</param>
    /// <param name="forProjectUse">Wenn <c>true</c>, wird die "UseForProject"-Checkbox gesetzt.</param>
    /// <returns>Diese Instanz.</returns>
    public PluginSelectionDialogView SelectPlugin(string pluginName, bool forProjectUse = false)
    {
        var dialog = GetDialogWindow();
        var pluginSelectionBox = WaitForElement(dialog, cf => cf.ByName("PluginSelection"), Short);
        SelectComboBoxItemByClick(pluginSelectionBox, pluginName, Short);
        ElementWaitHelper.WaitForSelectedComboBoxItem(pluginSelectionBox, pluginName, Short);

        if (forProjectUse)
        {
            var checkbox = WaitForElement(dialog, cf => cf.ByName("UseForProject"), Short);
            checkbox.AsCheckBox().IsChecked = true;
        }

        return this;
    }

    /// <summary>Bestätigt den Dialog über den "OK"-Button.</summary>
    public void Confirm()
    {
        var dialog = GetDialogWindow();
        WaitForElement(dialog, cf => cf.ByName("OK"), Short).AsButton().ClickInForeground();
    }

    /// <summary>Bricht den Dialog über den "Cancel"-Button ab, ohne ein Plugin zu übernehmen.</summary>
    public void Cancel()
    {
        var dialog = GetDialogWindow();
        WaitForElement(dialog, cf => cf.ByName("Cancel"), Short).AsButton().ClickInForeground();
    }
}
