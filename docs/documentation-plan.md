# Dokumentationsplan – Feature `startdevelopmentasync-test-overload-removal`

## Phase 1 – Analyse

### API-Dokumentation
- `docs/api/` ist vorhanden und gepflegt.
- Das in der Agentendefinition genannte Verzeichnis `src/ReceiptScanner.Api/Endpoints/` existiert in diesem Repository nicht.
- Es existieren aktuell keine öffentlichen REST-/Minimal-API-Endpunkte; der Status ist bereits in `docs/api/http-endpoints.md` dokumentiert.
- Lücke für dieses Feature: In den technischen Vertragsdokumenten fehlte die explizite Aussage, dass nur noch die kanonische `StartDevelopmentAsync(..., model, executionId, ct)`-Signatur gilt (kein test-spezifischer Kurz-Overload).

### Flow-Dokumentation
- `docs/flows/` ist vorhanden.
- Kernabläufe sind dokumentiert, inkl. Copilot-Task-Flow.
- Lücke für dieses Feature: Flow-Notation verwendete teils noch `executionId?` und benannte die Vertragskonsolidierung nicht explizit.

### Business-Dokumentation
- `docs/business/` ist vorhanden und deckt die Produktfunktionen vollständig ab.
- Fachlich kein neues Endnutzer-Feature; es handelt sich um API-/Testkonsolidierung.
- Lücke für dieses Feature: In der Endnutzerbeschreibung fehlte ein kurzer Hinweis, dass sich für Anwender trotz interner Signaturbereinigung nichts ändert.

### README-Analyse
- README ist strukturell vollständig (Projektname, Features, Installation, Usage, Konfiguration, Architektur, Tests, Deployment, Lizenz, Changelog).
- Lücke für dieses Feature: Changelog/Feature-Hinweise erwähnten die Signaturkonsolidierung noch nicht explizit.

## Phase 1 – Priorisierter Ausführungsplan

### Neu zu erstellen
- Keine neuen Zielbereiche erforderlich (Feature-Artefakte liegen bereits in Requirements/Architecture/ERM/Review/Planning vor).

### Zu aktualisieren
1. `docs/api/plugin-interfaces.md`
2. `docs/api/copilot-task-binding.md`
3. `docs/flows/copilot-task-binding-flow.md`
4. `docs/flows/development-process-flow.md`
5. `docs/business/features/F003-ki-entwicklungsprozess.md`
6. `README.md`
7. `docs/documentation-plan.md`

---

## Ergebnis (Phase 3)

### Aktualisiert
- `docs/api/plugin-interfaces.md`
- `docs/api/copilot-task-binding.md`
- `docs/flows/copilot-task-binding-flow.md`
- `docs/flows/development-process-flow.md`
- `docs/business/features/F003-ki-entwicklungsprozess.md`
- `README.md`
- `docs/documentation-plan.md`

### Bereits vorhanden und als Kontext genutzt
- `docs/requirements/startdevelopmentasync-test-overload-removal-requirements-analysis.md`
- `docs/architecture/startdevelopmentasync-test-overload-removal-architecture-blueprint.md`
- `docs/architecture/startdevelopmentasync-test-overload-removal-entity-relationship-model.md`
- `docs/improvements/startdevelopmentasync-test-overload-removal-architecture-review.md`
- `docs/planning/startdevelopmentasync-test-overload-removal-planning-overview.md`
- `docs/api/plugin-interfaces.md`
- `docs/api/copilot-task-binding.md`

### Validierung
- Baseline vor Änderungen:
  - `dotnet build .\Softwareschmiede.slnx --nologo` ❌ (50 bestehende Compile-Fehler außerhalb dieser Doku-Aufgabe)
- Nach Doku-Änderungen:
  - `dotnet build .\Softwareschmiede.slnx --nologo` ❌ (unverändert gleiche Baseline)

### Offene Punkte
- Keine offenen inhaltlichen Dokumentationslücken für das Feature `startdevelopmentasync-test-overload-removal`.
- Repository-Build ist aktuell unabhängig von dieser Doku-Aufgabe bereits fehlerhaft (bestehende Baseline-Probleme).
