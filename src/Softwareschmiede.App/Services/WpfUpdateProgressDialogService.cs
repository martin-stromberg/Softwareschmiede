using Softwareschmiede.App.ViewModels;
using Softwareschmiede.App.Views;

namespace Softwareschmiede.App.Services;

/// <summary>WPF-Implementierung des Update-Fortschrittsdialogs.</summary>
public sealed class WpfUpdateProgressDialogService : IUpdateProgressDialogService
{
    private readonly Dictionary<UpdateProgressViewModel, UpdateProgressDialog> _dialogs = [];

    /// <inheritdoc/>
    public void Show(UpdateProgressViewModel viewModel)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            if (_dialogs.ContainsKey(viewModel))
                return;

            var dialog = new UpdateProgressDialog
            {
                DataContext = viewModel,
                Owner = System.Windows.Application.Current.MainWindow
            };
            _dialogs[viewModel] = dialog;
            dialog.Closed += (_, _) => _dialogs.Remove(viewModel);
            dialog.Show();
        });
    }

    /// <inheritdoc/>
    public void Close(UpdateProgressViewModel viewModel)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            if (_dialogs.TryGetValue(viewModel, out var dialog))
            {
                dialog.Close();
            }
        });
    }
}
