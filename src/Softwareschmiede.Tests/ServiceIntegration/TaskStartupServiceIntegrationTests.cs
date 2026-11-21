using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.ServiceIntegration;

/// <summary>E2E-Test: Aufgabe starten → Arbeitsverzeichnis einrichten → Branch erstellen.</summary>
public sealed class TaskStartupServiceIntegrationTests : IDisposable
{
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiedeDbContext _db;
    private readonly ProjectService _projectService;
    private readonly WorkItemService _workItemService;

    /// <summary>TaskStartupServiceIntegrationTests.</summary>
    public TaskStartupServiceIntegrationTests()
    {
        _db = TestDbContextFactory.Create();
        _projectService = new ProjectService(_db, NullLogger<ProjectService>.Instance);
        _workItemService = new WorkItemService(_db, NullLogger<WorkItemService>.Instance, new TodoService(_db, NullLogger<TodoService>.Instance));
    }

    /// <summary>Dispose.</summary>
    public void Dispose() => _db.Dispose();

    /// <summary>StartAsync_Setzt_Status_AufStarted.</summary>
    [Fact]
    public async Task StartAsync_Sets_Status_OnStarted()
    {
        var project = await _projectService.CreateAsync("Startup-Projekt", null, TestContext.Current.CancellationToken);
        var workItem = await _workItemService.CreateAsync(project.Id, "Startup-Aufgabe", null, ct: TestContext.Current.CancellationToken);

        workItem.Status.Should().Be(WorkItemStatus.New);

        await _workItemService.StartAsync(workItem.Id, "feature/test-branch", "/tmp/repo", ct: TestContext.Current.CancellationToken);

        var loaded = await _workItemService.GetByIdAsync(workItem.Id, TestContext.Current.CancellationToken);
        loaded!.Status.Should().Be(WorkItemStatus.Started);
        loaded.BranchName.Should().Be("feature/test-branch");
        loaded.LocalClonePath.Should().Be("/tmp/repo");
    }

    /// <summary>StatusUebergang_Gestartet_NachWartend_IstErlaubt.</summary>
    [Fact]
    public async Task StatusTransition_Started_AfterWaiting_IsAllows()
    {
        var project = await _projectService.CreateAsync("Uebergang-Projekt", null, TestContext.Current.CancellationToken);
        var workItem = await _workItemService.CreateAsync(project.Id, "Uebergang-Aufgabe", null, ct: TestContext.Current.CancellationToken);

        await _workItemService.StartAsync(workItem.Id, "main", "/tmp/repo", ct: TestContext.Current.CancellationToken);
        await _workItemService.SetStatusAsync(workItem.Id, WorkItemStatus.Waiting, TestContext.Current.CancellationToken);

        var loaded = await _workItemService.GetByIdAsync(workItem.Id, TestContext.Current.CancellationToken);
        loaded!.Status.Should().Be(WorkItemStatus.Waiting);
    }

    /// <summary>StatusUebergang_Wartend_NachGestartet_IstErlaubt.</summary>
    [Fact]
    public async Task StatusTransition_Waiting_AfterStarted_IsAllows()
    {
        var project = await _projectService.CreateAsync("Resume-Projekt", null, TestContext.Current.CancellationToken);
        var workItem = await _workItemService.CreateAsync(project.Id, "Resume-Aufgabe", null, ct: TestContext.Current.CancellationToken);

        await _workItemService.StartAsync(workItem.Id, "main", "/tmp/repo", ct: TestContext.Current.CancellationToken);
        await _workItemService.SetStatusAsync(workItem.Id, WorkItemStatus.Waiting, TestContext.Current.CancellationToken);
        await _workItemService.SetStatusAsync(workItem.Id, WorkItemStatus.Started, TestContext.Current.CancellationToken);

        var loaded = await _workItemService.GetByIdAsync(workItem.Id, TestContext.Current.CancellationToken);
        loaded!.Status.Should().Be(WorkItemStatus.Started);
    }
}
