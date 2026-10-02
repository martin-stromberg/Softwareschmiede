# Plan-Review

## Ergebnis

**Status:** Vollständig umgesetzt

Alle im Plan geforderten Elemente wurden im Code verifiziert (Basis: uncommittete Changes auf `task/issue-271-…`, Diff gegen `b092b64`). Die untenstehenden Namens-/Strukturabweichungen sind dokumentiert, aber funktional gleichwertig — keine offenen Aufgaben.

## Umgesetzte Planelemente

### Neue Klassen/Interfaces (Contracts)

- [x] `TerminalProviderCapabilities` ([Flags]-Enum, `src/Softwareschmiede.Plugin.Contracts/Domain/Enums/TerminalProviderCapabilities.cs`) — `None=0`, `SupportsPty=1`, `RequiresPty=2`
- [x] `TerminalSessionStartSpec` (record, `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/TerminalSessionStartSpec.cs`) — alle 8 geforderten Felder vorhanden (`FileName`, `Arguments`, `WorkingDirectory`, `EnvironmentVariables`, `Capabilities`, `PluginName`, `OptionalParameters`, `UseShellExecute`)
- [x] `IKiPlugin.TerminalCapabilities`-Property + `GetTerminalStartSpecAsync` (`src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IKiPlugin.cs:29,36`) — einziger Spec-Weg für den interaktiven Pfad
- [x] `CliKiPluginBase`: virtuelle `TerminalCapabilities` (Default `SupportsPty`, `:36`), `GetTerminalStartSpecAsync` (`:39`), `BuildTerminalStartSpec` mit Mapping aus `BuildProcessStartInfo` + `FileName`-Validierung → `ArgumentException` (`:46-50`)
- [x] Capability-Overrides `RequiresPty | SupportsPty` in `ClaudeCliPlugin.cs:29`, `CodexPlugin.cs:32`, `GitHubCopilotPlugin.cs:34`, `DevinPlugin.cs:33`; `KiSimulatorPlugin` behält Default
- [x] `KiSimulatorPlugin.BuildProcessStartInfo` → `cmd.exe /k "echo KI-Simulator läuft..."` (`KiSimulatorPlugin.cs:59-60`), `ping -n 31` entfernt

### Neue Klassen/Interfaces (Infrastructure.Terminal)

