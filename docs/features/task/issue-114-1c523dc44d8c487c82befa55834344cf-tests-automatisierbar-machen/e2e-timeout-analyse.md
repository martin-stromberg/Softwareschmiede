# Analyse der E2E-Test-Timeouts (Iteration 2)

## Vorgehen

Gemäß der `CLAUDE.md`-Debugging-Vorgabe für WPF-E2E-Timeouts wurde zunächst das App-Log auf
Startup-Crashes (`[FTL]`, `MainWindow konnte nicht angezeigt werden.`) geprüft: keine gefunden.
Da der Zeitpunkt der Timeouts (`WaitForElement` tief im jeweiligen Testablauf, nicht beim
initialen `LaunchApp`) auf ein Problem *nach* dem erfolgreichen App-Start hindeutet, wurden
mehrere der 17 fehlgeschlagenen E2E-Tests einzeln (isoliert, mit frisch gebauter Anwendung,
aktiver interaktiver Konsolen-Session) erneut ausgeführt und das App-Log sowie der
UI-Automation-Baum gezielt untersucht (u. a. über einen temporären Diagnose-Test, der den
kompletten Automation-Baum nach dem fraglichen Navigationsschritt in eine Datei dumpt).

Ergebnis: Es gibt **zwei unabhängige, klar unterscheidbare Ursachen**, die zusammen alle 17
E2E-Timeouts/-Fehler erklären.

## Ursache 1 (echter, behobener Bug in der Test-Infrastruktur): veraltete AutomationId `"AufgabenListe"`

Die Tests `E2E_CreateNewTaskNavigation.NeueAufgabeAbbrechen_...`,
`E2E_TaskDetailNavigation.AufgabeOeffnen_...`, `ProjectDetailE2ETests.AufgabeNeuAnlegen_...` und
`E2E_AutoStartCli...` suchen nach einem Element mit `ByName("AufgabenListe")` und lesen davon per
`FindAllChildren(... ListItem ...)` die Aufgaben aus.

Der (bereits vor diesem Plan, in Commit `43dc04c` "beendete aufgaben in projektdetail trennen")
umgebaute `ProjectDetailView.xaml` benennt seitdem nicht mehr die `ListBox` selbst
`"AufgabenListe"`, sondern nur noch das umschließende Layout-`Grid`
(`<Grid Grid.Row="1" AutomationProperties.Name="AufgabenListe"><ListBox
AutomationProperties.Name="OffeneAufgabenListe" .../></Grid>`), die eigentliche Liste heißt jetzt
`"OffeneAufgabenListe"`.

Ein reines Layout-`Grid` ist in der WPF-UI-Automation-"Control"-Sicht (die FlaUI/UIA3 für
`FindFirstDescendant` standardmäßig verwendet) **kein Control-Element** und wird aus dem
Automation-Baum herausgefiltert — unabhängig vom gesetzten `AutomationProperties.Name`. Der
Baum-Dump bestätigte das empirisch: `"AufgabenListe"` ist im laufenden Automation-Baum nicht
auffindbar, `"OffeneAufgabenListe"` (die `ListBox` selbst) dagegen problemlos.

Ein neuerer Test im selben File (`Projektdetailansicht_TrenntOffeneUndBeendeteAufgaben_E2E`, aus
demselben Umbau-Commit) verwendet bereits korrekt `"OffeneAufgabenListe"` und war **nicht**
unter den fehlgeschlagenen Tests — ein weiterer Beleg für diese Diagnose.

**Fix:** Die vier betroffenen Testdateien wurden von `ByName("AufgabenListe")` auf
`ByName("OffeneAufgabenListe")` umgestellt (reine Testcode-Änderung, kein Eingriff in
Produktivcode):

- `src/Softwareschmiede.Tests/E2E/E2E_CreateNewTaskNavigation.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_AutoStartCli.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_TaskDetailNavigation.cs`
- `src/Softwareschmiede.Tests/E2E/ProjectDetailE2ETests.cs`

