# Lifecycle Report: Aufgabe-Status-Recovery

## Planung
- Anforderungen und Akzeptanzkriterien wurden in `docs/requirements/aufgabe-status-recovery-requirements-analysis.md` und ergänzend in `docs/requirements/aufgabe-recovery-wiederherstellung-requirements-analysis.md` dokumentiert.
- Architektur, ERM und Review wurden in `docs/architecture/aufgabe-status-recovery-architecture-blueprint.md`, `docs/architecture/aufgabe-status-recovery-entity-relationship-model.md`, `docs/improvements/aufgabe-status-recovery-architecture-review.md` sowie ergänzend in `docs/architecture/aufgabe-recovery-wiederherstellung-architecture-blueprint.md` und `docs/improvements/aufgabe-recovery-wiederherstellung-architecture-review.md` ausgearbeitet.

## Umsetzung
- Eine manuelle Recovery-Funktion für festhängende Aufgaben wurde implementiert.
- In `AufgabeDetail` gibt es die Aktion „Aufgabe wiederherstellen“ für geeignete Zustände (`KiAktiv`, `TestsLaufen`).
- Recovery ist nur möglich, wenn für die betroffene Aufgabe keine Verarbeitung läuft; bei Erfolg erfolgt Statuswechsel nach `InBearbeitung` plus Audit-Log.
- Technische Absicherung via aufgabenspezifischer Laufprüfung und Optimistic Concurrency (`RecoveryVersion`) wurde integriert.

## Tests
- Unit-, Integrations- und UI-Tests für das Recovery-Verhalten wurden ergänzt/erweitert.
- Abgedeckt sind u. a. gültige/ungültige Zustände, laufende Verarbeitung als Sperre, erfolgreiche Wiederherstellung, Audit-Logging und Concurrency-Konflikte.

## Dokumentation
- API-/Flow-/Business-/User-Dokumentation wurde ergänzt bzw. aktualisiert:
  - `docs/api/aufgabe-recovery.md`
  - `docs/flows/aufgabe-recovery-flow.md`
  - `docs/business/features/F016-fehlerbehandlung-und-recovery.md`
  - `docs/business/features/F003-ki-entwicklungsprozess.md`
  - `docs/user-guide.md`
  - zusätzlich aktualisierte Übersichten in `docs/api/README.md` und `docs/flows/README.md`

## Offene Punkte / Hinweise
- Im Gesamt-Testlauf bestehen weiterhin bereits bekannte, nicht feature-spezifische Fehler im `GitHubCopilotPlugin`-Bereich.
- Die Recovery-Dokumentation wurde unter einem zweiten Namensschema ergänzt (`aufgabe-recovery-wiederherstellung-*`); bei Gelegenheit kann eine Konsolidierung der Dokumentnamen erfolgen.
