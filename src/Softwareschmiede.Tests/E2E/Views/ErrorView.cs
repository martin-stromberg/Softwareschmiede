using FlaUI.Core.AutomationElements;

namespace Softwareschmiede.Tests.E2E.Views;

/// <summary>View für das Fehlerbanner ("FehlerMeldung"), das in mehreren Haupt-Views inline eingeblendet wird.</summary>
public sealed class ErrorView : BaseWindowView
{
    /// <param name="window">Das Hauptfenster der Anwendung.</param>
    public ErrorView(Window window) : base(window)
    {
    }

    /// <inheritdoc/>
    public override bool IsVisible => ElementExists(Window, cf => cf.ByName("FehlerMeldung"));

    /// <inheritdoc/>
    public override ErrorView ForceShow() => this;

    /// <inheritdoc/>
    public override ErrorView ForceClose(bool recurseToDashboard) => this;

    /// <returns>Der Text der aktuellen Fehlermeldung.</returns>
    public string GetErrorMessage() => GetHelpTextOrName(WaitForElement(Window, cf => cf.ByName("FehlerMeldung"), Short));

    /// <summary>
    /// Wartet, bis das Fehlerbanner verschwindet. Es gibt keinen dedizierten Schließen-Button - die
    /// Anwendung blendet die Fehlermeldung aus, sobald eine Folgeaktion sie im ViewModel zurücksetzt.
    /// </summary>
    /// <returns>Diese Instanz.</returns>
    public ErrorView DismissError()
    {
        WaitUntilGone(Window, cf => cf.ByName("FehlerMeldung"), Medium);
        return this;
    }
}
