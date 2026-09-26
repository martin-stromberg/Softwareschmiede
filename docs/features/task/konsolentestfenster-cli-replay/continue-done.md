# Offene Aufgaben

Erstellt am: 2026-09-24
Abbruchgrund: Maximale Iterationsanzahl erreicht
Nacharbeit-Lauf am: 2026-09-26 — alle in dieser Sandbox lösbaren Punkte erledigt;
verbleibende `[ ]`-Einträge sind begründet sandbox-limitiert.

## Offene Planelemente

- [ ] Task 38: Live-Reproduktion des Devin-CLI-Rendering-Fehlers (ausgelassene/doppelte Texte) an einer echten `.clireplay`-Aufzeichnung. In dieser Sandbox nicht durchführbar — das Werkzeug (Mitschnitt + Konsolentestfenster) existiert und ist getestet; drei per statischer Analyse gefundene Defekte wurden bereits behoben (Rebuild-Race in `PseudoConsoleSession`, `AnsiSequenceParser`-ESC-State-Bugs, `TerminalReplaySession`-Pause-Deadlock). Nachholbedarf: echte Devin-Session aufzeichnen, im Konsolentestfenster abspielen, Restfehler (falls vorhanden) isolieren. **Entscheidung:** bewusst offen gelassen — erfordert interaktive Session mit echtem Devin-Plugin; im Rahmen dieses Laufs nicht behebbar.

## Code-Review-Befunde

- [x] `TerminalReplaySession.WiedergabeLoopAsync`: `ct` wird zwischen Chunks nicht beachtet, wenn `delay == 0` → behoben: `ct.ThrowIfCancellationRequested()` am Schleifenanfang (`TerminalReplaySession.cs` Z. 239) und nach dem Gate-Wait im In-Flight-Retry (Z. 263); Regressionstest `Dispose_MitZeitrafferNull_BrichtAbStattChunksZuDraenen`.
- [x] `KonsolenTestViewModel.ErzeugeReplaySession`: Zeitraffer-Fallback `TimeSpan.MaxValue` bei ungültigem Text → behoben: `_zeitrafferSchwelle`-Feld hält die zuletzt gültige Schwelle (`KonsolenTestViewModel.cs` Z. 33–37/148/262); Regressionstest `ZeitrafferSchwelleText_Ungueltig_UebernimmtLetzteGueltigeSchwelleAufFrischeSession`.
- [x] `E2E_ConPtyLifecycle.cs` (`ConPtyCliReplayExport_...`): `finally` ohne Dialog-Cleanup → behoben: `settings`/`dialog` außerhalb des `try`, `TryCloseKonsolenTestfenster(dialog, settings)` im `finally` (Z. 75–76/116).

## Usability-Befunde

- [x] `KonsolenTestDialog.xaml`: Dateipfad überläuft Werkzeugleiste → behoben: `MaxWidth="320"` + `TextTrimming="CharacterEllipsis"` + `ToolTip`/`HelpText` auf `DateiPfad` (Z. 29–38).
- [x] `TaskDetailViewModel`: irreführende „keine Aufzeichnung"-Meldung → behoben: Meldung erwähnt Flüchtigkeit des Mitschnitts (nur Arbeitsspeicher, letzte 8 Aufgaben, Verlust bei Neustart/durch neuere Ausführungen) — `TaskDetailViewModel.cs` Z. 2367.

## Fehlgeschlagene Tests

Keine — `test-results.md` trägt den Status „Keine Fehler". Nicht ausgeführte Szenarien (Sandbox-Limitation, in interaktiver Session/Visual Studio nachzuholen):

- [ ] `End2EndTest.RunConPtyTests` inkl. Phase `ConPtyCliReplayExport_ExportOeffnetKonsolenTestUndSpieltAb_E2E` (Echt-Session-Export + Öffnen + Abspielen) — per `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` übersprungen. **Entscheidung:** bewusst offen gelassen — ConPTY-Kindprozesse werden in dieser Sandbox nicht isoliert (dokumentiert, kein Code-Bug); nicht „reparierbar".
- [ ] `E2E_RepositoryInitialisierungAusfuehrungTests.InitialisierungsskriptAusfuehrung` — gleiche ConPTY-Limitation, gleiche Entscheidung.
