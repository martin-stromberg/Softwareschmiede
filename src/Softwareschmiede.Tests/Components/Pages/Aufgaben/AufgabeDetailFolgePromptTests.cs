using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Moq;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Components.Pages.Aufgaben;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Components.Pages.Aufgaben;

public sealed class AufgabeDetailFolgePromptTests : IDisposable
{
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiededDbContext _db = TestDbContextFactory.Create();

    [Fact]
    public void KiPanelMarkup_ShouldContainUnifiedPanel_AndConditionalContextMode()
    {
        var root = FindRepositoryRoot();
        var razorPath = Path.Combine(root, "src", "Softwareschmiede", "Components", "Pages", "Aufgaben", "AufgabeDetail.razor");
        var markup = File.ReadAllText(razorPath);

        markup.Should().Contain("@if (_aufgabe.Status is AufgabeStatus.InBearbeitung or AufgabeStatus.KiAktiv)");
        markup.Should().Contain("<div class=\"card-title\">💬 KI-Anfrage</div>");
        markup.Should().Contain("@bind=\"_kiAgentName\"");
        markup.Should().Contain("@if (_protokoll.Any(p => p.Typ == ProtokollTyp.Prompt))");
        markup.Should().Contain("Kontext mitgeben");
        markup.Should().Contain("Kontext ignorieren");
        markup.Should().Contain("Kontext neu beginnen");
        markup.Should().Contain("@onclick=\"KiStartenAsync\"");
        markup.Should().Contain("disabled=\"@(_processing || !IsAgentenauswahlGueltig)\"");
        markup.Should().NotContain("Folge-Prompt");
        markup.Should().NotContain("Kontext automatisch");
    }

    [Fact]
    public void KiPanelMarkup_ShouldKeepPluginPackageAgentOrder_InPromptAndStartDialog()
    {
        var root = FindRepositoryRoot();
        var razorPath = Path.Combine(root, "src", "Softwareschmiede", "Components", "Pages", "Aufgaben", "AufgabeDetail.razor");
        var markup = File.ReadAllText(razorPath);

        var promptPluginIndex = markup.IndexOf("<label class=\"form-label\">KI-Plugin</label>", StringComparison.Ordinal);
        var promptPackageIndex = markup.IndexOf("<label class=\"form-label\">Agentenpaket</label>", StringComparison.Ordinal);
        var promptAgentIndex = markup.IndexOf("<label class=\"form-label\">Agent auswählen</label>", StringComparison.Ordinal);
        promptPluginIndex.Should().BeGreaterThan(-1);
        promptPackageIndex.Should().BeGreaterThan(promptPluginIndex);
        promptAgentIndex.Should().BeGreaterThan(promptPackageIndex);

        var startDialogMarker = markup.IndexOf("<!-- Modal: Agentenpaket & Agent wählen beim Starten -->", StringComparison.Ordinal);
        startDialogMarker.Should().BeGreaterThan(-1);
        var startDialogMarkup = markup[startDialogMarker..];

        var startPluginIndex = startDialogMarkup.IndexOf("<label class=\"form-label\">KI-Plugin</label>", StringComparison.Ordinal);
        var startPackageIndex = startDialogMarkup.IndexOf("<label class=\"form-label\">Agentenpaket</label>", StringComparison.Ordinal);
        var startAgentIndex = startDialogMarkup.IndexOf("<label class=\"form-label\">Agent</label>", StringComparison.Ordinal);
        startPluginIndex.Should().BeGreaterThan(-1);
        startPackageIndex.Should().BeGreaterThan(startPluginIndex);
        startAgentIndex.Should().BeGreaterThan(startPackageIndex);
    }

    [Fact]
    public async Task OnInitializedAsync_ShouldDefaultSelectedAgentToInitialAgent()
    {
        var sut = await CreateSutAsync(initialAgent: "agent-initial", weitereAgenten: ["agent-alt"]);

        await sut.InvokeOnInitializedAsync();

        GetPrivateField<string>(sut, "_kiAgentName").Should().Be("agent-initial");
    }

    [Fact]
    public async Task OnInitializedAsync_ShouldPreselectStoredDefaultKiPlugin()
    {
        var sut = await CreateSutAsync(
            initialAgent: "agent-initial",
            weitereAgenten: ["agent-alt"],
            kiPlugins: [("KI A", "Softwareschmiede.KiA"), ("KI B", "Softwareschmiede.KiB")],
            storedDefaultKiPluginPrefix: "Softwareschmiede.KiB");

        await sut.InvokeOnInitializedAsync();

        GetPrivateField<string>(sut, "_selectedKiPluginPrefix").Should().Be("Softwareschmiede.KiB");
    }

    [Fact]
    public async Task OnInitializedAsync_ShouldPreselectTaskKiPlugin_WhenTaskStoresOne()
    {
        var sut = await CreateSutAsync(
            initialAgent: "agent-initial",
            weitereAgenten: ["agent-alt"],
            kiPlugins: [("KI A", "Softwareschmiede.KiA"), ("KI B", "Softwareschmiede.KiB")],
            storedTaskKiPluginPrefix: "Softwareschmiede.KiB",
            storedDefaultKiPluginPrefix: "Softwareschmiede.KiA");

        await sut.InvokeOnInitializedAsync();

        GetPrivateField<string>(sut, "_selectedKiPluginPrefix").Should().Be("Softwareschmiede.KiB");
    }

    /// <summary>Prüft, dass der Query-Parameter das Projektverzeichnis-Register aktiviert.</summary>
    [Fact]
    public async Task OnParametersSetAsync_ShouldEnableExplorer_WhenViewQueryIsTree()
    {
        var sut = await CreateSutAsync(initialAgent: "agent-initial", weitereAgenten: ["agent-alt"]);

        sut.View = "tree";
        await sut.InvokeOnParametersSetAsync();

        GetPrivateProperty<bool>(sut, "IsProjektverzeichnisRegisterAktiv").Should().BeTrue();
    }

    /// <summary>Prüft, dass nur die Explorer-Ansicht das Projektverzeichnis-Register aktiviert.</summary>
    [Fact]
    public async Task OnParametersSetAsync_ShouldDisableExplorer_WhenViewQueryIsTaskOrMissing()
    {
        var taskSut = await CreateSutAsync(initialAgent: "agent-initial", weitereAgenten: ["agent-alt"]);
        taskSut.View = "task";

        await taskSut.InvokeOnParametersSetAsync();

        GetPrivateProperty<bool>(taskSut, "IsProjektverzeichnisRegisterAktiv").Should().BeFalse();

        var defaultSut = await CreateSutAsync(initialAgent: "agent-initial", weitereAgenten: ["agent-alt"]);
        defaultSut.View = null;

        await defaultSut.InvokeOnParametersSetAsync();

        GetPrivateProperty<bool>(defaultSut, "IsProjektverzeichnisRegisterAktiv").Should().BeFalse();
    }

    /// <summary>Prüft den Fallback auf Register „Aufgabe“ bei ungültigem register-Querywert.</summary>
    [Fact]
    public async Task OnParametersSetAsync_ShouldFallbackToAufgabeRegister_WhenRegisterQueryIsInvalid()
    {
        var sut = await CreateSutAsync(initialAgent: "agent-initial", weitereAgenten: ["agent-alt"]);
        sut.Register = "ungueltig";

        await sut.InvokeOnParametersSetAsync();

        GetPrivateProperty<bool>(sut, "IsAufgabeRegisterAktiv").Should().BeTrue();
        GetPrivateProperty<bool>(sut, "IsAusfuehrungRegisterAktiv").Should().BeFalse();
        GetPrivateProperty<bool>(sut, "IsProjektverzeichnisRegisterAktiv").Should().BeFalse();
    }

