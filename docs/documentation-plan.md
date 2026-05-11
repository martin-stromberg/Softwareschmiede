# Dokumentationsplan – Kontextsteuerung bei Folgeanweisungen (2026-05-11)

## Phase 1 – Analyse

### API-Dokumentation (`docs/api/`)
- Verzeichnis ist vorhanden und aktuell.
- Es existieren keine öffentlichen HTTP-Endpunkte; die Funktion betrifft UI/Application-Logik.
- Lücke: expliziter Verweis auf Kontextsteuerung (kein API-Impact) in der API-Übersicht ergänzen.

### Flow-Dokumentation (`docs/flows/`)
- Bestehende Flows decken Entwicklungsprozess, Plugin-Discovery und Workdir ab.
- Lücke: Der Ablauf der Kontextsteuerung bei Folgeanweisungen (Kontext mitgeben / ignorieren / neu beginnen) ist nicht als eigener Flow dokumentiert.

### Business-Dokumentation (`docs/business/`)
- F011 (Agent-Auswahl bei Folgeanweisungen) existiert.
- Lücke: Fachliche Nutzerdoku für die neue Kontextsteuerung fehlt als eigenständige Feature-Seite.

### README (`README.md`)
- README ist umfangreich, erwähnt Folgeanweisungen und Agentenauswahl.
- Lücke: Kontextmodus-Auswahl und Verhalten sind noch nicht klar und konsistent als implementiertes Feature beschrieben.

### Referenzartefakte
- Anforderungen, Architektur, Improvements und Tests für Kontextsteuerung liegen bereits vor:
  - `docs/requirements/kontextsteuerung-folgeanweisungen-requirements-analysis.md`
  - `docs/architecture/kontextsteuerung-folgeanweisungen-architecture-blueprint.md`
  - `docs/improvements/kontextsteuerung-folgeanweisungen-architecture-review.md`
  - `docs/tests/testplan-kontextsteuerung-folgeanweisungen.md`
  - `docs/tests/testluecken-kontextsteuerung-folgeanweisungen.md`

## Phase 1 – Priorisierter Ausführungsplan

### Zu erstellen
1. `docs/flows/follow-up-context-steering-flow.md`
2. `docs/business/features/F012-kontextsteuerung-folgeanweisungen.md`

### Zu aktualisieren
1. `README.md`
2. `docs/business/features.md`
3. `docs/business/features/F003-ki-entwicklungsprozess.md`
4. `docs/flows/README.md`
5. `docs/api/README.md`
6. `docs/api/http-endpoints.md`

### Leitplanken
- Keine Redundanz zu Anforderungen/Architektur/Tests; diese werden referenziert.
- Konsistente Benennung und bestehende Repository-Struktur beibehalten.
- Feature-Status als implementiert und testseitig erweitert dokumentieren.

## Ergebnis (Phase 3)

### Erstellt
1. `docs/flows/follow-up-context-steering-flow.md`
2. `docs/business/features/F012-kontextsteuerung-folgeanweisungen.md`

### Aktualisiert
1. `README.md`
2. `docs/api/README.md`
3. `docs/api/http-endpoints.md`
4. `docs/api/plugin-interfaces.md`
5. `docs/business/features.md`
6. `docs/business/features/F003-ki-entwicklungsprozess.md`
7. `docs/flows/README.md`

### Validierung
- Alle in Phase 2 erzeugten/angepassten Doku-Dateien existieren und sind nicht leer.
- Querverweise auf Requirements/Architektur/Review/Tests sind gesetzt, ohne inhaltliche Duplizierung der Spezifikationsdetails.
- Repository-Build wurde zur Baseline-Prüfung ausgeführt: `dotnet build .\Softwareschmiede.slnx --nologo` (fehlgeschlagen wegen vorbestehender, dokumentationsunabhängiger Compilerfehler im Codebestand).

### Offene Punkte
- Keine offenen Dokumentationslücken für das Feature innerhalb von API-, Flow-, Business- und README-Dokumentation.
- Technische Build-Fehler im Projekt bleiben separat im Code-Track zu beheben.
