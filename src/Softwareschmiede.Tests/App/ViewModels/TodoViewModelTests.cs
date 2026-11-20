using System.ComponentModel;
using FluentAssertions;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.Domain.Entities;

namespace Softwareschmiede.Tests.App.ViewModels;

/// <summary>Unit-Tests für TodoViewModel.</summary>
public sealed class TodoViewModelTests
{
    private static Todo CreateTodo(DateTimeOffset? completedAt = null) => new()
    {
        Id = Guid.NewGuid(),
        WorkItemId = Guid.NewGuid(),
        Description = "Testbeschreibung",
        CreatedAt = DateTimeOffset.UtcNow,
        CompletedAt = completedAt
    };

    /// <summary>Properties sind nach der Konstruktion korrekt bindbar.</summary>
    [Fact]
    public void Constructor_PropertiesBindable()
    {
        var todo = CreateTodo();
        var sut = new TodoViewModel(todo);

        sut.Id.Should().Be(todo.Id);
        sut.Description.Should().Be(todo.Description);
        sut.CreatedAt.Should().Be(todo.CreatedAt);
        sut.IsCompleted.Should().BeFalse();
    }

    /// <summary>IstErledigt ist true, wenn ErledigtAm gesetzt ist.</summary>
    [Fact]
    public void Constructor_IsCompletedTrue_WhenCompletedAtSet()
    {
        var todo = CreateTodo(DateTimeOffset.UtcNow);
        var sut = new TodoViewModel(todo);

        sut.IsCompleted.Should().BeTrue();
    }

    /// <summary>PropertyChanged wird ausgelöst, wenn Beschreibung geändert wird.</summary>
    [Fact]
    public void PropertyChanged_RaisedOnPropertyUpdate_ForDescription()
    {
        var sut = new TodoViewModel(CreateTodo());
        var raised = new List<string?>();
        sut.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        sut.Description = "Neue Beschreibung";

        raised.Should().Contain(nameof(TodoViewModel.Description));
        sut.Description.Should().Be("Neue Beschreibung");
    }

    /// <summary>PropertyChanged wird für IstErledigt ausgelöst, wenn der Wert geändert wird.</summary>
    [Fact]
    public void PropertyChanged_RaisedOnPropertyUpdate_ForIsCompleted()
    {
        var sut = new TodoViewModel(CreateTodo());
        var raised = new List<string?>();
        sut.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        sut.IsCompleted = true;

        raised.Should().Contain(nameof(TodoViewModel.IsCompleted));
        sut.IsCompleted.Should().BeTrue();
    }

    /// <summary>PropertyChanged wird nicht erneut ausgelöst, wenn sich der Wert nicht ändert.</summary>
    [Fact]
    public void PropertyChanged_NotRaised_WhenValueUnchanged()
    {
        var sut = new TodoViewModel(CreateTodo());
        sut.Description = "Unverändert";
        var raised = new List<string?>();
        sut.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        sut.Description = "Unverändert";

        raised.Should().BeEmpty();
    }
}
