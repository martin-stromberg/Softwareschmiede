## Testklassen

### `KiAusfuehrungsServiceTests` (`src/Softwareschmiede.Tests/Application/Services/KiAusfuehrungsServiceTests.cs`)
- `IsRunning_ShouldReturnFalse_WhenNoProcessStarted`, `GetRunningCount_ShouldReturnZero_WhenNoProcessStarted`, `GetLastExitCode_ShouldReturnNull_WhenNoProcessStarted`, `StopCliAsync_ShouldNotThrow_WhenNoProcessStarted`, `UpdateHeartbeat_ShouldNotThrow_WhenNoProcessStarted` — Verhalten ohne laufenden Prozess, nutzen den ConPTY-Pfad nicht.
- `TestCliStartAsync`, `StartCliAsync_ShouldReturnHandle_WhenPluginProvidesValidProcessStartInfo` — klassischer Start (`StartCliAsync`, kein ConPTY).
- `GetPseudoConsoleSession_GibtNull_OhneSession` — kein Prozess gestartet.
- `ProcessExited_ScopeFactoryDisposed_PersistiertNichtUndWirftNicht`, `ProcessExited_SubscriberThrows_LogsAndDoesNotCrash` — klassischer Start, mocken `IKiPlugin` mit `FileName = "cmd", Arguments = "/c exit 1"` bzw. ähnlich kurzlebig — Prozess soll bewusst schnell enden.
- `ConPtyProcessExited_SubscriberThrows_LogsAndDoesNotCrash`, `KiAusfuehrungsService_HandleProcessExited_DisposesSession`, `KiAusfuehrungsService_Dispose_CancelsAllSessions`, `StartWithPseudoConsoleAsync_ProzessEndetVorVerzoegertemSenden_KeineWarnungWegenGeschlossenemStream` — nutzen `StartWithPseudoConsoleAsync` mit `FileName = "cmd"` (bare, kein `/c`), erwarten aber jeweils explizit ein schnelles/erzwungenes Prozessende (eigener `StopCliAsync`-Aufruf, `Dispose()`, oder gezielt getestete Race Condition) — sind **nicht** auf ein langes Überleben des ConPTY-Kindprozesses angewiesen und daher vom in der Anforderung beschriebenen Sofort-Sterben nicht in der gleichen Weise betroffen wie die 14 E2E-Tests.

**Konstruktor-Aufrufstellen von `new KiAusfuehrungsService(...)` im gesamten Testbaum:** 19 Vorkommen in 13 Dateien (u. a. `CliEmbeddingServiceIntegrationTests.cs`, `TaskDetailViewModelTestFactory.cs`, `AufgabeDetailWorkspacePreviewBunitTests.cs`, `AufgabeDetailGitActionsBunitTests.cs`, `TaskDetailViewModelTests.cs`, `CliProcessManagerTests*.cs`, `AufgabeDetailFolgePromptTests.cs`, `EntwicklungsprozessServiceTests.cs`, `KiAusfuehrungsServiceTests*.cs`). Relevant für die Designentscheidung, ob ein neuer Konstruktorparameter für den Launcher optional (mit Default) oder verpflichtend sein soll.

## E2E-Testklassen (die 14 betroffenen)

Alle 14 Tests durchlaufen `WpfTestBase.SetupProjectMitNeuerAufgabe` → `StartenUndPluginWaehlen(mainWindow, "Softwareschmiede.KiSimulator")` und warten anschließend auf `WaitForElement(mainWindow, cf => cf.ByName("CliStoppen"), Medium)` (15s) als gemeinsames Kernkriterium — der ConPTY-Kindprozess muss also mindestens so lange "laufend" erscheinen, dass der Status auf `Gestartet` wechselt und der UI-Button gerendert wird.

Zusätzliche, testspezifische Anforderungen an das Prozessverhalten:

