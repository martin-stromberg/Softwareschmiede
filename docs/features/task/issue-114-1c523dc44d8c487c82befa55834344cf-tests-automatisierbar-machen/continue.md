# Offene Aufgaben

Erstellt am: 2026-07-11
Abbruchgrund: Maximale Iterationsanzahl erreicht (3/3)

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und müssen manuell oder in einem erneuten Lauf bearbeitet werden.

## Was in diesem Lauf erledigt wurde (zur Einordnung)

- `AppStartupLogInspector` + `AppStartupLogInspectorTests` (Log-Diagnose bei Startup-Fehlern)
- `WpfTestBase`: Log-Offset-Snapshot, `CheckAppStartupException()`, `Dispose` wartet auf
  Prozess-Exit ohne Kill
- Build-Hook warnt bei laufender `Softwareschmiede.App.exe` (kein Kill, Self-Hosting-Regel)
- 3 E2E-Selektor-Bugs behoben (`"AufgabenListe"` → `"OffeneAufgabenListe"`, stale seit Commit
  `43dc04c`)
- Code-Review-Cleanup: Timeout-Konstanten vereinheitlicht, doppelte App-Pfad-Auflösung in
  `LaunchApp` entfernt, `StartAndNavigateToProjects()`-Helper aus `ProjectDetailE2ETests` nach
  `WpfTestBase` gezogen und in 3 Testklassen wiederverwendet
- **Root-Cause-Fix für Problem 1 der Anforderung** (Build-Korruption während Tests, außerhalb des
  ursprünglichen Plan-Scopes, mit Rücksprache freigegeben): Der Build-Lock zwischen
  `build_before_test.py` (PreToolUse) und dem Stop-Hook `test-csharp-startup.ps1` deckte bisher
  nur die kurzen Build-Schritte ab, nicht die tatsächliche, oft mehrminütige Laufzeit von
  `dotnet test`. Live reproduziert (fehlende `Softwareschmiede.App.runtimeconfig.json`, 17/18
  fehlgeschlagene Tests) und behoben: Lock wird jetzt über einen neuen PostToolUse-Hook
  (`release_build_lock.py`) erst nach Abschluss des eigentlichen Testlaufs freigegeben, mit
  Ownership-Prüfung per `tool_use_id` (verhindert Freigabe eines fremden Locks). Der Stop-Hook
  überspringt den Smoke-Test bei belegtem Lock statt ungeschützt weiterzubauen.
- **Neue, verbindliche Regel in `CLAUDE.md`:** `dotnet test` darf nie mit `run_in_background: true`
  laufen (technischer Block per Hook wurde versucht, griff im Harness aber nachweislich nicht).
- **Punkt aus der ursprünglichen Kundenrückmeldung** ("ConPTY-Tests scheitern beim KI-Agenten,
  funktionieren aber aus Visual Studio") wurde untersucht: Root Cause verifiziert und in
  `e2e-timeout-analyse.md` dokumentiert (siehe unten, kein Code-Bug, Umgebungslimitation dieses
  Sandbox-Ausführungskontexts).

## Code-Review-Befunde (nicht behoben, dokumentiert)

- [ ] `WpfTestBase.LaunchApp`: Die Startup-Log-Diagnose wirft nur, wenn eine `[ERR]`/`[FTL]`-Zeile
  im App-Log gefunden wird. Stürzt die App ohne Log-Eintrag ab oder hängt sie (kein Log-Signal),
  gibt der Aufrufer den bereits beendeten Prozess weiterhin stillschweigend zurück — der Fall, den
  die Diagnose eigentlich abdecken sollte, bleibt dort opak. (`review-code.md`, Befund 3)
- [ ] `AppStartupLogInspector`: Bei einem Log-Rollover zwischen `Snapshot()` und `GetNewEntries()`
  (Datei wechselt) wird der alte Byte-Offset auf die neue, jüngste Datei angewandt und kann deren
  Anfang (inkl. eines dortigen Startup-`[ERR]`) überspringen. Seltener Randfall, aber nicht
  unmöglich bei einem Tageswechsel während eines Testlaufs. (`review-code.md`, Befund 4)

## Fehlgeschlagene Tests (dokumentierte Umgebungslimitationen, nicht im Scope behebbar)

- [ ] 14 ConPTY-bezogene E2E-Tests scheitern in diesem Sandbox-Ausführungskontext reproduzierbar
  (ConPTY-Kindprozess meldet sich ~15-25ms nach Start als beendet, `ExitCode: null`). Vom
  Anwender bestätigt: Aus Visual Studio heraus laufen dieselben Tests erfolgreich — deckt sich
  mit der ursprünglichen Kundenrückmeldung. Betroffene Produktionslogik
  (`KiAusfuehrungsService`, ConPTY-Klassen) liegt außerhalb des Scopes dieses Plans. Siehe
  `e2e-timeout-analyse.md` für die vollständige Root-Cause-Analyse. Liste:
  `E2E_WorkingDirectory`, `E2E_TaskWechselUeberMenue`, `E2E_TaskExecutionCommandLineParameters`,
  `E2E_PluginWechsel`, `E2E_PluginSelectionDialog`, `E2E_PluginProjectDefault_NextTask`,
  `E2E_PluginProjectDefault`, `E2E_ConPtyTerminalStart`, `E2E_ConPtyResize`,
  `E2E_ConPtyProcessEnd`, `E2E_ConPtyKeyboardInput`, `E2E_AutoStartCli`, `E2E_AufgabeStarten`,
  `E2E_ArbeitsstatusAktualisierung`.
- [ ] 2 Clipboard-bezogene Unit-Tests (`TerminalControlTests.OnPreviewKeyDown_CtrlV_*`) sind
  vorbestehend/umgebungsbedingt flaky (Clipboard-Zugriff in nicht-interaktiven/parallelen
  Sessions). Dateien von diesem Branch nicht berührt.

## Rückmeldung des Kunden

Für die Erreichung des Ziels "Tests automatisierbar machen" ist es entscheidend, dass die E2E-Tests sowohl in der Entwicklungsumgebung als auch durch den KI-Agenten zuverlässig ausgeführt werden können. Bitte überprüfen Sie die folgenden Punkte:

- [ ] Die E2E-Tests, bei denen ein Kommando in der Pseudo-Konsole ausgeführt wird, scheitern offenbar bei der Ausführung durch den KI-Agenten (siehe oben). Wird der Test aus Visual Studio heraus ausgeführt, so ist die Ausführung erfolgreich. Es scheint, dass der KI-Agent die Pseudo-Konsole nicht korrekt initialisiert oder die Eingaben nicht richtig verarbeitet. Bitte prüfen Sie die Konfiguration des KI-Agenten und stellen Sie sicher, dass alle notwendigen Abhängigkeiten und Umgebungsvariablen korrekt gesetzt sind. Beispiel: E2E_ConPtyKeyboardInput.ConPtyKeyboardInput_NachStart_KeinFehlerBanner_E2E
  