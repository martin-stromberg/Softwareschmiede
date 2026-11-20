using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Tests für den TodoService.</summary>
public sealed class TodoServiceTests : IDisposable
{
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiedeDbContext _db;
    private readonly TodoService _sut;
    private readonly Guid _aufgabeId;

    /// <summary>TodoServiceTests.</summary>
    public TodoServiceTests()
    {
        _db = TestDbContextFactory.Create();
        _sut = new TodoService(_db, NullLogger<TodoService>.Instance);

        var projectId = Guid.NewGuid();
        _db.Projects.Add(new Project
        {
            Id = projectId,
            Name = "Testprojekt",
            CreatedAt = DateTimeOffset.UtcNow,
            Status = ProjectStatus.Active
        });

        _aufgabeId = Guid.NewGuid();
        _db.WorkItems.Add(new WorkItem
        {
            Id = _aufgabeId,
            ProjectId = projectId,
            Title = "Testaufgabe",
            Status = WorkItemStatus.New,
            CreatedAt = DateTimeOffset.UtcNow
        });
        _db.SaveChanges();
    }

    /// <summary>Dispose.</summary>
    public void Dispose() => _db.Dispose();

    /// <summary>CreateTodoAsync erstellt ein neues Todo mit korrekten Werten und speichert es in der Datenbank.</summary>
    [Fact]
    public async Task CreateTodoAsync_CreatesAndSavesTodo()
    {
        // Act
        var result = await _sut.CreateTodoAsync(_aufgabeId, "Erstes Todo", TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().NotBeEmpty();
        result.WorkItemId.Should().Be(_aufgabeId);
        result.Description.Should().Be("Erstes Todo");
        result.CompletedAt.Should().BeNull();

        var gespeichert = _db.Todos.Find(result.Id);
        gespeichert.Should().NotBeNull();
        gespeichert!.Description.Should().Be("Erstes Todo");
    }

    /// <summary>CreateTodoAsync lehnt eine leere Beschreibung ab.</summary>
    [Fact]
    public async Task CreateTodoAsync_ShouldThrow_WhenBeschreibungIsEmpty()
    {
        // Act
        var act = () => _sut.CreateTodoAsync(_aufgabeId, string.Empty);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    /// <summary>MarkTodoAsCompletedAsync setzt ErledigtAm auf einen Zeitstempel.</summary>
    [Fact]
    public async Task MarkTodoAsCompletedAsync_SetsErledigtAm()
    {
        // Arrange
        var todo = await _sut.CreateTodoAsync(_aufgabeId, "Zu erledigendes Todo", TestContext.Current.CancellationToken);
        var vorMarkierung = DateTimeOffset.UtcNow;

        // Act
        await _sut.MarkTodoAsCompletedAsync(todo.Id, TestContext.Current.CancellationToken);

        // Assert
        var aktualisiert = _db.Todos.Find(todo.Id);
        aktualisiert!.CompletedAt.Should().NotBeNull();
        aktualisiert.CompletedAt!.Value.Should().BeOnOrAfter(vorMarkierung.AddSeconds(-1));
    }

    /// <summary>DeleteTodoAsync entfernt das Todo aus der Datenbank.</summary>
    [Fact]
    public async Task DeleteTodoAsync_RemovesFromDatabase()
    {
        // Arrange
        var todo = await _sut.CreateTodoAsync(_aufgabeId, "Zu löschendes Todo", TestContext.Current.CancellationToken);

        // Act
        await _sut.DeleteTodoAsync(todo.Id, TestContext.Current.CancellationToken);

        // Assert
        var geloescht = _db.Todos.Find(todo.Id);
        geloescht.Should().BeNull();
    }

    /// <summary>GetOpenTodosAsync gibt nur offene Todos zurück.</summary>
    [Fact]
    public async Task GetOpenTodosAsync_ReturnsOnlyOpen()
    {
        // Arrange
        var offen = await _sut.CreateTodoAsync(_aufgabeId, "Offenes Todo", TestContext.Current.CancellationToken);
        var erledigt = await _sut.CreateTodoAsync(_aufgabeId, "Erledigtes Todo", TestContext.Current.CancellationToken);
        await _sut.MarkTodoAsCompletedAsync(erledigt.Id, TestContext.Current.CancellationToken);

        // Act
        var result = await _sut.GetOpenTodosAsync(_aufgabeId, TestContext.Current.CancellationToken);

        // Assert
        result.Should().ContainSingle();
        result[0].Id.Should().Be(offen.Id);
    }

    /// <summary>GetAllTodosAsync gibt alle Todos (offen und erledigt) zurück.</summary>
    [Fact]
    public async Task GetAllTodosAsync_ReturnsAll()
    {
        // Arrange
        var offen = await _sut.CreateTodoAsync(_aufgabeId, "Offenes Todo", TestContext.Current.CancellationToken);
        var erledigt = await _sut.CreateTodoAsync(_aufgabeId, "Erledigtes Todo", TestContext.Current.CancellationToken);
        await _sut.MarkTodoAsCompletedAsync(erledigt.Id, TestContext.Current.CancellationToken);

        // Act
        var result = await _sut.GetAllTodosAsync(_aufgabeId, TestContext.Current.CancellationToken);

        // Assert
        result.Should().HaveCount(2);
        result.Select(t => t.Id).Should().Contain([offen.Id, erledigt.Id]);
    }

    /// <summary>GetTodoCountAsync gibt die korrekte Anzahl offener Todos zurück.</summary>
    [Fact]
    public async Task GetTodoCountAsync_ReturnsCorrectCount()
    {
        // Arrange
        await _sut.CreateTodoAsync(_aufgabeId, "Todo 1", TestContext.Current.CancellationToken);
        await _sut.CreateTodoAsync(_aufgabeId, "Todo 2", TestContext.Current.CancellationToken);
        var erledigt = await _sut.CreateTodoAsync(_aufgabeId, "Todo 3", TestContext.Current.CancellationToken);
        await _sut.MarkTodoAsCompletedAsync(erledigt.Id, TestContext.Current.CancellationToken);

        // Act
        var result = await _sut.GetTodoCountAsync(_aufgabeId, TestContext.Current.CancellationToken);

        // Assert
        result.Should().Be(2);
    }

    /// <summary>GetOpenTodoCountsAsync zählt offene Todos für mehrere Aufgaben und ignoriert erledigte Todos.</summary>
    [Fact]
    public async Task GetOpenTodoCountsAsync_ShouldReturnCountsForMultipleTasks()
    {
        // Arrange
        var zweiteAufgabeId = Guid.NewGuid();
        _db.WorkItems.Add(new WorkItem
        {
            Id = zweiteAufgabeId,
            ProjectId = _db.Projects.Single().Id,
            Title = "Zweite Testaufgabe",
            Status = WorkItemStatus.New,
            CreatedAt = DateTimeOffset.UtcNow
        });
        _db.SaveChanges();

        await _sut.CreateTodoAsync(_aufgabeId, "Aufgabe 1 - offen 1", TestContext.Current.CancellationToken);
        await _sut.CreateTodoAsync(_aufgabeId, "Aufgabe 1 - offen 2", TestContext.Current.CancellationToken);
        var erledigt = await _sut.CreateTodoAsync(_aufgabeId, "Aufgabe 1 - erledigt", TestContext.Current.CancellationToken);
        await _sut.MarkTodoAsCompletedAsync(erledigt.Id, TestContext.Current.CancellationToken);
        await _sut.CreateTodoAsync(zweiteAufgabeId, "Aufgabe 2 - offen", TestContext.Current.CancellationToken);

        // Act
        var result = await _sut.GetOpenTodoCountsAsync([_aufgabeId, zweiteAufgabeId, _aufgabeId], TestContext.Current.CancellationToken);

        // Assert
        result.Should().HaveCount(2);
        result[_aufgabeId].Should().Be(2);
        result[zweiteAufgabeId].Should().Be(1);
    }

    /// <summary>GetOpenTodoCountsAsync gibt keine falschen Einträge für unbekannte oder leere IDs zurück.</summary>
    [Fact]
    public async Task GetOpenTodoCountsAsync_ShouldReturnOnlyMatchingCounts()
    {
        // Arrange
        await _sut.CreateTodoAsync(_aufgabeId, "Offen", TestContext.Current.CancellationToken);
        var unbekannteAufgabeId = Guid.NewGuid();

        // Act
        var result = await _sut.GetOpenTodoCountsAsync([_aufgabeId, unbekannteAufgabeId], TestContext.Current.CancellationToken);
        var emptyResult = await _sut.GetOpenTodoCountsAsync([], TestContext.Current.CancellationToken);

        // Assert
        result.Should().ContainSingle();
        result[_aufgabeId].Should().Be(1);
        result.Should().NotContainKey(unbekannteAufgabeId);
        emptyResult.Should().BeEmpty();
    }
}
