using System.Windows.Controls;
using Softwareschmiede.App.ViewModels;

namespace Softwareschmiede.App.Views;

/// <summary>Code-behind für DashboardView.</summary>
public sealed partial class DashboardView : UserControl
{
    /// <inheritdoc cref="DashboardView"/>
    public DashboardView()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (DataContext is DashboardViewModel vm)
                vm.LadenCommand.Execute(null);
        };
    }
}
