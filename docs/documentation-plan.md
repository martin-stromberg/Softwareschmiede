# Dokumentationsplan – Feature „Changed Artifact Detection & Agentendefinitions-Compliance“ – 2026-05-24

## Kontext
- Feature-Kontext: **„Erkennung geänderter Planungsdokumente zusätzlich zu Codedateien und Sicherstellung der Agentendefinitions-Compliance“**.
- Planungs-, Implementierungs- und Testarbeiten waren bereits umgesetzt; Ziel dieses Laufs war die vollständige Dokumentationssynchronisation.
- Hinweis Agentendefinition: `~/.copilot/agents/documentation-orchestrator.agent.md` war in der Laufzeitumgebung nicht vorhanden (`AGENT_FILE_MISSING`), daher wurde der Orchestrator-Ablauf gemäß hinterlegter Definition im Repository-Kontext umgesetzt.
- Hinweis Subagentenlauf: Der vorgesehene parallele Analyse-Schwarm/Unteragentenlauf war aufgrund API-Rate-Limit (429) nicht verfügbar; Analyse und Ausführung wurden im gleichen Phasenschema manuell abgeschlossen.

## Phase 1 – Analyse

### 1) API-Docs-Analyse
- `docs/api/` vorhanden; technische Kernverträge sind dokumentiert.
- Lücken: Feature-Hinweis zu **Changed Artifact Detection + Agentendefinitions-Compliance** fehlte im API-Index und bei HTTP-Endpunkt-Abgrenzung.
- `docs/api/live-project-browser-git-status.md` enthielt die Klassifikation `CodeFiles`/`PlanningDocuments` und Fallback-Logik noch nicht explizit.
- `docs/api/plugin-interfaces.md` enthielt teilweise veraltete Aussagen zur Agentenpaketstruktur/Deployment-Topologie und Fehlerbehandlung.

### 2) Flow-Docs-Analyse
- `docs/flows/` vorhanden inkl. `live-project-browser-git-status-flow.md`.
- Lücke: Ablauf enthielt keinen expliziten Schritt zur Fallback-Erkennung geänderter Planungsdokumente und deren Wirkung auf den UI-Workflow.

### 3) Business-Docs-Analyse
- `docs/business/` vorhanden mit Featurekatalog.
- Lücken:
  - F021 beschrieb den Live Browser ohne explizite Nutzerwirkung der getrennten Planungsdokument-Erkennung.
  - F004 beschrieb Agentenpakete ohne klaren Compliance-Hinweis (Kompatibilitätsregeln + robuste Fehlerpfade).

### 4) README-Analyse
- README-Struktur vollständig.
- Lücken: Feature-Kontext (Ziel/Verhalten/Komponenten/Compliance/Testabdeckung/Workflow-Auswirkung) war nur fragmentiert sichtbar; Test- und Doku-Links für das Feature fehlten.

## Phase 1 – Priorisierter Ausführungsplan
1. **Hoch:** API-/Flow-/Business-Dokumente auf Featureverhalten (`CodeFiles` + `PlanningDocuments`, Fallback, Compliance-Regeln) synchronisieren.
2. **Hoch:** README um Featureziele, Testabdeckung und Doku-Referenzen ergänzen.
3. **Mittel:** Test-Dokumentindex und Querverweise vervollständigen.

## Phase 2 – Parallele Dokumentationserstellung
- `documentation-api`: ausgeführt (Scope `docs/api/`)
- `documentation-flow`: ausgeführt (Scope `docs/flows/`)
- `documentation-business`: ausgeführt (Scope `docs/business/`)
- `documentation-readme-writer`: ausgeführt (Scope `README.md`)

## Anhang: Ergebnis (Phase 3)

### Aktualisiert
1. `docs/api/README.md`
2. `docs/api/http-endpoints.md`
3. `docs/api/live-project-browser-git-status.md`
4. `docs/api/plugin-interfaces.md`
5. `docs/flows/README.md`
6. `docs/flows/live-project-browser-git-status-flow.md`
7. `docs/flows/development-process-flow.md`
8. `docs/business/features.md`
9. `docs/business/features/F004-agentenpakete.md`
10. `docs/business/features/F021-live-project-browser-git-status.md`
11. `docs/tests/README.md`
12. `README.md`
13. `docs/documentation-plan.md`

### Validierung
- Existenz-/Nicht-Leerheitsprüfung der aktualisierten Dateien: **erfolgreich**.
- Konsistenzabgleich gegen implementierte Komponenten/Tests durchgeführt:
  - `GitWorkspaceBrowserService` (`PlanningDocuments`-Klassifikation + Fallback)
  - `WorkspaceSnapshot` (`CodeFiles`, `PlanningDocuments`)
  - `AufgabeDetail` (Weiterverwendung der getrennten Artefaktlisten)
  - `GitHubCopilotPlugin`, `ClaudeCliPlugin`, `AgentPackageReader` (Compliance-/Fehlerpfade)
  - Tests: `GitWorkspaceBrowserServiceTests`, `AufgabeDetailWorkspacePreviewBunitTests`, `GitHubCopilotPluginTests`, `ClaudeCliPluginTests`, `AgentPackageReaderTests`

### Offene Punkte
- Keine kritischen offenen Dokumentationslücken im Feature-Scope identifiziert.

---

# Dokumentationsplan – Feature „favicon-hammer-pick-svg“ – 2026-05-24

## Kontext
- Feature-Slug: **`favicon-hammer-pick-svg`**
- Implementierungsstand laut Scope:
  - `src/Softwareschmiede/Components/App.razor` angepasst (SVG-Favicon-Links)
  - `src/Softwareschmiede/wwwroot/favicon-hammer-pick.svg` hinzugefügt
  - Tests ergänzt:
    - `src/Softwareschmiede.Tests/Components/AppTests.cs`
    - `src/Softwareschmiede.Tests/Infrastructure/StaticAssets/FaviconHammerPickSvgTests.cs`
- Hinweis zur Orchestrierung: Der in der Agent-Datei vorgesehene Analyse-Schwarm (`explore`, Haiku) war initial durch Rate-Limit blockiert; Phase 1 wurde daher manuell im identischen Prüfschema durchgeführt. Phase 2 wurde danach mit den vier Dokumentationsagenten ausgeführt.

## Phase 1 – Analyse

### 1) API-Docs-Analyse
- `docs/api/` existiert.
- Relevante bestehende API-Dokumente vorhanden (`README.md`, `http-endpoints.md`, `diff.md`, ...).
- Pfad `src/ReceiptScanner.Api/Endpoints/` existiert im aktuellen Repository nicht; öffentliche HTTP-API bleibt unverändert.
- Lücke identifiziert: Es fehlte eine explizite technische Dokumentation des App-/Static-Asset-Contracts für das neue SVG-Favicon.

### 2) Flow-Docs-Analyse
- `docs/flows/` existiert mit umfangreichem Flow-Katalog.
- Lücke identifiziert: Kein dedizierter Ablauf für Favicon-Auslieferung (Head-Linkdefinition → Static Assets → Browser-Selektion/Fallback).

### 3) Business-Docs-Analyse
- `docs/business/` existiert; Feature-Katalog bis F024 war dokumentiert.
- Lücke identifiziert: Fachliche Beschreibung des neuen Branding-Favicons für Anwender:innen fehlte.

### 4) README-Analyse
- README-Struktur vollständig (Projektname, Features, Installation, Usage, Konfiguration, Architektur, Tests, Deployment, Lizenz, Changelog).
- Lücke identifiziert: Feature `favicon-hammer-pick-svg` und dessen Testabdeckung waren noch nicht enthalten.

## Phase 1 – Priorisierter Ausführungsplan
1. **Hoch:** Neue API-/Flow-/Business-Dokumente für das Favicon-Feature erstellen.
2. **Hoch:** README auf Feature, Tests und Dokumentationsverweise aktualisieren.
3. **Mittel:** API-Index klarstellen, dass keine neuen HTTP-Endpunkte entstanden sind.

