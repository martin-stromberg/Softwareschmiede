using System.Windows;
using System.Windows.Controls;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.App.Views;

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

    /// <summary>Template für Kommandozeilenparameter mit Hilfe-Button.</summary>
    public DataTemplate? CommandLineParametersTemplate { get; set; }

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
            PluginSettingFieldType.CommandLineParameters => CommandLineParametersTemplate,
            _ => TextTemplate
        } ?? base.SelectTemplate(item, container);
    }
}
