# Architektur-Review – Separates Arbeitsverzeichnis mit Git-Workflow

> **Dokument-Typ:** Architecture Review  
> **Status:** 🔍 Reviewed (kritische Punkte offen)  
> **Version:** 2.0.0  
> **Datum:** 2026-05-13

---

## 1. Executive Summary

Die 2.0.0-Planungsdokumente adressieren die fachliche Korrektur gegenüber 1.0.0 klar:  
Pull ohne Merge, Push als Copy/Overwrite, Delete-Sync über Git-Erkennung und Legacy-Schutz sind sauber als Zielbild formuliert.

Für eine belastbare Umsetzung fehlen jedoch noch verbindliche technische Verträge in sicherheits- und konsistenzkritischen Punkten (Atomizität, Konflikt-/Parallelitätsverhalten, Scope des Datei-Syncs).  
Ohne diese Präzisierungen besteht hohes Risiko für Datenverlust, nicht-deterministische Ergebnisse und schwer reproduzierbare Supportfälle.

---

## 2. Strukturierte Review-Bewertung

### 2.1 Systemarchitektur (Schichten, Module, Integrationen)
- **Stärken:** klare Trennung `SeparateWorkingDirectory` vs. Legacy; sinnvolle Service-Schnitte (`PullNoMergeService`, `PushSyncService`, `DeleteSyncService`).
- **Schwachpunkt:** kein expliziter Transaktions-/Rollback-Mechanismus für mehrphasigen Push-Flow (Copy + Delete).
- **Bewertung:** **bedingt tragfähig**, aber noch nicht releasefest.

### 2.2 Technologieentscheidungen
- **Stärken:** Git-Status als Quelle für Löschentscheidungen ist grundsätzlich nachvollziehbar.
- **Schwachpunkt:** nicht definiert, welche Git-Statusklassen in den Delta-Algorithmus einfließen (staged/unstaged/renamed/type-changed).
- **Bewertung:** **funktional plausibel**, Spezifikation unvollständig.

### 2.3 UI/UX-Review
- **Stärken:** Pflicht-Hinweis vor Pull ist explizit verankert.
- **Schwachpunkt:** Text/Bestätigungsmechanik/Abbruchpfad nicht normiert; kein Nachweis für 100%-Erzwingung auf allen UI-Einstiegen.
- **Bewertung:** **konzeptionell korrekt**, Ausführungskonsistenz offen.

### 2.4 Qualitätsziele
- **Stärken:** MUST-Ziele sind benannt (kein `git push`, Legacy regressionsfrei, deterministische Sync-Semantik).
- **Schwachpunkt:** mehrere Ziele sind ohne messbare, automatisierbare Orakel formuliert (z. B. „kein teil-synchronisierter Zustand“).
- **Bewertung:** **zielgerichtet**, aber zu wenig operationalisiert.

---

## 3. Priorisierte Findings (Blocker / Major / Minor)

| ID | Priorität | Finding | Betroffene Doku | Konkrete Empfehlung |
|---|---|---|---|---|
| AR-2.0-01 | **Blocker** | Kein atomarer Konsistenzvertrag für Push (Copy + Delete) bei Teilfehlern | Blueprint, Requirements | Zwei-Phasen-Sync mit Journal/Commit-Markierung definieren; bei Fehlern klarer Rollback- oder „safe fail“-Pfad. |
| AR-2.0-02 | **Blocker** | Sync-Scope unklar (`.git`, Symlinks, versteckte Dateien, Dateirechte, Locks) | Blueprint, Requirements | Verbindliche Include/Exclude-Regeln + Sicherheits-Guardrails (Pfadnormalisierung, Root-Boundary, Symlink-Policy) spezifizieren. |
| AR-2.0-03 | **Blocker** | Delete-Sync-Regeln nicht vollständig für Git-Statusvarianten (rename/typechange/staged vs. unstaged) | ERM, Blueprint | Delta-Algorithmus als Tabelle mit Status-Mapping und Testorakeln festschreiben. |
| AR-2.0-04 | **Major** | Parallelitäts-/Race-Verhalten bei mehreren Push/Pull auf denselben Pfaden nicht definiert | Blueprint | Pfadbasierte Sperren (Lock pro SourceDirectory) und Timeout/Retry-Regeln ergänzen. |
| AR-2.0-05 | **Major** | Pull-ohne-Merge fachlich gefordert, aber Upstream-/Konfliktstrategie technisch unpräzise | Requirements, Blueprint | Präzise Pull-Strategie definieren (Quelle der Aktualisierung, Konfliktcode, Verhalten bei lokalen Divergenzen). |
| AR-2.0-06 | **Major** | Legacy-Regressionsfreiheit ist Ziel, aber ohne vollständige Kompatibilitätsmatrix | Requirements, Blueprint | Liste aller Legacy-Kommandopfad-Invarianten + Golden-Regression-Suite ergänzen. |
| AR-2.0-07 | **Minor** | Telemetrie strukturiert, aber ohne KPI-Schwellen und Alerting-Grenzen | Blueprint | SLO/KPI-Set ergänzen (Fehlerrate, Abbruchrate Pull-Hinweis, Median/P95-Sync-Zeit). |
| AR-2.0-08 | **Minor** | `git init`-/Initial-Commit-Policy bzgl. Autor, Commit-Message, Audit-Trail nicht abschließend | Requirements, Blueprint | Minimalkonvention für Commit-Metadaten und Audit-Event verpflichtend machen. |

---

