# Aufgabenliste – Anforderungsbearbeitung

Branch: `task/issue-98-b8ff2fdc38d04d51a51d41d9a97da10e-unterverzeichnis-fuer-ki-ausfu`

| Status | Schritt | Beschreibung | Artefakt |
|--------|---------|--------------|----------|
| [x] | 1 | Branch-Name ermitteln | – |
| [x] | 2 | Verzeichnisstruktur vorbereiten | `docs/features/{branchname}/` |
| [x] | – | Einstiegspunkt ermitteln | – (continue.md vorhanden → Einstieg bei Schritt 6) |
| [x] | 3 | Anforderung übersetzen (Unteragent) | `requirement.md` (bereits in Vorlauf erledigt, Artefakt in Git-Historie unter 8c99b6b) |
| [x] | 4 | Bestandsaufnahme (Unteragent) | `inventory.md`, `inventory/` (bereits in Vorlauf erledigt, Artefakt in Git-Historie unter 8c99b6b) |
| [x] | 5 | Umsetzungsplanung (Unteragent) | `plan.md` (bereits in Vorlauf erledigt, Artefakt in Git-Historie unter 8c99b6b) |
| [x] | 5a | Offene Punkte prüfen und ggf. Planung wiederholen | `plan.md` (aktualisiert) (bereits in Vorlauf erledigt) |
| [x] | 5b | Planungscommit | – (bereits erfolgt: Commit 8c99b6b) |
| [x] | 6 | Implementierung (Unteragent) | Codeänderungen (verifiziert: Build 0 Fehler, Dateien vorhanden) |
| [x] | 7 | Plan-Review (Unteragent, bedingt) | `review.md` (Status: Offene Aufgaben vorhanden, 1 Punkt) |
| [x] | 8 | Code-Review (Unteragent) | `review-code.md` (Status: Befunde vorhanden, 5 Punkte) |
| [x] | 8b | Tests ausführen (Unteragent) | `test-results.md` (763 Tests, 757 bestanden, 6 fehlgeschlagen — teils verifiziert als Flaky/E2E-Timing) |
| [x] | – | Iteration oder Abschluss entscheiden | Iteration 1 abgeschlossen, Fortschritt möglich → Iteration 2 |
| [x] | 6 (Iter. 2) | Implementierung Review-Befunde (Unteragent) | Codeänderungen (verifiziert: Build 0 Fehler, Nicht-E2E-Tests grün bis auf 2 vorbestehende) |
| [x] | 7 (Iter. 2) | Plan-Review (Unteragent) | `review.md` (Status: Offene Aufgaben vorhanden, 1 neuer Punkt: ValidateWorkingDirectoryAfterCloneAsync nicht verdrahtet) |
| [x] | 8 (Iter. 2) | Code-Review (Unteragent) | `review-code.md` (Status: Befunde vorhanden, 1 mittel + 3 niedrig) |
| [x] | – | Iteration oder Abschluss entscheiden (nach Iter. 2) | Fortschritt erkannt (6 → 5 offene Punkte) → Iteration 3; alte `continue.md`-Punkte erledigt → `continue-done.md` |
| [x] | 6 (Iter. 3) | Implementierung Review-Befunde (direkt, Umfang überschaubar) | Codeänderungen (verifiziert: Build 0 Fehler) |
| [x] | 7 (Iter. 3) | Plan-Review (Unteragent) | `review.md` (Status: Vollständig umgesetzt) |
| [x] | 8 (Iter. 3) | Code-Review (Unteragent) | `review-code.md` (1 Befund gefunden, direkt behoben + verifiziert → Status: Keine Befunde) |
| [x] | 8b (Iter. 3) | Tests ausführen | `test-results.md` (768 Tests, 741 bestanden, 27 fehlgeschlagen — 26 umgebungsbedingt/flaky verifiziert, 1 echter vorbestehender Bug dokumentiert) |
| [x] | – | Iteration oder Abschluss entscheiden (nach Iter. 3) | Iterationszähler = 3 (letzte erlaubte Iteration) → Schleife abgebrochen, weiter mit Schritt 8a |
| [x] | 8a | Folgeaufgaben dokumentieren (bei Schleifenabbruch) | `continue.md` (1 vorbestehender, nicht in Iteration 3 behobener Bug: `WorkingDirectoryResolver` inkompatibel mit `LocalDirectoryPlugin`-`InSourceDirectory`-Modus; verbleibende Testfehlschläge als umgebungsbedingt klassifiziert) |
| [x] | 9 | Dokumentation erstellen | `docs/help/projekte/dialog-arbeitsverzeichnis-bearbeiten.md` (neu), `dialog-repository-auswahl.md`, `plugins/api.md` aktualisiert |
| [x] | 9b | README aktualisieren | `README.md` (Issue-98-Abschnitt und Arbeitsverzeichnis-Anleitung aktualisiert) |
| [ ] | – | Feature-Verzeichnis löschen | – |
| [ ] | 10 | Nacharbeiten abschliessen, offene Punkte aus `continue.md` | `continue-done.md` |
| [ ] | – | Commit durchführen | – |
