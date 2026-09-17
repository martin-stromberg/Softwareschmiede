using FlaUI.Core.AutomationElements;

namespace Softwareschmiede.Tests.E2E.Views.Dialogs;

/// <summary>
/// Native Win32-Ja/Nein-MessageBox der Update-Sicherheitsbestätigung ("Update starten?"),
/// die bei riskanten aktiven CLI-Aufgaben erscheint. Die Automation-IDs entsprechen den
/// sprachunabhängigen IDYES/IDNO-Konstanten (6/7), wie bei
/// <see cref="DeleteConfirmationDialogView"/>.
/// </summary>
public sealed class UpdateSafetyDialogView : DialogView
{
    /// <param name="window">Das Hauptfenster der Anwendung.</param>
    public UpdateSafetyDialogView(Window window) : base(window)
    {
    }

    /// <inheritdoc/>
    protected override string DialogTitle => "Update starten?";

    /// <summary>Bestätigt das Update trotz riskanter Aufgaben über den "Ja"-Button (IDYES).</summary>
    /// <returns>Diese Instanz.</returns>
    public UpdateSafetyDialogView Confirm()
    {
        var dialog = GetDialogWindow();
        WaitForElement(dialog, cf => cf.ByAutomationId("6"), Short).AsButton().Click();
        return this;
    }

    /// <summary>Bricht das Update über den "Nein"-Button (IDNO) ab.</summary>
    /// <returns>Diese Instanz.</returns>
    public UpdateSafetyDialogView Cancel()
    {
        var dialog = GetDialogWindow();
        WaitForElement(dialog, cf => cf.ByAutomationId("7"), Short).AsButton().Click();
        return this;
    }
}
