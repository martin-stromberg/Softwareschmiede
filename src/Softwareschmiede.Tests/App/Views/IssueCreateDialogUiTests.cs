using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.App.Views;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.App.Views;

/// <summary>UI-Level-Tests für den Issue-Anlage-Dialog.</summary>
public sealed class IssueCreateDialogUiTests
{
    /// <summary>Die KI-Plugin-Auswahl zeigt alle verfügbaren Textgeneratoren an.</summary>
    [Fact]
    public void IssueCreateDialog_ShouldListAllKiPluginsInSelection()
    {
        WpfUnitTestHelpers.RunOnSta(() =>
        {
            var pluginManagerMock = new Mock<IPluginManager>();
            var plugins = new[]
            {
                new UiTestKiPlugin("Softwareschmiede.Codex"),
                new UiTestKiPlugin("Softwareschmiede.ClaudeCli"),
                new UiTestKiPlugin("Softwareschmiede.Devin"),
                new UiTestKiPlugin("Softwareschmiede.GitHubCopilot"),
            };
            pluginManagerMock.Setup(p => p.GetDevelopmentAutomationPlugins()).Returns(plugins);

            var issueProvider = new UiTestIssueProvider();
            var templateProvider = new UiTestTemplateProvider();
            var viewModel = new IssueCreateDialogViewModel(
                pluginManagerMock.Object,
                NullLogger<IssueCreateDialogViewModel>.Instance);
            viewModel.Initialize(
                issueProvider,
                templateProvider,
                "owner/repo",
                "Aufgabe",
                "Original",
                "Softwareschmiede.Devin");
            viewModel.SelectedTemplate = new IssueTemplate("Bug", "Template");

            var dialog = new IssueCreateDialog(viewModel);
            dialog.Show();
            dialog.UpdateLayout();

            var comboBox = FindComboBox(dialog, "IssueKiProvider");
            comboBox.Should().NotBeNull();
            comboBox!.ItemsSource.Should().BeEquivalentTo(new[]
            {
                "Softwareschmiede.Codex",
                "Softwareschmiede.ClaudeCli",
                "Softwareschmiede.Devin",
                "Softwareschmiede.GitHubCopilot"
            });
            comboBox.SelectedItem.Should().Be("Softwareschmiede.Devin");

            dialog.Close();
        });
    }

    private static ComboBox? FindComboBox(DependencyObject parent, string automationName)
    {
        if (parent is ComboBox comboBox
            && AutomationProperties.GetName(comboBox) == automationName)
        {
            return comboBox;
        }

        foreach (var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())
        {
            var found = FindComboBox(child, automationName);
            if (found is not null)
                return found;
        }

        return null;
    }

    private sealed class UiTestIssueProvider : IIssueCreateProvider
    {
        public Task<bool> CanCreateIssueAsync(string repositoryId, CancellationToken ct = default) => Task.FromResult(true);

        public Task<IssueCreateResult> CreateIssueAsync(string repositoryId, IssueCreateRequest request, CancellationToken ct = default)
            => Task.FromResult(IssueCreateResult.Success(new Issue(1, request.Title, request.Body, [], null, "https://example.test/issues/1")));
    }

    private sealed class UiTestTemplateProvider : IIssueTemplateProvider
    {
        public Task<IssueTemplateLoadResult> GetIssueTemplatesAsync(string repositoryId, CancellationToken ct = default)
            => Task.FromResult(IssueTemplateLoadResult.Success([]));
    }

    private sealed class UiTestKiPlugin : IKiPlugin, IIssueTemplateTextGenerator
    {
        public UiTestKiPlugin(string prefix)
        {
            PluginPrefix = prefix;
        }

        public string PluginName => PluginPrefix;
        public string PluginPrefix { get; }
        public PluginType PluginType => PluginType.DevelopmentAutomation;

        public Task<string> FillIssueTemplateAsync(string templateBody, string? originalRequirement, CancellationToken ct = default)
            => Task.FromResult($"Ergebnis für {PluginPrefix}");

        public IReadOnlyList<PluginSettingGroup> GetSettingGroups() => [];
        public Task<ProcessStartInfo> StartCliAsync(string localRepoPath, string? parameters = null, CancellationToken ct = default)
            => Task.FromResult(new ProcessStartInfo());
        public string GetProcessWindowTitle(Guid aufgabeId) => PluginName;
        public bool SupportsSessionContinuation() => false;
        public Task<bool> CheckHealthAsync(CancellationToken ct = default) => Task.FromResult(true);
    }
}
