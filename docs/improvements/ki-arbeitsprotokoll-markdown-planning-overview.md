# Planungsübersicht – KI-Arbeitsprotokoll als Markdown

> **Dokument-Typ:** Planungsübersicht (Orchestrator-Ergebnis)  
> **Status:** ✅ Planungsphase abgeschlossen  
> **Datum:** 2026-05-24

## 1. Anlass

Das bisherige KI-Arbeitsprotokoll als Textblock soll strukturiert werden:

- Datumszeile als Markdown-Heading `# {Datum}`
- bessere Trennung einzelner Arbeitsschritte
- formatierte Markdown-Ausgabe in der Webansicht

## 2. Erzeugte/aktualisierte Artefakte

| Dokument | Zweck | Link |
|---|---|---|
| Requirements Analysis | Ziele, Scope, FR/NFR, Akzeptanzkriterien, Domänenmodell | [../requirements/ki-arbeitsprotokoll-markdown-requirements-analysis.md](../requirements/ki-arbeitsprotokoll-markdown-requirements-analysis.md) |
| Architektur-Blueprint | Zielarchitektur, Komponenten, Datenfluss, Qualitätsziele | [../architecture/ki-arbeitsprotokoll-markdown-architecture-blueprint.md](../architecture/ki-arbeitsprotokoll-markdown-architecture-blueprint.md) |
| ERM | Entitäten, Beziehungen, Integritätsregeln | [../architecture/ki-arbeitsprotokoll-markdown-entity-relationship-model.md](../architecture/ki-arbeitsprotokoll-markdown-entity-relationship-model.md) |
| Architektur-Review | Risiken, Schwachstellen, priorisierte Verbesserungen | [./ki-arbeitsprotokoll-markdown-architecture-review.md](./ki-arbeitsprotokoll-markdown-architecture-review.md) |

## 3. Kernentscheidungen

1. Protokollerzeugung bleibt im bestehenden Service, wird aber auf strukturiertes Markdown festgelegt.
2. Datumszeile ist verbindlich als `# yyyy-MM-dd` auszugeben.
3. Schritte werden als `## Schritt n` mit klarer Abschnittstrennung dargestellt.
4. In der UI wird Markdown aktiv gerendert statt als Rohtext angezeigt.
5. Sicherheits- und Fallback-Mechanismen bleiben verpflichtender Teil der Render-Pipeline.

## 4. Priorisierte Folgepunkte aus dem Review

- **Hoch:** Sanitizing-Strategie robuster machen (Whitelist-basierter Ansatz).
- **Hoch:** Schrittsegmentierung semantisch verbessern (nicht rein zeilenbasiert).
- **Mittel:** Monitoring/Benchmarking für Render-Performance ergänzen.
- **Mittel:** Telemetrie für Fallback-/Fehlerfälle erweitern.

## 5. Traceability

Die Artefakte sind gegenseitig verlinkt und decken den vollständigen Ablauf ab:

**Anforderungsanalyse → Architektur-Blueprint → ERM → Architektur-Review**

