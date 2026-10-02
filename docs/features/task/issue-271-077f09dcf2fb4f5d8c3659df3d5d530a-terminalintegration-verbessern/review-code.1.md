# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

Hinweis zur Prüftiefe: `dotnet build src/Softwareschmiede/Softwareschmiede.csproj` ist sauber (0 Warnungen/0 Fehler); der Arbeitsstand war zuvor bereits vollständig kompiliert worden (`Softwareschmiede.Tests.dll` neuer als alle Quellen — die Interface-Erweiterung von `IKiPlugin` ist in allen Fake-Implementierungen konsistent nachgezogen). Die für dieses Projekt geforderte UI-Event-Verdrahtung ist geprüft: `TaskDetailViewModel.TerminalSessionGestartet`, `CliGestoppt` und `PromptVorlageGesendet` besitzen alle einen abonnierenden Handler in `TaskDetailView.xaml.cs` (`OnDataContextChanged` Zeile 53–55, Abmeldung in `CleanupViewModel`); `ITerminalSession.BufferChanged`/`RuntimeStatusChanged` sind in `TerminalControl` bzw. `TaskDetailViewModel.AttachCliStatusSession` korrekt an-/abgemeldet.

## Befunde

### TerminalBuffer.cs (TerminalBuffer)

- **Fehlerhafte/inkonsistente Scrollback-Behandlung bei CSI S (SU)** — `Apply`, Zeile 138–142: Bei `ScreenScrolledEvent` wird `pushToScrollback: e.DeltaRows >= _rows` übergeben. Das ist zweifach falsch: (a) SU-Zeilen gehören nach xterm-Semantik grundsätzlich nicht in den Scrollback (im Gegensatz zum Newline-Scroll am Regionsrand); (b) die Bedingung ignoriert `_isAlternateScreen` und `IsFullScrollRegion()` — ein `CSI <n>S` mit `n >= _rows` im Alternate Screen schiebt Alt-Screen-Zeilen in den Hauptscreen-Scrollback, der beim Verlassen des Alt-Screens wieder sichtbar wird. `AdvanceLine` (Zeile 288–301) verwendet dagegen korrekt `IsFullScrollRegion() && !_isAlternateScreen`.

  Empfehlung: `pushToScrollback` für `ScreenScrolledEvent` auf `false` setzen (oder mindestens auf dieselbe Bedingung wie in `AdvanceLine` gaten). Zusätzlich prüfen: `Resize` schiebt beim Verkleinern Zeilen in den Scrollback, auch wenn der Alternate Screen aktiv ist (Zeile 191–199) — Alt-Screen-Zeilen sollten nicht in den Hauptscreen-Scrollback wandern.

### Win32PseudoConsoleProcessLauncher.cs (Win32PseudoConsoleProcessLauncher)

- **Ressourcen-Leck bei Fehler in `CreatePseudoConsoleSession`** — Zeile 76: Wenn der FileStream- oder `PseudoConsoleSession`-Konstruktor wirft, wird zwar in `CreatePseudoConsoleSession` (Zeile 110–116) `inputStream`/`outputStream` disposed, aber `pseudoConsole`, das native `startResult.ProcessHandle` und der bereits gestartete Kindprozess (`process`) bleiben offen bzw. laufen weiter — der Prozess läuft dann als verwaister ConPTY-Kindprozess ohne Session weiter.

  Empfehlung: `CreatePseudoConsoleSession`-Aufruf in `Start` mit try/catch umgeben und im Fehlerfall `PseudoConsoleNativeMethods.CloseHandle(startResult.ProcessHandle)`, `pseudoConsole.Dispose()` und `process.Kill(entireProcessTree: true)`/`process.Dispose()` ausführen.

### TerminalSessionService.cs + TerminalSessionDiagnostics.cs (TerminalSessionService, TerminalSessionDiagnostics)

