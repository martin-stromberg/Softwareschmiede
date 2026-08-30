# Aufgabe: Unerwartetes Beenden der Anwendung

- [x] GitHub-Actions-Tests stabilisieren: Die Tests in den GitHub Actions sollen auf stabile Tests reduziert werden, also ohne flake-anfällige WPF/FlaUI-/E2E-Tests. Der Rest der Tests wird lokal während der Entwicklung ausgeführt.

- [x] Nebenbefund behoben (nicht die Ursache dieses Tests): `PseudoConsoleSession.Dispose()` blockierte bis zu 5s synchron auf `_readLoopTask.Wait(...)`, aufgerufen aus dem Process.Exited-Handler (ThreadPool-Thread). Unter ThreadPool-Druck durch parallele CLI-Sitzungen verzögerte das die Exited-Behandlung anderer Sitzungen. Fix: kein blockierendes Wait mehr, stattdessen nicht-blockierende Continuation. Regressionstest: `Dispose_ReadLoopNeverCompletes_ReturnsPromptlyWithoutWaiting` (rot vorher, grün danach). Volle Nicht-E2E-Suite: 650/650 grün.
- [x] ConPtyProcessExited_SubscriberThrows_LogsAndDoesNotCrash — im aktuellen Branch erneut verifiziert. Der Test nutzt inzwischen den kontrollierten `SimulatedPseudoConsoleProcessLauncher` und ist isoliert grün.
Softwareschmiede.Tests.Application.Services.KiAusfuehrungsServiceTests.ConPtyProcessExited_SubscriberThrows_LogsAndDoesNotCrash
   Quelle: KiAusfuehrungsServiceTests.cs Zeile 181
   Dauer: 15,1 Sek.

  Nachricht: 
Expected finished to be System.Threading.Tasks.Task {Status=WaitingForActivation} because der Exited-Handler muss die Exception des werfenden Subscribers loggen, statt die Anwendung abstürzen zu lassen, but found System.Threading.Tasks.Task+DelayPromise {Status=RanToCompletion}.

  Stapelüberwachung: 
ObjectAssertions`2.Be(TSubject expected, String because, Object[] becauseArgs)
KiAusfuehrungsServiceTests.AssertSubscriberExceptionIsLoggedAsync(Func`4 startAsync, TimeSpan timeout) Zeile 235
KiAusfuehrungsServiceTests.ConPtyProcessExited_SubscriberThrows_LogsAndDoesNotCrash() Zeile 183
--- End of stack trace from previous location ---


- [x] AufgabeWechselUeberSeitenleiste_ZeigtNeueAufgabeMitEigenerCli_E2E — im aktuellen Branch erneut isoliert verifiziert und grün:
 Softwareschmiede.Tests.E2E.E2E_TaskWechselUeberMenue.AufgabeWechselUeberSeitenleiste_ZeigtNeueAufgabeMitEigenerCli_E2E
   Quelle: E2E_TaskWechselUeberMenue.cs Zeile 46
   Dauer: 33,6 Sek.

  Nachricht: 
System.TimeoutException : TerminalConsole zeigte innerhalb des Timeouts keine Prozess-ID (HelpText) an.

  Stapelüberwachung:
E2E_TaskWechselUeberMenue.WaitForTerminalProzessId(AutomationElement mainWindow, TimeSpan timeout) Zeile 155
E2E_TaskWechselUeberMenue.AufgabeWechselUeberSeitenleiste_ZeigtNeueAufgabeMitEigenerCli_E2E() Zeile 61
InvokeStub_E2E_TaskWechselUeberMenue.AufgabeWechselUeberSeitenleiste_ZeigtNeueAufgabeMitEigenerCli_E2E(Object, Object, IntPtr*)
MethodBaseInvoker.InvokeWithNoArgs(Object obj, BindingFlags invokeAttr)

## Verifikation am 2026-07-19

- `dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "FullyQualifiedName=Softwareschmiede.Tests.Application.Services.KiAusfuehrungsServiceTests.ConPtyProcessExited_SubscriberThrows_LogsAndDoesNotCrash"`: bestanden, 1/1.
- `dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "FullyQualifiedName=Softwareschmiede.Tests.E2E.E2E_TaskWechselUeberMenue.AufgabeWechselUeberSeitenleiste_ZeigtNeueAufgabeMitEigenerCli_E2E"`: bestanden, 1/1.

