using System.Windows;
using Softwareschmiede.App.ViewModels;

namespace Softwareschmiede.App.Views;

/// <summary>Modaler Dialog zum Einstellen oder Aufheben einer Aufgaben-Pause.</summary>
public partial class AufgabePausierenDialog : Window
{
    private AufgabePausierenDialog()
    {
        InitializeComponent();
    }

    /// <inheritdoc/>
    public AufgabePausierenDialog(AufgabePausierenDialogViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.CloseRequested += OnCloseRequested;
        Closed += (_, _) => viewModel.CloseRequested -= OnCloseRequested;
    }

    private void OnCloseRequested(object? sender, bool dialogResult)
    {
        DialogResult = dialogResult;
        Close();
    }
}
