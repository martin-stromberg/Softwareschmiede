using System.Windows;
using Microsoft.Win32;
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
    public bool ConfirmDialog(string message, string title)
        => MessageBox.Show(
            message,
            title,
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning) == MessageBoxResult.Yes;

    /// <inheritdoc/>
    public bool RepositoryAssignDialog(RepositoryAssignViewModel viewModel)
        => System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            var dialog = new RepositoryAssignDialog(viewModel)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };
            return dialog.ShowDialog() == true;
        });

    /// <inheritdoc/>
    public bool WorkingDirectoryEditDialog(WorkingDirectoryEditViewModel viewModel)
        => System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            var dialog = new WorkingDirectoryEditDialog(viewModel)
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
    public Task<IssueCreateDialogResult?> ShowIssueCreateDialogAsync(
        IssueCreateDialogViewModel viewModel,
        CancellationToken ct = default)
    {
        return System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            var dialog = new IssueCreateDialog(viewModel)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };
            var result = dialog.ShowDialog();
            return result == true && viewModel.CreatedIssue is not null
                ? new IssueCreateDialogResult(
                    viewModel.CreatedIssue,
                    viewModel.UpdateTaskDescriptionAfterCreate,
                    viewModel.Body)
                : null;
        }).Task;
    }

    /// <inheritdoc/>
    public Task ShowOpenTodosDialogAsync(
        OpenTodosDialogViewModel viewModel,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            ct.ThrowIfCancellationRequested();
            var dialog = new OpenTodosDialog(viewModel)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };
            dialog.ShowDialog();
        }).Task;
    }

    /// <inheritdoc/>
    public Task<string?> ShowSolutionSelectionDialogAsync(
        IReadOnlyList<string> solutionPath,
        CancellationToken ct = default)
    {
        return System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            var viewModel = new SolutionSelectionDialogViewModel(solutionPath);
            var dialog = new SolutionSelectionDialog(viewModel)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };
            var result = dialog.ShowDialog();
            return result == true ? viewModel.SelectedSolution : null;
        }).Task;
    }

    /// <inheritdoc/>
    public Task<PauseWorkItemResult?> ShowWorkItemPauseDialogAsync(
        PauseWorkItemDialogViewModel viewModel,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return ShowDialogAsync(
            () => new PauseWorkItemDialog(viewModel),
            () => viewModel.Result);
    }

    /// <inheritdoc/>
    public Task<string?> ShowSaveFileDialogAsync(
        string title,
        string filter,
        string defaultFileName,
        string? initialDirectory = null,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        return System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            ct.ThrowIfCancellationRequested();

            var dialog = new SaveFileDialog
            {
                Title = title,
                Filter = filter,
                FileName = defaultFileName
            };

            if (!string.IsNullOrWhiteSpace(initialDirectory))
                dialog.InitialDirectory = initialDirectory;

            var result = dialog.ShowDialog(ActiveDialogOwnerWindow());
            return result == true ? dialog.FileName : null;
        }).Task;
    }

    /// <inheritdoc/>
    public Task<string?> ShowOpenFileDialogAsync(
        string title,
        string filter,
        string? initialDirectory = null,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        return System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            ct.ThrowIfCancellationRequested();

            var dialog = new OpenFileDialog
            {
                Title = title,
                Filter = filter
            };

            if (!string.IsNullOrWhiteSpace(initialDirectory))
                dialog.InitialDirectory = initialDirectory;

            var result = dialog.ShowDialog(ActiveDialogOwnerWindow());
            return result == true ? dialog.FileName : null;
        }).Task;
    }

    /// <inheritdoc/>
    public Task ShowConsoleTestDialogAsync(
        ConsoleTestViewModel viewModel,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            ct.ThrowIfCancellationRequested();
            // Nicht-modal: das Diagnose-Fenster bleibt parallel zur Live-Ansicht nutzbar
            // (Vergleich Replay ↔ laufende Ausgabe). Der Lebenszyklus des ViewModels wird
            // über den Closed-Handler des Fensters disponiert.
            var dialog = new ConsoleTestDialog(viewModel)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };
            dialog.Show();
        }).Task;
    }

    /// <summary>Liefert das aktuell aktive Fenster als Dialog-Owner — bei Aufrufen aus einem
    /// nicht-modalen Fenster (z. B. dem Konsolentestfenster) ist das modal geöffnete
    /// Datei-Dialogfenster sonst falsch zugeordnet (Owner=MainWindow liegt hinter dem
    /// geöffneten Owned-Window) und erhält keinen zuverlässigen Fokus.</summary>
    private static Window ActiveDialogOwnerWindow()
        => System.Windows.Application.Current.Windows
            .OfType<Window>()
            .FirstOrDefault(w => w.IsActive)
            ?? System.Windows.Application.Current.MainWindow;

    /// <summary>Erzeugt über <paramref name="dialogFactory"/> ein Dialogfenster, zeigt es modal an und liefert das über <paramref name="resultSelector"/> bestimmte Ergebnis.</summary>
    private static Task<TResult?> ShowDialogAsync<TResult>(Func<Window> dialogFactory, Func<TResult?> resultSelector)
    {
        return System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            var dialog = dialogFactory();
            dialog.Owner = System.Windows.Application.Current.MainWindow;
            var result = dialog.ShowDialog();
            return result == true ? resultSelector() : default;
        }).Task;
    }
}
