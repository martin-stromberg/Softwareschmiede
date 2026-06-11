using System.Windows.Controls;
using Softwareschmiede.App.ViewModels;

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
}
