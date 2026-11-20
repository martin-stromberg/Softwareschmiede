# Tests: Bestandsaufnahme und Ausgangszustand

## Test-Ausgangszustand vor der Umsetzung

- **Zeitpunkt (mit Zeitzone):** 2026-11-19, ca. 22:14–22:45 Uhr +0100 (21:14–21:45 UTC)
- **Branch und Commit-ID:** `task/issue-428-3a03b2a950b844468c09c6a65dfc52c2-flaky-tests-umgebungssensitive`, Commit `20f24b4d0a0eb227127d4d548dc11a6cc256469a`
- **Uncommittete Änderungen im getesteten Stand:** Keine Quellcode-Änderungen; lediglich das untracked Feature-Dokumentationsverzeichnis `docs/features/task/issue-428-…/` (requirement.md, todo.md, inventory). Getesteter Code = Commit-Stand.
- **Testumgebung und Runtime-/SDK-Versionen:** Windows (Git-Bash-Sandbox ohne interaktive Desktop-Session, kein laufender `Softwareschmiede.App.exe`- oder `testhost`-Prozess), .NET SDK `11.0.100`, Ziel-Framework `net11.0` bzw. `net11.0-windows10.0.17763.0`, xUnit v3 (`xunit.v3` 4.0.3, VSTest-Modus über `IsTestingPlatformApplication=false`).
- **Ermittelte Testsuiten und Quellen der Testbefehle:** Zwei Testprojekte mit `Microsoft.NET.Test.Sdk`: `src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj` und `src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj`. Befehle und Lane-Aufteilung aus `CLAUDE.md` (stabile Lane `Category!=OsInterface`), `.github/workflows/pr-staging-ci.yml` (Jobs `build & test` / `os-interface tests`) und `docs/help/stabilitaet/os-interface-tests.md`. Vor den Testläufen voller Build beider Testprojekte in `-c Debug` (je 0 Warnungen, 0 Fehler).
- **Gesetzte Umgebungsvariablen:** `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` bei allen Läufen (Projektregel für diese Sandbox, siehe `CLAUDE.md`); zusätzlich `SOFTWARESCHMIEDE_SKIP_WPF_E2E_TESTS=1` beim `OsInterface`-Lauf von `Softwareschmiede.Tests` — identisch zur Env des CI-Jobs `os-interface tests`.

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| 1 | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj --no-build -c Debug --filter "Category!=OsInterface" --logger "trx;LogFileName=inventory-regular-integration.trx" --logger "console;verbosity=normal"` | Repo-Root | 0 | 366 | 0 | 0 | [TRX](test-results/inventory-regular-integration.trx) |
| 2 | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj --no-build -c Debug --filter "Category=OsInterface" --logger "trx;LogFileName=inventory-osinterface-integration.trx" --logger "console;verbosity=normal"` | Repo-Root | 0 | 9 | 0 | 0 | [TRX](test-results/inventory-osinterface-integration.trx) |
| 3 | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --no-build -c Debug --filter "Category!=OsInterface" --logger "trx;LogFileName=inventory-regular-tests.trx" --logger "console;verbosity=normal"` | Repo-Root | 0 | 2692 | 0 | 3 | [TRX](test-results/inventory-regular-tests.trx) |
| 4 | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 SOFTWARESCHMIEDE_SKIP_WPF_E2E_TESTS=1 dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --no-build -c Debug --filter "Category=OsInterface" --logger "trx;LogFileName=inventory-osinterface-tests.trx" --logger "console;verbosity=normal"` | Repo-Root | 0 | 51 | 0 | 20 | [TRX](test-results/inventory-osinterface-tests.trx) |

Laufzeiten: Lauf 1 ≈ 42 s, Lauf 2 ≈ 4 s, Lauf 3 ≈ 3,5 min, Lauf 4 ≈ 24 s. Zusammen 3141 entdeckte Tests in beiden Projekten über beide Kategorien.

### Nachgewiesene bestehende Testfehler

