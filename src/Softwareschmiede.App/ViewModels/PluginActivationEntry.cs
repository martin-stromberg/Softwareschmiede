using Softwareschmiede.Domain.Interfaces;

namespace Softwareschmiede.App.ViewModels;

/// <summary>Darstellbarer Listeneintrag im Plugins-Tab mit Aktivierungsstatus.</summary>
public sealed class PluginActivationEntry : ViewModelBase
{
    private bool _isEnabled;

    /// <inheritdoc cref="PluginActivationEntry"/>
    /// <param name="plugin">Das zugehörige Plugin.</param>
    /// <param name="isEnabled">Der aktuelle Aktivierungsstatus des Plugins.</param>
    public PluginActivationEntry(IPlugin plugin, bool isEnabled)
    {
        Plugin = plugin;
        PluginName = plugin.PluginName;
        PluginPrefix = plugin.PluginPrefix;
        _isEnabled = isEnabled;
    }

    /// <summary>Das zugehörige Plugin.</summary>
    public IPlugin Plugin { get; }

    /// <summary>Anzeigename des Plugins.</summary>
    public string PluginName { get; }

    /// <summary>Eindeutiger Prefix des Plugins.</summary>
    public string PluginPrefix { get; }

    /// <summary>Aktivierungsstatus des Plugins (bindbar).</summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }
}
