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
        var project = await service.CreateAsync("Todo-Integrationstestprojekt", null);
        return project.Id;
    }

    /// <summary>Todos werden in der Datenbank gespeichert und können über einen neuen Context gelesen werden.</summary>
    [Fact]
    public async Task CreateTodoAsync_ShouldPersistTodo_WhenValidDataGiven()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync(TestContext.Current.CancellationToken);
        var projectId = await CreateTestProjektAsync(db);
        var aufgabeService = new AufgabeService(db.Context, NullLogger<AufgabeService>.Instance, new TodoService(db.Context, NullLogger<TodoService>.Instance));
        var todoService = new TodoService(db.Context, NullLogger<TodoService>.Instance);
        var workItem = await aufgabeService.CreateAsync(projectId, "Aufgabe mit Todo", null, ct: TestContext.Current.CancellationToken);

        // Act
        var created = await todoService.CreateTodoAsync(workItem.Id, "Persistiertes Todo", TestContext.Current.CancellationToken);

        // Assert
        await using var db2 = db.CreateNewContext();
        var loaded = await db2.Todos.FindAsync(new object?[] { created.Id }, TestContext.Current.CancellationToken);

        loaded.Should().NotBeNull();
        loaded!.Description.Should().Be("Persistiertes Todo");
        loaded.WorkItemId.Should().Be(workItem.Id);
        loaded.CompletedAt.Should().BeNull();
    }

    /// <summary>Cascade-Delete: Löschen der Aufgabe entfernt alle zugehörigen Todos.</summary>
    [Fact]
    public async Task Cascade_Delete_DeletesAllTodos()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync(TestContext.Current.CancellationToken);
        var projectId = await CreateTestProjektAsync(db);
        var aufgabeService = new AufgabeService(db.Context, NullLogger<AufgabeService>.Instance, new TodoService(db.Context, NullLogger<TodoService>.Instance));
        var todoService = new TodoService(db.Context, NullLogger<TodoService>.Instance);
        var workItem = await aufgabeService.CreateAsync(projectId, "Aufgabe mit Todos", null, ct: TestContext.Current.CancellationToken);
        await todoService.CreateTodoAsync(workItem.Id, "Todo 1", TestContext.Current.CancellationToken);
        await todoService.CreateTodoAsync(workItem.Id, "Todo 2", TestContext.Current.CancellationToken);

        // Act
        await aufgabeService.DeleteAsync(workItem.Id, TestContext.Current.CancellationToken);

        // Assert
        await using var db2 = db.CreateNewContext();
        var verbleibendeTodos = db2.Todos.Where(t => t.WorkItemId == workItem.Id).ToList();
        verbleibendeTodos.Should().BeEmpty();
    }

    /// <summary>Die Beziehung zwischen Aufgabe und Todo ist über die Navigationseigenschaft korrekt geladen.</summary>
    [Fact]
    public async Task GetDetailAsync_ShouldIncludeTodos_WhenTodosExist()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync(TestContext.Current.CancellationToken);
        var projectId = await CreateTestProjektAsync(db);
        var aufgabeService = new AufgabeService(db.Context, NullLogger<AufgabeService>.Instance, new TodoService(db.Context, NullLogger<TodoService>.Instance));
        var todoService = new TodoService(db.Context, NullLogger<TodoService>.Instance);
        var workItem = await aufgabeService.CreateAsync(projectId, "Aufgabe mit verknüpften Todos", null, ct: TestContext.Current.CancellationToken);
        await todoService.CreateTodoAsync(workItem.Id, "Verknüpftes Todo", TestContext.Current.CancellationToken);

        // Act
        var detail = await aufgabeService.GetDetailAsync(workItem.Id, TestContext.Current.CancellationToken);

        // Assert
        detail.Should().NotBeNull();
        detail!.Todos.Should().ContainSingle(t => t.Description == "Verknüpftes Todo");
    }

    /// <summary>ErledigtAm wird korrekt gespeichert und beim erneuten Laden abgerufen.</summary>
    [Fact]
    public async Task MarkTodoAsCompletedAsync_ShouldPersistErledigtAm_WhenTodoExists()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync(TestContext.Current.CancellationToken);
        var projectId = await CreateTestProjektAsync(db);
        var aufgabeService = new AufgabeService(db.Context, NullLogger<AufgabeService>.Instance, new TodoService(db.Context, NullLogger<TodoService>.Instance));
        var todoService = new TodoService(db.Context, NullLogger<TodoService>.Instance);
        var workItem = await aufgabeService.CreateAsync(projectId, "Aufgabe mit erledigtem Todo", null, ct: TestContext.Current.CancellationToken);
        var todo = await todoService.CreateTodoAsync(workItem.Id, "Zu erledigendes Todo", TestContext.Current.CancellationToken);
        var vorMarkierung = DateTimeOffset.UtcNow.AddSeconds(-1);

        // Act
        await todoService.MarkTodoAsCompletedAsync(todo.Id, TestContext.Current.CancellationToken);

        // Assert
        await using var db2 = db.CreateNewContext();
        var loaded = await db2.Todos.FindAsync(new object?[] { todo.Id }, TestContext.Current.CancellationToken);

        loaded!.CompletedAt.Should().NotBeNull();
        loaded.CompletedAt!.Value.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(3));
        loaded.CompletedAt!.Value.Should().BeOnOrAfter(vorMarkierung);
    }
}
