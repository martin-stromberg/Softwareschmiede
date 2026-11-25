using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Application.Services.Updates;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Infrastructure.Data;
using Softwareschmiede.StatusContracts;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>
/// Tests des <see cref="StatusSnapshotProjector"/> (Issue 478): Feldabbildung der
/// Aufgaben-/Ereignis-Projektion, Filter auf aktive/wartende Aufgaben, Live-RunStatus
/// sowie Instanz-/Versionsfelder.
/// </summary>
public sealed class StatusSnapshotProjectorTests : IDisposable
{
    private readonly SoftwareschmiedeDbContext _db;
    private readonly AppSettingService _appSettings;
    private readonly ServiceProvider _provider;
    private readonly Mock<IRunningAutomationStatusSource> _statusSource = new();
    private readonly Mock<IRunningAutomationsCount> _automationsCount = new();
    private readonly Mock<IApplicationVersionProvider> _versionProvider = new();
    private readonly TestSupport.TestTimeProvider _timeProvider = new();

    /// <summary>StatusSnapshotProjectorTests.</summary>
    public StatusSnapshotProjectorTests()
    {
        _db = TestDbContextFactory.Create();
        _appSettings = new AppSettingService(_db, NullLogger<AppSettingService>.Instance);
        _provider = new ServiceCollection()
            .AddSingleton(_db)
            .AddSingleton(_appSettings)
            .BuildServiceProvider();
        _statusSource.Setup(s => s.GetLiveRunStatus(It.IsAny<Guid>())).Returns((WorkItemRunStatus?)null);
        _automationsCount.Setup(c => c.CountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(3);
        _versionProvider
            .Setup(v => v.GetInstalledVersionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstalledVersionInfo("1.2.3", "v1.2.3", null, null));
    }

    /// <summary>Dispose.</summary>
    public void Dispose()
    {
        _provider.Dispose();
        _db.Dispose();
    }

    /// <summary>Der Snapshot enthält Instanz-ID/-Name, Version, Laufzähler und die Protokollversion.</summary>
    [Fact]
    public async Task CreateAsync_ProjectsInstanceVersionAndCount()
    {
        await _appSettings.SetRemoteStatusInstanceNameAsync("Martin-PC", TestContext.Current.CancellationToken);
        var sut = CreateSut();

        var snapshot = await sut.CreateAsync(TestContext.Current.CancellationToken);

        snapshot.InstanceId.Should().NotBeNullOrWhiteSpace();
        snapshot.InstanceId.Should().Be(
            await _appSettings.GetOrCreateRemoteStatusInstanceIdAsync(TestContext.Current.CancellationToken));
        snapshot.InstanceName.Should().Be("Martin-PC");
        snapshot.ProtocolVersion.Should().Be(RelayProtocolVersion.Current);
        snapshot.AppVersion.Should().Be("v1.2.3");
        snapshot.RunningAutomationsCount.Should().Be(3);
        snapshot.CreatedUtc.Should().Be(_timeProvider.GetUtcNow());
    }

    /// <summary>Nur Aufgaben im Status Started/Waiting werden projiziert; inaktive bleiben außen vor.</summary>
    [Fact]
    public async Task CreateAsync_ProjectsOnlyActiveOrWaitingWorkItems()
    {
        _db.WorkItems.AddRange(
            CreateWorkItem("aktiv", WorkItemStatus.Started),
            CreateWorkItem("wartend", WorkItemStatus.Waiting),
            CreateWorkItem("neu", WorkItemStatus.New),
            CreateWorkItem("fertig", WorkItemStatus.Completed),
            CreateWorkItem("archiviert", WorkItemStatus.Archived));
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var sut = CreateSut();

        var snapshot = await sut.CreateAsync(TestContext.Current.CancellationToken);

        snapshot.WorkItems.Should().HaveCount(2);
        snapshot.WorkItems.Select(w => w.Title).Should().BeEquivalentTo("aktiv", "wartend");
        snapshot.WorkItems.Select(w => w.Status).Should().BeEquivalentTo("Started", "Waiting");
    }

    /// <summary>Ein projizierter Arbeitsauftrag enthält Anzeigefelder: Kind, Zweck, Repo, Issue, Pull-Requests.</summary>
    [Fact]
    public async Task CreateAsync_MapsWorkItemDisplayFields()
    {
        var repository = new GitRepository { Id = Guid.NewGuid(), RepositoryName = "mein-repo" };
        var workItem = CreateWorkItem("titel", WorkItemStatus.Started);
        workItem.Kind = WorkItemKind.Instruction;
        workItem.Purpose = "Zweck";
        workItem.GitRepositoryId = repository.Id;
        workItem.GitRepository = repository;
        workItem.IssueReference = new IssueReference
        {
            Id = Guid.NewGuid(),
            IssueNumber = 478,
            IssueUrl = "https://github.com/org/repo/issues/478"
        };
        workItem.PullRequests.Add(new PullRequestReference
        {
            Id = Guid.NewGuid(),
            WorkItemId = workItem.Id,
            PullRequestNumber = 12,
            Title = "PR-Titel",
            Url = "https://github.com/org/repo/pull/12",
            Status = PullRequestStatus.Open,
            RepositoryId = "org/repo",
        });
        _db.WorkItems.Add(workItem);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var sut = CreateSut();

        var snapshot = await sut.CreateAsync(TestContext.Current.CancellationToken);

        var item = snapshot.WorkItems.Should().ContainSingle().Subject;
        item.Title.Should().Be("titel");
        item.Kind.Should().Be("Instruction");
        item.Purpose.Should().Be("Zweck");
        item.RepositoryName.Should().Be("mein-repo");
        item.IssueNumber.Should().Be(478);
        item.IssueUrl.Should().Be("https://github.com/org/repo/issues/478");
        var pr = item.PullRequests.Should().ContainSingle().Subject;
        pr.Number.Should().Be(12);
        pr.Title.Should().Be("PR-Titel");
        pr.Url.Should().Be("https://github.com/org/repo/pull/12");
        pr.Status.Should().Be("Open");
        pr.RepositoryId.Should().Be("org/repo");
    }

    /// <summary>Der Live-Laufstatus der Statusquelle ersetzt den persistierten RunStatus.</summary>
    [Fact]
    public async Task CreateAsync_UsesLiveRunStatusFromStatusSource()
    {
        var workItem = CreateWorkItem("live", WorkItemStatus.Started);
        workItem.RunStatus = WorkItemRunStatus.Running;
        _db.WorkItems.Add(workItem);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        _statusSource.Setup(s => s.GetLiveRunStatus(workItem.Id)).Returns(WorkItemRunStatus.WaitingForInput);
        var sut = CreateSut();

        var snapshot = await sut.CreateAsync(TestContext.Current.CancellationToken);

        snapshot.WorkItems.Should().ContainSingle()
            .Which.RunStatus.Should().Be("WaitingForInput");
    }

    /// <summary>SCM-Ereignisse werden absteigend nach Empfangszeit projiziert (Status, Grund, Zeitpunkt);
    /// unlesbares ObservationJson bleibt ohne Referenzanzeige.</summary>
    [Fact]
    public async Task CreateAsync_ProjectsScmEventsNewestFirst_WithFallbackOnBadJson()
    {
        var workItem = CreateWorkItem("referenz", WorkItemStatus.Started);
        _db.WorkItems.Add(workItem);
        var older = DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeMilliseconds();
        var newer = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        _db.ScmIncomingEvents.AddRange(
            new ScmIncomingEvent
            {
                EventKey = "alt",
                WorkItemId = workItem.Id,
                Status = ScmIncomingEventStatus.Verarbeitet,
                Reason = "älter",
                ReceivedUtc = older,
                ObservationJson = "kein-json",
            },
            new ScmIncomingEvent
            {
                EventKey = "neu",
                WorkItemId = workItem.Id,
                Status = ScmIncomingEventStatus.Wartend,
                Reason = "neuer",
                ReceivedUtc = newer,
                ObservationJson = "kein-json",
            });
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var sut = CreateSut();

        var snapshot = await sut.CreateAsync(TestContext.Current.CancellationToken);

        snapshot.ScmEvents.Should().HaveCount(2);
        snapshot.ScmEvents[0].Status.Should().Be(ScmIncomingEventStatus.Wartend);
        snapshot.ScmEvents[0].Reason.Should().Be("neuer");
        snapshot.ScmEvents[0].ReceivedUtc.Should().Be(DateTimeOffset.FromUnixTimeMilliseconds(newer));
        snapshot.ScmEvents[0].ReferenceDisplay.Should().BeNull();
        snapshot.ScmEvents[1].Status.Should().Be(ScmIncomingEventStatus.Verarbeitet);
    }

    /// <summary>ObservationJson mit dem JSON-Literal „null“ liefert ein Ereignis ohne
    /// Referenzanzeige, statt die Snapshot-Erzeugung mit einer NRE abzubrechen.</summary>
    [Fact]
    public async Task CreateAsync_NullLiteralObservationJson_YieldsEventWithoutReference()
    {
        _db.ScmIncomingEvents.Add(new ScmIncomingEvent
        {
            EventKey = "null-json",
            Status = ScmIncomingEventStatus.Wartend,
            ReceivedUtc = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            ObservationJson = "null",
        });
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var sut = CreateSut();

        var snapshot = await sut.CreateAsync(TestContext.Current.CancellationToken);

        var evt = snapshot.ScmEvents.Should().ContainSingle().Subject;
        evt.Status.Should().Be(ScmIncomingEventStatus.Wartend);
        evt.ReferenceDisplay.Should().BeNull();
        evt.Url.Should().BeNull();
    }

    private StatusSnapshotProjector CreateSut() =>
        new(
            _provider.GetRequiredService<IServiceScopeFactory>(),
            _statusSource.Object,
            _automationsCount.Object,
            _versionProvider.Object,
            _timeProvider,
            NullLogger<StatusSnapshotProjector>.Instance);

    private static WorkItem CreateWorkItem(string title, WorkItemStatus status) =>
        new()
        {
            Id = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            Title = title,
            Status = status,
            CreatedAt = DateTimeOffset.UtcNow,
        };
}