- [x] `ITerminalSession` (`src/Softwareschmiede/Infrastructure/Terminal/ITerminalSession.cs`) — alle geforderten Member: `InputStream`, `OutputStream`, `Process`, `Buffer`, `RuntimeStatus`, `IsPseudoTerminal`, `ExitCode`, `WriteInputAsync`, `WritePromptAsync`, `Resize`, `MarkInputActivity`, `MarkOutputActivity`, `DrainOutputAsync`, `RebuildBufferFromReplay`, Events `OutputChunk`/`Exited`/`Failed`/`BufferChanged`/`RuntimeStatusChanged`, `IDisposable`
- [x] `TerminalOutputChunkEventArgs` (`ReadOnlyMemory<byte> Data`), `TerminalSessionExitedEventArgs` (`int? ExitCode`), `TerminalSessionFailedEventArgs` (`Error` + `Phase`) — eigene Dateien
- [x] `ITerminalSessionFactory` (`ITerminalSessionFactory.cs`) — `StartAsync(aufgabeId, spec, outputSink, healthCheck, ct)` → `TerminalSessionStartResult`
- [x] `TerminalSessionService` (`TerminalSessionService.cs`) — `IOptions<TerminalSessionOptions>` + `IServiceScopeFactory` + `ptyLauncher`/`pipeLauncher` (interface-typisiert) + Logger; liest `Terminal.ForcePtyUnavailable` via `AppEinstellungService` (`:143-157`); defensive `ReplayBufferByteBudget > 0`-Validierung (`:62`); `FileName`-Leer-Check → `ArgumentException` (`:58`); Resolver vor Preflight (`:67-70`); Fehlerfälle `NotFound`/`NotExecutable` und `RequiresPty && !PtyVerfuegbar` → `[Terminal-Diagnose]`-Marker via `outputSink.OnOutputChunk` (UTF-8, `\r\n`) + `InvalidOperationException` (`:72-84`); Backend-Reihenfolge E2E→Pipe, `!SupportsPty`→Pipe+Diagnose, PTY→PTY sonst Pipe+Diagnose (`SelectBackend :100-119`); Launcher erhalten `resolution.NormalizedSpec` (`:94`)
- [x] `TerminalExecutableResolver` + `TerminalExecutableResolution` + `TerminalExecutableStatus` (`TerminalExecutableResolver.cs`) — `Direct`/`CmdWrapped`/`NotFound`/`NotExecutable`; Suchreihenfolge `WorkingDirectory` → `PATH` (Spec-Env vor Prozess-Env); PATHEXT aus Spec-Env → Prozess-Env → Default `.COM;.EXE;.BAT;.CMD`; `FileName` mit Verzeichnisanteil → nur dieses Verzeichnis; Erweiterung → nur literal; `.cmd`/`.bat` → `cmd.exe /d /s /c "<pfad>" <args>`; erweiterungslose Nicht-PE-Treffer → übersprungen/`NotExecutable`
- [x] `TerminalSessionDiagnostics` + `TerminalPreflightResult`/`TerminalPreflightCheck` + `TerminalBackendEmpfehlung` (`TerminalSessionDiagnostics.cs`) — DB-frei; OS-Build ≥ 17763 mit `forcePtyUnavailable`-Erzwingung; Executable-Check aus `resolution` (Unterscheidung nicht gefunden/nicht ausführbar); `healthCheck`-Delegate nur bei erfolgreicher Auflösung, `false`/Exception → nicht-fataler `Ok=false`-Eintrag (`:92-107`); Encoding, Terminalgröße aus Options, Pluginparameter; `PtyVerfuegbar` + `BackendEmpfehlung`
- [x] `TerminalSessionStartResult` (record: `Process`, `ITerminalSession`, `NativeProcessHandle`, `IsPseudoTerminal`)
- [x] `TerminalReplayBuffer` — Ringpuffer mit Byte-Budget, `Append(ReadOnlySpan<byte>)`, `GetChunks()`, `ArgumentOutOfRangeException` bei Budget ≤ 0
- [x] `TerminalSessionOptions` — `SectionName = "Terminal"`, `ReplayBufferByteBudget` = 512 KiB, `DefaultCols`/`DefaultRows` = 220/50

### Neue TerminalEvent-Records (`src/Softwareschmiede/Domain/Terminal/TerminalEvents.cs`)

- [x] `AlternateScreenChangedEvent` (`Enabled` statt Plan-Name `Active` — siehe Hinweise)
- [x] `LinesInsertedEvent` / `LinesDeletedEvent` (CSI `L`/`M`)
- [x] `CharsInsertedEvent` / `CharsDeletedEvent` / `CharsErasedEvent` (CSI `@`/`P`/`X`)
- [x] `ScrollRegionChangedEvent` (Plan-Name `ScrollRegionSetEvent`, CSI `r`/DECSTBM)
- [x] `ScreenScrolledEvent` (`int DeltaRows` mit Vorzeichen statt `Count`+`Up`, CSI `S`/`T`)
- [x] `CursorSavedEvent(bool Restored)` (Plan: zwei Records `CursorSavedEvent`/`CursorRestoredEvent` — als ein Record mit Flag umgesetzt, ESC `7`/`8` und CSI `s`/`u`)
- [x] `TerminalResetEvent` (ESC `c`/RIS)

### Geänderte bestehende Klassen