## Phase 2 – Parallele Dokumentationserstellung
- `documentation-api`: ausgeführt
- `documentation-flow`: ausgeführt
- `documentation-business`: ausgeführt
- `documentation-readme-writer`: ausgeführt

## Anhang: Ergebnis (Phase 3)

### Neu erstellt
1. `docs/api/favicon-hammer-pick-svg.md`
2. `docs/flows/favicon-delivery-flow.md`
3. `docs/business/features/F025-favicon-hammer-pick-svg.md`

### Aktualisiert
1. `docs/api/README.md`
2. `docs/api/http-endpoints.md`
3. `docs/flows/README.md`
4. `docs/business/features.md`
5. `README.md`

### Validierung (Existenz + nicht leer)
- Erfolgreich für alle oben genannten neu erstellten/aktualisierten Dokumentdateien.

### Offene Punkte
- Die im Kontext genannten Planungsdokumente unter
  - `docs/requirements/favicon-hammer-pick-svg-requirements.md`
  - `docs/architecture/favicon-hammer-pick-svg-architecture-blueprint.md`
  - `docs/architecture/favicon-hammer-pick-svg-entity-relationship-model.md`
  - `docs/improvements/favicon-hammer-pick-svg-architecture-review.md`
  sind im aktuellen Stand nicht vorhanden und wurden in diesem Lauf nicht neu erstellt, da der Scope auf Dokumentationsorchestrierung der Zielbereiche `docs/api`, `docs/flows`, `docs/business` und `README.md` lag.

---

# Dokumentationsplan – Korrekte Diff-Anzeige im DiffViewer für geänderte Dateien – 2026-05-24

## Kontext
- Feature laut Auftrag umgesetzt: **Korrekte Diff-Anzeige im DiffViewer für geänderte Dateien**.
- Vorhandene Artefakte geprüft: `docs/requirements/diffviewer-correct-diff-display-*`, `docs/architecture/diffviewer-correct-diff-display-*`, `docs/tests/testplan-diffviewer-geaenderte-dateien.md`, `docs/tests/testluecken-diffviewer-geaenderte-dateien.md`.
- Agentendefinition `documentation-orchestrator` wurde befolgt; der vorgesehene Subagentenlauf war wegen API-Rate-Limit nicht verfügbar, daher manueller Vollablauf im selben Schema.

## Phase 1 – Analyse

### 1) API-Docs-Analyse
- `docs/api/` existiert und ist umfangreich dokumentiert.
- Spezifischer Soll-Ist-Abgleich zum Feature zeigte veraltete Formulierungen in `docs/api/diff-viewer.md` (globale `_latestDiffResultId`-Sicht statt dateispezifischer Vorschau-ID).
- Pfad `src/ReceiptScanner.Api/Endpoints/` existiert im Repository nicht; Endpoint-Inventar wurde stattdessen gegen bestehende Softwareschmiede-Struktur abgeglichen (öffentliche Diff-REST-Endpunkte unter `/api/diff` bleiben unverändert).

### 2) Flow-Docs-Analyse
- `docs/flows/` existiert inkl. `diffviewer-integration-flow.md`.
- Lücke: Ablaufbeschreibung spiegelte noch nicht vollständig die dateispezifische Auflösung (`ResolveSelectedWorkspaceDiffResultIdAsync` + `GetLatestDiffResultIdForFileAsync`) wider.

### 3) Business-Docs-Analyse
- `docs/business/` existiert mit Featurekatalog bis F024.
- Lücke: F022 beschrieb den Nutzen bereits, aber die präzise Aussage „Meldung nur wenn für ausgewählte Datei kein Diff existiert“ war nicht explizit genug.

### 4) README-Analyse
- README-Struktur vollständig (Projektname, Features, Installation, Usage, Konfiguration, Architektur, Tests, Deployment, Lizenz, Changelog).
- Lücken: dateispezifische Diff-Auflösung und neue DiffViewer-Testplan/-Lückenartefakte waren nicht vollständig in Features/Tests/Changelog integriert.

## Phase 1 – Priorisierter Ausführungsplan
1. **Hoch:** API-/Flow-Doku auf dateispezifische Diff-Zuordnung aktualisieren.
2. **Hoch:** README auf Feature- und Testartefaktstand synchronisieren.
3. **Mittel:** Business-Formulierungen zu F022 präzisieren und Querverweise konsistent halten.

## Ergebnis (Phase 3)

### Aktualisiert
1. `docs/api/diff-viewer.md`
2. `docs/api/README.md`
3. `docs/flows/diffviewer-integration-flow.md`
4. `docs/flows/README.md`
5. `docs/business/features/F022-diff-vergleichskomponente.md`
6. `docs/business/features.md`
7. `README.md`

### Validierung
- Existenz-/Nicht-Leerheitsprüfung der aktualisierten Dateien: **erfolgreich**.
- Konsistenzabgleich gegen implementierte Feature-Änderungen durchgeführt:
  - `AufgabeDetail`: `_selectedWorkspaceDiffResultId`, `ResolveSelectedWorkspaceDiffResultIdAsync`
  - `AufgabeService`: `GetLatestDiffResultIdForFileAsync` inkl. Pfadnormalisierung
  - Tests: `AufgabeServiceTests`, `AufgabeDetailWorkspacePreviewBunitTests`

### Offene Punkte
- Keine kritischen offenen Dokumentationslücken im Feature-Scope identifiziert.

---

# Dokumentationsplan – Diff-Funktionalität & ergänzte Tests – 2026-05-22

## Kontext
- Ursprüngliche Anforderung: `ae13a240-9557-470e-994b-8c550d843312.copilot-task.md`
- Planung, Implementierung und Testabdeckung wurden bereits umgesetzt.
- Ziel dieses Laufs: Vollständiger Dokumentationsabgleich mit dem implementierten Diff-Feature inkl. ergänzter Tests.

## Phase 1 – Analyse (Agentenschwarm)

### API (`docs/api/`)
- Bestand vorhanden: `README.md`, `http-endpoints.md`, `diff.md`, weitere API-Dokumente.
- Endpunkte in `DiffController` vollständig in `docs/api/http-endpoints.md` und `docs/api/diff.md` dokumentiert.
- **Lücke:** keine kritische API-Lücke identifiziert; Konsistenz-/Aktualitätsabgleich erforderlich.

### Flows (`docs/flows/`)
- Bestand mit zahlreichen Flows vorhanden.
- **Lücke (hoch):** kein dedizierter Ablaufplan für die Diff-Pipeline (`DiffController` + `DiffService` + `DiffAlgorithmService` + `DiffCachingService`) und keinen expliziten Test-/Validierungsfluss.

### Business (`docs/business/`)
- Feature-Dokumentation bis F021 vorhanden.
- **Lücke (hoch):** Diff-Funktionalität ist implementiert, aber noch nicht als eigenes Business-Feature dokumentiert.

### README
- Struktur vollständig vorhanden.
- **Lücken (hoch):** Diff-Funktionalität und zugehörige Testartefakte sind in Features/Usage/Tests nicht ausreichend abgebildet.
- **Bekannte Nebenpunkte (niedrig):** Lizenz weiterhin „zu definieren“, Changelog nur im README.

## Phase 1 – Priorisierter Ausführungsplan

### Neu zu erstellen
1. `docs/flows/diff-service-flow.md`
2. `docs/business/features/F022-diff-vergleichskomponente.md`

### Zu aktualisieren
1. `README.md` (Diff-Feature, Nutzung, Testabdeckung)
2. `docs/flows/README.md` (Verlinkung neuer Diff-Flow)
3. `docs/business/features.md` (Eintrag F022)
4. `docs/api/diff.md` / `docs/api/http-endpoints.md` (nur falls beim Abgleich Abweichungen auffallen)

