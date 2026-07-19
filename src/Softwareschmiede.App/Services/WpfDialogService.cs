using System.Windows;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.App.Views;
using Softwareschmiede.Domain.ValueObjects;

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
    public bool ArbeitsverzeichnisBearbeitenDialog(ArbeitsverzeichnisBearbeitenViewModel viewModel)
        => System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            var dialog = new ArbeitsverzeichnisBearbeitenDialog(viewModel)
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

    /// <inheritdoc/>
    public Task<Issue?> ShowIssueSelectionDialogAsync(
        IssueSelectionDialogViewModel viewModel,
        CancellationToken ct = default)
    {
        return System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            var dialog = new IssueSelectionDialog(viewModel)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };
            var result = dialog.ShowDialog();
            return result == true ? viewModel.SelectedIssue : null;
        }).Task;
    }

    /// <inheritdoc/>
    public Task<string?> ShowSolutionSelectionDialogAsync(
        IReadOnlyList<string> solutionPfade,
        CancellationToken ct = default)
    {
        return System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            var viewModel = new SolutionSelectionDialogViewModel(solutionPfade);
            var dialog = new SolutionSelectionDialog(viewModel)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };
            var result = dialog.ShowDialog();
            return result == true ? viewModel.SelectedSolution : null;
        }).Task;
    }
}
