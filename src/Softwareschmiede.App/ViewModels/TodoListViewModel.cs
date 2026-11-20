using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Application.Services;

namespace Softwareschmiede.App.ViewModels;

/// <summary>
/// Presentation Model der To-Do-Liste einer Aufgabe: verwaltet Todos, offene Anzahl, Eingabefeld
/// sowie die Erstellen-/Erledigt-/Löschen-/Ansicht-Commands.
/// </summary>
public sealed class TodoListViewModel : ViewModelBase
{
    private readonly TodoService _todoService;
    private readonly ILogger<TodoListViewModel> _logger;

    private Guid _workItemId;
    private int _openTodoCount;
    private string? _newTodoDescription;

    /// <summary>To-Dos der Aufgabe.</summary>
    public ObservableCollection<TodoViewModel> Todos { get; } = new();

    /// <summary>Anzahl offener To-Dos der Aufgabe. Wird für die Badge-Anzeige im Ribbon verwendet.</summary>
    public int OpenTodoCount
    {
        get => _openTodoCount;
        private set => SetProperty(ref _openTodoCount, value);
    }

    /// <summary>Bindung des Eingabefelds für die Beschreibung eines neuen To-Dos.</summary>
    public string? NewTodoDescription
    {
        get => _newTodoDescription;
        set => SetProperty(ref _newTodoDescription, value);
    }

    /// <summary>Wird von <see cref="TodoViewCommand"/> aufgerufen, damit der Besitzer zur Todo-Ansicht wechselt.</summary>
    public Action? ActivateViewCallback { get; set; }

    /// <summary>Wird bei Erfolg oder Fehlschlag einer Todo-Operation mit der (ggf. null) Fehlermeldung aufgerufen, damit der Besitzer sie anzeigen kann.</summary>
    public Action<string?>? ErrorCallback { get; set; }

    /// <summary>Erstellt ein neues To-Do mit der eingegebenen Beschreibung.</summary>
    public ICommand AddTodoCommand { get; }

    /// <summary>Markiert ein To-Do als erledigt.</summary>
    public ICommand MarkTodoAsCompletedCommand { get; }

    /// <summary>Löscht ein To-Do.</summary>
    public ICommand DeleteTodoCommand { get; }

    /// <summary>Wechselt zur Todo-Ansicht.</summary>
    public ICommand TodoViewCommand { get; }

    /// <inheritdoc cref="TodoListViewModel"/>
    public TodoListViewModel(TodoService todoService, ILogger<TodoListViewModel> logger)
    {
        _todoService = todoService;
        _logger = logger;

        AddTodoCommand = new AsyncRelayCommand(AddTodoAsync, () => !string.IsNullOrWhiteSpace(_newTodoDescription));
        MarkTodoAsCompletedCommand = new AsyncRelayCommand<Guid>(MarkTodoAsCompletedAsync);
        DeleteTodoCommand = new AsyncRelayCommand<Guid>(DeleteTodoAsync);
        TodoViewCommand = new RelayCommand(() => ActivateViewCallback?.Invoke());
    }

    /// <summary>Lädt die To-Dos der angegebenen Aufgabe und aktualisiert <see cref="OpenTodoCount"/>.</summary>
    /// <param name="workItemId">ID der Aufgabe, deren To-Dos geladen werden sollen.</param>
    /// <param name="ct">Abbruch-Token.</param>
    public async Task LoadAsync(Guid workItemId, CancellationToken ct)
    {
        _workItemId = workItemId;
        Todos.Clear();

        if (workItemId == Guid.Empty)
        {
            OpenTodoCount = 0;
            return;
        }

        var todos = await _todoService.GetAllTodosAsync(workItemId, ct);
        foreach (var todo in todos)
            Todos.Add(new TodoViewModel(todo));

        RefreshOpenTodoCount();
    }

    private async Task AddTodoAsync(CancellationToken ct)
    {
        if (_workItemId == Guid.Empty || string.IsNullOrWhiteSpace(_newTodoDescription))
            return;

        ErrorCallback?.Invoke(null);

        try
        {
            var todo = await _todoService.CreateTodoAsync(_workItemId, _newTodoDescription, ct);
            Todos.Add(new TodoViewModel(todo));
            RefreshOpenTodoCount();
            NewTodoDescription = null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Erstellen eines To-Dos für Aufgabe {AufgabeId}.", _workItemId);
            ErrorCallback?.Invoke($"To-Do konnte nicht erstellt werden: {ex.Message}");
        }
    }

    private async Task MarkTodoAsCompletedAsync(Guid todoId, CancellationToken ct)
    {
        ErrorCallback?.Invoke(null);

        try
        {
            await _todoService.MarkTodoAsCompletedAsync(todoId, ct);
            var todo = Todos.FirstOrDefault(t => t.Id == todoId);
            if (todo is not null)
                todo.IsCompleted = true;

            RefreshOpenTodoCount();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Markieren des To-Dos {TodoId} als erledigt.", todoId);
            ErrorCallback?.Invoke($"To-Do konnte nicht als erledigt markiert werden: {ex.Message}");
        }
    }

    private async Task DeleteTodoAsync(Guid todoId, CancellationToken ct)
    {
        ErrorCallback?.Invoke(null);

        try
        {
            await _todoService.DeleteTodoAsync(todoId, ct);
            var todo = Todos.FirstOrDefault(t => t.Id == todoId);
            if (todo is not null)
                Todos.Remove(todo);

            RefreshOpenTodoCount();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Löschen des To-Dos {TodoId}.", todoId);
            ErrorCallback?.Invoke($"To-Do konnte nicht gelöscht werden: {ex.Message}");
        }
    }

    private void RefreshOpenTodoCount()
    {
        OpenTodoCount = Todos.Count(t => !t.IsCompleted);
    }
}
