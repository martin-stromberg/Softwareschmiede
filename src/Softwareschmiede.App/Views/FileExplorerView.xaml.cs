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
            _subscribedViewModel.DiffLineFocused -= OnDiffLineFocused;

        _subscribedViewModel = e.NewValue as FileExplorerViewModel;

        if (_subscribedViewModel is not null)
            _subscribedViewModel.DiffLineFocused += OnDiffLineFocused;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_subscribedViewModel is not null)
            _subscribedViewModel.DiffLineFocused -= OnDiffLineFocused;

        _subscribedViewModel = null;
    }

    private void OnDiffLineFocused(int index)
    {
        DiffViewerControl.ScrollToIndex(index);
    }

    private void OnTreeSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is FileExplorerViewModel vm)
            vm.SelectedNode = e.NewValue as WorkspaceFileNode;
    }

    private void OnCommitNodeExpanded(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is TreeViewItem { DataContext: BranchCommit commit }
            && DataContext is FileExplorerViewModel vm)
        {
            vm.ExpandCommitAsync(commit).SafeFireAndForget(_logger, "FileExplorerView.CommitAufklappenAsync");
        }
    }

    private void OnTreeNodeExpanded(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is TreeViewItem { DataContext: WorkspaceFileNode node }
            && DataContext is FileExplorerViewModel vm)
        {
            vm.LoadChildrenAsync(node).SafeFireAndForget(_logger, "FileExplorerView.LadeKinderAsync");
        }
    }

    private void OnTreeNodeCollapsed(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is TreeViewItem { DataContext: WorkspaceFileNode node }
            && DataContext is FileExplorerViewModel vm)
        {
            vm.BeraeumeNode(node);
        }
    }
}
