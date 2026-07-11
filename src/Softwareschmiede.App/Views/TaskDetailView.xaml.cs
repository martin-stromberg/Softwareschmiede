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
        AutomationProperties.SetHelpText(TerminalConsole, TryGetProcessId(session));
    }

    /// <summary>
    /// Liest die Prozess-ID einer Sitzung robust aus. <see cref="PseudoConsoleSession.Process"/> stammt
    /// bei ConPTY-Sitzungen aus <see cref="System.Diagnostics.Process.GetProcessById(int)"/> - ist der
    /// zugrunde liegende Prozess bereits beendet (z. B. sehr kurzlebige ConPTY-Kindprozesse), kann jeder
    /// Zugriff darauf mit <see cref="InvalidOperationException"/> ("No process is associated with this
    /// object") fehlschlagen, siehe KiAusfuehrungsService.TryGetExitCode für denselben Fehlermodus. Da
    /// dieser Wert nur diagnostisch (E2E-Test-HelpText) ist, darf ein solcher Fehler die UI nicht
    /// beeinträchtigen.
    /// </summary>
    /// <param name="session">Die Sitzung, deren Prozess-ID gelesen werden soll, oder <c>null</c>.</param>
    /// <returns>Die Prozess-ID als String, oder ein leerer String, wenn keine Sitzung vorhanden ist oder die ID nicht (mehr) gelesen werden kann.</returns>
    private static string TryGetProcessId(PseudoConsoleSession? session)
    {
        if (session is null)
            return string.Empty;

        try
        {
            return session.Process.Id.ToString();
        }
        catch (InvalidOperationException)
        {
            return string.Empty;
        }
    }
}
