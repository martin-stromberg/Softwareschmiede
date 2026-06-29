using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.Domain.Abstractions;
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

    private async void OnHilfeButtonClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel vm) return;
        var plugin = vm.KiPlugins.FirstOrDefault(p => p.PluginName == vm.DefaultKiPlugin);
        if (plugin is not CliKiPluginBase cliPlugin) return;
        var element = sender as FrameworkElement;
        if (element is not null) element.IsEnabled = false;
        try
        {
            var helpText = await cliPlugin.GetCliHelpTextAsync();
            var text = helpText ?? "Hilfe nicht verfügbar: Kommandozeilen-Tool nicht erreichbar.";
            var dialog = new HelpTextDialog(text) { Owner = Window.GetWindow(this) };
            dialog.ShowDialog();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _ = ex;
            var dialog = new HelpTextDialog("Hilfe nicht verfügbar: Fehler beim Abrufen des Hilfetexts.") { Owner = Window.GetWindow(this) };
            dialog.ShowDialog();
        }
        finally
        {
            if (element is not null) element.IsEnabled = true;
        }
    }
}
