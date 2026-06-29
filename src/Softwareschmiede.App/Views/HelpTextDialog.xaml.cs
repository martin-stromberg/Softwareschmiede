using System.Windows;

namespace Softwareschmiede.App.Views;

/// <summary>Dialog zur Anzeige des CLI-Hilfetexts.</summary>
public partial class HelpTextDialog : Window
{
    /// <summary>Erstellt eine neue Instanz von <see cref="HelpTextDialog"/>.</summary>
    public HelpTextDialog(string text)
    {
        InitializeComponent();
        HelpTextBox.Text = text;
    }

    private void OnSchliessenClick(object sender, RoutedEventArgs e) => Close();
}
