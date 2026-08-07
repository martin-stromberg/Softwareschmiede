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
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiededDbContext _db;
    private readonly TodoService _sut;
    private readonly Guid _aufgabeId;

    /// <summary>TodoServiceTests.</summary>
    public TodoServiceTests()
    {
        _db = TestDbContextFactory.Create();
        _sut = new TodoService(_db, NullLogger<TodoService>.Instance);

        var projektId = Guid.NewGuid();
        _db.Projekte.Add(new Projekt
        {
            Id = projektId,
            Name = "Testprojekt",
            ErstellungsDatum = DateTimeOffset.UtcNow,
            Status = ProjektStatus.Aktiv
        });

        _aufgabeId = Guid.NewGuid();
        _db.Aufgaben.Add(new Aufgabe
        {
            Id = _aufgabeId,
            ProjektId = projektId,
            Titel = "Testaufgabe",
            Status = AufgabeStatus.Neu,
            ErstellungsDatum = DateTimeOffset.UtcNow
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
        var result = await _sut.CreateTodoAsync(_aufgabeId, "Erstes Todo");

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().NotBeEmpty();
        result.AufgabeId.Should().Be(_aufgabeId);
        result.Beschreibung.Should().Be("Erstes Todo");
        result.ErledigtAm.Should().BeNull();

        var gespeichert = _db.Todos.Find(result.Id);
        gespeichert.Should().NotBeNull();
        gespeichert!.Beschreibung.Should().Be("Erstes Todo");
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
        var todo = await _sut.CreateTodoAsync(_aufgabeId, "Zu erledigendes Todo");
        var vorMarkierung = DateTimeOffset.UtcNow;

        // Act
        await _sut.MarkTodoAsCompletedAsync(todo.Id);

        // Assert
        var aktualisiert = _db.Todos.Find(todo.Id);
        aktualisiert!.ErledigtAm.Should().NotBeNull();
        aktualisiert.ErledigtAm!.Value.Should().BeOnOrAfter(vorMarkierung.AddSeconds(-1));
    }

    /// <summary>DeleteTodoAsync entfernt das Todo aus der Datenbank.</summary>
    [Fact]
    public async Task DeleteTodoAsync_RemovesFromDatabase()
    {
        // Arrange
        var todo = await _sut.CreateTodoAsync(_aufgabeId, "Zu löschendes Todo");

        // Act
        await _sut.DeleteTodoAsync(todo.Id);

        // Assert
        var geloescht = _db.Todos.Find(todo.Id);
        geloescht.Should().BeNull();
    }

    /// <summary>GetOpenTodosAsync gibt nur offene Todos zurück.</summary>
    [Fact]
    public async Task GetOpenTodosAsync_ReturnsOnlyOpen()
    {
        // Arrange
        var offen = await _sut.CreateTodoAsync(_aufgabeId, "Offenes Todo");
        var erledigt = await _sut.CreateTodoAsync(_aufgabeId, "Erledigtes Todo");
        await _sut.MarkTodoAsCompletedAsync(erledigt.Id);

        // Act
        var result = await _sut.GetOpenTodosAsync(_aufgabeId);

        // Assert
        result.Should().ContainSingle();
        result[0].Id.Should().Be(offen.Id);
    }

    /// <summary>GetAllTodosAsync gibt alle Todos (offen und erledigt) zurück.</summary>
    [Fact]
    public async Task GetAllTodosAsync_ReturnsAll()
    {
        // Arrange
        var offen = await _sut.CreateTodoAsync(_aufgabeId, "Offenes Todo");
        var erledigt = await _sut.CreateTodoAsync(_aufgabeId, "Erledigtes Todo");
        await _sut.MarkTodoAsCompletedAsync(erledigt.Id);

        // Act
        var result = await _sut.GetAllTodosAsync(_aufgabeId);

        // Assert
        result.Should().HaveCount(2);
        result.Select(t => t.Id).Should().Contain([offen.Id, erledigt.Id]);
    }

    /// <summary>GetTodoCountAsync gibt die korrekte Anzahl offener Todos zurück.</summary>
    [Fact]
    public async Task GetTodoCountAsync_ReturnsCorrectCount()
    {
        // Arrange
        await _sut.CreateTodoAsync(_aufgabeId, "Todo 1");
        await _sut.CreateTodoAsync(_aufgabeId, "Todo 2");
        var erledigt = await _sut.CreateTodoAsync(_aufgabeId, "Todo 3");
        await _sut.MarkTodoAsCompletedAsync(erledigt.Id);

        // Act
        var result = await _sut.GetTodoCountAsync(_aufgabeId);

        // Assert
        result.Should().Be(2);
    }

    /// <summary>GetOpenTodoCountsAsync zählt offene Todos für mehrere Aufgaben und ignoriert erledigte Todos.</summary>
    [Fact]
    public async Task GetOpenTodoCountsAsync_ShouldReturnCountsForMultipleTasks()
    {
        // Arrange
        var zweiteAufgabeId = Guid.NewGuid();
        _db.Aufgaben.Add(new Aufgabe
        {
            Id = zweiteAufgabeId,
            ProjektId = _db.Projekte.Single().Id,
            Titel = "Zweite Testaufgabe",
            Status = AufgabeStatus.Neu,
            ErstellungsDatum = DateTimeOffset.UtcNow
        });
        _db.SaveChanges();

        await _sut.CreateTodoAsync(_aufgabeId, "Aufgabe 1 - offen 1");
        await _sut.CreateTodoAsync(_aufgabeId, "Aufgabe 1 - offen 2");
        var erledigt = await _sut.CreateTodoAsync(_aufgabeId, "Aufgabe 1 - erledigt");
        await _sut.MarkTodoAsCompletedAsync(erledigt.Id);
        await _sut.CreateTodoAsync(zweiteAufgabeId, "Aufgabe 2 - offen");

        // Act
        var result = await _sut.GetOpenTodoCountsAsync([_aufgabeId, zweiteAufgabeId, _aufgabeId]);

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
        await _sut.CreateTodoAsync(_aufgabeId, "Offen");
        var unbekannteAufgabeId = Guid.NewGuid();

        // Act
        var result = await _sut.GetOpenTodoCountsAsync([_aufgabeId, unbekannteAufgabeId]);
        var emptyResult = await _sut.GetOpenTodoCountsAsync([]);

        // Assert
        result.Should().ContainSingle();
        result[_aufgabeId].Should().Be(1);
        result.Should().NotContainKey(unbekannteAufgabeId);
        emptyResult.Should().BeEmpty();
    }
}
