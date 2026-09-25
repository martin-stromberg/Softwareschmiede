# Test-Ergebnisse

## Ergebnis

**Status:** Keine Fehler

Testrunde 3 (nach Behebung der 8 Code-Befunde und 7 Testfehler aus Runde 2). Alle ausgeführten Tests sind grün; **alle 7 Fehlschläge aus Runde 2 sind behoben**:

1. **`End2EndTest.RunGeneralTests` läuft jetzt vollständig durch** (7 min 4 s) — einschließlich der in Runde 2 abgebrochenen Phase `TerminalFallbackDiagnose_PipeFallbackUndRequiresPtyFehler_E2E` (Pipe-Fallback mit `PtyVerfuegbar=false`, Devin-`RequiresPty`-Fehlerbanner, Normalstart nach Schlüssel-Löschung) und aller nachfolgenden Phasen (`CommandLineParameters_*`, `ViewPatternHappyPath_*`, `AnsichtenErkennung_*`, `MenueNavigation_*`, `ForceShow_*`, `ForceClose_*` ×2, `DialogErkennung_*`, `UnbekannteAnsicht_*`, `FehlerAnsichtErkennung_*`, beide `Fixture_*`-Szenarien, `Settings_AllModesAndPrereleasesPersist` und die Update-Pipeline-Szenarien E-01–E-07). Der `GetCliStatusText`-Defekt ist behoben.
2. **Alle 6 `TerminalControlTests.ReadClipboardAndInsertAsync_*` sind jetzt grün** — der Reflection-Helper wurde auf die Signatur `ReadClipboardAndInsertAsync(ITerminalSession)` angepasst (`NullReferenceException` behoben); auch die transienten `CLIPBRD_E_CANT_OPEN`-Fehler traten nicht erneut auf.

Build vorab: `dotnet build Softwareschmiede.slnx` — **0 Fehler, 0 Warnungen**.

Ausgeführte Lanes (jeweils mit `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`, Sandbox-Regel):

- `dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category!=OsInterface" --collect:"XPlat Code Coverage"` — **1780 Tests: 1779 bestanden, 0 fehlgeschlagen, 1 übersprungen**
- `dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category=OsInterface"` — **51 Tests: 49 bestanden, 0 fehlgeschlagen, 2 übersprungen** (`RunConPtyTests`, `InitialisierungsskriptAusfuehrung` — ConPTY-Sandbox-Skip)
- `dotnet test src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj --filter "Category!=OsInterface"` — **69 Tests, alle bestanden**
- `dotnet test src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj --filter "Category=OsInterface"` — **9 Tests, alle bestanden**

> **Sandbox-Einschränkung (kein Testfehler):** Die ConPTY-abhängigen E2E-Szenarien in `RunConPtyTests` (u. a. `ConPtyLifecycle_*`, `PluginAuswahlAbbrechenOkUndWechsel_E2E`) sind in dieser Sandbox bestätigt nicht ausführbar und wurden ehrlich als „Übersprungen" gemeldet — **nicht** als bestanden. Sie bleiben für eine interaktive Session/Visual-Studio-Ausführung offen. Das neue Feature-E2E-Szenario `TerminalFallbackDiagnose_PipeFallbackUndRequiresPtyFehler_E2E` benötigt kein echtes ConPTY (simuliertes Pipe-Backend) und lief **vollständig und erfolgreich** in dieser Sandbox.

## Fehlgeschlagene Tests

Keine — 0 Fehlschläge in allen vier Lanes.

### Nicht ausgeführt (Sandbox: ConPTY nicht verfügbar, per `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` übersprungen)

- **End2EndTest.RunConPtyTests** — übersprungen; enthält u. a. `ConPtyLifecycle_StartResizeTastatureingabeUndProzessende_E2E`, `PluginAuswahlAbbrechenOkUndWechsel_E2E`, `AufgabeStarten_MitKonfiguriertemArbeitsverzeichnis_*`, `AufgabeWechselUeberSeitenleiste_*`, `CliRawExport_*` (`MainTest.cs:72–102`)
- **E2E_RepositoryInitialisierungAusfuehrungTests.InitialisierungsskriptAusfuehrung** — übersprungen (ConPTY-Abhängigkeit)

### Manueller Validierungsschritt (Plan Schritt 9)