### Priorität
1. **Hoch:** Business- und Flow-Dokumentation der Diff-Funktionalität
2. **Hoch:** README-Abgleich mit Diff und Tests
3. **Mittel:** API-Konsistenzprüfung und Querverweise

## Phase 2 – Ausführung
- Paralleler Lauf von `documentation-api`, `documentation-flow`, `documentation-business`, `documentation-readme-writer` mit diesem Plan als Kontext.

## Ergebnis

### Neu erstellt
1. `docs/flows/diff-service-flow.md`
2. `docs/business/features/F022-diff-vergleichskomponente.md`

### Aktualisiert
1. `README.md`
2. `docs/flows/README.md`
3. `docs/business/features.md`
4. `docs/api/diff.md`
5. `docs/api/http-endpoints.md`
6. `docs/api/README.md`

### Validierung
- Existenz- und Nicht-Leerheitsprüfung aller Zielartefakte erfolgreich.
- Flow-/Business-/README-Inhalte auf implementierte Diff-Funktionalität und vorhandene Testartefakte abgeglichen.

### Offene Punkte
1. Lizenz ist weiterhin nicht final festgelegt (`README.md`, Abschnitt Lizenz).
2. Es gibt weiterhin keine separate `CHANGELOG.md` (Changelog derzeit im README).
3. Fachlich/technisch optional: Fehlerklassifikation der Diff-API kann künftig präzisiert werden (z. B. „Aufgabe nicht gefunden“ aktuell als 500 aus Service-Exception).

---

# Dokumentationsplan – Feature „Benachrichtigungssystem für abgeschlossene KI-Aufgaben“ – 2026-05-23

## Kontext
- Feature-Status laut Auftrag: geplant, implementiert und testseitig ergänzt.
- Fokus dieses Laufs: ausschließlich dokumentationsrelevante Artefakte für das Benachrichtigungssystem.

## Phase 1 – Analyse (Agentenschwarm + Codeabgleich)

### API (`docs/api/`)
- `docs/api/` existiert; öffentliche HTTP-Endpunkte betreffen weiterhin nur `/api/diff`.
- Für das Benachrichtigungssystem wurden **keine neuen öffentlichen REST-Endpunkte** gefunden.
- Feature-relevanter API-Bedarf: technische Einordnung als interne App-/Service-Schnittstelle statt HTTP-API.

### Flows (`docs/flows/`)
- `docs/flows/` existiert, jedoch kein dedizierter Ablauf für:
  - Abschlussereignis-Publikation aus `EntwicklungsprozessService`
  - Hub-Verteilung (`KiAufgabenBenachrichtigungsHub`)
  - UI-Verarbeitung in `MainLayout` (Toast/Ton, Modusmatrix, Audit)
  - Einstellungen/Audio-Upload in `Einstellungen`
- **Lücke (hoch):** fehlender End-to-End-Flow für das Benachrichtigungssystem.

### Business (`docs/business/`)
- Feature-Katalog bis `F023` vorhanden.
- **Lücke (hoch):** kein fachlicher Feature-Eintrag für Benachrichtigungen bei abgeschlossenen KI-Aufgaben.

### README (`README.md`)
- Struktur vollständig; Featureliste enthält das Benachrichtigungssystem noch nicht.
- **Lücke (hoch):** fehlende Sichtbarkeit des Features in Features/Usage/Tests/Dokumentationslinks.

## Phase 1 – Priorisierter Ausführungsplan

### Neu zu erstellen
1. `docs/flows/benachrichtigungssystem-flow.md`
2. `docs/business/features/F024-benachrichtigungssystem-fuer-abgeschlossene-ki-aufgaben.md`

### Zu aktualisieren
1. `docs/flows/README.md` (Index + Kurzbeschreibung des neuen Flows)
2. `docs/business/features.md` (Eintrag F024)
3. `README.md` (Feature, Nutzung, Testhinweise, Dokumentationsverweise)
4. `docs/api/README.md` und/oder `docs/api/http-endpoints.md` (Klarstellung: keine neuen REST-Endpunkte für F024)
5. `docs/tests/README.md` (Referenz auf feature-relevante Testabdeckung)

### Priorität
1. **Hoch:** Business- und Flow-Doku (fachlich + technisch vollständig)
2. **Hoch:** README-Abgleich für Nutzer- und Projektübersicht
3. **Mittel:** API-/Testindex-Konsistenz für korrekte Erwartungshaltung

## Phase 2 – Ausführung
- Parallele Delegation an:
  - `documentation-api`
  - `documentation-flow`
  - `documentation-business`
  - `documentation-readme-writer`
- Kontext für alle Agenten: dieser Plan und die implementierten Code-/Testartefakte des Features.

## Ergebnis
- Parallele Ausführung der vier Dokumentationsagenten abgeschlossen.

### Neu erstellt
1. `docs/flows/benachrichtigungssystem-flow.md`
2. `docs/business/features/F024-benachrichtigungssystem-fuer-abgeschlossene-ki-aufgaben.md`

### Aktualisiert
1. `README.md`
2. `docs/api/README.md`
3. `docs/api/http-endpoints.md`
4. `docs/flows/README.md`
5. `docs/business/features.md`
6. `docs/tests/README.md`
7. `docs/documentation-plan.md`

### Validierung
- Alle genannten Artefakte existieren und sind nicht leer.
- Feature-relevante Aussagen wurden gegen implementierte Komponenten abgeglichen:
  - Event-Publikation: `EntwicklungsprozessService` -> `KiAufgabenBenachrichtigungsHub`
  - UI-Verarbeitung: `MainLayout` (Toast/Ton, Modusmatrix, Dedupe, Audit)
  - Einstellungs-/Audiofluss: `Einstellungen` + `BenachrichtigungsEinstellungenService` + `notifications.js`
  - Testabdeckung: `EntwicklungsprozessServiceTests`, `MainLayoutTests`, `BenachrichtigungsEinstellungenServiceTests`

### Offene Punkte
- Keine kritischen offenen Dokumentationslücken im Feature-Scope identifiziert.

---

# Dokumentationsplan – Vollständige Aktualisierung (Standardplugin & KI-Plugin-Auswahl) – 2026-05-12

## Kontext
- Fachliche Anforderung: `c11e26e2-b280-4797-95f3-58a8da6589f0.copilot-task.md`
- Ziel: Vollständige, konsistente Projektdokumentation aus API-, Business- und Flow-Sicht.
- Leitplanken: Bestehende Dokumentation ergänzen/aktualisieren; keine irrelevanten Codeänderungen.

## Phase 1 – Analyse

### API-Dokumentation (`docs/api/`)
- `docs/api/` existiert und enthält: `README.md`, `http-endpoints.md`, `plugin-interfaces.md`, `plugin-default-selection.md`, `workdir-configuration.md`.
- Öffentliche REST-/Minimal-API-Endpunkte sind derzeit nicht vorhanden; die API-Dokumentation deckt stattdessen Plugin-Verträge und interne Integrationsschnittstellen ab.
- Lücken/Verbesserungen:
  - Konsistenz- und Querverweisabgleich für das Feature „Standardplugin je Pluginart + KI-Plugin-Auswahl beim Prompt“.
  - Klarere Abgrenzung „keine HTTP-Endpunkte vorhanden“ im API-Index.

### Flow-Dokumentation (`docs/flows/`)
- `docs/flows/` existiert und enthält zentrale Flows.
- Lücken/Verbesserungen:
  - Dedizierter Flow für `ProjektService` fehlt.
  - Dedizierter Flow für `AgentPackageFileService` fehlt.
  - `GitOrchestrationService`-Abläufe sind nur teilweise in Sammelflows sichtbar.
  - Fallback-/Auflösungslogik für Pluginauswahl (explizite Auswahl → gespeichertes Standardplugin → Fallback) soll durchgängig und einheitlich beschrieben sein.

