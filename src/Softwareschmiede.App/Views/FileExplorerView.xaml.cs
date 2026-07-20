using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.App.Views;

/// <summary>Code-behind für FileExplorerView.</summary>
public sealed partial class FileExplorerView : UserControl
{
    private readonly ILogger _logger = (ILogger?)App.Services?.GetService<ILogger<FileExplorerView>>() ?? NullLogger.Instance;
    private FileExplorerViewModel? _subscribedViewModel;

    /// <inheritdoc cref="FileExplorerView"/>
    public FileExplorerView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Unloaded += OnUnloaded;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_subscribedViewModel is not null)
            _subscribedViewModel.DiffZeileFokussiert -= OnDiffZeileFokussiert;

        _subscribedViewModel = e.NewValue as FileExplorerViewModel;

        if (_subscribedViewModel is not null)
            _subscribedViewModel.DiffZeileFokussiert += OnDiffZeileFokussiert;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_subscribedViewModel is not null)
            _subscribedViewModel.DiffZeileFokussiert -= OnDiffZeileFokussiert;

        _subscribedViewModel = null;
    }

    private void OnDiffZeileFokussiert(int index)
    {
        DiffViewerControl.ScrollToIndex(index);
    }

    private void OnBaumSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is FileExplorerViewModel vm)
            vm.AusgewaehlterKnoten = e.NewValue as WorkspaceFileNode;
    }

    private void OnCommitKnotenExpanded(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is TreeViewItem { DataContext: BranchCommit commit }
            && DataContext is FileExplorerViewModel vm)
        {
            vm.CommitAufklappenAsync(commit).SafeFireAndForget(_logger, "FileExplorerView.CommitAufklappenAsync");
        }
    }

    private void OnBaumKnotenExpanded(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is TreeViewItem { DataContext: WorkspaceFileNode node }
            && DataContext is FileExplorerViewModel vm)
        {
            vm.LadeKinderAsync(node).SafeFireAndForget(_logger, "FileExplorerView.LadeKinderAsync");
        }
    }

    private void OnBaumKnotenCollapsed(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is TreeViewItem { DataContext: WorkspaceFileNode node }
            && DataContext is FileExplorerViewModel vm)
        {
            vm.BeraeumeKnoten(node);
        }
    }
}
