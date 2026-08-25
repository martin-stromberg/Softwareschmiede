using FlaUI.Core.AutomationElements;

namespace Softwareschmiede.Tests.E2E.Views;

/// <summary>View für die Detailansicht einer autonomen Aufgabe.</summary>
public sealed class AutonomAufgabeDetailView : BaseWindowView
{
    /// <param name="window">Das Hauptfenster der Anwendung.</param>
    public AutonomAufgabeDetailView(Window window) : base(window)
    {
    }

    /// <inheritdoc/>
    public override bool IsVisible => ElementExists(Window, cf => cf.ByName("AutonomAufgabeDetailTabs"));

    /// <returns>Diese Instanz, wenn die Ansicht bereits sichtbar ist.</returns>
    /// <exception cref="InvalidOperationException">Wird geworfen, wenn die Ansicht noch nicht sichtbar ist.</exception>
    public override AutonomAufgabeDetailView ForceShow()
    {
        if (IsVisible)
            return this;

        throw new InvalidOperationException(
            "AutonomAufgabeDetailView.ForceShow() kann nicht ohne vorherige Initialisierung einer autonomen " +
            "Aufgabe navigieren. Nutze AutonomAufgabeInitialisierungsDialogView, um eine autonome Aufgabe zu starten.");
    }

    /// <summary>Klickt den "Zurück"-Button und wartet auf die Rückkehr zur <c>TaskDetailView</c> ("EditTitel"-Feld).</summary>
    /// <param name="recurseToDashboard">Wenn <c>true</c>, werden auch alle übergeordneten Ansichten bis zum Dashboard geschlossen.</param>
    /// <returns>Diese Instanz (Fluent-API).</returns>
    /// <exception cref="TimeoutException">Wird geworfen, wenn der "Zurück"-Button nicht rechtzeitig gefunden wird.</exception>
    public override AutonomAufgabeDetailView ForceClose(bool recurseToDashboard)
    {
        WaitForElement(Window, cf => cf.ByName("Zurück"), Short).AsButton().Click();
        WaitForElement(Window, cf => cf.ByName("EditTitel"), Medium);

        if (recurseToDashboard)
            RecurseToDashboard();

        return this;
    }
}
