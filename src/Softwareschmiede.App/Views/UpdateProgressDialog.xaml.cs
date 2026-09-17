using System.Windows;
using Softwareschmiede.App.ViewModels;

namespace Softwareschmiede.App.Views;

/// <summary>Dialog zur Anzeige des Update-Vorbereitungsfortschritts.</summary>
public sealed partial class UpdateProgressDialog : Window
{
    /// <inheritdoc cref="UpdateProgressDialog"/>
    public UpdateProgressDialog()
    {
        InitializeComponent();
    }

    private void OnSchliessenClick(object sender, RoutedEventArgs e) => Close();

    /// <inheritdoc/>
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (DataContext is UpdateProgressViewModel { CanClose: false })
        {
            e.Cancel = true;
        }

        base.OnClosing(e);
    }
}
