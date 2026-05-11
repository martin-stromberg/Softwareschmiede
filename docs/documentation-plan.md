# Dokumentationsplan – Feature „Agent-Auswahl bei Folgeanweisungen“ (2026-05-11)

## Phase 1 – Analyse

### API-Dokumentation (`docs/api/`)
- Vorhanden, aber keine öffentliche HTTP-API für dieses Feature betroffen.
- Für das Feature **keine API-Interface- oder Endpoint-Änderung** erforderlich.
- Maßnahme: Nur Konsistenzhinweis im Abschlussbericht.

### Flow-Dokumentation (`docs/flows/`)
- `docs/flows/development-process-flow.md` beschreibt KI-Start und Streaming, aber nicht explizit die neue Agentenwahl bei Folgeanweisungen.
- Lücke: Standardwert „Initial-Agent“, Änderbarkeit vor Senden und Rücksetzen nach Senden fehlen.

### Business-Dokumentation (`docs/business/`)
- `F003 – KI-Entwicklungsprozess` erwähnt Folge-Anweisungen, aber ohne präzise Regeln zur Agenten-Auswahl.
- Lücke: Verhalten bei Folgeanweisung nicht ausreichend fachlich beschrieben.

### README-Analyse
- README enthält Folge-Prompts und Agenten-Auswahl allgemein.
- Lücke: Feature-Details zu Folgeanweisungen (Default = Initial-Agent, manuelle Änderung, Versand an tatsächlich ausgewählten Agenten) fehlen.

### Anforderungen / Architektur / Improvements
- In `docs/requirements/requirements-analysis.md` und `docs/architecture/architecture-blueprint.md` sind Folge-Prompts bereits allgemein beschrieben.
- Lücke: Kein dediziertes Feature-Dokument mit den fünf Akzeptanzkriterien auf implementiertem Stand.
- In `docs/improvements/` fehlt ein gezieltes Architektur-Review für das neue Verhalten.

## Phase 1 – Priorisierter Ausführungsplan

### Neu zu erstellen (Priorität hoch)
1. `docs/requirements/agent-selection-follow-up-prompts-requirements-analysis.md`
2. `docs/architecture/agent-selection-follow-up-prompts-architecture-blueprint.md`
3. `docs/improvements/agent-selection-follow-up-prompts-architecture-review.md`

### Zu aktualisieren (Priorität hoch)
1. `docs/requirements/requirements-analysis.md` (UC-4 / ACs präzisieren)
2. `docs/architecture/architecture-blueprint.md` (Aufgabe-Detailseite / Folge-Prompt-Verhalten präzisieren)
3. `docs/business/features/F003-ki-entwicklungsprozess.md` (fachliche Bedienlogik erweitern)
4. `docs/flows/development-process-flow.md` (Ablaufdiagramm + Schritte für Folgeanweisung ergänzen)
5. `README.md` (Feature- und Usage-Abschnitt aktualisieren)

### Optional / prüfend
- `docs/api/*`: keine inhaltliche Änderung geplant, nur Validierung „kein API-Impact“.

## Ergebnis (Phase 3)

### Neu erstellt
1. `docs/requirements/agent-selection-follow-up-prompts-requirements-analysis.md`
2. `docs/architecture/agent-selection-follow-up-prompts-architecture-blueprint.md`
3. `docs/improvements/agent-selection-follow-up-prompts-architecture-review.md`
4. `docs/business/features/F011-agent-auswahl-bei-folgeanweisungen.md`

### Aktualisiert
1. `README.md`
2. `docs/api/README.md`
3. `docs/api/http-endpoints.md`
4. `docs/api/plugin-interfaces.md`
5. `docs/flows/development-process-flow.md`
6. `docs/flows/README.md`
7. `docs/business/features.md`
8. `docs/business/features/F003-ki-entwicklungsprozess.md`
9. `docs/requirements.md`
10. `docs/requirements/requirements-analysis.md`
11. `docs/architecture/architecture-blueprint.md`

### Validierung
- Alle geplanten Doku-Dateien existieren und sind nicht leer.
- Akzeptanzkriterien 1–5 sind in Requirements-, Architektur-, Flow-, Business- und README-Doku konsistent nachgeführt.
- API-Dokumentation enthält expliziten Hinweis „kein API-Impact“ für dieses Feature.

