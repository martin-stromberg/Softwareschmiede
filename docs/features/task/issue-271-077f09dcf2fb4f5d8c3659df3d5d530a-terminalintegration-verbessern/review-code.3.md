# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

Hinweis zur Prüftiefe (Runde 3): `dotnet build src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj` ist sauber (0 Warnungen/0 Fehler). **Alle Befunde aus Runde 2 sind am echten Code verifiziert behoben:**

- `KiAusfuehrungsService.StartTerminalSessionAsync`: `_handles[aufgabeId] = handle` steht jetzt **vor** der Event-Verdrahtung (`KiAusfuehrungsService.cs:239` vor `session.Exited +=`/`session.Failed +=` in `:241–242`), der Kommentar stimmt mit der Reihenfolge überein; die Doppel-Endzustand-Gefahr wird zusätzlich durch den Guard in `:258–261` abgefangen (kein `Gestartet`-Event mehr, wenn ein zwischenzeitliches `Exited`/`Failed` das Handle bereits entfernt hat). `HandleExitedCoreAsync` bleibt über atomares `TryRemove` + `ReferenceEquals`-Check (`:450–462`) genau-einmalig.
- `SimulatedPseudoConsoleProcessLauncher.Start`: Session-Erzeugung ist jetzt in try/catch (`:62–84`) mit `process.Kill(entireProcessTree: true)` + `process.Dispose()` im Fehlerfall — symmetrisch zum Win32-Launcher.
- `TerminalControl.OnSessionChanged`: `session.BufferChanged += OnBufferChanged` steht jetzt **vor** `RebuildBufferFromReplay()` (`TerminalControl.cs:120` vor `:124`), das Race-Fenster ist geschlossen; die anschließende `InvalidateVisual` deckt den Initial-Render ab.
- `CrSubmittingInputStream`: `_pendingCr`-Carry-Byte korrekt implementiert (`SimulatedPseudoConsoleProcessLauncher.cs:193–234`) — `\r\n` an Chunk-Grenzen bleibt ein Paar, einzelnes abschließendes `\r` wird bei Flush/Dispose zu `\r\n` aufgelöst; alle Eckfälle (Chunk endet mit `\r`, `\n` folgt / folgt nicht, leere Folgeschreiboperation) sind durch neue Tests in `SimulatedPseudoConsoleProcessLauncherTests.cs:143–202` belegt.
- `TerminalExecutableResolver.IsPeImage`: Catch eingeschränkt auf `IOException or UnauthorizedAccessException` (`:141`) — `PathTooLongException` ist eine `IOException`-Subklasse und damit mit abgedeckt.
- `TerminalSessionDiagnostics`: Terminalgrößen-Check jetzt `> 0 and <= short.MaxValue` (`:113–114`), synchron zur Hartvalidierung in `TerminalSessionService.StartAsync` (`:69–72`); belegt durch Theorie-Test `TerminalSessionDiagnosticsTests.cs:113–135`.
- `IPseudoConsoleProcessLauncher`-XML-Doc aktualisiert (`:16–17`); stale Verweis in `AutonomAufgabeDetailViewModel.cs:129` auf `StartTerminalSessionAsync` korrigiert.
- `TaskDetailView.xaml:667`: `AutomationProperties.AutomationId="CliStatusText"` statt `Name`; E2E-Helper `GetCliStatusText` nutzt `ByAutomationId("CliStatusText")` (`E2E/Views/TaskDetailView.cs:424`).
- Test-Reflection-Helper auf `(ITerminalSession)`-Signatur umgestellt (`TerminalControlTests.ClipboardPaste.cs:335–340` mit `typeof(ITerminalSession)`-Parameterarray; `KiAusfuehrungsServiceTests.cs:418` `AssertSessionDisposed(ITerminalSession)`).

Die geforderte UI-Event-Verdrahtung ist weiterhin korrekt: `TerminalSessionGestartet`/`CliGestoppt`/`PromptVorlageGesendet` in `TaskDetailView.xaml.cs:53–55` (Abmeldung `:71–73` inkl. `Unloaded`-Handler); `BufferChanged` in `TerminalControl:97/120`; `RuntimeStatusChanged` in `TaskDetailViewModel.AttachCliStatusSession:1954–1961` (Abmeldung via `AttachCliStatusSession(null)` in Dispose `:1752`) sowie `CliProcessManager.SubscribeRuntimeStatus`/`UnsubscribeRuntimeStatus`.

