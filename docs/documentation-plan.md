# Dokumentationsplan – KI-Arbeitsprotokoll als Markdown (2026-05-11)

## Phase 1 – Analyse

### API-Dokumentation (`docs/api/`)
- `docs/api/` ist vorhanden und beschreibt Plugin-Schnittstellen sowie den Status ohne öffentliche HTTP-Endpunkte.
- Für das umgesetzte Feature wurden keine REST-Endpunkte ergänzt.
- Lücke: Explizite technische Einordnung des neuen Protokollformats (Markdown mit Datumszeile, Schritttrennung, Rendering-Sanitizing-Fallback) in der API-Dokumentation ist noch nicht konsistent verankert.

### Flow-Dokumentation (`docs/flows/`)
- Vorhandene Flows decken Entwicklungsprozess, Kontextsteuerung, Plugin-Discovery und Workdir ab.
- Lücke: Es fehlt ein dedizierter Ablaufplan für die Rendering-Pipeline des KI-Arbeitsprotokolls (Persistierung → Markdown-Render → Sanitizing → Fallback).

### Business-Dokumentation (`docs/business/`)
- Funktionsübersicht und Feature-Seiten F001–F012 sind vorhanden.
- Lücke: In `F005-aufgabenprotokoll.md` fehlt die verständliche Beschreibung des strukturierten Markdown-Formats inkl. Datumszeile, Schritttrennung und sicherer Webdarstellung.

### README (`README.md`)
- README ist umfangreich und enthält bereits Struktur zu Features, Architektur, Tests und Changelog.
- Lücke: Das neu umgesetzte Protokoll-Feature ist im Feature-/Doku-Teil noch nicht ausdrücklich und vollständig beschrieben.

### Einbezogene Referenzartefakte
- Implementierung:
  - `src/Softwareschmiede/Application/Services/EntwicklungsprozessService.cs`
  - `src/Softwareschmiede/Components/Pages/Aufgaben/AufgabeDetail.razor.cs`
- Tests:
  - `src/Softwareschmiede.Tests/Application/Services/EntwicklungsprozessServiceTests.cs`
  - `src/Softwareschmiede.Tests/Components/Pages/Aufgaben/AufgabeDetailFolgePromptTests.cs`

## Phase 1 – Priorisierter Ausführungsplan

### Zu erstellen
1. `docs/flows/ki-arbeitsprotokoll-rendering-flow.md`

### Zu aktualisieren
1. `docs/api/README.md`
2. `docs/api/http-endpoints.md`
3. `docs/business/features/F005-aufgabenprotokoll.md`
4. `docs/flows/README.md`
5. `README.md`

### Priorität
1. **Hoch:** `docs/business/features/F005-aufgabenprotokoll.md`, `docs/flows/ki-arbeitsprotokoll-rendering-flow.md`
2. **Mittel:** `README.md`, `docs/flows/README.md`
3. **Mittel:** `docs/api/README.md`, `docs/api/http-endpoints.md`

### Leitplanken
- Bestehende Dokumentation nicht löschen, nur ergänzen/aktualisieren.
- Keine Duplikation von Quellcode; stattdessen strukturierte, wartbare Referenzdoku mit gezielten Dateiverweisen.
- Konsistente Terminologie: „KI-Arbeitsprotokoll“, „Markdown-Rendering“, „Sanitizing“, „Fallback“.

## Ergebnis (Phase 3)

### Erstellt
1. `docs/flows/ki-arbeitsprotokoll-rendering-flow.md`

### Aktualisiert
1. `docs/api/README.md`
2. `docs/api/http-endpoints.md`
3. `docs/business/features/F005-aufgabenprotokoll.md`
4. `docs/flows/README.md`
5. `README.md`

### Validierung
- Alle geplanten Artefakte existieren und sind nicht leer.
- Querverweise zwischen API-, Flow-, Business- und README-Dokumentation sind gesetzt.
- Repository-Validierung ausgeführt:
  - `dotnet build .\Softwareschmiede.slnx --nologo`
  - `dotnet test .\Softwareschmiede.slnx --nologo --verbosity minimal`
- Ergebnis der technischen Validierung: fehlgeschlagen aufgrund vorbestehender, dokumentationsunabhängiger Compilerfehler im aktuellen Codebestand (u. a. fehlende Typen/Namespaces wie `IGitPlugin`, `IKiPlugin`, `AgentInfo`, `ICliRunner`).

### Offene Punkte
- Für das dokumentierte Feature „KI-Arbeitsprotokoll als Markdown“ bestehen in den Zielartefakten keine offenen Dokumentationslücken.
- Technische Build-Probleme sind separat im Code-Track zu beheben.
