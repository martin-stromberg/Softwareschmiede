using FlaUI.Core.AutomationElements;

namespace Softwareschmiede.Tests.E2E.Views.Dialogs;

/// <summary>View für den Issue-Erstellung-Dialog.</summary>
public sealed class IssueCreateDialogView : DialogView
{
    /// <param name="window">Das Hauptfenster der Anwendung.</param>
    public IssueCreateDialogView(Window window) : base(window)
    {
    }

    /// <inheritdoc/>
    protected override string DialogTitle => "Issue anlegen";
}