- [x] `PseudoConsoleSession` → implementiert `ITerminalSession` (`PseudoConsoleSession.cs:14`); Events `OutputChunk`/`Exited`/`Failed` (`:78-84`); `IsPseudoTerminal`/`ExitCode` (`:87-90`); Konstruktorparameter `nativeProcessHandle` + `TerminalSessionOptions` (`:102,117`); `ReadLoopAsync`-Reihenfolge MarkOutputActivity → Replay-Append → Sink → `OutputChunk` → Parse+Apply unter `_renderLock` → `BufferChanged` (`:313-329`); `RebuildBufferFromReplay` mit `Buffer.Reset()` + frischem `AnsiSequenceParser` unter Render-Lock (`:349-359`); `GetReplayChunks` (`:363`); `Resize`-Dedupe + Serialisierung über `_resizeLock` (`:180-196`); `Exited` via `Process.Exited`/Stream-Ende mit `GetExitCodeProcess` auf nativem Handle bzw. `Process.ExitCode`, STILL_ACTIVE-geschützt, einmalig via `Interlocked` (`:369-422`); `Dispose` schließt natives Handle (`:238-241`); `Failed` bei Read-/Write-Fehlern (`:302,338,539`)
- [x] `IPseudoConsoleProcessLauncher.Start` → `(Guid, TerminalSessionStartSpec, ITerminalOutputSink?)` → `TerminalSessionStartResult`; `IsPseudoTerminal`-Property (`IPseudoConsoleProcessLauncher.cs:9,18`)
- [x] `Win32PseudoConsoleProcessLauncher` — `IOptions<TerminalSessionOptions>`-Konstruktorparameter; `PseudoConsole.Create(DefaultCols, DefaultRows)` (`:49`); Direct-Start der Spec ohne cmd.exe-Hülle; `options.Value` an `PseudoConsoleSession` (`:107`); `IsPseudoTerminal = true`
- [x] `SimulatedPseudoConsoleProcessLauncher` — `IOptions<TerminalSessionOptions>`; Direct-Start auf Pipes; `CrSubmittingInputStream` beibehalten (`:65,84`); `IsPseudoTerminal = false`; `options.Value` an Session (`:70`)
- [x] `KiAusfuehrungsService` — Pflichtparameter `ITerminalSessionFactory sessionFactory` (`:33`); `StartTerminalSessionAsync` (`:182`) mit `GetTerminalStartSpecAsync` + `kiPlugin.CheckHealthAsync` als Delegate (`:209,219`); `session.Exited`/`Failed`-Verdrahtung (`:238-239`); `GetTerminalSession` → `ITerminalSession?` (`:270`); `HandleSessionEndedAsync`/`HandleSessionFailedAsync` auf `TerminalSessionExitedEventArgs` (`:423,430`); `DisposeSessionResourcesAsync` (Drain → Dispose → `CompleteAsync`, `:593`); `SendCommandDelayedAsync`/`BuildCliCommand` entfernt (kein Treffer mehr); `StartCliAsync`-Klassikpfad erhalten (`:92`)
- [x] `CliProcessHandle` — `Session` (`ITerminalSession?`, `:628`); `SendCts`/`NativeProcessHandle` entfernt
- [x] `CliProcessManager` — `SubscribeRuntimeStatus`/`UnsubscribeRuntimeStatus` auf `ITerminalSession` über `GetTerminalSession` (`CliProcessManager.cs:28,198`)
- [x] `PromptZeitVersandService` (`GetTerminalSession` `:195`), `ProjektleiterAgentService` (`StartTerminalSessionAsync` `:94`, `GetTerminalSession` `:317`), `EntwicklungsprozessService` (`StartTerminalSessionAsync` `:157,239`) umgestellt
- [x] `TaskDetailViewModel` — `TerminalSessionGestartet` (`Action<ITerminalSession>`, `:665`), `GetTerminalSession` (`:678`), `AttachCliStatusSession(ITerminalSession?)` (`:1939`); `TaskDetailView.SetTerminalSession(ITerminalSession?)` (`TaskDetailView.xaml.cs:144`); `AutonomAufgabeDetailViewModel` angepasst
- [x] `AnsiSequenceParser` — persistenter `System.Text.Decoder` für chunk-übergreifendes UTF-8 (`:17,174-187`); `Reset()` inkl. Decoder-Reset (`:160`); neue CSI `d`/`e`/`` ` ``/`G`/`a`/`E`/`F`/`L`/`M`/`@`/`P`/`X`/`S`/`T`/`r`/`s`/`u` (`:217-275`); `?1049`/`?1047`/`?47`/`?1048` in `ProcessCsiQuestionCommand` (`:294-314`); ESC `7`/`8`/`c`/`(`-`)`-`*`-`#`-Zeichensatzsequenzen (`:75-95`); `\b`/`\t`/BEL-Behandlung (via `TerminalBuffer.ApplyText`, siehe Hinweise)
- [x] `TerminalBuffer` — `IsAlternateScreenActive` (`:44`), `Reset()` (`:156`), `Apply` für alle neuen Events (`:117-148`), Alt-Screen-Grid ohne Scrollback (`_mainScreenGrid`, `:408-437`), `GetSnapshot` liefert im Alt-Screen nur das Alt-Grid (`:541`), Scroll-Region (DECSTBM, `:383` + `AdvanceLine` `:295`), Save/Restore-Cursor, SU/SD, RIS; `Resize` berücksichtigt Alt-Screen (Hauptscreen-Restore nur bei passender Größe, `:423-425`)
- [x] `TerminalControl` — `Session`-DP auf `ITerminalSession` (`:43,48`); `OnSessionChanged` mit RebuildBufferFromReplay + Buffer-Übernahme + Resize + Subscribe (`:94-129`); Alt-Screen: `UpdateScrollInfo` meldet Extent = sichtbares Grid (`:451-453`), `SetVerticalOffset` klemmt auf 0 → `Line*`/`Page*`/`MouseWheel*` No-Ops (`:419-429`)
- [x] `App.xaml.cs` — `services.Configure<TerminalSessionOptions>(...SectionName)` (`:233`); beide Launcher als konkrete Singletons + `ITerminalSessionFactory` per Factory-Lambda (`:302-309`); alte Entweder-oder-Launcher-Registrierung entfernt (verbliebener `SOFTWARESCHMIEDE_TEST_DB_PATH`-Check betrifft nur `IProzessStarter`, `:288`)
- [x] `appsettings.json` — `"Terminal"`-Sektion mit `ReplayBufferByteBudget` 524288, `DefaultCols` 220, `DefaultRows` 50 (`:27-31`)
- [x] `CliSessionService` + `ICliSessionService` gelöscht (`git status`: `D`), keine Rest-Referenzen

