using System.Windows;
using Softwareschmiede.App.ViewModels;

namespace Softwareschmiede.App.Views;

/// <summary>Dialog für die Anlage eines neuen Issues.</summary>
public partial class IssueCreateDialog : Window
{
    private IssueCreateDialog()
    {
        InitializeComponent();
    }

    /// <inheritdoc/>
    public IssueCreateDialog(IssueCreateDialogViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.CloseRequested += OnCloseRequested;
        Loaded += (_, _) => viewModel.LoadTemplatesCommand.Execute(null);
        Closed += (_, _) => viewModel.CloseRequested -= OnCloseRequested;
    }

    private void OnCloseRequested(object? sender, bool dialogResult)
    {
        DialogResult = dialogResult;
        Close();
    }
}
