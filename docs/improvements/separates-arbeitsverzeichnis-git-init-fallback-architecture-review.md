# Architektur-Review – Separates Arbeitsverzeichnis mit Source-Copy und Git-Bootstrap

> **Dokument-Typ:** Architecture Review
> **Status:** ✅ Reviewed (umgesetzt)
> **Version:** 3.0.0
> **Datum:** 2026-05-13

---

## 1. Executive Summary

Die neue Zielrichtung ist fachlich klarer als die vorige Variante:

- Das Quellverzeichnis bleibt unberührt.
- Die Arbeitskopie entsteht per Dateikopie.
- `git init` läuft nur im Arbeitsverzeichnis.
- In `SeparateWorkingDirectory` ist die Git-Init-Option nicht konfigurierbar.

Damit ist der Bootstrap-Flow einfacher und besser verständlich.
Für eine belastbare Umsetzung müssen aber Copy-Scope, Zielzustand und UI-Konsistenz noch verbindlich abgesichert werden.

## 2. Review-Bewertung

### 2.1 Architektur
- **Stärke:** klare Trennung zwischen Source und Working Directory.
- **Risiko:** unklare Kopiergrenzen können versehentlich Metadaten oder Sonderfälle übernehmen.
- **Bewertung:** tragfähig, aber Guardrails fehlen.

### 2.2 Technologieentscheidungen
- **Stärke:** `git init` im Working Directory ist technisch einfach und reproduzierbar.
- **Risiko:** Wenn das Zielverzeichnis nicht leer oder nicht sauber vorbereitet ist, entstehen inkonsistente Repository-Zustände.
- **Bewertung:** funktional plausibel, aber noch nicht vollständig abgesichert.

### 2.3 UI/UX
- **Stärke:** keine Benutzerentscheidung für Git-Init im separaten Modus reduziert Komplexität.
- **Risiko:** Die UI muss die Regel überall konsistent verbergen oder als nicht editierbar darstellen.
- **Bewertung:** korrektes Zielbild, Umsetzungskonsistenz offen.

## 3. Priorisierte Findings

| ID | Priorität | Finding | Empfehlung |
|---|---|---|---|
| AR-3.0-01 | Blocker | Copy-Scope ist nicht normiert; Metadaten, `.git`, Locks und Symlinks könnten ungewollt übernommen werden | Verbindliche Include-/Exclude-Regeln definieren |
| AR-3.0-02 | Blocker | Zielverzeichniszustand vor Copy/Init nicht exakt spezifiziert | Fail-Fast bei nicht leerem oder inkonsistentem Zielverzeichnis |
| AR-3.0-03 | Major | Git-Init-Option könnte in Nebenansichten oder Persistenzen weiterhin sichtbar sein | Einheitliche Settings-Projection für alle UI-Einstiege festlegen |
| AR-3.0-04 | Major | Verhalten bei bereits bestehendem Git-Repository im Source-Baum nicht klar genug beschrieben | Source nur als Datenquelle behandeln, keine Initialisierung ableiten |
| AR-3.0-05 | Minor | Reason-Codes für Copy- und Bootstrap-Fehler noch nicht normiert | Einheitliche Fehlerklassifikation ergänzen |

## 4. Risikoanalyse

| Risiko | Impact | Bewertung |
|---|---|---|
| Teilweise oder falsche Kopie | Hoch | Hoch |
| Ungewollte Übernahme von Repository-Metadaten | Hoch | Hoch |
| Inkonsistenter UI-Status zur Git-Init-Regel | Mittel | Mittel |
| Nicht reproduzierbarer Start bei vollem Zielverzeichnis | Hoch | Hoch |

## 5. Verbesserungsplan

### P0 – vor Implementierungsfreigabe
1. Copy-Scope verbindlich definieren.
2. Zielverzeichnis-Validierung normieren.
3. Git-Init-Option im separaten Modus in allen UI-Pfaden deaktivieren.

### P1 – während Umsetzung
4. Fehlerklassen für Copy und Bootstrap ergänzen.
5. Regressionstests für Source-Immutability und Working-Directory-Init anlegen.

### P2 – vor Release
6. Logging- und Supporttexte für den neuen Bootstrap-Flow schärfen.

## 6. Abnahmekriterien

- [x] Das Quellverzeichnis bleibt nach dem Start unverändert.
- [x] `git init` wird im separaten Modus nur im Arbeitsverzeichnis ausgeführt.
- [x] In `SeparateWorkingDirectory` ist kein editierbarer Git-Init-Schalter sichtbar.
- [x] Copy-Scope, Fehlerfälle und Zielverzeichnisregeln sind testspezifiziert.

## 7. Verlinkung

- Anforderungen: [../requirements/separates-arbeitsverzeichnis-git-init-fallback-requirements-analysis.md](../requirements/separates-arbeitsverzeichnis-git-init-fallback-requirements-analysis.md)
- Architektur-Blueprint: [../architecture/separates-arbeitsverzeichnis-git-init-fallback-architecture-blueprint.md](../architecture/separates-arbeitsverzeichnis-git-init-fallback-architecture-blueprint.md)
- ERM: [../architecture/separates-arbeitsverzeichnis-git-init-fallback-entity-relationship-model.md](../architecture/separates-arbeitsverzeichnis-git-init-fallback-entity-relationship-model.md)

## 8. Versionierung

| Version | Datum | Autor | Änderung |
|---|---|---|---|
| 2.0.0 | 2026-05-13 | Review-Agent | Review zur früheren Fallback-Variante |
| 3.0.0 | 2026-05-13 | Planning-Orchestrator | Review auf Source-Copy, Working-Directory-Git-Bootstrap und nicht konfigurierbares Git-Init angepasst |
| 3.1.0 | 2026-05-13 | Implementation-Orchestrator | Umsetzung abgeschlossen und Abnahmekriterien erfüllt |
