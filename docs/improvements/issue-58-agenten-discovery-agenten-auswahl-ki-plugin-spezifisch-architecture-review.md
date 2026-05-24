# Architektur-Review – Issue 58: Agenten-Discovery und Agenten-Auswahl KI-Plugin-spezifisch

> **Dokument-Typ:** Feature-spezifisches Architektur-Review  
> **Projekt:** Softwareschmiede  
> **Reviewte Unterlagen:**  
> - [../requirements/issue-58-agenten-discovery-agenten-auswahl-ki-plugin-spezifisch-requirements-analysis.md](../requirements/issue-58-agenten-discovery-agenten-auswahl-ki-plugin-spezifisch-requirements-analysis.md)  
> - [../architecture/issue-58-agenten-discovery-agenten-auswahl-ki-plugin-spezifisch-architecture-blueprint.md](../architecture/issue-58-agenten-discovery-agenten-auswahl-ki-plugin-spezifisch-architecture-blueprint.md)  
> - [../architecture/issue-58-agenten-discovery-agenten-auswahl-ki-plugin-spezifisch-entity-relationship-model.md](../architecture/issue-58-agenten-discovery-agenten-auswahl-ki-plugin-spezifisch-entity-relationship-model.md)  
> **Datum:** 2026-05-24

---

## 1) Executive Summary

Die Zielarchitektur ist tragfähig, wenn der Selektionsflow UI-seitig eindeutig konsolidiert und die No-Compatibility-Fälle hart abgesichert werden.

**Bewertung:** ⚠️ **Freigabe mit Auflagen**

---

## 2) Priorisierte Findings

| ID | Priorität | Finding | Risiko | Maßnahme |
|---|---|---|---|---|
| F-01 | **Blocker** | Potenziell mehrdeutige UI-Selektion (Plugin/Paket/Agent nicht überall identisch geführt) | Inkonsistente Ausführung und schwer reproduzierbare Fehler | Einen gemeinsamen Zustandsflow für Start- und Folgeprompt-UI erzwingen |
| F-02 | **Major** | No-Compatibility-Pfad unzureichend abgesichert | Nutzer kann in ungültigen Zustand gelangen | Harte Disable-Regeln + eindeutige Fehlermeldungen |
| F-03 | **Major** | Legacy-Discovery-Pfade könnten partiell verbleiben | Doppelte Logik, divergierende Ergebnisse | Altpfade vollständig entfernen und Plugin-Discovery zentralisieren |
| F-04 | **Major** | Testabdeckung für Übergangsfälle nicht vollständig | Regressionen bei Refactoring | bUnit-/Integrationsfälle für State-Resets, Fallback und No-Compat ergänzen |
| F-05 | **Minor** | Dokumentationsdrift zwischen Implementierung und Planungsstand möglich | Spätere Entscheidungsunklarheit | Querverweise in Planungsübersicht und Changelog diszipliniert pflegen |

---

## 3) Maßnahmenplan (priorisiert)

1. **Blocker schließen:** UI-Zustandsmodell vereinheitlichen (`Plugin → Paket → Agent`).
2. **Fehlerpfade härten:** Kein Start/Senden bei fehlender Kompatibilität.
3. **Legacy entfernen:** plugin-unabhängige Discovery an einer Stelle rückbauen.
4. **Tests erweitern:** insbesondere Übergang von Alt-Tasks ohne Prefix und Plugin-Wechsel.
5. **Dokumentation synchron halten:** Anforderungen/Blueprint/ERM gemeinsam versionieren.

---

## 4) Freigabeempfehlung

**GO**, wenn folgende Bedingungen erfüllt sind:
- einheitlicher Selektionsflow in allen UI-Pfaden,
- robuste Behandlung fehlender kompatibler Pakete/Agenten,
- vollständige Entfernung der Legacy-Discovery-Pfade,
- grüne Testmatrix für Discovery, Fallback, Persistenz und UI-Zustände.