- **Reale Validierung DevinPlugin + CodexPlugin (Start, Eingabe, Escape-Sequenzen, Resize, Resolver-Nachweis)** — nicht automatisiert; in dieser Sandbox nicht möglich.

## E2E-Abdeckung

Geplante Szenarien aus `plan.md` („E2E-Tests (primärer Funktionsnachweis)") und `plan-check.md` („E2E-Abdeckung"):

| Szenario | Test / Testklasse | Ergebnis |
|----------|-------------------|----------|
| CLI-Start mit Arbeitsverzeichnis/Umgebung via Direct-Start (`/k`-Banner, `echo`-Marker → `CliOutput`-DB, `exit /b` Exit-Code) | `ConPtyLifecycle_StartResizeTastatureingabeUndProzessende_E2E` + `AufgabeStarten_MitKonfiguriertemArbeitsverzeichnis_*` (`RunConPtyTests`) | Nicht ausgeführt (Sandbox: ConPTY nicht verfügbar) |
| `\r`-Fortschritt, ANSI-Farben, mehrzeilige Ausgabe, Burst ohne UI-Blockade (`type <datei>`) | `ConPtyLifecycle` (`RunConPtyTests`) | Nicht ausgeführt (Sandbox) |
| Resize vom Prozess beobachtbar | `ConPtyLifecycle` (`RunConPtyTests`) | Nicht ausgeführt (Sandbox) |
| Session-Neuanbindung ohne doppelte Ausgaben | `AufgabeWechselUeberSeitenleiste_*` / Lifecycle-Schritt (`RunConPtyTests`); Unit-Abdeckung: `TerminalControlTests.OnSessionChanged_ReattachedSession_KeineDoppelteAusgabe`, `PseudoConsoleSessionTests.RebuildBufferFromReplay_*` | Bestanden (Unit-Ebene) / E2E nicht ausgeführt (Sandbox) |
| Paste (Ctrl+V) im neuen Pfad | `ConPtyLifecycle` (`RunConPtyTests`); Unit: `TerminalControlTests.ReadClipboardAndInsertAsync_*` + `OnPreviewKeyDown_CtrlV_CallsReadClipboardAndInsertAsync` | Bestanden (Unit-Ebene, 7 Tests) / E2E nicht ausgeführt (Sandbox) |
| Ctrl-C ohne Fehler + `exit /b`-Exit-Code-Anzeige (optional) | `ConPtyLifecycle` (`RunConPtyTests`) | Nicht ausgeführt (Sandbox) |
| Fallback-Diagnose sichtbar: (a) `Terminal.ForcePtyUnavailable` → Pipe + `[Terminal-Diagnose]`/`PtyVerfuegbar=False` + „eingeschränkter Modus"-Statuszeile; (b) Devin `RequiresPty` → Fehlerbanner, kein `CliStoppen`; (c) Schlüssel löschen → Normalstart | `TerminalFallbackDiagnose_PipeFallbackUndRequiresPtyFehler_E2E` (Phase in `RunGeneralTests`, `MainTest.cs:34`) | **Bestanden** — alle drei Teilabschnitte (a), (b), (c) liefen durch |
| Plugin-Wechsel bei laufender CLI (Wechselziel `Softwareschmiede.Codex`, `ExecutablePath`-Seeding) | `PluginAuswahlAbbrechenOkUndWechsel_E2E` (`RunConPtyTests`) | Nicht ausgeführt (Sandbox) |
| Prompt-Versand / Rate-Limit-Marker mit `/k`-Shell | `SessionLimit_MarkerPauseProtokollUndUpdateSicherheit_E2E` (Phase in `RunGeneralTests`, `MainTest.cs:33`) | Bestanden |
| Validierung mit Devin + Codex als erste migrierte CLIs | manueller Schritt 9 — kein automatisierter Test | Nicht ausgeführt |

## Zusammenfassung

- Gesamt: 1909
- Bestanden: 1906
- Fehlgeschlagen: 0
- Übersprungen: 3 (`RunConPtyTests`, `InitialisierungsskriptAusfuehrung` — ConPTY-Sandbox-Skip; `ArbeitsverzeichnisOeffnenServiceTests.Oeffne_AufNichtWindows_WirftPlatformNotSupportedException` — plattformbedingt auf Windows übersprungen)

