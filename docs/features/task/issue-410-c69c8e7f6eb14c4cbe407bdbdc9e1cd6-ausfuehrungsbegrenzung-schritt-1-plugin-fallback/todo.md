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
| [x] | 5 | Umsetzungsplanung (Unteragent) | `plan.md` |
| [x] | 5a | Offene Punkte prüfen und ggf. Planung wiederholen | `plan.md` (aktualisiert) |
| [x] | 5b | Plan gegen Anforderung und Testbedarf prüfen (Unteragent) | `plan-check.md` |
| [x] | 5c | Planungscommit | – |
| [x] | 6 | Implementierung (Unteragent) | Codeänderungen |
| [x] | 7 | Plan-Review (Unteragent, bedingt) | `review.md` (`Vollständig umgesetzt`) |
| [x] | 8 | Usability-Review (Unteragent, bei UI-Änderungen) | `review-usability.md` (keine UI-Änderungen, `Keine Befunde`) |
| [x] | 9 | Code-Review (Unteragent) | `review-code.md` (`Keine Befunde`; zwei Befunde vorab behoben) |
| [x] | 10 | Tests ausführen (Unteragent) | `test-results.md` (`Keine Fehler`; reale E2E-Abnahme dokumentiert als `Nicht durchführbar` gemäß Anwender-Vorgabe) |
| [x] | – | Iteration oder Abschluss entscheiden | – (alle vier Prüfungen grün → Schritt 12) |
| [x] | 11 | Folgeaufgaben dokumentieren (bei Schleifenabbruch) | `continue.md` (entfällt: keine offenen Punkte) |
| [x] | 12 | Dokumentation erstellen (Unteragent) | `docs/help/plugins/github-ereignisse/` (beschreibung/business-rules/troubleshooting), `changes.log` |
| [x] | 12b | README aktualisieren (Unteragent) | `README.md` (Abschnitt GitHub-Ereignisüberwachung) |
| [x] | 12c | Release Notes aktualisieren (Unteragent) | `docs/RELEASE_NOTES.md` |
| [x] | – | Feature-Verzeichnis löschen | – (abweichend vom Skill nicht gelöscht: Anwender-Vorgabe „Preserve lifecycle artifacts and commit them" — Artefakte werden mit dem Abschlusscommit erhalten) |
| [x] | – | Commit durchführen | `feat: robustes Plugin-Fallback für SCM-Ereignisse` |
