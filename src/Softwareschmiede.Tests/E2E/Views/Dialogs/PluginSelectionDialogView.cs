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
}
