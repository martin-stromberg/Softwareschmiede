using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Components.Pages.Aufgaben;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Components.Pages.Aufgaben;

public sealed class NeueAufgabeBunitTests : TestContext
{
    /// <summary>Prüft den Flow: Issues laden, Issue auswählen, Aufgabe aus Issue erstellen und zur Detailseite navigieren.</summary>
    [Fact]
    public async Task NeueAufgabe_ShouldLoadIssuesAndCreateTaskFromSelectedIssue()
    {
        // Arrange
        var issue = new Issue(11, "Issue Titel", "Issue Body", ["bug"], null, "https://github.com/test/repo/issues/11");
        await using var harness = await ConfigureComponentServicesAsync([issue], null);
        var navigationManager = Services.GetRequiredService<FakeNavigationManager>();

        // Act
        var cut = RenderComponent<NeueAufgabe>(parameters => parameters.Add(page => page.ProjektId, harness.ProjektId));
        cut.WaitForAssertion(() => cut.FindAll("option").Any(option => option.TextContent.Contains("#11: Issue Titel", StringComparison.Ordinal)).Should().BeTrue());
        cut.Find("select").Change("11");
        cut.Find("button.btn.btn-primary").Click();

        // Assert
        cut.WaitForAssertion(() => navigationManager.Uri.Should().Contain("/aufgaben/"));
        cut.WaitForAssertion(() =>
        {
            var aufgabe = harness.Db.Aufgaben.Include(a => a.IssueReferenz).Single();
            aufgabe.Titel.Should().Be("Issue Titel");
            aufgabe.AnforderungsBeschreibung.Should().Be("Issue Body");
            aufgabe.GitRepositoryId.Should().Be(harness.RepositoryId);
            aufgabe.IssueReferenz.Should().NotBeNull();
            aufgabe.IssueReferenz!.IssueNummer.Should().Be(11);
        });

        harness.GitPluginMock.Verify(plugin => plugin.GetIssuesAsync("owner/repo", It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Prüft, dass bei Fehlern beim Abrufen der Issues eine Warnung angezeigt wird.</summary>
    [Fact]
    public async Task NeueAufgabe_ShouldShowWarning_WhenIssuesAbrufenAsyncThrows()
    {
        // Arrange
        await using var harness = await ConfigureComponentServicesAsync([], new InvalidOperationException("boom"));

        // Act
        var cut = RenderComponent<NeueAufgabe>(parameters => parameters.Add(page => page.ProjektId, harness.ProjektId));

        // Assert
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Issues konnten nicht geladen werden: boom"));
        cut.Markup.Should().Contain("alert-warning");
    }

    private async Task<TestHarness> ConfigureComponentServicesAsync(
        IEnumerable<Issue> issues,
        Exception? issuesException)
    {
        var db = TestDbContextFactory.Create();
        var projektService = new ProjektService(db, NullLogger<ProjektService>.Instance);
        var aufgabeService = new AufgabeService(db, NullLogger<AufgabeService>.Instance);
        var protokollService = new ProtokollService(db, NullLogger<ProtokollService>.Instance);

        var projekt = await projektService.CreateAsync("NeueAufgabe BUnit Projekt", null);
        var repository = await projektService.AddRepositoryAsync(
            projekt.Id,
            "GitHub",
            "https://github.com/owner/repo",
            "owner/repo");

        var gitPluginMock = new Mock<IGitPlugin>();
        gitPluginMock.SetupGet(plugin => plugin.PluginName).Returns("GitHub");
        gitPluginMock.SetupGet(plugin => plugin.PluginPrefix).Returns("Softwareschmiede.GitHub");
        gitPluginMock.SetupGet(plugin => plugin.PluginType).Returns(PluginType.SourceCodeManagement);
        gitPluginMock.Setup(plugin => plugin.GetSettingGroups()).Returns([]);
        if (issuesException is null)
        {
            gitPluginMock.Setup(plugin => plugin.GetIssuesAsync("owner/repo", It.IsAny<CancellationToken>()))
                .ReturnsAsync(issues);
        }
        else
        {
            gitPluginMock.Setup(plugin => plugin.GetIssuesAsync("owner/repo", It.IsAny<CancellationToken>()))
                .ThrowsAsync(issuesException);
        }

        var kiPluginMock = new Mock<IKiPlugin>();
        kiPluginMock.SetupGet(plugin => plugin.PluginName).Returns("KI");
        kiPluginMock.SetupGet(plugin => plugin.PluginPrefix).Returns("Softwareschmiede.TestKi");
        kiPluginMock.SetupGet(plugin => plugin.PluginType).Returns(PluginType.DevelopmentAutomation);
        kiPluginMock.Setup(plugin => plugin.GetSettingGroups()).Returns([]);

        var pluginManagerMock = new Mock<IPluginManager>();
        pluginManagerMock.Setup(manager => manager.GetSourceCodeManagementPlugins()).Returns([gitPluginMock.Object]);
        pluginManagerMock.Setup(manager => manager.GetDefaultSourceCodeManagementPlugin()).Returns(gitPluginMock.Object);
        pluginManagerMock.Setup(manager => manager.GetDevelopmentAutomationPlugins()).Returns([kiPluginMock.Object]);
        pluginManagerMock.Setup(manager => manager.GetDefaultDevelopmentAutomationPlugin()).Returns(kiPluginMock.Object);

        var pluginDefaultSettingsService = new PluginDefaultSettingsService(db, NullLogger<PluginDefaultSettingsService>.Instance);
        var pluginSelectionService = new PluginSelectionService(
            pluginManagerMock.Object,
            pluginDefaultSettingsService,
            NullLogger<PluginSelectionService>.Instance);

        var gitService = new GitOrchestrationService(
            aufgabeService,
            projektService,
            protokollService,
            gitPluginMock.Object,
            pluginSelectionService,
            NullLogger<GitOrchestrationService>.Instance);

        Services.AddSingleton(aufgabeService);
        Services.AddSingleton(projektService);
        Services.AddSingleton(gitService);

        return new TestHarness(db, gitPluginMock, projekt.Id, repository.Id);
    }

    private sealed class TestHarness(
        Softwareschmiede.Infrastructure.Data.SoftwareschmiededDbContext db,
        Mock<IGitPlugin> gitPluginMock,
        Guid projektId,
        Guid repositoryId) : IAsyncDisposable
    {
        public Softwareschmiede.Infrastructure.Data.SoftwareschmiededDbContext Db { get; } = db;
        public Mock<IGitPlugin> GitPluginMock { get; } = gitPluginMock;
        public Guid ProjektId { get; } = projektId;
        public Guid RepositoryId { get; } = repositoryId;

        public ValueTask DisposeAsync()
        {
            Db.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
