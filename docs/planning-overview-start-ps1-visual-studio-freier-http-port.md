# Planungsübersicht – `start.ps1` (parameterlos, autonome Webprojekt-Erkennung)

> **Dokument-Typ:** Planungsoverview (planning-orchestrator)  
> **Status:** ✅ Vollständiger Planungsdurchlauf abgeschlossen  
> **Version:** 1.1.0  
> **Datum:** 2026-05-14

---

## 1. Durchlaufstatus

Der Ablauf aus `planning-orchestrator.agent.md` wurde vollständig durchgeführt:
1. Anforderungen analysiert und auf Breaking Change aktualisiert
2. Architektur-Blueprint auf parameterlosen Mehrprojekt-Ansatz aktualisiert
3. ERM auf nicht-persistentes Laufzeitmodell aktualisiert
4. Architecture-Review mit priorisierten Findings und Auflagen erstellt
5. Flow-Dokument auf parameterlosen Startvertrag synchronisiert
6. Konsolidierung und Verlinkung abgeschlossen

---

## 2. Artefakte

| Bereich | Datei | Stand |
|---|---|---|
| Anforderungen | [docs/requirements/start-ps1-visual-studio-freier-http-port-requirements-analysis.md](requirements/start-ps1-visual-studio-freier-http-port-requirements-analysis.md) | v1.1.0 |
| Architektur | [docs/architecture/start-ps1-visual-studio-freier-http-port-architecture-blueprint.md](architecture/start-ps1-visual-studio-freier-http-port-architecture-blueprint.md) | v1.1.0 |
| ERM | [docs/architecture/start-ps1-visual-studio-freier-http-port-entity-relationship-model.md](architecture/start-ps1-visual-studio-freier-http-port-entity-relationship-model.md) | v1.1.0 |
| Review | [docs/improvements/start-ps1-visual-studio-freier-http-port-architecture-review.md](improvements/start-ps1-visual-studio-freier-http-port-architecture-review.md) | v1.1.0 |
| Flow | [docs/flows/start-ps1-visual-studio-freier-http-port-flow.md](flows/start-ps1-visual-studio-freier-http-port-flow.md) | synchronisiert |

---

## 3. Konsolidierte Kernentscheidungen

1. Das Startskript ist fachlich **parameterlos** (`.\start.ps1`).
2. Relevante Web-Projekte werden **autonom** über `launchSettings.json` + `profiles.http` erkannt.
3. Portvergabe erfolgt pro Zielprojekt im Skript; die Anwendung bleibt **skript-agnostisch**.
4. Exit-Codes und Diagnostik werden projektbezogen erzeugt und laufweit aggregiert.
5. Es sind **keine Persistenz- oder Migrationsänderungen** erforderlich.

---

## 4. Konsolidierte Risiken und Auflagen

- TOCTOU-Risiko zwischen Portermittlung und Start bleibt als Restrisiko bestehen.
- Discovery-Regeln, Teilfehler-Policy und Exit-Code-Aggregation müssen implementierungsseitig strikt eingehalten werden.
- Atomisches Schreiben und Fehlerpfade (`10/11/12/13`) sind testseitig vollständig nachzuweisen.

---

## 5. Nächster Schritt

Implementierungsphase gemäß Planungsartefakten starten:
1. `start.ps1` auf parameterlosen Mehrprojektvertrag umbauen
2. `RepositoryStartskriptService` entkoppeln (keine Portsteuerparameter)
3. Tests auf neuen Vertrag umstellen
4. Flow-Dokument ist auf v1.1 synchronisiert
