# Offene Aufgaben — Nacharbeitslauf abgeschlossen

Erstellt am: 2026-09-24 (aus dem Schleifenabbruch des ersten Laufs)
Abgeschlossen am: 2026-09-25 (Nacharbeitslauf, Schritt 6 mit Einstieg über `continue.md`;
ohne Unteragenten selbst durchgeführt — dokumentierte Abweichung gemäß SKILL.md)

## Offene Planelemente

Keine — `review.md` trägt den Status „Vollständig umgesetzt".

## Code-Review-Befunde

- [x] `KiAusfuehrungsService.StartTerminalSessionAsync`: Early-Exit-Pfad verliert Fehler-Mapping —
  **behoben**: der `process.HasExited`-Block delegiert jetzt an `HandleSessionEndedAsync(aufgabeId,
  handle, session.ExitCode ?? TryGetExitCode(process), "Terminal")` (`KiAusfuehrungsService.cs:262–266`),
  sodass `HandleExitedCoreAsync` das Status-Mapping (`exitCode != 0` → `CliProcessStatus.Fehler` inkl.
  `PersistFehlgeschlagenAsync`-Protokolleintrag) übernimmt. Verifiziert durch den Regressionstest
  `StartTerminalSessionAsync_ProzessVorVerdrahtungBeendet_MapptExitCodeAufStatus` (`exit 0` → Gestoppt,
  `exit 1` → Fehler + Protokolleintrag) und `review-code.md` Runde 4 („Keine Befunde").
- [x] `KiAusfuehrungsService.StartTerminalSessionAsync`: vor der Verdrahtung gefeuertes `Failed` bei
  laufendem Prozess nicht detektierbar — **behoben**: `ITerminalSession` exponiert jetzt
  `TerminalSessionFailedEventArgs? Failure` (`ITerminalSession.cs:32–36`), gesetzt in
  `PseudoConsoleSession.RaiseFailed` **vor** dem Event-Invoke via `Interlocked.CompareExchange`
  (erster Fehler gewinnt). `StartTerminalSessionAsync` prüft `session.Failure` unmittelbar nach der
  Event-Registrierung (`KiAusfuehrungsService.cs:250–254`) und delegiert an `HandleSessionFailedAsync`.
  Zusätzlich plan-konform korrigiert: `HandleSessionFailedAsync` meldet jetzt `CliProcessStatus.Fehler`
  (statt faktisch `Gestoppt` via `exitCode: null`) über den neuen `istFehlerhaftesEnde`-Parameter in
  `HandleExitedCoreAsync`. Verifiziert durch `StartTerminalSessionAsync_FailedVorVerdrahtung_*`,
  `StartTerminalSessionAsync_SessionFailed_MeldetFehlerStattGestoppt` und
  `PseudoConsoleSessionTests.*Failure*`.

## Usability-Befunde

Keine — `review-usability.md` trägt den Status „Keine Befunde" (Runde 4, unverändert gegenüber Runde 3).

## Fehlgeschlagene Tests

Keine Fehlschläge in allen vier Lanes (`test-results.md` Runde 4: „Keine Fehler", 1912/1915 bestanden,
3 Sandbox-Skips).

## Nicht in dieser Sandbox lösbar — externer Nachholbedarf (kein Code-Befund)

Die folgenden Einträge bleiben **bewusst nicht abgehakt**: Sie sind in dieser Agenten-Sandbox
strukturell nicht ausführbar (ConPTY-Kindprozess wird nicht an die Pseudo-Konsole gebunden —
bestätigte Umgebungslimitation, kein Code-Defizit) und müssen in einer interaktiven
Session/Visual-Studio-Ausführung nachgeholt werden. Die Entscheidung, `continue.md` trotzdem nach
`continue-done.md` umzubenennen, basiert darauf, dass alle **in dieser Umgebung lösbaren** Punkte
abgeschlossen sind; die verbleibenden Einträge sind dokumentierte, ehrlich als „nicht ausgeführt"
gemeldete Nachweis-Lücken (siehe `test-results.md`, Abschnitt „Nicht ausgeführt") — sie erfordern
manuelle Intervention außerhalb dieser Sandbox und können hier nicht „repariert" werden:

- [ ] `End2EndTest.RunConPtyTests` — in dieser Sandbox per `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`
  übersprungen; enthält die Pflichtszenarien `ConPtyLifecycle_StartResizeTastatureingabeUndProzessende_E2E`
  (Direct-Start, `/k`-Banner, Echo-Marker, ANSI-Burst, Paste, Reattach, Resize, Exit),
  `PluginAuswahlAbbrechenOkUndWechsel_E2E`, `AufgabeStarten_MitKonfiguriertemArbeitsverzeichnis_*`,
  `AufgabeWechselUeberSeitenleiste_*`
- [ ] `E2E_RepositoryInitialisierungAusfuehrungTests.InitialisierungsskriptAusfuehrung` —
  ConPTY-abhängig, ebenfalls geskippt
- [ ] Manuelle Validierung der migrierten CLIs `DevinPlugin` + `CodexPlugin` (Plan Schritt 9: Start,
  Eingabe, Escape-Sequenzen, Resize, Resolver-Nachweis für reales Installationslayout) — nicht
  automatisierbar, in dieser Sandbox nicht möglich
