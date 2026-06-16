using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Softwareschmiede.App.ViewModels;

namespace Softwareschmiede.App.Views;

/// <summary>Hilfsmethoden für die Bearbeitung von <see cref="PluginSettingEntry"/> in XAML-Views (PasswordBox-Zwei-Wege-Bindung und Dateiauswahl-Dialog).</summary>
public static class PluginSettingEntryEditHelper
{
    /// <summary>Initialisiert das Passwort einer PasswordBox aus dem gebundenen <see cref="PluginSettingEntry"/>.</summary>
    public static void OnPasswordBoxLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox pb && pb.Tag is PluginSettingEntry entry)
            pb.Password = entry.Value ?? string.Empty;
    }

    /// <summary>Übernimmt eine Passwortänderung in den gebundenen <see cref="PluginSettingEntry"/>.</summary>
    public static void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox pb && pb.Tag is PluginSettingEntry entry)
            entry.Value = pb.Password;
    }

    /// <summary>Öffnet einen Dateiauswahl-Dialog und übernimmt den gewählten Pfad in den <see cref="PluginSettingEntry"/>.</summary>
    public static void OnDateiAuswaehlenClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.DataContext is not PluginSettingEntry entry)
            return;

        var dialog = new OpenFileDialog
        {
            Title = entry.Field.Label,
            Filter = "Alle Dateien (*.*)|*.*"
        };

        if (!string.IsNullOrEmpty(entry.Value) && System.IO.File.Exists(entry.Value))
            dialog.InitialDirectory = System.IO.Path.GetDirectoryName(entry.Value);

        if (dialog.ShowDialog() == true)
            entry.Value = dialog.FileName;
    }
}
