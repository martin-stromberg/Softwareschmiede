# Testplan – Claude-CLI-Integration (Aufruf-Fix & Session-Wiederverwendung)

## Ziel
Absicherung der Claude-CLI-Integration gegen Regressionen mit Fokus auf:
- stabile CLI-Aufrufe,
- Session-Wiederverwendung (`-n`/`-r`),
- Fallback bei `session not found`,
- Large-Prompt-Pipe-Weg,
- Token-Weitergabe als `ANTHROPIC_API_KEY`.

## Ausgangsbasis
- Implementierung: `plugins/Softwareschmiede.Plugin.ClaudeCli/ClaudeCliPlugin.cs`
- Testklasse: `src/Softwareschmiede.Tests/Infrastructure/Plugins/ClaudeCliPluginTests.cs`
- Lückenliste: `docs/tests/testluecken-claude-cli-integration.md`

## Testumfang

### 1) Session-Wiederverwendung
- `StartDevelopmentAsync_ShouldUseResumeArguments_OnFollowUpRun`
- `StartDevelopmentAsync_ShouldReuseGeneratedSessionId_WhenNoContextFileExists`

**Erwartung:** Erstlauf nutzt `-n`; Folgeaufruf nutzt `-r` mit derselben `taskId`.

### 2) Fallback bei Sitzungsverlust
- `StartDevelopmentAsync_ShouldFallbackToFirstRun_WhenSessionIsMissing`

**Erwartung:** Bei `session not found` wird derselbe Task auf Erstlauf zurückgesetzt und erneut gestartet.

### 3) Aufruf-Fix für große Prompts
- `StartDevelopmentAsync_ShouldUseResumeArgs_WithCommandWrapper_WhenLargePromptOnFollowUp`
- `StartDevelopmentAsync_ShouldUsePowerShellPipe_WhenPromptIsLarge`

**Erwartung:** Große Prompts laufen über `powershell`/`sh`-Pipe statt Inline-Argument.

### 4) Token- und CLI-Verhalten
- `StartDevelopmentAsync_ShouldUseClaudeCommandAndProviderTaskFile`
- `CheckHealthAsync_ShouldReturnTrue_WhenClaudeVersionSucceeds`
- `CheckHealthAsync_ShouldReturnFalse_WhenClaudeVersionFails`

**Erwartung:** Token wird als `ANTHROPIC_API_KEY` übergeben; Health-Check-Verhalten ist deterministisch.

### 5) Paket-/Agentenverhalten
- `GetAvailableAgentsAsync_*` (Discovery in `.claude/commands`)
- `IsAgentPackageCompatibleAsync_*` (Kompatibilität über `.claude/commands`)
- `DeployAgentPackageAsync_*` (Deploy von `.claude`)

## Ausführung
1. Selektiver Lauf:
   - `dotnet test .\src\Softwareschmiede.Tests\Softwareschmiede.Tests.csproj --filter "ClaudeCliPluginTests"`
2. Regression im Gesamtsystem:
   - `dotnet test .\Softwareschmiede.slnx`

## Abnahmekriterien
- Alle oben genannten Claude-Tests bestehen.
- In `testluecken-claude-cli-integration.md` sind keine priorisierten offenen Punkte.
- Dokumentation und Implementierung sind konsistent (Requirements/Architecture/Flow/Lifecycle).

## Verknüpfte Artefakte
- [Requirements Analysis](../requirements/claude-cli-integration-requirements-analysis.md)
- [Architektur-Blueprint](../architecture/claude-cli-integration-architecture-blueprint.md)
- [Entity-Relationship-Model](../architecture/claude-cli-integration-entity-relationship-model.md)
- [Architecture-Review](../improvements/claude-cli-integration-architecture-review.md)
- [Planungsübersicht](../planning-overview-claude-cli-integration.md)
- [Lifecycle Report](../lifecycle-report-claude-cli-integration.md)
