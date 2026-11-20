using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.ServiceIntegration;

/// <summary>E2E-Test: Projekt erstellen und Aufgabe hinzufügen.</summary>
public sealed class ProjectServiceIntegrationTests : IDisposable
{
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiedeDbContext _db;
    private readonly ProjectService _projectService;
    private readonly WorkItemService _workItemService;

    /// <summary>ProjectServiceIntegrationTests.</summary>
    public ProjectServiceIntegrationTests()
    {
        _db = TestDbContextFactory.Create();
        _projectService = new ProjectService(_db, NullLogger<ProjectService>.Instance);
        _workItemService = new WorkItemService(_db, NullLogger<WorkItemService>.Instance, new TodoService(_db, NullLogger<TodoService>.Instance));
    }

    /// <summary>Dispose.</summary>
    public void Dispose() => _db.Dispose();

    /// <summary>ProjektErstellen_UndAufgabeHinzufuegen_StatusIstNeu.</summary>
    [Fact]
    public async Task ProjektErstellen_UndAufgabeHinzufuegen_StatusIstNeu()
    {
        var project = await _projectService.CreateAsync("Neues Projekt", null, TestContext.Current.CancellationToken);
        project.Id.Should().NotBeEmpty();
        project.Name.Should().Be("Neues Projekt");

        var workItem = await _workItemService.CreateAsync(project.Id, "Erste Aufgabe", "Beschreibung", ct: TestContext.Current.CancellationToken);
        workItem.Id.Should().NotBeEmpty();
        workItem.ProjectId.Should().Be(project.Id);
        workItem.Status.Should().Be(WorkItemStatus.New);
    }

    /// <summary>MehrereProjekte_KoennenErstellt_UndGeladen_Werden.</summary>
    [Fact]
    public async Task MehrereProjekte_KoennenErstellt_UndGeladen_Werden()
    {
        await _projectService.CreateAsync("Projekt A", null, TestContext.Current.CancellationToken);
        await _projectService.CreateAsync("Projekt B", null, TestContext.Current.CancellationToken);

        var alle = await _projectService.GetAllAsync(TestContext.Current.CancellationToken);
        alle.Count.Should().BeGreaterThanOrEqualTo(2);
    }

    /// <summary>Aufgabe_KannInProjektGeladen_Werden.</summary>
    [Fact]
    public async Task Aufgabe_KannInProjektGeladen_Werden()
    {
        var project = await _projectService.CreateAsync("Test Projekt", null, TestContext.Current.CancellationToken);
        await _workItemService.CreateAsync(project.Id, "Aufgabe 1", null, ct: TestContext.Current.CancellationToken);
        await _workItemService.CreateAsync(project.Id, "Aufgabe 2", null, ct: TestContext.Current.CancellationToken);

        var workItems = await _workItemService.GetByProjectAsync(project.Id, TestContext.Current.CancellationToken);
        workItems.Should().HaveCount(2);
        workItems.All(a => a.ProjectId == project.Id).Should().BeTrue();
    }
}
