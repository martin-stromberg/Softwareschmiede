# Anforderungsanalyse: GUID-Präfix für `.copilot-task.md`

## 1. Zielbild und Problem
- Ist: Task-Dateiname ist im Plugin auf `.copilot-task.md` fixiert.
- Ist: In der Praxis entstehen Dateien als `{GUID}.copilot-task.md`.
- Folge: Regel `/.copilot-task.md` ignoriert GUID-Dateien nicht zuverlässig.
- Ziel: Hybrid-Lösung mit optionaler `executionId`, Fallback-Generierung, wildcard-basierter `.gitignore`-Regel und vollständiger Nachvollziehbarkeit.

## 2. Scope
### In Scope
- Erweiterung `IKiPlugin.StartDevelopmentAsync(..., string? executionId = null, ...)`
- Normalisierung/Validierung von `executionId` auf GUID-Format `N`
- Dateiformat `{executionId}.copilot-task.md`
- `.gitignore`-Zielregel `*.copilot-task.md` inkl. Legacy-Konsolidierung
- Logging/Tracing und robuste Fehlerbehandlung inkl. Cleanup
- Unit- und Integrationstestanforderungen

### Out of Scope
- Änderung der Copilot-CLI selbst
- Globale Prozesssperren für konkurrierende `.gitignore`-Änderungen über Prozessgrenzen hinweg
- Persistenz neuer Datenbankobjekte

## 3. Funktionale Anforderungen
| ID | Anforderung | Priorität |
|---|---|---|
| FR-1 | `StartDevelopmentAsync` akzeptiert optional `executionId`. | MUST |
| FR-2 | Bei leerer/fehlender `executionId` wird eine neue GUID generiert. | MUST |
| FR-3 | Gültige GUID-Eingaben werden auf Format `N` normalisiert. | MUST |
| FR-4 | Ungültige `executionId` führt zu klarer `ArgumentException` vor Dateischreib-/CLI-Schritt. | MUST |
| FR-5 | Task-Datei wird als `{executionId}.copilot-task.md` erzeugt. | MUST |
| FR-6 | `.gitignore` wird idempotent auf `*.copilot-task.md` synchronisiert; alte Regeln (`/.copilot-task.md`, `.copilot-task.md`) werden konsolidiert. | MUST |
| FR-7 | CLI-Aufruf nutzt den dynamischen Prompt-Pfad `--prompt @<taskFile>`. | MUST |
| FR-8 | Cleanup erfolgt garantiert im `finally`; Fehler im Cleanup sind non-blocking Warning. | MUST |
| FR-9 | Logs enthalten `ExecutionId` über alle Prozessschritte. | MUST |

## 4. Nicht-funktionale Anforderungen
| ID | Anforderung |
|---|---|
| NFR-1 | Backward-Compatibility: bestehende Aufrufer ohne `executionId` funktionieren unverändert. |
| NFR-2 | Idempotenz: wiederholte Läufe erzeugen keine duplizierten `.gitignore`-Regeln. |
| NFR-3 | Observability: strukturierte Logs mit Schritt, Ergebnis, ExecutionId, Dauer. |
| NFR-4 | Fehlertoleranz: Cleanup-Fehler überschreiben Hauptfehler nicht. |
| NFR-5 | Testbarkeit: Positiv- und Negativpfade sind automatisiert prüfbar. |

## 5. Akzeptanzkriterien
### AC-1: Lauf ohne executionId (Fallback)
**Given** kein `executionId`-Parameter  
**When** `StartDevelopmentAsync` startet  
**Then** wird eine GUID im Format `N` generiert und als Dateipräfix verwendet.

### AC-2: Lauf mit executionId im D-Format
**Given** `executionId = 8934d257-5588-473e-9882-9b19d322851b`  
**When** Validierung/Normalisierung erfolgt  
**Then** wird `8934d2575588473e98829b19d322851b.copilot-task.md` verwendet.

### AC-3: Ungültige executionId
**Given** `executionId = "invalid-format"`  
**When** Start aufgerufen wird  
**Then** wird vor Dateischreiben/CLI-Aufruf eine klare Exception geworfen und ein Fehler geloggt.

### AC-4: .gitignore-Migration
**Given** `.gitignore` enthält `/.copilot-task.md`  
**When** der neue Lauf startet  
**Then** enthält `.gitignore` danach genau eine Regel `*.copilot-task.md`.

### AC-5: Idempotenz
**Given** `.gitignore` enthält bereits `*.copilot-task.md`  
**When** mehrere Läufe stattfinden  
**Then** entsteht keine doppelte Regel.

### AC-6: Cleanup-Garantie
**Given** CLI-Lauf endet erfolgreich oder fehlerhaft  
**When** `StartDevelopmentAsync` abgeschlossen wird  
**Then** wird Task-Datei im `finally` gelöscht; Cleanup-Fehler nur als Warning.

## 6. Logging- und Fehlerbehandlungsanforderungen
- Pflicht-Logfelder: `ExecutionId`, `AgentName`, `RepoPath`, `TaskFilePath`, `Step`, `Result`.
- Pflichtschritte: `validate-id`, `write-task-file`, `sync-gitignore`, `invoke-cli`, `cleanup`.
- Fehlerklassen:
  - Validierung (`ArgumentException`)
  - I/O bei Taskfile/GitIgnore (`IOException`)
  - Cleanup (`Warning`, non-blocking)

## 7. Migration und Kompatibilität
1. API optional erweitern (`executionId = null`).
2. Interne Berechnung auf `{executionId}.copilot-task.md` umstellen.
3. `.gitignore`-Konsolidierung auf wildcard.
4. Bestehende Caller ohne `executionId` unverändert betreiben.

## 8. Testanforderungen
### Unit
- `NormalizeAndValidateExecutionId`: null/leer, GUID-D, GUID-N, invalid.
- Dateinamensbildung.
- `.gitignore`-Konsolidierung + Idempotenz.
- Cleanup-Verhalten.

### Integration
- End-to-End-Lauf mit/ohne `executionId`.
- CLI-Argument enthält richtigen dynamischen Prompt-Dateipfad.
- Fehlerpfade: Schreibfehler, Locking/Retry, Cleanup-Fehler.

## 9. Traceability
- Architektur: `../architecture/guid-prefix-copilot-task-solution-blueprint.md`
- ERM: `../architecture/copilot-task-guid-prefix-entity-relationship-model.md`
- Review: `../improvements/copilot-task-guid-prefix-architecture-review.md`
- Quell-Blueprint: `../../f4c23b69-ba69-467f-9d51-cafd20cdfdcf.copilot-task.md`
