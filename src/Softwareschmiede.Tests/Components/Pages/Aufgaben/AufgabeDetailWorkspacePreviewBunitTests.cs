using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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

public sealed class AufgabeDetailWorkspacePreviewBunitTests : TestContext
{
    /// <summary>
    /// Verifiziert, dass bei schnellem Dateiwechsel nur die letzte Auswahl im Previewbereich angezeigt wird.
    /// </summary>
    [Fact]
    public async Task AufgabeDetail_ShouldKeepLatestPreview_WhenFilesAreSelectedQuickly()
    {
        await using var harness = await ConfigureComponentServicesAsync();
        var navigationManager = Services.GetRequiredService<NavigationManager>();
        var uri = navigationManager.GetUriWithQueryParameter("view", "tree");
        navigationManager.NavigateTo(uri);

        var cut = RenderComponent<AufgabeDetail>(parameters => parameters
            .Add(page => page.Id, harness.AufgabeId));

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("alpha.cs"));
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("beta.cs"));

        cut.FindAll(".tree-item").Single(item => item.TextContent.Contains("alpha.cs", StringComparison.Ordinal)).Click();
        cut.FindAll(".tree-item").Single(item => item.TextContent.Contains("beta.cs", StringComparison.Ordinal)).Click();

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Hint Beta");
            cut.Markup.Should().NotContain("Hint Alpha");
        }, timeout: TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task AufgabeDetail_ShouldShowPreviewErrorHint_WhenLoadPreviewThrows()
    {
        await using var harness = await ConfigureComponentServicesAsync(loadPreviewShouldThrow: true);
        var navigationManager = Services.GetRequiredService<NavigationManager>();
        var uri = navigationManager.GetUriWithQueryParameter("view", "tree");
        navigationManager.NavigateTo(uri);

        var cut = RenderComponent<AufgabeDetail>(parameters => parameters
            .Add(page => page.Id, harness.AufgabeId));

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("alpha.cs"));
        cut.FindAll(".tree-item").Single(item => item.TextContent.Contains("alpha.cs", StringComparison.Ordinal)).Click();

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Preview failed for alpha.cs");
            cut.Markup.Should().NotContain("Hint Alpha");
        });
    }

    private async Task<TestHarness> ConfigureComponentServicesAsync(bool loadPreviewShouldThrow = false)
    {
        var db = TestDbContextFactory.Create();

        var projekt = new Projekt
        {
            Id = Guid.NewGuid(),
            Name = "Workspace Preview Test Projekt",
            Status = ProjektStatus.Aktiv,
            ErstellungsDatum = DateTimeOffset.UtcNow,
        };

        var aufgabe = new Aufgabe
        {
            Id = Guid.NewGuid(),
            ProjektId = projekt.Id,
            Titel = "Workspace Preview Test Aufgabe",
            Status = AufgabeStatus.InBearbeitung,
            LokalerKlonPfad = Path.GetTempPath(),
            ErstellungsDatum = DateTimeOffset.UtcNow,
        };

        db.Projekte.Add(projekt);
        db.Aufgaben.Add(aufgabe);
        await db.SaveChangesAsync();

        var aufgabeService = new AufgabeService(db, NullLogger<AufgabeService>.Instance);
        var protokollService = new ProtokollService(db, NullLogger<ProtokollService>.Instance);
        var projektService = new ProjektService(db, NullLogger<ProjektService>.Instance);

        var gitPluginMock = new Mock<IGitPlugin>();
        gitPluginMock.SetupGet(plugin => plugin.PluginName).Returns("Local Directory");
        gitPluginMock.SetupGet(plugin => plugin.PluginPrefix).Returns("LocalDirectoryPlugin");
        gitPluginMock.SetupGet(plugin => plugin.PluginType).Returns(PluginType.SourceCodeManagement);
        gitPluginMock.Setup(plugin => plugin.GetSettingGroups()).Returns([]);
        gitPluginMock
            .Setup(plugin => plugin.GetGitActionCapabilitiesAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GitActionCapabilities(
                RepositoryKind.LocalDirectory,
                IsWorkingDirectoryCopy: true,
                CanPush: false,
                CanPull: false,
                CanCreatePullRequest: false,
                CanMergeToSource: true));

        var kiPluginMock = new Mock<IKiPlugin>();
        kiPluginMock.SetupGet(plugin => plugin.PluginName).Returns("Test KI");
        kiPluginMock.SetupGet(plugin => plugin.PluginPrefix).Returns("Softwareschmiede.TestKi");
        kiPluginMock.SetupGet(plugin => plugin.PluginType).Returns(PluginType.DevelopmentAutomation);
        kiPluginMock.Setup(plugin => plugin.GetSettingGroups()).Returns([]);

        var pluginManagerMock = new Mock<IPluginManager>();
        pluginManagerMock.Setup(manager => manager.GetSourceCodeManagementPlugins()).Returns([gitPluginMock.Object]);
        pluginManagerMock.Setup(manager => manager.GetDefaultSourceCodeManagementPlugin()).Returns(gitPluginMock.Object);
        pluginManagerMock.Setup(manager => manager.GetDevelopmentAutomationPlugins()).Returns([kiPluginMock.Object]);
        pluginManagerMock.Setup(manager => manager.GetDefaultDevelopmentAutomationPlugin()).Returns(kiPluginMock.Object);

        var pluginDefaultSettings = new PluginDefaultSettingsService(db, NullLogger<PluginDefaultSettingsService>.Instance);
        var pluginSelection = new PluginSelectionService(
            pluginManagerMock.Object,
            pluginDefaultSettings,
            NullLogger<PluginSelectionService>.Instance);

        var agentPackageServiceMock = new Mock<IAgentPackageService>();
        agentPackageServiceMock
            .Setup(service => service.GetPackagesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var workspaceBrowserServiceMock = new Mock<IGitWorkspaceBrowserService>();
        workspaceBrowserServiceMock
            .Setup(service => service.LoadSnapshotAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateWorkspaceSnapshot());
        workspaceBrowserServiceMock
            .Setup(service => service.LoadPreviewAsync(It.IsAny<string>(), It.IsAny<WorkspaceFileNode>(), It.IsAny<CancellationToken>()))
            .Returns<string, WorkspaceFileNode, CancellationToken>(async (_, node, ct) =>
            {
                if (loadPreviewShouldThrow)
                {
                    throw new InvalidOperationException($"Preview failed for {node.RelativePath}");
                }

                if (string.Equals(node.RelativePath, "alpha.cs", StringComparison.Ordinal))
                {
                    await Task.Delay(250, ct);
                    return new FilePreview(node.RelativePath, null, false, false, false, "alpha", null, "Hint Alpha");
                }

                await Task.Delay(25, ct);
                return new FilePreview(node.RelativePath, null, false, false, false, "beta", null, "Hint Beta");
            });

        var arbeitsverzeichnisResolverMock = new Mock<IArbeitsverzeichnisResolver>();
        arbeitsverzeichnisResolverMock
            .Setup(resolver => resolver.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ArbeitsverzeichnisResolutionResult(Path.GetTempPath(), false, "configured", null));

        var entwicklungsprozessService = new EntwicklungsprozessService(
            aufgabeService,
            protokollService,
            gitPluginMock.Object,
            pluginSelection,
            agentPackageServiceMock.Object,
            arbeitsverzeichnisResolverMock.Object,
            new ConfigurationBuilder().Build(),
            NullLogger<EntwicklungsprozessService>.Instance);

        var gitService = new GitOrchestrationService(
            aufgabeService,
            projektService,
            protokollService,
            gitPluginMock.Object,
            pluginSelection,
            NullLogger<GitOrchestrationService>.Instance);

        var runningStatusSourceMock = new Mock<IRunningAutomationStatusSource>();
        runningStatusSourceMock.Setup(source => source.GetRunningCount()).Returns(0);
        runningStatusSourceMock.Setup(source => source.IsRunning(It.IsAny<Guid>())).Returns(false);

        Services.AddSingleton(new Mock<IServiceScopeFactory>().Object);
        Services.AddSingleton(pluginManagerMock.Object);
        Services.AddSingleton(pluginSelection);
        Services.AddSingleton(aufgabeService);
        Services.AddSingleton(runningStatusSourceMock.Object);
        Services.AddSingleton(new AufgabeRecoveryService(db, runningStatusSourceMock.Object, NullLogger<AufgabeRecoveryService>.Instance));
        Services.AddSingleton(entwicklungsprozessService);
        Services.AddSingleton(new KiAusfuehrungsService(new Mock<IServiceScopeFactory>().Object, NullLogger<KiAusfuehrungsService>.Instance));
        Services.AddSingleton(gitService);
        Services.AddSingleton(protokollService);
        Services.AddSingleton(projektService);
        Services.AddSingleton(agentPackageServiceMock.Object);
        Services.AddSingleton(workspaceBrowserServiceMock.Object);
        Services.AddSingleton<ILogger<AufgabeDetail>>(NullLogger<AufgabeDetail>.Instance);

        return new TestHarness(db, aufgabe.Id);
    }

    private static WorkspaceSnapshot CreateWorkspaceSnapshot()
    {
        var alpha = new WorkspaceFileNode
        {
            Name = "alpha.cs",
            RelativePath = "alpha.cs",
            IsDirectory = false,
            IsDeleted = false,
            Status = new WorkspaceFileStatus('M', ' '),
        };

        var beta = new WorkspaceFileNode
        {
            Name = "beta.cs",
            RelativePath = "beta.cs",
            IsDirectory = false,
            IsDeleted = false,
            Status = new WorkspaceFileStatus('M', ' '),
        };

        return new WorkspaceSnapshot
        {
            RepositoryPath = Path.GetTempPath(),
            CommitCount = 1,
            ChangedFileCount = 2,
            RootNodes = [alpha, beta],
            FlatFiles = [alpha, beta],
        };
    }

    private sealed class TestHarness(
        Softwareschmiede.Infrastructure.Data.SoftwareschmiededDbContext db,
        Guid aufgabeId) : IAsyncDisposable
    {
        public Guid AufgabeId { get; } = aufgabeId;

        public ValueTask DisposeAsync()
        {
            db.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
