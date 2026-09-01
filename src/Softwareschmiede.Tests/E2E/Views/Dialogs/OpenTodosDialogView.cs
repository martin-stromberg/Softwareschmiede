using FlaUI.Core.AutomationElements;

namespace Softwareschmiede.Tests.E2E.Views.Dialogs;

/// <summary>View für den Dialog mit offenen To-Dos.</summary>
public sealed class OpenTodosDialogView : DialogView
{
    /// <param name="window">Das Hauptfenster der Anwendung.</param>
    public OpenTodosDialogView(Window window) : base(window)
    {
    }

    /// <inheritdoc/>
    protected override string DialogTitle => "Offene Todos";
}
