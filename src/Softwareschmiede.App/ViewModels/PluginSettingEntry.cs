using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.App.ViewModels;

/// <summary>Repräsentiert eine bearbeitbare Einstellung eines Plugins.</summary>
public sealed class PluginSettingEntry : ViewModelBase
{
    private string _value = string.Empty;
    private bool _boolValue;

    /// <summary>Feld-Definition des Einstellungsfelds.</summary>
    public PluginSettingField Field { get; }

    /// <summary>Feldt-Typ des Einstellungsfelds (Shortcut zu Field.FieldType).</summary>
    public PluginSettingFieldType FieldType => Field.FieldType;

    /// <summary>Aktueller Wert des Felds als Zeichenkette.</summary>
    public string Value
    {
        get => _value;
        set
        {
            if (SetProperty(ref _value, value) && Field.FieldType == PluginSettingFieldType.Boolean)
            {
                _boolValue = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
                OnPropertyChanged(nameof(BoolValue));
            }
        }
    }

    /// <summary>Aktueller Wert als Boolean (für Checkbox-Binding bei FieldType == Boolean).</summary>
    public bool BoolValue
    {
        get => _boolValue;
        set
        {
            if (SetProperty(ref _boolValue, value))
            {
                _value = value ? "true" : "false";
                OnPropertyChanged(nameof(Value));
            }
        }
    }

    /// <inheritdoc cref="PluginSettingEntry"/>
    public PluginSettingEntry(PluginSettingField field, string? currentValue)
    {
        Field = field;
        _value = currentValue ?? string.Empty;
        _boolValue = string.Equals(_value, "true", StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>Repräsentiert eine Plugin-Einstellungsgruppe mit ihren Feldern.</summary>
public sealed class PluginSettingGroupEntry
{
    /// <summary>Name der Gruppe.</summary>
    public string GroupName { get; }

    /// <summary>Felder der Gruppe als bearbeitbare Einträge.</summary>
    public IReadOnlyList<PluginSettingEntry> Entries { get; }

    /// <inheritdoc cref="PluginSettingGroupEntry"/>
    public PluginSettingGroupEntry(string groupName, IReadOnlyList<PluginSettingEntry> entries)
    {
        GroupName = groupName;
        Entries = entries;
    }
}
