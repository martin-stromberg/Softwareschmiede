# Lifecycle Report: Kontextsteuerung bei Folgeanweisungen

## Geplante Ergebnisse
- Requirements: [docs/requirements/kontextsteuerung-folgeanweisungen-requirements-analysis.md](requirements/kontextsteuerung-folgeanweisungen-requirements-analysis.md)
- Architecture Blueprint: [docs/architecture/kontextsteuerung-folgeanweisungen-architecture-blueprint.md](architecture/kontextsteuerung-folgeanweisungen-architecture-blueprint.md)
- ERM: [docs/architecture/kontextsteuerung-folgeanweisungen-entity-relationship-model.md](architecture/kontextsteuerung-folgeanweisungen-entity-relationship-model.md)
- Architecture Review: [docs/improvements/kontextsteuerung-folgeanweisungen-architecture-review.md](improvements/kontextsteuerung-folgeanweisungen-architecture-review.md)
- Planning Overview: [docs/planning-overview-kontextsteuerung-folgeanweisungen.md](planning-overview-kontextsteuerung-folgeanweisungen.md)

## Implementierung
- Einführung eines dedizierten Kontextmodus für Folgeanweisungen mit drei Optionen: **Kontext mitgeben**, **Kontext ignorieren**, **Kontext neu beginnen**.
- Erweiterung der Aufgabe-Detail-UI inklusive Schutzmechanik (Bestätigung) für den Modus **Kontext neu beginnen**.
- Durchgängige Weitergabe des Modus von UI über Ausführungsservice bis zur Prompt-Erzeugung.
- Datei-basierte Kontextpersistenz je Aufgabe (`{aufgabeId}.copilot.context.md`) mit Soft/Hard-Limit-Strategie, KI-Komprimierung, atomischem Schreiben und Backup.
- Audit-/Nachvollziehbarkeits-Erweiterung über korrelierbare IDs (`RunId`, `ContextEventId`).

## Ergänzte Tests
- Erweiterte UI-Tests für Moduswechsel, Guardrails und ungültige Eingaben:
  - `src/Softwareschmiede.Tests/Components/Pages/Aufgaben/AufgabeDetailFolgePromptTests.cs`
- Erweiterte Service-Tests für Kontextdatei- und Komprimierungsfehlerpfade:
  - `src/Softwareschmiede.Tests/Application/Services/EntwicklungsprozessServiceTests.cs`
- Erweiterte Ausführungsservice-Tests für Lifecycle-/Fehlerverhalten:
  - `src/Softwareschmiede.Tests/Application/Services/KiAusfuehrungsServiceTests.cs`
- Testdokumentation:
  - `docs/tests/testluecken-kontextsteuerung-folgeanweisungen.md`
  - `docs/tests/testplan-kontextsteuerung-folgeanweisungen.md`

## Dokumentation
- Neue/aktualisierte API-, Business- und Flow-Dokumentation für die Kontextsteuerung:
  - `docs/flows/follow-up-context-steering-flow.md`
  - `docs/business/features/F012-kontextsteuerung-folgeanweisungen.md`
  - `docs/api/http-endpoints.md`
  - `docs/api/plugin-interfaces.md`
  - `docs/business/features/F003-ki-entwicklungsprozess.md`
  - `docs/documentation-plan.md`
  - sowie ergänzende Aktualisierungen in `README.md`, `docs/api/README.md`, `docs/business/features.md`, `docs/flows/README.md`.

## Offene Punkte / Hinweise
- In der Gesamtsuite besteht weiterhin ein vorbestehender, feature-unabhängiger Fehler in den `GitHubCopilotPluginTests` (Prompt-Datei/CLI-Übergabe).
- Ein dedizierter automatisierter Nachweis für `.bak`-Fallback bei Kontextdatei-Lesefehlern bleibt als verbleibendes Restrisiko markiert.
