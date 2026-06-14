using System.Windows.Controls;
using System.Windows.Input;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.Domain.Entities;

namespace Softwareschmiede.App.Views;

/// <summary>Code-behind für ProjectDetailView.</summary>
public sealed partial class ProjectDetailView : UserControl
{
    /// <inheritdoc cref="ProjectDetailView"/>
    public ProjectDetailView()
    {
        InitializeComponent();
        Loaded += (_, _) => ProjektNameTextBox.Focus();
    }

    private void AufgabeDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem { DataContext: Aufgabe aufgabe }
            && DataContext is ProjectDetailViewModel vm)
        {
            vm.AufgabeOeffnenCommand.Execute(aufgabe.Id);
        }
    }
}
