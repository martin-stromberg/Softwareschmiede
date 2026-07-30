using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Infrastructure.Data;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Tests fuer <see cref="PullRequestMonitoringService"/>.</summary>
public sealed class PullRequestMonitoringServiceTests
{
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 7, 29, 12, 0, 0, TimeSpan.Zero));

    /// <summary>Transiente Providerfehler speichern LastError und bleiben nach Retry-Delay faellig.</summary>
    [Fact]
    public async Task RunOnceAsync_ShouldKeepPullRequestDue_WhenProviderFailsTransiently()
    {
        using var db = TestDbContextFactory.Create();
        var (_, aufgabe) = AddAufgabe(db);
        var pullRequest = AddPullRequest(db, aufgabe.Id, PullRequestMonitoringPhase.PreMergeRunning, _timeProvider.GetUtcNow());
        var plugin = CreatePluginMock();
        plugin.Setup(p => p.GetPullRequestStatusAsync("owner/repo", 42, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("rate limit"));
        using var provider = BuildProvider(db, plugin.Object);
        var sut = provider.GetRequiredService<PullRequestMonitoringService>();

        await sut.RunOnceAsync();

        var saved = db.PullRequestReferenzen.Single(p => p.Id == pullRequest.Id);
        Assert.Equal(PullRequestMonitoringPhase.PreMergeRunning, saved.MonitoringPhase);
        Assert.Equal("rate limit", saved.LastError);
        Assert.True(saved.NextCheckUtc > _timeProvider.GetUtcNow());

        _timeProvider.Advance(TimeSpan.FromMinutes(5));
        var due = await provider.GetRequiredService<PullRequestReferenzService>()
            .GetDueForMonitoringAsync(_timeProvider.GetUtcNow(), 10);
        Assert.Contains(due, p => p.Id == pullRequest.Id);
    }

    /// <summary>Gemergte PRs ohne Merge-SHA werden als unsicherer Wartezustand modelliert.</summary>
    [Fact]
    public async Task RunOnceAsync_ShouldMarkPostMergeUncertain_WhenMergedPullRequestHasNoMergeSha()
    {
        using var db = TestDbContextFactory.Create();
        var (_, aufgabe) = AddAufgabe(db);
        var pullRequest = AddPullRequest(db, aufgabe.Id, PullRequestMonitoringPhase.PreMergeSucceeded, _timeProvider.GetUtcNow());
        var plugin = CreatePluginMock();
        plugin.Setup(p => p.GetPullRequestStatusAsync("owner/repo", 42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Status(PullRequestStatus.Merged, PullRequestMergeStatus.Merged, mergeCommitSha: null));
        plugin.Setup(p => p.GetPullRequestWorkflowRunsAsync("owner/repo", 42, "head", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        using var provider = BuildProvider(db, plugin.Object);

        await provider.GetRequiredService<PullRequestMonitoringService>().RunOnceAsync();

        var saved = db.PullRequestReferenzen.Single(p => p.Id == pullRequest.Id);
        Assert.Equal(PullRequestMonitoringPhase.PostMergeUncertain, saved.MonitoringPhase);
        Assert.Contains("Merge-Commit-SHA", saved.LastError);
        Assert.True(saved.NextCheckUtc > _timeProvider.GetUtcNow());
    }

    /// <summary>ApprovalOnly wird nicht als gemergter Abschluss persistiert und wird nicht sofort erneut ausgefuehrt.</summary>
    [Fact]
    public async Task RunOnceAsync_ShouldPersistApprovalOnlyAsNonTerminalApproval()
    {
        using var db = TestDbContextFactory.Create();
        var (_, aufgabe) = AddAufgabe(db);
        var pullRequest = AddPullRequest(db, aufgabe.Id, PullRequestMonitoringPhase.PreMergeRunning, _timeProvider.GetUtcNow());
        var plugin = CreatePluginMock();
        plugin.Setup(p => p.GetPullRequestStatusAsync("owner/repo", 42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Status(PullRequestStatus.Open, PullRequestMergeStatus.Mergeable));
        plugin.Setup(p => p.GetPullRequestWorkflowRunsAsync("owner/repo", 42, "head", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new PullRequestWorkflowRunInfo("1", "Build", null, "head", "feature/pr", WorkflowRunStatus.Completed, WorkflowRunConclusion.Success, null, null, false)
            ]);
        plugin.Setup(p => p.CompletePullRequestAsync(
                "owner/repo",
                42,
                It.Is<PullRequestCompletionOptions>(o => o.Strategy == PullRequestCompletionStrategy.ApprovalOnly),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(PullRequestCompletionResult.Approved("approved"));
        using var provider = BuildProvider(
            db,
            plugin.Object,
            new Dictionary<string, string>
            {
                ["Softwareschmiede.GitHub.AutoCompletePullRequests"] = "true",
                ["Softwareschmiede.GitHub.PullRequestCompletionStrategy"] = "ApprovalOnly"
            });

        await provider.GetRequiredService<PullRequestMonitoringService>().RunOnceAsync();

        var saved = db.PullRequestReferenzen.Single(p => p.Id == pullRequest.Id);
        Assert.Equal(PullRequestMonitoringPhase.Approved, saved.MonitoringPhase);
        Assert.Equal(PullRequestStatus.Open, saved.Status);
        Assert.Null(saved.MergeCommitSha);
        Assert.True(saved.NextCheckUtc > _timeProvider.GetUtcNow());
        plugin.Verify(p => p.CompletePullRequestAsync(
            "owner/repo",
            42,
            It.IsAny<PullRequestCompletionOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>ApprovalOnly wird bei unveraendert offenem PR nach dem Warteintervall nicht erneut ausgefuehrt.</summary>
    [Fact]
    public async Task RunOnceAsync_ShouldNotApproveAgain_WhenApprovalOnlyAlreadySucceeded()
    {
        using var db = TestDbContextFactory.Create();
        var (_, aufgabe) = AddAufgabe(db);
        var pullRequest = AddPullRequest(db, aufgabe.Id, PullRequestMonitoringPhase.PreMergeRunning, _timeProvider.GetUtcNow());
        var plugin = CreatePluginMock();
        plugin.Setup(p => p.GetPullRequestStatusAsync("owner/repo", 42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Status(PullRequestStatus.Open, PullRequestMergeStatus.Mergeable));
        plugin.Setup(p => p.GetPullRequestWorkflowRunsAsync("owner/repo", 42, "head", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new PullRequestWorkflowRunInfo("1", "Build", null, "head", "feature/pr", WorkflowRunStatus.Completed, WorkflowRunConclusion.Success, null, null, false)
            ]);
        plugin.Setup(p => p.CompletePullRequestAsync(
                "owner/repo",
                42,
                It.Is<PullRequestCompletionOptions>(o => o.Strategy == PullRequestCompletionStrategy.ApprovalOnly),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(PullRequestCompletionResult.Approved("approved"));
        using var provider = BuildProvider(
            db,
            plugin.Object,
            new Dictionary<string, string>
            {
                ["Softwareschmiede.GitHub.AutoCompletePullRequests"] = "true",
                ["Softwareschmiede.GitHub.PullRequestCompletionStrategy"] = "ApprovalOnly"
            });
        var sut = provider.GetRequiredService<PullRequestMonitoringService>();

        await sut.RunOnceAsync();
        _timeProvider.Advance(TimeSpan.FromMinutes(5));
        await sut.RunOnceAsync();

        var saved = db.PullRequestReferenzen.Single(p => p.Id == pullRequest.Id);
        Assert.Equal(PullRequestMonitoringPhase.Approved, saved.MonitoringPhase);
        Assert.Equal(PullRequestStatus.Open, saved.Status);
        Assert.Contains("genehmigt", saved.LastError);
        plugin.Verify(p => p.CompletePullRequestAsync(
            "owner/repo",
            42,
            It.IsAny<PullRequestCompletionOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>AutoMerge ohne tatsaechlichen Merge bleibt nicht-terminal und weiter faellig.</summary>
    [Fact]
    public async Task RunOnceAsync_ShouldKeepAutoMergeOpen_WhenCompletionDidNotMergePullRequest()
    {
        using var db = TestDbContextFactory.Create();
        var (_, aufgabe) = AddAufgabe(db);
        var pullRequest = AddPullRequest(db, aufgabe.Id, PullRequestMonitoringPhase.PreMergeRunning, _timeProvider.GetUtcNow());
        var plugin = CreatePluginMock();
        plugin.Setup(p => p.GetPullRequestStatusAsync("owner/repo", 42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Status(PullRequestStatus.Open, PullRequestMergeStatus.Mergeable));
        plugin.Setup(p => p.GetPullRequestWorkflowRunsAsync("owner/repo", 42, "head", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new PullRequestWorkflowRunInfo("1", "Build", null, "head", "feature/pr", WorkflowRunStatus.Completed, WorkflowRunConclusion.Success, null, null, false)
            ]);
        plugin.Setup(p => p.CompletePullRequestAsync(
                "owner/repo",
                42,
                It.Is<PullRequestCompletionOptions>(o => o.Strategy == PullRequestCompletionStrategy.AutoMerge),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(PullRequestCompletionResult.WaitingForMerge("auto merge enabled"));
        using var provider = BuildProvider(
            db,
            plugin.Object,
            new Dictionary<string, string>
            {
                ["Softwareschmiede.GitHub.AutoCompletePullRequests"] = "true",
                ["Softwareschmiede.GitHub.PullRequestCompletionStrategy"] = "AutoMerge"
            });

        await provider.GetRequiredService<PullRequestMonitoringService>().RunOnceAsync();

        var saved = db.PullRequestReferenzen.Single(p => p.Id == pullRequest.Id);
        Assert.Equal(PullRequestMonitoringPhase.Approved, saved.MonitoringPhase);
        Assert.Equal(PullRequestStatus.Open, saved.Status);
        Assert.Null(saved.MergeCommitSha);
        Assert.Contains("Auto-Merge", saved.LastError);
        Assert.True(saved.NextCheckUtc > _timeProvider.GetUtcNow());
    }

    /// <summary>AutoMerge wird nach bereits aktiviertem Wartestatus bei unveraendert offenem PR nicht erneut getriggert.</summary>
    [Fact]
    public async Task RunOnceAsync_ShouldNotEnableAutoMergeAgain_WhenWaitingForMerge()
    {
        using var db = TestDbContextFactory.Create();
        var (_, aufgabe) = AddAufgabe(db);
        var pullRequest = AddPullRequest(db, aufgabe.Id, PullRequestMonitoringPhase.PreMergeRunning, _timeProvider.GetUtcNow());
        var plugin = CreatePluginMock();
        plugin.Setup(p => p.GetPullRequestStatusAsync("owner/repo", 42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Status(PullRequestStatus.Open, PullRequestMergeStatus.Mergeable));
        plugin.Setup(p => p.GetPullRequestWorkflowRunsAsync("owner/repo", 42, "head", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new PullRequestWorkflowRunInfo("1", "Build", null, "head", "feature/pr", WorkflowRunStatus.Completed, WorkflowRunConclusion.Success, null, null, false)
            ]);
        plugin.Setup(p => p.CompletePullRequestAsync(
                "owner/repo",
                42,
                It.Is<PullRequestCompletionOptions>(o => o.Strategy == PullRequestCompletionStrategy.AutoMerge),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(PullRequestCompletionResult.WaitingForMerge("auto merge enabled"));
        using var provider = BuildProvider(
            db,
            plugin.Object,
            new Dictionary<string, string>
            {
                ["Softwareschmiede.GitHub.AutoCompletePullRequests"] = "true",
                ["Softwareschmiede.GitHub.PullRequestCompletionStrategy"] = "AutoMerge"
            });
        var sut = provider.GetRequiredService<PullRequestMonitoringService>();

        await sut.RunOnceAsync();
        _timeProvider.Advance(TimeSpan.FromMinutes(5));
        await sut.RunOnceAsync();

        var saved = db.PullRequestReferenzen.Single(p => p.Id == pullRequest.Id);
        Assert.Equal(PullRequestMonitoringPhase.Approved, saved.MonitoringPhase);
        Assert.Equal(PullRequestStatus.Open, saved.Status);
        Assert.Contains("Auto-Merge", saved.LastError);
        plugin.Verify(p => p.CompletePullRequestAsync(
            "owner/repo",
            42,
            It.IsAny<PullRequestCompletionOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Geskippte Pre-Merge-Runs gelten als neutral erfolgreicher Abschluss.</summary>
    [Fact]
    public async Task RunOnceAsync_ShouldTreatSkippedPreMergeRunsAsSucceeded()
    {
        using var db = TestDbContextFactory.Create();
        var (_, aufgabe) = AddAufgabe(db);
        var pullRequest = AddPullRequest(db, aufgabe.Id, PullRequestMonitoringPhase.PreMergeRunning, _timeProvider.GetUtcNow());
        var plugin = CreatePluginMock();
        plugin.Setup(p => p.GetPullRequestStatusAsync("owner/repo", 42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Status(PullRequestStatus.Open, PullRequestMergeStatus.Mergeable));
        plugin.Setup(p => p.GetPullRequestWorkflowRunsAsync("owner/repo", 42, "head", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new PullRequestWorkflowRunInfo("1", "Optional", null, "head", "feature/pr", WorkflowRunStatus.Completed, WorkflowRunConclusion.Skipped, null, null, false)
            ]);
        using var provider = BuildProvider(db, plugin.Object);

        await provider.GetRequiredService<PullRequestMonitoringService>().RunOnceAsync();

        var saved = db.PullRequestReferenzen.Single(p => p.Id == pullRequest.Id);
        Assert.Equal(PullRequestMonitoringPhase.PreMergeSucceeded, saved.MonitoringPhase);
        Assert.Null(saved.LastError);
    }

    /// <summary>Geskippte Post-Merge-Runs gelten wie Pre-Merge als neutral erfolgreicher Abschluss.</summary>
    [Fact]
    public async Task RunOnceAsync_ShouldTreatSkippedPostMergeRunsAsSucceeded()
    {
        using var db = TestDbContextFactory.Create();
        var (_, aufgabe) = AddAufgabe(db);
        var pullRequest = AddPullRequest(db, aufgabe.Id, PullRequestMonitoringPhase.PreMergeSucceeded, _timeProvider.GetUtcNow());
        var plugin = CreatePluginMock();
        plugin.Setup(p => p.GetPullRequestStatusAsync("owner/repo", 42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Status(PullRequestStatus.Merged, PullRequestMergeStatus.Merged, mergeCommitSha: "merge"));
        plugin.Setup(p => p.GetPullRequestWorkflowRunsAsync("owner/repo", 42, "head", "merge", It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new PullRequestWorkflowRunInfo("1", "Optional", null, "merge", "main", WorkflowRunStatus.Completed, WorkflowRunConclusion.Skipped, null, null, true)
            ]);
        using var provider = BuildProvider(db, plugin.Object);

        await provider.GetRequiredService<PullRequestMonitoringService>().RunOnceAsync();

        var saved = db.PullRequestReferenzen.Single(p => p.Id == pullRequest.Id);
        Assert.Equal(PullRequestMonitoringPhase.PostMergeSucceeded, saved.MonitoringPhase);
        Assert.Null(saved.LastError);
    }

    /// <summary>Terminal abgeschlossene PRs fallen aus der faelligen Menge.</summary>
    [Fact]
    public async Task GetDueForMonitoringAsync_ShouldExcludeCompletedPullRequests()
    {
        using var db = TestDbContextFactory.Create();
        var (_, aufgabe) = AddAufgabe(db);
        var pullRequest = AddPullRequest(db, aufgabe.Id, PullRequestMonitoringPhase.Completed, _timeProvider.GetUtcNow().AddMinutes(-1));
        var service = new PullRequestReferenzService(db, _timeProvider, NullLogger<PullRequestReferenzService>.Instance);

        var due = await service.GetDueForMonitoringAsync(_timeProvider.GetUtcNow(), 10);

        Assert.DoesNotContain(due, p => p.Id == pullRequest.Id);
    }

    /// <summary>Ein manueller Refresh aktualisiert auch terminal gespeicherte PRs erneut vom Provider.</summary>
    [Fact]
    public async Task RefreshAufgabeAsync_ShouldUpdateCompletedPullRequestStatus_WhenProviderReportsMerged()
    {
        using var db = TestDbContextFactory.Create();
        var (_, aufgabe) = AddAufgabe(db);
        var pullRequest = AddPullRequest(db, aufgabe.Id, PullRequestMonitoringPhase.Completed, _timeProvider.GetUtcNow().AddMinutes(-1));
        var plugin = CreatePluginMock();
        plugin.Setup(p => p.GetPullRequestStatusAsync("owner/repo", 42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Status(PullRequestStatus.Merged, PullRequestMergeStatus.Merged, mergeCommitSha: "merge"));
        plugin.Setup(p => p.GetPullRequestWorkflowRunsAsync("owner/repo", 42, "head", "merge", It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new PullRequestWorkflowRunInfo("1", "Post Merge", null, "merge", "main", WorkflowRunStatus.Completed, WorkflowRunConclusion.Success, null, null, true)
            ]);
        using var provider = BuildProvider(db, plugin.Object);

        await provider.GetRequiredService<PullRequestMonitoringService>().RefreshAufgabeAsync(aufgabe.Id);

        var saved = db.PullRequestReferenzen.Single(p => p.Id == pullRequest.Id);
        Assert.Equal(PullRequestStatus.Merged, saved.Status);
        Assert.Equal(PullRequestMergeStatus.Merged, saved.MergeStatus);
        Assert.Equal("merge", saved.MergeCommitSha);
        Assert.Equal(PullRequestMonitoringPhase.PostMergeSucceeded, saved.MonitoringPhase);
    }

    /// <summary>Ein manueller Refresh aktualisiert nur den Providerstatus und loest keinen Auto-Abschluss aus.</summary>
    [Fact]
    public async Task RefreshAufgabeAsync_ShouldNotCompletePullRequest_WhenOpenPreMergeSucceeded()
    {
        using var db = TestDbContextFactory.Create();
        var (_, aufgabe) = AddAufgabe(db);
        var pullRequest = AddPullRequest(db, aufgabe.Id, PullRequestMonitoringPhase.Completed, _timeProvider.GetUtcNow().AddMinutes(-1));
        var plugin = CreatePluginMock();
        plugin.Setup(p => p.GetPullRequestStatusAsync("owner/repo", 42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Status(PullRequestStatus.Open, PullRequestMergeStatus.Mergeable));
        plugin.Setup(p => p.GetPullRequestWorkflowRunsAsync("owner/repo", 42, "head", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new PullRequestWorkflowRunInfo("1", "Build", null, "head", "feature/pr", WorkflowRunStatus.Completed, WorkflowRunConclusion.Success, null, null, false)
            ]);
        using var provider = BuildProvider(
            db,
            plugin.Object,
            new Dictionary<string, string>
            {
                ["Softwareschmiede.GitHub.AutoCompletePullRequests"] = "true"
            });

        await provider.GetRequiredService<PullRequestMonitoringService>().RefreshAufgabeAsync(aufgabe.Id);

        var saved = db.PullRequestReferenzen.Single(p => p.Id == pullRequest.Id);
        Assert.Equal(PullRequestStatus.Open, saved.Status);
        Assert.Equal(PullRequestMonitoringPhase.PreMergeSucceeded, saved.MonitoringPhase);
        plugin.Verify(p => p.CompletePullRequestAsync(
            "owner/repo",
            42,
            It.IsAny<PullRequestCompletionOptions>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private ServiceProvider BuildProvider(
        SoftwareschmiededDbContext db,
        IGitPlugin gitPlugin,
        IReadOnlyDictionary<string, string>? settings = null)
    {
        var credentials = new Mock<ICredentialStore>();
        credentials.Setup(c => c.GetCredential(It.IsAny<string>()))
            .Returns((string key) => settings is not null && settings.TryGetValue(key, out var value) ? value : null);

        var pluginManager = new Mock<IPluginManager>();
        pluginManager.Setup(p => p.GetSourceCodeManagementPlugins()).Returns([gitPlugin]);
        pluginManager.Setup(p => p.GetDefaultSourceCodeManagementPlugin()).Returns(gitPlugin);

        var services = new ServiceCollection();
        services.AddSingleton(db);
        services.AddSingleton<TimeProvider>(_timeProvider);
        services.AddSingleton(pluginManager.Object);
        services.AddSingleton(credentials.Object);
        services.AddSingleton(new PluginSettingsService(credentials.Object, NullLogger<PluginSettingsService>.Instance));
        services.AddSingleton<Microsoft.Extensions.Logging.ILogger<PullRequestReferenzService>>(NullLogger<PullRequestReferenzService>.Instance);
        services.AddScoped<PullRequestReferenzService>();
        services.AddScoped<ProtokollService>(_ => new ProtokollService(db, NullLogger<ProtokollService>.Instance));
        services.AddSingleton<Microsoft.Extensions.Logging.ILogger<PullRequestMonitoringService>>(NullLogger<PullRequestMonitoringService>.Instance);
        services.AddSingleton<PullRequestMonitoringService>();
        return services.BuildServiceProvider();
    }

    private static Mock<IGitPlugin> CreatePluginMock()
    {
        var plugin = new Mock<IGitPlugin>();
        plugin.SetupGet(p => p.PluginName).Returns("GitHub");
        plugin.SetupGet(p => p.PluginPrefix).Returns("Softwareschmiede.GitHub");
        plugin.SetupGet(p => p.PluginType).Returns(PluginType.SourceCodeManagement);
        plugin.Setup(p => p.GetSettingGroups()).Returns(
        [
            new PluginSettingGroup("Pull Requests",
            [
                new PluginSettingField("AutoCompletePullRequests", "Auto", PluginSettingFieldType.Boolean, DefaultValue: "false"),
                new PluginSettingField("PullRequestCompletionStrategy", "Strategie", PluginSettingFieldType.Enum, DefaultValue: "Merge"),
                new PluginSettingField("PullRequestMergeMethod", "Methode", PluginSettingFieldType.Enum, DefaultValue: "Squash"),
                new PluginSettingField("AllowProtectedBranchBypass", "Bypass", PluginSettingFieldType.Boolean, DefaultValue: "false")
            ])
        ]);
        return plugin;
    }

    private static PullRequestStatusInfo Status(
        PullRequestStatus status,
        PullRequestMergeStatus mergeStatus,
        string? mergeCommitSha = null)
        => new(
            PullRequestProvider.GitHub,
            "owner/repo",
            42,
            "PR_kw",
            "https://github.com/owner/repo/pull/42",
            "PR",
            "feature/pr",
            "main",
            "head",
            mergeCommitSha,
            status,
            mergeStatus,
            DateTimeOffset.UtcNow);

    private static PullRequestReferenz AddPullRequest(
        SoftwareschmiededDbContext db,
        Guid aufgabeId,
        PullRequestMonitoringPhase phase,
        DateTimeOffset nextCheckUtc)
    {
        var pullRequest = new PullRequestReferenz
        {
            Id = Guid.NewGuid(),
            AufgabeId = aufgabeId,
            Provider = PullRequestProvider.GitHub,
            RepositoryId = "owner/repo",
            PullRequestNumber = 42,
            Url = "https://github.com/owner/repo/pull/42",
            Titel = "PR",
            SourceBranch = "feature/pr",
            TargetBranch = "main",
            HeadSha = "head",
            Status = PullRequestStatus.Open,
            MergeStatus = PullRequestMergeStatus.Mergeable,
            MonitoringPhase = phase,
            CreatedUtc = nextCheckUtc,
            NextCheckUtc = nextCheckUtc
        };

        db.PullRequestReferenzen.Add(pullRequest);
        db.SaveChanges();
        return pullRequest;
    }

    private static (Projekt Projekt, Aufgabe Aufgabe) AddAufgabe(SoftwareschmiededDbContext db)
    {
        var projekt = new Projekt
        {
            Id = Guid.NewGuid(),
            Name = "Projekt",
            Beschreibung = "Test",
            Status = ProjektStatus.Aktiv,
            ErstellungsDatum = DateTimeOffset.UtcNow
        };
        var aufgabe = new Aufgabe
        {
            Id = Guid.NewGuid(),
            ProjektId = projekt.Id,
            Titel = "Aufgabe",
            Status = AufgabeStatus.Neu,
            ErstellungsDatum = DateTimeOffset.UtcNow
        };

        db.Projekte.Add(projekt);
        db.Aufgaben.Add(aufgabe);
        db.SaveChanges();
        return (projekt, aufgabe);
    }
}
