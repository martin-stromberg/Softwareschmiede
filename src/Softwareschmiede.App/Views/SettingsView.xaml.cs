using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.Domain.Interfaces;

namespace Softwareschmiede.App.Views;

/// <summary>Code-behind für SettingsView.</summary>
public sealed partial class SettingsView : UserControl
{
    /// <inheritdoc cref="SettingsView"/>
    public SettingsView()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (DataContext is SettingsViewModel vm)
                vm.LadenCommand.Execute(null);
        };
    }

    private void OnScmPluginSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not SettingsViewModel vm)
            return;

        if (e.AddedItems.Count > 0 && e.AddedItems[0] is IGitPlugin plugin)
            vm.ScmPluginSelectedCommand.Execute(plugin);
    }

    private void OnKiPluginSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not SettingsViewModel vm)
            return;

        if (e.AddedItems.Count > 0 && e.AddedItems[0] is IKiPlugin plugin)
            vm.KiPluginSelectedCommand.Execute(plugin);
    }

    private void OnPasswordBoxLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox pb && pb.Tag is PluginSettingEntry entry)
            pb.Password = entry.Value ?? string.Empty;
    }

    private void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox pb && pb.Tag is PluginSettingEntry entry)
            entry.Value = pb.Password;
    }

    private void OnDateiAuswaehlenClick(object sender, RoutedEventArgs e)
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
