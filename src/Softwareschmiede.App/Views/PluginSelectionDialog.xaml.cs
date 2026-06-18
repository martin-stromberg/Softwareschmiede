using System.Windows;
using Softwareschmiede.App.ViewModels;

namespace Softwareschmiede.App.Views;

/// <summary>Dialog für die Auswahl eines KI-Plugins.</summary>
public partial class PluginSelectionDialog : Window
{
    private PluginSelectionDialog()
    {
        InitializeComponent();
    }

    /// <inheritdoc/>
    public PluginSelectionDialog(PluginSelectionDialogViewModel viewModel) : this()
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
