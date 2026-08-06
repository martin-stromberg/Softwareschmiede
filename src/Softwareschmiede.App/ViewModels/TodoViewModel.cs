using Softwareschmiede.Domain.Entities;

namespace Softwareschmiede.App.ViewModels;

/// <summary>WPF-ViewModel für ein einzelnes To-Do-Element.</summary>
public sealed class TodoViewModel : ViewModelBase
{
    private string _beschreibung;
    private DateTimeOffset? _erledigtAm;

    /// <inheritdoc cref="TodoViewModel"/>
    public TodoViewModel(Todo todo)
    {
        Id = todo.Id;
        _beschreibung = todo.Beschreibung;
        _erledigtAm = todo.ErledigtAm;
        ErstellungsDatum = todo.ErstellungsDatum;
    }

    /// <summary>Eindeutige ID des To-Dos.</summary>
    public Guid Id { get; }

    /// <summary>Text des To-Dos.</summary>
    public string Beschreibung
    {
        get => _beschreibung;
        set => SetProperty(ref _beschreibung, value);
    }

    /// <summary>Gibt an, ob das To-Do erledigt ist, abgeleitet aus <see cref="Todo.ErledigtAm"/>.</summary>
    public bool IstErledigt
    {
        get => _erledigtAm is not null;
        set => SetProperty(ref _erledigtAm, value ? DateTimeOffset.UtcNow : null, nameof(IstErledigt));
    }

    /// <summary>Erstellungszeitstempel des To-Dos.</summary>
    public DateTimeOffset ErstellungsDatum { get; }
}