### Business-Dokumentation (`docs/business/`)
- Feature-Dokumentation F001–F014 ist vorhanden.
- Lücken/Verbesserungen:
  - Querschnittsbeschreibung zu Einstellungen/Persistenz und Plugin-Konfiguration ist verstreut.
  - Fehler-/Recovery-Sicht für Nutzer:innen und Admins ist nicht zentral dokumentiert.
  - Konsistente Verlinkung von F014 in Übersichten und angrenzenden Featureseiten sicherstellen.

### README (`README.md`)
- Grundstruktur ist vorhanden.
- Lücken/Verbesserungen:
  - Vollständigkeitsabgleich der Best-Practice-Abschnitte (Features, Installation, Usage, Konfiguration, Architektur, Tests, Deployment, Lizenz, Changelog).
  - Präzisere Beschreibung der Standardplugin-/KI-Auswahl inkl. Verweise auf API-, Business- und Flow-Dokumente.

## Phase 1 – Priorisierter Ausführungsplan

### Neu zu erstellen (falls noch nicht vorhanden)
1. `docs/flows/projekt-service-flow.md`
2. `docs/flows/agent-package-file-service-flow.md`
3. `docs/business/features/F015-einstellungen-und-persistenz.md`
4. `docs/business/features/F016-fehlerbehandlung-und-recovery.md`

### Zu aktualisieren
1. `README.md`
2. `docs/api/README.md`
3. `docs/api/plugin-default-selection.md`
4. `docs/api/plugin-interfaces.md`
5. `docs/api/http-endpoints.md`
6. `docs/flows/README.md`
7. `docs/flows/plugin-default-selection-flow.md`
8. `docs/flows/development-process-flow.md`
9. `docs/business/features.md`
10. `docs/business/features/F014-standardplugin-ki-plugin-auswahl.md`
11. `docs/user-guide.md` (falls erforderlich zur Konsistenz)

### Priorität
1. **Hoch:** Konsistenz rund um F014 in API/Flow/Business + README
2. **Hoch:** Fehlende Kernflows (`ProjektService`, `AgentPackageFileService`)
3. **Mittel:** Business-Querschnitt (Einstellungen/Persistenz, Recovery)
4. **Mittel:** Verlinkungen und Glossar-/Terminologieabgleich

### Leitplanken
- Bestehende Doku nicht löschen, nur ergänzen/aktualisieren.
- Einheitliche Terminologie: *Standardplugin*, *Pluginart*, *KI-Plugin-Auswahl*, *Fallback*.
- Konsistente Querverweise zwischen `docs/api/`, `docs/business/`, `docs/flows/` und `README.md`.

## Ergebnis (Phase 3)

### Neu erstellt
1. `docs/api/plugin-default-selection.md`
2. `docs/flows/projekt-service-flow.md`
3. `docs/flows/agent-package-file-service-flow.md`
4. `docs/flows/plugin-default-selection-flow.md`
5. `docs/business/features/F015-einstellungen-und-persistenz.md`
6. `docs/business/features/F016-fehlerbehandlung-und-recovery.md`

### Aktualisiert
1. `README.md`
2. `docs/api/README.md`
3. `docs/api/http-endpoints.md`
4. `docs/api/plugin-interfaces.md`
5. `docs/flows/README.md`
6. `docs/business/features.md`
7. `docs/business/features/F014-standardplugin-ki-plugin-auswahl.md`
8. `docs/business/features/F003-ki-entwicklungsprozess.md`
9. `docs/business/features/F009-arbeitsverzeichnis-konfigurieren.md`
10. `docs/business/features/F010-plugin-prinzip-integrationen.md`
11. `docs/business/features/F011-agent-auswahl-bei-folgeanweisungen.md`
12. `docs/business/features/F012-kontextsteuerung-folgeanweisungen.md`
13. `docs/business/features/F013-claude-cli-integration.md`

### Validierung
- Existenz- und Nicht-Leerheitsprüfung für alle Zielartefakte: **erfolgreich**.
- Relative Markdown-Linkprüfung für zentrale geänderte Dateien: **1 offener Verweis**.
  - `README.md -> docs/images/dashboard.png` (Datei nicht vorhanden).
- Dedizierter Doku-Linter/Doc-Test im Repository für Markdown nicht gefunden.

### Offene Punkte
1. Fehlende Screenshot-Datei `docs/images/dashboard.png` in `README.md`.
2. Optional: Einführung eines automatisierten Markdown-/Link-Lints in CI.

---

# Dokumentationsplan – Feature „Lokales Verzeichnis Plugin“ – 2026-05-12

## Kontext
- Feature wurde bereits geplant, implementiert und getestet.
- Relevante Planungsdokumente: `docs/requirements/`, `docs/architecture/`, `docs/improvements/`, insbesondere `docs/planning-overview-lokales-verzeichnis-plugin.md`.
- Ziel dieses Laufs: bestehende Projektdokumentation konsistent zum real implementierten Featurestand aktualisieren.

## Phase 1 – Analyse

### API-Dokumentation (`docs/api/`)
- `docs/api/` existiert.
- HTTP-Endpunkte sind weiterhin nicht vorhanden; API-Doku beschreibt primär Plugin-Verträge.
- Lücken für das Feature:
  - `LocalDirectoryPlugin` als `IGitPlugin`-Implementierung ist nicht ausreichend dokumentiert.
  - Einstellungen/Guardrails (`WorkspaceMode`, Pfade, Limits, Timeout) sind in API-Doku unvollständig.
  - NotSupported-Operationen des Plugins sind nicht klar beschrieben.

### Flow-Dokumentation (`docs/flows/`)
- `docs/flows/` existiert und enthält zentrale Flows.
- Feature-relevante Arbeitsverzeichnis-Flows sind vorhanden, aber Abgleich auf Plugin-spezifische Ablaufdarstellung nötig.
- Für diesen Lauf wird auf konsistente Darstellung des LocalDirectory-Ablaufs und seiner Workspace-Modi fokussiert.

### Business-Dokumentation (`docs/business/`)
- `docs/business/` existiert mit Feature-Dokumenten F001–F016.
- Kritische Lücke: kein eigener, klarer Business-Abschnitt für das Feature „Lokales Verzeichnis Plugin“ als nutzbares Produkt-Feature.
- Verknüpfung zu bereits vorhandenen Planungs-/Anforderungsdokumenten für das Feature muss verbessert werden.

### README (`README.md`)
- README-Struktur ist vorhanden.
- Feature-spezifische Lücken:
  - Featureliste nennt LocalDirectoryPlugin nicht oder nicht ausreichend.
  - Usage/Konfiguration enthalten keine klaren LocalDirectoryPlugin-Beispiele.
  - Architektur/Test/Changelog/Doc-Links sind nicht vollständig auf den Featurestand synchronisiert.

## Phase 1 – Priorisierter Ausführungsplan

### Neu zu erstellen (falls nicht vorhanden)
1. `docs/business/features/F017-lokales-verzeichnis-plugin.md` (fachliche Feature-Dokumentation)
2. `docs/api/local-directory-plugin.md` (technische API-/Plugin-Doku, sofern Detailtiefe in `plugin-interfaces.md` nicht ausreicht)

### Zu aktualisieren
1. `README.md`
2. `docs/api/plugin-interfaces.md`
3. `docs/api/README.md` (Index/Verweise)
4. `docs/api/http-endpoints.md` (Klarstellung Endpunktstatus, falls notwendig)
5. `docs/flows/workdir-resolution-flow.md` (Feature-Konsistenzcheck)
6. `docs/flows/README.md` (Verweise)
7. `docs/business/features.md`
8. `docs/business/features/F009-arbeitsverzeichnis-konfigurieren.md`
9. `docs/business/features/F010-plugin-prinzip-integrationen.md`
10. `docs/business/features/F017-lokales-verzeichnis-plugin.md` (nach Erstellung)

