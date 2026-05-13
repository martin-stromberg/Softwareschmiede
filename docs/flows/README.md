# Flows – Dokumentationsindex

Dieses Verzeichnis enthält die Programmablaufplan-Dokumentation für **Softwareschmiede**, eine Blazor Server-Anwendung für KI-gestützte Softwareentwicklung.

## Dokumentierte Abläufe

| Ablauf | Datei | Beschreibung |
|--------|-------|--------------|
| [Entwicklungsprozess-Abläufe](./development-process-flow.md) | `development-process-flow.md` | Alle zentralen Abläufe des Entwicklungszyklus (inkl. Agent-Auswahl bei Folgeanweisungen) |
| [Kontextsteuerung bei Folgeanweisungen](./follow-up-context-steering-flow.md) | `follow-up-context-steering-flow.md` | Folgeanweisungsfluss inkl. Moduslogik, Plugin-Auflösung (Copilot/Claude CLI), Komprimierung und persistenter Kontextdateien |
| [AufgabeService Statusübergänge](./aufgabe-service-status-flow.md) | `aufgabe-service-status-flow.md` | Statuslebenszyklus einer Aufgabe mit Guard-Checks, Persistenz und Fehlerpfaden |
| [AutoShutdownOrchestrator](./auto-shutdown-orchestrator-flow.md) | `auto-shutdown-orchestrator-flow.md` | Ereignisgesteuerter Auto-Shutdown beim Übergang laufender Automatisierungen von >0 auf 0 |
| [PluginSettingsService](./plugin-settings-service-flow.md) | `plugin-settings-service-flow.md` | Lesen/Schreiben von Plugin-Credentials und Laufzeitbezug zur Claude-CLI-Integration |
| [Arbeitsverzeichnis-Auflösung](./workdir-resolution-flow.md) | `workdir-resolution-flow.md` | Ablauf für Konfiguration, Laufzeit-Auflösung und Fallback des Basis-Arbeitsverzeichnisses |
| [Plugin-Discovery und Laden](./plugin-discovery-load-flow.md) | `plugin-discovery-load-flow.md` | Host-Start, Lazy-Discovery im PluginManager und robuste Registrierung von SCM-/Automation-Plugins |
| [KI-Arbeitsprotokoll: Persistierung und Rendering](./ki-arbeitsprotokoll-rendering-flow.md) | `ki-arbeitsprotokoll-rendering-flow.md` | Persistierung des Markdown-Protokolls mit Datumszeile/Schritten sowie sicheres Rendering mit Sanitizing und Fallback |
| [Standardplugin-Auflösung & KI-Dispatch](./plugin-default-selection-flow.md) | `plugin-default-selection-flow.md` | Persistente Standardplugins je Pluginart und Auflösung der effektiven KI-Plugin-Instanz pro Prompt (explizit → Default → Fallback) |
| [ProjektService: Projektverwaltung](./projekt-service-flow.md) | `projekt-service-flow.md` | End-to-End-Flow für Projektübersicht, Detailaktionen (Bearbeiten/Archivieren/Löschen) und Repository-Zuordnung |
| [AgentPackageFileService: Dateisystem & Sicherheit](./agent-package-file-service-flow.md) | `agent-package-file-service-flow.md` | Paket-/Dateioperationen inkl. sicherer Pfadauflösung, Validierung und rekursivem Dateibaum |
| [LocalDirectoryPlugin: WorkspaceMode & Guardrails](./local-directory-plugin-flow.md) | `local-directory-plugin-flow.md` | Plugin-spezifischer Ablauf für InSourceDirectory/SeparateWorkingDirectory inkl. git-init-Fallback, Kopier-Guardrails, Dateisynchronisation und Workspace-Mapping |
| [GitOrchestrationService: Git-Aktionen & PR-Auflösung](./git-orchestration-service-flow.md) | `git-orchestration-service-flow.md` | Issue-Import, Commit/Reset/Push/Pull mit plugin-spezifischer Semantik (Remote-Git vs. Datei-Sync) sowie Pull-Request-Erstellung mit Repository-Guards |
| [KiAusfuehrungsService: Hintergrundläufe](./ki-ausfuehrungs-service-flow.md) | `ki-ausfuehrungs-service-flow.md` | Singleton-Sessionmanagement für KI-Streaming, Live-Subscriptions und RunningCount-Events |

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

