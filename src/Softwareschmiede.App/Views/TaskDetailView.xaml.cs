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
        _ = WaitForWindowHandleAsync(process);
    }

    private async Task WaitForWindowHandleAsync(System.Diagnostics.Process process)
    {
        var capturedViewModel = DataContext as TaskDetailViewModel;
        try
        {
            var deadline = DateTime.UtcNow.AddSeconds(15);
            while (DateTime.UtcNow < deadline && !process.HasExited)
            {
                process.Refresh();
                if (process.MainWindowHandle != IntPtr.Zero)
                    break;
                await Task.Delay(200);
            }

            var handle = process.MainWindowHandle;
            if (handle != IntPtr.Zero && DataContext is TaskDetailViewModel vm && ReferenceEquals(vm, capturedViewModel))
                vm.EmbeddedWindowHandle = handle;
        }
        catch (Exception)
        {
            // Prozess ist bereits beendet oder nicht zugänglich – kein Einbetten möglich
        }
    }
}
