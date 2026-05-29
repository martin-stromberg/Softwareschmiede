# Planning Overview – Claude-CLI-Integration (Aufruf-Fix & Session-Wiederverwendung)

## Zielbild
Stabile Claude-CLI-Ausführung mit:
1. sicherer Token-Weitergabe,
2. konsistenter Session-Wiederverwendung,
3. robustem Fallback bei Session-Verlust,
4. zuverlässigem Verhalten bei großen Prompts.

## Arbeitspakete

### P1 – Session-Management
- Task-ID-Auflösung aus Kontextdatei oder Repo-Session
- Erstlauf/Folgeaufruf-Entscheidung (`-n`/`-r`)

### P2 – Aufruf-Fix
- Inline-Argument nur bei kleinen Prompts
- stdin-Pipe bei > 8 KB (PowerShell/Sh)

### P3 – Fehlerbehandlung
- Erkennung `session not found`
- Automatischer Fallback auf Erstlauf

### P4 – Testabdeckung
- Plugin-Tests für Session-Reuse, Fallback und Large-Prompt-Weg
- Regression gegen bestehende KI-Plugin-Logik

### P5 – Dokumentationssynchronisation
- Business-, API-, Flow-, Requirements-, Architektur- und Lifecycle-Artefakte konsistent verknüpfen

## Abhängigkeiten
- `ICredentialStore` für Token
- `ICliRunner` für Prozess-/Streamingausführung
- `CliKiPluginBase` für provider-spezifische Dateinamen

## Abnahme
- Siehe [Testplan](./tests/testplan-claude-cli-integration.md) und [Testlücken](./tests/testluecken-claude-cli-integration.md).

## Verknüpfte Artefakte
- [Requirements Analysis](./requirements/claude-cli-integration-requirements-analysis.md)
- [Architektur-Blueprint](./architecture/claude-cli-integration-architecture-blueprint.md)
- [Entity-Relationship-Model](./architecture/claude-cli-integration-entity-relationship-model.md)
- [Architecture-Review](./improvements/claude-cli-integration-architecture-review.md)
- [Lifecycle Report](./lifecycle-report-claude-cli-integration.md)