### [Ablauf 2b: Agent-Auswahl bei Folgeanweisungen](./development-process-flow.md#ablauf-2b-agent-auswahl-bei-folgeanweisungen)
**Typ:** `flowchart TD` · **Services:** `AufgabeDetail`, `EntwicklungsprozessService`, `IKiPlugin`

Beschreibt die Folge-Prompt-Logik mit Agenten-Auswahl, Start-Agent als Standardwert, frei änderbarer Auswahl vor dem Senden, Versand an den gewählten Agenten und Reset auf den Start-Agenten.

---

### [Ablauf 2c: Kontextsteuerung bei Folgeanweisungen](./follow-up-context-steering-flow.md)
**Typ:** `sequenceDiagram` + `flowchart TD` · **Services:** `AufgabeDetail`, `KiAusfuehrungsService`, `EntwicklungsprozessService`, `PluginManager`, `IKiPlugin`

Beschreibt die Moduslogik **Kontext mitgeben / ignorieren / neu beginnen** inkl. UI-Bestätigung, Soft-/Hard-Limit-Komprimierung, Fehlerpfaden und pluginabhängiger Persistenz (`{id}.copilot.context.md` / `{id}.claude.context.md`).

---

### [Ablauf 2d: AufgabeService Statusübergänge](./aufgabe-service-status-flow.md)
**Typ:** `stateDiagram-v2` + `flowchart TD` · **Services:** `AufgabeService`, `SoftwareschmiededDbContext`

Zeigt die vollständigen Statusübergänge von `Offen` bis `Archiviert` inklusive fachlicher Guards (z. B. Archivierung nur aus Endzuständen) und Fehlerbehandlung.

---

### [Ablauf 2e: AutoShutdown-Orchestrierung](./auto-shutdown-orchestrator-flow.md)
**Typ:** `sequenceDiagram` + `flowchart TD` · **Services:** `MainLayout`, `AutoShutdownOrchestrator`, `KiAusfuehrungsService`, `ISystemShutdownService`

Dokumentiert den Toggle-gesteuerten Shutdown-Mechanismus mit Idempotenz pro Zero-Transition und Final-Recheck vor Ausführung des OS-Kommandos.

---

### [Ablauf 2f: Plugin-Settings und Credential-Persistenz](./plugin-settings-service-flow.md)
**Typ:** `sequenceDiagram` + `flowchart TD` · **Services:** `EinstellungenBase`, `PluginSettingsService`, `IPluginManager`, `ICredentialStore`

Beschreibt die Schlüsselbildung `<PluginPrefix>.<FieldKey>`, das Speichern/Löschen von Credentials und die konkrete Nutzung des Claude-Tokens in `ClaudeCliPlugin`.

---

### [Ablauf 3: Aufgabe abschließen](./development-process-flow.md#ablauf-3-aufgabe-abschlie%C3%9Fen)
**Typ:** `flowchart TD` · **Services:** `GitOrchestrationService`, `EntwicklungsprozessService`, `IGitPlugin`

Beschreibt den Abschluss einer Aufgabe: Commit → Push (Remote-Git oder Datei-Sync) → Pull Request erstellen → Klon-Bereinigung → Status `Abgeschlossen`.

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

### [Ablauf 8: KI-Arbeitsprotokoll – Persistierung, Rendering und Fallback](./ki-arbeitsprotokoll-rendering-flow.md)
**Typ:** `sequenceDiagram` + `flowchart TD` · **Services:** `EntwicklungsprozessService`, `ProtokollService`, `AufgabeDetail`

Beschreibt die Ende-zu-Ende-Pipeline vom Erzeugen des Markdown-Protokolls (`# Datum`, `## Schritt n`) über die DB-Persistierung bis zur sicheren HTML-Darstellung in der UI inklusive Sanitizing und `<pre>`-Fallback.

---

