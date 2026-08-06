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

    private Guid _aufgabeId;
    private int _offeneTodoCount;
    private string? _neuesTodoBeschreibung;

    /// <summary>To-Dos der Aufgabe.</summary>
    public ObservableCollection<TodoViewModel> Todos { get; } = new();

    /// <summary>Anzahl offener To-Dos der Aufgabe. Wird für die Badge-Anzeige im Ribbon verwendet.</summary>
    public int OffeneTodoCount
    {
        get => _offeneTodoCount;
        private set => SetProperty(ref _offeneTodoCount, value);
    }

    /// <summary>Bindung des Eingabefelds für die Beschreibung eines neuen To-Dos.</summary>
    public string? NeuesTodoBeschreibung
    {
        get => _neuesTodoBeschreibung;
        set => SetProperty(ref _neuesTodoBeschreibung, value);
    }

    /// <summary>Wird von <see cref="TodoAnsichtCommand"/> aufgerufen, damit der Besitzer zur Todo-Ansicht wechselt.</summary>
    public Action? AnsichtAktivierenCallback { get; set; }

    /// <summary>Wird bei Erfolg oder Fehlschlag einer Todo-Operation mit der (ggf. null) Fehlermeldung aufgerufen, damit der Besitzer sie anzeigen kann.</summary>
    public Action<string?>? FehlerCallback { get; set; }

    /// <summary>Erstellt ein neues To-Do mit der eingegebenen Beschreibung.</summary>
    public ICommand TodoHinzufuegenCommand { get; }

    /// <summary>Markiert ein To-Do als erledigt.</summary>
    public ICommand TodoAlsErledigtMarkierenCommand { get; }

    /// <summary>Löscht ein To-Do.</summary>
    public ICommand TodoLoeschenCommand { get; }

    /// <summary>Wechselt zur Todo-Ansicht.</summary>
    public ICommand TodoAnsichtCommand { get; }

    /// <inheritdoc cref="TodoListViewModel"/>
    public TodoListViewModel(TodoService todoService, ILogger<TodoListViewModel> logger)
    {
        _todoService = todoService;
        _logger = logger;

        TodoHinzufuegenCommand = new AsyncRelayCommand(TodoHinzufuegenAsync, () => !string.IsNullOrWhiteSpace(_neuesTodoBeschreibung));
        TodoAlsErledigtMarkierenCommand = new AsyncRelayCommand<Guid>(TodoAlsErledigtMarkierenAsync);
        TodoLoeschenCommand = new AsyncRelayCommand<Guid>(TodoLoeschenAsync);
        TodoAnsichtCommand = new RelayCommand(() => AnsichtAktivierenCallback?.Invoke());
    }

    /// <summary>Lädt die To-Dos der angegebenen Aufgabe und aktualisiert <see cref="OffeneTodoCount"/>.</summary>
    /// <param name="aufgabeId">ID der Aufgabe, deren To-Dos geladen werden sollen.</param>
    /// <param name="ct">Abbruch-Token.</param>
    public async Task LadenAsync(Guid aufgabeId, CancellationToken ct)
    {
        _aufgabeId = aufgabeId;
        Todos.Clear();

        if (aufgabeId == Guid.Empty)
        {
            OffeneTodoCount = 0;
            return;
        }

        var todos = await _todoService.GetAllTodosAsync(aufgabeId, ct);
        foreach (var todo in todos)
            Todos.Add(new TodoViewModel(todo));

        OffeneTodoCountAktualisieren();
    }

    private async Task TodoHinzufuegenAsync(CancellationToken ct)
    {
        if (_aufgabeId == Guid.Empty || string.IsNullOrWhiteSpace(_neuesTodoBeschreibung))
            return;

        FehlerCallback?.Invoke(null);

        try
        {
            var todo = await _todoService.CreateTodoAsync(_aufgabeId, _neuesTodoBeschreibung, ct);
            Todos.Add(new TodoViewModel(todo));
            OffeneTodoCountAktualisieren();
            NeuesTodoBeschreibung = null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Erstellen eines To-Dos für Aufgabe {AufgabeId}.", _aufgabeId);
            FehlerCallback?.Invoke($"To-Do konnte nicht erstellt werden: {ex.Message}");
        }
    }

    private async Task TodoAlsErledigtMarkierenAsync(Guid todoId, CancellationToken ct)
    {
        FehlerCallback?.Invoke(null);

        try
        {
            await _todoService.MarkTodoAsCompletedAsync(todoId, ct);
            var todo = Todos.FirstOrDefault(t => t.Id == todoId);
            if (todo is not null)
                todo.IstErledigt = true;

            OffeneTodoCountAktualisieren();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Markieren des To-Dos {TodoId} als erledigt.", todoId);
            FehlerCallback?.Invoke($"To-Do konnte nicht als erledigt markiert werden: {ex.Message}");
        }
    }

    private async Task TodoLoeschenAsync(Guid todoId, CancellationToken ct)
    {
        FehlerCallback?.Invoke(null);

        try
        {
            await _todoService.DeleteTodoAsync(todoId, ct);
            var todo = Todos.FirstOrDefault(t => t.Id == todoId);
            if (todo is not null)
                Todos.Remove(todo);

            OffeneTodoCountAktualisieren();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Löschen des To-Dos {TodoId}.", todoId);
            FehlerCallback?.Invoke($"To-Do konnte nicht gelöscht werden: {ex.Message}");
        }
    }

    private void OffeneTodoCountAktualisieren()
    {
        OffeneTodoCount = Todos.Count(t => !t.IstErledigt);
    }
}
