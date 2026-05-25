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
| [KI-Protokoll Auto-Scroll](./ki-protokoll-auto-scroll-flow.md) | `ki-protokoll-auto-scroll-flow.md` | Initial-Scroll beim Einblenden, konditionales Follow-Scroll bei neuen Inhalten und Scroll-Lock bei manuellem Hochscrollen |
| [Standardplugin-Auflösung & KI-Dispatch](./plugin-default-selection-flow.md) | `plugin-default-selection-flow.md` | Persistente Standardplugins je Pluginart und Auflösung der effektiven KI-Plugin-Instanz pro Prompt (explizit → Default → Fallback) |
| [KI-Plugin-spezifische Agenten-Discovery/Auswahl (Issue 58)](./ki-plugin-spezifische-agenten-discovery-auswahl-flow.md) | `ki-plugin-spezifische-agenten-discovery-auswahl-flow.md` | Verbindlicher Auswahlfluss `KI-Plugin → Agentenpaket → Agent` inkl. Persistenz `KiPluginPrefix`, plugin-spezifischer Discovery und einheitlicher Auflösung in Start/Folgeprompt |
| [ProjektService: Projektverwaltung](./projekt-service-flow.md) | `projekt-service-flow.md` | End-to-End-Flow für Projektübersicht, Detailaktionen (Bearbeiten/Archivieren/Löschen) und Repository-Zuordnung |
| [AgentPackageFileService: Dateisystem & Sicherheit](./agent-package-file-service-flow.md) | `agent-package-file-service-flow.md` | Paket-/Dateioperationen inkl. sicherer Pfadauflösung, Validierung, rekursivem Dateibaum und robusten I/O-Fehlerpfaden |
| [LocalDirectoryPlugin: WorkspaceMode & Guardrails](./local-directory-plugin-flow.md) | `local-directory-plugin-flow.md` | Plugin-spezifischer Ablauf für InSourceDirectory/SeparateWorkingDirectory inkl. Source-Copy-Bootstrap, Kopier-Guardrails, Dateisynchronisation, Capability-Flags und UI-Aktionsmatrix (Push/Pull/PR ausblenden, Merge einblenden) |
| [Live Project Browser mit Git-Status](./live-project-browser-git-status-flow.md) | `live-project-browser-git-status-flow.md` | Snapshot-Laden mit getrennter `CodeFiles`/`PlanningDocuments`-Klassifikation, Fallback-Erkennung für Planungsdokument-Pfade sowie Compliance-Bezug zum Agentenpaket-Workflow |
| [Manuelle Aufgaben-Recovery](./aufgabe-recovery-flow.md) | `aufgabe-recovery-flow.md` | UI- und Serviceablauf zur Wiederherstellung festhängender Aufgaben mit Laufzeit-Guard, Audit-Log und Concurrency-Schutz |
| [GitOrchestrationService: Git-Aktionen & PR-Auflösung](./git-orchestration-service-flow.md) | `git-orchestration-service-flow.md` | Issue-Import, Commit/Reset/Push/Pull mit plugin-spezifischer Semantik (Remote-Git vs. Datei-Sync) sowie Pull-Request-Erstellung mit Repository-Guards |
| [KiAusfuehrungsService: Hintergrundläufe](./ki-ausfuehrungs-service-flow.md) | `ki-ausfuehrungs-service-flow.md` | Singleton-Sessionmanagement für KI-Streaming, Live-Subscriptions und RunningCount-Events |
| [Issue-, Branch- und PR-Verknüpfung](./issue-branch-pr-linking-flow.md) | `issue-branch-pr-linking-flow.md` | End-to-End-Flow von der Issue-Auswahl über issuebezogenen Branch bis zur PR-Closing-Direktive (`Closes #<Issue>`) |
| [Diff-Pipeline (Controller, Service, Algorithmus, Cache)](./diff-service-flow.md) | `diff-service-flow.md` | End-to-End-Ablauf für Diff-Generierung, 2-Tier-Caching, Persistierung sowie Validierungs- und Fehlerpfade |
| [DiffViewer-Integration (UI, FR-4, Route)](./diffviewer-integration-flow.md) | `diffviewer-integration-flow.md` | UI-Integrationsfluss zwischen `AufgabeDetail`, `DiffPreviewPanel` und `DiffViewer` inkl. dateispezifischer Diff-Auflösung, FR-4-Fallbackpfaden, Parameterwechsel-Stabilität und `/diff/{DiffResultId:guid}`-Kompatibilität |
| [Repository-Startskript mit freier Portzuweisung](./repository-startskript-freier-port-flow.md) | `repository-startskript-freier-port-flow.md` | Konfigurations- und Laufzeitablauf für repositorybezogene Startskripte inkl. Portreservierung und PowerShell-Ausführung beim Prozessstart |
| [`start.ps1` für VS-Debug mit freiem HTTP-Port](./start-ps1-visual-studio-freier-http-port-flow.md) | `start-ps1-visual-studio-freier-http-port-flow.md` | Ablauf für Portauflösung (Parameter/Env/Auto), `launchSettings.json`-Update und Exit-Code-Pfade des lokalen Startskripts |
| [Benachrichtigungssystem für abgeschlossene KI-Aufgaben](./benachrichtigungssystem-flow.md) | `benachrichtigungssystem-flow.md` | End-to-End-Flow von `KiAufgabenAbschlussEreignis` über Hub-Dispatch bis zu Toast/Ton, Modusmatrix, Audit und Einstellungsverwaltung |
| [Favicon-Auslieferung und Browser-Fallback](./favicon-delivery-flow.md) | `favicon-delivery-flow.md` | Lebenszyklus des SVG-Favicons von `App.razor`-Head-Links über `MapStaticAssets()` bis zur browserabhängigen Icon-Auswahl und `/favicon.ico`-Fallback |

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