---

# Aufgabe: Pull-Request-Aktionsbuttons in der UI fehlen

- [x] In der UI fehlen Aktionsbuttons fuer den Pull Request. Anwender koennen die Pull-Request-Aktion deshalb nicht ueber die Oberflaeche ausloesen, obwohl die fachliche Pull-Request-Erstellung vorhanden ist.

- [x] Es fehlen Tests, die sicherstellen, dass die Pull-Request-Aktionsbuttons in der UI vorhanden sind. Die Tests muessen ergaenzt werden, muessen in diesem Lauf aber nicht ausgefuehrt werden.

## Verifikation am 2026-07-19

- `dotnet build src\Softwareschmiede.Tests\Softwareschmiede.Tests.csproj --no-restore`: erfolgreich, 0 Fehler.
- Die neu ergaenzten Pull-Request-UI-Button-Tests wurden gemaess Nutzervorgabe nicht ausgefuehrt.

---

# Fehler in der Umsetzung: Pull-Request-Button faelschlich an Status Beendet gekoppelt

- [x] Der Pull-Request-Aktionsbutton wurde faelschlich so umgesetzt, dass er nur bei Aufgaben im Status `Beendet` sichtbar/aktiv ist. Diese Status-Einschraenkung war nicht gefordert und soll entfernt werden.

- [x] Der Button soll sichtbar/aktiv sein, sobald ein Pull Request technisch erstellt werden kann, insbesondere wenn ein Branch vorhanden ist, ein verknuepftes Git-Repository vorhanden ist und ein SCM-/Git-Plugin bzw. PR-Capability verfuegbar ist. Der Aufgabenstatus `Beendet` darf keine zwingende Voraussetzung sein.

- [x] Die Tests zur Command-Verfuegbarkeit und UI-Button-Praesenz muessen entsprechend angepasst oder ergaenzt werden, sodass sie die Nicht-Kopplung an `Beendet` absichern.

## Verifikation am 2026-07-19

- `dotnet test src\Softwareschmiede.Tests\Softwareschmiede.Tests.csproj --filter "FullyQualifiedName~TaskDetailViewModelTests.PullRequestErstellenCommand|FullyQualifiedName~TaskDetailViewTests"`: bestanden, 3/3.

---

# Aufgabe: Autonome Aufgaben in den Einstellungen deaktivierbar machen

- [x] Die gesamte Funktionalität rund um die autonomen Aufgaben soll über die Einstellungen aktivierbar/deaktivierbar sein, wobei sie standardmäßig aktiviert ist.
- [x] Ist die Funktionalität deaktiviert, so muss das einfache Starten einer Aufgabe mit CLI-Ausführung weiterhin wie bisher möglich sein (die Deaktivierung darf ausschließlich die autonome Ablaufsteuerung betreffen, nicht die grundlegende manuelle CLI-Ausführung einer Aufgabe).

Umgesetzt über einen Dual-Layer-Feature-Flag: `AutonomAufgabenOptions.Enabled` (appsettings.json/Umgebungsvariable-Deployment-Default, Standard `true`) plus ein DB-persistierter Laufzeit-Schalter über eine neue Checkbox „Autonome Aufgaben aktivieren" in den Einstellungen (Registerkarte „Allgemein"), zusammengeführt über den zentralen Helper `AppEinstellungService.GetAutonomAufgabenEnabledAsync(deploymentDefault, ct)`. Guard-Klauseln in `AutonomAufgabenInitialisierungsService`, `ProjektleiterAgentService` und `AutonomAufgabeStartService` verhindern bei deaktiviertem Flag den autonomen Ablauf; die reguläre CLI-Aufgabenausführung bleibt davon unberührt. `TaskDetailViewModel` blendet die Registerkarte „Automatisierung" entsprechend aus.

## Verifikation am 2026-08-30

- `dotnet build Softwareschmiede.slnx`: erfolgreich, 0 Warnungen, 0 Fehler.
- `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category!=OsInterface"`: bestanden, 1495/1496 (1 übersprungen, plattformbedingt, unabhängig von dieser Änderung).
- `dotnet format Softwareschmiede.slnx --verify-no-changes`: keine Formatierungsabweichungen.
- Vier Code-Review-Runden durchlaufen (drei mit Befunden, jeweils behoben; vierte Runde: keine Befunde mehr).
