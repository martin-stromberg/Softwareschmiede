# Anforderung

## Fachliche Zusammenfassung

Beim Laden von Aufgaben mit großem Protokoll (`AufgabeService.GetDetailAsync`) bleibt die Benutzeroberfläche blockiert. Die Aufgabendetailansicht soll schnell angezeigt werden, während das Laden der Protokolleinträge asynchron im Hintergrund erfolgt. Dies erfordert die Entkopplung der Aufgaben-Basisinformation vom Protokoll sowie die Implementierung eines asynchronen Nachlade-Mechanismus ohne Blockierung des UI-Threads.

## Betroffene Klassen und Komponenten

### Persistenz und Datenzugriff

- `AufgabeService.GetDetailAsync()` — derzeit lädt die Methode sämtliche Protokolleinträge mit `Include()`, was bei großen Protokollen blockiert
- `ProtokollService.GetByAufgabeAsync()` — separate Methode zum Abrufen der Protokolleinträge (derzeit redundant mit `GetDetailAsync`)
- `SoftwareschmiededDbContext` — keine Änderung erforderlich, aber EF Core-Queries werden optimiert

### Aufgabendatenmodell

- `Aufgabe`-Entität — Navigationseigenschaft `Protokolleintraege` wird künftig nicht mehr in `GetDetailAsync` via `Include()` geladen
- `Protokolleintrag`-Entität — keine Änderungen am Datenmodell

### Benutzeroberfläche und ViewModel

- `TaskDetailViewModel.LadenAsync()` — derzeit wartet auf `GetDetailAsync`, dann auf `_protokollService.GetByAufgabeAsync()` synchron/sequenziell
- `TaskDetailViewModel.Protokolleintraege` — ObservableCollection wird derzeit erst nach vollständigem Laden der Protokolle gefüllt
- `TaskDetailView` — Binding auf `Protokolleintraege`-Collection; UI bleibt leer, bis die Collection gefüllt ist

### Tests

- `AufgabeServiceTests.cs` — Tests für `GetDetailAsync` müssen angepasst werden (andere Includes/Excludes)
- `TaskDetailViewModelTests.cs` — Tests für das asynchrone Laden-Verhalten müssen überprüft/erweitert werden
- Mögliche Performance-Tests, um die Verbesserung zu verifizieren

## Funktionale Anforderungen

1. Die Aufgaben-Detailansicht soll öffnen und die Aufgabenbasisinformation (Titel, Status, Branch, Beschreibung, etc.) schnell anzeigen.
2. Das Protokoll darf asynchron nachgeladen werden, ohne die Anzeige der Aufgabenbasisinformation zu blockieren.
3. Während das Protokoll lädt, soll die UI responsiv bleiben und ggf. einen Lade-Indikator zeigen.
4. Nach erfolgreichem Laden der Protokolleinträge sollen diese in der UI angezeigt werden.
5. Das Verhalten darf sich für den Benutzer nicht verändern — es ist eine interne Optimierung.

## Implementierungsansatz

### Auftrennung der Datenabfragen

Modifiziere `AufgabeService.GetDetailAsync()` so, dass es die Protokolleinträge NICHT mehr mit `Include()` lädt:

**Aktuell (Zeile 82–96):**
```csharp
public async Task<Aufgabe?> GetDetailAsync(Guid id, CancellationToken ct = default)
{
    return await _db.Aufgaben
        .AsNoTracking()
        .Include(a => a.Projekt)
        .Include(a => a.IssueReferenz)
        .Include(a => a.AlertReferenz)
        .Include(a => a.GitRepository).ThenInclude(r => r!.StartKonfiguration)
        .Include(a => a.Protokolleintraege)           // ENTFERNEN
            .ThenInclude(p => p.TestErgebnisse)       // ENTFERNEN
        .Include(a => a.Todos)
        .FirstOrDefaultAsync(a => a.Id == id, ct);
}
```

