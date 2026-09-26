# Offene Aufgaben

Erstellt am: 2026-09-24
Abbruchgrund: Maximale Iterationsanzahl erreicht

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und müssen manuell oder in einem erneuten Lauf bearbeitet werden.

## Offene Planelemente

- [ ] Task 38: Live-Reproduktion des Devin-CLI-Rendering-Fehlers (ausgelassene/doppelte Texte) an einer echten `.clireplay`-Aufzeichnung. In dieser Sandbox nicht durchführbar — das Werkzeug (Mitschnitt + Konsolentestfenster) existiert und ist getestet; drei per statischer Analyse gefundene Defekte wurden bereits behoben (Rebuild-Race in `PseudoConsoleSession`, `AnsiSequenceParser`-ESC-State-Bugs, `TerminalReplaySession`-Pause-Deadlock). Nachholbedarf: echte Devin-Session aufzeichnen, im Konsolentestfenster abspielen, Restfehler (falls vorhanden) isolieren.

## Code-Review-Befunde

- [ ] `TerminalReplaySession.WiedergabeLoopAsync`: `ct` wird zwischen Chunks nicht beachtet, wenn `delay == 0` (bereits erfüllter Gate-Task). Nach `Dispose`/Neustart mit `ZeitrafferSchwelle = 0` drain-t die Schleife alle restlichen Chunks auf der disposed Session. Empfehlung: `ct.ThrowIfCancellationRequested()` am Schleifenanfang (threadsicher ja, abbruchreif nein).
- [ ] `KonsolenTestViewModel.ErzeugeReplaySession`: bei ungültigem `ZeitrafferSchwelleText` bekommt eine frische Session den Default `TimeSpan.MaxValue` statt der zuletzt gültigen Schwelle — Neustart mit stehengelassenem Fehlertext ändert still das Wiedergabe-Tempo. Empfehlung: letzte gültige Schwelle als `TimeSpan`-Feld halten.
- [ ] `E2E_ConPtyLifecycle.cs` (`ConPtyCliReplayExport_...`): `finally` löscht nur die Datei — bei Assert-Fehler bleiben Dialog/Settings offen (inkonsistent zum `TryCloseKonsolenTestfenster`-Muster).

## Usability-Befunde

- [ ] `KonsolenTestDialog.xaml` (~Z. 29–34): geladener Dateipfad wird ungekürzt in der einzeiligen Werkzeugleiste angezeigt (kein `MaxWidth`/`TextTrimming`) — typische Exportpfade (~70–90 Zeichen) schieben Zeitraffer-Eingabe, Status/Position und „Schließen" aus dem sichtbaren Bereich. Empfehlung: `MaxWidth` + `CharacterEllipsis` mit ToolTip oder Pfad aus der Leiste lösen.
- [ ] `TaskDetailViewModel`: Meldung „noch keine Aufzeichnung vor" ist irreführend, wenn der Mitschnitt existierte, aber verworfen wurde (nur im Arbeitsspeicher, letzte 8 Aufgaben, verloren bei App-Neustart). Empfehlung: Flüchtigkeit in der Meldung erwähnen.

## Fehlgeschlagene Tests

Keine — `test-results.md` trägt den Status „Keine Fehler" (1990 Tests, 1987 bestanden, 0 fehlgeschlagen, 3 übersprungen). Nicht ausgeführte Szenarien (Sandbox-Limitation, in interaktiver Session/Visual Studio nachzuholen):

- [ ] `End2EndTest.RunConPtyTests` inkl. Phase `ConPtyCliReplayExport_ExportOeffnetKonsolenTestUndSpieltAb_E2E` (Echt-Session-Export + Öffnen + Abspielen) — per `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` übersprungen
- [ ] `E2E_RepositoryInitialisierungAusfuehrungTests.InitialisierungsskriptAusfuehrung` — gleiche ConPTY-Limitation
