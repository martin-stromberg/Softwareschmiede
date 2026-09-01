using FlaUI.Core.AutomationElements;

namespace Softwareschmiede.Tests.E2E.Views.Dialogs;

/// <summary>View für den Update-Fortschritts-Dialog.</summary>
public sealed class UpdateProgressDialogView : DialogView
{
    /// <param name="window">Das Hauptfenster der Anwendung.</param>
    public UpdateProgressDialogView(Window window) : base(window)
    {
    }

    /// <inheritdoc/>
    protected override string DialogTitle => "Update vorbereiten";
}
