using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Domain.Enums;

/// <summary>Tests für Statusübergänge von Aufgaben.</summary>
public sealed class AufgabeStatusTransitionTests : IDisposable
{
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiedeDbContext _db;
    private readonly WorkItemService _sut;
    private readonly Guid _projektId = Guid.NewGuid();

    /// <summary>AufgabeStatusTransitionTests.</summary>
    public AufgabeStatusTransitionTests()
    {
        _db = TestDbContextFactory.Create();
        _sut = new WorkItemService(_db, new Mock<ILogger<WorkItemService>>().Object, new TodoService(_db, new Mock<ILogger<TodoService>>().Object));

        _db.Projects.Add(new Project
        {
            Id = _projektId,
            Name = "Transition-Testprojekt",
            CreatedAt = DateTimeOffset.UtcNow,
            Status = ProjectStatus.Active
        });
        _db.SaveChanges();
    }

    /// <summary>Dispose.</summary>
    public void Dispose() => _db.Dispose();

    /// <summary>Direkter Übergang Neu → Gestartet ist erlaubt.</summary>
    [Fact]
    public async Task TestStatusTransitions_NeuToGestartet_Direct_Succeeds()
    {
        var workItem = await _sut.CreateAsync(_projektId, "Transition-Test", null, ct: TestContext.Current.CancellationToken);

        await _sut.SetStatusAsync(workItem.Id, WorkItemStatus.Started, TestContext.Current.CancellationToken);

        var result = await _sut.GetByIdAsync(workItem.Id, TestContext.Current.CancellationToken);
        result!.Status.Should().Be(WorkItemStatus.Started);
    }

    /// <summary>ArbeitsverzeichnisEingerichtet existiert nicht mehr im Enum.</summary>
    [Fact]
    public void TestStatusTransitions_OldArbeitsverzeichnisStatus_Removed()
    {
        Enum.GetNames<WorkItemStatus>().Should().NotContain("ArbeitsverzeichnisEingerichtet");
    }

    /// <summary>InArbeit existiert nicht mehr im Enum.</summary>
    [Fact]
    public void TestStatusTransitions_OldInArbeitStatus_Removed()
    {
        Enum.GetNames<WorkItemStatus>().Should().NotContain("InArbeit");
    }

    /// <summary>Übergang Gestartet → Wartend ist erlaubt.</summary>
    [Fact]
    public async Task TestStatusValidation_GestartetToWartend_IsAllowed()
    {
        var workItem = await ErstelleAufgabeInStatus(WorkItemStatus.Started);

        var act = () => _sut.SetStatusAsync(workItem.Id, WorkItemStatus.Waiting);
        await act.Should().NotThrowAsync();
    }

    /// <summary>Übergang Gestartet → Beendet ist erlaubt.</summary>
    [Fact]
    public async Task TestStatusValidation_GestartetToBeendet_IsAllowed()
    {
        var workItem = await ErstelleAufgabeInStatus(WorkItemStatus.Started);

        var act = () => _sut.SetStatusAsync(workItem.Id, WorkItemStatus.Completed);
        await act.Should().NotThrowAsync();
    }

    /// <summary>Erlaubter Übergang: Gestartet → Wartend → Gestartet → Beendet.</summary>
    [Fact]
    public async Task TestStatusTransitions_WartendSequence_Succeeds()
    {
        var workItem = await ErstelleAufgabeInStatus(WorkItemStatus.Started);

        await _sut.SetStatusAsync(workItem.Id, WorkItemStatus.Waiting, TestContext.Current.CancellationToken);
        await _sut.SetStatusAsync(workItem.Id, WorkItemStatus.Started, TestContext.Current.CancellationToken);
        await _sut.SetStatusAsync(workItem.Id, WorkItemStatus.Completed, TestContext.Current.CancellationToken);

        var result = await _sut.GetByIdAsync(workItem.Id, TestContext.Current.CancellationToken);
        result!.Status.Should().Be(WorkItemStatus.Completed);
    }

    /// <summary>Übergang von jedem Status nach Archiviert ist erlaubt.</summary>
    [Theory]
    [InlineData(WorkItemStatus.New)]
    [InlineData(WorkItemStatus.Started)]
    [InlineData(WorkItemStatus.Waiting)]
    [InlineData(WorkItemStatus.Completed)]
    public async Task TestStatusTransitions_AnyStatusToArchiviert_IsAllowed(WorkItemStatus fromStatus)
    {
        var workItem = await ErstelleAufgabeInStatus(fromStatus);

        var act = () => _sut.SetStatusAsync(workItem.Id, WorkItemStatus.Archived);
        await act.Should().NotThrowAsync();
    }

    /// <summary>Ungültiger Übergang Neu → Beendet wirft InvalidStatusTransitionException.</summary>
    [Fact]
    public async Task TestStatusValidation_InvalidTransition_ThrowsException()
    {
        var workItem = await _sut.CreateAsync(_projektId, "Ungültiger Übergang", null, ct: TestContext.Current.CancellationToken);

        var act = () => _sut.SetStatusAsync(workItem.Id, WorkItemStatus.Completed);
        await act.Should().ThrowAsync<InvalidStatusTransitionException>();
    }

    /// <summary>Ungültiger Übergang Beendet → Gestartet wirft InvalidStatusTransitionException.</summary>
    [Fact]
    public async Task TestStatusValidation_BeendetToGestartet_ThrowsException()
    {
        var workItem = await ErstelleAufgabeInStatus(WorkItemStatus.Completed);

        var act = () => _sut.SetStatusAsync(workItem.Id, WorkItemStatus.Started);
        await act.Should().ThrowAsync<InvalidStatusTransitionException>();
    }

    /// <summary>Ungültiger Übergang Archiviert → Neu wirft InvalidStatusTransitionException.</summary>
    [Fact]
    public async Task TestStatusValidation_ArchiviertToNeu_ThrowsException()
    {
        var workItem = await ErstelleAufgabeInStatus(WorkItemStatus.Archived);

        var act = () => _sut.SetStatusAsync(workItem.Id, WorkItemStatus.New);
        await act.Should().ThrowAsync<InvalidStatusTransitionException>();
    }

    private async Task<WorkItem> ErstelleAufgabeInStatus(WorkItemStatus status)
    {
        var workItem = new WorkItem
        {
            Id = Guid.NewGuid(),
            ProjectId = _projektId,
            Title = $"Aufgabe-{status}",
            Status = status,
            CreatedAt = DateTimeOffset.UtcNow
        };
        _db.WorkItems.Add(workItem);
        await _db.SaveChangesAsync();
        return workItem;
    }
}
