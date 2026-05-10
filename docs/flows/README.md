# Flows – Dokumentationsindex

Dieses Verzeichnis enthält die Programmablaufplan-Dokumentation für **Softwareschmiede**, eine Blazor Server-Anwendung für KI-gestützte Softwareentwicklung.

## Dokumentierte Abläufe

| Ablauf | Datei | Beschreibung |
|--------|-------|--------------|
| [Entwicklungsprozess-Abläufe](./development-process-flow.md) | `development-process-flow.md` | Alle zentralen Abläufe des Entwicklungszyklus (5 Kern-Flows) |
| [Arbeitsverzeichnis-Auflösung](./workdir-resolution-flow.md) | `workdir-resolution-flow.md` | Ablauf für Konfiguration, Laufzeit-Auflösung und Fallback des Basis-Arbeitsverzeichnisses |
| [Plugin-Discovery und Laden](./plugin-discovery-load-flow.md) | `plugin-discovery-load-flow.md` | Host-Start, Lazy-Discovery im PluginManager und robuste Registrierung von SCM-/Automation-Plugins |

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

### [Ablauf 6: Arbeitsverzeichnis-Auflösung für lokale Klone](./workdir-resolution-flow.md)
**Typ:** `sequenceDiagram` · **Services:** `ArbeitsverzeichnisSettingsService`, `ArbeitsverzeichnisResolver`, `EntwicklungsprozessService`

Beschreibt, wie die Einstellung `repositories.workdir` gespeichert wird, wie der Laufzeit-Fallback funktioniert und wie der finale Klonpfad `<basis>/softwareschmiede/<aufgabeId>` gebildet wird.

---

### [Ablauf 7: Plugin-Discovery und Laden](./plugin-discovery-load-flow.md)
**Typ:** `flowchart TD` · **Services:** `Program`, `PluginManager`, `IPluginManager`

Beschreibt den Ablauf von der DI-Registrierung bis zur Lazy-Discovery aus `<Programmverzeichnis>/plugins`, inklusive Fehlerbehandlung für defekte DLLs.

---

## Statusübergänge (Kurzreferenz)

```
Offen → InBearbeitung → KiAktiv → InBearbeitung (nach KI-Abschluss)
                                 ↘ Fehlgeschlagen
         InBearbeitung → Abgeschlossen
         InBearbeitung → Offen (nach Abbrechen)
```

Vollständiges Zustandsdiagramm: [development-process-flow.md – Zustandsdiagramm](./development-process-flow.md#zustandsdiagramm-aufgabestatus)

## Verknüpfte Dokumentation

- Technische Schnittstellen: [docs/api/plugin-interfaces.md](../api/plugin-interfaces.md)
- Fachliche Einordnung: [F010 – Plugin-Prinzip für Integrationen](../business/features/F010-plugin-prinzip-integrationen.md)
- Testabdeckung: [Testplan Plugin-Klassenbibliotheken](../tests/testplan-plugin-klassenbibliotheken-github-und-copilot.md)
