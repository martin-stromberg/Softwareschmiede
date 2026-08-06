using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.IntegrationTests.Infrastructure;

namespace Softwareschmiede.IntegrationTests.Services;

/// <summary>Integrationstests für <see cref="TodoService"/> mit echter SQLite-Datenbank.</summary>
public sealed class TodoServiceTests
{
    private static async Task<Guid> CreateTestProjektAsync(DatabaseFixture db)
    {
        var service = new ProjektService(db.Context, NullLogger<ProjektService>.Instance);
        var projekt = await service.CreateAsync("Todo-Integrationstestprojekt", null);
        return projekt.Id;
    }

    /// <summary>Todos werden in der Datenbank gespeichert und können über einen neuen Context gelesen werden.</summary>
    [Fact]
    public async Task CreateTodoAsync_ShouldPersistTodo_WhenValidDataGiven()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync();
        var projektId = await CreateTestProjektAsync(db);
        var aufgabeService = new AufgabeService(db.Context, NullLogger<AufgabeService>.Instance, new TodoService(db.Context, NullLogger<TodoService>.Instance));
        var todoService = new TodoService(db.Context, NullLogger<TodoService>.Instance);
        var aufgabe = await aufgabeService.CreateAsync(projektId, "Aufgabe mit Todo", null);

        // Act
        var created = await todoService.CreateTodoAsync(aufgabe.Id, "Persistiertes Todo");

        // Assert
        await using var db2 = db.CreateNewContext();
        var loaded = await db2.Todos.FindAsync(created.Id);

        loaded.Should().NotBeNull();
        loaded!.Beschreibung.Should().Be("Persistiertes Todo");
        loaded.AufgabeId.Should().Be(aufgabe.Id);
        loaded.ErledigtAm.Should().BeNull();
    }

    /// <summary>Cascade-Delete: Löschen der Aufgabe entfernt alle zugehörigen Todos.</summary>
    [Fact]
    public async Task Cascade_Delete_DeletesAllTodos()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync();
        var projektId = await CreateTestProjektAsync(db);
        var aufgabeService = new AufgabeService(db.Context, NullLogger<AufgabeService>.Instance, new TodoService(db.Context, NullLogger<TodoService>.Instance));
        var todoService = new TodoService(db.Context, NullLogger<TodoService>.Instance);
        var aufgabe = await aufgabeService.CreateAsync(projektId, "Aufgabe mit Todos", null);
        await todoService.CreateTodoAsync(aufgabe.Id, "Todo 1");
        await todoService.CreateTodoAsync(aufgabe.Id, "Todo 2");

        // Act
        await aufgabeService.DeleteAsync(aufgabe.Id);

        // Assert
        await using var db2 = db.CreateNewContext();
        var verbleibendeTodos = db2.Todos.Where(t => t.AufgabeId == aufgabe.Id).ToList();
        verbleibendeTodos.Should().BeEmpty();
    }

    /// <summary>Die Beziehung zwischen Aufgabe und Todo ist über die Navigationseigenschaft korrekt geladen.</summary>
    [Fact]
    public async Task GetDetailAsync_ShouldIncludeTodos_WhenTodosExist()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync();
        var projektId = await CreateTestProjektAsync(db);
        var aufgabeService = new AufgabeService(db.Context, NullLogger<AufgabeService>.Instance, new TodoService(db.Context, NullLogger<TodoService>.Instance));
        var todoService = new TodoService(db.Context, NullLogger<TodoService>.Instance);
        var aufgabe = await aufgabeService.CreateAsync(projektId, "Aufgabe mit verknüpften Todos", null);
        await todoService.CreateTodoAsync(aufgabe.Id, "Verknüpftes Todo");

        // Act
        var detail = await aufgabeService.GetDetailAsync(aufgabe.Id);

        // Assert
        detail.Should().NotBeNull();
        detail!.Todos.Should().ContainSingle(t => t.Beschreibung == "Verknüpftes Todo");
    }

    /// <summary>ErledigtAm wird korrekt gespeichert und beim erneuten Laden abgerufen.</summary>
    [Fact]
    public async Task MarkTodoAsCompletedAsync_ShouldPersistErledigtAm_WhenTodoExists()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync();
        var projektId = await CreateTestProjektAsync(db);
        var aufgabeService = new AufgabeService(db.Context, NullLogger<AufgabeService>.Instance, new TodoService(db.Context, NullLogger<TodoService>.Instance));
        var todoService = new TodoService(db.Context, NullLogger<TodoService>.Instance);
        var aufgabe = await aufgabeService.CreateAsync(projektId, "Aufgabe mit erledigtem Todo", null);
        var todo = await todoService.CreateTodoAsync(aufgabe.Id, "Zu erledigendes Todo");
        var vorMarkierung = DateTimeOffset.UtcNow.AddSeconds(-1);

        // Act
        await todoService.MarkTodoAsCompletedAsync(todo.Id);

        // Assert
        await using var db2 = db.CreateNewContext();
        var loaded = await db2.Todos.FindAsync(todo.Id);

        loaded!.ErledigtAm.Should().NotBeNull();
        loaded.ErledigtAm!.Value.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(3));
        loaded.ErledigtAm!.Value.Should().BeOnOrAfter(vorMarkierung);
    }
}