**Nach der Änderung:**
```csharp
public async Task<Aufgabe?> GetDetailAsync(Guid id, CancellationToken ct = default)
{
    return await _db.Aufgaben
        .AsNoTracking()
        .Include(a => a.Projekt)
        .Include(a => a.IssueReferenz)
        .Include(a => a.AlertReferenz)
        .Include(a => a.GitRepository).ThenInclude(r => r!.StartKonfiguration)
        // .Include(a => a.Protokolleintraege) — ENTFERNT
        .Include(a => a.Todos)
        .FirstOrDefaultAsync(a => a.Id == id, ct);
}
```

### Asynchrones Laden im ViewModel

Die Methode `TaskDetailViewModel.LadenAsync()` (Zeile 634–700) wird angepasst:

**Aktuell:**
```csharp
private async Task LadenAsync(CancellationToken ct)
{
    // ...
    Aufgabe = await _aufgabeService.GetDetailAsync(_aufgabeId, ct);  // Blockiert bei großen Protokollen
    // ...
    var protokolleintraege = await _protokollService.GetByAufgabeAsync(_aufgabeId, ct);  // Folgt danach
    Protokolleintraege.Clear();
    foreach (var eintrag in protokolleintraege)
        Protokolleintraege.Add(eintrag);
    // ...
}
```

**Nach der Änderung:**
```csharp
private async Task LadenAsync(CancellationToken ct)
{
    // ...
    Aufgabe = await _aufgabeService.GetDetailAsync(_aufgabeId, ct);  // Schnell, ohne Protokolle
    
    // Übrige Initialisierungen...
    
    // Protokolle asynchron im Hintergrund laden (kein Await, nicht blockierend)
    _ = LadeProtokolleAsynch(ct);
    
    // Übrige asynchrone Operationen wie LadePullRequestsAsync, etc.
    // ...
}

/// <summary>
/// Lädt Protokolleinträge asynchron im Hintergrund, ohne das UI zu blockieren.
/// Fehler werden protokolliert und dem Benutzer ggf. angezeigt.
/// </summary>
private async Task LadeProtokolleAsynch(CancellationToken ct)
{
    try
    {
        var protokolleintraege = await _protokollService.GetByAufgabeAsync(_aufgabeId, ct);
        Protokolleintraege.Clear();
        foreach (var eintrag in protokolleintraege)
            Protokolleintraege.Add(eintrag);
    }
    catch (OperationCanceledException)
    {
        // Erwarteter Fehler bei Abbruch
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Fehler beim asynchronen Laden der Protokolle für Aufgabe {AufgabeId}.", _aufgabeId);
        // Optional: Fehlermeldung in der UI anzeigen
    }
}
```

### Optionale UI-Verbesserung

Falls gewünscht kann die UI einen Lade-Indikator oder Platzhalter zeigen, während die Protokolle geladen werden:
- Beispiel: `Protokolleintraege.Count == 0` könnte "Protokoll wird geladen…" bedeuten oder leer bleiben
- Der Benutzer sieht dies nur, wenn das Protokoll besonders groß ist

## Konfiguration

Keine neue Konfiguration erforderlich. Die Änderung ist rein technisch und verändert das Benutzerverhalten nicht.

## Nicht-Ziele

- Paginierung oder Lazy-Loading des Protokolls (wird aus den Anforderungen nicht verlangt, aber könnten später hinzugefügt werden)
- Caching der Protokolleinträge
- Änderungen am Datenmodell (`Aufgabe`, `Protokolleintrag`)
- Anpassungen an andere Services oder API-Endpunkte

## Offene Fragen

1. Soll die UI einen expliziten Lade-Indikator oder Platzhalter zeigen, während die Protokolle geladen werden, oder sind die Protokolle still im Hintergrund verborgen?
2. Wenn das Laden der Protokolle fehlschlägt oder sehr lange dauert: Soll der Benutzer eine Fehlermeldung/Warnung sehen oder wird dies nur protokolliert?
3. Können die Todos (`Aufgabe.Todos`) auch groß sein? Sollen diese ebenfalls asynchron nachgeladen werden?
4. Gibt es Tests für das Lade-Verhalten, die angepasst werden müssen?
5. Soll die Änderung von `GetDetailAsync` auch andere Aufrufer dieser Methode beeinflussen (z. B. in anderen Services oder Views)?
