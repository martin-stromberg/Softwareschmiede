# Test-Ergebnisse

Nacharbeit-Lauf (`continue.md`): Fixes für 3 Code-Befunde (Replay-Loop ct-Check, Zeitraffer-Schwellen-Fallback, E2E-finally-Cleanup) und 2 Usability-Befunde (Pfad-Überlauf, „keine Aufzeichnung"-Meldung) + 2 neue Regressionstests.

Ausgeführte Befehle (jeweils synchron, mit `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`):

1. `dotnet build Softwareschmiede.slnx` — erfolgreich, 1 pre-existing Warnung (CS8602, `CliOutputProtokollWriterTests.cs:151`), 0 Fehler
2. `dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "FullyQualifiedName~TerminalReplaySessionTests|FullyQualifiedName~KonsolenTestViewModelTests|FullyQualifiedName~TaskDetailViewModelTests_CliReplayExport"` — 30 Tests, 30 bestanden (inkl. beider neuer Regressionstests)
3. `dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category!=OsInterface"` — 1859 Tests, 1858 bestanden, 1 übersprungen, 0 fehlgeschlagen (~1,3 min)
4. `dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category=OsInterface"` — 55 Tests, 51 bestanden, 2 übersprungen, 2 fehlgeschlagen (~10,3 min); **Retry der 2 Fehlschläge einzeln: beide bestanden (49 s)** — siehe unten
5. `dotnet test src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj --filter "Category!=OsInterface"` — 69 Tests, 69 bestanden (~14 s)
6. `dotnet test src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj --filter "Category=OsInterface"` — 9 Tests, 9 bestanden (~3 s)
7. `dotnet format Softwareschmiede.slnx --verify-no-changes` — sauber (Exit 0)

## Ergebnis

**Status:** Keine Fehler

`RunGeneralTests` ist vollständig durchgelaufen — inklusive `KonsolenTestfenster_OeffnetLaedtAufzeichnungUndSpieltAb_E2E` mit dem Pfad-/Zeitraffer-/Neustart-Ablauf des Konsolentestfensters.

## Zu den 2 E2E-Fehlschlägen im OsInterface-Lauf

- `ProjectDetailE2ETests.ProjektDetailSzenarien` — `TimeoutException` (Element nicht in 15 s gefunden) in `ProjectListView.OpenProject`
- `E2E_RepositoryManagementTests.BasisBranchVerwaltung` — `TimeoutException` (Element nicht in 20 s gefunden) in `ProjectDetailView.SetBaseBranch`

Bewertung als **Umgebungs-Flake, keine Feature-Regression**: (a) das App-Log der Test-Instanz
(`src/Softwareschmiede.App/bin/Debug/net10.0-windows10.0.17763.0/logs/softwareschmiede-20260926.log`)
enthält **keine** Start-Ausnahme (`MainWindow konnte nicht angezeigt werden`, `XamlParseException`) —
Fenster und Prozess starteten sauber; (b) beide Szenarien betreffen Projektliste/-detail und
Basis-Branch-Verwaltung — fachlich und code-seitig unabhängig vom Diff dieses Laufs
(Replay-Session-Loop, Konsolentest-ViewModel/-Dialog, eine Meldungstext-Zeile, ein E2E-`finally`);
(c) der isolierte Re-Run beider Tests war ohne Änderung sofort grün. Die Fehlschläge traten in den
ersten ~80 s des Laufs auf (Cold-Start der ersten App-Instanzen in der Sandbox).

## E2E-Abdeckung

Geplante Benutzerfluss-E2E-Szenarien und ihr Stand in diesem Lauf:

- `End2EndTest.RunGeneralTests` → Phase `KonsolenTestfenster_OeffnetLaedtAufzeichnungUndSpieltAb_E2E` (Dialog öffnen, defekte Datei/Fehlerbanner, Öffnen-Dialog-Abbruch, Pause/Fortsetzen, erneutes Abspielen, Neustart aus Pausiert, Zeitraffer 0, Quell-Liste mit ␛-Sequenzen) — **bestanden**
- `End2EndTest.RunConPtyTests` inkl. Phase `ConPtyCliReplayExport_ExportOeffnetKonsolenTestUndSpieltAb_E2E` (Echt-Session-Export + Öffnen + Abspielen) — **übersprungen** (`SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`, bestätigte Sandbox-Limitation — kein Code-Defekt; in interaktiver Session/Visual Studio nachzuholen)
- `E2E_RepositoryInitialisierungAusfuehrungTests.InitialisierungsskriptAusfuehrung` — **übersprungen** (gleiche ConPTY-Limitation)

## Fehlgeschlagene Tests

Keine — nach dem erfolgreichen Re-Run der beiden E2E-Flakes stehen alle ausführbaren Tests auf grün.