### Priorität
1. **Hoch:** README + API-Verträge zum LocalDirectoryPlugin
2. **Hoch:** Fachliche Feature-Doku (F017) inkl. klarer Nutzerperspektive
3. **Mittel:** Flow-/Index-/Verlinkungskonsistenz
4. **Mittel:** Ergänzende Klarstellungen zu Endpunktstatus

### Leitplanken
- Nur Dokumentationsänderungen.
- Keine Löschung bestehender Dokumentation; nur Ergänzen/Präzisieren.
- Einheitliche Terminologie: *LocalDirectoryPlugin*, *WorkspaceMode*, *InSourceDirectory*, *SeparateWorkingDirectory*.
- Aussagen ausschließlich auf Basis tatsächlicher Implementierung und vorhandener Tests.

## Ergebnis (Anhang, Phase 3)

### Neu erstellt
1. `docs/api/local-directory-plugin.md`
2. `docs/flows/local-directory-plugin-flow.md`
3. `docs/business/features/F017-lokales-verzeichnis-plugin.md`

### Aktualisiert
1. `README.md`
2. `docs/api/README.md`
3. `docs/api/plugin-interfaces.md`
4. `docs/flows/README.md`
5. `docs/flows/workdir-resolution-flow.md`
6. `docs/business/features.md`
7. `docs/business/features/F003-ki-entwicklungsprozess.md`
8. `docs/business/features/F009-arbeitsverzeichnis-konfigurieren.md`
9. `docs/business/features/F010-plugin-prinzip-integrationen.md`
10. `docs/business/features/F014-standardplugin-ki-plugin-auswahl.md`
11. `docs/business/features/F015-einstellungen-und-persistenz.md`
12. `docs/documentation-plan.md`

### Validierung
- Existenz- und Nicht-Leerheitsprüfung der erzeugten/aktualisierten Zielartefakte: **erfolgreich**.
- Fokusabgleich gegen Implementierung und Tests des Features „Lokales Verzeichnis Plugin“: **durchgeführt**.
- Von den Subagenten gemeldete Testläufe:
  - `dotnet build .\\Softwareschmiede.slnx` erfolgreich.
  - `dotnet test .\\Softwareschmiede.slnx` erfolgreich.
  - `dotnet test --filter "LocalDirectoryPlugin"` erfolgreich.

### Offene Punkte
- Keine kritischen offenen Dokumentationslücken für das Feature „Lokales Verzeichnis Plugin“ identifiziert.

---

# Dokumentationsplan – Feature „Übersetzte WorkspaceMode-Werte & dynamische Repository-Felder“ – 2026-05-13

## Kontext
- Feature bereits implementiert und mit ergänzten Tests abgesichert.
- Fokus:
  - Übersetzte `WorkspaceMode`-Enumwerte in den Einstellungen
  - Kein `WorkingDirectory`-Plugin-Setting mehr
  - `SourceDirectory` im Projekt-/Repository-Kontext
  - Plugin-gesteuerte dynamische Repository-Felder inkl. Standardplugin-Vorauswahl
  - GitHub-Felder `RepositoryUrl` und `RepositoryName`

## Phase 1 – Analyse

### API-Dokumentation (`docs/api/`)
- `local-directory-plugin.md` enthielt veralteten Eintrag `WorkingDirectory`.
- `plugin-interfaces.md` beschrieb `GetRepositoryLinkFields` im Interface nicht explizit.
- `README.md` in `docs/api/` nannte dynamische Repository-Feldschemata noch nicht.

### Flow-Dokumentation (`docs/flows/`)
- `local-directory-plugin-flow.md` referenzierte noch `WorkingDirectory` im Clone-Pfad.
- `projekt-service-flow.md` beschrieb Repository-Verknüpfung noch als starre Parameter statt plugin-gesteuertem Feldschema.

### Business-Dokumentation (`docs/business/`)
- `F009` erwähnte veraltet ein plugin-spezifisches `WorkingDirectory`.
- `F015` und `F017` deckten neue Aspekte (übersetzte WorkspaceMode-Labels, `SourceDirectory` als Repository-Linkfeld) noch nicht vollständig ab.

### README
- Enthielt noch `LocalDirectoryPlugin.WorkingDirectory`.
- Repository-Verknüpfung war nicht klar als plugin-gesteuertes dynamisches Feldschema beschrieben.

## Phase 1 – Priorisierter Ausführungsplan
1. **Hoch:** Veraltete `WorkingDirectory`-Aussagen in API/Flow/Business/README entfernen.
2. **Hoch:** Dynamische Repository-Feldlogik (`GetRepositoryLinkFields`, Default-SCM-Vorauswahl) in API/Flow/README dokumentieren.
3. **Mittel:** Fachliche Texte zu übersetzten WorkspaceMode-Labels und `SourceDirectory`-Nutzung präzisieren.

## Ergebnis (Phase 3)

### Aktualisiert
1. `docs/api/local-directory-plugin.md`
2. `docs/api/plugin-interfaces.md`
3. `docs/api/README.md`
4. `docs/flows/local-directory-plugin-flow.md`
5. `docs/flows/projekt-service-flow.md`
6. `docs/business/features/F009-arbeitsverzeichnis-konfigurieren.md`
7. `docs/business/features/F015-einstellungen-und-persistenz.md`
8. `docs/business/features/F017-lokales-verzeichnis-plugin.md`
9. `README.md`
10. `docs/documentation-plan.md`

### Validierung
- Alle aktualisierten Dateien vorhanden und nicht leer.
- Dokumentationsinhalte auf Implementierung abgeglichen:
  - `EinstellungenBase` (`WorkspaceModeDisplayLabels`, Enum-Rendering)
  - `ProjektDetail` (`GetRepositoryLinkFields`, Default-SCM-Auflösung)
  - `LocalDirectoryPlugin` (kein `WorkingDirectory`-Setting, `SourceDirectory`)
  - `GitHubPlugin` (`RepositoryUrl`, `RepositoryName`)

### Offene Punkte
- Keine fachlich kritischen offenen Dokumentationslücken im genannten Feature-Fokus identifiziert.

---

# Dokumentationsplan – Vollständige Orchestrierung (Agentenschwarm + Parallelisierung) – 2026-05-13

## Kontext
- Ausführung gemäß Agentendefinition `documentation-orchestrator`.
- Ziel: Vollständige Analyse und Aktualisierung der Projektdokumentation in `docs/api/`, `docs/flows/`, `docs/business/` sowie `README.md`.

## Phase 1 – Analyse

### API-Dokumentation (`docs/api/`)
- `docs/api/` existiert und enthält bereits Kernartefakte (`README.md`, `http-endpoints.md`, `plugin-interfaces.md`, `local-directory-plugin.md`, `plugin-default-selection.md`, `workdir-configuration.md`).
- Im Code wurden weiterhin keine öffentlichen HTTP-Endpoints identifiziert (Blazor/Razor Components statt REST-Controller/Minimal-API-Routen).
- Lücke: explizite, featurebezogene Klarstellung in `http-endpoints.md` zu neueren Features.

### Flow-Dokumentation (`docs/flows/`)
- `docs/flows/` existiert und enthält viele bestehende Flows.
- Lücken: keine dedizierten Flows für `GitOrchestrationService` und `KiAusfuehrungsService`; Querverweise aus vorhandenen Flows ausbaufähig.

### Business-Dokumentation (`docs/business/`)
- `docs/business/features.md` mit bestehendem Feature-Katalog vorhanden.
- Lücken: einzelne Feature-Seiten waren gegenüber der aktuellen UI/Prozessführung veraltet (u. a. F001, F002, F006, F008); Auto-Shutdown-Funktion fachlich noch nicht als eigenes Feature dokumentiert.

### README (`README.md`)
- Struktur grundsätzlich vollständig.
- Lücken: einzelne veraltete/inkonsistente Inhalte, inklusive früherer Bild-/Verweisprobleme und notwendiger Präzisierungen zum Implementierungsstatus.