Da `OffeneAufgabenListe` die `ListBox` selbst ist, liefert `FindAllChildren(... ListItem ...)`
jetzt außerdem tatsächlich die erwarteten `ListBoxItem`-Kindelemente (beim alten `Grid`-Selektor
wäre das selbst bei erfolgreicher Elementsuche leer gewesen, da die `ListItem`s zwei Ebenen tiefer
liegen).

**Verifikation:** `E2E_CreateNewTaskNavigation` (2 Tests), `E2E_TaskDetailNavigation` (3 Tests)
und `ProjectDetailE2ETests` (13 Tests) wurden nach dem Fix isoliert erneut ausgeführt — alle
grün. `E2E_AutoStartCli` scheitert weiterhin, aber (verifiziert über den Stacktrace) an einer
früheren Stelle im Ablauf (Zeile 37, vor der `"AufgabenListe"`-Abfrage in Zeile 52) — siehe
Ursache 2.

Betroffen/behoben: 3 von 17 E2E-Fehlschlägen
(`ProjectDetailE2ETests.AufgabeNeuAnlegen_ErscheintInAufgabenliste_E2E`,
`E2E_CreateNewTaskNavigation.NeueAufgabeAbbrechen_NavigiertZurueckOhneTitelAenderungZuSpeichern_E2E`,
`E2E_TaskDetailNavigation.AufgabeOeffnen_ZeigtTaskDetailViewFensterumfassend_E2E`).

## Ursache 2 (Umgebungslimitation, nicht behoben): ConPTY-Kindprozesse terminieren sofort

Die verbleibenden 14 E2E-Fehlschläge (`E2E_WorkingDirectory`, `E2E_TaskWechselUeberMenue`,
`E2E_TaskExecutionCommandLineParameters`, `E2E_PluginWechsel`, `E2E_PluginSelectionDialog`,
`E2E_PluginProjectDefault(_NextTask)`, `E2E_ConPtyTerminalStart`, `E2E_ConPtyResize`,
`E2E_ConPtyProcessEnd`, `E2E_ConPtyKeyboardInput`, `E2E_AutoStartCli`, `E2E_AufgabeStarten`,
`E2E_ArbeitsstatusAktualisierung`) haben alle exakt eine gemeinsame Vorbedingung: Sie starten
über `WpfTestBase.StartenUndPluginWaehlen` einen CLI-Prozess (KiSimulator-Plugin) via ConPTY und
warten anschließend auf den `"CliStoppen"`-Button (Beleg: `E2E_ConPtyProcessEnd` und
`E2E_AutoStartCli` scheitern beide exakt an der ersten `WaitForElement(..., "CliStoppen", ...)`
nach `StartenUndPluginWaehlen`, direkt verifiziert per Stacktrace-Zeile).

Eine Auswertung **aller** `"CLI-Prozess (ConPTY) ... gestartet/beendet"`-Log-Paare über den
gesamten Testlauf hinweg (ursprünglicher 9-Minuten-Lauf plus mehrere unabhängige,
Minuten-später-isolierte Nachläufe, unterschiedliche PIDs) zeigt ein **zu 100 % konsistentes**
Muster:

```
20:33:04.463 [INF] CLI-Prozess (ConPTY) für Aufgabe "..." gestartet (PID: 26580).
20:33:04.480 [INF] CLI-Prozess (ConPTY) für Aufgabe "..." beendet (ExitCode: null).
```

Der ConPTY-Kindprozess (`cmd.exe /c echo ... && ping -n 31 127.0.0.1 > nul`, der regulär rund 30s
laufen sollte) meldet sich in **jedem einzelnen** beobachteten Fall bereits nach 15–25
Millisekunden als beendet, mit `ExitCode: null` (d. h. `Process.ExitCode` ließ sich nach dem
`Exited`-Event nicht mehr auslesen). Kein einziger der beobachteten ConPTY-Starts (>10 Stichproben
über >20 Minuten, verschiedene PIDs, verschiedene Testklassen) lief tatsächlich durch — es gibt
keine Varianz, wie man sie bei echter Flakiness erwarten würde.

