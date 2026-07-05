using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Components.Pages.Aufgaben;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Components.Pages.Aufgaben;

/// <summary>AufgabeDetailGitActionsBunitTests.</summary>
public sealed class AufgabeDetailGitActionsBunitTests : TestContext
{
    /// <summary><summary>AufgabeDetail_ShouldRenderExactlyThreeRegisterTabs_WithTabSemantics.</summary>.</summary>
    [Fact]
    /// <summary>AufgabeDetail_ShouldRenderExactlyThreeRegisterTabs_WithTabSemantics.</summary>
    public async Task AufgabeDetail_ShouldRenderExactlyThreeRegisterTabs_WithTabSemantics()
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

        var tabs = cut.FindAll("button[role='tab']");
        tabs.Should().HaveCount(3);
        tabs.Select(tab => tab.TextContent.Trim()).Should().BeEquivalentTo(["Aufgabe", "Ausführung", "Projektverzeichnis"]);
        cut.Find("div[role='tablist']").GetAttribute("aria-label").Should().Be("Aufgabe Detail Register");
    }

    /// <summary><summary>AufgabeDetail_ShouldKeepGlobalInfoBoxesVisible_AcrossAllRegisters.</summary>.</summary>
    [Fact]
    /// <summary>AufgabeDetail_ShouldKeepGlobalInfoBoxesVisible_AcrossAllRegisters.</summary>
    public async Task AufgabeDetail_ShouldKeepGlobalInfoBoxesVisible_AcrossAllRegisters()
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

        foreach (var register in new[] { "Aufgabe", "Ausführung", "Projektverzeichnis" })
        {
            cut.FindAll("button").Single(button => button.TextContent.Trim() == register).Click();
            cut.WaitForAssertion(() =>
            {
                cut.Markup.Should().Contain("Commits");
                cut.Markup.Should().Contain("Geänderte Dateien");
            });
        }
    }

    /// <summary><summary>AufgabeDetail_ShouldRenderExactlyOneRegisterContent_AfterEachTabSwitch.</summary>.</summary>
    [Fact]
    /// <summary>AufgabeDetail_ShouldRenderExactlyOneRegisterContent_AfterEachTabSwitch.</summary>
    public async Task AufgabeDetail_ShouldRenderExactlyOneRegisterContent_AfterEachTabSwitch()
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

        cut.Markup.Should().Contain("📋 Anforderung");
        cut.Markup.Should().NotContain("🗂️ Repository-Explorer");
        cut.Markup.Should().NotContain("💬 KI-Anfrage");

        cut.FindAll("button").Single(button => button.TextContent.Trim() == "Ausführung").Click();
        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("📜 Protokoll");
            cut.Markup.Should().Contain("💬 KI-Anfrage");
            cut.Markup.Should().NotContain("📋 Anforderung");
            cut.Markup.Should().NotContain("🗂️ Repository-Explorer");
        });

        cut.FindAll("button").Single(button => button.TextContent.Trim() == "Projektverzeichnis").Click();
        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("🗂️ Repository-Explorer");
            cut.Markup.Should().NotContain("📋 Anforderung");
            cut.Markup.Should().NotContain("💬 KI-Anfrage");
        });
    }

    /// <summary><summary>AufgabeDetail_ShouldCloseGitDialogs_WhenLeavingProjectDirectoryRegister.</summary>.</summary>
    [Fact]
    /// <summary>AufgabeDetail_ShouldCloseGitDialogs_WhenLeavingProjectDirectoryRegister.</summary>
    public async Task AufgabeDetail_ShouldCloseGitDialogs_WhenLeavingProjectDirectoryRegister()
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

        cut.FindAll("button").Single(button => button.TextContent.Trim() == "Projektverzeichnis").Click();
        cut.FindAll("button").Single(button => button.TextContent.Trim() == "⬇️ Pull").Click();
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Jetzt pullen"));

        cut.FindAll("button").Single(button => button.TextContent.Trim() == "Aufgabe").Click();
        cut.WaitForAssertion(() => cut.Markup.Should().NotContain("Jetzt pullen"));

        cut.FindAll("button").Single(button => button.TextContent.Trim() == "Projektverzeichnis").Click();
        cut.WaitForAssertion(() => cut.Markup.Should().NotContain("Jetzt pullen"));
    }

    /// <summary><summary>AufgabeDetail_ShouldShowRecoveryButton_ForRecoverableStates.</summary>.</summary>
    /// <summary><summary>AufgabeDetail_ShouldShowRecoveryButton_ForRecoverableStates.</summary>.</summary>
    /// <summary><summary>AufgabeDetail_ShouldShowRecoveryButton_ForRecoverableStates.</summary>.</summary>
    [Theory]
    [InlineData(AufgabeStatus.InArbeit)]
    [InlineData(AufgabeStatus.Wartend)]
    /// <summary>AufgabeDetail_ShouldShowRecoveryButton_ForRecoverableStates.</summary>
    public async Task AufgabeDetail_ShouldShowRecoveryButton_ForRecoverableStates(AufgabeStatus status)
    {
        var capabilities = new GitActionCapabilities(
            RepositoryKind.LocalDirectory,
            IsWorkingDirectoryCopy: true,
            CanPush: false,
            CanPull: false,
            CanCreatePullRequest: false,
            CanMergeToSource: true);

        await using var harness = await ConfigureComponentServicesAsync(capabilities, status, isRunning: false);

        var cut = RenderComponent<AufgabeDetail>(parameters => parameters.Add(page => page.Id, harness.AufgabeId));
        cut.WaitForAssertion(() => cut.Markup.Should().NotContain("Wird geladen..."));

        var recoveryButton = cut.FindAll("button").Single(button => button.TextContent.Contains("Aufgabe wiederherstellen"));
        recoveryButton.HasAttribute("disabled").Should().BeFalse();
    }

    /// <summary><summary>AufgabeDetail_ShouldShowProjectNameBelowTitle_WhenProjectIsAssigned.</summary>.</summary>
    [Fact]
    /// <summary>AufgabeDetail_ShouldShowProjectNameBelowTitle_WhenProjectIsAssigned.</summary>
    public async Task AufgabeDetail_ShouldShowProjectNameBelowTitle_WhenProjectIsAssigned()
    {
        var capabilities = new GitActionCapabilities(
            RepositoryKind.LocalDirectory,
            IsWorkingDirectoryCopy: true,
            CanPush: false,
            CanPull: false,
            CanCreatePullRequest: false,
            CanMergeToSource: true);

        await using var harness = await ConfigureComponentServicesAsync(capabilities, projectName: "Alpha");

        var cut = RenderComponent<AufgabeDetail>(parameters => parameters.Add(page => page.Id, harness.AufgabeId));
        cut.WaitForAssertion(() => cut.Markup.Should().NotContain("Wird geladen..."));

        cut.Markup.Should().Contain("Projekt: Alpha");
    }

    /// <summary><summary>AufgabeDetail_ShouldShowProjectFallback_WhenProjectNameIsEmpty.</summary>.</summary>
    [Fact]
    /// <summary>AufgabeDetail_ShouldShowProjectFallback_WhenProjectNameIsEmpty.</summary>
    public async Task AufgabeDetail_ShouldShowProjectFallback_WhenProjectNameIsEmpty()
    {
        var capabilities = new GitActionCapabilities(
            RepositoryKind.LocalDirectory,
            IsWorkingDirectoryCopy: true,
            CanPush: false,
            CanPull: false,
            CanCreatePullRequest: false,
            CanMergeToSource: true);

        await using var harness = await ConfigureComponentServicesAsync(capabilities, projectName: string.Empty);

        var cut = RenderComponent<AufgabeDetail>(parameters => parameters.Add(page => page.Id, harness.AufgabeId));
        cut.WaitForAssertion(() => cut.Markup.Should().NotContain("Wird geladen..."));

        cut.Markup.Should().Contain("Projekt: ohne projekt");
    }

    /// <summary><summary>AufgabeDetail_ShouldShowProjectFallback_WhenProjectNameIsWhitespace.</summary>.</summary>
    [Fact]
    /// <summary>AufgabeDetail_ShouldShowProjectFallback_WhenProjectNameIsWhitespace.</summary>
    public async Task AufgabeDetail_ShouldShowProjectFallback_WhenProjectNameIsWhitespace()
    {
        var capabilities = new GitActionCapabilities(
            RepositoryKind.LocalDirectory,
            IsWorkingDirectoryCopy: true,
            CanPush: false,
            CanPull: false,
            CanCreatePullRequest: false,
            CanMergeToSource: true);

        await using var harness = await ConfigureComponentServicesAsync(
            capabilities,
            projectName: "   ");

        var cut = RenderComponent<AufgabeDetail>(parameters => parameters.Add(page => page.Id, harness.AufgabeId));
        cut.WaitForAssertion(() => cut.Markup.Should().NotContain("Wird geladen..."));

        cut.Markup.Should().Contain("Projekt: ohne projekt");
    }

    /// <summary><summary>AufgabeDetail_ShouldRenderProjectNameAsPlainText_WhenProjectNameContainsHtml.</summary>.</summary>
    [Fact]
    /// <summary>AufgabeDetail_ShouldRenderProjectNameAsPlainText_WhenProjectNameContainsHtml.</summary>
    public async Task AufgabeDetail_ShouldRenderProjectNameAsPlainText_WhenProjectNameContainsHtml()
    {
        var capabilities = new GitActionCapabilities(
            RepositoryKind.LocalDirectory,
            IsWorkingDirectoryCopy: true,
            CanPush: false,
            CanPull: false,
            CanCreatePullRequest: false,
            CanMergeToSource: true);

        await using var harness = await ConfigureComponentServicesAsync(
            capabilities,
            projectName: "<b>Alpha</b>");

        var cut = RenderComponent<AufgabeDetail>(parameters => parameters.Add(page => page.Id, harness.AufgabeId));
        cut.WaitForAssertion(() => cut.Markup.Should().NotContain("Wird geladen..."));

        cut.Markup.Should().Contain("Projekt: &lt;b&gt;Alpha&lt;/b&gt;");
        cut.Markup.Should().NotContain("Projekt: <b>Alpha</b>");
    }

    /// <summary><summary>AufgabeDetail_ShouldRenderProjectTextDirectlyBelowTitle.</summary>.</summary>
    [Fact]
    /// <summary>AufgabeDetail_ShouldRenderProjectTextDirectlyBelowTitle.</summary>
    public async Task AufgabeDetail_ShouldRenderProjectTextDirectlyBelowTitle()
    {
        var capabilities = new GitActionCapabilities(
            RepositoryKind.LocalDirectory,
            IsWorkingDirectoryCopy: true,
            CanPush: false,
            CanPull: false,
            CanCreatePullRequest: false,
            CanMergeToSource: true);

        await using var harness = await ConfigureComponentServicesAsync(capabilities, projectName: "Alpha");

        var cut = RenderComponent<AufgabeDetail>(parameters => parameters.Add(page => page.Id, harness.AufgabeId));
        cut.WaitForAssertion(() => cut.Markup.Should().NotContain("Wird geladen..."));

        var headerContent = cut.Find(".page-header > div");
        var titleElement = headerContent.QuerySelector("h2.page-title");
        var projectElement = headerContent.QuerySelector("div");

        titleElement.Should().NotBeNull();
        projectElement.Should().NotBeNull();
        titleElement!.NextElementSibling.Should().BeSameAs(projectElement);
        projectElement!.TextContent.Should().Contain("Projekt: Alpha");
    }

    /// <summary><summary>AufgabeDetail_ShouldDisableRecoveryButtonAndShowReason_WhenAutomationIsRunning.</summary>.</summary>
    [Fact]
    /// <summary>AufgabeDetail_ShouldDisableRecoveryButtonAndShowReason_WhenAutomationIsRunning.</summary>
    public async Task AufgabeDetail_ShouldDisableRecoveryButtonAndShowReason_WhenAutomationIsRunning()
    {
        var capabilities = new GitActionCapabilities(
            RepositoryKind.LocalDirectory,
            IsWorkingDirectoryCopy: true,
            CanPush: false,
            CanPull: false,
            CanCreatePullRequest: false,
            CanMergeToSource: true);

        await using var harness = await ConfigureComponentServicesAsync(capabilities, AufgabeStatus.InArbeit, isRunning: true);

        var cut = RenderComponent<AufgabeDetail>(parameters => parameters.Add(page => page.Id, harness.AufgabeId));
        cut.WaitForAssertion(() => cut.Markup.Should().NotContain("Wird geladen..."));

        var recoveryButton = cut.FindAll("button").Single(button => button.TextContent.Contains("Aufgabe wiederherstellen"));
        recoveryButton.HasAttribute("disabled").Should().BeTrue();
        cut.Markup.Should().Contain("Wiederherstellung nicht möglich, Verarbeitung läuft noch.");
    }

    /// <summary><summary>AufgabeDetail_ShouldNotShowRecoveryButton_ForInvalidStates.</summary>.</summary>
    [Fact]
    /// <summary>AufgabeDetail_ShouldNotShowRecoveryButton_ForInvalidStates.</summary>
    public async Task AufgabeDetail_ShouldNotShowRecoveryButton_ForInvalidStates()
    {
        var capabilities = new GitActionCapabilities(
            RepositoryKind.LocalDirectory,
            IsWorkingDirectoryCopy: true,
            CanPush: false,
            CanPull: false,
            CanCreatePullRequest: false,
            CanMergeToSource: true);

        await using var harness = await ConfigureComponentServicesAsync(capabilities, AufgabeStatus.Gestartet, isRunning: false);

        var cut = RenderComponent<AufgabeDetail>(parameters => parameters.Add(page => page.Id, harness.AufgabeId));
        cut.WaitForAssertion(() => cut.Markup.Should().NotContain("Wird geladen..."));

        cut.FindAll("button").Any(button => button.TextContent.Contains("Aufgabe wiederherstellen")).Should().BeFalse();
    }

    /// <summary><summary>AufgabeDetail_ShouldRecoverTaskAndShowSuccess_WhenRecoveryIsTriggered.</summary>.</summary>
    [Fact]
    /// <summary>AufgabeDetail_ShouldRecoverTaskAndShowSuccess_WhenRecoveryIsTriggered.</summary>
    public async Task AufgabeDetail_ShouldRecoverTaskAndShowSuccess_WhenRecoveryIsTriggered()
    {
        var capabilities = new GitActionCapabilities(
            RepositoryKind.LocalDirectory,
            IsWorkingDirectoryCopy: true,
            CanPush: false,
            CanPull: false,
            CanCreatePullRequest: false,
            CanMergeToSource: true);

        await using var harness = await ConfigureComponentServicesAsync(capabilities, AufgabeStatus.Wartend, isRunning: false);

        var cut = RenderComponent<AufgabeDetail>(parameters => parameters.Add(page => page.Id, harness.AufgabeId));
        cut.WaitForAssertion(() => cut.Markup.Should().NotContain("Wird geladen..."));

        cut.FindAll("button").Single(button => button.TextContent.Contains("Aufgabe wiederherstellen")).Click();
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Aufgabe wiederherstellen?"));
        cut.FindAll("button").Single(button => button.TextContent.Contains("Ja, wiederherstellen")).Click();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Aufgabe wurde erfolgreich wiederhergestellt."));

        var loaded = await harness.Db.Aufgaben.AsNoTracking().SingleAsync(a => a.Id == harness.AufgabeId);
        loaded.Status.Should().Be(AufgabeStatus.Gestartet);
        harness.Db.Protokolleintraege
            .Count(e => e.AufgabeId == harness.AufgabeId && e.Typ == ProtokollTyp.StatusUebergang && e.Inhalt.Contains("Manuelle Wiederherstellung"))
            .Should().Be(1);
    }

    /// <summary><summary>AufgabeDetail_ShouldShowStatusResetButton_ForKiAktiv_WhenAutomationIsNotRunning.</summary>.</summary>
    [Fact]
    /// <summary>AufgabeDetail_ShouldShowStatusResetButton_ForKiAktiv_WhenAutomationIsNotRunning.</summary>
    public async Task AufgabeDetail_ShouldShowStatusResetButton_ForKiAktiv_WhenAutomationIsNotRunning()
    {
        var capabilities = new GitActionCapabilities(
            RepositoryKind.LocalDirectory,
            IsWorkingDirectoryCopy: true,
            CanPush: false,
            CanPull: false,
            CanCreatePullRequest: false,
            CanMergeToSource: true);

        await using var harness = await ConfigureComponentServicesAsync(capabilities, AufgabeStatus.InArbeit, isRunning: false);

        var cut = RenderComponent<AufgabeDetail>(parameters => parameters.Add(page => page.Id, harness.AufgabeId));
        cut.WaitForAssertion(() => cut.Markup.Should().NotContain("Wird geladen..."));

        var statusResetButton = cut.FindAll("button").Single(button => button.TextContent.Contains("Status zurücksetzen"));
        statusResetButton.HasAttribute("disabled").Should().BeFalse();

        statusResetButton.Click();
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Status zurücksetzen?"));

        cut.FindAll("button").Single(button => button.TextContent.Contains("Ja, Status zurücksetzen")).Click();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Status wurde zurückgesetzt. Eine neue Anfrage kann jetzt gesendet werden."));

        var loaded = await harness.Db.Aufgaben.AsNoTracking().SingleAsync(a => a.Id == harness.AufgabeId);
        loaded.Status.Should().Be(AufgabeStatus.Gestartet);
    }

    /// <summary><summary>AufgabeDetail_ShouldDisableStatusResetButtonAndShowReason_WhenAutomationIsRunning.</summary>.</summary>
    [Fact]
    /// <summary>AufgabeDetail_ShouldDisableStatusResetButtonAndShowReason_WhenAutomationIsRunning.</summary>
    public async Task AufgabeDetail_ShouldDisableStatusResetButtonAndShowReason_WhenAutomationIsRunning()
    {
        var capabilities = new GitActionCapabilities(
            RepositoryKind.LocalDirectory,
            IsWorkingDirectoryCopy: true,
            CanPush: false,
            CanPull: false,
            CanCreatePullRequest: false,
            CanMergeToSource: true);

        await using var harness = await ConfigureComponentServicesAsync(capabilities, AufgabeStatus.InArbeit, isRunning: true);

        var cut = RenderComponent<AufgabeDetail>(parameters => parameters.Add(page => page.Id, harness.AufgabeId));
        cut.WaitForAssertion(() => cut.Markup.Should().NotContain("Wird geladen..."));

        var statusResetButton = cut.FindAll("button").Single(button => button.TextContent.Contains("Status zurücksetzen"));
        statusResetButton.HasAttribute("disabled").Should().BeTrue();
        cut.Markup.Should().Contain("Status kann nicht zurückgesetzt werden, solange die Verarbeitung läuft.");
    }

    /// <summary>Prüft, dass offene Aufgaben die neue Verwerfen-Aktion anzeigen.</summary>
    [Fact]
    public async Task AufgabeDetail_ShouldShowVerwerfenButton_ForOffeneAufgabe()
    {
        var capabilities = new GitActionCapabilities(
            RepositoryKind.LocalDirectory,
            IsWorkingDirectoryCopy: true,
            CanPush: false,
            CanPull: false,
            CanCreatePullRequest: false,
            CanMergeToSource: true);

        await using var harness = await ConfigureComponentServicesAsync(capabilities, AufgabeStatus.Neu, isRunning: false);

        var cut = RenderComponent<AufgabeDetail>(parameters => parameters.Add(page => page.Id, harness.AufgabeId));
        cut.WaitForAssertion(() => cut.Markup.Should().NotContain("Wird geladen..."));

        var buttonTexts = cut.FindAll("button").Select(button => button.TextContent.Trim()).ToArray();
        buttonTexts.Should().Contain("🗑️ Verwerfen");
        buttonTexts.Should().Contain("🚀 Entwicklung starten");
        buttonTexts.Should().NotContain("📦 Archivieren");
        buttonTexts.Should().NotContain("🗑️ Löschen");
    }

    /// <summary>Prüft, dass offene Aufgaben direkt archiviert werden können.</summary>
    [Fact]
    public async Task AufgabeDetail_ShouldArchiveOffeneAufgabe_WhenVerwerfenArchivierenSelected()
    {
        var capabilities = new GitActionCapabilities(
            RepositoryKind.LocalDirectory,
            IsWorkingDirectoryCopy: true,
            CanPush: false,
            CanPull: false,
            CanCreatePullRequest: false,
            CanMergeToSource: true);

        await using var harness = await ConfigureComponentServicesAsync(capabilities, AufgabeStatus.Neu, isRunning: false);

        var cut = RenderComponent<AufgabeDetail>(parameters => parameters.Add(page => page.Id, harness.AufgabeId));
        cut.WaitForAssertion(() => cut.Markup.Should().NotContain("Wird geladen..."));

        cut.FindAll("button").Single(button => button.TextContent.Contains("Verwerfen")).Click();
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Aufgabe verwerfen?"));

        cut.FindAll("button").Single(button => button.TextContent.Contains("Archivieren")).Click();

        var loaded = await harness.Db.Aufgaben.AsNoTracking().SingleAsync(a => a.Id == harness.AufgabeId);

        loaded.Status.Should().Be(AufgabeStatus.Archiviert);
    }

    /// <summary>Prüft, dass offene Aufgaben direkt gelöscht werden können.</summary>
    [Fact]
    public async Task AufgabeDetail_ShouldDeleteOffeneAufgabe_WhenVerwerfenLoeschenSelected()
    {
        var capabilities = new GitActionCapabilities(
            RepositoryKind.LocalDirectory,
            IsWorkingDirectoryCopy: true,
            CanPush: false,
            CanPull: false,
            CanCreatePullRequest: false,
            CanMergeToSource: true);

        await using var harness = await ConfigureComponentServicesAsync(capabilities, AufgabeStatus.Neu, isRunning: false);

        var cut = RenderComponent<AufgabeDetail>(parameters => parameters.Add(page => page.Id, harness.AufgabeId));
        cut.WaitForAssertion(() => cut.Markup.Should().NotContain("Wird geladen..."));

        cut.FindAll("button").Single(button => button.TextContent.Contains("Verwerfen")).Click();
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Aufgabe verwerfen?"));

        cut.FindAll("button").Single(button => button.TextContent.Contains("Dauerhaft löschen")).Click();

        var loaded = await harness.Db.Aufgaben.AsNoTracking().SingleOrDefaultAsync(a => a.Id == harness.AufgabeId);

        loaded.Should().BeNull();
    }

    /// <summary>Prüft, dass Push/Pull und Pull-Request bei lokalem Repo mit separatem Arbeitsverzeichnis deaktiviert sind.</summary>
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
        cut.FindAll("button").Single(button => button.TextContent.Trim() == "Projektverzeichnis").Click();

        var buttonTexts = cut.FindAll("button").Select(button => button.TextContent.Trim()).ToArray();
        buttonTexts.Should().Contain("▶️ Startskript ausführen");
        buttonTexts.Should().Contain("⬆️ Push");
        buttonTexts.Should().Contain("⬇️ Pull");
        buttonTexts.Should().Contain("🔀 Pull Request");

        cut.FindAll("button").Single(button => button.TextContent.Trim() == "⬆️ Push").HasAttribute("disabled").Should().BeTrue();
        cut.FindAll("button").Single(button => button.TextContent.Trim() == "⬇️ Pull").HasAttribute("disabled").Should().BeTrue();
        cut.FindAll("button").Single(button => button.TextContent.Trim() == "🔀 Pull Request").HasAttribute("disabled").Should().BeTrue();
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
        cut.FindAll("button").Single(button => button.TextContent.Trim() == "Projektverzeichnis").Click();

        var buttonTexts = cut.FindAll("button").Select(button => button.TextContent.Trim()).ToArray();
        buttonTexts.Should().Contain("▶️ Startskript ausführen");
        buttonTexts.Should().Contain("⬆️ Push");
        buttonTexts.Should().Contain("⬇️ Pull");
        buttonTexts.Should().Contain("🔀 Pull Request");
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
        cut.FindAll("button").Single(button => button.TextContent.Trim() == "Projektverzeichnis").Click();

        var buttonTexts = cut.FindAll("button").Select(button => button.TextContent.Trim()).ToArray();
        buttonTexts.Should().Contain("⬆️ Push");
        buttonTexts.Should().Contain("⬇️ Pull");
        buttonTexts.Should().Contain("🔀 Pull Request");

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
        cut.FindAll("button").Single(button => button.TextContent.Trim() == "Projektverzeichnis").Click();

        var buttonTexts = cut.FindAll("button").Select(button => button.TextContent.Trim()).ToArray();
        buttonTexts.Should().Contain("⬆️ Push");
        buttonTexts.Should().Contain("⬇️ Pull");
        buttonTexts.Should().Contain("🔀 Pull Request");

        cut.FindAll("button").Single(button => button.TextContent.Trim() == "⬆️ Push").HasAttribute("disabled").Should().BeTrue();
        cut.FindAll("button").Single(button => button.TextContent.Trim() == "⬇️ Pull").HasAttribute("disabled").Should().BeTrue();
        cut.FindAll("button").Single(button => button.TextContent.Trim() == "🔀 Pull Request").HasAttribute("disabled").Should().BeTrue();

        harness.SelectedGitPlugin.Verify(
            plugin => plugin.GetGitActionCapabilitiesAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
        harness.DefaultGitPlugin.Verify(
            plugin => plugin.GetGitActionCapabilitiesAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary><summary>AufgabeDetail_ShouldInvokePullOnProjectSelectedGitPlugin.</summary>.</summary>
    [Fact]
    /// <summary>AufgabeDetail_ShouldInvokePullOnProjectSelectedGitPlugin.</summary>
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
        cut.FindAll("button").Single(button => button.TextContent.Trim() == "Projektverzeichnis").Click();

        cut.FindAll("button").First(button => button.TextContent.Trim() == "⬇️ Pull").Click();
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Jetzt pullen"));
        cut.FindAll("button").First(button => button.TextContent.Trim() == "Jetzt pullen").Click();

        cut.WaitForAssertion(() =>
            harness.SelectedGitPlugin.Verify(
                plugin => plugin.PullAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.AtLeastOnce));
        harness.DefaultGitPlugin.Verify(
            plugin => plugin.PullAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary><summary>AufgabeDetail_ShouldShowDiffButtonAndNavigateToWrapperRoute_WhenLatestDiffExists.</summary>.</summary>
    [Fact]
    /// <summary>AufgabeDetail_ShouldShowDiffButtonAndNavigateToWrapperRoute_WhenLatestDiffExists.</summary>
    public async Task AufgabeDetail_ShouldShowDiffButtonAndNavigateToWrapperRoute_WhenLatestDiffExists()
    {
        var capabilities = new GitActionCapabilities(
            RepositoryKind.LocalDirectory,
            IsWorkingDirectoryCopy: true,
            CanPush: false,
            CanPull: false,
            CanCreatePullRequest: false,
            CanMergeToSource: true);

        await using var harness = await ConfigureComponentServicesAsync(
            capabilities,
            latestDiffResultId: Guid.NewGuid());

        var navigationManager = Services.GetRequiredService<NavigationManager>();
        var cut = RenderComponent<AufgabeDetail>(parameters => parameters.Add(page => page.Id, harness.AufgabeId));
        cut.WaitForAssertion(() => cut.Markup.Should().NotContain("Wird geladen..."));

        var diffButton = cut.FindAll("button").Single(button => button.TextContent.Trim() == "🔎 Diff anzeigen");
        diffButton.Click();

        navigationManager.Uri.Should().EndWith($"/diff/{harness.LatestDiffResultId}");
    }

    /// <summary><summary>AufgabeDetail_ShouldNotShowDiffButton_WhenNoLatestDiffExists.</summary>.</summary>
    [Fact]
    /// <summary>AufgabeDetail_ShouldNotShowDiffButton_WhenNoLatestDiffExists.</summary>
    public async Task AufgabeDetail_ShouldNotShowDiffButton_WhenNoLatestDiffExists()
    {
        var capabilities = new GitActionCapabilities(
            RepositoryKind.LocalDirectory,
            IsWorkingDirectoryCopy: true,
            CanPush: false,
            CanPull: false,
            CanCreatePullRequest: false,
            CanMergeToSource: true);

        await using var harness = await ConfigureComponentServicesAsync(capabilities, latestDiffResultId: null);

        var cut = RenderComponent<AufgabeDetail>(parameters => parameters.Add(page => page.Id, harness.AufgabeId));
        cut.WaitForAssertion(() => cut.Markup.Should().NotContain("Wird geladen..."));

        cut.FindAll("button").Any(button => button.TextContent.Trim() == "🔎 Diff anzeigen").Should().BeFalse();
    }

    private async Task<TestHarness> ConfigureComponentServicesAsync(
        GitActionCapabilities capabilities,
        AufgabeStatus status = AufgabeStatus.Gestartet,
        bool isRunning = false,
        Guid? latestDiffResultId = null,
        string projectName = "BUnit-Test-Projekt")
    {
        var db = TestDbContextFactory.Create();
        var projekt = new Projekt
        {
            Id = Guid.NewGuid(),
            Name = projectName,
            Status = ProjektStatus.Aktiv,
            ErstellungsDatum = DateTimeOffset.UtcNow
        };

        var aufgabe = new Aufgabe
        {
            Id = Guid.NewGuid(),
            ProjektId = projekt.Id,
            Titel = "BUnit-Test-Aufgabe",
            Status = status,
            AgentenpaketName = "paket-a",
            AgentenName = "agent-a",
            LokalerKlonPfad = Path.GetTempPath(),
            ErstellungsDatum = DateTimeOffset.UtcNow
        };

        db.Projekte.Add(projekt);
        db.Aufgaben.Add(aufgabe);
        if (latestDiffResultId.HasValue)
        {
            db.DiffResults.Add(new DiffResult
            {
                Id = latestDiffResultId.Value,
                AufgabeId = aufgabe.Id,
                FilePath = "src/example.cs",
                SourceVersion = "HEAD~1",
                TargetVersion = "HEAD",
                DiffType = DiffType.Full,
                Status = DiffResultStatus.Generated,
                GeneratedAt = DateTimeOffset.UtcNow,
                GeneratedBy = nameof(AufgabeDetailGitActionsBunitTests),
            });
        }

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
        kiPluginMock
            .Setup(plugin => plugin.GetAvailableAgentsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([agentInfo]);
        kiPluginMock
            .Setup(plugin => plugin.IsAgentPackageCompatibleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        kiPluginMock
            .Setup(plugin => plugin.DeployAgentPackageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
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
            arbeitsverzeichnisResolverMock.Object,
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
        runningStatusSourceMock.Setup(source => source.IsRunning(It.IsAny<Guid>())).Returns(isRunning);
        Services.AddSingleton(new Mock<IServiceScopeFactory>().Object);
        Services.AddSingleton(pluginManagerMock.Object);
        Services.AddSingleton(pluginSelection);
        Services.AddSingleton(aufgabeService);
        Services.AddSingleton(runningStatusSourceMock.Object);
        Services.AddSingleton(new AufgabeRecoveryService(db, runningStatusSourceMock.Object, NullLogger<AufgabeRecoveryService>.Instance));
        Services.AddSingleton(entwicklungsprozessService);
        Services.AddSingleton(new KiAusfuehrungsService(NullLogger<KiAusfuehrungsService>.Instance, NullLoggerFactory.Instance, new Mock<IServiceScopeFactory>().Object));
        Services.AddSingleton(gitService);
        Services.AddSingleton(protokollService);
        Services.AddSingleton(projektService);
        Services.AddSingleton(agentPackageServiceMock.Object);
        Services.AddSingleton(workspaceBrowserServiceMock.Object);
        Services.AddSingleton<ILogger<AufgabeDetail>>(NullLogger<AufgabeDetail>.Instance);

        return new TestHarness(db, aufgabe.Id, latestDiffResultId, defaultGitPlugin: gitPluginMock, selectedGitPlugin: gitPluginMock);
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
            Status = AufgabeStatus.Gestartet,
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
        kiPluginMock
            .Setup(plugin => plugin.GetAvailableAgentsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([agentInfo]);
        kiPluginMock
            .Setup(plugin => plugin.IsAgentPackageCompatibleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        kiPluginMock
            .Setup(plugin => plugin.DeployAgentPackageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
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
            arbeitsverzeichnisResolverMock.Object,
            NullLogger<EntwicklungsprozessService>.Instance);

        var gitService = new GitOrchestrationService(
            aufgabeService,
            projektService,
            protokollService,
            defaultGitPluginMock.Object,
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
        Services.AddSingleton(new KiAusfuehrungsService(NullLogger<KiAusfuehrungsService>.Instance, NullLoggerFactory.Instance, new Mock<IServiceScopeFactory>().Object));
        Services.AddSingleton(gitService);
        Services.AddSingleton(protokollService);
        Services.AddSingleton(projektService);
        Services.AddSingleton(agentPackageServiceMock.Object);
        Services.AddSingleton(workspaceBrowserServiceMock.Object);
        Services.AddSingleton<ILogger<AufgabeDetail>>(NullLogger<AufgabeDetail>.Instance);

        return new TestHarness(db, aufgabe.Id, null, defaultGitPluginMock, selectedGitPluginMock);
    }

    private sealed class TestHarness(
        Softwareschmiede.Infrastructure.Data.SoftwareschmiededDbContext db,
        Guid aufgabeId,
        Guid? latestDiffResultId,
        Mock<IGitPlugin> defaultGitPlugin,
        Mock<IGitPlugin> selectedGitPlugin) : IAsyncDisposable
    {
        public Softwareschmiede.Infrastructure.Data.SoftwareschmiededDbContext Db { get; } = db;
        public Guid AufgabeId { get; } = aufgabeId;
        public Guid? LatestDiffResultId { get; } = latestDiffResultId;
        public Mock<IGitPlugin> DefaultGitPlugin { get; } = defaultGitPlugin;
        public Mock<IGitPlugin> SelectedGitPlugin { get; } = selectedGitPlugin;

        /// <summary>DisposeAsync.</summary>
        public ValueTask DisposeAsync()
        {
            db.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