Verifizierte Nebenprüfung (kein Befund): `TestTerminalSessionFactory.DeterministicPseudoConsoleProcessLauncher` legt `Process.GetCurrentProcess()` im Handle ab — ein `KiAusfuehrungsService.Dispose` würde den Test-Host killen, wenn `Kill(entireProcessTree: true)` auf den eigenen Prozess griffe. Empirisch verifiziert: .NET wirft dabei `InvalidOperationException` ("Cannot be used to terminate a process tree containing the calling process"), die vom vorhandenen `catch (Exception)` in `Dispose` geschluckt wird — harmlos, aber erwähnenswert, falls das Muster je in Produktivcode wandert.

## Befunde

### KiAusfuehrungsService.cs (KiAusfuehrungsService)

- **Fehlende Fehler-Status-Mapping im Early-Exit-Pfad** — `StartTerminalSessionAsync`, Zeile 244–254: Wenn der Prozess bereits vor der Event-Verdrahtung beendet wurde (Session hat `Exited` bereits gefeuert, bevor `session.Exited +=` lief), räumt der Block auf und meldet **immer** `CliProcessStatus.Gestoppt` — anders als `HandleExitedCoreAsync` (`:475–488`), das bei `exitCode != 0` `CliProcessStatus.Fehler` meldet und `PersistFehlgeschlagenAsync` (Protokoll-Eintrag "CLI-Prozess mit Fehler beendet") anstößt. Eine CLI, die sofort mit Fehlercode stirbt (z. B. ungültige Plugin-Argumente, `cmd.exe /c exit 1`), verliert so Fehlerstatus und Fehler-Protokolleintrag. Der Exit-Code ist zu diesem Zeitpunkt verfügbar: `RaiseExited` in `PseudoConsoleSession` setzt `session.ExitCode` *vor* dem Event-Invoke (`PseudoConsoleSession.cs:400–403`), zusätzlich liefert `TryGetExitCode(process)` den Code.

  Empfehlung: Statt der duplizierten Cleanup-Sequenz `await HandleSessionEndedAsync(aufgabeId, handle, session.ExitCode ?? TryGetExitCode(process), "Terminal")` aufrufen — das atomare `TryRemove` in `HandleExitedCoreAsync` hält die Genau-einmal-Semantik auch gegen ein parallel zugestelltes `Exited`, und Fehler-Mapping/Persistierung laufen identisch zum normalen Exited-Pfad.

- **Residual: vor der Verdrahtung ausgelöstes `Failed` ist nicht detektierbar** — `StartTerminalSessionAsync`, Zeile 241–261: Der in Runde 2 benannte Fall "`Failed` (Leseschleifen-Fehler) bei noch laufendem Prozess" ist nur für Events *nach* der Registrierung gelöst. Die Leseschleife startet bereits im `PseudoConsoleSession`-Konstruktor innerhalb von `_sessionFactory.StartAsync` (`PseudoConsoleSession.cs:137`) — ein `Failed`, das zwischen Session-Erzeugung und `session.Failed +=` feuert, während der Prozess weiterläuft, geht verloren: `process.HasExited` (`:247`) greift nicht (Prozess lebt), das Handle bleibt als "Gestartet" registriert, obwohl die Leseschleife tot ist (keine weitere Ausgabe, Pipe kann volllaufen). Die Session exponiert keinen beobachtbaren Fehlerzustand (`ExitCode` bleibt `null`, `RaiseExited` kehrt bei laufendem Prozess ohne Event zurück, `:371–373`).

  Empfehlung: Fehlerzustand auf `ITerminalSession` sichtbar machen (z. B. `bool HasFailed` oder `Exception? Failure`, gesetzt in `RaiseFailed`) und nach der Event-Registrierung prüfen — analog zum `process.HasExited`-Recheck. Alternativ kann `RaiseFailed` bei noch laufendem Prozess zusätzlich eine interne Zustandsmarkierung setzen, die `HandleSessionFailedAsync` nachträglich auslöst.

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
- `src/Softwareschmiede/Infrastructure/Services/CliSessionService.cs` (gelöscht — Referenzfreiheit in Runde 2 verifiziert)
- `src/Softwareschmiede/Infrastructure/Services/ICliSessionService.cs` (gelöscht)
- `src/Softwareschmiede/Infrastructure/Plugins/PluginManager.cs`
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
- `src/Softwareschmiede.App/ViewModels/AutonomAufgabeDetailViewModel.cs`
- `src/Softwareschmiede.App/Views/TaskDetailView.xaml`
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
- `src/Softwareschmiede.Tests/App/Controls/TerminalControlTests.ClipboardPaste.cs`
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
