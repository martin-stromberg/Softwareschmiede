using FlaUI.Core.AutomationElements;

namespace Softwareschmiede.Tests.E2E.Views.Dialogs;

/// <summary>View für den Visual-Studio-Lösungs-Auswahl-Dialog.</summary>
public sealed class SolutionSelectionDialogView : DialogView
{
    /// <param name="window">Das Hauptfenster der Anwendung.</param>
    public SolutionSelectionDialogView(Window window) : base(window)
    {
    }

    /// <inheritdoc/>
    protected override string DialogTitle => "Solution auswählen";
}