**Keine.** In allen vier Läufen gab es keinen einzigen fehlgeschlagenen Test. Insbesondere liefen die 10 bisher ungetaggten RestrictedProcess-Testfälle (Lauf 1, reguläre Lane) vollständig erfolgreich — der dokumentierte CI-Fehlschlag aus PR #427 (`RestrictedWindowsProcess_ReadsWritesAndCompletesChannel_ButCannotWriteAppData(provider: "Devin", rename: False)`) ist in dieser Sandbox nicht reproduziert; er gilt damit **nicht** als „preexisting" im Sinne der Nachweisregel, sondern als umgebungs-/lastabhängiges Flake-Bild.

### Testlücken und Ausführungsprobleme

- **Lauf 3 — 3 Übersprungene (plattformbedingt, ehrliche Skips):**
  - `SystemShutdownServiceTests.ResolveShutdownCommand_Linux_BleibtUnveraendert` — `Assert.SkipUnless(OperatingSystem.IsLinux(), …)`
  - `SystemShutdownServiceTests.ResolveShutdownCommand_MacOS_BleibtUnveraendert` — `Assert.SkipUnless(OperatingSystem.IsMacOS(), …)`
  - `ArbeitsverzeichnisOeffnenServiceTests.Oeffne_AufNichtWindows_WirftPlatformNotSupportedException` — `Assert.SkipWhen(OperatingSystem.IsWindows(), …)`
- **Lauf 4 — 20 Übersprungene (Flag-bedingt, decken sich mit dem CI-Job-Verhalten):**
  - 15× `SOFTWARESCHMIEDE_SKIP_WPF_E2E_TESTS=1` (FlaUI-Fenster-E2E, u. a. `End2EndTest.General_*_E2E`, `WpfE2ETests.WpfBasisSzenarien`, `ProjectDetailE2ETests.ProjektDetailSzenarien`, `ScmEventsE2ETests`, `E2E_RepositoryManagementTests`, `E2E_RepositoryInitialisierungConfigTests`, `ReplayText_*_E2E`) — in dieser Sandbox ohne interaktive Desktop-Session nicht ausführbar.
  - 5× `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` (`End2EndTest.ConPty_LifecycleUndReplay_E2E`, `ConPty_WeitereLebenszyklusSzenarien_E2E`, `ConPty_SessionReattach_KeineDoppelausgabe_E2E`, `TerminalText_LiveMarkCopyAndKeepSelection_E2E`, `E2E_RepositoryInitialisierungAusfuehrungTests.InitialisierungsskriptAusfuehrung`).
  - Damit sind die echten Fenster-/ConPTY-E2E-Tests in dieser Bestandsaufnahme **nicht ausgeführt** — ihr Zustand ist „unbekannt", nicht „grün".
- **Umgebungsbeobachtung (kein Testfehler):** In Lauf 4 schrieben von Tests gestartete `cmd.exe /c timeout /t 30`-Kindprozesse `timeout: invalid time interval '/t'` auf die Konsole — in dieser Sandbox löste `cmd.exe` das MSYS-GNU-`timeout` statt `C:\Windows\System32\timeout.exe` auf. Die betroffenen `[OsInterfaceFact]`-Tests bestanden trotzdem. Relevanz: triviale Prozessstarts in Test-Helfern (`StartTestProcess` in `KiAusfuehrungsServiceTests.cs`, Zeile 1033 ff.) sind subtil umgebungsabhängig.

## Testklassen

### `ScmEventCliExchangeAccessTests` (`Softwareschmiede.IntegrationTests.Services`, `sealed partial`, 3 Dateien)

Datei `src/Softwareschmiede.IntegrationTests/Services/ScmEventCliExchangeAccessTests.RestrictedProcess.cs` — **echte OS-Schnittstelle, ohne Trait:**

