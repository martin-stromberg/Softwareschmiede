using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.Infrastructure.Terminal;

namespace Softwareschmiede.App.Views;

/// <summary>Code-behind für TaskDetailView.</summary>
public sealed partial class TaskDetailView : UserControl
{
    private TaskDetailViewModel? _subscribedViewModel;

    /// <inheritdoc cref="TaskDetailView"/>
    public TaskDetailView()
    {
        InitializeComponent();

        // WPF erzeugt keine neue TaskDetailView-Instanz, wenn CurrentView von einer
        // TaskDetailViewModel-Instanz zu einer anderen desselben Typs wechselt (implizites
        // DataTemplate in MainWindow.xaml) — nur die Bindings aktualisieren sich, Loaded/Unloaded
        // feuern dabei nicht erneut. Die Terminal-Sitzung muss deshalb über DataContextChanged
        // synchronisiert werden, nicht nur über Loaded/Unloaded.
        DataContextChanged += OnDataContextChanged;
        Unloaded += (_, _) =>
        {
            UnsubscribeAndDispose(_subscribedViewModel);
            _subscribedViewModel = null;
        };
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (ReferenceEquals(_subscribedViewModel, e.NewValue))
            return;

        UnsubscribeAndDispose(_subscribedViewModel);
        _subscribedViewModel = null;

        if (e.NewValue is TaskDetailViewModel vm)
        {
            vm.PseudoConsoleSessionGestartet += OnPseudoConsoleSessionGestartet;
            vm.CliGestoppt += OnCliGestoppt;
            _subscribedViewModel = vm;

            SetTerminalSession(vm.GetPseudoConsoleSession());
        }
        else
        {
            SetTerminalSession(null);
        }
    }

    private void UnsubscribeAndDispose(TaskDetailViewModel? vm)
    {
        if (vm is null)
            return;

        vm.PseudoConsoleSessionGestartet -= OnPseudoConsoleSessionGestartet;
        vm.CliGestoppt -= OnCliGestoppt;
        vm.Dispose();
    }

    private void OnPseudoConsoleSessionGestartet(PseudoConsoleSession session)
    {
        SetTerminalSession(session);
    }

    private void OnCliGestoppt()
    {
        SetTerminalSession(null);
    }

    /// <summary>Setzt die im TerminalControl angezeigte Sitzung und legt deren Prozess-ID zu Testzwecken als AutomationProperties.HelpText ab (siehe E2E_TaskWechselUeberMenue).</summary>
    /// <param name="session">Die anzuzeigende CLI-Sitzung, oder <c>null</c>, wenn keine Sitzung eingebettet werden soll.</param>
    private void SetTerminalSession(PseudoConsoleSession? session)
    {
        TerminalConsole.Session = session;
        AutomationProperties.SetHelpText(TerminalConsole, session?.Process.Id.ToString() ?? string.Empty);
    }
}
