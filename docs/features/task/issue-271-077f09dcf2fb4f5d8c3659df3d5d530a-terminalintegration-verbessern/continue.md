# Offene Aufgaben

Erstellt am: 2026-09-24
Abbruchgrund: Maximale Iterationsanzahl erreicht

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und müssen manuell oder in einem erneuten Lauf bearbeitet werden.

## Offene Planelemente

Keine — `review.md` trägt den Status „Vollständig umgesetzt".

## Code-Review-Befunde

- [ ] `KiAusfuehrungsService.StartTerminalSessionAsync` (~Zeile 244–254): Early-Exit-Pfad verliert Fehler-Mapping — stirbt der Prozess vor der Event-Verdrahtung, wird stets `CliProcessStatus.Gestoppt` gemeldet; `Fehler`-Status und `PersistFehlgeschlagenAsync`-Protokolleintrag fallen weg, obwohl `session.ExitCode`/`TryGetExitCode(process)` verfügbar wären. Empfehlung: an `HandleSessionEndedAsync(aufgabeId, handle, exitCode, "Terminal")` delegieren statt duplizierter Cleanup-Sequenz.
- [ ] `KiAusfuehrungsService.StartTerminalSessionAsync` (~Zeile 241–261): Ein `Failed`-Event (Leseschleifen-Fehler), das zwischen Session-Erzeugung und `session.Failed +=` feuert, ist bei laufendem Prozess nicht detektierbar — Handle bleibt „Gestartet" mit toter Leseschleife. Empfehlung: Fehlerzustand auf `ITerminalSession` exponieren (z. B. `HasFailed`/`Failure`) und nach Event-Registrierung prüfen, analog zum `process.HasExited`-Recheck.

## Usability-Befunde

Keine — `review-usability.md` trägt den Status „Keine Befunde" (Runde 3).

## Fehlgeschlagene Tests

Keine Fehlschläge in allen vier Lanes (`test-results.md`: „Keine Fehler"). Nicht ausgeführte Pflicht-E2E-Szenarien (Sandbox-Limitation, in interaktiver Session/Visual Studio nachzuholen):

- [ ] `End2EndTest.RunConPtyTests` — in dieser Sandbox per `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` übersprungen; enthält die Pflichtszenarien `ConPtyLifecycle_StartResizeTastatureingabeUndProzessende_E2E` (Direct-Start, `/k`-Banner, Echo-Marker, ANSI-Burst, Paste, Reattach, Resize, Exit), `PluginAuswahlAbbrechenOkUndWechsel_E2E`, `AufgabeStarten_MitKonfiguriertemArbeitsverzeichnis_*`, `AufgabeWechselUeberSeitenleiste_*`
- [ ] `E2E_RepositoryInitialisierungAusfuehrungTests.InitialisierungsskriptAusfuehrung` — ConPTY-abhängig, ebenfalls geskippt
- [ ] Manuelle Validierung der migrierten CLIs `DevinPlugin` + `CodexPlugin` (Plan Schritt 9: Start, Eingabe, Escape-Sequenzen, Resize, Resolver-Nachweis für reales Installationslayout) — nicht automatisierbar, in dieser Sandbox nicht möglich