Zeigt die vollständigen Statusübergänge von `Offen` bis `Archiviert` inklusive fachlicher Guards, Verwerfen offener Aufgaben und Fehlerbehandlung.

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

### [Ablauf 4b: Offene Aufgabe verwerfen](./development-process-flow.md#ablauf-4b-offene-aufgabe-verwerfen)
**Typ:** `flowchart TD` · **Services:** `AufgabeService`, `AufgabeDetail`

Direktes Verwerfen einer noch nicht gestarteten Aufgabe: je nach Auswahl wird sie archiviert oder dauerhaft gelöscht; ein lokaler Klon existiert dabei noch nicht.

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

### [Ablauf 8b: KI-Protokoll Auto-Scroll (Initial, Follow, Scroll-Lock)](./ki-protokoll-auto-scroll-flow.md)
**Typ:** `flowchart TD` · **Services:** `AufgabeDetail`, `IJSRuntime`, `softwareschmiedeLogScroll`

Dokumentiert die Scroll-Entscheidungslogik für Streaming- und Historiencontainer: Initial-Scroll beim Einblenden, Follow-Scroll nur bei zuvor erreichtem Ende sowie Positionsbeibehaltung bei manuellem Hochscrollen.

---

### [Ablauf 9: Standardplugin-Auflösung und KI-Plugin-Dispatch](./plugin-default-selection-flow.md)
**Typ:** `sequenceDiagram` + `flowchart TD` · **Services:** `EinstellungenBase`, `PluginDefaultSettingsService`, `PluginSelectionService`, `AufgabeDetail`, `KiAusfuehrungsService`, `EntwicklungsprozessService`

Beschreibt den End-to-End-Pfad von der Default-Konfiguration in den Einstellungen bis zur tatsächlichen Prompt-Ausführung mit klarer Auflösungskette (**explizit → Default → Fallback**) und KI-Fallback-Präferenz.

---

