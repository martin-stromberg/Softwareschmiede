using FlaUI.Core.AutomationElements;

namespace Softwareschmiede.Tests.E2E.Views.Dialogs;

/// <summary>Native Win32-MessageBox zur Löschbestätigung. Automation-IDs entsprechen den stabilen, sprachunabhängigen IDYES/IDNO-Konstanten.</summary>
public sealed class DeleteConfirmationDialogView : DialogView
{
    /// <param name="window">Das Hauptfenster der Anwendung.</param>
    public DeleteConfirmationDialogView(Window window) : base(window)
    {
    }

    /// <inheritdoc/>
    protected override string DialogTitle => "Löschen bestätigen";

    /// <summary>Bestätigt die Löschung über den "Ja"-Button (IDYES).</summary>
    /// <returns>Diese Instanz.</returns>
    public DeleteConfirmationDialogView Confirm()
    {
        var dialog = GetDialogWindow();
        WaitForElement(dialog, cf => cf.ByAutomationId("6"), Short).AsButton().Click();

        return this;
    }

    /// <summary>Bricht die Löschung über den "Nein"-Button (IDNO) ab.</summary>
    /// <returns>Diese Instanz.</returns>
    public DeleteConfirmationDialogView Cancel()
    {
        var dialog = GetDialogWindow();
        WaitForElement(dialog, cf => cf.ByAutomationId("7"), Short).AsButton().Click();

        return this;
    }
}
