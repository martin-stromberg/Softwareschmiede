using System.Windows;
using System.Windows.Controls;
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
        => PluginSettingEntryEditHelper.OnPasswordBoxLoaded(sender, e);

    private void OnPasswordChanged(object sender, RoutedEventArgs e)
        => PluginSettingEntryEditHelper.OnPasswordChanged(sender, e);

    private void OnDateiAuswaehlenClick(object sender, RoutedEventArgs e)
        => PluginSettingEntryEditHelper.OnDateiAuswaehlenClick(sender, e);
}