## Phase 1 – Priorisierter Ausführungsplan
1. **Hoch:** Konsistenz zwischen realer Implementierung und API-/Business-/Flow-Dokumentation herstellen.
2. **Hoch:** Fehlende dedizierte Flows für zentrale Services ergänzen.
3. **Mittel:** README auf aktuellen Stand inkl. validierbarer lokaler Verweise bringen.
4. **Mittel:** Fachliche Abdeckung von Auto-Shutdown als eigenes Feature ergänzen.

## Ergebnis (Phase 3)

### Neu erstellt
1. `docs/flows/git-orchestration-service-flow.md`
2. `docs/flows/ki-ausfuehrungs-service-flow.md`
3. `docs/business/features/F018-automatisches-herunterfahren.md`

### Aktualisiert
1. `README.md`
2. `docs/api/http-endpoints.md`
3. `docs/flows/README.md`
4. `docs/flows/development-process-flow.md`
5. `docs/flows/auto-shutdown-orchestrator-flow.md`
6. `docs/business/features.md`
7. `docs/business/features/F001-projektverwaltung.md`
8. `docs/business/features/F002-aufgabenverwaltung.md`
9. `docs/business/features/F006-aufgabe-abschliessen.md`
10. `docs/business/features/F008-dashboard.md`

### Validierung
- Existenz- und Nicht-Leerheitsprüfung für alle in diesem Lauf erzeugten/aktualisierten Hauptartefakte: **erfolgreich**.
- API-Status geprüft: **keine öffentlichen HTTP-Endpunkte** weiterhin konsistent dokumentiert.

### Offene Punkte
1. `LICENSE`-Datei im Repository fehlt weiterhin (Lizenztext derzeit nur im README referenziert).
2. Optional: Dedizierte `CHANGELOG.md` ergänzen (aktuell Changelog-Abschnitt im README).

---

# Dokumentationsplan – Vollständige Orchestrierung (Aktueller Lauf) – 2026-05-13

## Kontext
- Ausführung gemäß Agentendefinition `documentation-orchestrator`.
- Ziel: Prüfen, ob die vorhandene Dokumentation aktuell und vollständig ist, und festgestellte Abweichungen schließen.

## Phase 1 – Analyse

### API-Dokumentation (`docs/api/`)
- `docs/api/` existiert und deckt die technischen Contracts sowie den expliziten Status „keine öffentlichen HTTP-Endpunkte“ ab.
- Keine fehlenden API-Seiten identifiziert.

### Flow-Dokumentation (`docs/flows/`)
- `docs/flows/` existiert und enthält die zentralen Service- und Prozessabläufe.
- Keine fehlenden Kernflows identifiziert.

### Business-Dokumentation (`docs/business/`)
- `docs/business/features.md` listet alle Features F001–F018.
- Keine fehlenden fachlichen Feature-Seiten identifiziert.

### README (`README.md`)
- Inhaltlich weitgehend vollständig.
- Auffälligkeit: veraltete Architektur-Bezeichnungen (`KiOrchestrationService`, `AgentPackageService`) und fehlender Link auf F018.

## Phase 1 – Priorisierter Ausführungsplan
1. README-Begriffe an die aktuelle Implementierung anpassen.
2. F018 im Dokumentationsindex sichtbar verlinken.

## Phase 3 – Ergebnis

### Aktualisiert
1. `README.md`
2. `docs/documentation-plan.md`

### Neu erstellt
- Keine neuen Dokumentationsdateien erforderlich.

### Offene Punkte
- Keine offenen Dokumentationslücken im Scope dieses Laufs identifiziert.

---

# Dokumentationsplan – Lokales Verzeichnis + Arbeitskopie-Aktionsmatrix (Aktueller Lauf) – 2026-05-14

## Kontext
- Feature-Fokus: `LocalDirectory + SeparateWorkingDirectory` soll in der UI **Push/Pull/PR ausblenden** und **Merge einblenden**.
- Technische Basis: Plugin liefert Capability-/Flag-Informationen (`GitActionCapabilities`) über den `IGitPlugin`-Contract.

## Phase 1 – Analyse

### API-Dokumentation (`docs/api/`)
- `plugin-interfaces.md` enthielt den erweiterten Contract (`GetGitActionCapabilitiesAsync`, `MergeToSourceAsync`) noch nicht vollständig.
- `local-directory-plugin.md` beschrieb Push/Pull-Sync bereits, aber die Capability-gesteuerte Aktionsmatrix fehlte.
- `http-endpoints.md` enthielt noch keinen expliziten Feature-Impact zu Capabilities/Flags.

### Flow-Dokumentation (`docs/flows/`)
- `local-directory-plugin-flow.md` hatte noch keine eigene Sequenz für Capabilities -> UI-Sichtbarkeit.
- `git-orchestration-service-flow.md` erwähnte die neue Capability-Abfrage und Merge-UI nur teilweise.

### Business-Dokumentation (`docs/business/`)
- `F017-lokales-verzeichnis-plugin.md` beschrieb Pull/Push-Sync, aber nicht die konkrete Button-Matrix in der Aufgabenansicht.

### README (`README.md`)
- Feature war technisch beschrieben, der explizite Hinweis auf die capability-gesteuerte Aktionsmatrix fehlte.

## Phase 1 – Priorisierter Ausführungsplan
1. **Hoch:** API-Contract-Dokumentation um Capabilities/Flags und Merge-Methode erweitern.
2. **Hoch:** Flow-Dokumentation um die UI-Sichtbarkeitslogik ergänzen.
3. **Mittel:** Business- und README-Texte um die konkrete Aktionsmatrix ergänzen.
4. **Mittel:** HTTP-Statusdokument um Nicht-HTTP-Charakter des Features ergänzen.

## Phase 3 – Ergebnis

### Aktualisiert
1. `docs/api/README.md`
2. `docs/api/plugin-interfaces.md`
3. `docs/api/local-directory-plugin.md`
4. `docs/api/http-endpoints.md`
5. `docs/flows/local-directory-plugin-flow.md`
6. `docs/flows/git-orchestration-service-flow.md`
7. `docs/business/features/F017-lokales-verzeichnis-plugin.md`
8. `README.md`
9. `docs/documentation-plan.md`

### Neu erstellt
- Keine neuen Dokumentationsdateien erforderlich.

### Validierung
- Alle aktualisierten Dateien vorhanden und nicht leer.
- Inhaltlicher Abgleich durchgeführt gegen:
  - `IGitPlugin` Contract (`GetGitActionCapabilitiesAsync`, `MergeToSourceAsync`)
  - `GitActionCapabilities` / `RepositoryKind`
  - `LocalDirectoryPlugin.GetGitActionCapabilitiesAsync`
  - `AufgabeDetail.EvaluateGitActionVisibility`

### Offene Punkte
- Keine kritischen Dokumentationslücken für den Feature-Fokus identifiziert.

---

# Dokumentationsplan – AufgabeDetail/GitOrchestrationService: projektspezifische IGitPlugin-Auswahl (Aktueller Lauf) – 2026-05-14

## Kontext
- Korrigierte/ergänzte Tests für `AufgabeDetail` und `GitOrchestrationService`.
- Fokus: korrekte projektspezifische `IGitPlugin`-Auswahl inklusive LocalRepository-/`LocalDirectoryPlugin`-Fall.

## Phase 1 – Analyse

### API-Dokumentation (`docs/api/`)
- `docs/api/plugin-default-selection.md` beschreibt nun explizit die Auflösungskette für Aufgabenaktionen:
  1. Aufgaben-Repository
  2. einzelnes aktives Projekt-Repository
  3. Standard-/Fallback-Plugin
- LocalRepository-/`LocalDirectoryPlugin`-Semantik ist im Auswahlkontext konkretisiert.

### Flow-Dokumentation (`docs/flows/`)
- `docs/flows/git-orchestration-service-flow.md` musste um die projektspezifische Plugin-Auflösung und den Mehrdeutigkeits-Fallback ergänzt werden.

