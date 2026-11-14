# Tests

## Test-Ausgangszustand vor der Umsetzung

- Zeitpunkt (mit Zeitzone): 2026-11-13, Testläufe ca. 20:20–20:40 (W. Europe Standard Time, UTC+01:00)
- Branch und Commit-ID: `task/issue-381-e4d9707347484d8d9f930cd35776e769-powershell-aufloesung-im-updat` @ `ca70cbd0b58b540d3050bef8e5adbf292d8245c5`
- Uncommittete Änderungen im getesteten Stand: nur die neuen Feature-Dokumente unter `docs/features/task/issue-381-…-powershell-aufloesung-im-updat/` (`requirement.md`, `todo.md`, `inventory/`); keinerlei Änderungen an Produktiv- oder Testcode.
- Testumgebung und Runtime-/SDK-Versionen: .NET SDK 10.0.403, Ziel-Frameworks `net10.0` / `net10.0-windows10.0.17763.0`, Windows, Debug-Konfiguration. `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` war bei allen Läufen gesetzt (Sandbox-Auflagen, wirkt nur auf ConPTY-E2E-Tests der OsInterface-Lane).
- Build vor den Tests: `dotnet build Softwareschmiede.slnx` — erfolgreich, 0 Warnungen, 0 Fehler.
- Ermittelte Testsuiten und Quellen der Testbefehle: CI-Workflow `.github/workflows/pr-staging-ci.yml` (Zeilen 128, 132, 222, 226) definiert vier Lanes — je `Category!=OsInterface` und `Category=OsInterface` für `Softwareschmiede.Tests` und `Softwareschmiede.IntegrationTests`. Projektdokumentation (CLAUDE.md) nennt die `Category!=OsInterface`-Lane als stabile Regulär-Lane; OsInterface enthält die umgebungssensitiven FlaUI-/ConPTY-/OS-E2E-Tests.

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| 1 — Regulär-Lane Unit/Integration | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category!=OsInterface"` | Repo-Root | 0 | 2352 | 0 | 3 | [TRX](test-results/baseline-regular-lane.trx), [Konsole](test-results/baseline-regular-lane.console.log) |
| 2 — Regulär-Lane IntegrationTests | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj --filter "Category!=OsInterface"` | Repo-Root | 0 | 280 | 0 | 0 | [TRX](test-results/baseline-integration-regular-lane.trx), [Konsole](test-results/baseline-integration-regular-lane.console.log) |
| 3 — OsInterface-Subset (Update-E2E) | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "FullyQualifiedName~End2EndTest.General_Update"` | Repo-Root | 0 | 2 | 0 | 0 | [TRX](test-results/baseline-update-e2e.trx), [Konsole](test-results/baseline-update-e2e.console.log) |

### Nachgewiesene bestehende Testfehler

Keine. Alle drei ausgeführten Läufe endeten mit 0 Fehlgeschlagenen.

### Testlücken und Ausführungsprobleme

- **3 übersprungene Tests in Lauf 1** — alle plattformbedingt via `SkippableFact`/`Skip.If`, keine Fehler:
  - `Softwareschmiede.Tests.Application.Services.ArbeitsverzeichnisOeffnenServiceTests.Oeffne_AufNichtWindows_WirftPlatformNotSupportedException` — gilt nur für Nicht-Windows.
  - `Softwareschmiede.Tests.Infrastructure.Services.SystemShutdownServiceTests.ResolveShutdownCommand_Linux_BleibtUnveraendert` — Linux-spezifisch.
  - `Softwareschmiede.Tests.Infrastructure.Services.SystemShutdownServiceTests.ResolveShutdownCommand_MacOS_BleibtUnveraendert` — macOS-spezifisch.
- **OsInterface-Lanes nur teilweise ausgeführt:** Von der `Category=OsInterface`-Lane des `Softwareschmiede.Tests`-Projekts wurde nur das feature-relevante Update-Subset (Lauf 3, die zwei konsolidierten Fakten `General_UpdateFixtureSmoke_E2E` und `General_UpdateStartvarianten_E2E`) ausgeführt — beide bestanden. Die übrige FlaUI-E2E-Suite (~35 E2E-Dateien mit App-Fenster-Starts, ConPTY-, Clipboard- und Credential-Store-Tests) sowie die komplette OsInterface-Lane des `Softwareschmiede.IntegrationTests`-Projekts (u. a. `StartPs1IntegrationTests` für das Repo-Startskript `start.ps1`) wurden nicht ausgeführt: umgebungssensitive, langlaufende Lane; für den nicht ausgeführten Umfang ist der Ausgangszustand unbekannt und es wird keine Aussage getroffen.
- **ConPTY-Tests:** `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` war gesetzt; die betroffenen ConPTY-E2E-Tests liegen in der OsInterface-Lane und waren ohnehin nicht Teil der ausgeführten Subsets. In Lauf 1 zeigte die Variable keinen sichtbaren Effekt (keine ConPTY-Tests in der Regulär-Lane).

## Testklassen

### `UpdateScriptServiceTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/Updates/UpdateScriptServiceTests.cs` — direkte Unit-Tests der geänderten Klasse (Regulär-Lane).