| Testdatei | Zusätzlich benötigtes Verhalten |
|-----------|----------------------------------|
| `E2E_ConPtyResize.cs` | Fenstergröße wird geändert (`Transform.Pattern.Resize`), danach muss `CliStoppen` weiterhin vorhanden sein und keine `FehlerMeldung` erscheinen — ruft intern `PseudoConsoleSession.Resize` auf; Test prüft nur Fehlerfreiheit, keine sichtbare Terminal-Reaktion. |
| `E2E_ConPtyKeyboardInput.cs` | Sendet `Keyboard.Type("hello")`; Testname (`...KeinFehlerBanner_E2E`, siehe `e2e-timeout-analyse.md`) und fehlende Text-Assertion zeigen: geprüft wird nur, dass keine `FehlerMeldung` erscheint, nicht dass „hello“ im Terminal sichtbar wird. |
| `E2E_ConPtyProcessEnd.cs` | Klickt `CliStoppen` selbst, erwartet danach `WaitUntilGone(..., "CliStoppen", ...)` und dass Status `Gestartet` bestehen bleibt (kein automatischer Rücksprung) — Prozess muss auf `StopCliAsync` (`CloseMainWindow`/`Kill`) sauber reagieren. |
| `E2E_AutoStartCli.cs` | Stoppt CLI manuell, navigiert weg und zurück, erwartet automatischen Neustart (`CliStoppen` erscheint erneut ohne manuellen Start-Klick) — der (simulierte) Prozess muss sich pro Aufgabe mehrfach hintereinander sauber (er)starten lassen. |
| `E2E_PluginWechsel.cs` | Wechselt das Plugin während eines laufenden CLI-Prozesses (`PluginAendern`-Dialog), erwartet danach erneut `CliStoppen` und Status `Gestartet` — Stop des alten + Start eines neuen (simulierten) Prozesses in Folge. |
| `E2E_TaskWechselUeberMenue.cs` | Wechselt zwischen zwei parallelen Aufgaben über das Menü; für Aufgabe B wird ebenfalls `CliStoppen` erwartet — mind. zwei simulierte Prozesse gleichzeitig/nacheinander. |
| `E2E_PluginProjectDefault_NextTask.cs` | Legt nach der ersten eine zweite Aufgabe an und startet erneut — mehrere simulierte Prozesse nacheinander in derselben App-Instanz. |
| Übrige (`E2E_WorkingDirectory`, `E2E_TaskExecutionCommandLineParameters`, `E2E_PluginSelectionDialog`, `E2E_PluginProjectDefault`, `E2E_ConPtyTerminalStart`, `E2E_AufgabeStarten`, `E2E_ArbeitsstatusAktualisierung`) | Nur das gemeinsame Kernkriterium (`CliStoppen` erscheint und bleibt bis zum jeweiligen Testende bestehen); `E2E_WorkingDirectory` und `E2E_AufgabeStarten` prüfen zusätzlich einen vorausgehenden **Fehlerfall** (`FehlerMeldung` bei ungültiger Konfiguration) — dieser Fehlerpfad entsteht vor jedem CLI-Start und ist vom ConPTY-Mechanismus unabhängig. |

Keiner der 14 Tests liest oder assertiert den tatsächlichen Terminal-Ausgabetext (`TerminalControl`-Inhalt, `PseudoConsoleSession.Buffer`-Text) direkt — geprüft wird ausschließlich UI-Zustand (Buttons, Status-Label, Fehlerbanner-Abwesenheit).

## Hilfsmethoden

### `WpfTestBase` (`src/Softwareschmiede.Tests/E2E/WpfTestBase.cs`)
- `LaunchApp(bool ensureDatabaseDeleted = true)` — startet `Softwareschmiede.App.exe` als externen Prozess, setzt `SOFTWARESCHMIEDE_TEST_DB_PATH` prozessweit (relevant: dieselbe Variable steuert bereits den Plugin-Testmodus in `PluginManager.IsTestMode()`).
- `SetupProjectMitNeuerAufgabe(string repositoryFolderName, string projektName, bool useInSourceDirectoryMode = true)` — kompletter Setup-Ablauf bis zur startbereiten Aufgabe.
- `StartenUndPluginWaehlen(AutomationElement mainWindow, string pluginName)` — klickt „Starten“, wählt Plugin im Dialog, bestätigt.
- `SkipWennConPtyNichtVerfuegbar()` — aktuelle Notlösung: überspringt Test bei gesetzter Umgebungsvariable `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` (siehe `ConPtyEnvironmentProbe`); wird durch die neue Teststrategie voraussichtlich überflüssig für die 14 betroffenen Tests.
- `WaitForElement`, `WaitUntilGone`, `WaitForWindow` — generische UI-Wartehilfen, nicht ConPTY-spezifisch.

### `ConPtyEnvironmentProbe` (`src/Softwareschmiede.Tests/E2E/ConPtyEnvironmentProbe.cs`)
- `IsAvailable` — liest `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS`, keine automatische Laufzeit-Erkennung (bewusst deaktiviert, siehe Kommentar im Quellcode: zwei frühere Anläufe mit echter Laufzeit-Probe erzeugten falsche Negative in Visual Studio).
