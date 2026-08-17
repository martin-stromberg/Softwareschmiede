# Aufgabenliste – Anforderungsbearbeitung

Branch: `task/issue-204-eab6dccb4b304e1a94ef7c2417f9b297-ide-plugins`

| Status | Schritt | Beschreibung | Artefakt |
|--------|---------|--------------|----------|
| [x] | 1 | Branch-Name ermitteln | – |
| [x] | 2 | Verzeichnisstruktur vorbereiten | `docs/features/{branchname}/` |
| [x] | – | Einstiegspunkt ermitteln | – |
| [x] | 3 | Anforderung übersetzen (Unteragent) | `requirement.md` |
| [x] | 4 | Bestandsaufnahme (Unteragent) | `inventory.md`, `inventory/` |
| [x] | 5 | Umsetzungsplanung (Unteragent) | `plan.md` |
| [x] | 5a | Offene Punkte prüfen und ggf. Planung wiederholen | `plan.md` (aktualisiert) |
| [x] | 5b | Planungscommit | – |
| [x] | 6 | Implementierung (Unteragent) | Codeänderungen (Iteration 3: 4 Review-Befunde behoben, IdeOeffnenService entfernt) |
| [x] | 7 | Plan-Review (Unteragent, bedingt) | `review.md` (bereits Vollständig umgesetzt — wird übersprungen) |
| [x] | 8 | Code-Review (Unteragent) | `review-code.md` (Iteration 3: 5 Befunde, davon 1 kritisch — KannIdeAuswaehlen nie beim View-Load gesetzt) |
| [x] | 8b | Tests ausführen (Unteragent) | `test-results.md` (Iteration 3: Keine Fehler laut Testlauf; ConPTY/FlaUI-E2E-Tests hier sandboxbedingt übersprungen, siehe review-code.md Befund 1+2) |
| [x] | – | Iteration oder Abschluss entscheiden | Iterationszähler = 3 erreicht → Schleife abgebrochen |
| [x] | 8a | Folgeaufgaben dokumentieren (bei Schleifenabbruch) | `continue.md` |
| [x] | 9 | Dokumentation erstellen (Unteragent) | `docs/help/` |
| [x] | 9b | README aktualisieren (Unteragent) | `README.md` (Fehler entdeckt: dokumentierte bereits entfernte Property `VerfuegbareEinstiegspunkte` — als Nacharbeit in `continue.md` erfasst) |
| [ ] | 10 | Nacharbeiten abschließen (offene Punkte aus `continue.md`) | `continue-done.md` |
| [ ] | – | Feature-Verzeichnis löschen | – |
| [ ] | – | Commit durchführen | – |
