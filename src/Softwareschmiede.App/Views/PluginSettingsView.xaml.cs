using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.App.Views;

/// <summary>Code-behind für PluginSettingsView.</summary>
public sealed partial class PluginSettingsView : UserControl
{
    /// <inheritdoc cref="PluginSettingsView"/>
    public PluginSettingsView()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (DataContext is PluginSettingsViewModel vm)
                vm.LadenCommand.Execute(null);
        };
    }

    private void OnDateiAuswaehlenClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.DataContext is not PluginSettingEntry entry)
            return;

        var dialog = new OpenFileDialog
        {
            Title = entry.Field.Label,
            Filter = "Audiodateien (*.mp3;*.wav;*.ogg)|*.mp3;*.wav;*.ogg|Alle Dateien (*.*)|*.*"
        };

        if (!string.IsNullOrEmpty(entry.Value) && System.IO.File.Exists(entry.Value))
            dialog.InitialDirectory = System.IO.Path.GetDirectoryName(entry.Value);

        if (dialog.ShowDialog() == true)
            entry.Value = dialog.FileName;
    }
}

/// <summary>Wählt das passende DataTemplate für einen <see cref="PluginSettingEntry"/> anhand des FieldType.</summary>
public sealed class PluginSettingFieldTemplateSelector : DataTemplateSelector
{
    /// <summary>Template für einzeiligen Text.</summary>
    public DataTemplate? TextTemplate { get; set; }

    /// <summary>Template für maskierte Passwort-/Token-Eingabe.</summary>
    public DataTemplate? SecretTemplate { get; set; }

    /// <summary>Template für URL-Eingabe.</summary>
    public DataTemplate? UrlTemplate { get; set; }

    /// <summary>Template für Ganzzahl-Eingabe.</summary>
    public DataTemplate? IntegerTemplate { get; set; }

    /// <summary>Template für Boolean-Checkbox.</summary>
    public DataTemplate? BooleanTemplate { get; set; }

    /// <summary>Template für Enum-ComboBox.</summary>
    public DataTemplate? EnumTemplate { get; set; }

    /// <summary>Template für Dateipfad-Auswahl.</summary>
    public DataTemplate? FilePathTemplate { get; set; }

    /// <inheritdoc/>
    public override DataTemplate? SelectTemplate(object item, DependencyObject container)
    {
        if (item is not PluginSettingEntry entry)
            return base.SelectTemplate(item, container);

        return entry.FieldType switch
        {
            PluginSettingFieldType.Secret => SecretTemplate,
            PluginSettingFieldType.Integer => IntegerTemplate,
            PluginSettingFieldType.Boolean => BooleanTemplate,
            PluginSettingFieldType.Enum => EnumTemplate,
            PluginSettingFieldType.FilePath => FilePathTemplate,
            _ => TextTemplate
        } ?? base.SelectTemplate(item, container);
    }
}
