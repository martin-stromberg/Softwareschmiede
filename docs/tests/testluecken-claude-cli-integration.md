# Testlücken – Claude-CLI-Integration (Aufruf-Fix & Session-Wiederverwendung)

## Status
Alle priorisierten Testlücken für den aktuellen Feature-Scope sind geschlossen.

## Nachweis (geschlossen)
- [x] Erstlauf/Folgeaufruf unterscheiden korrekt `-n`/`-r`.
- [x] Session-Wiederverwendung je Repository ohne vorhandene Kontextdatei.
- [x] Fallback auf Erstlauf bei `session not found`.
- [x] Große Prompts werden via `powershell`/`sh` Pipe übergeben.
- [x] `ANTHROPIC_API_KEY` wird aus CredentialStore an die CLI-Umgebung übergeben.
- [x] Discovery/Kompatibilität basiert auf `.claude/commands`.
- [x] Deploy kopiert `.claude`-Inhalte rekursiv und überschreibt Zielstände.
- [x] Positiver und negativer Health-Check-Pfad (`claude --version`) sind abgedeckt.

## Offene Restpunkte
- [x] Keine offenen Restpunkte im aktuellen Scope.

## Verknüpfte Artefakte
- [Testplan](./testplan-claude-cli-integration.md)
- [Requirements Analysis](../requirements/claude-cli-integration-requirements-analysis.md)
- [Architektur-Blueprint](../architecture/claude-cli-integration-architecture-blueprint.md)
- [Lifecycle Report](../lifecycle-report-claude-cli-integration.md)