### [Ablauf 9: Standardplugin-Auflösung und KI-Plugin-Dispatch](./plugin-default-selection-flow.md)
**Typ:** `sequenceDiagram` + `flowchart TD` · **Services:** `EinstellungenBase`, `PluginDefaultSettingsService`, `PluginSelectionService`, `AufgabeDetail`, `KiAusfuehrungsService`, `EntwicklungsprozessService`

Beschreibt den End-to-End-Pfad von der Default-Konfiguration in den Einstellungen bis zur tatsächlichen Prompt-Ausführung mit klarer Auflösungskette (**explizit → Default → Fallback**) und KI-Fallback-Präferenz.

---

### [Ablauf 10: ProjektService – Projektverwaltung und Repository-Zuordnung](./projekt-service-flow.md)
**Typ:** `sequenceDiagram` + `flowchart TD` · **Services:** `ProjektService`, `ProjektListe`, `ProjektDetail`, `AufgabeService`

Beschreibt die Trennung zwischen Übersichtsaktionen (Neu anlegen) und Einzelaktionen auf der Detailseite (Bearbeiten, Archivieren, Löschen, Repository hinzufügen) inklusive Persistenzpfaden.

---

### [Ablauf 11: AgentPackageFileService – Dateisystemoperationen und Pfadsicherheit](./agent-package-file-service-flow.md)
**Typ:** `sequenceDiagram` + `flowchart TD` · **Services:** `IAgentPackageFileService`, `AgentenpaketeSeite`

Dokumentiert den vollständigen Datei-/Verzeichnis-Flow für Agentenpakete inklusive `ResolveSafePath`-Guard, Namensvalidierung und rekursivem Dateibaum.

---

### [Ablauf 12: LocalDirectoryPlugin – WorkspaceMode, Kopierpfad und Guardrails](./local-directory-plugin-flow.md)
**Typ:** `sequenceDiagram` + `flowchart TD` · **Services:** `LocalDirectoryPlugin`, `EntwicklungsprozessService`, `GitOrchestrationService`, `ArbeitsverzeichnisResolver`

Beschreibt den End-to-End-Integrationspfad des lokalen Verzeichnis-Plugins: Einstellungen → Prozessstart → `CloneRepositoryAsync`-Verzweigung nach `WorkspaceMode` (inkl. git-init-Fallback) → sichere Kopie mit Limits/Symlink-Schutz → Pointer-/Mapping-Auflösung für Folgeoperationen sowie Push/Pull als Datei-Sync.

---

### [Ablauf 13: GitOrchestrationService – Git-Aktionen und PR-Auflösung](./git-orchestration-service-flow.md)
**Typ:** `sequenceDiagram` + `flowchart TD` · **Services:** `GitOrchestrationService`, `AufgabeDetail`, `NeueAufgabe`, `IGitPlugin`

Dokumentiert den End-to-End-Pfad für Issue-Import und manuelle Git-Aktionen inklusive LocalDirectory-Fallbacks (Push als Datei-Sync, Pull ohne Merge mit Hinweis, Delete-Sync via `git status`) sowie die Pull-Request-Repository-Auflösung (**Aufgaben-Repository → Projekt-Repository → Fehler bei Mehrdeutigkeit**).

---

### [Ablauf 14: KiAusfuehrungsService – Hintergrundlauf und Sessionpuffer](./ki-ausfuehrungs-service-flow.md)
**Typ:** `sequenceDiagram` + `flowchart TD` · **Services:** `KiAusfuehrungsService`, `EntwicklungsprozessService`, `AufgabeDetail`, `AutoShutdownOrchestrator`

Beschreibt den Ablauf von `StartKiLauf` bis `onCompleted`, inklusive Session-Wiederaufnahme, Live-Subscriptions und Running-Count-Events für die Auto-Shutdown-Orchestrierung.

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
- Testabdeckung Plugin-Architektur: [Testplan Plugin-Klassenbibliotheken](../tests/testplan-plugin-klassenbibliotheken-github-und-copilot.md)
- Testabdeckung Claude-CLI: [Testplan Claude-CLI-Integration](../tests/testplan-claude-cli-integration.md)
