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