### Business-Dokumentation (`docs/business/`)
- `docs/business/features/F017-lokales-verzeichnis-plugin.md` wurde um den Vorrang der projektspezifischen Plugin-Zuordnung vor dem globalen Standard ergänzt.

### Testdokumentation (`docs/tests/`)
- Für den konkreten Change-Fokus fehlte ein dedizierter Testplan mit Pflichtfällen für Service + UI.

## Phase 2 – Umsetzung

### Aktualisiert
1. `docs/flows/git-orchestration-service-flow.md`
2. `docs/business/features/F017-lokales-verzeichnis-plugin.md`
3. `docs/tests/README.md`
4. `docs/documentation-plan.md`

### Neu erstellt
1. `docs/tests/testplan-aufgabe-detail-project-selected-git-plugin.md`

## Phase 3 – Ergebnis / Validierung
- Existenz-/Nicht-Leerheitsprüfung der geänderten/neuen Dateien: durchgeführt.
- Build/Test-Validierung:
  - `dotnet build .\Softwareschmiede.slnx` erfolgreich
  - `dotnet test .\Softwareschmiede.slnx --no-build` erfolgreich

## Bekannte offene Punkte
- Bei mehreren aktiven Projekt-Repositories ohne Aufgabenverknüpfung bleibt der Standard-Fallback bewusst aktiv; eine mögliche spätere UX-Verbesserung wäre eine explizite Auswahlaufforderung statt Fallback.

---

# Dokumentationsplan – Feature „Issue-Auswahl, Branch-Verknüpfung und PR Auto-Close“ – 2026-05-14

## Kontext
- Feature bereits implementiert und mit Tests abgesichert.
- Scope:
  - Issue-Auswahl bei Aufgabenanlage
  - issuebezogene Branch-Namensbildung beim Prozessstart
  - PR-Closing-Direktive (`Closes #<Issue>`) für Auto-Close beim Merge

## Phase 1 – Analyse

### API-Dokumentation (`docs/api/`)
- `docs/api/` existiert, aber kein dediziertes Contract-Dokument für den End-to-End-Zusammenhang Issue -> Branch -> PR.
- `plugin-interfaces.md` dokumentierte `CreateBranchAsync` und `CreatePullRequestAsync`, aber ohne explizite Einordnung der Issue-bezogenen Namensbildung/Closing-Logik in der Orchestrierung.
- `http-endpoints.md` enthielt noch keinen expliziten Feature-Impact-Hinweis für dieses nicht-HTTP Feature.

### Flow-Dokumentation (`docs/flows/`)
- `docs/flows/` existiert, aber es fehlte ein dedizierter Ablaufplan für die komplette Featurekette.
- `development-process-flow.md` nutzte beim Branch-Format noch eine verkürzte Darstellung ohne Issue-Präfix.
- `git-orchestration-service-flow.md` beschrieb PR-Erstellung, aber nicht explizit die Conditional-Logik für die Closing-Direktive.

### Business-Dokumentation (`docs/business/`)
- Kein eigenes Fachdokument für das Feature vorhanden.
- F002/F006 beschrieben Teilaspekte, aber ohne vollständige End-to-End-Sicht für nicht-technische Stakeholder.

### README
- Feature- und Usage-Abschnitte erwähnten Issue-Import und Branches, aber nicht die vollständige Kette inklusive Auto-Close-Direktive.
- Dokumentationsindex enthielt noch keine direkten Links zu einem dedizierten API-/Flow-/Business-Dokument für dieses Feature.

## Phase 1 – Priorisierter Ausführungsplan
1. **Hoch:** Dedizierte API-, Flow- und Business-Dokumente für das Feature erstellen.
2. **Hoch:** README (Features, Usage, Doku-Index) auf die End-to-End-Kette synchronisieren.
3. **Mittel:** Bestehende Flow/API-Dokumente mit präzisen Hinweisen zur Branch-/Closing-Logik ergänzen.
4. **Mittel:** Testdokumentationsindex auf Feature-Testplan/Testlücken-Dokument erweitern.

## Ergebnis (Phase 3)

### Neu erstellt
1. `docs/api/issue-branch-pr-linking.md`
2. `docs/flows/issue-branch-pr-linking-flow.md`
3. `docs/business/features/F019-issue-branch-pr-verknuepfung.md`

### Aktualisiert
1. `README.md`
2. `docs/api/README.md`
3. `docs/api/plugin-interfaces.md`
4. `docs/api/http-endpoints.md`
5. `docs/flows/README.md`
6. `docs/flows/development-process-flow.md`
7. `docs/flows/git-orchestration-service-flow.md`
8. `docs/business/features.md`
9. `docs/business/features/F002-aufgabenverwaltung.md`
10. `docs/business/features/F006-aufgabe-abschliessen.md`
11. `docs/user-guide.md`
12. `docs/tests/README.md`
13. `docs/documentation-plan.md`

### Validierung
- Existenz- und Nicht-Leerheitsprüfung aller neu erstellten/aktualisierten Doku-Dateien: durchgeführt.
- Link-/Inhaltsabgleich gegen implementierten Code und Tests für:
  - `AufgabeService.CreateFromIssueAsync`
  - `EntwicklungsprozessService.ErstelleTaskBranchName`
  - `GitOrchestrationService.BuildPullRequestBody`
  - relevante Unit-/BUnit-Tests (`NeueAufgabeBunitTests`, `EntwicklungsprozessServiceTests`, `GitOrchestrationServiceTests`)

### Offene Punkte
- Keine kritischen offenen Dokumentationslücken für den Feature-Scope identifiziert.

---

# Dokumentationsplan – DiffViewer-Integration (Parameterwechsel-Stabilität, FR-4, Zustandsverantwortung, Route-Kompatibilität) – 2026-05-23

## Kontext
- Feature-Fokus: DiffViewer-Integration mit stabilen Parameterwechseln, FR-4-Fallback-Logik, klarer Zustandsverantwortung zwischen `AufgabeDetail` / `DiffPreviewPanel` / `DiffViewer` sowie kompatibler Route `/diff/{DiffResultId:guid}`.
- Implementierung und Tests wurden in Vorphasen bereits umgesetzt; dieser Lauf schließt den Dokumentationsabgleich.

## Phase 1 – Analyse (Agentenschwarm)

### API-Dokumentation (`docs/api/`)
- `diff-viewer.md` vorhanden, aber Verantwortungsschnitt und Dual-Mode (embedded/standalone) waren nicht ausreichend präzisiert.
- `docs/api/README.md` musste den Feature-Fokus auf die Integrationsaspekte erweitern.

### Flow-Dokumentation (`docs/flows/`)
- `diff-service-flow.md` deckt die Backend-Pipeline ab.
- Es fehlte ein dedizierter UI-Integrationsfluss für `AufgabeDetail -> DiffPreviewPanel -> DiffViewer` inklusive FR-4-Entscheidungslogik und Routenkompatibilität.

### Business-Dokumentation (`docs/business/`)
- `F022-diff-vergleichskomponente.md` deckte den generellen Diff-Nutzen ab, aber nicht klar genug die eingebettete Vorschau, Fallbacks und Zustandsgrenzen der Komponenten.

### README (`README.md`)
- Diff-Funktionsumfang vorhanden, aber Feature-Status und Testübersicht mussten um die Integrationsdetails (Dual-Mode, Parameterwechsel-Stabilität, neue bUnit-Tests) ergänzt werden.

## Phase 1 – Priorisierter Ausführungsplan
1. **Hoch:** API- und Flow-Dokumentation für Zustandsverantwortung, FR-4-Fallbacks und `/diff/{DiffResultId:guid}` konsistent machen.
2. **Hoch:** Business-Feature F022 und README an den finalen Integrationsstand anpassen.
3. **Mittel:** Indizes/Querverweise (`docs/api/README.md`, `docs/flows/README.md`) auf den neuen Integrationsflow erweitern.

