# Logik-Services: Aufgabe und Protokoll

## `AufgabeService`
Datei: `src/Softwareschmiede/Application/Services/AufgabeService.cs`

### Kritische Methode für diese Anforderung

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetDetailAsync(Guid id, CancellationToken ct)` | public | **BLOCKIEREND**: Lädt eine Aufgabe mit ALL ihren Relationen: `Projekt`, `IssueReferenz`, `AlertReferenz`, `GitRepository` + `StartKonfiguration`, `Protokolleintraege` + nested `TestErgebnisse`, und `Todos`. |

**Zeilen 82–96 (kritischer Code):**
```csharp
public async Task<Aufgabe?> GetDetailAsync(Guid id, CancellationToken ct = default)
{
    _logger.LogInformation("Aufgabe {AufgabeId} mit Details abrufen.", id);
    return await _db.Aufgaben
        .AsNoTracking()
        .Include(a => a.Projekt)
        .Include(a => a.IssueReferenz)
        .Include(a => a.AlertReferenz)
        .Include(a => a.GitRepository)
            .ThenInclude(r => r!.StartKonfiguration)
        .Include(a => a.Protokolleintraege)        // ← BLOCKIERT bei großen Protokollen
            .ThenInclude(p => p.TestErgebnisse)
        .Include(a => a.Todos)
        .FirstOrDefaultAsync(a => a.Id == id, ct);
}
```

**Problem**: Die `Include(a => a.Protokolleintraege).ThenInclude(p => p.TestErgebnisse)` lädt rekursiv alle Protokolleinträge mit Testergebnissen, was bei großen Protokollen die Datenbankabfrage blockiert und den UI-Thread stoppt.

### Andere relevante Methoden

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetByIdAsync(Guid id, CancellationToken ct)` | public | Lädt eine Aufgabe ohne Include (nur Basis-Properties) — schnell |
| `GetByProjektAsync(Guid projektId, CancellationToken ct)` | public | Lädt alle Aufgaben eines Projekts mit IssueReferenz und AlertReferenz, aber **ohne** Protokolleinträge |

---

## `ProtokollService`
Datei: `src/Softwareschmiede/Application/Services/ProtokollService.cs`

### Kritische Methode für diese Anforderung

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetByAufgabeAsync(Guid aufgabeId, CancellationToken ct)` | public | Lädt **alle** Protokolleinträge einer Aufgabe mit `.Include(p => p.TestErgebnisse)`. Derzeit redundant mit der Protokoll-Portion von `AufgabeService.GetDetailAsync`. |

**Zeilen 25–34 (Alternative zur GetDetailAsync-Protokoll-Portion):**
```csharp
public async Task<IReadOnlyList<Protokolleintrag>> GetByAufgabeAsync(Guid aufgabeId, CancellationToken ct = default)
{
    _logger.LogInformation("Protokolleinträge für Aufgabe {AufgabeId} abrufen.", aufgabeId);
    return await _db.Protokolleintraege
        .AsNoTracking()
        .Include(p => p.TestErgebnisse)
        .Where(p => p.AufgabeId == aufgabeId)
        .OrderBy(p => p.Zeitstempel)
        .ToListAsync(ct);
}
```

### Weitere Methoden

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `AddEintragAsync(Guid aufgabeId, ProtokollTyp typ, string inhalt, ...)` | public | Erstellt einen neuen Protokolleintrag |
| `AddTestErgebnisseAsync(Guid aufgabeId, TestResult testResult, ...)` | public | Erstellt einen Protokolleintrag vom Typ TestErgebnis mit nested TestErgebnisse |
| `SuchenAsync(Guid aufgabeId, string suchbegriff, ...)` | public | Sucht in Inhalt und AgentName der Protokolleinträge (mit `.Include(p => p.TestErgebnisse)`) |

**Dienste sind Dependencies:**
- `AufgabeService` ist DI-Dependency von `TaskDetailViewModel`
- `ProtokollService` ist DI-Dependency von `TaskDetailViewModel`

---

## Aufrufer-Beziehungen

**`TaskDetailViewModel` verwendet:**
- `_aufgabeService.GetDetailAsync(_aufgabeId, ct)` in `LadenAsync()` (Zeile 644)
- `_protokollService.GetByAufgabeAsync(_aufgabeId, ct)` in `LadenAsync()` (Zeile 666)

**Problem-Sequenz:**
```
LadenAsync()
  → await GetDetailAsync()           (blockiert bei großem Protokoll)
  → await GetByAufgabeAsync()        (redundant: lädt Protokoll erneut)
```

Dies blockiert die UI-Rendering, bis beide Abfragen abgeschlossen sind.
