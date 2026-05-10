# Dokumentationsplan – Feature „konfigurierbares Arbeitsverzeichnis für lokale Repositories“

## Phase 1 – Analyse

### API-Doku
- `docs/api/` existiert.
- Bestehende API-/Schnittstellen-Doku war vorhanden, aber um das neue Workdir-Feature zu ergänzen.
- Bedarf:
  - Ergänzung einer dedizierten technischen Feature-Doku.
  - Konsistenzupdate bestehender API-/Plugin-Referenzen.

### Flow-Doku
- `docs/flows/` existiert.
- Bestehende Prozessbeschreibung war vorhanden, aber ohne vollständigen Workdir-Auflösungsfluss.
- Bedarf:
  - Ergänzung eines eigenen Workdir-Resolution-Flows.
  - Verlinkung/Konsistenz mit bestehendem Entwicklungsprozess-Flow.

### Business-Doku
- `docs/business/` existiert.
- Fachliche Feature-Übersicht vorhanden, Feature „konfigurierbares Arbeitsverzeichnis“ fehlte als eigener Eintrag.
- Bedarf:
  - Neues Fachfeature-Dokument.
  - Aktualisierung verwandter Feature-Beschreibungen.

### README
- `README.md` vorhanden.
- Feature/Verhalten war nicht vollständig und konsistent in Konfiguration/Usage reflektiert.
- Bedarf:
  - Ergänzung/Konsolidierung der Hinweise zum konfigurierbaren Arbeitsverzeichnis.

## Phase 1 – Priorisierter Ausführungsplan

### Neu zu erstellen
1. `docs/api/workdir-configuration.md`
2. `docs/flows/workdir-resolution-flow.md`
3. `docs/business/features/F009-arbeitsverzeichnis-konfigurieren.md`

### Zu aktualisieren
1. `README.md`
2. `docs/api/README.md`
3. `docs/api/plugin-interfaces.md`
4. `docs/flows/README.md`
5. `docs/flows/development-process-flow.md`
6. `docs/business/features.md`
7. `docs/business/features/F001-projektverwaltung.md`
8. `docs/business/features/F002-aufgabenverwaltung.md`
9. `docs/business/features/F003-ki-entwicklungsprozess.md`
10. `docs/user-guide.md`
11. `docs/requirements.md`
12. `docs/architecture/architecture-blueprint.md`
13. `docs/architecture/entity-relationship-model.md`
14. `docs/tests/testplan-arbeitsverzeichnis.md`
15. `docs/tests/testluecken-arbeitsverzeichnis.md`

---

## Ergebnis (Phase 3)

### Erstellt
- `docs/api/workdir-configuration.md`
- `docs/flows/workdir-resolution-flow.md`
- `docs/business/features/F009-arbeitsverzeichnis-konfigurieren.md`

### Aktualisiert
- `README.md`
- `docs/api/README.md`
- `docs/api/plugin-interfaces.md`
- `docs/flows/README.md`
- `docs/flows/development-process-flow.md`
- `docs/business/features.md`
- `docs/business/features/F001-projektverwaltung.md`
- `docs/business/features/F002-aufgabenverwaltung.md`
- `docs/business/features/F003-ki-entwicklungsprozess.md`
- `docs/user-guide.md`
- `docs/requirements.md`
- `docs/architecture/architecture-blueprint.md`
- `docs/architecture/entity-relationship-model.md`
- `docs/tests/testplan-arbeitsverzeichnis.md`
- `docs/tests/testluecken-arbeitsverzeichnis.md`

### Validierung
- Dateiexistenz geprüft (neu erstellte Dokumente vorhanden und nicht leer).
- Konsistenzprüfung der Verlinkung/Terminologie innerhalb der betroffenen Dokumente durchgeführt.
- Projekt-Tests angestoßen (`dotnet test src/Softwareschmiede.Tests --nologo`): aktuell fehlschlagend in bestehenden, nicht-dokumentationsspezifischen Tests.

### Offene Punkte
- Keine offenen Dokumentationspunkte für das Workdir-Feature identifiziert.
- Bekannte Testfehler liegen außerhalb der reinen Dokumentationsänderungen.
