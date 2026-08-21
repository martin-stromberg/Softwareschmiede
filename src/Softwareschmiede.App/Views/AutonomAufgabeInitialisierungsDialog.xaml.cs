using System.Windows;
using Softwareschmiede.App.ViewModels;

namespace Softwareschmiede.App.Views;

/// <summary>Dialog zur Initialisierung einer Autonomen Aufgabe.</summary>
public partial class AutonomAufgabeInitialisierungsDialog : Window
{
    private AutonomAufgabeInitialisierungsDialog()
    {
        InitializeComponent();
    }

    /// <inheritdoc/>
    public AutonomAufgabeInitialisierungsDialog(AutonomAufgabeInitialisierungsDialogViewModel viewModel) : this()
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
