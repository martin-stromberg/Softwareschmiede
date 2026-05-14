using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
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

public sealed class AufgabeDetailFolgePromptTests : IDisposable
{
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiededDbContext _db = TestDbContextFactory.Create();

    [Fact]
    public void FolgePromptMarkup_ShouldContainAgentSelectionBinding_AndExactContextModes()
    {
        var root = FindRepositoryRoot();
        var razorPath = Path.Combine(root, "src", "Softwareschmiede", "Components", "Pages", "Aufgaben", "AufgabeDetail.razor");
        var markup = File.ReadAllText(razorPath);

        markup.Should().Contain("@if (_aufgabe.Status == AufgabeStatus.InBearbeitung && _protokoll.Any(p => p.Typ == ProtokollTyp.KiAntwort))");
        markup.Should().Contain("@bind=\"_folgeAgentName\"");
        markup.Should().Contain("Kontext mitgeben");
        markup.Should().Contain("Kontext ignorieren");
        markup.Should().Contain("Kontext neu beginnen");
        markup.Should().NotContain("Kontext automatisch");
    }

    [Fact]
    public async Task OnInitializedAsync_ShouldDefaultFolgeAgentToInitialAgent()
    {
        var sut = await CreateSutAsync(initialAgent: "agent-initial", weitereAgenten: ["agent-alt"]);

        await sut.InvokeOnInitializedAsync();

        GetPrivateField<string>(sut, "_folgeAgentName").Should().Be("agent-initial");
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
    public async Task FolgePromptAsync_ShouldUseSelectedFollowAgent_AndResetToInitialAgent()
    {
        var sut = await CreateSutAsync(initialAgent: "agent-initial", weitereAgenten: ["agent-alt"]);
        await sut.InvokeOnInitializedAsync();
        SetPrivateField(sut, "_folgePrompt", "Bitte passe die Tests an.");
        SetPrivateField(sut, "_folgeAgentName", "agent-alt");
        SetPrivateField(sut, "_folgeKontextmodus", FolgeanweisungsKontextmodus.KontextIgnorieren);

        await InvokePrivateAsync(sut, "FolgePromptAsync");

        sut.StartedRuns.Should().ContainSingle();
        sut.StartedRuns[0].Prompt.Should().Be("Bitte passe die Tests an.");
        sut.StartedRuns[0].Agent.Name.Should().Be("agent-alt");
        sut.StartedRuns[0].Kontextmodus.Should().Be(FolgeanweisungsKontextmodus.KontextIgnorieren);
        GetPrivateField<string>(sut, "_folgePrompt").Should().BeEmpty();
        GetPrivateField<string>(sut, "_folgeAgentName").Should().Be("agent-initial");
        GetPrivateField<FolgeanweisungsKontextmodus>(sut, "_folgeKontextmodus").Should().Be(FolgeanweisungsKontextmodus.KontextMitgeben);
    }

    [Fact]
    public async Task KiStartenAsync_ShouldKeepInitialPromptBehavior()
    {
        var sut = await CreateSutAsync(initialAgent: "agent-initial", weitereAgenten: ["agent-alt"]);
        await sut.InvokeOnInitializedAsync();
        SetPrivateField(sut, "_kiAgentName", "agent-alt");
        SetPrivateField(sut, "_prompt", "Initialer Prompt");

        await InvokePrivateAsync(sut, "KiStartenAsync");

        sut.StartedRuns.Should().ContainSingle();
        sut.StartedRuns[0].Prompt.Should().Be("Initialer Prompt");
        sut.StartedRuns[0].Agent.Name.Should().Be("agent-alt");
        sut.StartedRuns[0].Kontextmodus.Should().BeNull();
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
    public async Task FolgePromptAsync_ShouldNotStart_WhenNeuBeginnenNotConfirmed()
    {
        var sut = await CreateSutAsync(initialAgent: "agent-initial", weitereAgenten: ["agent-alt"]);
        await sut.InvokeOnInitializedAsync();
        SetPrivateField(sut, "_folgePrompt", "Neuer Start");
        SetPrivateField(sut, "_folgeKontextmodus", FolgeanweisungsKontextmodus.KontextNeuBeginnen);
        SetPrivateField(sut, "_folgeKontextNeuBeginnenBestaetigt", false);

        await InvokePrivateAsync(sut, "FolgePromptAsync");

        sut.StartedRuns.Should().BeEmpty();
        GetPrivateField<string?>(sut, "_fehler").Should().Contain("bestätigen");
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
    public async Task FolgePromptAsync_ShouldStart_WhenNeuBeginnenConfirmed()
    {
        var sut = await CreateSutAsync(initialAgent: "agent-initial", weitereAgenten: ["agent-alt"]);
        await sut.InvokeOnInitializedAsync();
        SetPrivateField(sut, "_folgePrompt", "Bitte starte neu.");
        SetPrivateField(sut, "_folgeKontextmodus", FolgeanweisungsKontextmodus.KontextNeuBeginnen);
        SetPrivateField(sut, "_folgeKontextNeuBeginnenBestaetigt", true);

        await InvokePrivateAsync(sut, "FolgePromptAsync");

        sut.StartedRuns.Should().ContainSingle();
        sut.StartedRuns[0].Kontextmodus.Should().Be(FolgeanweisungsKontextmodus.KontextNeuBeginnen);
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

    /// <summary>Verifiziert, dass bei unsichtbaren Aktionen geöffnete Formulare wieder geschlossen werden.</summary>
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
        SetPrivateField(sut, "_showPushPullButtons", true);
        SetPrivateField(sut, "_showPullRequestForm", true);

        await InvokePrivateAsync(sut, "LadeGitActionCapabilitiesAsync");

        GetPrivateField<bool>(sut, "_showPushPullButtons").Should().BeFalse();
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

    [Fact]
    public void RenderProtokollInhalt_ShouldReturnDashPre_WhenInputIsWhitespace()
    {
        var result = InvokePrivateStatic<Microsoft.AspNetCore.Components.MarkupString>("RenderProtokollInhalt", "  \n\t  ");

        result.Value.Should().Be("<pre>&#x2013;</pre>");
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
        string? storedDefaultKiPluginPrefix = null,
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
            LokalerKlonPfad = Path.GetTempPath(),
            ErstellungsDatum = DateTimeOffset.UtcNow
        };

        _db.Projekte.Add(projekt);
        _db.Aufgaben.Add(aufgabe);
        _db.Protokolleintraege.Add(new Protokolleintrag
        {
            Id = Guid.NewGuid(),
            AufgabeId = aufgabe.Id,
            Typ = ProtokollTyp.KiAntwort,
            Inhalt = "Antwort",
            Zeitstempel = DateTimeOffset.UtcNow
        });
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

        var sut = new TestAufgabeDetailPage { Id = aufgabe.Id };
        SetInjectedProperty(sut, "ServiceScopeFactory", new Mock<IServiceScopeFactory>().Object);
        SetInjectedProperty(sut, "PluginManager", pluginManagerMock.Object);
        SetInjectedProperty(sut, "PluginSelection", pluginSelectionService);
        SetInjectedProperty(sut, "AufgabeService", aufgabeService);
        SetInjectedProperty(sut, "EntwicklungsprozessService", entwicklungsprozessService);
        SetInjectedProperty(sut, "KiAusfuehrungsService", new KiAusfuehrungsService(new Mock<IServiceScopeFactory>().Object, NullLogger<KiAusfuehrungsService>.Instance));
        SetInjectedProperty(sut, "GitService", gitService);
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
}