- `RestrictedWindowsProcess_ReadsWritesAndCompletesChannel_ButCannotWriteAppData` — `[Theory]`, 6 Fälle (provider × rename): startet via `ScmRestrictedWindowsProcess.Start` einen echten, per `CreateRestrictedToken`/`CreateProcessAsUser` eingeschränkten PowerShell-Kindprozess mit dedizierter SID, ACL-gesichertem Workspace und App-Data-Verzeichnis; Host-Pollfenster `probeDeadline`/`deadline` je 30 s (Zeilen 137, 154) vs. Kind-Budget `$phaseTimeout = 90` (Zeile 56).
- `RestrictedWindowsProcess_WithoutDedicatedCapability_DeniesChannel` — `[Theory]`, 3 Fälle: gleicher echter eingeschränkter Prozess, erwartet `UnauthorizedAccessException` (Exit-Code 5).
- `RestrictingSid_RejectsAccountsAndBroadPrincipals` — `[Fact]`: nutzt `WindowsIdentity.GetCurrent()` und `ScmRestrictedProcessAccess.ValidateRestrictingSid` (reine Auswertelogik, aber Windows-Identitäts-API).
- Hilfsmethoden: `RestrictDirectory` (ACL-Manipulation via `DirectorySecurity`, Zeilen 278–290), `GitInit` (echter `git init`-Prozessstart, Zeilen 292–300).

Datei `src/Softwareschmiede.IntegrationTests/Services/ScmEventCliExchangeAccessTests.cs` — deterministisch (Mock-Sessions), kein Trait:

- `ReadOnlySimulator_ReportsChannelDenialWithoutLosingPromptRecognition`, `ActualStartSpec_AllowsWorkspaceChannelWithoutAddingBypass` (7 Fälle, enthält `GitInit` → echter `git`-Spawn), `UnsupportedVersion_ReportsHonestReason` (4), `UnparseableVersion_ReportsVersionNotDetermined`, `TokenlessAnchorReprint_KeepsClassifiedRecognition` (5), `TokenlessAnchor_UpgradesWhenVersionAppears`, `SimulatorShell_ReportsMissingChannelInsteadOfMissingBanner`, `UnknownCli_ReportsHonestReason`, `ActualStartSpec_RejectsOverridesWithoutMutatingThem` (30), `ConfigurationFixtures_EnforceRestrictionsAndPreserveFiles` (21), `LivingRun_RequiresUnchangedCapturedConfiguration` (3, `GitInit` + `ScmDispatchFixture.CompleteProbeAsync`).
- `Dispose()` (Zeile 329): nacktes `Directory.Delete(_root, recursive: true)` — kein Prozess-Kill auf Fixture-Ebene, kein Retry, wirft bei `IOException` direkt (überdeckt den eigentlichen Testfehler als `AggregateException`); `_root` (Zeile 19) unter `Path.GetTempPath()`.

Datei `src/Softwareschmiede.IntegrationTests/Services/ScmEventCliExchangeAccessTests.PendingProbe.cs` — deterministisch (Mock-Sessions + `SettableTimeProvider`), kein Trait:

- `PendingProbe_ResendsAfterDelay_UpToCap` (4), `PendingProbe_ResendKeepsSessionName`, `ProbeLostToTrustDialog_ResumesAfterAnswer` (3), `TrustPrompt_NeverReceivesProbeOrConfirmation` (3) — alle rufen `GitInit` auf (echter `git init`-Spawn, siehe offene Frage 4 des Issues); Hilfsmethode `SetScreen`.

### `StartPs1IntegrationTests` (`Softwareschmiede.IntegrationTests.Scripts`)

Datei `src/Softwareschmiede.IntegrationTests/Scripts/StartPs1IntegrationTests.cs` — `[Trait("Category", "OsInterface")]` auf Klassenebene (Zeile 10), 9 `[Fact]`-Methoden, starten echte `powershell.exe`-Prozesse (`RunStartScriptAsync`, Zeile 414); `Dispose()` löscht Temp-Verzeichnisse ohne Retry (Zeilen 320–326). Einziges bisher getaggtes Testartefakt im Integrationsprojekt.

### `ScmEventDispatchTests_*` und Monitoring-Testklassen (`Softwareschmiede.IntegrationTests.Services`)

