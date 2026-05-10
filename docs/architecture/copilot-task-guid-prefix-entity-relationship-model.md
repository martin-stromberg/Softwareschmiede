# ERM: GUID-Präfix für Copilot-Task-Dateien

## 1. Modellfokus
Kein neues DB-Schema. Modelliert werden Laufzeit-Entitäten und Zustände:
- `ExecutionContext`
- `ExecutionId`
- `TaskFile`
- `GitIgnoreDocument` / `GitIgnoreRule`
- `CopilotCliInvocation`
- `CleanupAction`

## 2. Entitäten und Beziehungen
| Entität | Attribute (Auszug) | Beziehung |
|---|---|---|
| ExecutionContext | repoPath, prompt, agentName, status | 1:1 zu ExecutionId, TaskFile, CliInvocation |
| ExecutionId | inputRaw, normalizedN, generatedByFallback | 1:1 zu TaskFile |
| TaskFile | fileName, fullPath, created, deleted | 1:0..1 zu CleanupAction |
| GitIgnoreDocument | path, changed, migratedLegacyRule | 1:n zu GitIgnoreRule |
| GitIgnoreRule | rawValue, normalizedValue | Bestandteil GitIgnoreDocument |
| CopilotCliInvocation | args, promptArg, status | 1:1 zu ExecutionContext |
| CleanupAction | result, error | gehört zu TaskFile |

## 3. Zustandsmodell
1. Init
2. ExecutionId validiert/normalisiert
3. Task-Datei geschrieben
4. `.gitignore` synchronisiert
5. CLI gestartet/gestreamt
6. Cleanup ausgeführt
7. Beendet

Fehlerkanten:
- invalid executionId → sofortiger Abbruch
- IO-Fehler bei TaskFile/GitIgnore → Abbruch
- Cleanup-Fehler → Warning, non-blocking

## 4. Invarianten
- `executionId` nach Normalisierung: `^[0-9a-f]{32}$`
- Dateiname immer `{executionId}.copilot-task.md`
- `.gitignore` enthält am Ende genau eine Zielregel `*.copilot-task.md`
- Cleanup wird immer im `finally` angestoßen

## 5. Mapping auf Code
| Modellbaustein | Zielmethode |
|---|---|
| ExecutionId-Normalisierung | `NormalizeAndValidateExecutionId` |
| TaskFile-Erstellung | `StartDevelopmentAsync` |
| GitIgnore-Konsolidierung | `EnsureGitIgnoreRuleAsync` (+ Rule-Normalisierung) |
| CLI-Aufruf | `BuildCopilotArgs` + `_cliRunner.StreamAsync` |
| Cleanup-Lebenszyklus | `CleanupTaskFileAsync` im `finally` |

## 6. Testrelevante Kantenfälle
- `executionId` null/leer
- `executionId` in D-Format
- `executionId` invalid
- Legacy `.gitignore` vorhanden
- `.gitignore` bereits korrekt (Idempotenz)
- CLI-Fehler mit anschließendem Cleanup

## 7. Traceability
- Anforderungen: `../requirements/copilot-task-guid-prefix-requirements-analysis.md`
- Architektur: `./guid-prefix-copilot-task-solution-blueprint.md`
- Review: `../improvements/copilot-task-guid-prefix-architecture-review.md`
