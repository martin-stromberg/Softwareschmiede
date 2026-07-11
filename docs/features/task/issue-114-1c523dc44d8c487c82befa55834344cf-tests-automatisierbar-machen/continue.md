# Offene Aufgaben

Erstellt am: 2026-07-11 (zuletzt aktualisiert: Fortsetzungslauf 2026-07-11)
Abbruchgrund: Verbleibender Punkt ist keine Code-Aufgabe mehr, sondern eine bestätigte
Umgebungslimitation der Sandbox (siehe unten) — erfordert eine menschliche Entscheidung, kein
weiterer automatisierter Implementierungsversuch.

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

## Was im Fortsetzungslauf vom 2026-07-11 erledigt wurde

- **Race-Condition-Bug in `KiAusfuehrungsService` behoben** (Auslöser: Kundenhinweis unten). Der
  verzögerte Plugin-Befehlsversand (`SendCommandDelayedAsync`) wird jetzt über einen mit dem
  Prozess-Exit gekoppelten `CancellationTokenSource` (`CliProcessHandle.SendCts`) storniert, bevor
  die `PseudoConsoleSession` disposed wird — kein Zugriff mehr auf den bereits geschlossenen
  Input-Stream. Neuer Regressionstest, Details siehe „Nachtrag (Iteration 4)" in
  `e2e-timeout-analyse.md`. Ergebnis nach kritischer Prüfung: **echter, jetzt behobener
  Produktivcode-Bug** — die pauschale Voreinschätzung „kein Code-Bug, reine
  Umgebungslimitation" war in Bezug auf diese konkrete Exception zu weitgehend.
- Beide zuvor offenen Code-Review-Befunde behoben (siehe unten).
- Vollständiger, eigenständig verifizierter Testlauf (`dotnet test`, 822 Tests, 8 min 16 s, siehe
  `test-results.md`): 14 Fehlschläge, alle ausschließlich die bereits bekannten
  ConPTY-Sandbox-Fälle; keine neuen Regressionen. Die zuvor separat geführten 2
  Clipboard-Unit-Tests liefen in diesem Vollauf grün.

## Code-Review-Befunde

- [x] `WpfTestBase.LaunchApp`: Wirft jetzt eine aussagekräftige `InvalidOperationException`, auch
  wenn das Hauptfenster fehlt/der Prozess beendet ist, aber keine `[ERR]`/`[FTL]`-Zeile im Log
  gefunden wurde — kein stillschweigender Rückgabewert mehr. Behoben und per Code-Review (xhigh)
  bestätigt.
- [x] `AppStartupLogInspector`: `Snapshot()` merkt sich jetzt zusätzlich den Dateipfad
  (`LogSnapshot`-Typ); `GetNewEntries()` wendet den Offset nur bei identischem Pfad an und liest bei
  Log-Rollover ab Dateianfang. Behoben, mit neuen Tests abgedeckt und per Code-Review (xhigh)
  bestätigt.

## Fehlgeschlagene Tests (dokumentierte Umgebungslimitation, weiterhin nicht im Scope behebbar)

