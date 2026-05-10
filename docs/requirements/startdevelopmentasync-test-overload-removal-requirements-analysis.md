# Anforderungsanalyse: Entfernung des test-spezifischen `StartDevelopmentAsync`-Overloads

## 1. Zielbild und Problem
- Ist: In `GitHubCopilotPlugin.cs` existieren zwei Overloads von `StartDevelopmentAsync`.
- Ist: Der kürzere Overload wird hauptsächlich in Tests genutzt.
- Problem: Doppelter API-Vertrag erhöht Wartungsaufwand und führt zu inkonsistenten Aufrufmustern.
- Ziel: Tests auf den kanonischen Overload mit `executionId` umstellen, damit der kürzere Overload entfallen kann.

## 2. Scope
### In Scope
- Anpassung von Tests, insbesondere `src/Softwareschmiede.Tests/Infrastructure/Plugins/GitHubCopilotPluginTests.cs`.
- Sicherstellung identischen Verhaltens nach Overload-Entfernung.
- Dokumentierte Traceability zwischen Requirements, Blueprint, ERM und Review.

### Out of Scope
- Funktionale Erweiterungen des Copilot-Workflows.
- Änderungen am fachlichen Prozess außerhalb von `StartDevelopmentAsync`.

## 3. Funktionale Anforderungen
| ID | Anforderung | Priorität |
|---|---|---|
| FR-1 | Tests verwenden ausschließlich den kanonischen Aufruf `StartDevelopmentAsync(prompt, agent, localRepoPath, model, executionId, ct)`. | MUST |
| FR-2 | Der kürzere Overload ist für erfolgreiche Testausführung nicht mehr erforderlich. | MUST |
| FR-3 | Verhalten bei `executionId == null` bleibt unverändert (interne Generierung/Normalisierung). | MUST |
| FR-4 | Fehler- und Cleanup-Pfade bleiben unverändert abgesichert. | MUST |
| FR-5 | Bestehende Assertions zu CLI-Argumenten, Prompt-Datei und `.gitignore` bleiben fachlich gültig. | MUST |

## 4. Nicht-funktionale Anforderungen
| ID | Anforderung |
|---|---|
| NFR-1 | Keine Regression im Laufzeitverhalten durch Signaturkonsolidierung. |
| NFR-2 | Tests bleiben deterministisch und stabil. |
| NFR-3 | API-Wartbarkeit verbessert sich durch eine eindeutige Signatur. |
| NFR-4 | Änderungen sind durchgehend nachvollziehbar dokumentiert (Traceability). |

## 5. Akzeptanzkriterien (Given/When/Then)
### AC-1: Testsignatur konsolidiert
**Given** die Datei `GitHubCopilotPluginTests.cs`  
**When** nach `StartDevelopmentAsync`-Aufrufen gesucht wird  
**Then** wird nur noch der kanonische Overload mit `executionId` verwendet.

### AC-2: Overload entfernbar
**Given** alle Tests sind auf die kanonische Signatur umgestellt  
**When** der kürzere Overload entfernt wird  
**Then** bleiben Build und Tests erfolgreich.

### AC-3: Null-Semantik bleibt gleich
**Given** `executionId` ist `null`  
**When** `StartDevelopmentAsync` ausgeführt wird  
**Then** bleibt das bisherige Verhalten zur ID-Erzeugung und Dateinamensbildung erhalten.

### AC-4: Fehlerpfade bleiben erhalten
**Given** ungültige `executionId` oder I/O-Fehler  
**When** der Entwicklungsstart ausgeführt wird  
**Then** greifen weiterhin die bisherigen Validierungs- und Fehlerpfade.

## 6. Testanforderungen
### Fokusdatei
- `src/Softwareschmiede.Tests/Infrastructure/Plugins/GitHubCopilotPluginTests.cs`

### Muss geprüft werden
1. Alle bisherigen Kurzaufrufe werden auf Aufrufe mit `executionId` (typisch `null`) umgestellt.
2. Cancellation-Aufrufe nutzen die Parameterreihenfolge `(model, executionId, ct)`.
3. Bestehende Assertions für:
   - Prompt-Datei (`*.copilot-task.md`)
   - CLI-Argumente (`--prompt @...`)
   - `.gitignore`-Synchronisierung
   - Cleanup im `finally`
   bleiben grün.
4. Negativtests zu ungültiger `executionId` bleiben vorhanden.

## 7. Blocker
- **B-01:** API-Vertragsentscheidung muss final synchron für `IKiPlugin` und `GitHubCopilotPlugin` getroffen werden (eine Signatur).
  - **Nächster Schritt:** Entscheidung im Blueprint festschreiben und anschließend Umsetzung/Testmigration durchführen.

## 8. Traceability
- Architektur: `../architecture/startdevelopmentasync-test-overload-removal-architecture-blueprint.md`
- ERM: `../architecture/startdevelopmentasync-test-overload-removal-entity-relationship-model.md`
- Review: `../improvements/startdevelopmentasync-test-overload-removal-architecture-review.md`
- Primärquelle: `../../5c8cddbe-82b3-4072-b7a2-4c2bfd66732a.copilot-task.md`
