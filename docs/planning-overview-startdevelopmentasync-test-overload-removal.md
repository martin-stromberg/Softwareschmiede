# Planungsübersicht – StartDevelopmentAsync Test-Overload Removal

> **Dokument-Typ:** Planungsübersicht (Orchestrator-Ergebnis)  
> **Status:** ✅ Planungsphase abgeschlossen  
> **Datum:** 2026-05-10

## 1. Anlass
In `GitHubCopilotPlugin.cs` existieren zwei `StartDevelopmentAsync`-Methoden.  
Die kürzere Methode wird nur von Tests genutzt und soll entfallen. Dafür müssen die Tests auf die kanonische Signatur migriert werden.

## 2. Erstellte Artefakte
| Dokument | Zweck | Link |
|---|---|---|
| Requirements Analysis | Ziele, Scope, FR/NFR, ACs | [requirements/startdevelopmentasync-test-overload-removal-requirements-analysis.md](requirements/startdevelopmentasync-test-overload-removal-requirements-analysis.md) |
| Architektur-Blueprint | Zielvertrag, Migrationsstrategie, Risiken | [architecture/startdevelopmentasync-test-overload-removal-architecture-blueprint.md](architecture/startdevelopmentasync-test-overload-removal-architecture-blueprint.md) |
| ERM | Modell der Signaturkonsolidierung | [architecture/startdevelopmentasync-test-overload-removal-entity-relationship-model.md](architecture/startdevelopmentasync-test-overload-removal-entity-relationship-model.md) |
| Architektur-Review | Priorisierte Findings und Maßnahmen | [improvements/startdevelopmentasync-test-overload-removal-architecture-review.md](improvements/startdevelopmentasync-test-overload-removal-architecture-review.md) |
| Planning-Unterordner | kompakte Planungszusammenfassung | [planning/startdevelopmentasync-test-overload-removal-planning-overview.md](planning/startdevelopmentasync-test-overload-removal-planning-overview.md) |

## 3. Kernentscheidungen
1. Einheitlicher API-Vertrag: nur noch die kanonische Signatur mit `executionId`.
2. Reihenfolge ist verbindlich: Testmigration vor Overload-Entfernung.
3. Verhaltensgleichheit (`executionId == null`, Cleanup, CLI-Args, Fehlerpfade) bleibt Pflicht.
4. Umsetzung wird durch bestehenden Testbestand abgesichert.

## 4. Blocker
- **Aktuell kein Blocker in der Planungsphase.**
- Nächster konkreter Schritt: Implementierungsphase starten und zuerst Testaufrufe auf die kanonische Signatur umstellen.
