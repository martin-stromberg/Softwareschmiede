using FlaUI.Core.AutomationElements;

namespace Softwareschmiede.Tests.E2E.Views.Dialogs;

/// <summary>View für den Hilfetext-Dialog.</summary>
public sealed class HelpTextDialogView : DialogView
{
    /// <param name="window">Das Hauptfenster der Anwendung.</param>
    public HelpTextDialogView(Window window) : base(window)
    {
    }

    /// <inheritdoc/>
    protected override string DialogTitle => "Hilfe";

    /// <summary>Schließt den Dialog über den "Schließen"-Button (statt Alt+F4 wie <see cref="DialogView.ForceClose"/>).</summary>
    public void Close()
    {
        var dialog = GetDialogWindow();
        WaitForElement(dialog, cf => cf.ByName("Schließen"), Short).AsButton().Click();
        WaitUntilGone(Window.Automation.GetDesktop(), DialogWindowCondition, Short);
    }
}
