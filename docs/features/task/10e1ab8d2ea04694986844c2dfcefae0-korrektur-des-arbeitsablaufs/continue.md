# Offene Aufgaben

Erstellt am: 2026-08-20
Zuletzt aktualisiert am: 2026-08-20 (Fortsetzungslauf über Lifecycle-Skill)
Abbruchgrund (ursprünglich): Kein Fortschritt zwischen den letzten zwei Iterationen

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und müssen manuell oder in einem erneuten Lauf bearbeitet werden.

## Offene Planelemente

Keine (Plan-Review: Vollständig umgesetzt).

## Code-Review-Befunde

Keine (Code-Review: Keine Befunde).

## Fehlgeschlagene Tests

- [x] `Softwareschmiede.Tests.E2E.End2EndTest.RunGeneralTests` (Szenario `RepositoryZuweisen_MitFehlgeschlagenemStrukturabruf_ZeigtTextBoxUndSpeichertManuellenPfad_E2E`, `E2E_WorkingDirectory.cs`) — **Behoben.** Ursache war ein ungeschützter Null-Zugriff auf `repo.Id` in `WaitForSavedWorkingDirectoryAsync` (`src/Softwareschmiede.Tests/E2E/E2E_WorkingDirectory.cs`, Zeilen 351-367), vorbestehend (Code-Alter laut `git blame`: 2026-07-13/2026-07-19, deutlich vor diesem Branch) und unabhängig vom eigentlichen Anforderungsumfang. Da es der einzige Weg war, diesen Punkt sauber abzuschließen, wurde ein risikoarmer Null-Guard ergänzt (`repo is null ? null : ...`). Verifiziert durch zwei reale Testläufe (`dotnet build` + `dotnet test --filter "FullyQualifiedName~End2EndTest.RunGeneralTests"`, `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`): Der ursprüngliche Fehler trat in keinem der beiden Läufe mehr auf, der Test kam beide Male signifikant weiter. Details siehe `test-results.md`, Abschnitt "Nachverfolgung (Iteration 3)".

- [ ] `Softwareschmiede.Tests.E2E.End2EndTest.RunGeneralTests` (Szenario `Todo_ErstellenAbhakenLoeschenUndAbschlussValidierung_E2E`, `E2E_TodoManagement.cs:33`) — **Neu aufgetreten (durch den obigen Fix sichtbar geworden).** `System.TimeoutException` in `WpfTestBase.WaitForElement`, aufgerufen aus `WpfTestBase.AufgabeDetailSpeichern` (Zeile 896): Das UI-Element für den Speichern-Button im Aufgabe-Detail-Dialog wurde nicht innerhalb von 15s gefunden. Konsistent reproduzierbar (2/2 Läufen, exakt derselbe Ort). App-Log (`softwareschmiede-20260820.log`) wurde gemäß CLAUDE.md-Regel geprüft: keine Startup-/Laufzeit-Exception im relevanten Zeitfenster, alle `[ERR]`-Einträge sind erwartete, von anderen Testphasen bewusst provozierte Fehlermeldungen — die App lief also sauber bis zum Timeout. Betroffener Code (`E2E_TodoManagement.cs`, `WpfTestBase.AufgabeDetailSpeichern`) stammt aus Commits vom 2026-08-06 bzw. 2026-08-13, Wochen vor diesem Branch, und wird vom hier vorgenommenen Fix nicht berührt — vermutlich vorbestehend/sandboxspezifisch (Timing/UI-Automation), Ursache aber nicht abschließend geklärt. **Erfordert menschliche Entscheidung/Prüfung** (ggf. in einer interaktiven Session, da FlaUI-UI-Automation in dieser Sandbox nur eingeschränkt beobachtbar ist).
