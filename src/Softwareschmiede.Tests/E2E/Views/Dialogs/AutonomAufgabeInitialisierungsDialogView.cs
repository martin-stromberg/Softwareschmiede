using FlaUI.Core.AutomationElements;

namespace Softwareschmiede.Tests.E2E.Views.Dialogs;

/// <summary>View für den Initialisierungs-Dialog einer autonomen Aufgabe.</summary>
public sealed class AutonomAufgabeInitialisierungsDialogView : DialogView
{
    /// <param name="window">Das Hauptfenster der Anwendung.</param>
    public AutonomAufgabeInitialisierungsDialogView(Window window) : base(window)
    {
    }

    /// <inheritdoc/>
    protected override string DialogTitle => "Autonome Aufgabe initialisieren";
}