    [Fact]
    public async Task KiStartenAsync_ShouldUseSelectedAgent_AndKeepSelection()
    {
        var sut = await CreateSutAsync(initialAgent: "agent-initial", weitereAgenten: ["agent-alt"]);
        await sut.InvokeOnInitializedAsync();
        SetPrivateField(sut, "_prompt", "Bitte passe die Tests an.");
        SetPrivateField(sut, "_kiAgentName", "agent-alt");
        SetPrivateField(sut, "_folgeKontextmodus", FolgeanweisungsKontextmodus.KontextIgnorieren);

        await InvokePrivateAsync(sut, "KiStartenAsync");

        sut.StartedRuns.Should().ContainSingle();
        sut.StartedRuns[0].Prompt.Should().Be("Bitte passe die Tests an.");
        sut.StartedRuns[0].Agent.Name.Should().Be("agent-alt");
        sut.StartedRuns[0].Kontextmodus.Should().Be(FolgeanweisungsKontextmodus.KontextIgnorieren);
        GetPrivateField<string>(sut, "_prompt").Should().BeEmpty();
        GetPrivateField<string>(sut, "_kiAgentName").Should().Be("agent-alt");
    }

    [Fact]
    public async Task KiStartenAsync_ShouldUseDefaultKontextmodus_WhenKiAntwortExists()
    {
        var sut = await CreateSutAsync(initialAgent: "agent-initial", weitereAgenten: ["agent-alt"]);
        await sut.InvokeOnInitializedAsync();
        SetPrivateField(sut, "_kiAgentName", "agent-alt");
        SetPrivateField(sut, "_prompt", "Initialer Prompt");

        await InvokePrivateAsync(sut, "KiStartenAsync");

        sut.StartedRuns.Should().ContainSingle();
        sut.StartedRuns[0].Prompt.Should().Be("Initialer Prompt");
        sut.StartedRuns[0].Agent.Name.Should().Be("agent-alt");
        sut.StartedRuns[0].Kontextmodus.Should().Be(FolgeanweisungsKontextmodus.KontextMitgeben);
        sut.StartedRuns[0].KiPluginPrefix.Should().Be("Softwareschmiede.TestKi");
        GetPrivateField<string>(sut, "_prompt").Should().BeEmpty();
    }

    [Fact]
    public async Task KiStartenAsync_ShouldForwardSelectedKiPluginPrefix()
    {
        var sut = await CreateSutAsync(
            initialAgent: "agent-initial",
            weitereAgenten: ["agent-alt"],
            kiPlugins: [("KI A", "Softwareschmiede.KiA"), ("KI B", "Softwareschmiede.KiB")]);
        await sut.InvokeOnInitializedAsync();
        SetPrivateField(sut, "_kiAgentName", "agent-alt");
        SetPrivateField(sut, "_selectedKiPluginPrefix", "Softwareschmiede.KiB");
        SetPrivateField(sut, "_prompt", "Initialer Prompt");

        await InvokePrivateAsync(sut, "KiStartenAsync");

        sut.StartedRuns.Should().ContainSingle();
        sut.StartedRuns[0].KiPluginPrefix.Should().Be("Softwareschmiede.KiB");
    }

    [Fact]
    public async Task KiStartenAsync_ShouldShowError_WhenNoKiPluginIsAvailable()
    {
        var sut = await CreateSutAsync(initialAgent: "agent-initial", weitereAgenten: ["agent-alt"], kiPlugins: []);
        await sut.InvokeOnInitializedAsync();
        SetPrivateField(sut, "_kiAgentName", "agent-alt");
        SetPrivateField(sut, "_prompt", "Initialer Prompt");

        await InvokePrivateAsync(sut, "KiStartenAsync");

        sut.StartedRuns.Should().BeEmpty();
        GetPrivateField<string?>(sut, "_fehler").Should().Contain("Kein KI-Plugin verfügbar");
    }

    [Fact]
    public async Task KiStartenAsync_ShouldShowError_WhenNoCompatibleAgentPackagesExist()
    {
        var sut = await CreateSutAsync(
            initialAgent: "agent-initial",
            weitereAgenten: ["agent-alt"],
            includeCompatibleAgents: false);
        await sut.InvokeOnInitializedAsync();
        SetPrivateField(sut, "_kiAgentName", "agent-alt");
        SetPrivateField(sut, "_prompt", "Initialer Prompt");

        await InvokePrivateAsync(sut, "KiStartenAsync");

        sut.StartedRuns.Should().BeEmpty();
        GetPrivateField<string?>(sut, "_fehler").Should().Contain("keine kompatiblen Agentenpakete");
    }

    [Fact]
    public async Task LadeAgentenpaketeAsync_ShouldResetDependentSelections_WhenSelectedPluginHasNoCompatiblePackages()
    {
        var sut = await CreateSutAsync(
            initialAgent: "agent-initial",
            weitereAgenten: ["agent-alt"],
            kiPlugins: [("KI A", "Softwareschmiede.KiA"), ("KI B", "Softwareschmiede.KiB")],
            availableAgentsByPluginPrefix: prefix => prefix.Equals("Softwareschmiede.KiA", StringComparison.Ordinal)
                ? [new AgentInfo("agent-initial", "Beschreibung agent-initial", "agent-initial.agent.md")]
                : []);
        await sut.InvokeOnInitializedAsync();

        SetPrivateField(sut, "_selectedKiPluginPrefix", "Softwareschmiede.KiB");
        SetPrivateField(sut, "_selectedPaketName", "paket-a");
        SetPrivateField(sut, "_selectedAgentName", "agent-initial");
        SetPrivateField(sut, "_kiAgentName", "agent-initial");

        await InvokePrivateAsync(sut, "LadeAgentenpaketeAsync", (IKiPlugin?)null);

        GetPrivateField<string>(sut, "_selectedPaketName").Should().BeEmpty();
        GetPrivateField<string>(sut, "_selectedAgentName").Should().BeEmpty();
        GetPrivateField<IReadOnlyList<AgentPackageInfo>>(sut, "_agentenpakete").Should().BeEmpty();
        GetPrivateField<List<AgentInfo>>(sut, "_agenten").Should().BeEmpty();
        GetPrivateProperty<string?>(sut, "AgentenauswahlHinweis")
            .Should().Contain("keine kompatiblen Agentenpakete");
    }

    [Fact]
    public async Task KiStartenAsync_ShouldNotStart_WhenNeuBeginnenNotConfirmedAndKiAntwortExists()
    {
        var sut = await CreateSutAsync(initialAgent: "agent-initial", weitereAgenten: ["agent-alt"]);
        await sut.InvokeOnInitializedAsync();
        SetPrivateField(sut, "_prompt", "Neuer Start");
        SetPrivateField(sut, "_folgeKontextmodus", FolgeanweisungsKontextmodus.KontextNeuBeginnen);
        SetPrivateField(sut, "_folgeKontextNeuBeginnenBestaetigt", false);

        await InvokePrivateAsync(sut, "KiStartenAsync");

        sut.StartedRuns.Should().BeEmpty();
        GetPrivateField<string?>(sut, "_fehler").Should().Contain("bestätigen");
    }

    [Fact]
    public async Task KiStartenAsync_ShouldForwardKontextmodus_WhenNoKiAntwortExists()
    {
        var sut = await CreateSutAsync(
            initialAgent: "agent-initial",
            weitereAgenten: ["agent-alt"],
            includeKiAntwort: false);
        await sut.InvokeOnInitializedAsync();
        SetPrivateField(sut, "_prompt", "Neuer Start ohne Antwort");
        SetPrivateField(sut, "_kiAgentName", "agent-alt");
        SetPrivateField(sut, "_folgeKontextmodus", FolgeanweisungsKontextmodus.KontextMitgeben);

        await InvokePrivateAsync(sut, "KiStartenAsync");

        sut.StartedRuns.Should().ContainSingle();
        sut.StartedRuns[0].Kontextmodus.Should().Be(FolgeanweisungsKontextmodus.KontextMitgeben);
    }