### [Ablauf 9b: KI-Plugin-spezifische Agenten-Discovery/Auswahl (Issue 58)](./ki-plugin-spezifische-agenten-discovery-auswahl-flow.md)
**Typ:** `sequenceDiagram` + `flowchart TD` · **Services:** `AufgabeDetail`, `PluginSelectionService`, `EntwicklungsprozessService`, `KiAusfuehrungsService`, `IAgentPackageService`, `IKiPlugin`, `AufgabeService`

Dokumentiert die verbindliche UI-Reihenfolge **KI-Plugin → Agentenpaket → Agent**, die plugin-spezifische Discovery pro Paket, die Persistenz von `KiPluginPrefix` sowie die konsistente Prefix-Auflösung in Start- und Folgeprompt-Pfaden.

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

Beschreibt den End-to-End-Integrationspfad des lokalen Verzeichnis-Plugins: Einstellungen → Prozessstart → `CloneRepositoryAsync`-Verzweigung nach `WorkspaceMode` (inkl. Source-Copy-Bootstrap) → sichere Kopie mit Limits/Symlink-Schutz → Pointer-/Mapping-Auflösung für Folgeoperationen sowie Push/Pull als Datei-Sync.

---

### [Ablauf 13: GitOrchestrationService – Git-Aktionen und PR-Auflösung](./git-orchestration-service-flow.md)
**Typ:** `sequenceDiagram` + `flowchart TD` · **Services:** `GitOrchestrationService`, `AufgabeDetail`, `NeueAufgabe`, `IGitPlugin`

Dokumentiert den End-to-End-Pfad für Issue-Import und manuelle Git-Aktionen inklusive LocalDirectory-Fallbacks (Push als Datei-Sync, Pull ohne Merge mit Hinweis, Delete-Sync via `git status`) sowie die Pull-Request-Repository-Auflösung (**Aufgaben-Repository → Projekt-Repository → Fehler bei Mehrdeutigkeit**).

---

### [Ablauf 14: KiAusfuehrungsService – Hintergrundlauf und Sessionpuffer](./ki-ausfuehrungs-service-flow.md)
**Typ:** `sequenceDiagram` + `flowchart TD` · **Services:** `KiAusfuehrungsService`, `EntwicklungsprozessService`, `AufgabeDetail`, `AutoShutdownOrchestrator`

Beschreibt den Ablauf von `StartKiLauf` bis `onCompleted`, inklusive Session-Wiederaufnahme, Live-Subscriptions und Running-Count-Events für die Auto-Shutdown-Orchestrierung.

---

### [Ablauf 15: Issue-Auswahl, Branch-Verknüpfung und PR Auto-Close](./issue-branch-pr-linking-flow.md)
**Typ:** `sequenceDiagram` + `flowchart TD` · **Services:** `NeueAufgabe`, `AufgabeService`, `EntwicklungsprozessService`, `GitOrchestrationService`, `IGitPlugin`

Dokumentiert die durchgängige Verknüpfung zwischen ausgewählter Issue, task-Branch und PR-Closing-Direktive inkl. Duplikatvermeidung bei bereits vorhandenen Direktiven.

---

### [Ablauf 16: Repository-Startskript mit freier Portzuweisung](./repository-startskript-freier-port-flow.md)
**Typ:** `sequenceDiagram` + `flowchart TD` · **Services:** `ProjektService`, `EntwicklungsprozessService`, `RepositoryStartskriptService`, `PortReservationService`, `ICliRunner`

Dokumentiert die repositorybezogene Startkonfiguration und die Ausführung eines Startskripts mit reserviertem Port beim Prozessstart inklusive Validierung, Sicherheitsgrenzen und Cleanup-Pfaden.

---

### [Ablauf 17: `start.ps1` für Visual-Studio-Debug](./start-ps1-visual-studio-freier-http-port-flow.md)
**Typ:** `sequenceDiagram` + `flowchart TD` · **Artefakte:** `start.ps1`, `launchSettings.json`, Visual-Studio-`http`-Profil

Dokumentiert den lokalen Skriptablauf von der Portquellen-Auflösung (**Parameter → Env → Auto**) über das gezielte `launchSettings`-Update bis zur Exit-Code-Rückgabe.

