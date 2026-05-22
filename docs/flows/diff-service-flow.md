# Ablauf – Diff-Pipeline (DiffController, DiffService, Algorithmus & Caching)

## Titel & Kontext

Dieser Ablauf dokumentiert die implementierte Diff-Pipeline vom HTTP-Einstieg bis zur Persistierung und Cache-Verwaltung.
Im Fokus stehen `DiffController`, `DiffService`, `DiffAlgorithmService` und `DiffCachingService` inklusive Validierungs- und Fehlerpfaden.
Die Beschreibung ist auf den aktuellen Code und die ergänzten Service-Tests abgestimmt.

---

## Diagramm – Programmablauf `GenerateDiffAsync`

```mermaid
flowchart TD
    A([HTTP POST /api/diff/generate]) --> B{SourceContent und TargetContent gesetzt?}
    B -- Nein -.-> B1[400 BadRequest ProblemDetails]
    B -- Ja --> C[DiffController ruft DiffService.GenerateDiffAsync auf]
    C --> D{Aufgabe vorhanden?}
    D -- Nein -.-> D1[InvalidOperationException]
    D -- Ja --> E[DiffResult mit Status Pending erstellen]
    E --> F[DiffCachingService.GetFromCacheAsync]
    F --> G{Cache-Hit?}
    G -- Ja --> H[Status Cached und Ergebnis zurückgeben]
    G -- Nein --> I[DiffAlgorithmService.GenerateDiffAsync]
    I --> J{Algorithmus-Eingaben valide?}
    J -- Nein -.-> J1[ArgumentNullException]
    J -- Ja --> K[Blöcke und Zeilenzählung berechnen]
    K --> L[DiffResult Status Generated persistieren]
    L --> M[SetInCacheAsync asynchron starten]
    M --> N[Controller mappt DTO und gibt 200 zurück]
    D1 -.-> O[Controller fängt Exception und gibt 500 zurück]
    J1 -.-> P[DiffService setzt Status Error und persistiert]
    P -.-> O
```

---

## Schrittbeschreibung

1. **HTTP-Validierung im Controller**
   - **Code:** `src/Softwareschmiede/Controllers/DiffController.cs` (`GenerateDiffAsync`)
   - **Eingaben:** `GenerateDiffRequest` (`AufgabeId`, `FilePath`, `SourceContent`, `TargetContent`, `SourceVersion`, `TargetVersion`, `DiffType`, `CachingStrategy`)
   - **Ausgaben/Seiteneffekte:** Bei leerem `SourceContent` oder `TargetContent` sofort `400 BadRequest` mit `ProblemDetails`; sonst Weitergabe an Service.

2. **Service-Initialisierung und Guard-Checks**
   - **Code:** `src/Softwareschmiede/Application/Services/DiffService.cs` (`GenerateDiffAsync`)
   - **Eingaben:** Aufgabe-/Dateikontext plus Textinhalte.
   - **Ausgaben/Seiteneffekte:** Validiert `filePath`, `sourceVersion`, `targetVersion`; lädt Aufgabe über EF Core (`_db.Aufgaben.FindAsync`), bei fehlender Aufgabe `InvalidOperationException`.

3. **Anlage eines neuen DiffResult**
   - **Code:** `DiffService.GenerateDiffAsync`
   - **Eingaben:** geprüfte Parameter.
   - **Ausgaben/Seiteneffekte:** Neues `DiffResult` mit `Status = Pending`, `GeneratedAt`, `ExpiresAt` (24h TTL), Metadaten (`FilePath`, Versionen, `DiffType`).

4. **Cache-Lookup (2-Tier)**
   - **Code:** `src/Softwareschmiede/Application/Services/DiffCachingService.cs` (`GetFromCacheAsync`)
   - **Eingaben:** `aufgabeId`, `filePath`, `sourceVersion`, `targetVersion`, `diffResultId`.
   - **Ausgaben/Seiteneffekte:** Prüft zuerst In-Memory-Cache (`IMemoryCache`), danach persistenten Cache (`DiffCaches` + JSON-Deserialisierung). Bei Treffer Rückgabe eines gecachten `DiffResult`.

5. **Diff-Berechnung**
   - **Code:** `src/Softwareschmiede/Application/Services/DiffAlgorithmService.cs` (`GenerateDiffAsync`, `ComputeDiff`, `GroupDiffsIntoBlocks`)
   - **Eingaben:** `sourceContent`, `targetContent`, `diffResultId`.
   - **Ausgaben/Seiteneffekte:** Zeilenbasierter Vergleich, Blockgruppierung (`DiffBlock`/`DiffLine`), Berechnung von `AddedLines`/`RemovedLines`/`ModifiedLines`.

6. **Persistierung des Ergebnisses**
   - **Code:** `DiffService.GenerateDiffAsync`
   - **Eingaben:** Algorithmusresultat.
   - **Ausgaben/Seiteneffekte:** `DiffResult` wird mit `Status = Generated` gespeichert (`_db.DiffResults.Add` + `SaveChangesAsync`); Inline-Inhalte werden nur bis 100 KB (`MaxInlineContentSize`) persistiert.