    [Fact]
    public async Task FolgeKontextmodusGeaendert_ShouldResetConfirmation_WhenSwitchingAwayFromNeuBeginnen()
    {
        var sut = await CreateSutAsync(initialAgent: "agent-initial", weitereAgenten: ["agent-alt"]);
        await sut.InvokeOnInitializedAsync();
        SetPrivateField(sut, "_folgeKontextmodus", FolgeanweisungsKontextmodus.KontextNeuBeginnen);
        SetPrivateField(sut, "_folgeKontextNeuBeginnenBestaetigt", true);

        await InvokePrivateAsync(
            sut,
            "FolgeKontextmodusGeaendert",
            new Microsoft.AspNetCore.Components.ChangeEventArgs
            {
                Value = FolgeanweisungsKontextmodus.KontextIgnorieren.ToString()
            });

        GetPrivateField<FolgeanweisungsKontextmodus>(sut, "_folgeKontextmodus")
            .Should().Be(FolgeanweisungsKontextmodus.KontextIgnorieren);
        GetPrivateField<bool>(sut, "_folgeKontextNeuBeginnenBestaetigt").Should().BeFalse();
    }

    [Fact]
    public async Task FolgeKontextmodusGeaendert_ShouldIgnoreInvalidValue_AndKeepState()
    {
        var sut = await CreateSutAsync(initialAgent: "agent-initial", weitereAgenten: ["agent-alt"]);
        await sut.InvokeOnInitializedAsync();
        SetPrivateField(sut, "_folgeKontextmodus", FolgeanweisungsKontextmodus.KontextMitgeben);
        SetPrivateField(sut, "_folgeKontextNeuBeginnenBestaetigt", true);

        await InvokePrivateAsync(
            sut,
            "FolgeKontextmodusGeaendert",
            new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "ungueltig" });

