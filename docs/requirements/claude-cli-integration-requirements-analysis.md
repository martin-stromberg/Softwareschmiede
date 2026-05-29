# Requirements Analysis – Claude-CLI-Integration (Aufruf-Fix & Session-Wiederverwendung)

## Ziel
Die Claude-CLI-Integration soll KI-Läufe in der Softwareschmiede stabil starten und Folgeanweisungen in derselben Sitzung wiederverwenden. Das umfasst den Aufruf-Fix für große Prompts sowie einen robusten Fallback bei verlorener Claude-Session.

## Scope
- `ClaudeCliPlugin` in `plugins/Softwareschmiede.Plugin.ClaudeCli/ClaudeCliPlugin.cs`
- Session-/Prompt-Verhalten in `StartDevelopmentAsync`
- Token-Nutzung über `Softwareschmiede.ClaudeCli.Token` -> `ANTHROPIC_API_KEY`
- Testabdeckung in `src/Softwareschmiede.Tests/Infrastructure/Plugins/ClaudeCliPluginTests.cs`

## Funktionale Anforderungen

### FR-1: Session-Wiederverwendung je Repository
- Das Plugin muss eine `taskId` je Repository wiederverwenden.
- Falls eine aktuelle Kontextdatei `*.claude.context.md` vorhanden ist, muss deren GUID bevorzugt werden.

### FR-2: Erstlauf/Folgeanweisung unterscheiden
- Erstlauf muss mit `-n <taskId>` gestartet werden.
- Folgeanweisung muss mit `-r <taskId> -p` ausgeführt werden.

### FR-3: Fallback bei verlorener Session
- Wenn ein Follow-up den Marker `session not found` liefert, muss das Plugin automatisch auf einen Erstlauf mit gleicher `taskId` zurückfallen.

### FR-4: Aufruf-Fix für große Prompts
- Große Prompts (> 8 KB) dürfen nicht als Inline-Argument übergeben werden.
- Stattdessen muss stdin-Piping genutzt werden:
  - Windows: `powershell`
  - Unix: `sh`

### FR-5: Token-Weitergabe
- Das gespeicherte Secret `Softwareschmiede.ClaudeCli.Token` muss als `ANTHROPIC_API_KEY` an den CLI-Prozess übergeben werden, wenn vorhanden.

## Nicht-funktionale Anforderungen
- **Robustheit:** Kein Hard-Fail bei Session-Verlust; kontrollierter Fallback.
- **Nachvollziehbarkeit:** Relevante Session- und Fallback-Schritte müssen im Ablauf nachvollziehbar sein.
- **Kompatibilität:** Agentenpaket-Kompatibilität für Claude bleibt `.claude/commands`.

## Akzeptanzkriterien
- Tests für `-n`/`-r`-Verhalten sind grün.
- Testfall „session not found“ validiert den automatischen Fallback.
- Testfall für große Prompts validiert den Pipe-Weg.
- Token-Übergabe an `ANTHROPIC_API_KEY` ist in Tests nachgewiesen.

## Verknüpfte Artefakte
- [Architektur-Blueprint](../architecture/claude-cli-integration-architecture-blueprint.md)
- [Entity-Relationship-Model](../architecture/claude-cli-integration-entity-relationship-model.md)
- [Architecture-Review](../improvements/claude-cli-integration-architecture-review.md)
- [Planungsübersicht](../planning-overview-claude-cli-integration.md)
- [Testplan](../tests/testplan-claude-cli-integration.md)
- [Testlücken](../tests/testluecken-claude-cli-integration.md)
- [Lifecycle Report](../lifecycle-report-claude-cli-integration.md)
