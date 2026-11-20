using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Infrastructure.Data;

namespace Softwareschmiede.Application.Services;

/// <summary>Service für To-Do-CRUD-Operationen und Abfragen.</summary>
public sealed class TodoService
{
    private readonly SoftwareschmiedeDbContext _db;
    private readonly ILogger<TodoService> _logger;

    /// <inheritdoc cref="TodoService"/>
    public TodoService(SoftwareschmiedeDbContext db, ILogger<TodoService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>Erstellt ein neues To-Do für eine Aufgabe.</summary>
    public async Task<Todo> CreateTodoAsync(Guid workItemId, string beschreibung, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(beschreibung);

        _logger.LogInformation("To-Do für Aufgabe {AufgabeId} erstellen.", workItemId);

        var todo = new Todo
        {
            Id = Guid.NewGuid(),
            WorkItemId = workItemId,
            Description = beschreibung,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.Todos.Add(todo);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("To-Do {TodoId} für Aufgabe {AufgabeId} erstellt.", todo.Id, workItemId);
        return todo;
    }

    /// <summary>Markiert ein To-Do als erledigt.</summary>
    public async Task MarkTodoAsCompletedAsync(Guid todoId, CancellationToken ct = default)
    {
        _logger.LogInformation("To-Do {TodoId} als erledigt markieren.", todoId);

        var todo = await _db.Todos.FindAsync([todoId], ct)
            ?? throw new InvalidOperationException($"To-Do {todoId} nicht gefunden.");

        todo.CompletedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("To-Do {TodoId} als erledigt markiert.", todoId);
    }

    /// <summary>Löscht ein To-Do.</summary>
    public async Task DeleteTodoAsync(Guid todoId, CancellationToken ct = default)
    {
        _logger.LogInformation("To-Do {TodoId} löschen.", todoId);

        var todo = await _db.Todos.FindAsync([todoId], ct)
            ?? throw new InvalidOperationException($"To-Do {todoId} nicht gefunden.");

        _db.Todos.Remove(todo);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("To-Do {TodoId} gelöscht.", todoId);
    }

    /// <summary>Gibt die offenen To-Dos (<see cref="Todo.IstOffen"/>) einer Aufgabe zurück.</summary>
    public async Task<IReadOnlyList<Todo>> GetOpenTodosAsync(Guid workItemId, CancellationToken ct = default)
        // t.ErledigtAm == null statt t.IstOffen: EF Core kann die berechnete Eigenschaft nicht in SQL
        // übersetzen ("Translation of member 'IstOffen' ... failed") - siehe TodoService.GetTodoCountAsync.
        => await _db.Todos
            .AsNoTracking()
            .Where(t => t.WorkItemId == workItemId && t.CompletedAt == null)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync(ct);

    /// <summary>Gibt alle To-Dos (offen und erledigt) einer Aufgabe zurück.</summary>
    public async Task<IReadOnlyList<Todo>> GetAllTodosAsync(Guid workItemId, CancellationToken ct = default)
        => await _db.Todos
            .AsNoTracking()
            .Where(t => t.WorkItemId == workItemId)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync(ct);

    /// <summary>Gibt die Anzahl offener To-Dos einer Aufgabe zurück.</summary>
    public async Task<int> GetTodoCountAsync(Guid workItemId, CancellationToken ct = default)
        // t.ErledigtAm == null statt t.IstOffen: EF Core kann die berechnete Eigenschaft Todo.IstOffen
        // nicht in SQL übersetzen (InvalidOperationException "Translation of member 'IstOffen' ...
        // failed"), da sie kein gemapptes Modellmitglied ist. Die Eigenschaft bleibt für In-Memory-
        // LINQ-Ausdrücke (z. B. EntwicklungsprozessService.AbschliessenAsync) nutzbar.
        => await _db.Todos
            .AsNoTracking()
            .CountAsync(t => t.WorkItemId == workItemId && t.CompletedAt == null, ct);

    /// <summary>Gibt die Anzahl offener To-Dos für mehrere Aufgaben zurück.</summary>
    public async Task<IReadOnlyDictionary<Guid, int>> GetOpenTodoCountsAsync(
        IEnumerable<Guid> aufgabeIds,
        CancellationToken ct = default)
    {
        var ids = aufgabeIds.Distinct().ToArray();
        if (ids.Length == 0)
            return new Dictionary<Guid, int>();

        return await _db.Todos
            .AsNoTracking()
            .Where(t => ids.Contains(t.WorkItemId) && t.CompletedAt == null)
            .GroupBy(t => t.WorkItemId)
            .Select(g => new { WorkItemId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.WorkItemId, x => x.Count, ct);
    }
}
