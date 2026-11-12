# Offene Aufgaben

Erstellt am: 2026-11-12
Abbruchgrund: Kein Fortschritt zwischen den letzten zwei Iterationen — der verbleibende offene Punkt ist vor dem Merge strukturell nicht lösbar (kein Implementierungsdefizit).

Die folgende Aufgabe konnte im automatisierten Zyklus nicht abgeschlossen werden
und muss im Rollout (nach Merge/Promotion nach `main` + Secret-Einrichtung)
nachgeholt werden.

## Offene Planelemente

Keine.

## Code-Review-Befunde

Keine (`review-code.md`: Keine Befunde; Iteration-1-Befund behoben, siehe `review-code.1.md`).

## Usability-Befunde

Keine.

## Fehlgeschlagene Tests

- [ ] Reale End-to-End-Abnahme der Akzeptanzszenarien 1–5 — vor Merge nicht durchführbar (Workflow-Datei muss auf `main` liegen; Secret `REVIEW_ISSUE_TOKEN` noch nicht hinterlegt; Test-Issue hätte produktive Wirkung). Auflösung: Post-Merge-Probelauf je Workflow via `workflow_dispatch` gemäß `plan.md` Rollout-Voraussetzungen + `docs/CI_CD.md`; Entscheidung (manuelle Abnahme / dokumentierter Verzicht) trifft der Anwender im PR-Review.