        GetPrivateField<FolgeanweisungsKontextmodus>(sut, "_folgeKontextmodus")
            .Should().Be(FolgeanweisungsKontextmodus.KontextMitgeben);
    }

    [Fact]
    public async Task KiStartenAsync_ShouldStart_WhenNeuBeginnenConfirmedAndKiAntwortExists()
    {
        var sut = await CreateSutAsync(initialAgent: "agent-initial", weitereAgenten: ["agent-alt"]);
        await sut.InvokeOnInitializedAsync();
        SetPrivateField(sut, "_prompt", "Bitte starte neu.");
        SetPrivateField(sut, "_folgeKontextmodus", FolgeanweisungsKontextmodus.KontextNeuBeginnen);
        SetPrivateField(sut, "_folgeKontextNeuBeginnenBestaetigt", true);

        await InvokePrivateAsync(sut, "KiStartenAsync");

        sut.StartedRuns.Should().ContainSingle();
        sut.StartedRuns[0].Kontextmodus.Should().Be(FolgeanweisungsKontextmodus.KontextNeuBeginnen);
    }

    /// <summary>Prüft, dass die Explorer-Ansicht zwischen Baum- und Listenlayout umschaltet.</summary>
    [Fact]
    public async Task VisibleWorkspaceNodes_ShouldSwitchBetweenTreeAndListLayouts()
    {
        var sut = await CreateSutAsync(initialAgent: "agent-initial", weitereAgenten: ["agent-alt"]);
        var sourceFile = new WorkspaceFileNode
        {
            Name = "Child.cs",
            RelativePath = Path.Combine("src", "Child.cs"),
            IsDirectory = false,
            Status = new WorkspaceFileStatus('M', ' '),
        };
        var directory = new WorkspaceFileNode
        {
            Name = "src",
            RelativePath = "src",
            IsDirectory = true,
            IsExpanded = false,
            ChildrenLoaded = true,
            ChangedFileCount = 1,
            Children = [sourceFile],
        };
        var rootFile = new WorkspaceFileNode
        {
            Name = "README.md",
            RelativePath = "README.md",
            IsDirectory = false,
            Status = new WorkspaceFileStatus(' ', 'M'),
        };

        SetPrivateField(sut, "_workspaceSnapshot", new WorkspaceSnapshot
        {
            RepositoryPath = Path.GetTempPath(),
            CommitCount = 1,
            ChangedFileCount = 2,
            RootNodes = [directory, rootFile],
            FlatFiles = [sourceFile, rootFile],
        });
        SetPrivateField(sut, "_useTreeLayout", true);

        var treeRows = GetPrivateProperty<ICollection<WorkspaceNodeRow>>(sut, "VisibleWorkspaceNodes");
        treeRows.Should().HaveCount(2);
        treeRows.ElementAt(0).Node.Should().Be(directory);
        treeRows.ElementAt(0).Depth.Should().Be(0);
        treeRows.ElementAt(1).Node.Should().Be(rootFile);
        treeRows.ElementAt(1).Depth.Should().Be(0);

        directory.IsExpanded = true;

        treeRows = GetPrivateProperty<ICollection<WorkspaceNodeRow>>(sut, "VisibleWorkspaceNodes");
        treeRows.Should().HaveCount(3);
        treeRows.ElementAt(0).Node.Should().Be(directory);
        treeRows.ElementAt(1).Node.Should().Be(sourceFile);
        treeRows.ElementAt(1).Depth.Should().Be(1);
        treeRows.ElementAt(2).Node.Should().Be(rootFile);

        SetPrivateField(sut, "_useTreeLayout", false);

        var listRows = GetPrivateProperty<ICollection<WorkspaceNodeRow>>(sut, "VisibleWorkspaceNodes");
        listRows.Should().HaveCount(2);
        listRows.Should().OnlyContain(row => row.Depth == 0);
        listRows.ElementAt(0).Node.Should().Be(sourceFile);
        listRows.ElementAt(1).Node.Should().Be(rootFile);
    }

    /// <summary>Prüft die Verzeichnisauswahl im Explorer.</summary>
    [Fact]
    public async Task WorkspaceNodeClickedAsync_ShouldToggleDirectoryExpansion()
    {
        var sut = await CreateSutAsync(initialAgent: "agent-initial", weitereAgenten: ["agent-alt"]);
        var directory = new WorkspaceFileNode
        {
            Name = "src",
            RelativePath = "src",
            IsDirectory = true,
            IsExpanded = false,
        };

        await InvokePrivateAsync(sut, "WorkspaceNodeClickedAsync", directory);

        directory.IsExpanded.Should().BeTrue();
        GetPrivateField<WorkspaceFileNode?>(sut, "_selectedWorkspaceNode").Should().Be(directory);
        GetPrivateField<FilePreview?>(sut, "_selectedWorkspacePreview").Should().BeNull();
        GetPrivateField<string?>(sut, "_selectedWorkspacePath").Should().Be("src");
    }

    [Fact]
    public void EvaluateGitActionVisibility_ShouldHidePushPullAndPr_WhenLocalCopyFlow()
    {
        var capabilities = new GitActionCapabilities(
            RepositoryKind.LocalDirectory,
            IsWorkingDirectoryCopy: true,
            CanPush: true,
            CanPull: true,
            CanCreatePullRequest: true,
            CanMergeToSource: true);

        var result = InvokePrivateStatic<(bool ShowPushPullToggle, bool ShowPush, bool ShowPull, bool ShowPullRequest, bool ShowMerge)>(
            "EvaluateGitActionVisibility",
            capabilities);

        result.ShowPushPullToggle.Should().BeFalse();
        result.ShowPush.Should().BeFalse();
        result.ShowPull.Should().BeFalse();
        result.ShowPullRequest.Should().BeFalse();
        result.ShowMerge.Should().BeTrue();
    }

    [Fact]
    public void EvaluateGitActionVisibility_ShouldUseFlags_WhenNotInLocalCopyFlow()
    {
        var capabilities = new GitActionCapabilities(
            RepositoryKind.RemoteGit,
            IsWorkingDirectoryCopy: false,
            CanPush: true,
            CanPull: false,
            CanCreatePullRequest: true,
            CanMergeToSource: false);

        var result = InvokePrivateStatic<(bool ShowPushPullToggle, bool ShowPush, bool ShowPull, bool ShowPullRequest, bool ShowMerge)>(
            "EvaluateGitActionVisibility",
            capabilities);

        result.ShowPushPullToggle.Should().BeTrue();
        result.ShowPush.Should().BeTrue();
        result.ShowPull.Should().BeFalse();
        result.ShowPullRequest.Should().BeTrue();
        result.ShowMerge.Should().BeFalse();
    }

    /// <summary>Prüft den Null-Fallback beim Laden der Git-Action-Capabilities.</summary>
    [Fact]
    public async Task LadeGitActionCapabilitiesAsync_ShouldApplyFallbackCapabilities_WhenPluginReturnsNull()
    {
        var sut = await CreateSutAsync(
            initialAgent: "agent-initial",
            weitereAgenten: ["agent-alt"],
            configureGitPlugin: plugin => plugin
                .Setup(g => g.GetGitActionCapabilitiesAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult<GitActionCapabilities>(null!)));
        await sut.InvokeOnInitializedAsync();

        await InvokePrivateAsync(sut, "LadeGitActionCapabilitiesAsync");

        var capabilities = GetPrivateField<GitActionCapabilities>(sut, "_gitActionCapabilities");
        capabilities.RepositoryKind.Should().Be(RepositoryKind.Unknown);
        capabilities.CanPush.Should().BeTrue();
        capabilities.CanPull.Should().BeTrue();
        capabilities.CanCreatePullRequest.Should().BeTrue();
        capabilities.CanMergeToSource.Should().BeFalse();
    }

    /// <summary>Prüft den Exception-Fallback beim Laden der Git-Action-Capabilities.</summary>
    [Fact]
    public async Task LadeGitActionCapabilitiesAsync_ShouldApplyFallbackCapabilities_WhenPluginThrows()
    {
        var sut = await CreateSutAsync(
            initialAgent: "agent-initial",
            weitereAgenten: ["agent-alt"],
            configureGitPlugin: plugin => plugin
                .Setup(g => g.GetGitActionCapabilitiesAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("kaputt")));
        await sut.InvokeOnInitializedAsync();

        await InvokePrivateAsync(sut, "LadeGitActionCapabilitiesAsync");

        var capabilities = GetPrivateField<GitActionCapabilities>(sut, "_gitActionCapabilities");
        capabilities.RepositoryKind.Should().Be(RepositoryKind.Unknown);
        capabilities.CanPush.Should().BeTrue();
        capabilities.CanPull.Should().BeTrue();
        capabilities.CanCreatePullRequest.Should().BeTrue();
        capabilities.CanMergeToSource.Should().BeFalse();
    }

    /// <summary>Verifiziert, dass bei deaktivierten Aktionen geöffnete Formulare wieder geschlossen werden.</summary>
    [Fact]
    public async Task LadeGitActionCapabilitiesAsync_ShouldClosePushPullAndPrForms_WhenActionsAreHidden()
    {
        var hiddenCapabilities = new GitActionCapabilities(
            RepositoryKind.LocalDirectory,
            IsWorkingDirectoryCopy: true,
            CanPush: false,
            CanPull: false,
            CanCreatePullRequest: false,
            CanMergeToSource: true);

        var sut = await CreateSutAsync(
            initialAgent: "agent-initial",
            weitereAgenten: ["agent-alt"],
            configureGitPlugin: plugin => plugin
                .Setup(g => g.GetGitActionCapabilitiesAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(hiddenCapabilities));
        await sut.InvokeOnInitializedAsync();
        SetPrivateField(sut, "_showPushForm", true);
        SetPrivateField(sut, "_showPullForm", true);
        SetPrivateField(sut, "_showPullRequestForm", true);

        await InvokePrivateAsync(sut, "LadeGitActionCapabilitiesAsync");

        GetPrivateField<bool>(sut, "_showPushForm").Should().BeFalse();
        GetPrivateField<bool>(sut, "_showPullForm").Should().BeFalse();
        GetPrivateField<bool>(sut, "_showPullRequestForm").Should().BeFalse();
    }

    /// <summary>Verifiziert die Erfolgsmeldung für den Merge-Handler der Detailseite.</summary>
    [Fact]
    public async Task MergeToSourceAsync_ShouldSetSuccessMessage_WhenServiceSucceeds()
    {
        var sut = await CreateSutAsync(
            initialAgent: "agent-initial",
            weitereAgenten: ["agent-alt"],
            configureGitPlugin: plugin =>
            {
                plugin.Setup(g => g.GetGitActionCapabilitiesAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new GitActionCapabilities(
                        RepositoryKind.LocalDirectory,
                        IsWorkingDirectoryCopy: true,
                        CanPush: false,
                        CanPull: false,
                        CanCreatePullRequest: false,
                        CanMergeToSource: true));
                plugin.Setup(g => g.MergeToSourceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .Returns(Task.CompletedTask);
            });
        await sut.InvokeOnInitializedAsync();

        await InvokePrivateAsync(sut, "MergeToSourceAsync");

        GetPrivateField<string?>(sut, "_erfolg").Should().Be("Merge ins Quellverzeichnis erfolgreich.");
        GetPrivateField<string?>(sut, "_fehler").Should().BeNull();
    }

    /// <summary>Verifiziert den Fehlerpfad für den Merge-Handler der Detailseite.</summary>
    [Fact]
    public async Task MergeToSourceAsync_ShouldSetErrorMessage_WhenServiceThrows()
    {
        var sut = await CreateSutAsync(
            initialAgent: "agent-initial",
            weitereAgenten: ["agent-alt"],
            configureGitPlugin: plugin =>
            {
                plugin.Setup(g => g.GetGitActionCapabilitiesAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new GitActionCapabilities(
                        RepositoryKind.LocalDirectory,
                        IsWorkingDirectoryCopy: true,
                        CanPush: false,
                        CanPull: false,
                        CanCreatePullRequest: false,
                        CanMergeToSource: true));
                plugin.Setup(g => g.MergeToSourceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new InvalidOperationException("merge failed"));
            });
        await sut.InvokeOnInitializedAsync();

        await InvokePrivateAsync(sut, "MergeToSourceAsync");

        GetPrivateField<string?>(sut, "_fehler").Should().Be("merge failed");
    }

    [Fact]
    public void RenderProtokollInhalt_ShouldRenderMarkdownHeadings()
    {
        var markdown = "# 2026-05-11\n\n## Schritt 1\nAnalyse";

        var result = InvokePrivateStatic<Microsoft.AspNetCore.Components.MarkupString>("RenderProtokollInhalt", markdown);

        result.Value.Should().Contain("<h1");
        result.Value.Should().Contain(">2026-05-11</h1>");
        result.Value.Should().Contain("<h2");
        result.Value.Should().Contain(">Schritt 1</h2>");
    }

    [Fact]
    public void RenderProtokollInhalt_ShouldSanitizeJavascriptLinks()
    {
        var markdown = "[Link](javascript:alert('xss'))";

        var result = InvokePrivateStatic<Microsoft.AspNetCore.Components.MarkupString>("RenderProtokollInhalt", markdown);

        result.Value.Should().NotContain("javascript:");
        result.Value.Should().Contain("href=\"#\"");
    }

    [Theory]
    [InlineData("[Data](data:text/html;base64,PHNjcmlwdD4=)", "data:")]
    [InlineData("[Vbs](vbscript:msgbox(1))", "vbscript:")]
    public void RenderProtokollInhalt_ShouldSanitizeUnsafeLinkSchemes(string markdown, string blockedScheme)
    {
        var result = InvokePrivateStatic<Microsoft.AspNetCore.Components.MarkupString>("RenderProtokollInhalt", markdown);

        result.Value.Should().NotContain(blockedScheme);
        result.Value.Should().Contain("href=\"#\"");
    }

    /// <summary>Stellt sicher, dass unsichere URI-Schemes in Bildquellen neutralisiert werden.</summary>
    [Theory]
    [InlineData("![Bild](javascript:alert('xss'))", "javascript:")]
    [InlineData("![Bild](data:text/html;base64,PHNjcmlwdD4=)", "data:")]
    public void RenderProtokollInhalt_ShouldSanitizeUnsafeImageSchemes(string markdown, string blockedScheme)
    {
        var result = InvokePrivateStatic<Microsoft.AspNetCore.Components.MarkupString>("RenderProtokollInhalt", markdown);

        result.Value.Should().NotContain(blockedScheme);
        result.Value.Should().Contain("src=\"#\"");
    }

    /// <summary>Verifiziert die case-insensitive Erkennung unsicherer URI-Schemes.</summary>
    [Fact]
    public void RenderProtokollInhalt_ShouldSanitizeUnsafeSchemesCaseInsensitive()
    {
        const string markdown = "[Link](JaVaScRiPt:alert(1)) ![Bild](DaTa:text/html;base64,PHNjcmlwdD4=)";

        var result = InvokePrivateStatic<Microsoft.AspNetCore.Components.MarkupString>("RenderProtokollInhalt", markdown);
        var normalized = result.Value.ToLowerInvariant();

        normalized.Should().NotContain("javascript:");
        normalized.Should().NotContain("data:");
        result.Value.Should().Contain("href=\"#\"");
        result.Value.Should().Contain("src=\"#\"");
    }

    /// <summary>Stellt sicher, dass sichere URI-Schemes nicht fälschlich blockiert werden.</summary>
    [Fact]
    public void RenderProtokollInhalt_ShouldKeepSafeSchemesIntact()
    {
        const string markdown = "[Web](https://example.org) [Mail](mailto:test@example.org) ![Bild](https://example.org/logo.png)";

        var result = InvokePrivateStatic<Microsoft.AspNetCore.Components.MarkupString>("RenderProtokollInhalt", markdown);

        result.Value.Should().Contain("https://example.org");
        result.Value.Should().Contain("mailto:test@example.org");
        result.Value.Should().NotContain("href=\"#\"");
        result.Value.Should().NotContain("src=\"#\"");
    }

    [Fact]
    public void SanitizeMarkdownHtml_ShouldRemoveHtmlEventHandlerAttributes()
    {
        var html = "<a href=\"#\" onclick=\"alert(1)\" onmouseover='alert(2)'>x</a><img src=\"ok\" oNeRrOr=\"alert(3)\">";

        var sanitized = InvokePrivateStatic<string>("SanitizeMarkdownHtml", html);
        var normalized = sanitized.ToLowerInvariant();

        normalized.Should().NotContain("onclick=");
        normalized.Should().NotContain("onmouseover=");
        normalized.Should().NotContain("onerror=");
    }

    /// <summary>Stellt sicher, dass leere oder null HTML-Eingaben als leeres Ergebnis verarbeitet werden.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t\r\n ")]
    public void SanitizeMarkdownHtml_ShouldReturnEmpty_WhenInputIsNullOrWhitespace(string? html)
    {
        var sanitized = InvokePrivateStatic<string>("SanitizeMarkdownHtml", html);

        sanitized.Should().BeEmpty();
    }

    [Fact]
    public void RenderProtokollInhalt_ShouldReturnDashPre_WhenInputIsWhitespace()
    {
        var result = InvokePrivateStatic<Microsoft.AspNetCore.Components.MarkupString>("RenderProtokollInhalt", "  \n\t  ");

        result.Value.Should().Be("<pre>&#x2013;</pre>");
    }

    [Fact]
    public async Task BuildStreamingArbeitsprotokollMarkdown_ShouldCreateDateHeadingAndStepSections()
    {
        var sut = await CreateSutAsync(initialAgent: "agent-initial", weitereAgenten: ["agent-alt"]);
        SetPrivateField(sut, "_kiStreamingStartedUtc", new DateTimeOffset(2026, 05, 24, 8, 30, 0, TimeSpan.Zero));
        SetPrivateField(sut, "_streamingLines", new List<string> { "Analyse", "Implementierung" });

        var markdown = InvokePrivate<string>(sut, "BuildStreamingArbeitsprotokollMarkdown");

        markdown.Should().Contain("# 2026-05-24");
        markdown.Should().Contain("## Schritt 1");
        markdown.Should().Contain("Analyse");
        markdown.Should().Contain("## Schritt 2");
        markdown.Should().Contain("Implementierung");
    }

    /// <summary>Verifiziert die Fallback-Ausgabe, solange noch keine Streaming-Zeilen vorliegen.</summary>
    [Fact]
    public async Task BuildStreamingArbeitsprotokollMarkdown_ShouldReturnFallback_WhenStreamingLinesAreEmpty()
    {
        var sut = await CreateSutAsync(initialAgent: "agent-initial", weitereAgenten: ["agent-alt"]);
        SetPrivateField(sut, "_kiStreamingStartedUtc", new DateTimeOffset(2026, 05, 24, 8, 30, 0, TimeSpan.Zero));
        SetPrivateField(sut, "_streamingLines", new List<string>());

        var markdown = InvokePrivate<string>(sut, "BuildStreamingArbeitsprotokollMarkdown");

        markdown.Should().Contain("# 2026-05-24");
        markdown.Should().Contain("## Schritt 1");
        markdown.Should().Contain("Warte auf Ausgabe...");
        markdown.Should().NotContain("## Schritt 2");
    }

    /// <summary>Verifiziert das Filtern leerer Zeilen und eine lückenlose Schrittnummerierung.</summary>
    [Fact]
    public async Task BuildStreamingArbeitsprotokollMarkdown_ShouldSkipWhitespaceLines_AndKeepStepNumbersContinuous()
    {
        var sut = await CreateSutAsync(initialAgent: "agent-initial", weitereAgenten: ["agent-alt"]);
        SetPrivateField(sut, "_kiStreamingStartedUtc", new DateTimeOffset(2026, 05, 24, 8, 30, 0, TimeSpan.Zero));
        SetPrivateField(sut, "_streamingLines", new List<string> { "Analyse", "   ", "\t", "Implementierung", string.Empty });

        var markdown = InvokePrivate<string>(sut, "BuildStreamingArbeitsprotokollMarkdown");

        markdown.Should().Contain("## Schritt 1");
        markdown.Should().Contain("Analyse");
        markdown.Should().Contain("## Schritt 2");
        markdown.Should().Contain("Implementierung");
        markdown.Should().NotContain("## Schritt 3");
    }

    /// <summary>Verifiziert das Entfernen von trailing whitespace pro Streaming-Schritt.</summary>
    [Fact]
    public async Task BuildStreamingArbeitsprotokollMarkdown_ShouldTrimTrailingWhitespace_PerStep()
    {
        var sut = await CreateSutAsync(initialAgent: "agent-initial", weitereAgenten: ["agent-alt"]);
        SetPrivateField(sut, "_kiStreamingStartedUtc", new DateTimeOffset(2026, 05, 24, 8, 30, 0, TimeSpan.Zero));
        SetPrivateField(sut, "_streamingLines", new List<string> { "Analyse   ", "Implementierung\t\t" });

        var markdown = InvokePrivate<string>(sut, "BuildStreamingArbeitsprotokollMarkdown");

        markdown.Should().Contain("Analyse");
        markdown.Should().Contain("Implementierung");
        markdown.Should().NotContain("Analyse   ");
        markdown.Should().NotContain("Implementierung\t\t");
    }

    [Fact]
    public void BuildFallbackHtml_ShouldEncodeInput()
    {
        var fallback = InvokePrivateStatic<string>("BuildFallbackHtml", "<script>alert('xss')</script>");

        fallback.Should().Contain("&lt;script&gt;alert");
        fallback.Should().NotContain("<script>");
    }

    [Theory]
    [InlineData(ProtokollTyp.Prompt, "protokoll-prompt")]
    [InlineData(ProtokollTyp.KiAntwort, "protokoll-antwort")]
    [InlineData(ProtokollTyp.StatusUebergang, "protokoll-status")]
    [InlineData(ProtokollTyp.GitAktion, "protokoll-git")]
    [InlineData(ProtokollTyp.TestErgebnis, "protokoll-test")]
    public void GetProtokollCssClass_ShouldReturnExpectedCssClass(ProtokollTyp typ, string expectedClass)
    {
        var cssClass = InvokePrivateStatic<string>("GetProtokollCssClass", typ);

        cssClass.Should().Be(expectedClass);
    }

    [Theory]
    [InlineData(ProtokollTyp.Prompt, "Prompt")]
    [InlineData(ProtokollTyp.KiAntwort, "KI")]
    [InlineData(ProtokollTyp.StatusUebergang, "Status")]
    [InlineData(ProtokollTyp.GitAktion, "Git")]
    [InlineData(ProtokollTyp.TestErgebnis, "Test")]
    public void GetProtokollLabel_ShouldReturnExpectedLabel(ProtokollTyp typ, string expectedLabel)
    {
        var label = InvokePrivateStatic<string>("GetProtokollLabel", typ);

        label.Should().Be(expectedLabel);
    }

    [Fact]
    public void AufgabeDetailMarkupAndCss_ShouldContainExpectedProtokollClasses()
    {
        var root = FindRepositoryRoot();
        var razorPath = Path.Combine(root, "src", "Softwareschmiede", "Components", "Pages", "Aufgaben", "AufgabeDetail.razor");
        var cssPath = Path.Combine(root, "src", "Softwareschmiede", "wwwroot", "app.css");
        var markup = File.ReadAllText(razorPath);
        var css = File.ReadAllText(cssPath);

        markup.Should().Contain("protokoll-markdown markdown-preview");
        markup.Should().Contain("streaming-output protokoll-markdown markdown-preview");
        markup.Should().Contain("id=\"streamingOutput\"");
        markup.Should().Contain("id=\"historyProtokoll\"");
        markup.Should().Contain("@RenderProtokollInhalt(BuildStreamingArbeitsprotokollMarkdown())");
        markup.Should().Contain("@if (_aufgabe.Status == AufgabeStatus.KiAktiv && _streamingLines.Count > 0)");
        markup.Should().Contain("class=\"@GetProtokollCssClass(eintrag.Typ)\"");
        markup.Should().Contain("@GetProtokollLabel(eintrag.Typ):");

        css.Should().Contain(".protokoll-markdown");
        css.Should().Contain(".markdown-preview");
        css.Should().Contain(".protokoll-antwort");
        css.Should().Contain(".streaming-output");
    }

    [Fact]
    public void ProtokollMarkup_ShouldRenderTimestampAndOptionalAgentName()
    {
        var root = FindRepositoryRoot();
        var razorPath = Path.Combine(root, "src", "Softwareschmiede", "Components", "Pages", "Aufgaben", "AufgabeDetail.razor");
        var markup = File.ReadAllText(razorPath);

        markup.Should().Contain("ToLocalTime().ToString(\"dd.MM.yyyy HH:mm:ss\")");
        markup.Should().Contain("eintrag.AgentName is not null ? $\"[{eintrag.AgentName}]\" : \"\"");
    }

    [Fact]
    public void AppMarkup_ShouldIncludeLogScrollScript()
    {
        var root = FindRepositoryRoot();
        var appPath = Path.Combine(root, "src", "Softwareschmiede", "Components", "App.razor");
        var markup = File.ReadAllText(appPath);

        markup.Should().Contain("js/log-scroll.js");
    }

    [Fact]
    public void LogScrollScript_ShouldExposeExpectedInteropFunctions()
    {
        var root = FindRepositoryRoot();
        var scriptPath = Path.Combine(root, "src", "Softwareschmiede", "wwwroot", "js", "log-scroll.js");
        var script = File.ReadAllText(scriptPath);

        script.Should().Contain("window.softwareschmiedeLogScroll");
        script.Should().Contain("getMetrics");
        script.Should().Contain("scrollToEnd");
    }

    [Theory]
    [InlineData(100d, 400d, 300d, 16d, true)]
    [InlineData(84d, 400d, 300d, 16d, true)]
    [InlineData(80d, 400d, 300d, 16d, false)]
    public void IsAtEnd_ShouldRespectThreshold(double scrollTop, double scrollHeight, double clientHeight, double thresholdPx, bool expected)
    {
        var isAtEnd = InvokePrivateStatic<bool>("IsAtEnd", scrollTop, scrollHeight, clientHeight, thresholdPx);

        isAtEnd.Should().Be(expected);
    }

    [Theory]
    [InlineData(84d, 400d, 300d, 1d, true)]
    [InlineData(80d, 400d, 300d, 1d, false)]
    public async Task TryReadAtEndStateAsync_ShouldEvaluateMetrics_WhenJsReturnsValidData(
        double scrollTop,
        double scrollHeight,
        double clientHeight,
        double existsFlag,
        bool expectedAtEnd)
    {
        var sut = await CreateSutAsync(initialAgent: "agent-initial", weitereAgenten: ["agent-alt"]);
        var jsRuntime = new FakeJsRuntime
        {
            MetricsResult = [scrollTop, scrollHeight, clientHeight, existsFlag]
        };
        SetInjectedProperty(sut, "JsRuntime", jsRuntime);

        var isAtEnd = await InvokePrivateAsync<bool>(sut, "TryReadAtEndStateAsync", "#streamingOutput");

        isAtEnd.Should().Be(expectedAtEnd);
        jsRuntime.Invocations.Should().ContainSingle(call =>
            call.Identifier == "softwareschmiedeLogScroll.getMetrics"
            && call.Args.Length == 1
            && Equals(call.Args[0], "#streamingOutput"));
    }

    [Fact]
    public async Task OnAfterRenderAsync_ShouldScrollToEnd_WhenStreamingBecomesVisibleAndInitialScrollPending()
    {
        var sut = await CreateSutAsync(initialAgent: "agent-initial", weitereAgenten: ["agent-alt"]);
        var jsRuntime = new FakeJsRuntime { ScrollToEndResult = true };
        SetInjectedProperty(sut, "JsRuntime", jsRuntime);
        SetPrivateField(sut, "_aufgabe", new Aufgabe { Status = AufgabeStatus.KiAktiv });
        SetPrivateField(sut, "_streamingLines", new List<string> { "Zeile 1" });
        SetPrivateField(sut, "_streamingInitialScrollPending", true);
        SetPrivateField(sut, "_protokoll", new List<Protokolleintrag>());

        await sut.InvokeOnAfterRenderAsync(firstRender: false);

        jsRuntime.Invocations.Should().ContainSingle(call =>
            call.Identifier == "softwareschmiedeLogScroll.scrollToEnd"
            && call.Args.Length == 1
            && Equals(call.Args[0], "#streamingOutput"));
        GetPrivateField<bool>(sut, "_streamingInitialScrollPending").Should().BeFalse();
    }

    [Fact]
    public async Task ApplyPendingScrollAsync_ShouldAutoScrollOnAppend_WhenUserWasAtEnd()
    {
        var sut = await CreateSutAsync(initialAgent: "agent-initial", weitereAgenten: ["agent-alt"]);
        var jsRuntime = new FakeJsRuntime
        {
            MetricsResult = [84d, 400d, 300d, 1d],
            ScrollToEndResult = true
        };
        SetInjectedProperty(sut, "JsRuntime", jsRuntime);
        SetPrivateField(sut, "_aufgabe", new Aufgabe { Status = AufgabeStatus.KiAktiv });
        SetPrivateField(sut, "_streamingLines", new List<string> { "Alt" });
        SetPrivateField(sut, "_streamingInitialScrollPending", false);
        SetPrivateField(sut, "_protokoll", new List<Protokolleintrag>());

        await InvokePrivateAsync(sut, "CaptureStreamingScrollStateBeforeUpdateAsync");
        var lines = GetPrivateField<List<string>>(sut, "_streamingLines");
        lines.Add("Neu");
        InvokePrivateVoid(sut, "RegisterStreamingContentUpdate");
        await InvokePrivateAsync(sut, "ApplyPendingScrollAsync");

        jsRuntime.Invocations.Should().Contain(call =>
            call.Identifier == "softwareschmiedeLogScroll.getMetrics");
        jsRuntime.Invocations.Should().Contain(call =>
            call.Identifier == "softwareschmiedeLogScroll.scrollToEnd"
            && call.Args.Length == 1
            && Equals(call.Args[0], "#streamingOutput"));
        GetPrivateField<int>(sut, "_streamingPendingScrollVersion").Should().Be(0);
    }

    [Fact]
    public async Task ApplyPendingScrollAsync_ShouldPreserveScrollPositionOnAppend_WhenUserWasNotAtEnd()
    {
        var sut = await CreateSutAsync(initialAgent: "agent-initial", weitereAgenten: ["agent-alt"]);
        var jsRuntime = new FakeJsRuntime
        {
            MetricsResult = [80d, 400d, 300d, 1d],
            ScrollToEndResult = true
        };
        SetInjectedProperty(sut, "JsRuntime", jsRuntime);
        SetPrivateField(sut, "_aufgabe", new Aufgabe { Status = AufgabeStatus.KiAktiv });
        SetPrivateField(sut, "_streamingLines", new List<string> { "Alt" });
        SetPrivateField(sut, "_streamingInitialScrollPending", false);
        SetPrivateField(sut, "_protokoll", new List<Protokolleintrag>());

        await InvokePrivateAsync(sut, "CaptureStreamingScrollStateBeforeUpdateAsync");
        var lines = GetPrivateField<List<string>>(sut, "_streamingLines");
        lines.Add("Neu");
        InvokePrivateVoid(sut, "RegisterStreamingContentUpdate");
        await InvokePrivateAsync(sut, "ApplyPendingScrollAsync");

        jsRuntime.Invocations.Should().Contain(call =>
            call.Identifier == "softwareschmiedeLogScroll.getMetrics");
        jsRuntime.Invocations.Should().NotContain(call =>
            call.Identifier == "softwareschmiedeLogScroll.scrollToEnd"
            && call.Args.Length == 1
            && Equals(call.Args[0], "#streamingOutput"));
        GetPrivateField<int>(sut, "_streamingPendingScrollVersion").Should().Be(0);
    }

    [Fact]
    public async Task Dispose_ShouldDisposeExistingKiSubscription()
    {
        var sut = await CreateSutAsync(initialAgent: "agent-initial", weitereAgenten: ["agent-alt"]);
        var trackingDisposable = new TrackingDisposable();
        SetPrivateField(sut, "_kiSubscription", trackingDisposable);

        sut.Dispose();

        trackingDisposable.IsDisposed.Should().BeTrue();
    }

    public void Dispose() => _db.Dispose();

    private async Task<TestAufgabeDetailPage> CreateSutAsync(
        string initialAgent,
        IReadOnlyList<string> weitereAgenten,
        IReadOnlyList<(string Name, string Prefix)>? kiPlugins = null,
        string? storedTaskKiPluginPrefix = null,
        string? storedDefaultKiPluginPrefix = null,
        bool includeCompatibleAgents = true,
        bool includeKiAntwort = true,
        Func<string, IEnumerable<AgentInfo>>? availableAgentsByPluginPrefix = null,
        Action<Mock<IGitPlugin>>? configureGitPlugin = null)
    {
        var projekt = new Projekt
        {
            Id = Guid.NewGuid(),
            Name = "Test-Projekt",
            Status = ProjektStatus.Aktiv,
            ErstellungsDatum = DateTimeOffset.UtcNow
        };

        var aufgabe = new Aufgabe
        {
            Id = Guid.NewGuid(),
            ProjektId = projekt.Id,
            Titel = "Aufgabe",
            Status = AufgabeStatus.InBearbeitung,
            AgentenpaketName = "paket-a",
            AgentenName = initialAgent,
            KiPluginPrefix = storedTaskKiPluginPrefix,
            LokalerKlonPfad = Path.GetTempPath(),
            ErstellungsDatum = DateTimeOffset.UtcNow
        };

        _db.Projekte.Add(projekt);
        _db.Aufgaben.Add(aufgabe);
        if (includeKiAntwort)
        {
            _db.Protokolleintraege.Add(new Protokolleintrag
            {
                Id = Guid.NewGuid(),
                AufgabeId = aufgabe.Id,
                Typ = ProtokollTyp.KiAntwort,
                Inhalt = "Antwort",
                Zeitstempel = DateTimeOffset.UtcNow
            });
        }
        await _db.SaveChangesAsync();

        var aufgabeService = new AufgabeService(_db, NullLogger<AufgabeService>.Instance);
        var protokollService = new ProtokollService(_db, NullLogger<ProtokollService>.Instance);
        var projektService = new ProjektService(_db, NullLogger<ProjektService>.Instance);

        var gitPluginMock = new Mock<IGitPlugin>();
        configureGitPlugin?.Invoke(gitPluginMock);
        var kiPluginDefinitions = kiPlugins ?? [("Test KI", "Softwareschmiede.TestKi")];
        var kiPluginMocks = kiPluginDefinitions.Select(def =>
        {
            var mock = new Mock<IKiPlugin>();
            mock.SetupGet(p => p.PluginName).Returns(def.Name);
            mock.SetupGet(p => p.PluginPrefix).Returns(def.Prefix);
            mock.SetupGet(p => p.PluginType).Returns(PluginType.DevelopmentAutomation);
            mock.Setup(p => p.GetSettingGroups()).Returns([]);
            return mock;
        }).ToList();
        var agentPackageServiceMock = new Mock<IAgentPackageService>();
        var arbeitsverzeichnisResolverMock = new Mock<IArbeitsverzeichnisResolver>();
        arbeitsverzeichnisResolverMock
            .Setup(r => r.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ArbeitsverzeichnisResolutionResult(Path.GetTempPath(), false, "configured", null));

        var pluginManagerMock = new Mock<IPluginManager>();
        pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([gitPluginMock.Object]);
        pluginManagerMock.Setup(m => m.GetDefaultSourceCodeManagementPlugin()).Returns(gitPluginMock.Object);
        var kiPluginObjects = kiPluginMocks.Select(m => m.Object).ToList();
        pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns(kiPluginObjects);
        pluginManagerMock.Setup(m => m.GetDefaultDevelopmentAutomationPlugin()).Returns(kiPluginObjects.FirstOrDefault() ?? new Mock<IKiPlugin>().Object);
        var pluginDefaultSettingsService = new PluginDefaultSettingsService(_db, NullLogger<PluginDefaultSettingsService>.Instance);
        if (!string.IsNullOrWhiteSpace(storedDefaultKiPluginPrefix))
        {
            await pluginDefaultSettingsService.SaveDefaultPluginPrefixAsync(PluginType.DevelopmentAutomation, storedDefaultKiPluginPrefix);
        }
        var pluginSelectionService = new PluginSelectionService(
            pluginManagerMock.Object,
            pluginDefaultSettingsService,
            NullLogger<PluginSelectionService>.Instance);

        var agentNamen = new List<string> { initialAgent };
        agentNamen.AddRange(weitereAgenten);
        var agenten = agentNamen
            .Select(name => new AgentInfo(name, $"Beschreibung {name}", $"{name}.agent.md"))
            .ToList();
        var paket = new AgentPackageInfo("paket-a", "/paket", agenten, []);
        foreach (var kiPluginMock in kiPluginMocks)
        {
            var pluginPrefix = kiPluginMock.Object.PluginPrefix;
            kiPluginMock
                .Setup(plugin => plugin.GetAvailableAgentsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(availableAgentsByPluginPrefix is not null
                    ? availableAgentsByPluginPrefix(pluginPrefix)
                    : includeCompatibleAgents
                        ? agenten.AsEnumerable()
                        : Array.Empty<AgentInfo>());
            kiPluginMock
                .Setup(plugin => plugin.IsAgentPackageCompatibleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            kiPluginMock
                .Setup(plugin => plugin.DeployAgentPackageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
        }
        agentPackageServiceMock
            .Setup(s => s.GetPackagesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([paket]);
        agentPackageServiceMock
            .Setup(s => s.GetPackageAsync("paket-a", It.IsAny<CancellationToken>()))
            .ReturnsAsync(paket);

        var entwicklungsprozessService = new EntwicklungsprozessService(
            aufgabeService,
            protokollService,
            gitPluginMock.Object,
            pluginSelectionService,
            agentPackageServiceMock.Object,
            arbeitsverzeichnisResolverMock.Object,
            new ConfigurationBuilder().Build(),
            NullLogger<EntwicklungsprozessService>.Instance);
        var gitService = new GitOrchestrationService(
            aufgabeService,
            projektService,
            protokollService,
            gitPluginMock.Object,
            pluginSelectionService,
            NullLogger<GitOrchestrationService>.Instance);

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

        var sut = new TestAufgabeDetailPage { Id = aufgabe.Id };
        SetInjectedProperty(sut, "ServiceScopeFactory", new Mock<IServiceScopeFactory>().Object);
        SetInjectedProperty(sut, "PluginManager", pluginManagerMock.Object);
        SetInjectedProperty(sut, "PluginSelection", pluginSelectionService);
        SetInjectedProperty(sut, "AufgabeService", aufgabeService);
        SetInjectedProperty(sut, "EntwicklungsprozessService", entwicklungsprozessService);
        SetInjectedProperty(sut, "KiAusfuehrungsService", new KiAusfuehrungsService(new Mock<IServiceScopeFactory>().Object, NullLogger<KiAusfuehrungsService>.Instance));
        SetInjectedProperty(sut, "GitService", gitService);
        SetInjectedProperty(sut, "GitWorkspaceBrowserService", workspaceBrowserServiceMock.Object);
        SetInjectedProperty(sut, "ProtokollService", protokollService);
        SetInjectedProperty(sut, "ProjektService", projektService);
        SetInjectedProperty(sut, "AgentPackageService", agentPackageServiceMock.Object);
        SetInjectedProperty(sut, "NavigationManager", new TestNavigationManager());
        SetInjectedProperty(sut, "_logger", NullLogger<AufgabeDetail>.Instance);

        return sut;
    }

    private static async Task InvokePrivateAsync(object target, string methodName, params object?[] args)
    {
        var method = typeof(AufgabeDetail)
            .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .FirstOrDefault(m =>
                m.Name.Equals(methodName, StringComparison.Ordinal)
                && m.GetParameters().Length == args.Length
                && m.GetParameters().Zip(args, (parameter, argument) => argument is null || parameter.ParameterType.IsInstanceOfType(argument)).All(x => x));
        method.Should().NotBeNull($"Method {methodName} should exist.");
        var result = method!.Invoke(target, args);
        if (result is null)
        {
            return;
        }

        if (result is Task task)
        {
            await task;
            return;
        }

        if (result is ValueTask valueTask)
        {
            await valueTask;
            return;
        }

        throw new InvalidOperationException($"{methodName} did not return Task or ValueTask.");
    }

    private static async Task<T> InvokePrivateAsync<T>(object target, string methodName, params object?[] args)
    {
        var method = typeof(AufgabeDetail)
            .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .FirstOrDefault(m =>
                m.Name.Equals(methodName, StringComparison.Ordinal)
                && m.GetParameters().Length == args.Length
                && m.GetParameters().Zip(args, (parameter, argument) => argument is null || parameter.ParameterType.IsInstanceOfType(argument)).All(x => x));
        method.Should().NotBeNull($"Method {methodName} should exist.");
        var result = method!.Invoke(target, args);
        result.Should().NotBeNull($"{methodName} should return a value.");

        if (result is Task<T> typedTask)
        {
            return await typedTask;
        }

        if (result is ValueTask<T> typedValueTask)
        {
            return await typedValueTask;
        }

        throw new InvalidOperationException($"{methodName} did not return Task<{typeof(T).Name}> or ValueTask<{typeof(T).Name}>.");
    }

    private static T InvokePrivateStatic<T>(string methodName, params object?[] args)
    {
        var method = typeof(AufgabeDetail).GetMethod(
            methodName,
            BindingFlags.Static | BindingFlags.NonPublic);
        method.Should().NotBeNull($"Static method {methodName} should exist.");
        var result = method!.Invoke(null, args);
        result.Should().NotBeNull($"{methodName} should return a value.");
        return (T)result!;
    }

    private static T InvokePrivate<T>(object target, string methodName, params object?[] args)
    {
        var method = typeof(AufgabeDetail).GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        method.Should().NotBeNull($"Method {methodName} should exist.");
        var result = method!.Invoke(target, args);
        result.Should().NotBeNull($"{methodName} should return a value.");
        return (T)result!;
    }

    private static void InvokePrivateVoid(object target, string methodName, params object?[] args)
    {
        var method = typeof(AufgabeDetail).GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        method.Should().NotBeNull($"Method {methodName} should exist.");
        method!.Invoke(target, args);
    }

    private static void SetPrivateField(object target, string fieldName, object? value)
    {
        var field = typeof(AufgabeDetail).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull($"Field {fieldName} should exist.");
        field!.SetValue(target, value);
    }

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        var field = typeof(AufgabeDetail).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull($"Field {fieldName} should exist.");
        return (T)field!.GetValue(target)!;
    }

    private static T GetPrivateProperty<T>(object target, string propertyName)
    {
        var property = typeof(AufgabeDetail).GetProperty(propertyName, BindingFlags.Instance | BindingFlags.NonPublic);
        property.Should().NotBeNull($"Property {propertyName} should exist.");
        return (T)property!.GetValue(target)!;
    }

    private static void SetInjectedProperty(object target, string propertyName, object value)
    {
        var property = typeof(AufgabeDetail).GetProperty(propertyName, BindingFlags.Instance | BindingFlags.NonPublic);
        property.Should().NotBeNull($"Property {propertyName} should exist for test setup.");
        property!.SetValue(target, value);
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Softwareschmiede.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Repository root could not be resolved.");
    }

    private sealed class TestAufgabeDetailPage : AufgabeDetail
    {
        public List<(string Prompt, AgentInfo Agent, string? Model, FolgeanweisungsKontextmodus? Kontextmodus, string? KiPluginPrefix)> StartedRuns { get; } = [];

        public Task InvokeOnInitializedAsync() => OnInitializedAsync();

        public Task InvokeOnParametersSetAsync() => OnParametersSetAsync();

        public Task InvokeOnAfterRenderAsync(bool firstRender) => OnAfterRenderAsync(firstRender);

        protected override void StartKiLauf(
            string prompt,
            AgentInfo agent,
            string? model,
            FolgeanweisungsKontextmodus? kontextmodus,
            string? selectedKiPluginPrefix)
        {
            StartedRuns.Add((prompt, agent, model, kontextmodus, selectedKiPluginPrefix));
        }

        protected override void NotifyStateChanged()
        {
            // no-op for unit tests without renderer
        }
    }

    private sealed class TestNavigationManager : Microsoft.AspNetCore.Components.NavigationManager
    {
        public TestNavigationManager()
        {
            Initialize("http://localhost/", "http://localhost/");
        }

        protected override void NavigateToCore(string uri, bool forceLoad)
        {
            Uri = ToAbsoluteUri(uri).ToString();
        }
    }

    private sealed class TrackingDisposable : IDisposable
    {
        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }

    private sealed class FakeJsRuntime : IJSRuntime
    {
        public double[] MetricsResult { get; set; } = [0d, 0d, 0d, 0d];
        public bool ScrollToEndResult { get; set; } = true;
        public Exception? Exception { get; set; }
        public List<JsCall> Invocations { get; } = [];

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            Invocations.Add(new JsCall(identifier, args ?? []));
            if (Exception is not null)
            {
                throw Exception;
            }

            object value = identifier switch
            {
                "softwareschmiedeLogScroll.getMetrics" when typeof(TValue) == typeof(double[]) => MetricsResult,
                "softwareschmiedeLogScroll.scrollToEnd" when typeof(TValue) == typeof(bool) => ScrollToEndResult,
                _ => default(TValue)!
            };

            return new ValueTask<TValue>((TValue)value);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            => InvokeAsync<TValue>(identifier, args);
    }

    private sealed record JsCall(string Identifier, object?[] Args);
}
