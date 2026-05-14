# Anforderungsanalyse – Separates Arbeitsverzeichnis mit Copy & Git-Bootstrap

> **Dokument-Typ:** Requirements Analysis
> **Status:** ✅ Implementiert
> **Version:** 3.0.0
> **Datum:** 2026-05-13

---

## 1. Überblick und Ziel

Die bisherige Planung erlaubte im Modus `SeparateWorkingDirectory` eine konfigurierbare Git-Initialisierung.
Die neue Anforderung stellt das Verhalten klar:

- Das Quellverzeichnis bleibt unverändert.
- Die Arbeitskopie wird per einfacher Dateikopie aus dem Quellverzeichnis erzeugt.
- `git init` wird ausschließlich im separaten Arbeitsverzeichnis ausgeführt.
- In den Einstellungen ist `git init` im separaten Modus nicht mehr konfigurierbar.

Ziel ist ein stabiler, nachvollziehbarer Start eines lokalen Repository-Workflows ohne Änderungen am Quellverzeichnis.

## 2. Funktionale Anforderungen

| Kennung | Beschreibung | Priorität |
|---|---|---|
| FR-1 | Beim Start einer Aufgabe im Modus `SeparateWorkingDirectory` wird das Quellverzeichnis nur gelesen und per Dateikopie in das Arbeitsverzeichnis übertragen. | MUST HAVE |
| FR-1.1 | Die Kopie wird ohne Änderung des Quellverzeichnisses erzeugt; Git-Metadaten des Quellverzeichnisses werden nicht als Quelle der Initialisierung verwendet. | MUST HAVE |
| FR-1.2 | `git init` wird ausschließlich im neu erzeugten Arbeitsverzeichnis ausgeführt. | MUST HAVE |
| FR-1.3 | Im Modus `SeparateWorkingDirectory` ist `git init` keine konfigurierbare Benutzeroption. | MUST HAVE |
| FR-2 | Die Arbeitskopie bleibt ein vollständiger lokaler Arbeitsstand, auf dem lokale Commits, Pull- und Push-Workflows weiter aufbauen können. | HIGH |
| FR-3 | Pull im separaten Arbeitsverzeichnis führt keinen Merge aus und bleibt als expliziter Aktualisierungsfluss mit Hinweis erhalten. | HIGH |
| FR-4 | Push im separaten Arbeitsverzeichnis spiegelt Änderungen als Dateisynchronisation in das Quellverzeichnis zurück; `git push` bleibt dabei ausgeschlossen. | HIGH |
| FR-5 | Gelöschte Dateien werden über Git-Änderungserkennung im Arbeitsverzeichnis erkannt und im Quellverzeichnis ebenfalls gelöscht. | HIGH |
| FR-6 | Der Modus `InSourceDirectory` behält die bisherige Git-Initialisierungslogik und Sicherheitsabfragen bei. | HIGH |
| FR-7 | Alle relevanten Schritte werden strukturiert protokolliert. | HIGH |

## 3. Nicht-funktionale Anforderungen

| Kennung | Beschreibung | Priorität |
|---|---|---|
| NFR-1 | Das Quellverzeichnis darf durch die Vorbereitung der Aufgabe nicht mutiert werden. | MUST HAVE |
| NFR-2 | Der Workflow muss deterministisch sein: gleiche Eingaben erzeugen denselben Vorbereitungszustand. | MUST HAVE |
| NFR-3 | UI, Einstellungen und Fachlogik müssen die nicht konfigurierbare Git-Initialisierung im separaten Modus konsistent abbilden. | MUST HAVE |
| NFR-4 | Fehlerzustände dürfen weder Quell- noch Arbeitsverzeichnis in einen undefinierten Teilzustand bringen. | MUST HAVE |
| NFR-5 | Copy- und Bootstrap-Vorgänge müssen für mittlere lokale Repositories performant und nachvollziehbar bleiben. | HIGH |

## 4. Akzeptanzkriterien

