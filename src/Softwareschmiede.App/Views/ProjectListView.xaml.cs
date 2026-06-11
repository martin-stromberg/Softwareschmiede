using System.Windows.Controls;
using Softwareschmiede.App.ViewModels;

namespace Softwareschmiede.App.Views;

/// <summary>Code-behind für ProjectListView.</summary>
public sealed partial class ProjectListView : UserControl
{
    /// <inheritdoc cref="ProjectListView"/>
    public ProjectListView()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (DataContext is ProjectListViewModel vm)
                vm.LadenCommand.Execute(null);
        };
    }
}
