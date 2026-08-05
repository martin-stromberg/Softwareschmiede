# Logik und Services - Bestandsaufnahme

## `AufgabeService`
Datei: `src/Softwareschmiede/Application/Services/AufgabeService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| GetByProjektAsync | public | Gibt alle aktiven (nicht archivierten) Aufgaben eines Projekts zurück |
| GetArchiviertByProjektAsync | public | Gibt alle archivierten Aufgaben eines Projekts zurück |
| GetAktiveUndWartendeCountAsync | public | Gibt die Anzahl aktiver und wartender Aufgaben als Tupel zurück |
| GetByIdAsync | public | Gibt eine Aufgabe anhand ihrer ID zurück |
| GetDetailAsync | public | Gibt eine Aufgabe mit IssueReferenz und Protokolleinträgen zurück |
| GetByAlertSourceKeyAsync | public | Gibt eine Aufgabe anhand des Alert-SourceKeys zurück |
| CreateAsync | public | Erstellt eine neue Aufgabe |
| UpdateAsync | public | Aktualisiert Titel und AnforderungsBeschreibung |
| DeleteAsync | public | Löscht eine Aufgabe |
| UpdateIssueReferenzAsync | public | Aktualisiert die Issue-Referenz |
| TryAssignIssueReferenzAndUpdateDescriptionIfNoneAsync | public | Weist Issue zu und aktualisiert optional Beschreibung |

**Include-Strategie in GetDetailAsync:**
```
.Include(a => a.Projekt)
.Include(a => a.IssueReferenz)
.Include(a => a.AlertReferenz)
.Include(a => a.GitRepository)
    .ThenInclude(r => r!.StartKonfiguration)
.Include(a => a.Protokolleintraege)
    .ThenInclude(p => p.TestErgebnisse)
```

**Fehlend:** Methoden für Todo-Verwaltung:
- `GetOpenTodosAsync(Guid aufgabeId)`
- `GetTodoCountAsync(Guid aufgabeId)`
- `CanCompleteTaskAsync(Guid aufgabeId)` (Validierungsmethode)

Die GetDetailAsync-Methode müsste um `.Include(a => a.Todos)` erweitert werden, um Todos zu laden.

## `EntwicklungsprozessService`
Datei: `src/Softwareschmiede/Application/Services/EntwicklungsprozessService.cs`

Dieser Service orchestriert den Abschluss einer Aufgabe. Die Methode `AbschliessenAsync` wird vom TaskDetailViewModel aufgerufen:

```csharp
await _entwicklungsprozessService.AbschliessenAsync(_aufgabeId, ct);
```

**Fehlend:** Validierungslogik in AbschliessenAsync, um offene Todos zu prüfen und zu verhindern, dass eine Aufgabe mit offenen Todos abgeschlossen wird.

## Verwandte Services (existieren bereits)

### `ProtokollService`
- Managementmethoden für Protokolleinträge
- Wird vom AufgabeService included
- Beispiel für Service-Patterns im Projekt

### `PullRequestReferenzService`
- Managementmethoden für Pull Requests
- Pattern für Verwaltung von 1:n-Beziehungen
- Wird vom TaskDetailViewModel verwendet

### `KiAusfuehrungsService`
- CLI-Verwaltung
- Status-Tracking

## Zu erstellende Services

**TodoService** (muss erstellt werden):
- `CreateTodoAsync(Guid aufgabeId, string beschreibung, CancellationToken ct)`
- `MarkTodoAsCompletedAsync(Guid todoId, CancellationToken ct)`
- `DeleteTodoAsync(Guid todoId, CancellationToken ct)`
- `GetOpenTodosAsync(Guid aufgabeId, CancellationToken ct)`
- `GetAllTodosAsync(Guid aufgabeId, CancellationToken ct)`
- `GetTodoCountAsync(Guid aufgabeId, CancellationToken ct)` (open count)

**Validierungsmethode:**
- Sollte in AufgabeService oder separatem Validierungsservice implementiert werden
- `CanCompleteTaskAsync(Guid aufgabeId, CancellationToken ct)` - prüft auf offene Todos
- Aufgerufen von EntwicklungsprozessService.AbschliessenAsync
