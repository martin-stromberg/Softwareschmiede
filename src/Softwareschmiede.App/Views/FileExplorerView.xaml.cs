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

    /// <inheritdoc cref="FileExplorerView"/>
    public FileExplorerView()
    {
        InitializeComponent();
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
}
