# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

Hinweis zur Prüftiefe (Runde 2): `dotnet build src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj` ist sauber (0 Warnungen/0 Fehler, inkl. `Softwareschmiede.App` und allen Plugins). **Alle Befunde aus Runde 1 sind am echten Code verifiziert behoben:** `TerminalBuffer` (SU ohne Scrollback-Push, `ScreenScrolledEvent` → `pushToScrollback: false`, Zeile 138–146; `Resize` gated auf `!_isAlternateScreen`, Zeile 199–201 — jeweils mit neuen Tests in `TerminalBufferTests` belegt); `Win32PseudoConsoleProcessLauncher` (try/catch-Aufräumung um `CreatePseudoConsoleSession`, Zeile 77–91, inkl. `process.Kill(entireProcessTree: true)` + `CloseHandle` + `pseudoConsole.Dispose()`; zusätzlich Cleanup bei `GetProcessById`-Fehlschlag, Zeile 69–74); `SelectBackend` konsumiert jetzt `preflight.BackendEmpfehlung` als einzige Entscheidungslogik (`TerminalSessionService.cs:117`, E2E-Override davor); harte `DefaultCols`/`DefaultRows`-Validierung (`:69–72`) inkl. Theorie-Test; `TestDatenbankPfadVariable` als `public const` (`:23`), referenziert aus `App.xaml.cs:288`, `PluginManager.cs:113`, `UpdateE2ETestConfiguration.cs:19`; `NativeProcessHandle` aus `TerminalSessionStartResult` entfernt; `UseShellExecute` aus `TerminalSessionStartSpec` entfernt; `PseudoConsoleSessionContext`-Record eingeführt (Konstruktor 5 Parameter); `TerminalControl.WriteToInputStream` geht über `session.WriteInputAsync` (`TerminalControl.cs:282–288`); tote parameterlose `ReadClipboardAndInsertAsync()` entfernt; Resolver-Scan-Catch entfernt; `CliKiPluginBase` wirft `InvalidOperationException` (`:50–51`); `"\x1b"`-Literale vereinheitlicht (`AnsiSequenceParserTests.cs:254/256`).

Die geforderte UI-Event-Verdrahtung ist geprüft: `TaskDetailViewModel.TerminalSessionGestartet`, `CliGestoppt`, `PromptVorlageGesendet` werden in `TaskDetailView.xaml.cs` an-/abgemeldet (`OnDataContextChanged` Zeile 53–55, `UnsubscribeAndDispose` Zeile 66–75, zusätzlich `Unloaded`-Handler); `ITerminalSession.BufferChanged` wird in `TerminalControl.OnSessionChanged` verwaltet (`:96–125`), `RuntimeStatusChanged` in `TaskDetailViewModel.AttachCliStatusSession` (`:1954–1961`, Abmeldung auch über `Dispose` → `AttachCliStatusSession(null)`, `:1752`) sowie in `CliProcessManager.SubscribeRuntimeStatus`/`UnsubscribeRuntimeStatus` (`:191–222`).

## Befunde

### KiAusfuehrungsService.cs (KiAusfuehrungsService)

- **Kommentar widerspricht der Code-Reihenfolge / enges Race-Fenster bei Event-Verdrahtung** — `StartTerminalSessionAsync`, Zeile 235–241: Der Kommentar behauptet „Das Handle wird vor der Event-Verdrahtung eingetragen, damit ein bereits ausgelöstes Exited das Handle vorfindet", aber `_handles[aufgabeId] = handle;` steht *nach* den `session.Exited +=`/`session.Failed +=`-Registrierungen. Ein zwischen Registrierung und Eintrag ausgelöstes `Exited`/`Failed` wird in `HandleExitedCoreAsync` stillschweigend verworfen (`_handles.TryRemove` schlägt fehl → frühes `return` ohne Cleanup und ohne Status-Event). Für `Exited` fängt der `process.HasExited`-Check (Zeile 246) das Fenster noch ab; ein `Failed` (Leseschleifen-Fehler) bei noch laufendem Prozess hingegen liefe ins Leere — das Handle würde mit einer toten Session als „Gestartet" registriert bleiben, bis der Prozess irgendwann selbst endet.

  Empfehlung: `_handles[aufgabeId] = handle;` vor die Event-Registrierung ziehen (analog `StartCliAsync`, Zeile 136–138, wo der Handle bewusst *vor* `process.Start()` eingetragen wird) und den Kommentar beibehalten — oder Kommentar an die tatsächliche Reihenfolge anpassen.

### SimulatedPseudoConsoleProcessLauncher.cs (SimulatedPseudoConsoleProcessLauncher)

- **Asymmetrischer Ressourcen-Cleanup bei Fehler nach Prozessstart** — `Start`, Zeile 58–72: Nach erfolgreichem `Process.Start(psi)` kann `new PseudoConsoleSession(...)` noch werfen; der gestartete `cmd.exe`-Kindprozess läuft dann verwaist und unbeobachtet weiter (kein `process.Kill`/`Dispose`). Genau dieser Fehlermodus wurde in Runde 1 am `Win32PseudoConsoleProcessLauncher` behoben (Zeile 77–91) — auf dem Pipe-Pfad fehlt das Gegenstück.

  Empfehlung: Session-Erzeugung in try/catch umgeben und im Fehlerfall `process.Kill(entireProcessTree: true)`/`process.Dispose()` ausführen (gleiche Best-Effort-Form wie im Win32-Launcher).

