# Test-Ergebnisse

## Ergebnis

**Status:** Keine Fehler

Testrunde 4 (Nacharbeitslauf aus `continue.md` — Behebung der 2 Code-Review-Befunde aus Runde 3:
Early-Exit-Pfad delegiert jetzt an `HandleSessionEndedAsync` mit korrektem Fehler-Mapping;
`ITerminalSession.Failure` macht ein vor der Event-Verdrahtung ausgelöstes `Failed` nachträglich
erkennbar). Alle ausgeführten Tests sind grün; die 5 neuen Regressionstest-Methoden (6 Testfälle)
laufen erfolgreich:

- `KiAusfuehrungsServiceTests.StartTerminalSessionAsync_ProzessVorVerdrahtungBeendet_MapptExitCodeAufStatus` — Theory (`exit 0` → `Gestoppt`, `exit 1` → `Fehler` inkl. `SystemMeldung`-Protokolleintrag mit Exit-Code)
- `KiAusfuehrungsServiceTests.StartTerminalSessionAsync_FailedVorVerdrahtung_WirdErkanntUndAlsFehlerGemeldet` — `Failed` vor Verdrahtung → `Fehler`, Handle entfernt, Session disposed
- `KiAusfuehrungsServiceTests.StartTerminalSessionAsync_SessionFailed_MeldetFehlerStattGestoppt` — `Failed` nach Verdrahtung (verzögerter Lesefehler) → `Gestartet`, dann `Fehler`
- `PseudoConsoleSessionTests.ReadLoopAsync_Lesefehler_SetztFailureZustand` — `Failure` bleibt ohne `Failed`-Subscriber sichtbar
- `PseudoConsoleSessionTests.WriteInputAsync_Schreibfehler_SetztFailureUndFeiertFailed` — `Failure` trägt dasselbe Args-Objekt wie das `Failed`-Event

Build vorab: `dotnet build Softwareschmiede.slnx` — **0 Fehler, 0 Warnungen**.

Ausgeführte Lanes (jeweils mit `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`, Sandbox-Regel):

- `dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category!=OsInterface"` — **1782 Tests: 1781 bestanden, 0 fehlgeschlagen, 1 übersprungen**
- `dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category=OsInterface"` — **55 Tests: 53 bestanden, 0 fehlgeschlagen, 2 übersprungen** (`RunConPtyTests`, `InitialisierungsskriptAusfuehrung` — ConPTY-Sandbox-Skip)
- `dotnet test src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj --filter "Category!=OsInterface"` — **69 Tests, alle bestanden**
- `dotnet test src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj --filter "Category=OsInterface"` — **9 Tests, alle bestanden**

