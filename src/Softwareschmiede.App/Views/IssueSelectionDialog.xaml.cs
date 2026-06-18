using System.Windows;
using Softwareschmiede.App.ViewModels;

namespace Softwareschmiede.App.Views;

/// <summary>Dialog für die Auswahl eines Issues aus dem SCM-Plugin.</summary>
public partial class IssueSelectionDialog : Window
{
    private IssueSelectionDialog()
    {
        InitializeComponent();
    }

    /// <inheritdoc/>
    public IssueSelectionDialog(IssueSelectionDialogViewModel viewModel) : this()
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
