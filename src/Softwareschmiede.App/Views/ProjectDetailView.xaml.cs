using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.App.Views;

/// <summary>Code-behind für ProjectDetailView.</summary>
public sealed partial class ProjectDetailView : UserControl
{
    private readonly ILogger? _logger = App.Services?.GetService<ILogger<ProjectDetailView>>();

    /// <inheritdoc cref="ProjectDetailView"/>
    public ProjectDetailView()
    {
        InitializeComponent();
        Loaded += (_, _) => ProjectNameTextBox.Focus();
    }

    private void WorkItemDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem { DataContext: WorkItem workItem }
            && DataContext is ProjectDetailViewModel vm)
        {
            vm.OpenWorkItemCommand.Execute(workItem.Id);
        }
    }

    private void IssueDoubleClick(object sender, MouseButtonEventArgs e)
    {
        try
        {
            if (sender is ListBoxItem { DataContext: ScmRequirement requirement }
                && DataContext is ProjectDetailViewModel vm)
            {
                vm.CreateWorkItemFromRequirementCommand.ExecuteAsync(requirement)
                    .SafeFireAndForget(_logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance,
                        "ProjectDetailView.CreateWorkItemFromRequirementCommand");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Fehler in IssueDoubleClick");
        }
    }
}
