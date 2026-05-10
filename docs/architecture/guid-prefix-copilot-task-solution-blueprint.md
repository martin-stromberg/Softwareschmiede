# Architektur-Blueprint: GUID-Präfix-Lösung für `.copilot-task`

## 1. Architekturentscheidung
**Entscheidung:** Hybrid-Ansatz mit optionaler `executionId` und Fallback-Generierung.  
**Format:** `{executionId}.copilot-task.md` mit GUID-Format `N` (32 Hex).  
**Kompatibilität:** Bestandsaufrufe ohne `executionId` bleiben funktionsfähig.

## 2. Betroffene Komponenten
| Komponente | Änderung |
|---|---|
| `IKiPlugin` | Optionaler Parameter `executionId` in `StartDevelopmentAsync` |
| `GitHubCopilotPlugin` | Normalisierung/Validierung, dynamischer Dateiname, Cleanup-Finally |
| `.gitignore`-Sync | Zielregel `*.copilot-task.md`, Legacy-Konsolidierung |
| CLI-Argumentbau | `--prompt @<dynamic-task-file>` |
| Aufrufkette Service → Plugin | Optionale Durchreichung der `executionId` |

## 3. Zielfluss (Soll)
1. Eingang `executionId?`
2. `NormalizeAndValidateExecutionId`:
   - null/leer → `Guid.NewGuid().ToString("N")`
   - GUID (D/B/P/N) → Parse + `ToString("N")`
   - invalid → `ArgumentException`
3. Task-Datei schreiben: `{executionId}.copilot-task.md`
4. `.gitignore` idempotent auf `*.copilot-task.md` synchronisieren
5. Copilot CLI mit `--prompt @<taskFile>`
6. Cleanup in `finally`

## 4. Fehlerbehandlung
| Fehlerfall | Verhalten |
|---|---|
| Ungültige `executionId` | Fail-fast vor Datei/CLI |
| Task-Datei nicht schreibbar | Fehlerabbruch (`IOException`) |
| `.gitignore`-Sync fehlschlägt | Retry und danach Fehlerabbruch |
| Cleanup fehlschlägt | Warning, Hauptfluss bleibt maßgeblich |

## 5. Logging/Tracing
Pflichtfelder pro Kernlog:  
`ExecutionId`, `AgentName`, `RepoPath`, `TaskFilePath`, `Step`, `Result`, `DurationMs`.

Pflichtschritte:
- `validate-id`
- `write-task-file`
- `sync-gitignore`
- `invoke-cli`
- `cleanup`

## 6. Kompatibilitäts- und Migrationsstrategie
1. API um optionalen Parameter erweitern.
2. Aufrufer schrittweise auf named arguments (`ct:`) härten.
3. `.gitignore` von Legacy-Regeln auf wildcard konsolidieren.
4. Regressionstests auf Altaufrufe und neue Aufrufe.

## 7. Technische Maßnahmen (Implementierungsreif)
- Neue Methode: `NormalizeAndValidateExecutionId(string? executionId): string`
- Neue Methode: `CleanupTaskFileAsync(string path, string executionId, CancellationToken ct)`
- Anpassung `StartDevelopmentAsync` Signatur + Ablauf
- Anpassung `.gitignore` Sync (Legacy-Regel-Erkennung/Konsolidierung)
- Erweiterung Unit-/Integrationtests für Positiv- und Negativpfade

## 8. Teststrategie
### Unit
- ID-Normalisierung und Validierung
- Dateinamensformat
- `.gitignore`-Idempotenz/Migration
- Cleanup-Verhalten

### Integration
- Lauf mit expliziter `executionId`
- Lauf ohne `executionId` (Fallback)
- Fehlerpfade: ungültige ID, IO-Fehler, Cleanup-Warnung
- CLI-Argument enthält dynamische Prompt-Datei

## 9. Qualitätsziele
- 100% Backward-Compatibility für Aufrufe ohne `executionId`
- Keine doppelte `.gitignore`-Regel
- Vollständige Ausführungs-Korrelation über `ExecutionId`
- Cleanup garantiert unabhängig vom CLI-Ausgang

## 10. Traceability
- Anforderungen: `../requirements/copilot-task-guid-prefix-requirements-analysis.md`
- ERM: `./copilot-task-guid-prefix-entity-relationship-model.md`
- Review: `../improvements/copilot-task-guid-prefix-architecture-review.md`
- Quell-Blueprint: `../../f4c23b69-ba69-467f-9d51-cafd20cdfdcf.copilot-task.md`
