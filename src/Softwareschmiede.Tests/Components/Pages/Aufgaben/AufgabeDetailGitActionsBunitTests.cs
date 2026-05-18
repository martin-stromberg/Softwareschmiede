using Bunit;
using FluentAssertions;
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

public sealed class AufgabeDetailGitActionsBunitTests : TestContext
{
    /// <summary>Prüft, dass Push/Pull und Pull-Request bei lokalem Repo mit separatem Arbeitsverzeichnis ausgeblendet sind.</summary>
    [Fact]
    public async Task AufgabeDetail_ShouldHidePushPullAndPullRequestButtons_WhenRepositoryIsLocalWorkingDirectoryCopy()
    {
        var capabilities = new GitActionCapabilities(
            RepositoryKind.LocalDirectory,
            IsWorkingDirectoryCopy: true,
            CanPush: true,
            CanPull: true,
            CanCreatePullRequest: true,
            CanMergeToSource: true);

        await using var harness = await ConfigureComponentServicesAsync(capabilities);

        var cut = RenderComponent<AufgabeDetail>(parameters => parameters.Add(page => page.Id, harness.AufgabeId));
        cut.WaitForAssertion(() => cut.Markup.Should().NotContain("Wird geladen..."));

        var buttonTexts = cut.FindAll("button").Select(button => button.TextContent.Trim()).ToArray();
        buttonTexts.Should().Contain("▶️ Startskript ausführen");
        buttonTexts.Should().NotContain("🔄 Push/Pull");
        buttonTexts.Should().NotContain("🔀 Pull Request");
        buttonTexts.Should().Contain("🔀 Merge");
    }

    /// <summary>Prüft, dass Push/Pull und Pull-Request für reguläre Remote-Repositories sichtbar sind.</summary>
    [Fact]
    public async Task AufgabeDetail_ShouldShowPushPullAndPullRequestButtons_WhenRepositoryIsRemote()
    {
        var capabilities = new GitActionCapabilities(
            RepositoryKind.RemoteGit,
            IsWorkingDirectoryCopy: false,
            CanPush: true,
            CanPull: true,
            CanCreatePullRequest: true,
            CanMergeToSource: false);

        await using var harness = await ConfigureComponentServicesAsync(capabilities);

        var cut = RenderComponent<AufgabeDetail>(parameters => parameters.Add(page => page.Id, harness.AufgabeId));
        cut.WaitForAssertion(() => cut.Markup.Should().NotContain("Wird geladen..."));

        var buttonTexts = cut.FindAll("button").Select(button => button.TextContent.Trim()).ToArray();
        buttonTexts.Should().Contain("▶️ Startskript ausführen");
        buttonTexts.Should().Contain("🔄 Push/Pull");
        buttonTexts.Should().Contain("🔀 Pull Request");
        buttonTexts.Should().NotContain("🔀 Merge");
    }

