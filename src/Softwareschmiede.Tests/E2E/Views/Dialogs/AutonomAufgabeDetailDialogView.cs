using FlaUI.Core.AutomationElements;

namespace Softwareschmiede.Tests.E2E.Views.Dialogs;

/// <summary>View für den Detail-Dialog einer autonomen Aufgabe.</summary>
public sealed class AutonomAufgabeDetailDialogView : DialogView
{
    /// <param name="window">Das Hauptfenster der Anwendung.</param>
    public AutonomAufgabeDetailDialogView(Window window) : base(window)
    {
    }

    /// <inheritdoc/>
    protected override string DialogTitle => "Autonome Aufgabe";
}
