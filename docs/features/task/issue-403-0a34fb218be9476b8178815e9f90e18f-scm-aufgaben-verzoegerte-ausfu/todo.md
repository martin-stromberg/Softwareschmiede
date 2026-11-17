# Aufgabenliste – Anforderungsbearbeitung

Branch: `task/issue-403-0a34fb218be9476b8178815e9f90e18f-scm-aufgaben-verzoegerte-ausfu`

Hinweis: Planungsartefakte vom Vorgängerbranch `feature/scm-verzoegerte-ausfuehrung`
übernommen (Commit 88870052) und hierher verschoben.

| Status | Schritt | Beschreibung | Artefakt |
|--------|---------|--------------|----------|
| [x] | 1 | Branch-Name ermitteln | – |
| [x] | 2 | Verzeichnisstruktur vorbereiten | `docs/features/task/issue-403-.../` |
| [x] | – | Einstiegspunkt ermitteln | – |
| [x] | 3 | Anforderung übersetzen | `requirement.md` |
| [x] | 4 | Bestandsaufnahme | `inventory.md` |
| [x] | 5 | Umsetzungsplanung | `plan.md` |
| [x] | 5a | Offene Punkte prüfen und Planung wiederholen | `plan.md` — Antworten vom Anwender via Issue #403 eingearbeitet |
| [ ] | 5b | Planungscommit | – |
| [ ] | 6 | Implementierung | Codeänderungen |
| [ ] | 7 | Plan-Review | `review.md` |
| [ ] | 8 | Code-Review | `review-code.md` |
| [ ] | 8b | Tests ausführen | `test-results.md` |
| [ ] | – | Iteration oder Abschluss entscheiden | – |
| [ ] | 9 | Dokumentation erstellen | `docs/help/` |
| [ ] | 9b | README aktualisieren | `README.md` |
| [ ] | – | Feature-Verzeichnis löschen | – |
| [ ] | – | Commit durchführen | – |

Hinweis: Die verfügbaren Subagenten-Profile sind generisch (explore/general) und decken
die Lifecycle-Kommandorollen nicht ab; Planung und Implementierung werden daher direkt
im Hauptagenten ausgeführt (vom Skill zugelassene Abweichung, wie im Vorgängerbranch
dokumentiert). Read-only-Reviews werden zusätzlich durch einen separaten Review-Agenten
mit frischem Blick gegengeprüft.