- **Doppelte Backend-Entscheidungslogik / ungenutztes Ergebnisfeld** — `TerminalPreflightResult.BackendEmpfehlung` (`TerminalSessionDiagnostics.cs:124–128`) wird berechnet, aber nirgends konsumiert: `TerminalSessionService.SelectBackend` (`TerminalSessionService.cs:100–119`) re-implementiert die Entscheidung eigenständig (E2E-Variable, `SupportsPty`, `PtyVerfuegbar`), und auch die `[Terminal-Diagnose]`-Markerzeile enthält die Empfehlung nicht. Die beiden Implementierungen können auseinanderlaufen — die `BackendEmpfehlung` kennt zudem den E2E-Override nicht und wäre dort ohnehin falsch.

  Empfehlung: Entweder `SelectBackend` auf `preflight.BackendEmpfehlung` umstellen (und den E2E-Fall in die Diagnose einziehen) oder das Feld/Enum aus `TerminalPreflightResult` entfernen, wenn es nur Dokumentationszweck hat.

- **Inkonsistente Options-Validierung** — `StartAsync` validiert `ReplayBufferByteBudget` hart (`ArgumentOutOfRangeException`, Zeile 62–63), `DefaultCols`/`DefaultRows` werden dagegen nur als nicht-fataler Preflight-Check protokolliert (`TerminalSessionDiagnostics.cs:111–112`) und dann ungeprüft per `(short)`-Cast an `PseudoConsole.Create` übergeben (`Win32PseudoConsoleProcessLauncher.cs:49`) — Werte > 32767 laufen über bzw. ≤ 0 erzeugen ein ungültiges ConPTY.

  Empfehlung: `DefaultCols`/`DefaultRows` in `StartAsync` ebenfalls hart validieren (sinnvolle Obergrenze ≤ `short.MaxValue`) oder im Options-Objekt clampen.

- **Doppelter Magic-String** — `SOFTWARESCHMIEDE_TEST_DB_PATH` liegt als `private const string TestDatenbankPfadVariable` (`TerminalSessionService.cs:21`) und erneut als Literal in `App.xaml.cs` (~Zeile 288) vor; beide Stellen müssen synchron bleiben, sonst greift der E2E-Override nur halb.

  Empfehlung: Die Konstante zentral sichtbar machen (z. B. `public const` auf einer gemeinsamen Stelle) und in `App.xaml.cs` referenzieren.

### TerminalSessionStartResult.cs (TerminalSessionStartResult)

- **Redundantes Feld / doppelte Ownership** — `NativeProcessHandle` wird von keinem Produktiv-Konsumenten mehr gelesen (`KiAusfuehrungsService` nutzt nur `Process` und `Session`; die Session besitzt das Handle und schließt es in `Dispose`). Einzig `SimulatedPseudoConsoleProcessLauncherTests` liest den Wert. Ein zweiter Handle-Eigentümer in der API lädt zu einem künftigen Doppel-Close ein.

  Empfehlung: Feld aus dem Record entfernen (oder klar als "nur informativ, Ownership liegt bei `Session`" dokumentieren).

### TerminalSessionStartSpec.cs (TerminalSessionStartSpec)

- **Totes Feld** — `UseShellExecute` wird in `CliKiPluginBase.BuildTerminalStartSpec` gesetzt (`CliKiPluginBase.cs:61`), aber nirgends gelesen: Beide Launcher setzen `UseShellExecute = false` hart, der Resolver wertet es nicht aus. Der Kommentar ("nur für den klassischen, nicht-interaktiven Pfad relevant") beschreibt einen Pfad, der die Spec gar nicht konsumiert.

  Empfehlung: Feld entfernen (der klassische Pfad nutzt weiterhin `IKiPlugin.StartCliAsync`/`ProcessStartInfo` direkt).

### PseudoConsoleSession.cs (PseudoConsoleSession)