### Tests

- [x] `TerminalReplayBufferTests` (5 Tests: Budget-Validierung, Reihenfolge, Overflow, Chunk > Budget, leerer Chunk)
- [x] `PseudoConsoleSessionTests` erweitert (`OutputChunk`-Rohbytes, EOF-ohne-Exited, `IsPseudoTerminal`, `RebuildBufferFromReplay_StelltBufferzustandWiederHer`, `Resize_IdentischeDimensionen_WirdDedupliziert`, Dispose-/ReadLoop-Verhalten)
- [x] `TerminalExecutableResolverTests` (12 Tests, alle 8 geplanten Fälle + erweiterungslose PE-/Skript-Sonderfälle)
- [x] `TerminalSessionDiagnosticsTests` (6 Tests inkl. `forcePtyUnavailable`, RequiresPty→Fehler, Health-Check-Skip/Nichtfatalität) + `TerminalSessionServiceTests` (9 Methoden: ArgumentException, NotFound/NotExecutable mit Marker, CmdShim-Normalisierung, HealthCheck-Durchreiche, `!SupportsPty`→Pipe, ForcePtyUnavailable→Pipe-Marker, RequiresPty-Fehler, Budget-Validierung)
- [x] `AnsiSequenceParserTests` erweitert (UTF-8-Chunk-Grenze, CSI L/M/@/P/X, `r`, Alt-Screen-Theory, Save/Restore-Theory, ESC `c`, SU/SD-Theory)
- [x] `TerminalBufferTests` erweitert (Alt-Screen Snapshot/Restore, IL/DL, ICH/DCH/ECH, Scroll-Region, Save/Restore, TerminalReset)
- [x] `CliKiPluginBaseTests` (`GetTerminalStartSpecAsync_MapptProcessStartInfoUndCapabilities`, `BuildTerminalStartSpec_FileNameLeer_WirftArgumentException`, RequiresPty-Capability-Test); `KiSimulatorPluginTests.GetTerminalStartSpecAsync_LiefertInteraktiveShellMitBanner`
- [x] `TerminalControlTests` — `OnSessionChanged_ReattachedSession_KeineDoppelteAusgabe` (`:199`), `ScrollInfo_AlternateScreen_KeinScrollbackKeinOffset` (`:237`), Session-DP-Typ `ITerminalSession`
- [x] `IKiPlugin`-Fakes erweitert (`UiTestKiPlugin`, `FakeKiPlugin`/`FakeFailingKiPlugin`/`FakeCancellingKiPlugin`/`FakePlainKiPlugin`, `CliEmbeddingServiceIntegrationTests.FakeKiPlugin`)
- [x] `KiPluginMockExtensions.SetupTerminalSpec` als gemeinsames Moq-Setup; `ProjektleiterAgentServiceTestDatenFactory` (`cmd.exe /c exit 0`, `:40`); `ProjektleiterAgentServiceTests_Fehlerfaelle` Wurf-Setup auf `GetTerminalStartSpecAsync` verlagert (`:207`)
- [x] `TestTerminalSessionFactory` in `Helpers` (delegiert an optionalen `IPseudoConsoleProcessLauncher`, sonst deterministische In-Memory-Session mit `new TerminalSessionOptions()`); alle `KiAusfuehrungsService`-Aufrufstellen über `TestKiAusfuehrungsServiceFactory` auf die neue Naht umgestellt
- [x] Launcher-Doubles `FixedOutput*`/`OutputByTask*`/`DelayedOutput*` auf neue `Start`-Signatur/`TerminalSessionStartResult` portiert (`KiAusfuehrungsServiceTests.cs:818-906`); `TestPseudoConsoleSessionFactory` mit optionalem `TerminalSessionOptions`-Parameter (`:22,47`)
- [x] `SimulatedPseudoConsoleProcessLauncherTests` auf neue Signatur + `Options.Create` umgestellt

