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
    private readonly ProjectService _projektService;
    private readonly WorkItemService _aufgabeService;

    /// <summary>TaskStartupServiceIntegrationTests.</summary>
    public TaskStartupServiceIntegrationTests()
    {
        _db = TestDbContextFactory.Create();
        _projektService = new ProjectService(_db, NullLogger<ProjectService>.Instance);
        _aufgabeService = new WorkItemService(_db, NullLogger<WorkItemService>.Instance, new TodoService(_db, NullLogger<TodoService>.Instance));
    }

    /// <summary>Dispose.</summary>
    public void Dispose() => _db.Dispose();

    /// <summary><summary>StartenAsync_Setzt_Status_AufGestartet.</summary>.</summary>
    [Fact]
    public async Task StartenAsync_Setzt_Status_AufGestartet()
    {
        var project = await _projektService.CreateAsync("Startup-Projekt", null, TestContext.Current.CancellationToken);
        var workItem = await _aufgabeService.CreateAsync(project.Id, "Startup-Aufgabe", null, ct: TestContext.Current.CancellationToken);

        workItem.Status.Should().Be(WorkItemStatus.New);

        await _aufgabeService.StartenAsync(workItem.Id, "feature/test-branch", "/tmp/repo", ct: TestContext.Current.CancellationToken);

        var geladen = await _aufgabeService.GetByIdAsync(workItem.Id, TestContext.Current.CancellationToken);
        geladen!.Status.Should().Be(WorkItemStatus.Started);
        geladen.BranchName.Should().Be("feature/test-branch");
        geladen.LocalClonePath.Should().Be("/tmp/repo");
    }

    /// <summary><summary>StatusUebergang_Gestartet_NachWartend_IstErlaubt.</summary>.</summary>
    [Fact]
    public async Task StatusUebergang_Gestartet_NachWartend_IstErlaubt()
    {
        var project = await _projektService.CreateAsync("Uebergang-Projekt", null, TestContext.Current.CancellationToken);
        var workItem = await _aufgabeService.CreateAsync(project.Id, "Uebergang-Aufgabe", null, ct: TestContext.Current.CancellationToken);

        await _aufgabeService.StartenAsync(workItem.Id, "main", "/tmp/repo", ct: TestContext.Current.CancellationToken);
        await _aufgabeService.SetStatusAsync(workItem.Id, WorkItemStatus.Waiting, TestContext.Current.CancellationToken);

        var geladen = await _aufgabeService.GetByIdAsync(workItem.Id, TestContext.Current.CancellationToken);
        geladen!.Status.Should().Be(WorkItemStatus.Waiting);
    }

    /// <summary><summary>StatusUebergang_Wartend_NachGestartet_IstErlaubt.</summary>.</summary>
    [Fact]
    public async Task StatusUebergang_Wartend_NachGestartet_IstErlaubt()
    {
        var project = await _projektService.CreateAsync("Resume-Projekt", null, TestContext.Current.CancellationToken);
        var workItem = await _aufgabeService.CreateAsync(project.Id, "Resume-Aufgabe", null, ct: TestContext.Current.CancellationToken);

        await _aufgabeService.StartenAsync(workItem.Id, "main", "/tmp/repo", ct: TestContext.Current.CancellationToken);
        await _aufgabeService.SetStatusAsync(workItem.Id, WorkItemStatus.Waiting, TestContext.Current.CancellationToken);
        await _aufgabeService.SetStatusAsync(workItem.Id, WorkItemStatus.Started, TestContext.Current.CancellationToken);

        var geladen = await _aufgabeService.GetByIdAsync(workItem.Id, TestContext.Current.CancellationToken);
        geladen!.Status.Should().Be(WorkItemStatus.Started);
    }
}
