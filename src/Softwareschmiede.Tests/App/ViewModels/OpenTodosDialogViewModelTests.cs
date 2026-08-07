using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.App.ViewModels;

/// <summary>Tests für OpenTodosDialogViewModel.</summary>
public sealed class OpenTodosDialogViewModelTests : IDisposable
{
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiededDbContext _db;
    private readonly TodoService _todoService;
    private readonly Guid _aufgabeId;

    /// <summary>OpenTodosDialogViewModelTests.</summary>
    public OpenTodosDialogViewModelTests()
    {
        _db = TestDbContextFactory.Create();
        _todoService = new TodoService(_db, NullLogger<TodoService>.Instance);

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

    /// <summary>LoadAsync lädt nur offene Todos und übernimmt Aufgabe-ID und Titel.</summary>
    [Fact]
    public async Task LoadAsync_ShouldLoadOnlyOpenTodosAndSetTaskData()
    {
        // Arrange
        var offen = await _todoService.CreateTodoAsync(_aufgabeId, "Offen");
        var erledigt = await _todoService.CreateTodoAsync(_aufgabeId, "Erledigt");
        await _todoService.MarkTodoAsCompletedAsync(erledigt.Id);
        var sut = new OpenTodosDialogViewModel(_todoService, NullLogger<OpenTodosDialogViewModel>.Instance);

        // Act
        await sut.LoadAsync(_aufgabeId, "Aufgabentitel");

        // Assert
        sut.AufgabeId.Should().Be(_aufgabeId);
        sut.AufgabenTitel.Should().Be("Aufgabentitel");
        sut.Todos.Should().ContainSingle();
        sut.Todos[0].Id.Should().Be(offen.Id);
        sut.HasOpenTodos.Should().BeTrue();
        sut.IsEmpty.Should().BeFalse();
    }

    /// <summary>LoadAsync setzt den Leerzustand, wenn keine offenen Todos vorhanden sind.</summary>
    [Fact]
    public async Task LoadAsync_ShouldSetEmptyState_WhenNoOpenTodosExist()
    {
        // Arrange
        var erledigt = await _todoService.CreateTodoAsync(_aufgabeId, "Erledigt");
        await _todoService.MarkTodoAsCompletedAsync(erledigt.Id);
        var sut = new OpenTodosDialogViewModel(_todoService, NullLogger<OpenTodosDialogViewModel>.Instance);

        // Act
        await sut.LoadAsync(_aufgabeId, "Leere Aufgabe");

        // Assert
        sut.Todos.Should().BeEmpty();
        sut.HasOpenTodos.Should().BeFalse();
        sut.IsEmpty.Should().BeTrue();
    }
}
