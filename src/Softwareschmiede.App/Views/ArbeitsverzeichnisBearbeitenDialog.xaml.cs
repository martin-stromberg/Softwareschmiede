using System.Windows;
using Softwareschmiede.App.ViewModels;

namespace Softwareschmiede.App.Views;

/// <summary>Dialog zur nachträglichen Bearbeitung des Arbeitsverzeichnisses eines zugewiesenen Repositories.</summary>
public partial class ArbeitsverzeichnisBearbeitenDialog : Window
{
    private ArbeitsverzeichnisBearbeitenDialog()
    {
        InitializeComponent();
    }

    /// <inheritdoc/>
    public ArbeitsverzeichnisBearbeitenDialog(ArbeitsverzeichnisBearbeitenViewModel viewModel) : this()
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
