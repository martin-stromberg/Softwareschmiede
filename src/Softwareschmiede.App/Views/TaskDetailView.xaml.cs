using System.Windows.Controls;
using Softwareschmiede.App.ViewModels;

namespace Softwareschmiede.App.Views;

/// <summary>Code-behind für TaskDetailView.</summary>
public sealed partial class TaskDetailView : UserControl
{
    /// <inheritdoc cref="TaskDetailView"/>
    public TaskDetailView()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (DataContext is TaskDetailViewModel vm)
            {
                vm.CliProzessGestartet += OnCliProzessGestartet;
                vm.LadenCommand.Execute(null);
            }
        };
        Unloaded += (_, _) =>
        {
            if (DataContext is TaskDetailViewModel vm)
            {
                vm.CliProzessGestartet -= OnCliProzessGestartet;
                vm.Dispose();
            }
        };
    }

    private void OnCliProzessGestartet(System.Diagnostics.Process process)
    {
        // Das Handle wird vom ViewModel gesetzt; ProcessWindowHost reagiert über Binding
        // Kein direkter Eingriff notwendig - EmbeddedWindowHandle im ViewModel wird aktualisiert
        _ = process;
    }
}