### TerminalControl.cs (TerminalControl)

- **Kleines Race-Fenster zwischen Replay-Rebuild und Event-Subscription** — `OnSessionChanged`, Zeile 119–125: `session.RebuildBufferFromReplay()` läuft, bevor `session.BufferChanged += OnBufferChanged` registriert wird. Ein Ausgabe-Chunk, der genau dazwischen in der Leseschleife verarbeitet wird, landet zwar korrekt im Buffer, löst aber keine `InvalidateVisual` aus — die Anzeige bleibt bis zum nächsten Chunk (oder einem Resize) um einen Chunk zurück. Trivial zu beheben durch Subscription vor dem Rebuild (der Rebuild läuft unter `_renderLock`, das Event feuert erst danach).

  Empfehlung: `session.BufferChanged += OnBufferChanged;` vor `RebuildBufferFromReplay()` setzen (die Subscription ist idempotent gegenüber der anschließenden `InvalidateVisual`-Zeile 128, die den Initial-Render ohnehin auslöst).

### IPseudoConsoleProcessLauncher.cs (IPseudoConsoleProcessLauncher)

- **Veralteter XML-Doc-Hinweis** — `Start`, Zeile 16–17: Der `<returns>`-Text nennt noch „nativem Prozess-Handle", obwohl `TerminalSessionStartResult.NativeProcessHandle` in dieser Nacharbeit entfernt wurde (Ownership liegt jetzt ausschließlich bei der Session).

  Empfehlung: Doc anpassen auf „mit Prozess, Session und Backend-Kennzeichnung".

### TerminalExecutableResolver.cs (TerminalExecutableResolver)

- **Verbliebener pauschaler Catch in `IsPeImage`** — Zeile 134–145: `catch { return false; }` schluckt weiterhin jede Exception kommentarlos. Der in Runde 1 kritisierte Scan-Schleifen-Catch ist entfernt; hier bleibt das Muster, allerdings inhaltlich vertretbarer (2-Byte-Magic-Probe). Eine Einschränkung auf `IOException`/`UnauthorizedAccessException`/`PathTooLongException` wäre trotzdem sauberer und verhindert, dass unerwartete Fehler (z. B. `OutOfMemoryException`) als „kein PE-Image" interpretiert werden.

  Empfehlung: Catch auf konkrete IO-Ausnahmen einschränken.

### TerminalSessionDiagnostics.cs (TerminalSessionDiagnostics)

- **Inkonsistente Grenze im Terminalgrößen-Check** — Zeile 111: `sizeOk` prüft nur `> 0`, während `TerminalSessionService.StartAsync` (Zeile 69–72) zusätzlich `<= short.MaxValue` hart validiert. Der Check würde bei einem überlaufenden Wert (z. B. 40000) „OK" melden, obwohl der Start damit hart scheitert — der Check ist dadurch in diesem Zustand widersprüchlich (in der Service-Pipeline zwar unerreichbar, als eigenständig konsumierbares Preflight-Ergebnis aber falsch positiv).

  Empfehlung: Check-Bedingung auf dieselbe Grenze angleichen (`> 0 && <= short.MaxValue`), damit Check und harte Validierung synchron bleiben.

### AutonomAufgabeDetailViewModel.cs (Doku-Nebeneffekt)

- **Veralteter Kommentarverweis** — Zeile 129 referenziert `KiAusfuehrungsService.StartWithPseudoConsoleAsync`; die Methode heißt jetzt `StartTerminalSessionAsync`. Die Datei selbst wurde im Diff nicht angefasst — der Verweis ist durch die Umbenennung in diesem Branch stale geworden.

  Empfehlung: Kommentar auf `StartTerminalSessionAsync` aktualisieren.

### SimulatedPseudoConsoleProcessLauncher.cs (CrSubmittingInputStream)

- **CR/LF-Trennung an Chunk-Grenzen nicht berücksichtigt** — `Translate` (Zeile 137–165) arbeitet pro `Write*`-Aufruf: Endet ein Chunk mit `\r` und beginnt der nächste mit `\n` (möglich, weil `WriteInputAsync` in 4-KB-Stücken schreibt), entsteht `\r\n` + `\n` — ein zusätzlicher Zeilenvorschub, der im Pipe-Backend einen leeren Zeilen-Submit auslöst. Sehr unwahrscheinlich (betrifft nur `\r\n`-Paare exakt an der 4096-Byte-Grenze), aber ein reales Korrektheitseck des neuen Übersetzers.

  Empfehlung: Ein ausstehendes `\r` am Chunk-Ende puffern (ein Byte „carry" im Stream-Zustand) und erst beim nächsten Schreibaufruf entscheiden, ob ein `\n` folgt.

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
- `src/Softwareschmiede/Infrastructure/Terminal/PseudoConsoleSessionContext.cs` (neu)
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
- `src/Softwareschmiede.App/Services/Testing/UpdateE2ETestConfiguration.cs`
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
- `src/Softwareschmiede.Tests/E2E/Views/TaskDetailView.cs`
- `src/Softwareschmiede.IntegrationTests/Services/EntwicklungsprozessServiceTests.cs`
