# Test-Ergebnisse

Testrunde 3 (nach Iteration 3: ANSI-Sequenzen im E2E, finally-Cleanup, Abbruch-Subphase im ConPTY-Export, „Neu starten"-Button, Overflow-Check, Lock-Splitting-Fix).

Ausgeführte Befehle (jeweils synchron, mit `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`):

1. `dotnet build Softwareschmiede.slnx` — erfolgreich, 0 Warnungen, 0 Fehler
2. `dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category!=OsInterface"` — 1857 Tests, 1856 bestanden, 1 übersprungen, 0 fehlgeschlagen (~1,3 min)
3. `dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category=OsInterface"` — 55 Tests, 53 bestanden, 2 übersprungen, 0 fehlgeschlagen (~9,6 min)
4. `dotnet test src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj --filter "Category!=OsInterface"` — 69 Tests, 69 bestanden (~9 s)
5. `dotnet test src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj --filter "Category=OsInterface"` — 9 Tests, 9 bestanden (~4 s)

## Ergebnis

**Status:** Keine Fehler

`RunGeneralTests` ist vollständig durchgelaufen (8 m 5 s) — einschließlich des erweiterten `KonsolenTestfenster_OeffnetLaedtAufzeichnungUndSpieltAb_E2E` mit den neuen Phasen (ANSI-Steuersequenzen in der Quell-Ansicht, echtes erneutes Abspielen nach Ende, Neustart aus dem Pausiert-Zustand, Formatfehler-Banner, OpenFileDialog-Abbruch) und dem `finally`-Cleanup (`TryCloseKonsolenTestfenster`).

## E2E-Abdeckung

| Szenario | Test / Testklasse | Ergebnis |
|----------|-------------------|----------|
| Einstellungen → „Diagnose" → Konsolentestfenster öffnen → `.clireplay` über nativen OpenFileDialog laden → Quell-Liste mit sichtbaren ANSI-Steuersequenzen (`ESC` → `␛`) → Abspielen → Position/Status → Pausieren (keine neuen Chunks) → Fortsetzen → Zeitraffer-Schwelle → „Wiedergabe beendet."; im selben Szenario: korrupte Datei → `FehlerMeldung`-Banner, OpenFileDialog-Abbruch (ESC) → kein Zustandswechsel; zusätzlich Iteration 3: erneutes Abspielen nach Ende + Neustart aus dem Pausiert-Zustand; Dialog-Schließen im `finally` | `KonsolenTestfenster_OeffnetLaedtAufzeichnungUndSpieltAb_E2E` (`src/Softwareschmiede.Tests/E2E/E2E_KonsolenTestfenster.cs`, aufgerufen in `End2EndTest.RunGeneralTests`, `MainTest.cs` Z. 36) | Bestanden |
| Aufgabe mit laufender CLI → „Aufzeichnung exportieren" → Save-Dialog-Abbruch + bestätigter Export → `.clireplay` lesbar; anschließend Konsolentestfenster öffnen und exportierte Datei abspielen (Abbruch-Subphase aus Iteration 3) | `ConPtyCliReplayExport_ExportOeffnetKonsolenTestUndSpieltAb_E2E` (`src/Softwareschmiede.Tests/E2E/E2E_ConPtyLifecycle.cs` Z. 70, konsolidiert in `ConPtyLifecycle_StartResizeTastatureingabeUndProzessende_E2E`, aufgerufen in `End2EndTest.RunConPtyTests`, `MainTest.cs` Z. 92) | Nicht ausgeführt — ConPTY-Tests sind in dieser Sandbox nicht ausführbar (`SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`, dokumentierte Sandbox-Limitation, kein Code-Defekt); in einer interaktiven Session/VS auszuführen |

## Zusammenfassung

- Gesamt: 1990
- Bestanden: 1987
- Fehlgeschlagen: 0
- Übersprungen: 3
  - `End2EndTest.RunConPtyTests` — ConPTY-Sandbox-Limitation (`SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`)
  - `E2E_RepositoryInitialisierungAusfuehrungTests.InitialisierungsskriptAusfuehrung` — ConPTY-Sandbox-Limitation (gleiche Variable)
  - `ArbeitsverzeichnisOeffnenServiceTests.Oeffne_AufNichtWindows_WirftPlatformNotSupportedException` — plattformbedingter Skip (nicht feature-bezogen)

## Testabdeckung

**Abdeckung:** Nicht messbar (kein Coverage-Collector im Testlauf verwendet; Fallback: Dateinamen-Konvention)

## Fehlende Tests

Quelle: `Dateinamen-Konvention`

Keine — alle neuen Quelldateien des Features haben korrespondierende Testabdeckung:

- `CliOutputRecorder.cs` → `CliOutputRecorderTests.cs`
- `CompositeTerminalOutputSink.cs` → `CompositeTerminalOutputSinkTests.cs`
- `CliReplayAufzeichnungStore.cs` → `CliReplayAufzeichnungStoreTests.cs`
- `TerminalReplaySession.cs` → `TerminalReplaySessionTests.cs`
- `CliChunkQuelltextFormatter.cs` → `CliChunkQuelltextFormatterTests.cs`
- `CliReplayExportService.cs` → `CliReplayExportServiceTests.cs`
- `KonsolenTestViewModel.cs` → `KonsolenTestViewModelTests.cs`
- Export-ViewModel-Pfad → `TaskDetailViewModelTests_CliReplayExport.cs`
- `KonsolenTestDialog.xaml(.cs)` / UI-Fluss → `E2E_KonsolenTestfenster.cs` (+ `KonsolenTestDialogView.cs`-Wrapper)
- Datenmodelle ohne eigenen Code (`CliOutputChunkRecord`, `CliOutputAufzeichnung`, `CliChunkAnzeigeEintrag`, `ITerminalDiagnoseSink`) — indirekt über die obigen Tests abgedeckt
