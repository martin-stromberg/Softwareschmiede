using System.Windows;
using System.Windows.Controls;
using Softwareschmiede.App.ViewModels;

namespace Softwareschmiede.App.Views;

/// <summary>Nicht-modales Konsolentestfenster für das CLI-Ausgabe-Replay (Diagnose-Werkzeug).</summary>
public partial class KonsolenTestDialog : Window
{
    private KonsolenTestDialog()
    {
        InitializeComponent();
    }

    /// <inheritdoc/>
    public KonsolenTestDialog(KonsolenTestViewModel viewModel) : this()
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

    private void OnQuellListeSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (QuellChunkListe.SelectedItem is { } selected)
            QuellChunkListe.ScrollIntoView(selected);
    }
}