---

### [Ablauf 18: Live Project Browser mit Git-Status](./live-project-browser-git-status-flow.md)
**Typ:** `sequenceDiagram` + `flowchart TD` · **Services:** `AufgabeDetail`, `GitWorkspaceBrowserService`

Beschreibt den kompletten UI-Pfad von der Aufgabenansicht über die Query-Parameter-gesteuerte Tree-/Listenansicht bis zur Dateivorschau und zum Refresh – inklusive getrennter Klassifikation von `CodeFiles` und `PlanningDocuments`, robuster Fallback-Erkennung bei Slash/Dot-Pfadvarianten sowie Workflow-Bezug zur Agentendefinitions-Compliance.

---

### [Ablauf 19: Diff-Pipeline mit Caching und Fehlerpfaden](./diff-service-flow.md)
**Typ:** `flowchart TD` · **Services:** `DiffController`, `DiffService`, `DiffAlgorithmService`, `DiffCachingService`

Dokumentiert den Ablauf von `POST /api/diff/generate` über Service-Orchestrierung, 2-Tier-Cache und Persistierung bis zu den relevanten Validierungs- und Fehlerpfaden sowie der zugehörigen Testabdeckung.

---

### [Ablauf 19b: DiffViewer-Integration (UI-Zustandsgrenzen, FR-4, Route)](./diffviewer-integration-flow.md)
**Typ:** `sequenceDiagram` + `flowchart TD` · **Services:** `AufgabeDetail`, `DiffPreviewPanel`, `DiffViewer`, `DiffService`, `GitWorkspaceBrowserService`

Dokumentiert den End-to-End-Integrationspfad der Diff-Vorschau in der Aufgabenansicht inklusive dateispezifischer Diff-Zuordnung pro ausgewählter Datei, FR-4-Fallback-Entscheidungen, parameterstabilen Lade-Guards und Wrapper-Route `/diff/{DiffResultId:guid}`.

---

### [Ablauf 20: Manuelle Aufgaben-Recovery](./aufgabe-recovery-flow.md)
**Typ:** `sequenceDiagram` + `flowchart TD` · **Services:** `AufgabeDetail`, `AufgabeRecoveryService`, `IRunningAutomationStatusSource`

Beschreibt die Wiederherstellung von `KiAktiv`/`TestsLaufen` nach `InBearbeitung` mit Bestätigung, Laufzeitprüfung, konkurrierender Schutzlogik und Audit-Eintrag.

---

### [Ablauf 21: Benachrichtigungssystem für abgeschlossene KI-Aufgaben](./benachrichtigungssystem-flow.md)
**Typ:** `sequenceDiagram` + `flowchart TD` · **Services:** `EntwicklungsprozessService`, `KiAufgabenBenachrichtigungsHub`, `MainLayout`, `BenachrichtigungsEinstellungenService`, `BenachrichtigungsAuditService`

Dokumentiert den End-to-End-Flow von der Event-Publikation über kanalabhängige Toast-/Ton-Dispatch-Entscheidungen (inkl. Modusmatrix und Dedupe) bis zur Audit-Persistierung sowie den Einstellungsablauf für Modus, Audio-Upload und Testton.

---

### [Ablauf 22: Favicon-Auslieferung und Browser-Fallback](./favicon-delivery-flow.md)
**Typ:** `sequenceDiagram` + `flowchart TD` · **Artefakte:** `App.razor`, `Program.cs`, `wwwroot/favicon-hammer-pick.svg`

Beschreibt den Lebenszyklus des Favicons vom Head-Linking (`icon`/`shortcut icon`/`mask-icon`) über die statische Auslieferung via `MapStaticAssets()` bis zur browserabhängigen Auswahl und Fallback-Kette.

---

## Statusübergänge (Kurzreferenz)

```
Offen → InBearbeitung → KiAktiv → TestsLaufen → InBearbeitung
                         ↘ (Recovery) InBearbeitung
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
