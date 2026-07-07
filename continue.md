# Aufgabe: Unerwartetes Beenden der Anwendung

- [x] Nebenbefund behoben (nicht die Ursache dieses Tests): `PseudoConsoleSession.Dispose()` blockierte bis zu 5s synchron auf `_readLoopTask.Wait(...)`, aufgerufen aus dem Process.Exited-Handler (ThreadPool-Thread). Unter ThreadPool-Druck durch parallele CLI-Sitzungen verzögerte das die Exited-Behandlung anderer Sitzungen. Fix: kein blockierendes Wait mehr, stattdessen nicht-blockierende Continuation. Regressionstest: `Dispose_ReadLoopNeverCompletes_ReturnsPromptlyWithoutWaiting` (rot vorher, grün danach). Volle Nicht-E2E-Suite: 650/650 grün.
- [ ] ConPtyProcessExited_SubscriberThrows_LogsAndDoesNotCrash — weiterhin ungelöst. Ursache liegt tiefer als der obige Nebenbefund: Der interaktiv (ohne `/c`) über ConPTY gestartete `cmd.exe`-Prozess gibt sein Start-Banner aus und verarbeitet danach offenbar keine weiteren Eingaben/Ausgaben mehr — `HasExited` bleibt beliebig lange `False`, ein per Input-Stream injiziertes `exit\r\n` hat nachweislich keine Wirkung. Reproduzierbar, wenn die Prozesskette in einer frischen nativen Konsolen-Session verwurzelt ist (PowerShell-Tool-Aufruf, vermutlich auch Visual Studio Test Explorer), nicht jedoch wenn sie von Git-Bash/pty geerbt wird. Versucht und ohne Wirkung wieder verworfen: (a) `ThreadPool.RegisterWaitForSingleObject` auf dem rohen Prozess-Handle statt `Process.Exited`, (b) explizite `exit\r\n`-Injektion in den Input-Stream (mit/ohne Warmup-Delay). Vermutlich ein Windows-Konsolen-/ConPTY-Subsystem-Verhalten, das tiefere native Diagnosetools (Process Monitor/ETW) erfordert. Diskutierte Idee: `cmd.exe` durch ein selbst kontrolliertes Hilfs-Konsolenprojekt ersetzen (offen, noch nicht umgesetzt/verifiziert).
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


- [ ] AufgabeWechselUeberSeitenleiste_ZeigtNeueAufgabeMitEigenerCli_E2E:
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
