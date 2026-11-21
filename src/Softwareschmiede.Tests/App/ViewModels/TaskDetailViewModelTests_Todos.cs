using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.App.ViewModels;

/// <summary>Unit-Tests für die To-Do-bezogenen Properties und Commands von TaskDetailViewModel und dessen TodoListViewModel.</summary>
public sealed class TaskDetailViewModelTests_Todos : IDisposable
{
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiedeDbContext _db;
    private readonly WorkItemService _workItemService;
    private readonly TodoService _todoService;
    private readonly Guid _projectId = Guid.NewGuid();

    /// <summary>TaskDetailViewModelTests_Todos.</summary>
    public TaskDetailViewModelTests_Todos()
    {
        _db = TestDbContextFactory.Create();
        _todoService = new TodoService(_db, NullLogger<TodoService>.Instance);
        _workItemService = new WorkItemService(_db, NullLogger<WorkItemService>.Instance, _todoService);

        _db.Projects.Add(new Project
        {
            Id = _projectId,
            Name = "Testprojekt",
            CreatedAt = DateTimeOffset.UtcNow,
            Status = ProjectStatus.Active
        });
        _db.SaveChanges();
    }

    /// <summary>Dispose.</summary>
    public void Dispose() => _db.Dispose();

    private TaskDetailViewModel CreateSut() => TaskDetailViewModelTestFactory.Create(_db, _workItemService);

    private async Task<WorkItem> CreateWorkItem(WorkItemStatus status = WorkItemStatus.New)
    {
        var workItem = await _workItemService.CreateAsync(_projectId, "Testaufgabe", "Beschreibung");
        if (status != WorkItemStatus.New)
            await _workItemService.SetStatusUncheckedAsync(workItem.Id, status);
        return await _workItemService.GetByIdAsync(workItem.Id) ?? workItem;
    }

    /// <summary>LadenAsync lädt die Todos der Aufgabe in die Todos-Collection des TodoListViewModel.</summary>
    [Fact]
    public async Task LoadAsync_LoadsTodos()
    {
        var workItem = await CreateWorkItem();
        await _todoService.CreateTodoAsync(workItem.Id, "Erstes Todo", TestContext.Current.CancellationToken);
        await _todoService.CreateTodoAsync(workItem.Id, "Zweites Todo", TestContext.Current.CancellationToken);

        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.TodoList.Todos.Should().HaveCount(2);
        sut.TodoList.Todos.Select(t => t.Description).Should().Contain(["Erstes Todo", "Zweites Todo"]);
    }

    /// <summary>OffeneTodoCount wird nach dem Laden korrekt anhand der offenen Todos berechnet.</summary>
    [Fact]
    public async Task OpenTodoCount_UpdatesCorrectly()
    {
        var workItem = await CreateWorkItem();
        await _todoService.CreateTodoAsync(workItem.Id, "Offenes Todo", TestContext.Current.CancellationToken);
        var completed = await _todoService.CreateTodoAsync(workItem.Id, "Erledigtes Todo", TestContext.Current.CancellationToken);
        await _todoService.MarkTodoAsCompletedAsync(completed.Id, TestContext.Current.CancellationToken);

        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.TodoList.OpenTodoCount.Should().Be(1);
    }

    /// <summary>TodoHinzufuegenCommand erstellt ein neues Todo und fügt es der Collection hinzu.</summary>
    [Fact]
    public async Task AddTodoCommand_CreatesTodo()
    {
        var workItem = await CreateWorkItem();
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.TodoList.NewTodoDescription = "Neues Todo";
        await ((AsyncRelayCommand)sut.TodoList.AddTodoCommand).ExecuteAsync();

        sut.TodoList.Todos.Should().ContainSingle(t => t.Description == "Neues Todo");
        sut.TodoList.OpenTodoCount.Should().Be(1);
        sut.TodoList.NewTodoDescription.Should().BeNullOrEmpty();
        var allTodos = await _todoService.GetAllTodosAsync(workItem.Id, TestContext.Current.CancellationToken);
        allTodos.Should().ContainSingle();
    }

    /// <summary>TodoDeleteCommand löscht ein Todo aus Datenbank und Collection.</summary>
    [Fact]
    public async Task DeleteTodoCommand_DeletesTodo()
    {
        var workItem = await CreateWorkItem();
        var todo = await _todoService.CreateTodoAsync(workItem.Id, "Zu löschendes Todo", TestContext.Current.CancellationToken);
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        await ((AsyncRelayCommand<Guid>)sut.TodoList.DeleteTodoCommand).ExecuteAsync(todo.Id);

        sut.TodoList.Todos.Should().BeEmpty();
        var allTodos = await _todoService.GetAllTodosAsync(workItem.Id, TestContext.Current.CancellationToken);
        allTodos.Should().BeEmpty();
    }

    /// <summary>TodoAlsErledigtMarkierenCommand markiert ein Todo als erledigt und aktualisiert OffeneTodoCount.</summary>
    [Fact]
    public async Task MarkTodoAsCompletedCommand_MarksTodoCompleted()
    {
        var workItem = await CreateWorkItem();
        var todo = await _todoService.CreateTodoAsync(workItem.Id, "Zu markierendes Todo", TestContext.Current.CancellationToken);
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        await ((AsyncRelayCommand<Guid>)sut.TodoList.MarkTodoAsCompletedCommand).ExecuteAsync(todo.Id);

        sut.TodoList.Todos.Single().IsCompleted.Should().BeTrue();
        sut.TodoList.OpenTodoCount.Should().Be(0);
        var allTodos = await _todoService.GetAllTodosAsync(workItem.Id, TestContext.Current.CancellationToken);
        allTodos.Single().CompletedAt.Should().NotBeNull();
    }

    /// <summary>AufgabeAbschliessenCommand zeigt eine Fehlermeldung an und blockiert den Abschluss, wenn offene Todos vorhanden sind.</summary>
    [Fact]
    public async Task CompleteWorkItemCommand_WithOpenTodos_ShowsError()
    {
        var workItem = await CreateWorkItem(WorkItemStatus.Started);
        await _todoService.CreateTodoAsync(workItem.Id, "Offenes Todo", TestContext.Current.CancellationToken);
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        await ((AsyncRelayCommand)sut.CompleteWorkItemCommand).ExecuteAsync();

        sut.ErrorMessage.Should().Contain("1 offene To-Do(s)");
        var updates = await _workItemService.GetByIdAsync(workItem.Id, TestContext.Current.CancellationToken);
        updates!.Status.Should().Be(WorkItemStatus.Started);
    }

    /// <summary>AufgabeAbschliessenCommand schließt die Aufgabe erfolgreich ab, wenn keine offenen Todos vorhanden sind.</summary>
    [Fact]
    public async Task CompleteWorkItemCommand_WithoutOpenTodos_Succeeds()
    {
        var workItem = await CreateWorkItem(WorkItemStatus.Started);
        var todo = await _todoService.CreateTodoAsync(workItem.Id, "Erledigtes Todo", TestContext.Current.CancellationToken);
        await _todoService.MarkTodoAsCompletedAsync(todo.Id, TestContext.Current.CancellationToken);
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        await ((AsyncRelayCommand)sut.CompleteWorkItemCommand).ExecuteAsync();

        sut.ErrorMessage.Should().BeNullOrEmpty();
        var updates = await _workItemService.GetByIdAsync(workItem.Id, TestContext.Current.CancellationToken);
        updates!.Status.Should().Be(WorkItemStatus.Completed);
    }
}