25 Dateien nutzen `ScmDispatchFixture` (u. a. `ScmEventDispatchTests.cs`, `…_AgentActions`, `…_Attachments`, `…_Cancel`, `…_Delay`, `…_Devin`, `…_HostRetry`, `…_IssueAufbereitung`, `…_IssueBodyUpdate`, `…_Limits`, `…_PromptAppendix`, `…_Startpruefung`, `ScmEventMonitoringServiceTests_*`, `ScmBackmergeMonitoringServiceTests`, `ScmIssueAufbereitungMonitoringServiceTests`, `ScmTaskCompletionMonitoringServiceTests`, `ScmEventCliExchangeAccessTests*`). Alle ohne `OsInterface`-Trait; die Fixture startet indirekt echte Prozesse (siehe [audit.md](audit.md) und [logic.md](logic.md)).

### `ScmEventExchangeServiceTests` / `ScmEventExchangeAttachmentTests` (`Softwareschmiede.Tests.Application.Services`)

- `ScmEventExchangeServiceTests.cs` — kein Trait; `Git(params string[])`-Helfer (Zeile 481 ff.) startet echtes `git` (ctor `Git("init")`, `check-ignore`, `add`, `status`, `ls-files` in mehreren Tests); `Dispose()` normalisiert Dateiattribute, dann `Directory.Delete(_root, true)` ohne Retry (Zeilen 494–499).
- `ScmEventExchangeAttachmentTests.cs` — kein Trait; `Collect_RejectsReparsePointInDeclaredPath` (ab Zeile 111) startet echten `cmd.exe /c mklink /J`-Prozess (Windows-guarded, early-return bei `ExitCode != 0`); `Git`-Helfer ab Zeile 248; `Dispose()` entfernt Reparse-Punkte, normalisiert Attribute, `Directory.Delete` ohne Retry (Zeilen 262–268).

### `GitHubEventSourceTests` (`Softwareschmiede.Tests.Infrastructure.Plugins`)

Kein Trait; Testmethode ab Zeile 637 startet echten `cmd.exe /c mklink /J`-Junction-Prozess (Windows-guarded, Zeilen 647–651) zum Reparse-Point-Nachweis.

### `KiAusfuehrungsServiceTests` (`Softwareschmiede.Tests.Application.Services`)

Gemischt: ~20 `[OsInterfaceFact]`/`[OsInterfaceTheory]`-Methoden (Zeilen 71–727) nutzen Launcher-Doubles mit echten `cmd.exe`-Prozessen (`StartTestProcess` `/c timeout /t 30`, `StartLongRunningTestProcess` `/c ping -n 31`, Zeilen 1032–1050); die übrigen `[Fact]`-Methoden sind deterministisch (`TestKiAusfuehrungsServiceFactory` mit In-Memory-Launcher bzw. `TestPseudoConsoleSessionFactory`). Bestehende Tagging-Granularität: methodenweise.

### `TerminalControlTests` (`Softwareschmiede.Tests.App.Controls`, `sealed partial`, 3 Dateien)

- `TerminalControlTests.ClipboardPaste.cs` und `TerminalControlTests.KeyInput.cs`: echte Clipboard-Tests mit `[OsInterfaceFact]` + `ClipboardTestLock` — bereits getaggt.
- `TerminalControlTests.cs`: deterministische `[Fact]`-Methoden über `TestPseudoConsoleSessionFactory` (In-Memory-Streams) — korrekt ungetaggt.

### `End2EndTest` / E2E-Testklassen (`Softwareschmiede.Tests.E2E`)

`MainTest.cs` (Zeilen 9–12): `[Trait("Category", "E2E")]`, `[OsInterface]`, `[Collection("E2E")]` auf Klassenebene — alle `E2E_*`-Partial-Dateien enthalten nur `protected`-Szenario-Methoden (`…_E2E`), keine eigenen Testmethoden → korrekt ohne eigenes Trait. Weitere getaggte E2E-Klassen: `E2E_ConPtyLifecycle.cs`, `E2E_RepositoryManagementTests.cs`, `E2E_RepositoryInitialisierung*Tests.cs`, `ProjectDetailE2ETests.cs`, `E2E_ScmEvents.cs` u. a.

