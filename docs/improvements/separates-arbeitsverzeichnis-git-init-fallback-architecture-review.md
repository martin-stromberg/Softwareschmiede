# Architektur-Review – Separates Arbeitsverzeichnis mit git-init-/Copy-Fallback

> **Dokument-Typ:** Architecture Review  
> **Status:** ✅ Umgesetzt  
> **Version:** 1.0.0  
> **Datum:** 2026-05-13

---

## 1. Executive Summary

Der geplante Ansatz ist fachlich korrekt und behebt den bekannten Clone-Fehler bei lokalen Nicht-Git-Quellen.  
Für eine belastbare Umsetzung müssen jedoch vorab Entscheidungslogik, Quellmutationsregeln und Fehler-/Cleanup-Verträge eindeutig festgelegt werden.

## 2. Positiv bewertete Entscheidungen

- Vorab-Git-Check verhindert den bisherigen Fatal-Fehler.
- Explizite Steuerung von `git init` über Settings.
- Copy-Fallback ermöglicht robusten Start auch ohne Git-Metadaten.
- Bestehende Git-Quellen bleiben rückwärtskompatibel.

## 3. Findings (priorisiert)

| ID | Priorität | Finding | Empfehlung |
|---|---|---|---|
| R-01 | Blocker | Entscheidungsmatrix nicht final formalisiert | Verbindliche Matrix + Tests je Zweig festschreiben |
| R-02 | Blocker | Regeln zur Quellmutation durch `git init` nicht präzise genug | Opt-in-Vertrag inkl. Nutzer-/Log-Transparenz fixieren |
| R-03 | Major | Race-Risiko bei parallelen Starts auf dieselbe Quelle | Pfadbezogene Synchronisation vorsehen |
| R-04 | Major | Cleanup-/Rollback-Vertrag bei Teilfehlern unvollständig | Atomare Zielvorbereitung und Cleanup-Policy definieren |
| R-05 | Major | Fehlertaxonomie noch nicht konsolidiert | Domänenfehlercodes + UX-Messages standardisieren |
| R-06 | Minor | Observability-Ziele nicht messbar definiert | Metriken/SLIs für Erfolgsrate und Pfadverteilung ergänzen |

## 4. Risikoanalyse

- **Betrieb:** Mehr Entscheidungszweige erhöhen Komplexität.
- **Sicherheit/Datenintegrität:** `git init` verändert Quelle; muss strikt kontrolliert werden.
- **Stabilität:** Teilfehler können Zielzustände verschmutzen, wenn Cleanup unvollständig ist.
- **UX/Support:** Ohne klare Fehlercodes bleibt Ursachenanalyse teuer.

## 5. Verbesserungsplan

### P0 (vor Umsetzung)
1. Entscheidungsmatrix als verbindliches Artefakt fixieren.
2. Quellmutationsregel (`git init`) als harte Opt-in-Policy dokumentieren.
3. Cleanup- und Idempotenzregeln formalisieren.

### P1 (während Umsetzung)
4. Fehlercodes und Meldungstexte je Pfad standardisieren.
5. Concurrency-Absicherung und Negativtests ergänzen.

### P2 (vor Release)
6. Telemetrie: Pfadverteilung (`clone/init+clone/copy`), Fehlergründe, Laufzeiten.

## 6. Prüfkatalog / Abnahmekriterien

- [x] Alle Matrix-Zweige sind in Unit-/Integrationstests abgedeckt.
- [x] Nicht-Git + init aktiv führt deterministisch zu `init+clone`.
- [x] Nicht-Git + init deaktiviert führt deterministisch zu Copy-Fallback ohne Clone-Versuch.
- [x] Teilfehler hinterlassen keine unkontrollierten Artefakte im Ziel.
- [x] Logs enthalten stets Pfadentscheidung und Reason-Code.
- [x] Bereits git-basierte Quellen verhalten sich regressionsfrei.

## 7. Verlinkung

- Anforderungen: [../requirements/separates-arbeitsverzeichnis-git-init-fallback-requirements-analysis.md](../requirements/separates-arbeitsverzeichnis-git-init-fallback-requirements-analysis.md)
- Architektur: [../architecture/separates-arbeitsverzeichnis-git-init-fallback-architecture-blueprint.md](../architecture/separates-arbeitsverzeichnis-git-init-fallback-architecture-blueprint.md)
- ERM: [../architecture/separates-arbeitsverzeichnis-git-init-fallback-entity-relationship-model.md](../architecture/separates-arbeitsverzeichnis-git-init-fallback-entity-relationship-model.md)
