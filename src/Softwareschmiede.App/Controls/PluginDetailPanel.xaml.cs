using System.Windows;
using System.Windows.Controls;

namespace Softwareschmiede.App.Controls;

/// <summary>
/// Wiederverwendbares Detail-Panel für ein ausgewähltes Plugin (SCM/KI- oder IDE-Register): zeigt Titel,
/// "Plugin aktiviert"-Checkbox und die Einstellungsgruppen des Plugins an. Wird in <c>SettingsView.xaml</c>
/// je einmal für die SCM/KI- und die IDE-Plugin-Details instanziiert, um die zuvor doppelt vorhandene
/// StackPanel-Struktur zu vermeiden.
/// </summary>
public sealed partial class PluginDetailPanel : UserControl
{
    /// <summary>DependencyProperty für die Titelzeile (Plugin-Name).</summary>
    public static readonly DependencyProperty HeaderTextProperty =
        DependencyProperty.Register(
            nameof(HeaderText),
            typeof(string),
            typeof(PluginDetailPanel),
            new PropertyMetadata(string.Empty));

    /// <summary>DependencyProperty für den Aktiviert-Status der "Plugin aktiviert"-Checkbox (TwoWay-Binding).</summary>
    public static readonly DependencyProperty IsEnabledCheckedProperty =
        DependencyProperty.Register(
            nameof(IsEnabledChecked),
            typeof(bool),
            typeof(PluginDetailPanel),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    /// <summary>DependencyProperty für die Datenquelle der Einstellungsgruppen (ItemsControl.ItemsSource).</summary>
    public static readonly DependencyProperty SettingsSourceProperty =
        DependencyProperty.Register(
            nameof(SettingsSource),
            typeof(object),
            typeof(PluginDetailPanel),
            new PropertyMetadata(null));

    /// <summary>DependencyProperty für das ItemTemplate der Einstellungsgruppen (z. B. das gemeinsame PluginSettingGroupsItemTemplate).</summary>
    public static readonly DependencyProperty SettingsItemTemplateProperty =
        DependencyProperty.Register(
            nameof(SettingsItemTemplate),
            typeof(DataTemplate),
            typeof(PluginDetailPanel),
            new PropertyMetadata(null));

    /// <summary>DependencyProperty für den AutomationProperties.Name-Wert der "Plugin aktiviert"-Checkbox.</summary>
    public static readonly DependencyProperty CheckboxAutomationNameProperty =
        DependencyProperty.Register(
            nameof(CheckboxAutomationName),
            typeof(string),
            typeof(PluginDetailPanel),
            new PropertyMetadata(string.Empty));

    /// <summary>Titelzeile (Plugin-Name), die oberhalb der Checkbox angezeigt wird.</summary>
    public string HeaderText
    {
        get => (string)GetValue(HeaderTextProperty);
        set => SetValue(HeaderTextProperty, value);
    }

    /// <summary>Aktiviert-Status der "Plugin aktiviert"-Checkbox (TwoWay-Binding an die zugrunde liegende Plugin-Einstellung).</summary>
    public bool IsEnabledChecked
    {
        get => (bool)GetValue(IsEnabledCheckedProperty);
        set => SetValue(IsEnabledCheckedProperty, value);
    }

    /// <summary>Datenquelle der Einstellungsgruppen für die ItemsControl.</summary>
    public object? SettingsSource
    {
        get => GetValue(SettingsSourceProperty);
        set => SetValue(SettingsSourceProperty, value);
    }

    /// <summary>ItemTemplate der Einstellungsgruppen (z. B. das gemeinsame PluginSettingGroupsItemTemplate).</summary>
    public DataTemplate? SettingsItemTemplate
    {
        get => (DataTemplate?)GetValue(SettingsItemTemplateProperty);
        set => SetValue(SettingsItemTemplateProperty, value);
    }

    /// <summary>AutomationProperties.Name-Wert der "Plugin aktiviert"-Checkbox, damit E2E-Tests sie gezielt ansteuern können.</summary>
    public string CheckboxAutomationName
    {
        get => (string)GetValue(CheckboxAutomationNameProperty);
        set => SetValue(CheckboxAutomationNameProperty, value);
    }

    /// <inheritdoc cref="PluginDetailPanel"/>
    public PluginDetailPanel()
    {
        InitializeComponent();
    }
}
