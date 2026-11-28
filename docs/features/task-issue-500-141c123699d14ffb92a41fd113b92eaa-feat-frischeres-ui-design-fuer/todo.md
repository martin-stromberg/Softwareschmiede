# Aufgabenliste – Anforderungsbearbeitung

Branch: `task/issue-500-141c123699d14ffb92a41fd113b92eaa-feat-frischeres-ui-design-fuer`

| Status | Schritt | Beschreibung | Artefakt |
|--------|---------|--------------|----------|
| [x] | 1 | Branch-Name ermitteln | – |
| [x] | 2 | Verzeichnisstruktur vorbereiten | `docs/features/{branchname}/` |
| [x] | – | Einstiegspunkt ermitteln | – |
| [x] | 3 | Anforderung übersetzen (Unteragent) | `requirement.md` |
| [x] | 4 | Bestandsaufnahme (Unteragent) | `inventory.md`, `inventory/` |
| [x] | 5 | Umsetzungsplanung (Unteragent) | `plan.md` |
| [ ] | 5a | Offene Punkte prüfen und ggf. Planung wiederholen | `plan.md` (aktualisiert) |
| [ ] | 5b | Plan gegen Anforderung und Testbedarf prüfen (Unteragent) | `plan-check.md` |
| [ ] | 5c | Planungscommit | – |
| [ ] | 6 | Implementierung (Unteragent) | Design-Mockups + Screenshots |
| [ ] | 7 | Plan-Review (Unteragent, bedingt) | `review.md` |
| [ ] | 8 | Usability-Review (Unteragent, bei UI-Änderungen) | `review-usability.md` |
| [ ] | 9 | Code-Review (Unteragent) | `review-code.md` |
| [ ] | 10 | Sichtprüfung der Design-Screenshots | `test-results.md` |
| [ ] | – | Iteration oder Abschluss entscheiden | – |
| [ ] | 11 | Folgeaufgaben dokumentieren (bei Schleifenabbruch) | `continue.md` |
| [ ] | 12 | Dokumentation erstellen (Unteragent) | `docs/help/` |
| [ ] | 12b | README aktualisieren (Unteragent) | `README.md` |
| [ ] | 12c | Release Notes aktualisieren (Unteragent) | `docs/RELEASE_NOTES.md` |
| [ ] | – | Feature-Verzeichnis löschen | – |
| [ ] | – | Commit durchführen | – |

Hinweis zum Scope dieses Laufs: Der Anwender hat bestätigt, dass der
Design-Entwurf nur ein Zwischenschritt ist — Screenshots aller Ansichten
werden im Issue gezeigt, die Code-Umsetzung folgt erst nach seiner
Bestätigung. Dieser Lauf endet daher mit Design-Artefakten + Issue-Kommentar;
die eigentliche Umsetzung im StatusServer ist ein späterer Schritt.
