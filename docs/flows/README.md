# Flows – Dokumentationsindex

Dieses Verzeichnis enthält die Programmablaufplan-Dokumentation für **Softwareschmiede**, eine Blazor Server-Anwendung für KI-gestützte Softwareentwicklung.

## Dokumentierte Abläufe

| Ablauf | Datei | Beschreibung |
|--------|-------|--------------|
| [Entwicklungsprozess-Abläufe](./development-process-flow.md) | `development-process-flow.md` | Alle zentralen Abläufe des Entwicklungszyklus (5 Flows) |

---

## Übersicht der einzelnen Flows

### [Ablauf 1: Entwicklungsprozess starten](./development-process-flow.md#ablauf-1-entwicklungsprozess-starten)
**Typ:** `sequenceDiagram` · **Services:** `EntwicklungsprozessService`, `IGitPlugin`, `IKiPlugin`

Beschreibt den vollständigen Einstieg in einen KI-gestützten Entwicklungszyklus: von der Aufgabenauswahl über das Repository-Klonen und Branch-Anlegen bis zum optionalen Deployen eines Agentenpakets.

---

### [Ablauf 2: KI-Streaming und Protokollierung](./development-process-flow.md#ablauf-2-ki-streaming-und-protokollierung)
**Typ:** `flowchart TD` · **Services:** `EntwicklungsprozessService`, `IKiPlugin`, `AufgabeService`

Zeigt den Streaming-Ablauf von `KiStartenAsync`: Statusprüfung, Prompt-Protokollierung, Chunk-weises Streamen der KI-Antwort und Statusrücksetzung – inklusive Fehlerfall (`Fehlgeschlagen`).

---

### [Ablauf 3: Aufgabe abschließen](./development-process-flow.md#ablauf-3-aufgabe-abschlie%C3%9Fen)
**Typ:** `flowchart TD` · **Services:** `GitOrchestrationService`, `EntwicklungsprozessService`, `IGitPlugin`

Beschreibt den Abschluss einer Aufgabe: Commit → Push → Pull Request erstellen → Klon-Bereinigung → Status `Abgeschlossen`.

---

### [Ablauf 4: Aufgabe abbrechen](./development-process-flow.md#ablauf-4-aufgabe-abbrechen)
**Typ:** `flowchart TD` · **Services:** `EntwicklungsprozessService`, `AufgabeService`

Geordneter Abbruch einer laufenden Aufgabe: lokaler Klon wird gelöscht (ohne Push), Aufgabe kehrt in den Status `Offen` zurück.

---

### [Ablauf 5: Issue aus GitHub importieren](./development-process-flow.md#ablauf-5-issue-aus-github-importieren-und-als-aufgabe-anlegen)
**Typ:** `sequenceDiagram` · **Services:** `GitOrchestrationService`, `IGitPlugin`, `AufgabeService`

Import von GitHub-Issues via `gh issue list` und direkte Übernahme als neue Aufgabe mit Titel, Body und Labels.

---

## Statusübergänge (Kurzreferenz)

```
Offen → InBearbeitung → KiAktiv → InBearbeitung (nach KI-Abschluss)
                                 ↘ Fehlgeschlagen
         InBearbeitung → Abgeschlossen
         InBearbeitung → Offen (nach Abbrechen)
```

Vollständiges Zustandsdiagramm: [development-process-flow.md – Zustandsdiagramm](./development-process-flow.md#zustandsdiagramm-aufgabestatus)
