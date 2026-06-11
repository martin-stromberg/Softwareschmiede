using System.Windows.Controls;
using System.Windows.Input;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.Domain.Entities;

namespace Softwareschmiede.App.Views;

/// <summary>Code-behind für TaskListView.</summary>
public sealed partial class TaskListView : UserControl
{
    /// <inheritdoc cref="TaskListView"/>
    public TaskListView()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (DataContext is TaskListViewModel vm)
                vm.LadenCommand.Execute(null);
        };
    }

    private void AufgabeDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListViewItem { DataContext: Aufgabe aufgabe }
            && DataContext is TaskListViewModel vm)
        {
            vm.AufgabeOeffnenCommand.Execute(aufgabe.Id);
        }
    }
}