## Phase 2 – Ausführung
- Parallele Delegation an `documentation-api`, `documentation-flow`, `documentation-business`, `documentation-readme-writer` mit diesem Plan als Kontext.

## Phase 3 – Ergebnis

### Neu erstellt
1. `docs/flows/diffviewer-integration-flow.md`

### Aktualisiert
1. `README.md`
2. `docs/api/README.md`
3. `docs/api/diff-viewer.md`
4. `docs/flows/README.md`
5. `docs/business/features/F022-diff-vergleichskomponente.md`
6. `docs/documentation-plan.md`

### Validierung
- Existenz- und Nicht-Leerheitsprüfung der neu erstellten/aktualisierten Dokumente erfolgreich.
- Inhaltsabgleich mit Implementierung durchgeführt (`AufgabeDetail`, `DiffPreviewPanel`, `DiffViewer`, `DiffViewerPage`):
  - Parameterwechsel-Stabilität (`OnParametersSetAsync`, Cancellation-/Version-Guards),
  - FR-4-Fallbackpfade im Vorschaupanel,
  - Route-Kompatibilität `/diff/{DiffResultId:guid}` via Wrapper.
- Basis-Testlauf wurde ausgeführt; bekannte bestehende Fehler in `Softwareschmiede.IntegrationTests` (LocalDirectoryPlugin) sind vorbestehend und nicht durch Dokumentationsänderungen verursacht.

### Offene Punkte
- Keine kritischen offenen Dokumentationslücken im Feature-Scope identifiziert.

---

# Dokumentationsplan – `start.ps1` für Visual-Studio-Debug mit freiem HTTP-Port – 2026-05-14

## Kontext
- Feature-Stand: Planung, Implementierung und Testabdeckung bereits abgeschlossen.
- Scope dieser Orchestrierung: technische + fachliche Dokumentation auf den finalen Featurestand bringen.
- Kernfokus: Nutzung von `start.ps1`, Portquellen-Priorität, Exit-Codes und Visual-Studio-Debug-Workflow.

## Phase 1 – Analyse

### API-Dokumentation (`docs/api/`)
- `docs/api/repository-startskript-freier-port.md` beschreibt den Gesamtvertrag für Startskripte im Prozessstart.
- Lücke: dedizierter Skriptvertrag für den direkten lokalen `start.ps1`-Aufruf (inkl. Priorität/Exit-Codes) fehlte.

### Flow-Dokumentation (`docs/flows/`)
- `repository-startskript-freier-port-flow.md` deckt primär den orchestrierten Aufgabenprozess ab.
- Lücke: kein separater Ablaufplan für den direkten `start.ps1`-Pfad bis zum Visual-Studio-F5-Start.

### Business-Dokumentation (`docs/business/`)
- `F020` ist vorhanden und beschreibt den Funktionsbereich.
- Lücke: konkrete Kurz-Anleitung für lokalen VS-Debug über `start.ps1` fehlte.

### README (`README.md`)
- Feature war grundsätzlich erwähnt.
- Lücke: kein klarer, kompakter Skriptvertrag in der Usage-Sektion (Aufruf, Priorität, Exit-Codes, Workflow).

## Phase 1 – Priorisierter Ausführungsplan
1. **Hoch:** Dedizierten API-Vertrag für `start.ps1` erstellen.
2. **Hoch:** Dedizierten Flow für `start.ps1`-Ablauf erstellen.
3. **Hoch:** README-Usage um klare Bedien- und Diagnoseinformationen ergänzen.
4. **Mittel:** F020 und Dokumentationsindizes um direkte Querverweise ergänzen.

## Ergebnis (Phase 3)

### Neu erstellt
1. `docs/api/start-ps1-visual-studio-freier-http-port.md`
2. `docs/flows/start-ps1-visual-studio-freier-http-port-flow.md`

### Aktualisiert
1. `README.md`
2. `docs/api/README.md`
3. `docs/api/repository-startskript-freier-port.md`
4. `docs/flows/README.md`
5. `docs/business/features/F020-repository-startskript-freier-port.md`
6. `docs/documentation-plan.md`

### Validierung
- Alle neu erstellten/aktualisierten Zielartefakte vorhanden und nicht leer.
- Konsistenzabgleich gegen Implementierung durchgeführt (`start.ps1`, `Softwareschmiede.csproj` linked item).
- Build/Test-Baselines im Repository erneut geprüft (`dotnet build`, `dotnet test` erfolgreich; `dotnet format --verify-no-changes` mit bestehendem, nicht-featurebezogenem Whitespace-Fehler in `plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.cs`).

### Offene Punkte
- Keine offenen Dokumentationslücken im Scope dieses Features identifiziert.

---

# Dokumentationsplan – Feature „Repository-Startskript mit freier Portzuweisung“ (Aktueller Lauf) – 2026-05-14

## Kontext
- Fortsetzung des Lifecycle-Prozesses nach Implementierung und erweiterter Testabdeckung.
- Feature-Fokus: `repository-startskript-freier-port`.
- Vorhandene Planungs-/Review-/Testartefakte wurden als Kontext einbezogen:
  - `docs/requirements/repository-startskript-freier-port-requirements-analysis.md`
  - `docs/architecture/repository-startskript-freier-port-architecture-blueprint.md`
  - `docs/architecture/repository-startskript-freier-port-entity-relationship-model.md`
  - `docs/improvements/repository-startskript-freier-port-architecture-review.md`
  - `docs/tests/testplan-repository-startskript-freier-port.md`
  - `docs/tests/testluecken-repository-startskript-freier-port.md`

## Phase 1 – Analyse

### API-Dokumentation (`docs/api/`)
- `docs/api/` existiert; für das neue Feature fehlte ein dedizierter technischer Contract.
- `http-endpoints.md` benötigte einen expliziten Nicht-HTTP-Feature-Impact-Eintrag.

### Flow-Dokumentation (`docs/flows/`)
- `docs/flows/` existiert; für Startkonfiguration -> Portreservierung -> Skriptausführung fehlte ein eigener Ablaufplan.

### Business-Dokumentation (`docs/business/`)
- Feature-Katalog endete bei `F019`; eine fachliche Seite für das neue Feature fehlte.

### README (`README.md`)
- Feature war im zentralen Projektüberblick, Tests und Dokumentationsindex noch nicht vollständig verlinkt.

## Phase 1 – Priorisierter Ausführungsplan
1. **Hoch:** API-/Flow-/Business-Artefakte für das Feature neu erstellen.
2. **Hoch:** Indizes (`docs/api`, `docs/flows`, `docs/business`, `docs/tests`) ergänzen.
3. **Mittel:** README auf aktuellen Feature- und Teststand synchronisieren.
4. **Mittel:** `docs/documentation-plan.md` mit Ergebnisanhang aktualisieren.

## Phase 3 – Ergebnis

### Neu erstellt
1. `docs/api/repository-startskript-freier-port.md`
2. `docs/flows/repository-startskript-freier-port-flow.md`
3. `docs/business/features/F020-repository-startskript-freier-port.md`

### Aktualisiert
1. `README.md`
2. `docs/api/README.md`
3. `docs/api/http-endpoints.md`
4. `docs/flows/README.md`
5. `docs/business/features.md`
6. `docs/tests/README.md`
7. `docs/documentation-plan.md`

### Validierung
- Existenz und Nicht-Leerheit aller neu erstellten/aktualisierten Dokumentationsdateien geprüft.
- Inhaltsabgleich gegen implementierten Code-Stand durchgeführt (u. a. `PortReservationService`, `RepositoryStartskriptService`, `ProjektService`, `ProjektDetail`, `EntwicklungsprozessService`, Migration `RepositoryStartKonfiguration`).
- Testartefakte für das Feature in den Dokumentationsindizes verknüpft.

### Offene Punkte
- Keine kritischen offenen Dokumentationslücken für den Feature-Scope identifiziert.