## Hilfsmethoden

### `ScmDispatchFixture` (`src/Softwareschmiede.Tests/Helpers/ScmDispatchFixture.cs`, per `<Compile Include … Link>` ins Integrationsprojekt eingebunden)

- Konstruktor: baut echten DI-`ServiceProvider` (SQLite-DB unter `Root`); `ITerminalSessionFactory`-Mock startet pro Session echten `cmd.exe /c ping -n 300 127.0.0.1`-Prozess (Zeile 136) und registriert ihn in `_processes` (Zeile 60).
- `CompleteProbeAsync` (Zeilen 296–304): vollzieht die Dateitool-Kanalprobe rein per Datei-I/O (deterministisch).
- `InitGit` (Zeilen 321–329): echter `git init --quiet`-Prozessstart.
- `DisposeAsync` (Zeilen 331–341): killt registrierte Prozesse mit `Kill(entireProcessTree: true)` + `WaitForExitAsync` (best-effort, fängt `InvalidOperationException`), disposed Services, dann `Directory.Delete(Root, true)` — ohne Retry.

### `ScmRestrictedWindowsProcess` (`src/Softwareschmiede.IntegrationTests/Services/ScmRestrictedWindowsProcess.cs`)

- `Start(workspace, command, capabilitySid)` (Zeile 16): `CreateRestrictedToken` + `CreateProcessAsUser` → echter `powershell.exe -EncodedCommand`-Kindprozess mit dedizierter Capability-SID, minimierter Umgebung (`PATH`, `TEMP`/`TMP` auf Workspace, `PSModuleAnalysisCachePath=NUL`, `$PSModuleAutoLoadingPreference='None'`).
- Private Hilfen: `SetDefaultDacl`, `GetLogonSid`, Win32-P/Invoke (`OpenProcessToken`, `GetTokenInformation`, `SetTokenInformation`, `CreateRestrictedToken`, `CreateProcessAsUser`, `CloseHandle`).

### `TestPseudoConsoleSessionFactory` / `TestTerminalSessionFactory` / `TestKiAusfuehrungsServiceFactory` (`src/Softwareschmiede.Tests/Helpers/`)

- `TestPseudoConsoleSessionFactory.Create(...)`: `PseudoConsoleSession` mit `NullPseudoConsoleHandle` + `Process.GetCurrentProcess()` + In-Memory-Streams — deterministisch, kein OS-Kontakt.
- `TestKiAusfuehrungsServiceFactory.Create(...)`: „OS-freie" `KiAusfuehrungsService`-Instanz (XML-Kommentar Zeile 11); optionaler `IPseudoConsoleProcessLauncher`-Launcher-Double.

### `ClipboardTestLock` / `ClipboardTestHelfer` (`src/Softwareschmiede.Tests/Infrastructure/Testing/`)

Serialisierungs-Lock und Retry-Helfer für echte Clipboard-Tests (genutzt von den `[OsInterfaceFact]`-Tests in `TerminalControlTests.*` und `E2E_KonsolenTestfenster`).

### `WpfTestBase` / `WpfE2EEnvironmentProbe` / `ConPtyEnvironmentProbe` (`src/Softwareschmiede.Tests/E2E/`)

- `LaunchApp` (Zeile 235 ff.): zentraler Skip-Punkt — `Assert.SkipWhen` bei gesetztem `SOFTWARESCHMIEDE_SKIP_WPF_E2E_TESTS`; startet echte `Softwareschmiede.App.exe` via `FlaUI.Core.Application.Launch`.
- `SkipWennConPtyNichtVerfuegbar` (Zeile 989): `Assert.SkipWhen` bei `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` (über `ConPtyEnvironmentProbe.IsAvailable`).
- Beide Probes sind bewusst reine Env-Flag-Auswertungen ohne Auto-Detection (dokumentierte Entscheidung aus Issue 114).