7. **Cache-Schreiben als Best-Effort**
   - **Code:** `DiffService.GenerateDiffAsync` + `DiffCachingService.SetInCacheAsync`
   - **Eingaben:** generiertes `DiffResult` + Cache-Key-Parameter.
   - **Ausgaben/Seiteneffekte:** Asynchrones Caching (Memory + `DiffCaches` mit 24h TTL); Cache-Fehler werden geloggt und unterbrechen den Hauptfluss nicht.

8. **Controller-Response und Mapping**
   - **Code:** `DiffController.MapToDto`, `DiffController.GenerateDiffAsync`
   - **Eingaben:** `DiffResult` aus Service.
   - **Ausgaben/Seiteneffekte:** Mapping nach `DiffResultDto` mit Block-/Zeilenstruktur; `200 OK`.

9. **Management-Endpunkte derselben Pipeline**
   - **Code:** `src/Softwareschmiede/Controllers/DiffController.cs` (`GetDiffAsync`, `ListDiffsAsync`, `GetStatisticsAsync`, `DeleteDiffAsync`, `InvalidateCacheAsync`) und `DiffService`-Methoden (`GetDiffAsync`, `SearchDiffsAsync`, `GetDiffCountAsync`, `GetStatisticsAsync`, `DeleteDiffAsync`, `InvalidateDiffCacheAsync`)
   - **Eingaben:** `diffResultId`, `aufgabeId`, Pagination.
   - **Ausgaben/Seiteneffekte:** Lesen, Suchen, Statistiken, Löschen und Cache-Invalidierung auf bereits persistierten Diff-Daten.

10. **Testabdeckung der Diff-Services**
    - **Code:** `src/Softwareschmiede.Tests/Application/Services/DiffAlgorithmServiceTests.cs`, `src/Softwareschmiede.Tests/Application/Services/DiffCachingServiceTests.cs`, `src/Softwareschmiede.Tests/Application/Services/DiffServiceTests.cs`
    - **Eingaben:** Unit-Testdaten für gültige/ungültige Inhalte, Cache-Expiry, Invalidation, Persistierung und Statistiken.
    - **Ausgaben/Seiteneffekte:** Verifiziert Validierungen, Block-/Line-Status, Error-Persistierung (`DiffResultStatus.Error`), Inline-Content-Limit und Cache-Cleanup-Verhalten.

---

## Fehlerbehandlung

- **Leere Inhalte im Request**
  - Pfad: `DiffController.GenerateDiffAsync`
  - Behandlung: `400 BadRequest` mit `ProblemDetails` (`"Quell- und Zielinhalt dürfen nicht leer sein."`).

- **Ungültige Service-Parameter (Pfad/Versionen)**
  - Pfad: `DiffService.GenerateDiffAsync`
  - Behandlung: `ArgumentException` (wird im Controller als generischer Fehler zu `500` behandelt).

- **Aufgabe nicht gefunden**
  - Pfad: `DiffService.GenerateDiffAsync` (`_db.Aufgaben.FindAsync`)
  - Behandlung: `InvalidOperationException`; Controller gibt `500` mit generischem Fehlertext zurück.

- **Algorithmusfehler bei ungültigem Content**
  - Pfad: `DiffAlgorithmService.GenerateDiffAsync` → Catch in `DiffService.GenerateDiffAsync`
  - Behandlung: Exception wird weitergeworfen, aber vorher wird `DiffResult` mit `Status = Error` persistiert.

- **Cache-Deserialisierung oder DB-Fehler**
  - Pfad: `DiffCachingService.GetFromCacheAsync` / `SetInCacheAsync`
  - Behandlung: Fehler werden geloggt; Rückfall auf Nicht-Cache-Pfad, Hauptablauf bleibt funktionsfähig.

- **Nicht vorhandener Diff bei Read/Delete/Invalidate**
  - Pfad: `DiffController.GetDiffAsync`, `DeleteDiffAsync`, `InvalidateCacheAsync`
  - Behandlung: `404 NotFound` mit `ProblemDetails`.

---

## Abhängigkeiten

- `src/Softwareschmiede/Controllers/DiffController.cs`
- `src/Softwareschmiede/Application/Services/DiffService.cs`
- `src/Softwareschmiede/Application/Services/DiffAlgorithmService.cs`
- `src/Softwareschmiede/Application/Services/DiffCachingService.cs`
- `src/Softwareschmiede/Domain/Entities/DiffResult.cs`
- `src/Softwareschmiede/Domain/Entities/DiffBlock.cs`
- `src/Softwareschmiede/Domain/Entities/DiffLine.cs`
- `src/Softwareschmiede/Domain/Entities/DiffCache.cs`
- `src/Softwareschmiede.Tests/Application/Services/DiffServiceTests.cs`
- `src/Softwareschmiede.Tests/Application/Services/DiffAlgorithmServiceTests.cs`
- `src/Softwareschmiede.Tests/Application/Services/DiffCachingServiceTests.cs`

> Verwandte Dokumentation: [API-Doku Diff-Endpunkte](../api/diff.md) · [HTTP-Endpunkte-Übersicht](../api/http-endpoints.md)
