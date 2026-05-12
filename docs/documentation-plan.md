# Dokumentationsplan – Vollständige Aktualisierung (claude-cli-integration) – 2026-05-12

## Phase 1 – Analyse

### API-Dokumentation (`docs/api/`)
- `docs/api/` existiert und ist grundsätzlich konsistent.
- Öffentliche HTTP-Endpunkte sind weiterhin nicht vorhanden; die API-Dokumentation fokussiert korrekt auf Plugin-Verträge.
- Lücke: Claude-CLI-Integration soll in den API-nahen Plugin-Dokumenten explizit als unterstützte KI-Plugin-Implementierung referenziert werden.

### Flow-Dokumentation (`docs/flows/`)
- `docs/flows/` existiert mit mehreren Kernabläufen.
- Lücken:
  - Kein dedizierter Ablauf für `AufgabeService`-Statusübergänge.
  - Kein dedizierter Ablauf für `AutoShutdownOrchestrator`.
  - Kein dedizierter Ablauf für `PluginSettingsService`.
  - Claude-CLI-Ausführung soll in den KI-relevanten Flows explizit sichtbar sein.

### Business-Dokumentation (`docs/business/`)
- `docs/business/features/` enthält F001–F012.
- Lücken:
  - Keine eigene fachliche Feature-Seite für `claude-cli-integration`.
  - Plugin-Einstellungen/Konfiguration sind fachlich nicht vollständig beschrieben.
  - Benutzerleitfaden benötigt Ergänzung zur Nutzung/Einrichtung von Claude CLI.

### README (`README.md`)
- Struktur ist vorhanden, aber `claude-cli-integration` ist unvollständig bzw. nicht durchgängig eingebunden.
- Lücken betreffen insbesondere Features, Voraussetzungen, Konfiguration, Projektstruktur, Roadmap und Dokumentationsverweise auf Testartefakte.

## Phase 1 – Priorisierter Ausführungsplan

### Neu zu erstellen
1. `docs/business/features/F013-claude-cli-integration.md`
2. `docs/flows/aufgabe-service-status-flow.md`
3. `docs/flows/auto-shutdown-orchestrator-flow.md`
4. `docs/flows/plugin-settings-service-flow.md`

### Zu aktualisieren
1. `README.md`
2. `docs/api/README.md`
3. `docs/api/plugin-interfaces.md`
4. `docs/api/http-endpoints.md`
5. `docs/flows/README.md`
6. `docs/flows/follow-up-context-steering-flow.md`
7. `docs/business/features.md`
8. `docs/user-guide.md`

### Priorität
1. **Hoch:** README, `F013-claude-cli-integration.md`, `docs/business/features.md`
2. **Hoch:** KI-/Plugin-Flows inkl. Claude-CLI-Bezug
3. **Mittel:** API-Docs-Abgleich/Verweise
4. **Mittel:** User-Guide-Ergänzungen

### Leitplanken
- Bestehende Dokumentation nicht löschen, nur ergänzen/aktualisieren.
- Fokus auf konsistente End-to-End-Dokumentation des Features `claude-cli-integration`.
- Querverweise zwischen `docs/requirements/`, `docs/architecture/`, `docs/improvements/`, `docs/tests/` herstellen.

## Ergebnis (Phase 3)

### Neu erstellt
1. `docs/business/features/F013-claude-cli-integration.md`
2. `docs/flows/aufgabe-service-status-flow.md`
3. `docs/flows/auto-shutdown-orchestrator-flow.md`
4. `docs/flows/plugin-settings-service-flow.md`

### Aktualisiert
1. `README.md`
2. `docs/api/README.md`
3. `docs/api/plugin-interfaces.md`
4. `docs/api/http-endpoints.md`
5. `docs/flows/README.md`
6. `docs/flows/follow-up-context-steering-flow.md`
7. `docs/business/features.md`
8. `docs/user-guide.md`

### Validierung
- Alle im Plan geforderten Zielartefakte wurden auf Existenz und Nicht-Leerheit geprüft.
- Build-/Testlauf wurde gestartet:
  - `dotnet build .\Softwareschmiede.slnx --nologo`
  - `dotnet test .\Softwareschmiede.slnx --nologo --verbosity minimal`
- Der Lauf blieb in der Umgebung bei `Softwareschmiede.IntegrationTests net10.0 / IncludeTransitiveProjectReferences` ohne Abschluss hängen und wurde nach Wartezeit beendet.

### Offene Punkte
- Inhaltlich sind die geplanten Dokumentationslücken für `claude-cli-integration` geschlossen.
- Technische Build-/Test-Stabilisierung in der aktuellen Arbeitskopie bleibt separat zu klären.