### US-1 – Arbeitskopie ohne Source-Mutation
1. Der Start einer Aufgabe im separaten Modus kopiert das Quellverzeichnis in das Arbeitsverzeichnis.
2. Das Quellverzeichnis bleibt unverändert.
3. Das Arbeitsverzeichnis erhält anschließend ein eigenes Git-Repository via `git init`.

### US-2 – Kein konfigurierbares `git init` im separaten Modus
1. In den Einstellungen wird für `SeparateWorkingDirectory` keine `git init`-Option angeboten.
2. Eine bestehende Konfiguration kann den Git-Init-Schritt im separaten Modus nicht aktivieren oder deaktivieren.
3. Die UI zeigt den Git-Bootstrap als festes Systemverhalten, nicht als Benutzerentscheidung.

### US-3 – Bestehende Folgeflüsse bleiben nutzbar
1. Pull arbeitet ohne Merge.
2. Push arbeitet als Dateisynchronisation.
3. Delete-Sync bleibt über Git-Status im Arbeitsverzeichnis nachvollziehbar.

## 5. Scope und Abgrenzung

### In Scope
- Initiale Arbeitskopie per Dateikopie
- Git-Initialisierung im Arbeitsverzeichnis
- Unterdrückung der Git-Init-Konfiguration im separaten Modus
- Weiterführung der bestehenden Pull-/Push-/Delete-Sync-Logik

### Out of Scope
- Remote-Git-Workflows
- Branch-Merge-Strategien auf Hosting-Plattformen
- Automatische Migration bestehender Quellen in Git-Repositorys

## 6. Domänenmodell (grob)

```mermaid
flowchart LR
    SRC[Quellverzeichnis] --> COPY[Source Copy]
    COPY --> WRK[Arbeitsverzeichnis]
    WRK --> INIT[git init im Working Directory]
    WRK --> FLOW[Pull / Push / Delete-Sync]
    SETTINGS[Einstellungen] -. keine Git-Init-Option .-> WRK
```

## 7. Annahmen und Abhängigkeiten

- Das Quellverzeichnis ist lesbar.
- Das Arbeitsverzeichnis kann neu angelegt oder geleert werden.
- Git ist auf dem System verfügbar.
- Der separate Modus bleibt lokal und ohne Remote-Pflichten.

## 8. Offene Punkte

1. Soll die Dateikopie Git-Metadaten wie `.git` ausdrücklich ausschließen?
   **Empfehlung:** Ja, damit `git init` im Arbeitsverzeichnis eindeutig wirkt.
2. Wie wird mit nicht leeren Zielverzeichnissen umgegangen?
   **Empfehlung:** Fail-Fast mit klarer Fehlermeldung.
3. Welche Pfade werden im Copy-Schritt explizit ausgeschlossen?
   **Empfehlung:** `.git`, Lock-Dateien und temporäre Artefakte.

## 9. Verlinkung

- Architektur-Blueprint: [../architecture/separates-arbeitsverzeichnis-git-init-fallback-architecture-blueprint.md](../architecture/separates-arbeitsverzeichnis-git-init-fallback-architecture-blueprint.md)
- ERM: [../architecture/separates-arbeitsverzeichnis-git-init-fallback-entity-relationship-model.md](../architecture/separates-arbeitsverzeichnis-git-init-fallback-entity-relationship-model.md)
- Architecture-Review: [../improvements/separates-arbeitsverzeichnis-git-init-fallback-architecture-review.md](../improvements/separates-arbeitsverzeichnis-git-init-fallback-architecture-review.md)

## 10. Versionierung

| Version | Datum | Autor | Änderung |
|---|---|---|---|
| 2.0.0 | 2026-05-13 | Planning-Agent | Vorherige Variante mit optionalem Git-Init-Fallback |
| 3.0.0 | 2026-05-13 | Planning-Orchestrator | Source bleibt unberührt, `git init` nur im Working Directory, Git-Init-Konfiguration im separaten Modus entfernt |
| 3.1.0 | 2026-05-13 | Implementation-Orchestrator | Source-Copy-Bootstrap umgesetzt und Settings-Hiding für `ConfirmGitInitInSourceDirectory` ergänzt |