- `CreateScriptAsync_ShouldWritePowerShellUpdaterScript` — `update.ps1` enthält `param(`-Block mit allen Parametern, `Write-UpdateLog`, Wait/Stop-/Copy-/Start-Logik und Erfolgs-/Fehler-Logzeilen.
- `StartScriptAsync_ShouldPassArgumentsToLauncher` — Argumentliste wird unverkettet an `IUpdateProcessLauncher` übergeben (`-File`, `-AppPid`, `-TargetDirectory`, `-ExtractedDirectory`, `-ExecutablePath`, `-LogPath`); `RunElevated` wird aus `UpdatePreparationResult.RequiresElevation` durchgereicht; `FileName` ist nicht leer (prüft aktuell **nicht**, ob `FileName` vollqualifiziert ist).
- `StartScriptAsync_ShouldFallbackToPwshCandidate_WhenPowerShellCandidateIsMissing` — injizierte Kandidatenliste `[<fehlender Pfad>, <existierende pwsh.exe-Datei im Temp-Verzeichnis>]` → `launcher.FileName == pwshPath`. Beweist die Kandidaten-Fallback-Reihenfolge; fachlich nach der Umstellung weiter gültig, solange die Injektion fester Pfade möglich bleibt.
- `StartScriptAsync_ShouldThrow_WhenLauncherReportsNoProcess` — Launcher-Rückgabe `false` → `InvalidOperationException` mit `*nicht gestartet*`.

Kein bestehender Test prüft den Inhalt von `GetDefaultPowerShellCandidates`, das PATH-/PATHEXT-Verhalten von `IsExecutableAvailable` oder den `"powershell.exe"`-Letztfallback von `FindPowerShellExecutable`.

### `UpdateServiceTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/Updates/UpdateServiceTests.cs`

- `StartPreparedUpdateAsync_ShouldNotShutdown_WhenScriptStartFails` (Zeilen 117–135) — `IUpdateScriptService.StartScriptAsync` wirft `InvalidOperationException` → `StartPreparedUpdateAsync` propagiert sie und `IApplicationShutdownService.Shutdown()` wird nie aufgerufen. Deckt die Fehlschlag-Semantik „kein Shutdown ohne erfolgreichen Start" bereits ab.
- Übrige Tests betreffen `CheckForUpdateAsync` (Release-Lookup, Kanalkonflikt-Warnung) — für diese Anforderung nicht relevant.

### `UpdatePackageServiceTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/Updates/UpdatePackageServiceTests.cs`

- Enthält das Test-Double `FakeScriptService : IUpdateScriptService` (ab Zeile 255) — für diese Anforderung nicht änderungsbedürftig.

### `End2EndTest` (Update-Szenarien)
Dateien: `src/Softwareschmiede.Tests/E2E/E2E_UpdateFixture.cs`, `src/Softwareschmiede.Tests/E2E/E2E_UpdateSettings.cs`, Bündelung in `src/Softwareschmiede.Tests/E2E/MainTest.cs` (OsInterface-Lane, `[OsInterface]` + `[Trait("Category","E2E")]`).

- `General_UpdateFixtureSmoke_E2E` (MainTest.cs:69–73) — führt `Fixture_UsesIsolatedRealUpdatePipeline` und `Fixture_SettingsReadFailureTargetsPhaseAfterDatabaseInitialization` aus. Prüft u. a. genau ein `UpdateProcessStartRecorded`-Protokollereignis (aufgezeichneter `fileName` des aufgelösten Interpreters, `runElevated == false`) und `ShutdownRequested` nach erfolgreichem Start. Die echte `UpdateScriptService`-Instanz läuft mit `testwurzel`-Basis und **echten** `GetDefaultPowerShellCandidates()` (keine Kandidateninjektion im E2E-Modus).
- `General_UpdateStartvarianten_E2E` (MainTest.cs:77–86) — sieben Update-Szenarien (E-01 bis E-07): Settings-Persistenz, Startautomatik, Kanal-/Asset-Auswahl, Fehler-/Abbruch-Robustheit; mehrere Pfade bis zur `UpdateProcessStartRecorded`-/`ShutdownRequested`-Grenze.

## Hilfsmethoden

### `UpdateScriptServiceTests` (private Doubles, UpdateScriptServiceTests.cs:117–145)
- `CreatePreparation(string basePath, bool requiresElevation)` — erzeugt `UpdatePreparationResult` mit Pfaden unterhalb der Temp-Basis (bewusst mit Leerzeichen: `"extracted path"`, `"updates path"`).
- `CapturingLauncher : IUpdateProcessLauncher` — zeichnet `FileName`, `Arguments`, `RunElevated` auf, gibt `true` zurück.
- `FailingLauncher : IUpdateProcessLauncher` — gibt `false` zurück (simuliert fehlgeschlagenen Prozessstart).

### `TempDirectory`
Datei: `src/Softwareschmiede.Tests/Helpers/` — `IDisposable`-Tempverzeichnis-Fixture (`temp.Path`), u. a. in `UpdateScriptServiceTests` genutzt.

### `UpdateE2EFixture`
Datei: `src/Softwareschmiede.Tests/E2E/UpdateE2EFixture.cs` — isolierte Update-Pipeline für E2E: Fixture-HTTP-Handler, Szenario-Steuerung, JSONL-Protokoll, Gate-/Lesefehler-Mechanik, Testwurzel.

### `RecordingUpdateProcessLauncher`
Datei: `src/Softwareschmiede.App/Services/Testing/RecordingUpdateProcessLauncher.cs` — produktivseitiger E2E-Adapter (siehe `inventory/logic.md`); macht den aufgelösten `fileName` im E2E-Protokoll sichtbar.