## Testabdeckung

**Abdeckung:** 28,4 % Zeilen gesamt (Cobertura, nur Lane `Category!=OsInterface` instrumentiert — OsInterface-/E2E-ausgeübter Code ist dadurch systematisch unterdeckt, nicht zwingend ungetestet)

| Datei | Abdeckung |
|-------|-----------|
| `src/Softwareschmiede/Infrastructure/Terminal/Win32PseudoConsoleProcessLauncher.cs` | 0 %* |
| `src/Softwareschmiede/Infrastructure/Terminal/PseudoConsoleProcessStarter.cs` | 0 %* |
| `src/Softwareschmiede/Infrastructure/Terminal/PseudoConsole.cs` | 0 %* |
| `src/Softwareschmiede/Infrastructure/Terminal/TerminalSessionExitedEventArgs.cs` | 0 % |
| `src/Softwareschmiede/Infrastructure/Terminal/TerminalSessionFailedEventArgs.cs` | 0 % |
| `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs` | 42,2 % |
| `src/Softwareschmiede.App/Controls/KeyToVt100Encoder.cs` | 48,9 % |
| `src/Softwareschmiede/Infrastructure/Terminal/SimulatedPseudoConsoleProcessLauncher.cs` | 55,2 % |
| `src/Softwareschmiede.Plugin.Contracts/Domain/Abstractions/CliKiPluginBase.cs` | 59,8 % |
| `src/Softwareschmiede/Infrastructure/Terminal/AnsiSequenceParser.cs` | 67,0 % |
| `src/Softwareschmiede/Infrastructure/Terminal/PseudoConsoleSession.cs` | 76,6 % |
| `src/Softwareschmiede.App/Controls/TerminalControl.cs` | 78,5 % |
| `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs` | 79,4 % |

\* Markierte Dateien werden überwiegend durch OsInterface-Tests/E2E ausgeübt (realer Prozessstart, ConPTY) — in der instrumentierten Lane nicht erfasst.

Verbesserung gegenüber Runde 2: `SimulatedPseudoConsoleProcessLauncher.cs` von 0 % auf 55,2 % (Tests laufen jetzt teilweise in der instrumentierten Lane), `PseudoConsoleSession.cs` 76,6 %, `TerminalEvents.cs` neu mit 95,7 %.

Gut abgedeckt (≥80 %): `TerminalExecutableResolver` 97,3 %, `TerminalSessionService` 86,5 %, `TerminalSessionDiagnostics` 100 %, `TerminalReplayBuffer` 100 %, `TerminalBuffer` 84,2 %, `TerminalEvents` 95,7 %, `CliProcessManager` 82,7 %, `PromptZeitVersandService` 82,2 %, `TerminalSessionOptions`/`TerminalSessionStartSpec`/`TerminalSessionStartResult`/`PseudoConsoleSessionContext`/`TerminalOutputChunkEventArgs`/`NullPseudoConsoleHandle`/`TerminalCell` 100 %, Plugins `KiSimulator` 96,2 %, `Devin` 88,1 %, `Codex` 86,3 %, `GitHubCopilot` 90,3 %, `ClaudeCli` 82,0 %.

## Fehlende Tests

Quelle: `Coverage-Daten`

- `src/Softwareschmiede/Infrastructure/Terminal/Win32PseudoConsoleProcessLauncher.cs` — 0 % Abdeckung (nur in OsInterface/E2E erreichbar; `RunConPtyTests` in dieser Sandbox nicht ausführbar)
- `src/Softwareschmiede/Infrastructure/Terminal/PseudoConsoleProcessStarter.cs` — 0 % Abdeckung (Win32-/ConPTY-Pfad, nur in geskippten ConPTY-Tests erreichbar)
- `src/Softwareschmiede/Infrastructure/Terminal/PseudoConsole.cs` — 0 % Abdeckung (P/Invoke-ConPTY-Wrapper, nur in geskippten ConPTY-Tests erreichbar)
- `src/Softwareschmiede/Infrastructure/Terminal/TerminalSessionExitedEventArgs.cs` / `TerminalSessionFailedEventArgs.cs` — 0 % Abdeckung (reine EventArgs-Datenklassen)
