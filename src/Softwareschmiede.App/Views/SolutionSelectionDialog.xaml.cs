using System.Windows;
using Softwareschmiede.App.ViewModels;

namespace Softwareschmiede.App.Views;

/// <summary>Modaler Dialog für die Auswahl einer Solution bei mehreren gefundenen <c>*.sln</c>-Dateien.</summary>
public partial class SolutionSelectionDialog : Window
{
    private SolutionSelectionDialog()
    {
        InitializeComponent();
    }

    /// <inheritdoc/>
    public SolutionSelectionDialog(SolutionSelectionDialogViewModel viewModel) : this()
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
