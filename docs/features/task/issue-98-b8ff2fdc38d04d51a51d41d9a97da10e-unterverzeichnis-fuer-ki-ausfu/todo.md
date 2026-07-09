# Aufgabenliste – Anforderungsbearbeitung

Branch: `task/issue-98-b8ff2fdc38d04d51a51d41d9a97da10e-unterverzeichnis-fuer-ki-ausfu`

Hinweis: Dies ist der zweite Nacharbeits-Zyklus für Issue #98. Das Feature wurde bereits mehrfach
vollständig durchlaufen (Commits `8c99b6b`, `3f0bd9c`, `4ecca88`, `46ea76c`). Frühere Artefakte
(requirement.md, inventory.md, plan.md, review*.md, test-results.md) wurden nach dem letzten Zyklus
gelöscht; ihr Inhalt bleibt über die Git-Historie erhalten. Einstiegspunkt gemäß lifecycle.md-Regel
„continue.md vorhanden → Schritt 6" wurde verwendet.

| Status | Schritt | Beschreibung | Artefakt |
|--------|---------|--------------|----------|
| [x] | 1 | Branch-Name ermitteln | – |
| [x] | 2 | Verzeichnisstruktur vorbereiten | `docs/features/{branchname}/` |
| [x] | – | Einstiegspunkt ermitteln (continue.md vorhanden → Schritt 6) | – |
| [x] | 3 | Anforderung übersetzen (Unteragent) | requirement.md (bereits in früherem Zyklus erledigt, Historie: `8c99b6b`) |
| [x] | 4 | Bestandsaufnahme (Unteragent) | inventory.md (bereits in früherem Zyklus erledigt, Historie: `8c99b6b`) |
| [x] | 5 | Umsetzungsplanung | continue.md dient als Spezifikation für diesen Zyklus (lifecycle.md: continue.md-Einstieg → Schritt 6); plan.md wird nach der Umsetzung restauriert/korrigiert |
| [x] | 5a | Offene Punkte prüfen und ggf. Planung wiederholen | entfällt in continue.md-Flow |
| [x] | 5b | Planungscommit | entfällt in continue.md-Flow (kein neuer Planungsstand vor Umsetzung) |
| [x] | 6 | Implementierung – offene Punkte aus continue.md (Punkt 1: InSourceDirectory-Bug; Punkt 2: Remote-Verzeichnisstruktur GitHub/BitBucket) | Codeänderungen, siehe `plan.md` „Nacharbeiten Zyklus 2" |
| [x] | 7 | Plan-Review | `review.md` (Status: Vollständig umgesetzt) |
| [x] | 8 | Code-Review | `review-code.md` (Status: Keine Befunde, 2 Bugs im selben Durchlauf behoben) |
| [x] | 8b | Tests ausführen | `test-results.md` (740/742 Unit-/Integrationstests grün, 2 vorbestehende unabhängige Fehler verifiziert; E2E durch Umgebungsfaktoren eingeschränkt, siehe Detailanalyse) |
| [x] | – | Iteration oder Abschluss entscheiden | Schleife erfolgreich beendet (Vollständig umgesetzt / Keine Befunde / keine durch diesen Zyklus verursachten Fehler) |
| [x] | 8a | Folgeaufgaben dokumentieren (bei Schleifenabbruch) | entfällt – kein Schleifenabbruch; ein neuer, kleinerer Folgepunkt (TaskDetailViewModel-Restart-Pfad) wurde dennoch in einer neuen `continue.md` dokumentiert |
| [x] | 9 | Dokumentation erstellen | `docs/help/projekte/dialog-repository-auswahl.md` aktualisiert (Remote-Verzeichnisstruktur für GitHub/BitBucket) |
| [x] | 9b | README aktualisieren | Geprüft, keine Änderung nötig (README verweist bereits nur generisch auf die Funktion, siehe Commit `46ea76c`) |
| [x] | – | Feature-Verzeichnis löschen | Durchgeführt (siehe finaler Commit) |
| [x] | – | Commit durchführen | Durchgeführt |
