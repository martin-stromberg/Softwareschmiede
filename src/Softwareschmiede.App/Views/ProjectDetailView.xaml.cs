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
        Loaded += (_, _) => ProjektNameTextBox.Focus();
    }

    private void AufgabeDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem { DataContext: Aufgabe aufgabe }
            && DataContext is ProjectDetailViewModel vm)
        {
            vm.AufgabeOeffnenCommand.Execute(aufgabe.Id);
        }
    }

    private void IssueDoubleClick(object sender, MouseButtonEventArgs e)
    {
        try
        {
            if (sender is ListBoxItem { DataContext: Issue issue }
                && DataContext is ProjectDetailViewModel vm)
            {
                vm.AufgabeAusIssueErstellenCommand.ExecuteAsync(issue)
                    .SafeFireAndForget(_logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance,
                        "ProjectDetailView.AufgabeAusIssueErstellenCommand");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Fehler in IssueDoubleClick");
        }
    }
}
