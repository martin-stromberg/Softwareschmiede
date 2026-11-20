using Softwareschmiede.Domain.Entities;

namespace Softwareschmiede.App.ViewModels;

/// <summary>WPF-ViewModel für ein einzelnes To-Do-Element.</summary>
public sealed class TodoViewModel : ViewModelBase
{
    private string _description;
    private DateTimeOffset? _completedAt;

    /// <inheritdoc cref="TodoViewModel"/>
    public TodoViewModel(Todo todo)
    {
        Id = todo.Id;
        _description = todo.Description;
        _completedAt = todo.CompletedAt;
        CreatedAt = todo.CreatedAt;
    }

    /// <summary>Eindeutige ID des To-Dos.</summary>
    public Guid Id { get; }

    /// <summary>Text des To-Dos.</summary>
    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    /// <summary>Gibt an, ob das To-Do erledigt ist, abgeleitet aus <see cref="Todo.CompletedAt"/>.</summary>
    public bool IsCompleted
    {
        get => _completedAt is not null;
        set => SetProperty(ref _completedAt, value ? DateTimeOffset.UtcNow : null, nameof(IsCompleted));
    }

    /// <summary>Erstellungszeitstempel des To-Dos.</summary>
    public DateTimeOffset CreatedAt { get; }
}
