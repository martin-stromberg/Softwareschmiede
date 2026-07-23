using System;
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

    /// <summary>
    /// Gemeinsamer Selektions-Handler für alle drei Plugin-Auswahlsteuerelemente im Plugins-Register
    /// (Standard-SCM-ComboBox, Standard-KI-ComboBox, Aktivierungslisten). Leitet das ausgewählte
    /// Element je nach Typ an das passende ViewModel-Kommando weiter.
    /// </summary>
    /// <param name="sender">Das auslösende Steuerelement.</param>
    /// <param name="e">Die Ereignisargumente mit dem neu ausgewählten Element.</param>
    private void OnPluginSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not SettingsViewModel vm || e.AddedItems.Count == 0)
            return;

        switch (e.AddedItems[0])
        {
            case IGitPlugin gitPlugin:
                vm.ScmPluginSelectedCommand.Execute(gitPlugin);
                break;
            case IKiPlugin kiPlugin:
                vm.KiPluginSelectedCommand.Execute(kiPlugin);
                break;
            case PluginActivationEntry entry:
                vm.PluginSelectedCommand.Execute(entry);
                break;
        }
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
