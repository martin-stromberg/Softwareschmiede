# Offene Aufgaben

Erstellt am: 2026-07-17
Abbruchgrund: Kein Fortschritt zwischen den letzten zwei Iterationen (offene Punkte Iteration 1: 1, Iteration 2: 4).

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und müssen manuell oder in einem erneuten Lauf bearbeitet werden.

## Offene Planelemente

Keine. Der eigentliche fachliche Fix (Filterkorrektur in `CliUpdateSafetyService.CheckAsync()`, Whitelist auf
`AufgabeLaufStatus.Laeuft`) ist vollständig umgesetzt und über zwei unabhängige Runden verifiziert:
`review.md` bestätigt "Vollständig umgesetzt", `review-code.md` bestätigt "Keine Befunde".

## Code-Review-Befunde

Keine.

## Fehlgeschlagene Tests

Alle vier fehlgeschlagenen Tests wurden untersucht und sind nachweislich **unabhängig** von der in dieser
Anforderung umgesetzten Änderung (betroffene Dateien laut `git diff main --stat`: ausschließlich
`CliUpdateSafetyService.cs` und `CliUpdateSafetyServiceTests.cs`; keiner der folgenden Tests berührt diese
Dateien oder `AufgabeLaufStatus`):

- [ ] `TaskDetailViewModelTests.CanAssignIssue_FalseWhenCliRunning` — "Expected sut.IsCliRunning to be True, but found False." Startet einen echten CLI-Prozess über `KiAusfuehrungsService`/ConPTY (`cmd.exe /c ping ...`), nicht gemockt. `TaskDetailViewModel.cs`/`TaskDetailViewModelTests.cs` sind auf diesem Branch nicht verändert. Deckt sich mit der in `CLAUDE.md` dokumentierten ConPTY-Sandbox-Einschränkung dieser Umgebung (ConPTY-Kindprozess wird nicht korrekt isoliert/erkannt) — reproduzierbar in 4 von 4 isolierten Einzelläufen, war jedoch in Iterationsrunde 1 noch grün. Manuell in einer echten interaktiven Session (Visual Studio) zu verifizieren.
- [ ] `TerminalControlTests.OnPreviewKeyDown_CtrlV_SetsHandledTrue` — `COMException: OpenClipboard fehlgeschlagen (CLIPBRD_E_CANT_OPEN)`. Erfordert Zwischenablage-Zugriff, der ohne interaktive Desktop-Session in dieser Sandbox nicht zuverlässig funktioniert. Kein Bezug zur Änderung.
- [ ] `WpfE2ETests.ProjektErstellen_UndNeueAufgabeAnlegen_E2E` — `TimeoutException` beim Warten auf ein UI-Element (15s). App-Log (`src/Softwareschmiede.App/bin/Debug/net10.0-windows10.0.17763.0/logs/softwareschmiede-20260717.log`) wurde geprüft: kein Startup-Absturz, keine `MainWindow konnte nicht angezeigt werden`-Meldung, keine `XamlParseException`. Deutet auf ein UI-Timing-/Sandbox-Problem hin, kein Code-Fehler. Kein Bezug zur Änderung.
- [ ] `E2E_WorkingDirectory.RepositoryZuweisen_MitFehlgeschlagenemStrukturabruf_ZeigtTextBoxUndSpeichertManuellenPfad_E2E` — `UnauthorizedAccessException` bei `Directory.Delete` (Zeile 76 des Tests). `E2E_WorkingDirectory.cs` ist auf diesem Branch nicht verändert. Derselbe Fehler ist bereits identisch in einer völlig unabhängigen, früheren Feature-Historie dokumentiert (`docs/features/task/6770ad62e0d6455c919d5ecc54be1f1a-dateiexplorer/test-results.md`, Commit `9184552`) und dort als Windows-Dateisperrenproblem (Virenscanner/Git-Prozess auf `.git`-Verzeichnis) eingeordnet — reproduziert konsistent über mehrere Läufe und Branches hinweg.

**Hinweis für den nächsten Bearbeitungslauf:** Alle vier Punkte sind Sandbox-/Umgebungsbeschränkungen dieser
Agentenumgebung (kein interaktiver Desktop, ConPTY-Prozessisolation, Datei-Locking), keine durch Code-Änderungen
behebbaren Fehler. Ein erneuter automatisierter Implementierungszyklus wird hieran voraussichtlich nichts ändern;
eine Verifikation in einer echten interaktiven Session (Visual Studio / Entwickler-PC) wird empfohlen, bevor
weitere automatisierte Iterationen investiert werden.
