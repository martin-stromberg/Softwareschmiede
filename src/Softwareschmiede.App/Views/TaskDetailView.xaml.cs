using System.Windows.Controls;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.Infrastructure.Terminal;

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
                vm.PseudoConsoleSessionGestartet += OnPseudoConsoleSessionGestartet;

                var existingSession = vm.GetPseudoConsoleSession();
                if (existingSession != null)
                    TerminalConsole.Session = existingSession;
            }
        };
        Unloaded += (_, _) =>
        {
            if (DataContext is TaskDetailViewModel vm)
            {
                vm.PseudoConsoleSessionGestartet -= OnPseudoConsoleSessionGestartet;
                vm.Dispose();
            }
        };
    }

    private void OnPseudoConsoleSessionGestartet(PseudoConsoleSession session)
    {
        TerminalConsole.Session = session;
    }
}
