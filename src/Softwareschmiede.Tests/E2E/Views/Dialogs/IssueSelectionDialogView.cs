using FlaUI.Core.AutomationElements;

namespace Softwareschmiede.Tests.E2E.Views.Dialogs;

/// <summary>View für den Issue-Auswahl-Dialog.</summary>
public sealed class IssueSelectionDialogView : DialogView
{
    /// <param name="window">Das Hauptfenster der Anwendung.</param>
    public IssueSelectionDialogView(Window window) : base(window)
    {
    }

    /// <inheritdoc/>
    protected override string DialogTitle => "Issue auswählen";
}
