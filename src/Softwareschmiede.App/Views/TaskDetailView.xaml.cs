using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Softwareschmiede.App.Controls;
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

    /// <summary>Reagiert auf einen DataContext-Wechsel und bindet die Session der neuen (oder keiner)
    /// Aufgabe an <see cref="TerminalControl"/>. Das Setzen von <c>TerminalConsole.Session</c> löst
    /// intern <c>TerminalControl.OnSessionChanged()</c> aus, welches den <c>BufferChanged</c>-Handler der
    /// alten Session deregistriert und ggf. den der neuen Session registriert (kein Verhaltensunterschied
    /// zu vorher, da <c>TerminalControl</c> die Leseschleife nicht mehr selbst besitzt — siehe Issue-86).</summary>
    /// <param name="sender">Die auslösende <see cref="TaskDetailView"/>-Instanz.</param>
    /// <param name="e">Die alte und neue DataContext-Instanz.</param>
    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (ReferenceEquals(_subscribedViewModel, e.NewValue))
            return;

        UnsubscribeAndDispose(_subscribedViewModel);
        _subscribedViewModel = null;

        if (e.NewValue is TaskDetailViewModel vm)
        {
            vm.TerminalSessionGestartet += OnTerminalSessionGestartet;
            vm.CliGestoppt += OnCliGestoppt;
            vm.PromptVorlageGesendet += OnPromptVorlageGesendet;
            _subscribedViewModel = vm;

            SetTerminalSession(vm.GetTerminalSession());
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

        vm.TerminalSessionGestartet -= OnTerminalSessionGestartet;
        vm.CliGestoppt -= OnCliGestoppt;
        vm.PromptVorlageGesendet -= OnPromptVorlageGesendet;
        vm.Dispose();
    }

    private void OnTerminalSessionGestartet(ITerminalSession session)
    {
        // Ein bereits eingeplantes Ereignis einer vorherigen Detailansicht kann noch nach
        // dem DataContext-Wechsel eintreffen. Die mitgelieferte Sitzung darf dann nicht
        // blind übernommen werden, weil sie zu einer anderen Aufgabe gehören kann.
        // Maßgeblich ist ausschließlich die Sitzung des aktuell gebundenen ViewModels.
        SetTerminalSession(_subscribedViewModel?.GetTerminalSession());
    }

    private void OnCliGestoppt()
    {
        // Analog zum Start-Ereignis darf ein verspätetes Stopp-Ereignis einer alten Aufgabe
        // die sichtbare Sitzung der aktuell gewählten Aufgabe nicht entfernen.
        SetTerminalSession(_subscribedViewModel?.GetTerminalSession());
    }

    private void OnPromptVorlageGesendet()
    {
        _ = Dispatcher.InvokeAsync(() =>
        {
            TerminalConsole.Focus();
            Keyboard.Focus(TerminalConsole);
        }, System.Windows.Threading.DispatcherPriority.ContextIdle);
    }

    private void OnTerminalScrollViewerPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!ShouldFocusTerminalFromScrollViewerMouseSource(e.OriginalSource))
            return;

        // Der ScrollViewer sorgt nur dafür, dass nach einem Klick der Terminal-Renderer
        // den Tastaturfokus erhält. Das Ereignis darf dabei nicht konsumiert werden:
        // TerminalControl.OnMouseDown muss denselben Klick anschließend erhalten, um
        // den Auswahlanker zu setzen und die Maus zu capturen.
        FocusTerminalConsole();

        _ = Dispatcher.InvokeAsync(
            FocusTerminalConsole,
            System.Windows.Threading.DispatcherPriority.Input);
    }

    private void FocusTerminalConsole()
    {
        FocusManager.SetFocusedElement(this, TerminalConsole);
        TerminalConsole.Focus();
        Keyboard.Focus(TerminalConsole);
    }

    internal static bool ShouldFocusTerminalFromScrollViewerMouseSource(object? originalSource)
    {
        var current = originalSource as DependencyObject;
        while (current is not null)
        {
            if (current is ScrollBar)
                return false;

            current = GetDependencyParent(current);
        }

        return true;
    }

    private static DependencyObject? GetDependencyParent(DependencyObject current)
    {
        if (current is Visual or System.Windows.Media.Media3D.Visual3D)
        {
            var visualParent = VisualTreeHelper.GetParent(current);
            if (visualParent is not null)
                return visualParent;
        }

        return LogicalTreeHelper.GetParent(current);
    }

    /// <summary>Setzt die im TerminalControl angezeigte Sitzung und legt die zugehörige Aufgaben-ID
    /// zu Testzwecken als AutomationProperties.HelpText ab (siehe E2E_TaskWechselUeberMenue).</summary>
    /// <param name="session">Die anzuzeigende CLI-Sitzung, oder <c>null</c>, wenn keine Sitzung eingebettet werden soll.</param>
    private void SetTerminalSession(ITerminalSession? session)
    {
        TerminalConsole.Session = session;
        AutomationProperties.SetHelpText(TerminalConsole, GetTerminalTaskId(session));
    }

    /// <summary>
    /// Liefert die Aufgaben-ID der aktuell eingebetteten Sitzung für die E2E-Diagnose. Die vorher
    /// verwendete Prozess-ID existiert bei dem im E2E-Testmodus vorgesehenen Pipe-Backend nicht und
    /// konnte deshalb die Zuordnung einer Sitzung zu ihrer Aufgabe nicht zuverlässig abbilden.
    /// </summary>
    /// <param name="session">Die anzuzeigende Sitzung, oder <c>null</c>.</param>
    /// <returns>Die Aufgaben-ID als String, oder ein leerer String, wenn keine Sitzung oder Aufgabe vorhanden ist.</returns>
    private string GetTerminalTaskId(ITerminalSession? session)
    {
        if (session is null || _subscribedViewModel?.AufgabeId is not { } aufgabeId || aufgabeId == Guid.Empty)
            return string.Empty;

        return aufgabeId.ToString("D");
    }
}
