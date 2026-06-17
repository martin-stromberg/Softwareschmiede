using Softwareschmiede.App.ViewModels;
using Softwareschmiede.App.Views;

namespace Softwareschmiede.App.Services;

/// <summary>Kapselt die Anzeige des Plugin-Auswahl-Dialogs (WPF-Technologiedetails).</summary>
public sealed class PluginSelectionDialogService
{
    /// <summary>Zeigt den Plugin-Auswahl-Dialog und gibt das Ergebnis der Benutzerauswahl zurück.</summary>
    public async Task<PluginSelectionResult> ShowPluginSelectionDialogAsync(
        IEnumerable<string> availablePlugins,
        string? currentSelection,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var dispatcherOperation = System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            ct.ThrowIfCancellationRequested();
            var viewModel = new PluginSelectionDialogViewModel(availablePlugins, currentSelection);
            var dialog = new PluginSelectionDialog(viewModel)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };

            var confirmed = dialog.ShowDialog() == true;
            return confirmed
                ? new PluginSelectionResult(viewModel.SelectedPluginPrefix, viewModel.SaveAsProjectDefault)
                : new PluginSelectionResult(null, false);
        });

        return await dispatcherOperation.Task.WaitAsync(ct);
    }
}
