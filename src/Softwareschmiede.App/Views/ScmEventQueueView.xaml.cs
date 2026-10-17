using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Softwareschmiede.App.ViewModels;

namespace Softwareschmiede.App.Views;

/// <summary>WPF-Ansicht für dauerhafte SCM-Ereignisse und deren bestätigte Benutzeraktionen.</summary>
public partial class ScmEventQueueView : UserControl
{
    /// <summary>Initialisiert die Queue-Ansicht.</summary>
    public ScmEventQueueView() => InitializeComponent();

    private void OnDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ScmEventQueueViewModel vm && e.OriginalSource is DependencyObject source &&
            ItemsControl.ContainerFromElement((DataGrid)sender, source) is DataGridRow && vm.OpenEventCommand.CanExecute(null))
            vm.OpenEventCommand.Execute(null);
    }
}