## 4. Risikoanalyse

| Risiko | Eintritt | Impact | Bewertung | Gegenmaßnahme |
|---|---|---|---|---|
| Teil-Sync bei Fehler (Copy fertig, Delete teilweise) | Mittel | Hoch | **Hoch** | Atomarer Ablaufvertrag + Recovery-Testfälle |
| Falsche Löschungen durch unvollständige Git-Statusauswertung | Mittel | Hoch | **Hoch** | Status-Mapping + Schutztests für Rename/Typechange |
| Datenüberschreibung außerhalb erwarteter Pfade | Niedrig-Mittel | Sehr hoch | **Hoch** | Harte Path-Guardrails, Canonicalization, Boundary-Checks |
| UI-Bypass des Pflicht-Hinweises | Mittel | Mittel | **Mittel** | Zentraler Server-/Service-Guard statt rein UI-seitig |
| Regression im Legacy-Flow | Mittel | Hoch | **Hoch** | Vollständige Legacy-Regressionsmatrix vor Release |

---

## 5. Verbesserungsplan (P0 / P1 / P2)

### P0 – vor Implementierungsfreigabe
1. Push-Konsistenzmodell (atomar oder exakt definierter fail-safe Zustand) verbindlich spezifizieren.  
2. Sync-Scope/Sicherheitsgrenzen inkl. `.git`/Symlink/Hidden/Permissions als Normtabelle definieren.  
3. Delete-Sync-Git-Status-Mapping mit eindeutigen Regeln und Negativfällen fixieren.

### P1 – während Umsetzung
4. Locking-Strategie für konkurrierende Operationen implementierbar ausarbeiten (Lock-Key, Timeout, Retry).  
5. Pull-ohne-Merge technisch präzisieren (Quelle, Konfliktbehandlung, Fehlercodes).  
6. Legacy-Kompatibilitätsmatrix in automatisierte Regressionstests überführen.

### P2 – vor Release
7. KPI/SLO und Alert-Schwellen für Observability verbindlich ergänzen.  
8. Auditierbare `git init`-/Commit-Metadaten finalisieren und testen.

---

## 6. Prüfbare Abnahmekriterien (Architecture Exit Criteria)

- [ ] Für Push existiert ein formaler Ablaufvertrag, der Teilfehlerzustände deterministisch behandelt (inkl. Tests für Fehler in Copy- und Delete-Phase).  
- [ ] Eine dokumentierte Sync-Scope-Tabelle legt verbindlich fest, welche Dateitypen/-pfade synchronisiert oder ausgeschlossen werden.  
- [ ] Delete-Sync ist für `deleted`, `renamed`, `typechanged`, staged/unstaged mit automatisierten Tests spezifiziert.  
- [ ] Nachweis: Im Modus `SeparateWorkingDirectory` wird in allen Push-Pfaden **0x** `git push` aufgerufen.  
- [ ] Nachweis: Pull startet ohne bestätigten Hinweis nie (service-seitig erzwungen, nicht nur UI-seitig).  
- [ ] Vollständige Legacy-Regression-Suite für `workingDirectory == sourceDirectory` ist grün und deckt bestehende Kommandopfade ab.  
- [ ] Fehlerereignisse enthalten in 100 % der Fälle `error_class`, `reason_code`, `phase`, `operation_type`, `taskId`.

---

## 7. Verbleibende Unklarheiten in den Planungsdokumenten

1. **Definition „Pull ohne Merge“**: Von welcher Quelle wird aktualisiert, und wie werden lokale divergente Änderungen behandelt?  
2. **Delete-Sync bei Rename**: Soll Rename als Delete+Add gespiegelt werden oder als Move behandelt werden?  
3. **Policy-Grenzen**: Darf `git init` still passieren oder nur mit expliziter Nutzerentscheidung?  
4. **Scope für große Dateien/Binarys**: Performance- und Integritätsstrategie fehlt (Checksums, Streaming, Retry).  
5. **Fehlerklassifikation vs. UX-Text**: Mapping von Reason-Codes zu Nutzertexten nicht normiert.

**Empfehlung:** Diese Punkte als verbindliche ADR-Ergänzungen (2.0.1) mit Testorakel je Regel aufnehmen, bevor Implementierung „done“ gesetzt wird.

---

## 8. Verlinkung (gleicher Basisname)

- Anforderungen: [../requirements/separates-arbeitsverzeichnis-git-init-fallback-requirements-analysis.md](../requirements/separates-arbeitsverzeichnis-git-init-fallback-requirements-analysis.md)
- Architektur-Blueprint: [../architecture/separates-arbeitsverzeichnis-git-init-fallback-architecture-blueprint.md](../architecture/separates-arbeitsverzeichnis-git-init-fallback-architecture-blueprint.md)
- ERM: [../architecture/separates-arbeitsverzeichnis-git-init-fallback-entity-relationship-model.md](../architecture/separates-arbeitsverzeichnis-git-init-fallback-entity-relationship-model.md)

---

## 9. Versionierung

| Version | Datum | Autor | Änderung |
|---|---|---|---|
| 1.0.0 | 2026-05-13 | Review-Agent | Erstreview zum Init/Clone/Copy-Fallback |
| 2.0.0 | 2026-05-13 | Architektur-Review-Agent | Kritisches Review zu Pull-ohne-Merge, Push-Sync, Delete-Sync, Legacy-Guard inkl. priorisierter Findings, Risikoanalyse, P0/P1/P2 und prüfbaren Abnahmekriterien |
