using FlaUI.Core.AutomationElements;

namespace Softwareschmiede.Tests.E2E.Views.Dialogs;

/// <summary>View für den Repository-Zuweisungs-Dialog.</summary>
public sealed class RepositoryAssignDialogView : DialogView
{
    /// <param name="window">Das Hauptfenster der Anwendung.</param>
    public RepositoryAssignDialogView(Window window) : base(window)
    {
    }

    /// <inheritdoc/>
    protected override string DialogTitle => "Repository zuweisen";

    /// <summary>Klickt den "Zuweisen"-Button im Hauptfenster (ProjectDetailView), falls der Dialog noch nicht sichtbar ist.</summary>
    /// <returns>Diese Instanz.</returns>
    public override RepositoryAssignDialogView ForceShow()
    {
        if (IsVisible)
            return this;

        WaitForElement(Window, cf => cf.ByName("Zuweisen"), Short).AsButton().Click();
        GetDialogWindow();

        return this;
    }
}