- **Long Parameter List** — Der interne Konstruktor (Zeile 107–118) hat inzwischen 11 Parameter (`pseudoConsole`, `process`, `inputStream`, `outputStream`, `timeProvider`, `waitingThreshold`, `logger`, `outputSink`, `nativeProcessHandle`, `options`, `isPseudoTerminal`); davon sind die meisten optional/technisch gebündelt.

  Empfehlung: Parameter-Objekt (z. B. `PseudoConsoleSessionContext`) einführen oder die optionalen Parameter (`timeProvider`, `waitingThreshold`, `options`) über einen Test-/Options-Überladungspfad kapseln.

### TerminalControl.cs (TerminalControl)

- **Input-Schreibpfad umgeht die Session-Serialisierung** — `WriteToInputStream` (Zeile 279–290) schreibt Tastaturbytes synchron direkt per `Session.InputStream.Write(bytes)` — am `WriteInputAsync`-Semaphore (`_inputWriteLock`), am 4-KB-Chunking und an dessen Fehlerbehandlung/`Failed`-Event vorbei. Ein zeitgleiches `WritePromptAsync` (zeitgesteuerter Prompt, Promptvorlage) kann sich so byte-genau mit einer Tastatureingabe vermischen. Der Pfad war auch vor der Umstellung so, aber mit der neuen `ITerminalSession.WriteInputAsync`-Abstraktion gibt es jetzt den vorgesehenen serialisierten Weg.

  Empfehlung: `WriteToInputStream` auf `session.WriteInputAsync(bytes)` umstellen (analog `WriteToInputStreamAsync`) statt den Roh-Stream zu verwenden.

- **Toter Code (Vorbestand, in geänderter Datei stehen geblieben)** — Die parameterlose Überladung `ReadClipboardAndInsertAsync()` (Zeile 491–492) hat keinen Aufrufer (der einzige Aufruf in `OnPreviewKeyDown` nutzt die `session`-Überladung).

  Empfehlung: Methode entfernen.

### TerminalExecutableResolver.cs (TerminalExecutableResolver)

- **Zu breiter, stiller Catch-Block** — `Resolve`, Zeile ~110–113: `catch { continue; }` im Verzeichnis-Scan schluckt jede Exception kommentarlos. Inhaltlich umschließt der try nur `Path.Combine`/`File.Exists`/`Path.HasExtension`, die praktisch nicht werfen — der Catch ist damit überflüssig bzw. verschleiert unerwartete Fehler (z. B. `OutOfMemoryException` würde ebenfalls still zu "nächster PATH-Eintrag" führen).

  Empfehlung: try/catch entfernen oder auf konkrete IO-Ausnahmen (`IOException`, `UnauthorizedAccessException`, `PathTooLongException`) einschränken und im `Detail` der Resolution dokumentieren, welche Verzeichnisse übersprungen wurden.

### CliKiPluginBase.cs (CliKiPluginBase)

- **Falscher `paramName` in Exception** — `BuildTerminalStartSpec` (Zeile ~51): `throw new ArgumentException("TerminalSessionStartSpec.FileName darf nicht leer sein.", nameof(localRepoPath))` — der Parametername verweist auf `localRepoPath`, obwohl das Problem das leere `psi.FileName` aus `BuildProcessStartInfo` ist. Der Hinweis führt den Aufrufer auf die falsche Spur.

  Empfehlung: `paramName` weglassen oder auf den tatsächlichen Verursacher verweisen (z. B. `nameof(parameters)` ist ebenfalls falsch — besser gar kein `paramName` oder eine `InvalidOperationException` mit Plugin-/Kontextangabe).

### AnsiSequenceParserTests.cs (AnsiSequenceParserTests)

- **Inkonsistente Literal-Schreibweise / Steuerzeichen im Quelltext** — Zeilen 254 und 256: `[InlineData("<ESC>" + "7", false)]` und `[InlineData("<ESC>" + "8", true)]` enthalten ein rohes ESC-Zeichen (0x1B) im String-Literal, während alle anderen Attribute in derselben Datei `"\x1b"` verwenden. Das Steuerzeichen ist im Editor unsichtbar und encoding-empfindlich.

  Empfehlung: Einheitlich `"\x1b" + "7"` bzw. `"\x1b" + "8"` schreiben.

