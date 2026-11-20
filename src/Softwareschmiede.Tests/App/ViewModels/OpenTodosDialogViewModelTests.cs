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
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiedeDbContext _db;
    private readonly TodoService _todoService;
    private readonly Guid _workItemId;

    /// <summary>OpenTodosDialogViewModelTests.</summary>
    public OpenTodosDialogViewModelTests()
    {
        _db = TestDbContextFactory.Create();
        _todoService = new TodoService(_db, NullLogger<TodoService>.Instance);

        var projectId = Guid.NewGuid();
        _db.Projects.Add(new Project
        {
            Id = projectId,
            Name = "Testprojekt",
            CreatedAt = DateTimeOffset.UtcNow,
            Status = ProjectStatus.Active
        });

        _workItemId = Guid.NewGuid();
        _db.WorkItems.Add(new WorkItem
        {
            Id = _workItemId,
            ProjectId = projectId,
            Title = "Testaufgabe",
            Status = WorkItemStatus.New,
            CreatedAt = DateTimeOffset.UtcNow
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
        var offen = await _todoService.CreateTodoAsync(_workItemId, "Offen", TestContext.Current.CancellationToken);
        var erledigt = await _todoService.CreateTodoAsync(_workItemId, "Erledigt", TestContext.Current.CancellationToken);
        await _todoService.MarkTodoAsCompletedAsync(erledigt.Id, TestContext.Current.CancellationToken);
        var sut = new OpenTodosDialogViewModel(_todoService, NullLogger<OpenTodosDialogViewModel>.Instance);

        // Act
        await sut.LoadAsync(_workItemId, "Aufgabentitel", TestContext.Current.CancellationToken);

        // Assert
        sut.WorkItemId.Should().Be(_workItemId);
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
        var erledigt = await _todoService.CreateTodoAsync(_workItemId, "Erledigt", TestContext.Current.CancellationToken);
        await _todoService.MarkTodoAsCompletedAsync(erledigt.Id, TestContext.Current.CancellationToken);
        var sut = new OpenTodosDialogViewModel(_todoService, NullLogger<OpenTodosDialogViewModel>.Instance);

        // Act
        await sut.LoadAsync(_workItemId, "Leere Aufgabe", TestContext.Current.CancellationToken);

        // Assert
        sut.Todos.Should().BeEmpty();
        sut.HasOpenTodos.Should().BeFalse();
        sut.IsEmpty.Should().BeTrue();
    }
}