> **Sandbox-Einschränkung (kein Testfehler):** Die ConPTY-abhängigen E2E-Szenarien in `RunConPtyTests` (u. a. `ConPtyLifecycle_*`, `PluginAuswahlAbbrechenOkUndWechsel_E2E`) sind in dieser Sandbox bestätigt nicht ausführbar und wurden ehrlich als „Übersprungen" gemeldet — **nicht** als bestanden. Sie bleiben für eine interaktive Session/Visual-Studio-Ausführung offen. Das Feature-E2E-Szenario `TerminalFallbackDiagnose_PipeFallbackUndRequiresPtyFehler_E2E` benötigt kein echtes ConPTY (simuliertes Pipe-Backend) und lief in Runde 3 **vollständig und erfolgreich** in dieser Sandbox.

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
| Fallback-Diagnose sichtbar: (a) `Terminal.ForcePtyUnavailable` → Pipe + `[Terminal-Diagnose]`/`PtyVerfuegbar=False` + „eingeschränkter Modus"-Statuszeile; (b) Devin `RequiresPty` → Fehlerbanner, kein `CliStoppen`; (c) Schlüssel löschen → Normalstart | `TerminalFallbackDiagnose_PipeFallbackUndRequiresPtyFehler_E2E` (Phase in `RunGeneralTests`, `MainTest.cs:34`) | **Bestanden** (Runde 3) — alle drei Teilabschnitte (a), (b), (c) liefen durch |
| Plugin-Wechsel bei laufender CLI (Wechselziel `Softwareschmiede.Codex`, `ExecutablePath`-Seeding) | `PluginAuswahlAbbrechenOkUndWechsel_E2E` (`RunConPtyTests`) | Nicht ausgeführt (Sandbox) |
| Prompt-Versand / Rate-Limit-Marker mit `/k`-Shell | `SessionLimit_MarkerPauseProtokollUndUpdateSicherheit_E2E` (Phase in `RunGeneralTests`, `MainTest.cs:33`) | Bestanden |
| Fehler-Mapping bei Early-Exit (Prozess vor Verdrahtung beendet): ExitCode != 0 → `Fehler` + Protokolleintrag, ExitCode == 0 → `Gestoppt` | `StartTerminalSessionAsync_ProzessVorVerdrahtungBeendet_MapptExitCodeAufStatus` (Regressionstest, neu in Runde 4) | **Bestanden** |
| `Failed` vor der Event-Verdrahtung (tote Leseschleife bei laufendem Prozess) → `Fehler` statt hängendem „Gestartet" | `StartTerminalSessionAsync_FailedVorVerdrahtung_WirdErkanntUndAlsFehlerGemeldet` + `PseudoConsoleSessionTests.*Failure*` (Regressionstests, neu in Runde 4) | **Bestanden** |
| `Failed` nach der Event-Verdrahtung → `Fehler` statt `Gestoppt` | `StartTerminalSessionAsync_SessionFailed_MeldetFehlerStattGestoppt` (Regressionstest, neu in Runde 4) | **Bestanden** |
| Validierung mit Devin + Codex als erste migrierte CLIs | manueller Schritt 9 — kein automatisierter Test | Nicht ausgeführt |

## Zusammenfassung

- Gesamt: 1915
- Bestanden: 1912
- Fehlgeschlagen: 0
- Übersprungen: 3 (`RunConPtyTests`, `InitialisierungsskriptAusfuehrung` — ConPTY-Sandbox-Skip; `ArbeitsverzeichnisOeffnenServiceTests.Oeffne_AufNichtWindows_WirftPlatformNotSupportedException` — plattformbedingt auf Windows übersprungen)

## Testabdeckung

Keine neue Coverage-Messung in Runde 4 (die reguläre Lane lief ohne `--collect:"XPlat Code Coverage"`). Die Zahlen aus Runde 3 bleiben referenzierbar: `KiAusfuehrungsService.cs` 42,2 % Zeilen (instrumentierte Lane), `PseudoConsoleSession.cs` 76,6 %; die neuen Runde-4-Pfade (Early-Exit-Delegation, `Failure`-Recheck, `istFehlerhaftesEnde`-Mapping) sind durch die oben genannten OsInterface-Regressionstests abgedeckt.

## Fehlende Tests

Unverändert gegenüber Runde 3:

- `src/Softwareschmiede/Infrastructure/Terminal/Win32PseudoConsoleProcessLauncher.cs` — nur in OsInterface/E2E erreichbar; `RunConPtyTests` in dieser Sandbox nicht ausführbar
- `src/Softwareschmiede/Infrastructure/Terminal/PseudoConsoleProcessStarter.cs` — Win32-/ConPTY-Pfad, nur in geskippten ConPTY-Tests erreichbar
- `src/Softwareschmiede/Infrastructure/Terminal/PseudoConsole.cs` — P/Invoke-ConPTY-Wrapper, nur in geskippten ConPTY-Tests erreichbar
- Manuelle Validierung der migrierten CLIs `DevinPlugin` + `CodexPlugin` (Plan Schritt 9) — nicht automatisierbar