    /// <summary>Prüft, dass bei GitHub-Auswahl das GitHub-Plugin statt des konkurrierenden Defaults genutzt wird.</summary>
    [Fact]
    public async Task AufgabeDetail_ShouldUseProjectSelectedGitPlugin_InInjectedGitOrchestrationService()
    {
        var defaultCapabilities = new GitActionCapabilities(
            RepositoryKind.LocalDirectory,
            IsWorkingDirectoryCopy: true,
            CanPush: false,
            CanPull: false,
            CanCreatePullRequest: false,
            CanMergeToSource: true);
        var selectedCapabilities = new GitActionCapabilities(
            RepositoryKind.RemoteGit,
            IsWorkingDirectoryCopy: false,
            CanPush: true,
            CanPull: true,
            CanCreatePullRequest: true,
            CanMergeToSource: false);

        await using var harness = await ConfigureComponentServicesWithProjectSelectedPluginAsync(
            defaultPluginPrefix: "LocalDirectoryPlugin",
            selectedPluginPrefix: "Softwareschmiede.GitHub",
            defaultCapabilities,
            selectedCapabilities);

        var cut = RenderComponent<AufgabeDetail>(parameters => parameters.Add(page => page.Id, harness.AufgabeId));
        cut.WaitForAssertion(() => cut.Markup.Should().NotContain("Wird geladen..."));

        var buttonTexts = cut.FindAll("button").Select(button => button.TextContent.Trim()).ToArray();
        buttonTexts.Should().Contain("🔄 Push/Pull");
        buttonTexts.Should().Contain("🔀 Pull Request");
        buttonTexts.Should().NotContain("🔀 Merge");

        harness.SelectedGitPlugin.Verify(
            plugin => plugin.GetGitActionCapabilitiesAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
        harness.DefaultGitPlugin.Verify(
            plugin => plugin.GetGitActionCapabilitiesAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>Prüft, dass bei LocalDirectory-Auswahl das lokale Plugin statt des konkurrierenden Defaults genutzt wird.</summary>
    [Fact]
    public async Task AufgabeDetail_ShouldUseLocalDirectoryPlugin_WhenSelectedPluginIsLocalAndDefaultIsGitHub()
    {
        var defaultCapabilities = new GitActionCapabilities(
            RepositoryKind.RemoteGit,
            IsWorkingDirectoryCopy: false,
            CanPush: true,
            CanPull: true,
            CanCreatePullRequest: true,
            CanMergeToSource: false);
        var selectedCapabilities = new GitActionCapabilities(
            RepositoryKind.LocalDirectory,
            IsWorkingDirectoryCopy: true,
            CanPush: false,
            CanPull: false,
            CanCreatePullRequest: false,
            CanMergeToSource: true);

        await using var harness = await ConfigureComponentServicesWithProjectSelectedPluginAsync(
            defaultPluginPrefix: "Softwareschmiede.GitHub",
            selectedPluginPrefix: "LocalDirectoryPlugin",
            defaultCapabilities,
            selectedCapabilities);

        var cut = RenderComponent<AufgabeDetail>(parameters => parameters.Add(page => page.Id, harness.AufgabeId));
        cut.WaitForAssertion(() => cut.Markup.Should().NotContain("Wird geladen..."));

        var buttonTexts = cut.FindAll("button").Select(button => button.TextContent.Trim()).ToArray();
        buttonTexts.Should().NotContain("🔄 Push/Pull");
        buttonTexts.Should().NotContain("🔀 Pull Request");
        buttonTexts.Should().Contain("🔀 Merge");

        harness.SelectedGitPlugin.Verify(
            plugin => plugin.GetGitActionCapabilitiesAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
        harness.DefaultGitPlugin.Verify(
            plugin => plugin.GetGitActionCapabilitiesAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AufgabeDetail_ShouldInvokePullOnProjectSelectedGitPlugin()
    {
        var defaultCapabilities = new GitActionCapabilities(
            RepositoryKind.LocalDirectory,
            IsWorkingDirectoryCopy: true,
            CanPush: false,
            CanPull: false,
            CanCreatePullRequest: false,
            CanMergeToSource: true);
        var selectedCapabilities = new GitActionCapabilities(
            RepositoryKind.RemoteGit,
            IsWorkingDirectoryCopy: false,
            CanPush: true,
            CanPull: true,
            CanCreatePullRequest: true,
            CanMergeToSource: false);

        await using var harness = await ConfigureComponentServicesWithProjectSelectedPluginAsync(
            defaultPluginPrefix: "LocalDirectoryPlugin",
            selectedPluginPrefix: "Softwareschmiede.GitHub",
            defaultCapabilities,
            selectedCapabilities);

        var cut = RenderComponent<AufgabeDetail>(parameters => parameters.Add(page => page.Id, harness.AufgabeId));
        cut.WaitForAssertion(() => cut.Markup.Should().NotContain("Wird geladen..."));

        cut.FindAll("button").First(button => button.TextContent.Trim() == "🔄 Push/Pull").Click();
        cut.WaitForAssertion(() => cut.FindAll("button").Any(button => button.TextContent.Trim() == "⬇️ Pull").Should().BeTrue());
        cut.FindAll("button").First(button => button.TextContent.Trim() == "⬇️ Pull").Click();

        cut.WaitForAssertion(() =>
            harness.SelectedGitPlugin.Verify(
                plugin => plugin.PullAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.AtLeastOnce));
        harness.DefaultGitPlugin.Verify(
            plugin => plugin.PullAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private async Task<TestHarness> ConfigureComponentServicesAsync(GitActionCapabilities capabilities)
    {
        var db = TestDbContextFactory.Create();

        var projekt = new Projekt
        {
            Id = Guid.NewGuid(),
            Name = "BUnit-Test-Projekt",
            Status = ProjektStatus.Aktiv,
            ErstellungsDatum = DateTimeOffset.UtcNow
        };

        var aufgabe = new Aufgabe
        {
            Id = Guid.NewGuid(),
            ProjektId = projekt.Id,
            Titel = "BUnit-Test-Aufgabe",
            Status = AufgabeStatus.InBearbeitung,
            AgentenpaketName = "paket-a",
            AgentenName = "agent-a",
            LokalerKlonPfad = Path.GetTempPath(),
            ErstellungsDatum = DateTimeOffset.UtcNow
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
            .ReturnsAsync(capabilities);
        gitPluginMock
            .Setup(plugin => plugin.PullAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

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
        var agentInfo = new AgentInfo("agent-a", "Agent A", "agent-a.md");
        var packageInfo = new AgentPackageInfo("paket-a", "/paket-a", [agentInfo], []);
        agentPackageServiceMock
            .Setup(service => service.GetPackagesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([packageInfo]);
        agentPackageServiceMock
            .Setup(service => service.GetPackageAsync("paket-a", It.IsAny<CancellationToken>()))
            .ReturnsAsync(packageInfo);

        var workspaceBrowserServiceMock = new Mock<IGitWorkspaceBrowserService>();
        workspaceBrowserServiceMock
            .Setup(service => service.LoadSnapshotAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkspaceSnapshot
            {
                RepositoryPath = Path.GetTempPath(),
                CommitCount = 0,
                ChangedFileCount = 0,
            });
        workspaceBrowserServiceMock
            .Setup(service => service.LoadPreviewAsync(It.IsAny<string>(), It.IsAny<WorkspaceFileNode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FilePreview(string.Empty, null, false, false, false, null, null, null));

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

        Services.AddSingleton(new Mock<IServiceScopeFactory>().Object);
        Services.AddSingleton(pluginManagerMock.Object);
        Services.AddSingleton(pluginSelection);
        Services.AddSingleton(aufgabeService);
        Services.AddSingleton(entwicklungsprozessService);
        Services.AddSingleton(new KiAusfuehrungsService(new Mock<IServiceScopeFactory>().Object, NullLogger<KiAusfuehrungsService>.Instance));
        Services.AddSingleton(gitService);
        Services.AddSingleton(protokollService);
        Services.AddSingleton(projektService);
        Services.AddSingleton(agentPackageServiceMock.Object);
        Services.AddSingleton(workspaceBrowserServiceMock.Object);
        Services.AddSingleton<ILogger<AufgabeDetail>>(NullLogger<AufgabeDetail>.Instance);

        return new TestHarness(db, aufgabe.Id, defaultGitPlugin: gitPluginMock, selectedGitPlugin: gitPluginMock);
    }

    private async Task<TestHarness> ConfigureComponentServicesWithProjectSelectedPluginAsync(
        string defaultPluginPrefix,
        string selectedPluginPrefix,
        GitActionCapabilities defaultCapabilities,
        GitActionCapabilities selectedCapabilities)
    {
        var db = TestDbContextFactory.Create();

        var projekt = new Projekt
        {
            Id = Guid.NewGuid(),
            Name = "BUnit-Test-Projekt",
            Status = ProjektStatus.Aktiv,
            ErstellungsDatum = DateTimeOffset.UtcNow
        };

        var repository = new GitRepository
        {
            Id = Guid.NewGuid(),
            ProjektId = projekt.Id,
            PluginTyp = selectedPluginPrefix,
            RepositoryUrl = "https://github.com/example/repo",
            RepositoryName = "repo",
            Aktiv = true
        };

        var aufgabe = new Aufgabe
        {
            Id = Guid.NewGuid(),
            ProjektId = projekt.Id,
            GitRepositoryId = repository.Id,
            Titel = "BUnit-Test-Aufgabe",
            Status = AufgabeStatus.InBearbeitung,
            AgentenpaketName = "paket-a",
            AgentenName = "agent-a",
            LokalerKlonPfad = Path.GetTempPath(),
            ErstellungsDatum = DateTimeOffset.UtcNow
        };

        db.Projekte.Add(projekt);
        db.GitRepositories.Add(repository);
        db.Aufgaben.Add(aufgabe);
        await db.SaveChangesAsync();

        var aufgabeService = new AufgabeService(db, NullLogger<AufgabeService>.Instance);
        var protokollService = new ProtokollService(db, NullLogger<ProtokollService>.Instance);
        var projektService = new ProjektService(db, NullLogger<ProjektService>.Instance);

        var defaultGitPluginMock = new Mock<IGitPlugin>();
        defaultGitPluginMock.SetupGet(plugin => plugin.PluginName).Returns("Default Plugin");
        defaultGitPluginMock.SetupGet(plugin => plugin.PluginPrefix).Returns(defaultPluginPrefix);
        defaultGitPluginMock.SetupGet(plugin => plugin.PluginType).Returns(PluginType.SourceCodeManagement);
        defaultGitPluginMock.Setup(plugin => plugin.GetSettingGroups()).Returns([]);
        defaultGitPluginMock
            .Setup(plugin => plugin.GetGitActionCapabilitiesAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(defaultCapabilities);

        var selectedGitPluginMock = new Mock<IGitPlugin>();
        selectedGitPluginMock.SetupGet(plugin => plugin.PluginName).Returns("Selected Plugin");
        selectedGitPluginMock.SetupGet(plugin => plugin.PluginPrefix).Returns(selectedPluginPrefix);
        selectedGitPluginMock.SetupGet(plugin => plugin.PluginType).Returns(PluginType.SourceCodeManagement);
        selectedGitPluginMock.Setup(plugin => plugin.GetSettingGroups()).Returns([]);
        selectedGitPluginMock
            .Setup(plugin => plugin.GetGitActionCapabilitiesAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(selectedCapabilities);
        selectedGitPluginMock
            .Setup(plugin => plugin.PullAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        defaultGitPluginMock
            .Setup(plugin => plugin.PullAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var kiPluginMock = new Mock<IKiPlugin>();
        kiPluginMock.SetupGet(plugin => plugin.PluginName).Returns("Test KI");
        kiPluginMock.SetupGet(plugin => plugin.PluginPrefix).Returns("Softwareschmiede.TestKi");
        kiPluginMock.SetupGet(plugin => plugin.PluginType).Returns(PluginType.DevelopmentAutomation);
        kiPluginMock.Setup(plugin => plugin.GetSettingGroups()).Returns([]);

        var pluginManagerMock = new Mock<IPluginManager>();
        pluginManagerMock
            .Setup(manager => manager.GetSourceCodeManagementPlugins())
            .Returns([defaultGitPluginMock.Object, selectedGitPluginMock.Object]);
        pluginManagerMock.Setup(manager => manager.GetDefaultSourceCodeManagementPlugin()).Returns(defaultGitPluginMock.Object);
        pluginManagerMock.Setup(manager => manager.GetDevelopmentAutomationPlugins()).Returns([kiPluginMock.Object]);
        pluginManagerMock.Setup(manager => manager.GetDefaultDevelopmentAutomationPlugin()).Returns(kiPluginMock.Object);

        var pluginDefaultSettings = new PluginDefaultSettingsService(db, NullLogger<PluginDefaultSettingsService>.Instance);
        var pluginSelection = new PluginSelectionService(
            pluginManagerMock.Object,
            pluginDefaultSettings,
            NullLogger<PluginSelectionService>.Instance);

        var agentPackageServiceMock = new Mock<IAgentPackageService>();
        var agentInfo = new AgentInfo("agent-a", "Agent A", "agent-a.md");
        var packageInfo = new AgentPackageInfo("paket-a", "/paket-a", [agentInfo], []);
        agentPackageServiceMock
            .Setup(service => service.GetPackagesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([packageInfo]);
        agentPackageServiceMock
            .Setup(service => service.GetPackageAsync("paket-a", It.IsAny<CancellationToken>()))
            .ReturnsAsync(packageInfo);

        var workspaceBrowserServiceMock = new Mock<IGitWorkspaceBrowserService>();
        workspaceBrowserServiceMock
            .Setup(service => service.LoadSnapshotAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkspaceSnapshot
            {
                RepositoryPath = Path.GetTempPath(),
                CommitCount = 0,
                ChangedFileCount = 0,
            });
        workspaceBrowserServiceMock
            .Setup(service => service.LoadPreviewAsync(It.IsAny<string>(), It.IsAny<WorkspaceFileNode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FilePreview(string.Empty, null, false, false, false, null, null, null));

        var arbeitsverzeichnisResolverMock = new Mock<IArbeitsverzeichnisResolver>();
        arbeitsverzeichnisResolverMock
            .Setup(resolver => resolver.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ArbeitsverzeichnisResolutionResult(Path.GetTempPath(), false, "configured", null));

        var entwicklungsprozessService = new EntwicklungsprozessService(
            aufgabeService,
            protokollService,
            selectedGitPluginMock.Object,
            pluginSelection,
            agentPackageServiceMock.Object,
            arbeitsverzeichnisResolverMock.Object,
            new ConfigurationBuilder().Build(),
            NullLogger<EntwicklungsprozessService>.Instance);

        var gitService = new GitOrchestrationService(
            aufgabeService,
            projektService,
            protokollService,
            defaultGitPluginMock.Object,
            pluginSelection,
            NullLogger<GitOrchestrationService>.Instance);

        Services.AddSingleton(new Mock<IServiceScopeFactory>().Object);
        Services.AddSingleton(pluginManagerMock.Object);
        Services.AddSingleton(pluginSelection);
        Services.AddSingleton(aufgabeService);
        Services.AddSingleton(entwicklungsprozessService);
        Services.AddSingleton(new KiAusfuehrungsService(new Mock<IServiceScopeFactory>().Object, NullLogger<KiAusfuehrungsService>.Instance));
        Services.AddSingleton(gitService);
        Services.AddSingleton(protokollService);
        Services.AddSingleton(projektService);
        Services.AddSingleton(agentPackageServiceMock.Object);
        Services.AddSingleton(workspaceBrowserServiceMock.Object);
        Services.AddSingleton<ILogger<AufgabeDetail>>(NullLogger<AufgabeDetail>.Instance);

        return new TestHarness(db, aufgabe.Id, defaultGitPluginMock, selectedGitPluginMock);
    }

    private sealed class TestHarness(
        Softwareschmiede.Infrastructure.Data.SoftwareschmiededDbContext db,
        Guid aufgabeId,
        Mock<IGitPlugin> defaultGitPlugin,
        Mock<IGitPlugin> selectedGitPlugin) : IAsyncDisposable
    {
        public Guid AufgabeId { get; } = aufgabeId;
        public Mock<IGitPlugin> DefaultGitPlugin { get; } = defaultGitPlugin;
        public Mock<IGitPlugin> SelectedGitPlugin { get; } = selectedGitPlugin;

        public ValueTask DisposeAsync()
        {
            db.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
