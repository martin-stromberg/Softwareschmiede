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
        if (!TryGetViewModelAndFirstAddedItem(e, out var vm, out var item))
            return;

        switch (item)
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

    /// <summary>Selektions-Handler für die IDE-Plugins-Aktivierungsliste im Plugins-Register.</summary>
    /// <param name="sender">Das auslösende Steuerelement.</param>
    /// <param name="e">Die Ereignisargumente mit dem neu ausgewählten Element.</param>
    private void OnIdePluginSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!TryGetViewModelAndFirstAddedItem(e, out var vm, out var item))
            return;

        if (item is PluginActivationEntry entry)
            vm.IdePluginSelectedCommand.Execute(entry);
    }

    /// <summary>
    /// Gemeinsame Guard-Prüfung für die Selektions-Handler der Plugin-Auswahlsteuerelemente: liefert das
    /// <see cref="SettingsViewModel"/> des <c>DataContext</c> sowie das zuerst neu hinzugefügte Element
    /// der Selektionsänderung, sofern beides vorhanden ist.
    /// </summary>
    /// <param name="e">Die Ereignisargumente mit dem neu ausgewählten Element.</param>
    /// <param name="vm">Das ermittelte ViewModel, oder <c>null</c>, falls die Prüfung fehlschlägt.</param>
    /// <param name="item">Das zuerst neu hinzugefügte Element, oder <c>null</c>, falls die Prüfung fehlschlägt.</param>
    /// <returns><c>true</c>, wenn sowohl ViewModel als auch ein neu hinzugefügtes Element vorhanden sind, sonst <c>false</c>.</returns>
    private bool TryGetViewModelAndFirstAddedItem(SelectionChangedEventArgs e, out SettingsViewModel vm, out object item)
    {
        if (DataContext is SettingsViewModel viewModel && e.AddedItems.Count > 0)
        {
            vm = viewModel;
            item = e.AddedItems[0]!;
            return true;
        }

        vm = null!;
        item = null!;
        return false;
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
        var cliPlugin = ResolveCliPluginForHelp(vm);
        if (cliPlugin is null) return;
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

    internal static CliKiPluginBase? ResolveCliPluginForHelp(SettingsViewModel vm)
    {
        var plugin = vm.SelectedPlugin?.Plugin as IKiPlugin
            ?? vm.KiPlugins.FirstOrDefault(p => p.PluginName == vm.DefaultKiPlugin);
        return plugin as CliKiPluginBase;
    }
}
