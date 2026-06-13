using System.Windows;
using Softwareschmiede.App.ViewModels;

namespace Softwareschmiede.App.Views;

/// <summary>Dialog für die Zuweisung eines Repository zu einem Projekt.</summary>
public partial class RepositoryAssignDialog : Window
{
    private RepositoryAssignDialog()
    {
        InitializeComponent();
    }

    /// <inheritdoc/>
    public RepositoryAssignDialog(RepositoryAssignViewModel viewModel) : this()
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