### E2E-Tests

- [x] `ConPtyLifecycle_StartResizeTastatureingabeUndProzessende_E2E` auf Direct-Start umgestellt: `/k`-Banner via CliOutput, Echo-Marker-Tastaturtest, ANSI-Burst via `type`, Ctrl+V-Paste, `exit`-Prozessende (`E2E_ConPtyLifecycle.cs:38-56`)
- [x] Session-Neuanbindungs-Szenario `ConPtySessionReattach_WegUndZurueck_KeineDoppelausgabe_E2E` (InfoCliToggle-Reattach, Marker-Count==1 im CliOutput-Protokoll) — konsolidiert als Phase im Lifecycle-Test (`:49,168-188`)
- [x] `TerminalFallbackDiagnose_PipeFallbackUndRequiresPtyFehler_E2E` in `RunGeneralTests` (`MainTest.cs:34`): `Terminal.ForcePtyUnavailable` via `OpenTestDbContext`, `PtyVerfuegbar=False`-Marker, KiSimulator-Pipe-Lauf, Devin-Wechsel mit Fehlerbanner (installationsunabhängig tolerant), Key-Remove + Normalstart auf zweiter Aufgabe (`E2E_TerminalFallbackDiagnose.cs`)
- [x] `PluginAuswahlAbbrechenOkUndWechsel_E2E`: Wechselziel `Softwareschmiede.Codex`, `Softwareschmiede.Codex.ExecutablePath` = `cmd.exe` geseedet + `CommandLineParameters` gelöscht (`E2E_PluginAuswahlUndWechsel.cs:103-104`), Assertions auf "Codex CLI" (`:118,121`)
- [x] `Einstellungen_SpeichernCodexAlsStandardKiPluginUndExecutablePath_…` gehärtet: vorheriger `ki.plugin.default`-Wert gelesen und im finally-Block via Test-DB wiederhergestellt (`E2E_SettingsKiPluginPersistence.cs:24-83`)
- [x] `E2E_SessionLimitPause` auf `/k`-Shell-Semantik angepasst (Kommentar `:122`); `CliRawExport_*`, `ZeitgesteuerterPrompt_*`, `AufgabeStarten_*` etc. unverändert kompatibel in `RunConPtyTests`/`RunGeneralTests`

