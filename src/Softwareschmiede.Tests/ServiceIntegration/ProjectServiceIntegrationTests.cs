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
    private readonly ProjektService _projektService;
    private readonly AufgabeService _aufgabeService;

    /// <summary>ProjectServiceIntegrationTests.</summary>
    public ProjectServiceIntegrationTests()
    {
        _db = TestDbContextFactory.Create();
        _projektService = new ProjektService(_db, NullLogger<ProjektService>.Instance);
        _aufgabeService = new AufgabeService(_db, NullLogger<AufgabeService>.Instance, new TodoService(_db, NullLogger<TodoService>.Instance));
    }

    /// <summary>Dispose.</summary>
    public void Dispose() => _db.Dispose();

    /// <summary><summary>ProjektErstellen_UndAufgabeHinzufuegen_StatusIstNeu.</summary>.</summary>
    [Fact]
    public async Task ProjektErstellen_UndAufgabeHinzufuegen_StatusIstNeu()
    {
        var project = await _projektService.CreateAsync("Neues Projekt", null, TestContext.Current.CancellationToken);
        project.Id.Should().NotBeEmpty();
        project.Name.Should().Be("Neues Projekt");

        var workItem = await _aufgabeService.CreateAsync(project.Id, "Erste Aufgabe", "Beschreibung", ct: TestContext.Current.CancellationToken);
        workItem.Id.Should().NotBeEmpty();
        workItem.ProjectId.Should().Be(project.Id);
        workItem.Status.Should().Be(WorkItemStatus.New);
    }

    /// <summary><summary>MehrereProjekte_KoennenErstellt_UndGeladen_Werden.</summary>.</summary>
    [Fact]
    public async Task MehrereProjekte_KoennenErstellt_UndGeladen_Werden()
    {
        await _projektService.CreateAsync("Projekt A", null, TestContext.Current.CancellationToken);
        await _projektService.CreateAsync("Projekt B", null, TestContext.Current.CancellationToken);

        var alle = await _projektService.GetAllAsync(TestContext.Current.CancellationToken);
        alle.Count.Should().BeGreaterThanOrEqualTo(2);
    }

    /// <summary><summary>Aufgabe_KannInProjektGeladen_Werden.</summary>.</summary>
    [Fact]
    public async Task Aufgabe_KannInProjektGeladen_Werden()
    {
        var project = await _projektService.CreateAsync("Test Projekt", null, TestContext.Current.CancellationToken);
        await _aufgabeService.CreateAsync(project.Id, "Aufgabe 1", null, ct: TestContext.Current.CancellationToken);
        await _aufgabeService.CreateAsync(project.Id, "Aufgabe 2", null, ct: TestContext.Current.CancellationToken);

        var workItems = await _aufgabeService.GetByProjektAsync(project.Id, TestContext.Current.CancellationToken);
        workItems.Should().HaveCount(2);
        workItems.All(a => a.ProjectId == project.Id).Should().BeTrue();
    }
}
