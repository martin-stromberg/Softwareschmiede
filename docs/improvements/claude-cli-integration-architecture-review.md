# Architecture Review – Claude-CLI-Integration (Aufruf-Fix & Session-Wiederverwendung)

## Review-Ziel
Prüfung, ob die implementierte Claude-CLI-Integration robust, konsistent und ausreichend dokumentiert ist.

## Review-Basis
- Code:
  - `plugins/Softwareschmiede.Plugin.ClaudeCli/ClaudeCliPlugin.cs`
  - `src/Softwareschmiede.Tests/Infrastructure/Plugins/ClaudeCliPluginTests.cs`
- Verknüpfte Doku:
  - Requirements / Blueprint / ERM / Testplan / Testlücken / Lifecycle

## Findings

### F1 – Session-Wiederverwendung robust umgesetzt (**Status: akzeptiert**)
- Erstlauf/Folgeaufruf trennen korrekt `-n` und `-r`.
- Wiederverwendung pro Repository und bevorzugte Kontextermittlung aus `*.claude.context.md` vorhanden.

### F2 – Recovery bei Session-Verlust vorhanden (**Status: akzeptiert**)
- Marker `session not found` löst kontrollierten Fallback auf Erstlauf aus.
- Testabdeckung für diesen Pfad ist vorhanden.

### F3 – Aufruf-Fix für große Prompts vorhanden (**Status: akzeptiert**)
- Pipe-basierter stdin-Pfad (`powershell`/`sh`) reduziert Risiken bei langen Argumentlisten.

### F4 – Doku-Konsistenz war unvollständig (**Status: behoben in diesem Lauf**)
- Feature-spezifische Requirements-/Architektur-/ERM-/Review-Dokumente wurden ergänzt.
- Lifecycle-Report und Querverweise wurden auf aktuellen Stand gebracht.

## Empfehlungen
- Optional: dedizierte Metrik/Tracing für Fallback-Häufigkeit (`session not found`) aufnehmen.
- Optional: Grenzwert-/Konfigurierbarkeit der Prompt-Size-Schwelle dokumentieren, falls künftig parametrierbar.

## Ergebnis
Die Architektur für das Feature ist für den aktuellen Scope stimmig und testseitig abgesichert; kritische offenen Punkte bestehen nicht.

## Verknüpfte Artefakte
- [Requirements Analysis](../requirements/claude-cli-integration-requirements-analysis.md)
- [Architektur-Blueprint](../architecture/claude-cli-integration-architecture-blueprint.md)
- [Entity-Relationship-Model](../architecture/claude-cli-integration-entity-relationship-model.md)
- [Testplan](../tests/testplan-claude-cli-integration.md)
- [Testlücken](../tests/testluecken-claude-cli-integration.md)
- [Lifecycle Report](../lifecycle-report-claude-cli-integration.md)