## Geprüfte Dateien

Liste aller geprüften Quelldateien (Dokumentationsänderungen unter `docs/` wurden nicht reviewet):

- `src/Softwareschmiede.Plugin.Contracts/Domain/Enums/TerminalProviderCapabilities.cs` (neu)
- `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/TerminalSessionStartSpec.cs` (neu)
- `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IKiPlugin.cs`
- `src/Softwareschmiede.Plugin.Contracts/Domain/Abstractions/CliKiPluginBase.cs`
- `src/Softwareschmiede/Infrastructure/Terminal/ITerminalSession.cs` (neu)
- `src/Softwareschmiede/Infrastructure/Terminal/ITerminalSessionFactory.cs` (neu)
- `src/Softwareschmiede/Infrastructure/Terminal/TerminalSessionService.cs` (neu)
- `src/Softwareschmiede/Infrastructure/Terminal/TerminalSessionDiagnostics.cs` (neu)
- `src/Softwareschmiede/Infrastructure/Terminal/TerminalExecutableResolver.cs` (neu)
- `src/Softwareschmiede/Infrastructure/Terminal/TerminalReplayBuffer.cs` (neu)
- `src/Softwareschmiede/Infrastructure/Terminal/TerminalSessionOptions.cs` (neu)
- `src/Softwareschmiede/Infrastructure/Terminal/TerminalSessionStartResult.cs` (neu)
- `src/Softwareschmiede/Infrastructure/Terminal/TerminalOutputChunkEventArgs.cs` (neu)
- `src/Softwareschmiede/Infrastructure/Terminal/TerminalSessionExitedEventArgs.cs` (neu)
- `src/Softwareschmiede/Infrastructure/Terminal/TerminalSessionFailedEventArgs.cs` (neu)
- `src/Softwareschmiede/Infrastructure/Terminal/PseudoConsoleSession.cs`
- `src/Softwareschmiede/Infrastructure/Terminal/IPseudoConsoleProcessLauncher.cs`
- `src/Softwareschmiede/Infrastructure/Terminal/Win32PseudoConsoleProcessLauncher.cs`
- `src/Softwareschmiede/Infrastructure/Terminal/SimulatedPseudoConsoleProcessLauncher.cs`
- `src/Softwareschmiede/Infrastructure/Terminal/AnsiSequenceParser.cs`
- `src/Softwareschmiede/Infrastructure/Services/CliSessionService.cs` (gelöscht — Referenzfreiheit verifiziert)
- `src/Softwareschmiede/Infrastructure/Services/ICliSessionService.cs` (gelöscht)
- `src/Softwareschmiede/Domain/Terminal/TerminalBuffer.cs`
- `src/Softwareschmiede/Domain/Terminal/TerminalEvents.cs`
- `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs`
- `src/Softwareschmiede/Application/Services/CliProcessManager.cs`
- `src/Softwareschmiede/Application/Services/EntwicklungsprozessService.cs`
- `src/Softwareschmiede/Application/Services/ProjektleiterAgentService.cs`
- `src/Softwareschmiede/Application/Services/PromptZeitVersandService.cs`
- `src/Softwareschmiede.App/App.xaml.cs`
- `src/Softwareschmiede.App/Controls/TerminalControl.cs`
- `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs`
- `src/Softwareschmiede.App/Views/TaskDetailView.xaml.cs`
- `src/Softwareschmiede/appsettings.json`
- `plugins/Softwareschmiede.Plugin.ClaudeCli/ClaudeCliPlugin.cs`
- `plugins/Softwareschmiede.Plugin.Codex/CodexPlugin.cs`
- `plugins/Softwareschmiede.Plugin.Devin/DevinPlugin.cs`
- `plugins/Softwareschmiede.Plugin.GitHubCopilot/GitHubCopilotPlugin.cs`
- `plugins/Softwareschmiede.Plugin.KiSimulator/KiSimulatorPlugin.cs`
- `src/Softwareschmiede.Tests/Helpers/TestTerminalSessionFactory.cs` (neu)
- `src/Softwareschmiede.Tests/Helpers/KiPluginMockExtensions.cs` (neu)
- `src/Softwareschmiede.Tests/Helpers/TestKiAusfuehrungsServiceFactory.cs`
- `src/Softwareschmiede.Tests/Helpers/TestPseudoConsoleSessionFactory.cs`
- `src/Softwareschmiede.Tests/Helpers/ProjektleiterAgentServiceTestDatenFactory.cs`
- `src/Softwareschmiede.Tests/Infrastructure/Terminal/TerminalSessionServiceTests.cs` (neu)
- `src/Softwareschmiede.Tests/Infrastructure/Terminal/TerminalSessionDiagnosticsTests.cs` (neu)
- `src/Softwareschmiede.Tests/Infrastructure/Terminal/TerminalExecutableResolverTests.cs` (neu)
- `src/Softwareschmiede.Tests/Infrastructure/Terminal/TerminalReplayBufferTests.cs` (neu)
- `src/Softwareschmiede.Tests/Infrastructure/Terminal/PseudoConsoleSessionTests.cs`
- `src/Softwareschmiede.Tests/Infrastructure/Terminal/SimulatedPseudoConsoleProcessLauncherTests.cs`
- `src/Softwareschmiede.Tests/Infrastructure/Terminal/AnsiSequenceParserTests.cs`
- `src/Softwareschmiede.Tests/Domain/Terminal/TerminalBufferTests.cs`
- `src/Softwareschmiede.Tests/Domain/Abstractions/CliKiPluginBaseTests.cs`
- `src/Softwareschmiede.Tests/App/Controls/TerminalControlTests.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests_PluginAktivierung.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests_ZeitgesteuerterPrompt.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/IssueCreateDialogViewModelTests.cs`
- `src/Softwareschmiede.Tests/App/Views/IssueCreateDialogUiTests.cs`
- `src/Softwareschmiede.Tests/Application/Services/CliProcessManagerTests.cs`
- `src/Softwareschmiede.Tests/Application/Services/CliProcessManagerTests_AktiverLauf.cs`
- `src/Softwareschmiede.Tests/Application/Services/CliProcessManagerTests_LaufStatus.cs`
- `src/Softwareschmiede.Tests/Application/Services/EntwicklungsprozessServiceTests.cs`
- `src/Softwareschmiede.Tests/Application/Services/KiAusfuehrungsServiceTests.cs`
- `src/Softwareschmiede.Tests/Application/Services/KiAusfuehrungsServiceTests_WorkingDirectory.cs`
- `src/Softwareschmiede.Tests/Application/Services/KiAusfuehrungsServiceTests_WorkingDirectory_InSourceDirectory.cs`
- `src/Softwareschmiede.Tests/Application/Services/ProjektleiterAgentServiceTests_CliIntegration.cs`
- `src/Softwareschmiede.Tests/Application/Services/ProjektleiterAgentServiceTests_Fehlerfaelle.cs`
- `src/Softwareschmiede.Tests/Application/Services/PromptZeitVersandServiceTests.cs`
- `src/Softwareschmiede.Tests/Infrastructure/Plugins/KiSimulatorPluginTests.cs`
- `src/Softwareschmiede.Tests/ServiceIntegration/CliEmbeddingServiceIntegrationTests.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_TerminalFallbackDiagnose.cs` (neu)
- `src/Softwareschmiede.Tests/E2E/E2E_ConPtyLifecycle.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_PluginAuswahlUndWechsel.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_SessionLimitPause.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_SettingsKiPluginPersistence.cs`
- `src/Softwareschmiede.Tests/E2E/MainTest.cs`
- `src/Softwareschmiede.IntegrationTests/Services/EntwicklungsprozessServiceTests.cs`