### Offene Punkte
- Keine weiteren Änderungen erforderlich.

---

# Dokumentationsplan – Vollständige Aktualisierung (Stand Repository)

## Phase 1 – Analyse

### API-Dokumentation
- `docs/api/` ist vorhanden und enthält technische Vertragsdokumente.
- Im aktuellen Codebestand gibt es keine öffentlichen REST-/Minimal-API-Endpunkte (`Program.cs` nutzt `MapRazorComponents`).
- Lücke: Explizite Dokumentation „keine HTTP-Endpunkte vorhanden“ fehlte.

### Flow-Dokumentation
- `docs/flows/` ist vorhanden und deckt Kernabläufe ab (Entwicklungsprozess, Workdir, Plugin-Discovery).
- Lücke: CLI-Referenzen waren teilweise veraltet (alte Copilot-CLI-Bezeichnung statt `copilot`).

### Business-Dokumentation
- `docs/business/` ist vorhanden und deckt die Funktionsbereiche F001–F010 ab.
- Lücke: technische Begriffe (CLI-Bezeichnungen) mussten mit der Implementierung konsolidiert werden.

### README-Analyse
- README ist umfangreich und deckt Features, Installation, Konfiguration, Architektur, Tests und Doku-Matrix ab.
- Lücken:
  - expliziter Abschnitt **Usage** fehlte,
  - expliziter Abschnitt **Deployment** fehlte,
  - expliziter Abschnitt **Changelog** fehlte,
  - einzelne CLI-Bezeichnungen waren veraltet.

### Referenzdokumente (requirements/architecture/improvements/tests)
- Dokumente sind vorhanden und strukturiert.
- Lücke: inkonsistente CLI-Namen sowie einzelne veraltete Technologieangaben (`.NET 9+`) in Architekturstrang.

## Phase 1 – Priorisierter Ausführungsplan

### Neu zu erstellen
1. `docs/api/http-endpoints.md`

### Zu aktualisieren
1. `README.md`
2. `docs/api/README.md`
3. `docs/flows/development-process-flow.md`
4. `docs/requirements.md`
5. `docs/requirements/requirements-analysis.md`
6. `docs/architecture/architecture-blueprint.md`
7. `docs/architecture/entity-relationship-model.md`
8. `docs/improvements/architecture-review.md`
9. `docs/planning-overview.md`
10. `docs/lifecycle-report-softwareschmiede.md`

---

## Ergebnis (Phase 3)

### Erstellt
- `docs/api/http-endpoints.md`

### Aktualisiert
- `README.md`
  - TOC erweitert um **Usage**, **Deployment**, **Changelog**
  - neue Abschnitte für Nutzung, Deployment und Changelog ergänzt
  - CLI-Bezeichnungen mit Implementierung synchronisiert (`copilot`)
- `docs/api/README.md`
  - neue HTTP-Endpunkt-Statusdoku verlinkt
- `docs/flows/development-process-flow.md`
  - CLI-Referenz auf `copilot` konsolidiert
- `docs/requirements.md`
- `docs/requirements/requirements-analysis.md`
- `docs/architecture/architecture-blueprint.md`
- `docs/architecture/entity-relationship-model.md`
- `docs/improvements/architecture-review.md`
- `docs/planning-overview.md`
- `docs/lifecycle-report-softwareschmiede.md`
  - CLI-Bezeichnungen und technische Terminologie auf aktuellen Implementierungsstand gebracht

### Validierung
- Baseline vor Änderungen erfolgreich:
  - `dotnet build .\Softwareschmiede.slnx --nologo`
  - `dotnet test .\src\Softwareschmiede.Tests\Softwareschmiede.Tests.csproj --nologo`
  - `dotnet test .\src\Softwareschmiede.IntegrationTests\Softwareschmiede.IntegrationTests.csproj --nologo`
- Nach Änderungen erneut ausgeführt, weiterhin erfolgreich.

### Offene Punkte
- Einige ältere Strategie-/Planungsdokumente führen weiterhin historische Statusmarkierungen (z. B. „Entwurf/Geplant“), obwohl die Implementierung inzwischen vorliegt. Diese Inhalte sind fachlich nutzbar, sollten aber in einem separaten Harmonisierungslauf vollständig auf „Ist-Stand“ umgestellt werden.
