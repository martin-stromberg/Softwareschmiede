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
    private readonly WorkItemService _aufgabeService;
    private readonly TodoService _todoService;
    private readonly Guid _projektId = Guid.NewGuid();

    /// <summary>TaskDetailViewModelTests_Todos.</summary>
    public TaskDetailViewModelTests_Todos()
    {
        _db = TestDbContextFactory.Create();
        _todoService = new TodoService(_db, NullLogger<TodoService>.Instance);
        _aufgabeService = new WorkItemService(_db, NullLogger<WorkItemService>.Instance, _todoService);

        _db.Projects.Add(new Project
        {
            Id = _projektId,
            Name = "Testprojekt",
            CreatedAt = DateTimeOffset.UtcNow,
            Status = ProjectStatus.Active
        });
        _db.SaveChanges();
    }

    /// <summary>Dispose.</summary>
    public void Dispose() => _db.Dispose();

    private TaskDetailViewModel CreateSut() => TaskDetailViewModelTestFactory.Create(_db, _aufgabeService);

    private async Task<WorkItem> ErstelleAufgabe(WorkItemStatus status = WorkItemStatus.New)
    {
        var workItem = await _aufgabeService.CreateAsync(_projektId, "Testaufgabe", "Beschreibung");
        if (status != WorkItemStatus.New)
            await _aufgabeService.StatusSetzenAsync(workItem.Id, status);
        return await _aufgabeService.GetByIdAsync(workItem.Id) ?? workItem;
    }

    /// <summary>LadenAsync lädt die Todos der Aufgabe in die Todos-Collection des TodoListViewModel.</summary>
    [Fact]
    public async Task LoadAsync_LoadsTodos()
    {
        var workItem = await ErstelleAufgabe();
        await _todoService.CreateTodoAsync(workItem.Id, "Erstes Todo", TestContext.Current.CancellationToken);
        await _todoService.CreateTodoAsync(workItem.Id, "Zweites Todo", TestContext.Current.CancellationToken);

        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.TodoList.Todos.Should().HaveCount(2);
        sut.TodoList.Todos.Select(t => t.Description).Should().Contain(["Erstes Todo", "Zweites Todo"]);
    }

    /// <summary>OffeneTodoCount wird nach dem Laden korrekt anhand der offenen Todos berechnet.</summary>
    [Fact]
    public async Task OffeneTodoCount_UpdatesCorrectly()
    {
        var workItem = await ErstelleAufgabe();
        await _todoService.CreateTodoAsync(workItem.Id, "Offenes Todo", TestContext.Current.CancellationToken);
        var erledigt = await _todoService.CreateTodoAsync(workItem.Id, "Erledigtes Todo", TestContext.Current.CancellationToken);
        await _todoService.MarkTodoAsCompletedAsync(erledigt.Id, TestContext.Current.CancellationToken);

        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.TodoList.OffeneTodoCount.Should().Be(1);
    }

    /// <summary>TodoHinzufuegenCommand erstellt ein neues Todo und fügt es der Collection hinzu.</summary>
    [Fact]
    public async Task TodoHinzufuegenCommand_CreatesTodo()
    {
        var workItem = await ErstelleAufgabe();
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.TodoList.NeuesTodoBeschreibung = "Neues Todo";
        await ((AsyncRelayCommand)sut.TodoList.TodoHinzufuegenCommand).ExecuteAsync();

        sut.TodoList.Todos.Should().ContainSingle(t => t.Description == "Neues Todo");
        sut.TodoList.OffeneTodoCount.Should().Be(1);
        sut.TodoList.NeuesTodoBeschreibung.Should().BeNullOrEmpty();
        var alleTodos = await _todoService.GetAllTodosAsync(workItem.Id, TestContext.Current.CancellationToken);
        alleTodos.Should().ContainSingle();
    }

    /// <summary>TodoLoeschenCommand löscht ein Todo aus Datenbank und Collection.</summary>
    [Fact]
    public async Task TodoLoeschenCommand_DeletesTodo()
    {
        var workItem = await ErstelleAufgabe();
        var todo = await _todoService.CreateTodoAsync(workItem.Id, "Zu löschendes Todo", TestContext.Current.CancellationToken);
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        await ((AsyncRelayCommand<Guid>)sut.TodoList.TodoLoeschenCommand).ExecuteAsync(todo.Id);

        sut.TodoList.Todos.Should().BeEmpty();
        var alleTodos = await _todoService.GetAllTodosAsync(workItem.Id, TestContext.Current.CancellationToken);
        alleTodos.Should().BeEmpty();
    }

    /// <summary>TodoAlsErledigtMarkierenCommand markiert ein Todo als erledigt und aktualisiert OffeneTodoCount.</summary>
    [Fact]
    public async Task TodoAlsErledigtMarkierenCommand_MarksTodoCompleted()
    {
        var workItem = await ErstelleAufgabe();
        var todo = await _todoService.CreateTodoAsync(workItem.Id, "Zu markierendes Todo", TestContext.Current.CancellationToken);
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        await ((AsyncRelayCommand<Guid>)sut.TodoList.TodoAlsErledigtMarkierenCommand).ExecuteAsync(todo.Id);

        sut.TodoList.Todos.Single().IstErledigt.Should().BeTrue();
        sut.TodoList.OffeneTodoCount.Should().Be(0);
        var alleTodos = await _todoService.GetAllTodosAsync(workItem.Id, TestContext.Current.CancellationToken);
        alleTodos.Single().CompletedAt.Should().NotBeNull();
    }

    /// <summary>AufgabeAbschliessenCommand zeigt eine Fehlermeldung an und blockiert den Abschluss, wenn offene Todos vorhanden sind.</summary>
    [Fact]
    public async Task AufgabeAbschliessenCommand_WithOpenTodos_ShowsError()
    {
        var workItem = await ErstelleAufgabe(WorkItemStatus.Started);
        await _todoService.CreateTodoAsync(workItem.Id, "Offenes Todo", TestContext.Current.CancellationToken);
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        await ((AsyncRelayCommand)sut.AufgabeAbschliessenCommand).ExecuteAsync();

        sut.FehlerMeldung.Should().Contain("1 offene To-Do(s)");
        var aktualisiert = await _aufgabeService.GetByIdAsync(workItem.Id, TestContext.Current.CancellationToken);
        aktualisiert!.Status.Should().Be(WorkItemStatus.Started);
    }

    /// <summary>AufgabeAbschliessenCommand schließt die Aufgabe erfolgreich ab, wenn keine offenen Todos vorhanden sind.</summary>
    [Fact]
    public async Task AufgabeAbschliessenCommand_WithoutOpenTodos_Succeeds()
    {
        var workItem = await ErstelleAufgabe(WorkItemStatus.Started);
        var todo = await _todoService.CreateTodoAsync(workItem.Id, "Erledigtes Todo", TestContext.Current.CancellationToken);
        await _todoService.MarkTodoAsCompletedAsync(todo.Id, TestContext.Current.CancellationToken);
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        await ((AsyncRelayCommand)sut.AufgabeAbschliessenCommand).ExecuteAsync();

        sut.FehlerMeldung.Should().BeNullOrEmpty();
        var aktualisiert = await _aufgabeService.GetByIdAsync(workItem.Id, TestContext.Current.CancellationToken);
        aktualisiert!.Status.Should().Be(WorkItemStatus.Completed);
    }
}
