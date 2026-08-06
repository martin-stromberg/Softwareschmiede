# ViewModel und Präsentation

## `TaskDetailViewModel`
Datei: `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs`

### Kritische Properties und Methoden für diese Anforderung

| Element | Typ | Zeile(n) | Beschreibung |
|---------|-----|----------|-------------|
| `LadenAsync(CancellationToken ct)` | `private async Task` | 634–700 | **Blockiert den UI-Thread**: Wartet sequenziell auf `GetDetailAsync` (inkl. Protokoll-Include) und dann auf `GetByAufgabeAsync` |
| `Protokolleintraege` | `ObservableCollection<Protokolleintrag>` | 242 | Bindet Protokolleinträge an die UI; wird nach vollständigem Laden aller Protokolle gefüllt |
| `Aufgabe` | `Aufgabe?` | Prop 109–146 | Gespeicherte Aufgabe mit allen Details (einschließlich Protokolleinträge von `GetDetailAsync`) |
| `IsLoading` | `bool` | 159–168 | Flag, das während `LadenAsync` gesetzt ist (blockiert UI-Interaktionen) |

### Kritisches Lade-Verhalten (Zeilen 634–700)

**Aktueller sequenzieller Ablauf:**
```csharp
private async Task LadenAsync(CancellationToken ct)
{
    // ... Init ...
    IsLoading = true;
    
    try
    {
        // Schritt 1: Aufgabe mit Protokoll laden (blockiert bei großem Protokoll!)
        Aufgabe = await _aufgabeService.GetDetailAsync(_aufgabeId, ct);  // Line 644
        
        // ... weitere Operationen ...
        
        // Schritt 2: Redundant - Protokoll erneut laden (weil GetDetailAsync nicht direkt ObservableCollection gibt)
        var protokolleintraege = await _protokollService.GetByAufgabeAsync(_aufgabeId, ct);  // Line 666
        Protokolleintraege.Clear();
        foreach (var eintrag in protokolleintraege)
            Protokolleintraege.Add(eintrag);
        
        // Weitere Operationen (nur nach Schritt 2 abgeschlossen):
        await LadePullRequestsAsync(ct);           // Line 671
        await _todoListViewModel.LadenAsync(...);  // Line 672
        await LadeVerfuegbarePluginsAsync(ct);     // Line 674
        await LadePromptVorlagenAsync(ct);         // Line 675
        await AktualisierePullRequestCapabilityAsync(ct);  // Line 676
        await AktualisiereIssueCreateCapabilityAsync(ct);  // Line 677
        
        // Prüfung für Auto-Restart (nur wenn obige alle fertig)
        if (Aufgabe?.Status == ...) {
            await CliAutomatischNeustartenAsync(ct);  // Line 684
        }
    }
    finally
    {
        IsLoading = false;
    }
}
```

**Blockierungsverursacher:**
- Zeile 644: `GetDetailAsync` wartet auf Protokoll-Include-Chain
- Zeile 666: `GetByAufgabeAsync` lädt Protokoll erneut
- Alle folgenden Operationen (Zeilen 671–677) können erst starten, wenn Protokoll komplett geladen ist

### Gebundene UI-Komponenten

- **`TaskDetailView`**: Bindet auf `Protokolleintraege` Collection (leerer bis Laden abgeschlossen)
- **`Aufgabenbasis-Informationen`**: Werden erst angezeigt, wenn `IsLoading = false` (nach Protokoll-Laden)

---

## Abhängigkeiten der Klasse

| Dependency | Typ | Verwendung |
|------------|-----|-----------|
| `_aufgabeService` | `AufgabeService` | Ruft `GetDetailAsync` in `LadenAsync` auf |
| `_protokollService` | `ProtokollService` | Ruft `GetByAufgabeAsync` in `LadenAsync` auf |
| `_kiService` | `KiAusfuehrungsService` | CLI-Prozessmanagement |
| `_entwicklungsprozessService` | `EntwicklungsprozessService` | Koordiniert Start/Stop/Abschluss |
| Weitere Services | diverse | PR-Verwaltung, Todos, Plugins, etc. |

---

## Abonnierte Events

- `_kiService.CliProcessStatusChanged` → `OnCliProcessStatusChanged` (Line 596)
- `_promptZeitVersandService.PromptSent` → `OnPromptSent` (Line 597)
