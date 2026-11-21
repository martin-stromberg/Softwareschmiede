using System.Windows;
using System.Windows.Controls;
using Softwareschmiede.App.ViewModels;

namespace Softwareschmiede.App.Views;

/// <summary>Nicht-modales Konsolentestfenster für das CLI-Ausgabe-Replay (Diagnose-Werkzeug).</summary>
public partial class ConsoleTestDialog : Window
{
    private ConsoleTestDialog()
    {
        InitializeComponent();
    }

    /// <inheritdoc/>
    public ConsoleTestDialog(ConsoleTestViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.CloseRequested += OnCloseRequested;
        Closed += (_, _) =>
        {
            viewModel.CloseRequested -= OnCloseRequested;
            viewModel.Dispose();
        };
    }

    private void OnCloseRequested(object? sender, bool dialogResult) => Close();

    private void OnSourceListSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SourceChunkList.SelectedItem is { } selected)
            SourceChunkList.ScrollIntoView(selected);
    }
}
