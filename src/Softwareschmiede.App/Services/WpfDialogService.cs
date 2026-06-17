using System.Windows;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.App.Views;

namespace Softwareschmiede.App.Services;

/// <summary>WPF-Implementierung von <see cref="IDialogService"/> mit MessageBox und eigenen Dialogen.</summary>
public sealed class WpfDialogService : IDialogService
{
    private readonly PluginSelectionDialogService _pluginSelectionDialogService;

    /// <inheritdoc cref="WpfDialogService"/>
    public WpfDialogService(PluginSelectionDialogService pluginSelectionDialogService)
    {
        _pluginSelectionDialogService = pluginSelectionDialogService;
    }

    /// <inheritdoc/>
    public bool BestaetigenDialog(string nachricht, string titel)
        => MessageBox.Show(
            nachricht,
            titel,
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning) == MessageBoxResult.Yes;

    /// <inheritdoc/>
    public bool RepositoryZuweisenDialog(RepositoryAssignViewModel viewModel)
        => System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            var dialog = new RepositoryAssignDialog(viewModel)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };
            return dialog.ShowDialog() == true;
        });

    /// <inheritdoc/>
    public Task<PluginSelectionResult> ShowPluginSelectionDialogAsync(
        IEnumerable<string> availablePlugins,
        string? currentSelection,
        CancellationToken ct = default)
        => _pluginSelectionDialogService.ShowPluginSelectionDialogAsync(availablePlugins, currentSelection, ct);
}
