# Aufgabenliste – Anforderungsbearbeitung

Branch: `task/issue-410-c69c8e7f6eb14c4cbe407bdbdc9e1cd6-ausfuehrungsbegrenzung-schritt-1-plugin-fallback`

**Hinweis:** Dieser Lifecycle-Lauf wird von einem Subagenten ausgeführt, der keine weiteren Unteragenten starten kann. Alle delegierten Schritte (Übersetzung, Bestandsaufnahme, Planung, Planprüfung, Implementierung, Reviews, Tests, Dokumentation) werden daher im selben Agenten als dokumentierte Abweichung nach den Skill-Anleitungen (`translate-requirements.md`, `inventory.md`, `plan.md`, `plan-check.md`, `implement.md`, `review-plan.md`, `review-usability.md`, `review-code.md`, `run-tests.md`, `update-docs.md`, `update-readme.md`, `update-release-notes.md`) selbst ausgeführt.

| Status | Schritt | Beschreibung | Artefakt |
|--------|---------|--------------|----------|
| [x] | 1 | Branch-Name ermitteln | – |
| [x] | 2 | Verzeichnisstruktur vorbereiten | `docs/features/{branchname}/` |
| [x] | – | Einstiegspunkt ermitteln | – |
| [x] | 3 | Anforderung übersetzen (Unteragent) | `requirement.md` |
| [x] | 4 | Bestandsaufnahme (Unteragent) | `inventory.md`, `inventory/` |
| [ ] | 5 | Umsetzungsplanung (Unteragent) | `plan.md` |
| [ ] | 5a | Offene Punkte prüfen und ggf. Planung wiederholen | `plan.md` (aktualisiert) |
| [ ] | 5b | Plan gegen Anforderung und Testbedarf prüfen (Unteragent) | `plan-check.md` |
| [ ] | 5c | Planungscommit | – |
| [ ] | 6 | Implementierung (Unteragent) | Codeänderungen |
| [ ] | 7 | Plan-Review (Unteragent, bedingt) | `review.md` |
| [ ] | 8 | Usability-Review (Unteragent, bei UI-Änderungen) | `review-usability.md` |
| [ ] | 9 | Code-Review (Unteragent) | `review-code.md` |
| [ ] | 10 | Tests ausführen (Unteragent) | `test-results.md` |
| [ ] | – | Iteration oder Abschluss entscheiden | – |
| [ ] | 11 | Folgeaufgaben dokumentieren (bei Schleifenabbruch) | `continue.md` |
| [ ] | 12 | Dokumentation erstellen (Unteragent) | `docs/help/` |
| [ ] | 12b | README aktualisieren (Unteragent) | `README.md` |
| [ ] | 12c | Release Notes aktualisieren (Unteragent) | `docs/RELEASE_NOTES.md` |
| [ ] | – | Feature-Verzeichnis löschen | – |
| [ ] | – | Commit durchführen | – |
