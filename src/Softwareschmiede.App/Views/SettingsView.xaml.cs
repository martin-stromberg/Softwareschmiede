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

    /// <summary>
    /// Selektiert den ListBoxItem-Container bereits in der Tunneling-Phase, bevor die darin
    /// enthaltene CheckBox das MouseLeftButtonDown-Ereignis für ihre eigene Umschalt-Logik als
    /// behandelt markiert. Ohne dies erreicht der Klick niemals die bubbling-basierte
    /// Selektionslogik von <see cref="ListBoxItem"/>, sodass ein Klick direkt auf die CheckBox
    /// (z. B. per AutomationProperties.Name in E2E-Tests oder beim manuellen Anklicken) den
    /// Eintrag toggelt, aber nicht auswählt und somit auch nicht <see cref="OnPluginSelectionChanged"/>
    /// auslöst.
    /// </summary>
    /// <param name="sender">Der <see cref="ListBoxItem"/>-Container, an dem der Handler registriert ist.</param>
    /// <param name="e">Die Ereignisargumente des Mausklicks.</param>
    private void OnPluginActivationItemPreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem { IsSelected: false } item)
            item.IsSelected = true;
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