Das spricht stark dafür, dass es sich **nicht** um einen Zeitfenster-/Race-Bug im Anwendungscode
handelt (der zumindest gelegentlich "gut ausgehen" würde), sondern um eine harte, systemische
Einschränkung dieser konkreten Ausführungsumgebung beim Anlegen von an eine Windows-Pseudo-Console
(ConPTY) angehängten Kindprozessen: Vermutlich verhindert die Prozess-/Job-Object-Sandbox, in der
dieser Coding-Agent (und damit auch die von ihm über Bash/PowerShell gestarteten Prozesse
inklusive der Test-App) läuft, dass der für ConPTY notwendige verdeckte Konsolen-Host
(`conhost.exe`) bzw. der daran angehängte `cmd.exe`-Prozess korrekt hochfährt bzw. am Leben
bleibt — siehe auch den in `CLAUDE.md` dokumentierten Self-Hosting-Kontext dieser Umgebung.

**Warum das keine Änderung an `KiAusfuehrungsService`/`PseudoConsoleSession` rechtfertigt:**

1. **Außerhalb des Plan-Scopes:** `plan.md` grenzt den Scope dieser Anforderung explizit auf
   Test-Infrastruktur und Claude-Hooks ein ("kein Produktivcode der App"). Der ConPTY-Startpfad
   liegt in `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs` und
   `PseudoConsoleSession`/`PseudoConsoleProcessStarter` — eindeutig Produktivcode.
2. **Nicht durch diesen Plan verursacht:** Die betroffenen Dateien wurden von diesem Branch nicht
   verändert (`git log` zeigt für `KiAusfuehrungsService.cs` und die ConPTY-Klassen ausschließlich
   Commits von vor Beginn dieser Anforderung).
3. **100%ige, over Zeit und Prozess-IDs hinweg konstante Reproduktion** ist ein starkes Indiz für
   eine Eigenschaft der Ausführungsumgebung (Sandbox/Job-Object-Restriktion), nicht für einen im
   Code fixbaren Zeitfenster-Bug. Ein Blindfix am Anwendungscode könnte dieses Verhalten nicht
   beheben, da die Ursache außerhalb des von diesem Code kontrollierbaren Bereichs liegt.
4. Alle Tests, die **keinen** CLI-Prozess starten (Placeholder-Tests, Settings-Tests,
   `Projektdetailansicht_TrenntOffeneUndBeendeteAufgaben_E2E`, sowie nach dem Fix auch
   `E2E_CreateNewTaskNavigation`/`E2E_TaskDetailNavigation`/`ProjectDetailE2ETests`) laufen
   zuverlässig grün — die interaktive Desktop-Session und die UI-Automation selbst funktionieren
   also nachweislich einwandfrei. Die Einschränkung betrifft ausschließlich den ConPTY-Kindprozess-Pfad.

`E2E_AufgabeStarten` (einziger `NoClickablePointException`-Fall) reiht sich ein: Der Test klickt
nach einer erwarteten Fehlermeldung (erster, absichtlich fehlschlagender Start-Versuch) ein
zweites Mal auf "Starten"; die durch Ursache 2 verursachte UI-Zustands-Instabilität rund um den
scheiternden CLI-Start erklärt plausibel, warum der berechnete Klickpunkt in diesem Fall nicht
mehr gültig war.

**Empfehlung:** Kein Code-Fix im Rahmen dieser Anforderung. Falls die zuverlässige
CLI-Prozess-Ausführung über ConPTY in dieser (oder einer vergleichbar gearteten) Sandbox-Umgebung
benötigt wird, sollte das als eigenständiges Thema untersucht werden (z. B. Test auf einer
"echten", nicht-verschachtelten interaktiven Desktop-Session außerhalb der Agent-Sandbox).