- [ ] 14 ConPTY-bezogene E2E-Tests scheitern in diesem Sandbox-Ausführungskontext weiterhin
  reproduzierbar (ConPTY-Kindprozess terminiert sofort, `ExitCode: null`). Der im Fortsetzungslauf
  behobene Race-Condition-Bug war **nicht** die Ursache dieses Verhaltens (empirisch bestätigt:
  Fix behebt die begleitende `ObjectDisposedException`-Warnung, aber die 14 Tests bleiben mit
  reinem `TimeoutException` unverändert rot). Betroffene Produktionslogik
  (`KiAusfuehrungsService`, ConPTY-Klassen) liegt außerhalb des Scopes dieses Plans; eine
  Lösung erfordert vermutlich eine andere Ausführungsumgebung (z. B. nicht-verschachtelte
  interaktive Desktop-Session), keinen weiteren Code-Fix. Siehe `e2e-timeout-analyse.md`
  (inkl. „Nachtrag (Iteration 4)") für die vollständige, jetzt zweifach kritisch geprüfte
  Root-Cause-Analyse. Liste (unverändert, im Vollauf vom 2026-07-11 erneut bestätigt):
  `E2E_WorkingDirectory`, `E2E_TaskWechselUeberMenue`, `E2E_TaskExecutionCommandLineParameters`,
  `E2E_PluginWechsel`, `E2E_PluginSelectionDialog`, `E2E_PluginProjectDefault_NextTask`,
  `E2E_PluginProjectDefault`, `E2E_ConPtyTerminalStart`, `E2E_ConPtyResize`,
  `E2E_ConPtyProcessEnd`, `E2E_ConPtyKeyboardInput`, `E2E_AutoStartCli`, `E2E_AufgabeStarten`,
  `E2E_ArbeitsstatusAktualisierung`.
- [x] 2 Clipboard-bezogene Unit-Tests (`TerminalControlTests.OnPreviewKeyDown_CtrlV_*`) liefen im
  vollständigen Testlauf vom 2026-07-11 grün (vorbestehend als umgebungsbedingt flaky dokumentiert;
  Dateien von diesem Branch nicht berührt).

## Rückmeldung des Kunden

Für die Erreichung des Ziels "Tests automatisierbar machen" ist es entscheidend, dass die E2E-Tests sowohl in der Entwicklungsumgebung als auch durch den KI-Agenten zuverlässig ausgeführt werden können. Bitte überprüfen Sie die folgenden Punkte:

- [ ] Die E2E-Tests, bei denen ein Kommando in der Pseudo-Konsole ausgeführt wird, scheitern offenbar bei der Ausführung durch den KI-Agenten (siehe oben). Wird der Test aus Visual Studio heraus ausgeführt, so ist die Ausführung erfolgreich. Es scheint, dass der KI-Agent die Pseudo-Konsole nicht korrekt initialisiert oder die Eingaben nicht richtig verarbeitet. Bitte prüfen Sie die Konfiguration des KI-Agenten und stellen Sie sicher, dass alle notwendigen Abhängigkeiten und Umgebungsvariablen korrekt gesetzt sind. Beispiel: E2E_ConPtyKeyboardInput.ConPtyKeyboardInput_NachStart_KeinFehlerBanner_E2E

  Hier gibt es folgenden Ausnahmefehler:
  [09:41:27 WRN] Plugin-Befehl konnte nicht an cmd.exe gesendet werden f�r Aufgabe b83c8a7a-4f3b-41c9-907c-531bb93b05f6.
System.ObjectDisposedException: Cannot access a closed file.
   at System.IO.FileStream.WriteAsync(ReadOnlyMemory`1 buffer, CancellationToken cancellationToken)
   at Softwareschmiede.Application.Services.KiAusfuehrungsService.SendCommandDelayedAsync(PseudoConsoleSession session, String command, Guid aufgabeId, CancellationToken ct) in D:\Repositories\softwareschmiede\1c523dc4-4d8c-487c-82be-fa55834344cf\src\Softwareschmiede\Application\Services\KiAusfuehrungsService.cs:line 583

  **Antwort (Fortsetzungslauf 2026-07-11):** Der gemeldete Ausnahmefehler wurde untersucht und
  bestätigt sich als echte, jetzt behobene Race Condition (Details oben und in
  `e2e-timeout-analyse.md`, Nachtrag „Iteration 4"). Er trat in keinem der 14 verbleibenden
  Testläufe mehr auf. Die zugrundeliegende, eigentliche Ursache des Testfehlschlags (ConPTY-
  Kindprozess terminiert in dieser Sandbox-Ausführungsumgebung sofort, unabhängig von diesem
  Code-Pfad) besteht davon unberührt fort und ist — nach jetzt dreifacher, unabhängiger
  Analyse — kein bekannter Code-Fehler, sondern eine Eigenschaft der Sandbox-Umgebung selbst
  (siehe Detailanalyse). Eine zuverlässige Ausführung dieser 14 Tests durch den KI-Agenten in
  dieser konkreten Sandbox ist mit den bisher verfügbaren Mitteln nicht erreichbar; dafür wäre
  eine andere Ausführungsumgebung nötig. Dieser Punkt bleibt daher als offen markiert und
  erfordert eine menschliche Entscheidung (z. B. Testausführung in einer nicht-sandboxed
  Umgebung, oder bewusste Akzeptanz dieser Einschränkung).
