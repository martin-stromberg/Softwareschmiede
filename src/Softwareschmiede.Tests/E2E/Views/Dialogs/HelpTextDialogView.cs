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
}
