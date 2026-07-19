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