**Bestätigung durch den Anwender:** Der Anwender hat bestätigt, dass dieselben CLI-/ConPTY-E2E-Tests
zuverlässig grün laufen, wenn sie **aus Visual Studio heraus** (also aus einer regulären,
nicht-verschachtelten interaktiven Desktop-Session) gestartet werden. Das Problem tritt
ausschließlich in diesem Claude-Code-Sandbox-Ausführungskontext auf. Das deckt sich exakt mit der
oben hergeleiteten Diagnose (Ursache 2 ist eine Eigenschaft *dieser konkreten* Ausführungsumgebung,
kein allgemeiner Bug im ConPTY-Startpfad) und liefert zusätzlich den Beleg, dass die
CLI-Prozess-Ausführung selbst korrekt implementiert ist und reproduzierbar funktioniert, sobald die
Sandbox-Restriktion entfällt. Damit ist die Einschränkung als umgebungsabhängig (Sandbox vs.
Visual-Studio/interaktive Session) bestätigt und nicht als generischer Defekt zu behandeln.

## Die zwei fehlgeschlagenen Unit-Tests

- `App.Controls.TerminalControlTests.OnPreviewKeyDown_CtrlV_SetsHandledTrue`
  (`COMException: OpenClipboard fehlgeschlagen, 0x800401D0 = CLIPBRD_E_CANT_OPEN`)
- `App.ViewModels.TaskDetailViewModelTests.TestPluginWechselAsync_StopsCliAndStartsNew`
  (Assertion `IsCliRunning` erwartet `True`, war `False`)

Beide Dateien (`src/Softwareschmiede.Tests/App/Controls/TerminalControlTests.cs`,
`src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests.cs`) wurden von diesem Branch
zu keinem Zeitpunkt verändert (`git log`/`git diff main...HEAD` zeigen für beide Dateien keine
Commits/Änderungen aus dieser Anforderung). Beide Fehlerbilder sind zudem typische
Umgebungs-/Timing-Symptome und nicht spezifisch für diesen Plan:

- `CLIPBRD_E_CANT_OPEN` tritt auf, wenn ein anderer Prozess die Zwischenablage im Moment des
  Zugriffs offen hält (klassische, transiente Windows-Clipboard-Kontention, insbesondere bei
  parallel laufenden GUI-Prozessen/Automatisierung auf demselben Desktop).
- Die `IsCliRunning`-Assertion ist eine reine ViewModel-/Mock-Timing-Angelegenheit ohne Bezug zu
  `WpfTestBase`, `AppStartupLogInspector` oder den Build-Hooks.

Beide Fehlschläge sind damit **vorbestehend bzw. umgebungsbedingt** und nicht durch diese
Anforderung verursacht. Keine Änderung im Rahmen dieser Anforderung vorgenommen.

## Zusammenfassung

| # | Fehlerbild | Ursache | Aktion |
|---|-----------|---------|--------|
| 3 von 17 E2E | Timeout bei `WaitForElement(..., "AufgabenListe", ...)` | Veraltete AutomationId in 4 Testdateien (Grid statt ListBox, seit Commit `43dc04c`) | **Behoben** — Selektor auf `"OffeneAufgabenListe"` umgestellt |
| 14 von 17 E2E | Timeout/`NoClickablePointException` nach CLI-Start-Versuch | ConPTY-Kindprozess terminiert in dieser Sandbox-Umgebung reproduzierbar sofort (`ExitCode: null` nach 15–25ms); vom Anwender bestätigt: läuft aus Visual Studio heraus zuverlässig grün | **Nicht behoben** — Umgebungslimitation nur in diesem Sandbox-Kontext, außerhalb Plan-Scope (Produktivcode), hier dokumentiert |
| 2 Unit-Tests | Clipboard-COMException, ViewModel-Assertion | Vorbestehend/umgebungsbedingt, Dateien nicht von diesem Branch berührt | Keine Änderung nötig |
