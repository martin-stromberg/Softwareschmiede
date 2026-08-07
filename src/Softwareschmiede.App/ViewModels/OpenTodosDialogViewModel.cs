using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Application.Services;

namespace Softwareschmiede.App.ViewModels;

/// <summary>Read-only ViewModel für die offenen To-Dos einer Aufgabe.</summary>
public sealed class OpenTodosDialogViewModel : ViewModelBase
{
    private readonly TodoService _todoService;
    private readonly ILogger<OpenTodosDialogViewModel> _logger;
    private Guid _aufgabeId;
    private string _aufgabenTitel = string.Empty;

    /// <inheritdoc cref="OpenTodosDialogViewModel"/>
    public OpenTodosDialogViewModel(TodoService todoService, ILogger<OpenTodosDialogViewModel> logger)
    {
        _todoService = todoService;
        _logger = logger;
    }

    /// <summary>Eindeutige ID der Aufgabe.</summary>
    public Guid AufgabeId
    {
        get => _aufgabeId;
        private set => SetProperty(ref _aufgabeId, value);
    }

    /// <summary>Titel der Aufgabe.</summary>
    public string AufgabenTitel
    {
        get => _aufgabenTitel;
        private set => SetProperty(ref _aufgabenTitel, value);
    }

    /// <summary>Offene To-Dos der Aufgabe.</summary>
    public ObservableCollection<TodoViewModel> Todos { get; } = new();

    /// <summary>Gibt an, ob offene To-Dos vorhanden sind.</summary>
    public bool HasOpenTodos => Todos.Count > 0;

    /// <summary>Gibt an, ob keine offenen To-Dos vorhanden sind.</summary>
    public bool IsEmpty => !HasOpenTodos;

    /// <summary>Lädt die offenen To-Dos einer Aufgabe.</summary>
    public async Task LoadAsync(Guid aufgabeId, string aufgabenTitel, CancellationToken ct = default)
    {
        _logger.LogInformation("Offene To-Dos für Aufgabe {AufgabeId} laden.", aufgabeId);

        AufgabeId = aufgabeId;
        AufgabenTitel = aufgabenTitel;

        Todos.Clear();
        var todos = await _todoService.GetOpenTodosAsync(aufgabeId, ct);
        foreach (var todo in todos)
        {
            Todos.Add(new TodoViewModel(todo));
        }

        OnPropertyChanged(nameof(HasOpenTodos));
        OnPropertyChanged(nameof(IsEmpty));
    }
}
