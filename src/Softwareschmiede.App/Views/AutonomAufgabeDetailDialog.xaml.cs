using System.Windows;
using Softwareschmiede.App.ViewModels;

namespace Softwareschmiede.App.Views;

/// <summary>Dialogfenster, das die AutonomAufgabeDetailView als eigenständiges Fenster anzeigt.</summary>
public partial class AutonomAufgabeDetailDialog : Window
{
    private AutonomAufgabeDetailDialog()
    {
        InitializeComponent();
    }

    /// <inheritdoc/>
    public AutonomAufgabeDetailDialog(AutonomAufgabeDetailViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }
}
