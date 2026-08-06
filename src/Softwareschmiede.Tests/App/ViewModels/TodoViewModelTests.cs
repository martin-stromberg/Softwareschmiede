using System.ComponentModel;
using FluentAssertions;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.Domain.Entities;

namespace Softwareschmiede.Tests.App.ViewModels;

/// <summary>Unit-Tests für TodoViewModel.</summary>
public sealed class TodoViewModelTests
{
    private static Todo CreateTodo(DateTimeOffset? erledaltAm = null) => new()
    {
        Id = Guid.NewGuid(),
        AufgabeId = Guid.NewGuid(),
        Beschreibung = "Testbeschreibung",
        ErstellungsDatum = DateTimeOffset.UtcNow,
        ErledigtAm = erledaltAm
    };

    /// <summary>Properties sind nach der Konstruktion korrekt bindbar.</summary>
    [Fact]
    public void Constructor_PropertiesBindable()
    {
        var todo = CreateTodo();
        var sut = new TodoViewModel(todo);

        sut.Id.Should().Be(todo.Id);
        sut.Beschreibung.Should().Be(todo.Beschreibung);
        sut.ErstellungsDatum.Should().Be(todo.ErstellungsDatum);
        sut.IstErledigt.Should().BeFalse();
    }

    /// <summary>IstErledigt ist true, wenn ErledigtAm gesetzt ist.</summary>
    [Fact]
    public void Constructor_IstErledigtTrue_WhenErledigtAmSet()
    {
        var todo = CreateTodo(DateTimeOffset.UtcNow);
        var sut = new TodoViewModel(todo);

        sut.IstErledigt.Should().BeTrue();
    }

    /// <summary>PropertyChanged wird ausgelöst, wenn Beschreibung geändert wird.</summary>
    [Fact]
    public void PropertyChanged_RaisedOnPropertyUpdate_ForBeschreibung()
    {
        var sut = new TodoViewModel(CreateTodo());
        var raised = new List<string?>();
        sut.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        sut.Beschreibung = "Neue Beschreibung";

        raised.Should().Contain(nameof(TodoViewModel.Beschreibung));
        sut.Beschreibung.Should().Be("Neue Beschreibung");
    }

    /// <summary>PropertyChanged wird für IstErledigt ausgelöst, wenn der Wert geändert wird.</summary>
    [Fact]
    public void PropertyChanged_RaisedOnPropertyUpdate_ForIstErledigt()
    {
        var sut = new TodoViewModel(CreateTodo());
        var raised = new List<string?>();
        sut.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        sut.IstErledigt = true;

        raised.Should().Contain(nameof(TodoViewModel.IstErledigt));
        sut.IstErledigt.Should().BeTrue();
    }

    /// <summary>PropertyChanged wird nicht erneut ausgelöst, wenn sich der Wert nicht ändert.</summary>
    [Fact]
    public void PropertyChanged_NotRaised_WhenValueUnchanged()
    {
        var sut = new TodoViewModel(CreateTodo());
        sut.Beschreibung = "Unverändert";
        var raised = new List<string?>();
        sut.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        sut.Beschreibung = "Unverändert";

        raised.Should().BeEmpty();
    }
}
