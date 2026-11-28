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
| [x] | 5a | Offene Punkte prüfen und ggf. Planung wiederholen | `plan.md` (aktualisiert) |
| [x] | 5b | Plan gegen Anforderung und Testbedarf prüfen (Unteragent) | `plan-check.md` |
| [x] | 5c | Planungscommit | – |
| [x] | 6 | Implementierung (Unteragent) | Design-Mockups + Screenshots |
| [x] | 7 | Plan-Review (Unteragent, bedingt) | `review.md` |
| [x] | 8 | Usability-Review (Unteragent, bei UI-Änderungen) | `review-usability.md` |
| [x] | 9 | Code-Review (Unteragent) | `review-code.md` |
| [x] | 10 | Sichtprüfung der Design-Screenshots | `test-results.md` |
| [x] | – | Iteration oder Abschluss entscheiden | – |
| [ ] | 11 | Folgeaufgaben dokumentieren (bei Schleifenabbruch) | `continue.md` |
| [x] | 12 | Dokumentation — nicht anwendbar (Design-Lauf ohne ausgeliefertes Feature; `docs/design/status-webseite/README.md` deckt die Artefakte) | – |
| [x] | 12b | README — nicht anwendbar (kein Produktivfeature) | – |
| [x] | 12c | Release Notes — nicht anwendbar (Zwischenschritt, nichts ausgeliefert) | – |
| [ ] | – | Feature-Verzeichnis löschen | – |
| [ ] | – | Commit durchführen | – |

Hinweis zum Scope dieses Laufs: Der Anwender hat bestätigt, dass der
Design-Entwurf nur ein Zwischenschritt ist — Screenshots aller Ansichten
werden im Issue gezeigt, die Code-Umsetzung folgt erst nach seiner
Bestätigung. Dieser Lauf endet daher mit Design-Artefakten + Issue-Kommentar;
die eigentliche Umsetzung im StatusServer ist ein späterer Schritt.

Endstand dieses Laufs: Design-Entwurf erstellt, geprueft und im Issue #500 zur
Abnahme vorgelegt (Draft-PR #510). Feature-Verzeichnis bleibt bestehen — die
Umsetzung in den Composer-Klassen folgt in einem weiteren Lauf nach
Anwender-Bestaetigung; erst dann wird das Verzeichnis aufgeraeumt.

## Iteration 2 (Anwender-Feedback: Google Stitch verbindlich)

| Status | Schritt | Beschreibung | Artefakt |
|--------|---------|--------------|----------|
| [x] | – | Anforderung revidiert (Stitch verbindlich, Ziel: Daten-Verständlichkeit) | `requirement.md` Rev. 2 |
| [ ] | 6' | Stitch-Generierung aller Ansichten + Downloads | `docs/design/status-webseite-stitch/` |
| [ ] | 8' | Usability-/Sichtprüfung der Stitch-Screenshots | `test-results.md` (ergänzt) |
| [ ] | – | Issue-Kommentar mit eingebetteten Screenshots | – |

## Iteration 3 (Stitch)

- [x] Stitch-API angebunden (MCP-Endpunkt, Key aus Benutzer-Umgebungsvariable)
- [x] Projekt + Design-System "Precision Operations" erstellt
- [x] 14 Light-Screens generiert, heruntergeladen, sichtgeprüft
- [x] 14 Dark-Varianten generiert, heruntergeladen, sichtgeprüft
- [x] Light|Dark-Composites + README erstellt
- [x] Commit + Push (78ab85a2), Issue-Kommentar mit 14 eingebetteten Composites
- [ ] Anwender-Entscheidung abwarten: manueller Entwurf vs. Stitch-Entwurf
