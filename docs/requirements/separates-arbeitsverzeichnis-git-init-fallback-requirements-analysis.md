# Anforderungsanalyse – Separates Arbeitsverzeichnis mit git-init-/Copy-Fallback

> **Dokument-Typ:** Requirements Analysis  
> **Status:** ✅ Umgesetzt  
> **Version:** 1.0.0  
> **Datum:** 2026-05-13

---

## 1. Überblick und Problem

Beim Aufgabenstart für ein lokales Quellverzeichnis im Modus `SeparateWorkingDirectory` schlägt `git clone` fehl, wenn die Quelle kein Git-Repository ist.

Fehlerbild:
- `fatal: '<Pfad>' does not appear to be a git repository`

Ziel ist ein deterministischer Vorbereitungsablauf mit klarer Entscheidung zwischen `clone`, `init+clone` und `copy`.

## 2. Funktionale Anforderungen

| Kennung | Beschreibung | Priorität | Status |
|---|---|---|---|
| FR-1 | Vor Clone prüfen, ob Quellverzeichnis git-basiert ist. | MUST HAVE | ✅ Umgesetzt |
| FR-2 | Ist Quelle nicht git-basiert und `git init` aktiviert: zuerst `git init` in der Quelle. | MUST HAVE | ✅ Umgesetzt |
| FR-3 | Clone erst nach erfolgreichem Git-Check bzw. erfolgreichem `git init`. | MUST HAVE | ✅ Umgesetzt |
| FR-4 | Ist Quelle nicht git-basiert und `git init` deaktiviert: Copy-Fallback ins separate Arbeitsverzeichnis statt Clone. | MUST HAVE | ✅ Umgesetzt |
| FR-5 | Gewählten Pfad (`clone` / `init+clone` / `copy`) nachvollziehbar protokollieren. | HIGH | ✅ Umgesetzt |
| FR-6 | Für bereits git-basierte Quellen bleibt Verhalten rückwärtskompatibel. | MUST HAVE | ✅ Umgesetzt |

## 3. Nicht-funktionale Anforderungen

| Kennung | Beschreibung | Priorität | Status |
|---|---|---|---|
| NFR-1 | Deterministisches Verhalten für gleiche Eingaben/Settings. | MUST HAVE | ✅ Umgesetzt |
| NFR-2 | Klare Fehlerklassifikation (Git-Check, Init, Clone, Copy). | MUST HAVE | ✅ Umgesetzt |
| NFR-3 | Kein inkonsistenter Zielzustand bei Teilfehlern (Cleanup). | MUST HAVE | ✅ Umgesetzt |
| NFR-4 | Copy-Guardrails (Dateien/Größe/Timeout) bleiben wirksam. | MUST HAVE | ✅ Umgesetzt |
| NFR-5 | Entscheidungspfad und Grund als strukturierte Logs. | HIGH | ✅ Umgesetzt |

## 4. Akzeptanzkriterien

- AC-1: Bei `SeparateWorkingDirectory` wird vor Clone immer ein Git-Check auf der Quelle ausgeführt.
- AC-2: Nicht-Git-Quelle + `git init` aktiv → `git init` in Quelle, danach Clone.
- AC-3: Schlägt `git init` fehl, wird Clone nicht gestartet und ein klarer Fehler ausgegeben.
- AC-4: Nicht-Git-Quelle + `git init` deaktiviert → kein Clone, stattdessen Dateikopie.
- AC-5: Bereits git-basierte Quelle nutzt weiterhin den bisherigen Clone-Pfad.
- AC-6: Logs enthalten den gewählten Vorbereitungspfad inklusive Begründung.

## 5. Betroffene Komponenten und Stakeholder

- Komponenten:
  - `LocalDirectoryPlugin`
  - `EntwicklungsprozessService`
  - `ArbeitsverzeichnisResolver`
  - Settings für `WorkspaceMode` und `ConfirmGitInitInSourceDirectory`
- Stakeholder:
  - Anwender (stabiler Start ohne Git-Fatal)
  - Entwicklung/QA (deterministische Logik, testbare Pfade)
  - Support (eindeutige Fehlerursachen)

## 6. Scope / Out-of-Scope

### In-Scope ✅
- Entscheidungslogik `git-check -> init+clone / clone / copy` für lokale Quellen im separaten Arbeitsverzeichnis.
- Fehlerbehandlung und Protokollierung des gewählten Pfads.

### Out-of-Scope ❌
- Änderungen an Remote-/GitHub-Plugins.
- UI-Redesign.
- Neue externe API-Endpunkte.

## 7. Domänenmodell (textuell)

- **SourceDirectory**: Lokales Quellverzeichnis.
- **WorkingDirectory**: Ziel-Arbeitsverzeichnis für die Aufgabe.
- **WorkspaceMode**: Betriebsmodus (`SeparateWorkingDirectory` relevant).
- **GitInitSetting**: Erlaubnis für Initialisierung im Quellverzeichnis.
- **PreparationStrategy**: Ergebnis der Entscheidung (`Clone`, `InitThenClone`, `CopyFallback`).

Regel: `SourceDirectory + WorkspaceMode + GitInitSetting` bestimmt die `PreparationStrategy`.

## 8. Randfälle / Fehlerszenarien

- `git init` ohne Berechtigung im Quellpfad.
- Quelle während Lauf nicht mehr erreichbar.
- Zielverzeichnis nicht leer.
- Copy-Fallback überschreitet Guardrails.
- Git-Check liefert technischen Fehler.

## 9. Risiken / offene Fragen

1. Quellmutation durch `git init` muss klar über Settings geregelt und kommuniziert sein.
2. Concurrency bei parallelen Starts auf dieselbe Quelle sollte in der Architektur berücksichtigt werden.
3. Einheitliche Fehlermeldungstexte für Support und UI nötig.

## 10. Verlinkte Folgeartefakte

- Architektur: [../architecture/separates-arbeitsverzeichnis-git-init-fallback-architecture-blueprint.md](../architecture/separates-arbeitsverzeichnis-git-init-fallback-architecture-blueprint.md)
- ERM: [../architecture/separates-arbeitsverzeichnis-git-init-fallback-entity-relationship-model.md](../architecture/separates-arbeitsverzeichnis-git-init-fallback-entity-relationship-model.md)
- Review: [../improvements/separates-arbeitsverzeichnis-git-init-fallback-architecture-review.md](../improvements/separates-arbeitsverzeichnis-git-init-fallback-architecture-review.md)