### Dokumentation/Verifikation

- [x] `docs/help/terminal/architektur.md` vollständig aktualisiert (Factory, Resolver inkl. `.exe`/`.cmd`/`.bat`-Regeln, Diagnostics, `Terminal:*`-Konfiguration, `Terminal.ForcePtyUnavailable`-Hook, Replay, Alt-Screen, Fehlertoleranzen)
- [x] Build/Tests laut Implementierungslauf grün (stabile Lane 1758/1759, OsInterface 49/51 mit ConPTY-Skip, IntegrationTests 78/78) — ConPTY-E2E in dieser Sandbox via `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` geskippt (bekannte Umgebungslimitation)

## Hinweise

- **Namensabweichungen (funktional gleichwertig):** `ScrollRegionSetEvent` → `ScrollRegionChangedEvent`; `AlternateScreenChangedEvent(bool Active)` → `(bool Enabled)`; `ScreenScrolledEvent(Count, Up)` → `(int DeltaRows)` mit Vorzeichen; `CursorSavedEvent`/`CursorRestoredEvent` → ein `CursorSavedEvent(bool Restored)`; `TerminalBackendEmpfehlung` heißt so statt schlicht `BackendEmpfehlung`-Enum (Feldname im Result identisch).
- **`.\b`/`\t`/BEL-Behandlung liegt im `TerminalBuffer.ApplyText`** (`TerminalBuffer.cs:256-263`), nicht im `AnsiSequenceParser` wie der Plan-Abschnitt formulierte — Steuerzeichen laufen als `TextWrittenEvent` durch und werden beim Apply interpretiert. Funktional abgedeckt und durch `TerminalBufferTests`/Parser-Tests belegt.
- **Veraltete Kommentar-Referenzen:** `AutonomAufgabeDetailViewModel.cs:129` nennt noch `KiAusfuehrungsService.StartWithPseudoConsoleAsync`; `AufgabeService.cs:931,961` referenzieren `PseudoConsoleSession` in Kommentaren. Rein kosmetisch — keine Code-Lücke.
- **`RequiresPty`-Reihenfolge** ist wie geplant vor dem E2E-Pipe-Zwang implementiert (`TerminalSessionService.cs:79` vor `SelectBackend :102`), d. h. im E2E-Modus greift der Fehler nur bei erzwungenem `PtyVerfuegbar=false`.
- **Diagnose-Marker** geht ausschließlich an die `ITerminalOutputSink` (CliOutput-Protokoll) — nicht in `TerminalReplayBuffer`/`TerminalBuffer` (`TerminalSessionService.cs:123-141`), wird im Fehlerfall vor dem Werfen geschrieben; `CliOutputProtokollWriter` existiert zu dem Zeitpunkt bereits (`KiAusfuehrungsService.cs:211-224` mit `CompleteAsync` im catch).
- **Manuelle End-to-End-Validierung** der migrierten CLIs (`DevinPlugin`, `CodexPlugin`) auf einem realen System steht aus — im Task-54-Nachweis entsprechend als ausstehend vermerkt; das ist kein Plan-Element, das im Code fehlt, sondern eine manuelle Prüfung.
- ConPTY-E2E (`RunConPtyTests`) wurde in dieser Sandbox per `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` geskippt — dokumentierte Umgebungslimitation; die neue Fallback-Diagnose-Phase liegt dagegen in `RunGeneralTests` und lief mit.
