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

    /// <summary>Bricht den Dialog über den "Abbrechen"-Button ab, ohne eine Solution zu öffnen.</summary>
    public void Cancel()
    {
        var dialog = GetDialogWindow();
        WaitForElement(dialog, cf => cf.ByName("Abbrechen"), Short).AsButton().Click();
        WaitUntilGone(Window.Automation.GetDesktop(), DialogWindowCondition, Short);
    }

    /// <summary>Wählt einen Eintrag in der "SolutionAuswahl"-Liste anhand seines (plugin-qualifizierten) Anzeigetexts aus.</summary>
    /// <param name="displayName">Der Anzeigetext, z. B. "Visual Studio: Zweite.slnx".</param>
    /// <returns>Diese Instanz.</returns>
    public SolutionSelectionDialogView SelectSolution(string displayName)
    {
        var dialog = GetDialogWindow();
        var liste = WaitForElement(dialog, cf => cf.ByName("SolutionAuswahl"), Short);
        WaitForElement(liste, cf => cf.ByName(displayName), Short).Click();
        return this;
    }

    /// <summary>Bestätigt die Auswahl über den "OK"-Button.</summary>
    public void Confirm()
    {
        var dialog = GetDialogWindow();
        WaitForElement(dialog, cf => cf.ByName("OK"), Short).AsButton().Click();
    }
}
