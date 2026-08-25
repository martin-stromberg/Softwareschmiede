using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace Softwareschmiede.Tests.E2E.Views;

/// <summary>View für die Dashboard-Startansicht.</summary>
public sealed class DashboardView : BaseWindowView
{
    /// <param name="window">Das Hauptfenster der Anwendung.</param>
    public DashboardView(Window window) : base(window)
    {
    }

    /// <inheritdoc/>
    public override bool IsVisible
        => ElementExists(Window, cf => cf.ByName("Dashboard").And(cf.ByControlType(ControlType.Text)));

    /// <inheritdoc/>
    public override DashboardView ForceShow()
    {
        if (IsVisible)
            return this;

        Menu.NavigateToDashboard();
        return this;
    }

    /// <inheritdoc/>
    public override DashboardView ForceClose(bool recurseToDashboard) => this;
}
