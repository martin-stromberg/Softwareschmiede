using System.Windows;
using Softwareschmiede.App.ViewModels;

namespace Softwareschmiede.App.Views;

/// <summary>Modaler read-only Dialog für offene To-Dos einer Aufgabe.</summary>
public partial class OpenTodosDialog : Window
{
    private OpenTodosDialog()
    {
        InitializeComponent();
    }

    /// <inheritdoc/>
    public OpenTodosDialog(OpenTodosDialogViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }
}
