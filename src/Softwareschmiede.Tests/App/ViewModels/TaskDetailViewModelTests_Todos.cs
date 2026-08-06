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
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiededDbContext _db;
    private readonly AufgabeService _aufgabeService;
    private readonly TodoService _todoService;
    private readonly Guid _projektId = Guid.NewGuid();

    /// <summary>TaskDetailViewModelTests_Todos.</summary>
    public TaskDetailViewModelTests_Todos()
    {
        _db = TestDbContextFactory.Create();
        _todoService = new TodoService(_db, NullLogger<TodoService>.Instance);
        _aufgabeService = new AufgabeService(_db, NullLogger<AufgabeService>.Instance, _todoService);

        _db.Projekte.Add(new Projekt
        {
            Id = _projektId,
            Name = "Testprojekt",
            ErstellungsDatum = DateTimeOffset.UtcNow,
            Status = ProjektStatus.Aktiv
        });
        _db.SaveChanges();
    }

    /// <summary>Dispose.</summary>
    public void Dispose() => _db.Dispose();

    private TaskDetailViewModel CreateSut() => TaskDetailViewModelTestFactory.Create(_db, _aufgabeService);

    private async Task<Aufgabe> ErstelleAufgabe(AufgabeStatus status = AufgabeStatus.Neu)
    {
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Testaufgabe", "Beschreibung");
        if (status != AufgabeStatus.Neu)
            await _aufgabeService.StatusSetzenAsync(aufgabe.Id, status);
        return await _aufgabeService.GetByIdAsync(aufgabe.Id) ?? aufgabe;
    }

    /// <summary>LadenAsync lädt die Todos der Aufgabe in die Todos-Collection des TodoListViewModel.</summary>
    [Fact]
    public async Task LoadAsync_LoadsTodos()
    {
        var aufgabe = await ErstelleAufgabe();
        await _todoService.CreateTodoAsync(aufgabe.Id, "Erstes Todo");
        await _todoService.CreateTodoAsync(aufgabe.Id, "Zweites Todo");

        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.TodoList.Todos.Should().HaveCount(2);
        sut.TodoList.Todos.Select(t => t.Beschreibung).Should().Contain(["Erstes Todo", "Zweites Todo"]);
    }

    /// <summary>OffeneTodoCount wird nach dem Laden korrekt anhand der offenen Todos berechnet.</summary>
    [Fact]
    public async Task OffeneTodoCount_UpdatesCorrectly()
    {
        var aufgabe = await ErstelleAufgabe();
        await _todoService.CreateTodoAsync(aufgabe.Id, "Offenes Todo");
        var erledigt = await _todoService.CreateTodoAsync(aufgabe.Id, "Erledigtes Todo");
        await _todoService.MarkTodoAsCompletedAsync(erledigt.Id);

        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.TodoList.OffeneTodoCount.Should().Be(1);
    }

    /// <summary>TodoHinzufuegenCommand erstellt ein neues Todo und fügt es der Collection hinzu.</summary>
    [Fact]
    public async Task TodoHinzufuegenCommand_CreatesTodo()
    {
        var aufgabe = await ErstelleAufgabe();
        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.TodoList.NeuesTodoBeschreibung = "Neues Todo";
        await ((AsyncRelayCommand)sut.TodoList.TodoHinzufuegenCommand).ExecuteAsync();

        sut.TodoList.Todos.Should().ContainSingle(t => t.Beschreibung == "Neues Todo");
        sut.TodoList.OffeneTodoCount.Should().Be(1);
        sut.TodoList.NeuesTodoBeschreibung.Should().BeNullOrEmpty();
        var alleTodos = await _todoService.GetAllTodosAsync(aufgabe.Id);
        alleTodos.Should().ContainSingle();
    }

    /// <summary>TodoLoeschenCommand löscht ein Todo aus Datenbank und Collection.</summary>
    [Fact]
    public async Task TodoLoeschenCommand_DeletesTodo()
    {
        var aufgabe = await ErstelleAufgabe();
        var todo = await _todoService.CreateTodoAsync(aufgabe.Id, "Zu löschendes Todo");
        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        await ((AsyncRelayCommand<Guid>)sut.TodoList.TodoLoeschenCommand).ExecuteAsync(todo.Id);

        sut.TodoList.Todos.Should().BeEmpty();
        var alleTodos = await _todoService.GetAllTodosAsync(aufgabe.Id);
        alleTodos.Should().BeEmpty();
    }

    /// <summary>TodoAlsErledigtMarkierenCommand markiert ein Todo als erledigt und aktualisiert OffeneTodoCount.</summary>
    [Fact]
    public async Task TodoAlsErledigtMarkierenCommand_MarksTodoCompleted()
    {
        var aufgabe = await ErstelleAufgabe();
        var todo = await _todoService.CreateTodoAsync(aufgabe.Id, "Zu markierendes Todo");
        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        await ((AsyncRelayCommand<Guid>)sut.TodoList.TodoAlsErledigtMarkierenCommand).ExecuteAsync(todo.Id);

        sut.TodoList.Todos.Single().IstErledigt.Should().BeTrue();
        sut.TodoList.OffeneTodoCount.Should().Be(0);
        var alleTodos = await _todoService.GetAllTodosAsync(aufgabe.Id);
        alleTodos.Single().ErledigtAm.Should().NotBeNull();
    }

    /// <summary>AufgabeAbschliessenCommand zeigt eine Fehlermeldung an und blockiert den Abschluss, wenn offene Todos vorhanden sind.</summary>
    [Fact]
    public async Task AufgabeAbschliessenCommand_WithOpenTodos_ShowsError()
    {
        var aufgabe = await ErstelleAufgabe(AufgabeStatus.Gestartet);
        await _todoService.CreateTodoAsync(aufgabe.Id, "Offenes Todo");
        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        await ((AsyncRelayCommand)sut.AufgabeAbschliessenCommand).ExecuteAsync();

        sut.FehlerMeldung.Should().Contain("1 offene To-Do(s)");
        var aktualisiert = await _aufgabeService.GetByIdAsync(aufgabe.Id);
        aktualisiert!.Status.Should().Be(AufgabeStatus.Gestartet);
    }

    /// <summary>AufgabeAbschliessenCommand schließt die Aufgabe erfolgreich ab, wenn keine offenen Todos vorhanden sind.</summary>
    [Fact]
    public async Task AufgabeAbschliessenCommand_WithoutOpenTodos_Succeeds()
    {
        var aufgabe = await ErstelleAufgabe(AufgabeStatus.Gestartet);
        var todo = await _todoService.CreateTodoAsync(aufgabe.Id, "Erledigtes Todo");
        await _todoService.MarkTodoAsCompletedAsync(todo.Id);
        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        await ((AsyncRelayCommand)sut.AufgabeAbschliessenCommand).ExecuteAsync();

        sut.FehlerMeldung.Should().BeNullOrEmpty();
        var aktualisiert = await _aufgabeService.GetByIdAsync(aufgabe.Id);
        aktualisiert!.Status.Should().Be(AufgabeStatus.Beendet);
    }
}
