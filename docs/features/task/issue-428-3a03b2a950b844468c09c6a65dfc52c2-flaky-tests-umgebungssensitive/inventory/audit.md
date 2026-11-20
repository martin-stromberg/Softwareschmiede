# Audit: OS-nahe Tests und Tagging-Lücken

Heuristik aus dem Issue (`Process.Start`, `ScmRestrictedWindowsProcess`, `PseudoConsole`, `ConPty`, `FlaUI`, `Clipboard`) über beide Testprojekte, jeden Treffer fachlich geprüft (echter OS-Kontakt vs. Helper/Indirektion vs. Kommentar). Stand: Commit `20f24b4`, 2026-11-19.

## 1. Bestätigte Tagging-Lücken (echter OS-Kontakt, kein `OsInterface`-Trait)

| Datei | Testmethoden | Echter OS-Kontakt | Trait heute |
|-------|--------------|-------------------|-------------|
| `src/Softwareschmiede.IntegrationTests/Services/ScmEventCliExchangeAccessTests.RestrictedProcess.cs` | `RestrictedWindowsProcess_ReadsWritesAndCompletesChannel_ButCannotWriteAppData` (Theory, 6), `RestrictedWindowsProcess_WithoutDedicatedCapability_DeniesChannel` (Theory, 3), `RestrictingSid_RejectsAccountsAndBroadPrincipals` (Fact) | `ScmRestrictedWindowsProcess.Start` → `CreateRestrictedToken`/`CreateProcessAsUser` echter eingeschränkter `powershell.exe`-Kindprozess (Zeilen 118, 235); `DirectorySecurity`-ACLs; `WindowsIdentity.GetCurrent()`; `git init` | **kein Trait** — lief im Ausgangslauf in der regulären Lane (10/10 grün) |
| `src/Softwareschmiede.Tests/Application/Services/ScmEventExchangeAttachmentTests.cs` | u. a. `Collect_RejectsReparsePointInDeclaredPath` (ab Zeile 111) | `Process.Start` auf `cmd.exe /c mklink /J` (Zeilen 117–119, Windows-guarded); `Git()`-Helfer echter `git`-Spawn (Zeile 248 ff., ctor Zeile 21) | kein Trait |
| `src/Softwareschmiede.Tests/Application/Services/ScmEventExchangeServiceTests.cs` | mehrere Tests mit `Git("check-ignore"/"add"/"status"/"ls-files")` (Zeilen 33–35, 116, 289 u. a.) | `Git()`-Helfer echter `git`-Spawn (Zeile 481 ff., ctor Zeile 19) | kein Trait |
| `src/Softwareschmiede.Tests/Infrastructure/Plugins/GitHubEventSourceTests.cs` | Testmethode ab Zeile 637 | `Process.Start` auf `cmd.exe /c mklink /J` (Zeilen 647–651, Windows-guarded) | kein Trait |

## 2. Indirekter OS-Kontakt über Fixtures (Audit-Sonderfall)

| Quelle | Effekt | Betroffener Umfang |
|--------|--------|--------------------|
| `src/Softwareschmiede.Tests/Helpers/ScmDispatchFixture.cs`, Zeile 136 | `ITerminalSessionFactory`-Mock startet pro gemockter Session einen echten `cmd.exe /c ping -n 300 127.0.0.1`-Prozess (registriert in `_processes`, sauber gekillt in `DisposeAsync`, Zeile 335) | **25 Dateien** in `Softwareschmiede.IntegrationTests` nutzen die Fixture (`ScmEventDispatchTests*`, `ScmEventMonitoringServiceTests_*`, `ScmBackmergeMonitoringServiceTests`, `ScmIssueAufbereitungMonitoringServiceTests`, `ScmTaskCompletionMonitoringServiceTests`, `ScmEventCliExchangeAccessTests*`) — darunter `ScmEventDispatchTests_Startpruefung.cs`, der im Issue genannte Audit-Kandidat (dessen eigener Treffer nur ein Kommentar ist, Zeile 39) |
| `ScmDispatchFixture.InitGit`, Zeilen 321–329 | echter `git init --quiet`-Prozessstart je `AddAsync`/`CloneRepositoryAsync`-Mock | dieselben 25 Dateien |
| `ScmEventCliExchangeAccessTests.GitInit`, Zeilen 292–300 | echter `git init`-Prozessstart | `ActualStartSpec_…`, `LivingRun_…`, alle `PendingProbe`-Tests u. a. — siehe offene Frage 4 des Issues, ob `git init` als Audit-Kriterium zählt |

Einordnung: Die Fixture-Prozesse sind triviale, deterministische Platzhalter-Prozesse (Ping), keine eingeschränkten/ConPTY-/UI-Prozesse; die Einordnung als `OsInterface` ist eine fachliche Entscheidung, keine offensichtliche Pflicht — dokumentiert, damit sie im Audit-Schritt bewertet wird.

## 3. Geprüfte Fehl-Positive (Heuristik-Treffer ohne echten OS-Kontakt)

| Datei | Grund des Treffers | Einordnung |
|-------|--------------------|------------|
| `src/Softwareschmiede.IntegrationTests/Services/ScmEventDispatchTests_Startpruefung.cs` | Zeile 39: Kommentar „Wie PseudoConsoleSession …" | kein eigener OS-Kontakt (indirekt via Fixture, siehe §2) |
| `src/Softwareschmiede.IntegrationTests/Services/ScmRestrictedWindowsProcess.cs` | eigener Klassenname | Test-Helfer, keine Testmethoden |
| `src/Softwareschmiede.Tests/Infrastructure/Terminal/PseudoConsoleSessionTests{,.WriteInputAsync,.WritePromptAsync}.cs` | Klassenname `PseudoConsoleSession` | alle Sessions via `TestPseudoConsoleSessionFactory` (In-Memory-Streams, `NullPseudoConsoleHandle`) — deterministisch |
| `src/Softwareschmiede.Tests/Infrastructure/Terminal/TerminalReplaySessionTests.cs`, `TerminalSessionServiceTests.cs` | Typname / `session.Process.StartInfo` | Replay-/Fake-Sessions, `RecordingLauncher`-Double |
| `src/Softwareschmiede.Tests/Application/Services/CliProcessManagerTests_LaufStatus.cs`, `PromptZeitVersandServiceTests.cs`, `AufgabeServiceTests_AktiverLauf.cs` | Typname `PseudoConsoleSession` in Code/Kommentaren | `TestPseudoConsoleSessionFactory` bzw. nur Kommentare — deterministisch |
| `src/Softwareschmiede.Tests/App/Controls/KeyToVt100EncoderTests.cs` | Methodenname `EncodeClipboardText` | reine Kodierungs-Unit-Tests, kein Clipboard-Zugriff |
| `src/Softwareschmiede.Tests/App/Controls/TerminalControlTests.cs` (Hauptdatei) | `PseudoConsoleSession`-Typ | Fake-Sessions via Factory; die echten Clipboard-Partials (`.ClipboardPaste.cs`, `.KeyInput.cs`) sind bereits `[OsInterfaceFact]`-getaggt |
| `src/Softwareschmiede.Tests/E2E/E2E_*.cs` ohne Trait-Treffer (u. a. `E2E_AufgabenListeModi`, `E2E_CliRawExport`, `E2E_KonsolenTestfenster`, `E2E_SettingsFeatureFlags`, `E2E_SettingsKiPluginPersistence`, `E2E_TerminalFallbackDiagnose`, `E2E_UpdateSettings`, `E2E_ViewPattern`) | `using FlaUI.*` | `public partial class End2EndTest` — nur `protected`-Szenario-Methoden ohne `[Fact]`/`[Theory]`; die Testmethoden stehen in `MainTest.cs` u. a. und sind auf Klassenebene `[OsInterface]`-getaggt |
| `src/Softwareschmiede.Tests/E2E/{WpfTestBase,ElementWaitHelper,ScmE2EFixture,AppStartupLogInspector,ConPtyEnvironmentProbe,WpfE2EEnvironmentProbe}.cs`, `E2E/Views/**` | FlaUI-Imports | E2E-Infrastruktur/Page-Objects, keine Testmethoden |
| `src/Softwareschmiede.Tests/Helpers/{ScmDispatchFixture,TestPseudoConsoleSessionFactory,TestTerminalSessionFactory,TestKiAusfuehrungsServiceFactory}.cs`, `Infrastructure/Testing/ClipboardTest*.cs` | Musterbegriffe | Test-Infrastruktur, keine Testmethoden |
| `src/Softwareschmiede.Tests/Infrastructure/Plugins/GitHubEventSourceTests.cs` (nur bezüglich Nicht-Junction-Tests) | — | siehe §1: nur die Junction-Methode ist OS-nah |
| `src/Softwareschmiede.Tests/ServiceIntegration/CliEmbeddingServiceIntegrationTests.cs` | `Process`-Bezüge | bereits `[OsInterfaceFact]`/`[Trait("Category","ConPTY")]`-getaggt (Zeilen 49, 64–65) |
| `src/Softwareschmiede.Tests/Infrastructure/Terminal/SimulatedPseudoConsoleProcessLauncherTests.cs` | Launcher-Name | bereits `[OsInterfaceFact]`-getaggt (Zeilen 20, 42, 79, 113) — startet echte `cmd.exe` über `SimulatedPseudoConsoleProcessLauncher` |
| `src/Softwareschmiede.Tests/Application/Services/KiAusfuehrungsServiceTests_WorkingDirectory*.cs` | Prozessbezüge | bereits `OsInterface`-markiert |

## 4. Verifizierte Issue-Fakten (Deadline-Mismatch, Cleanup)

| Behauptung im Issue | Verifikation |
|---------------------|--------------|
| Host-Poll `probeDeadline` ≈ 30 s | `…RestrictedProcess.cs` Zeile 137: `DateTime.UtcNow.AddSeconds(30)` |
| Host-Poll `deadline` ≈ 30 s | Zeile 154: `DateTime.UtcNow.AddSeconds(30)` |
| Kind-Budget `$phaseTimeout = 90` | Zeile 56 des eingebetteten PowerShell-Skripts; drei sequentielle Kind-Phasen je 90 s (Zeilen 57, 72, 100) |
| Fragiles `Directory.Delete` im Fixture-`Dispose` | `ScmEventCliExchangeAccessTests.cs` Zeile 329: `Dispose() => Directory.Delete(_root, recursive: true)` — kein Kill auf Fixture-Ebene (Kill nur im Test-`finally`, Zeilen 184/250), kein Retry, kein Logging |
| Weitere `Dispose`-Fragilität | `ScmEventExchangeServiceTests` (Attribute-Normalisierung + `Directory.Delete`, Zeilen 494–499), `ScmEventExchangeAttachmentTests` (`RemoveReparsePoints` + `Directory.Delete`, Zeilen 262–268), `StartPs1IntegrationTests.Dispose` (Zeilen 320–326), `ScmDispatchFixture.DisposeAsync` (Kill vorhanden, `Directory.Delete` ohne Retry, Zeile 340) |
| `OsInterface`-Attribute nur in `Softwareschmiede.Tests` | `src/Softwareschmiede.Tests/Infrastructure/Testing/{OsInterfaceAttribute,OsInterfaceFactAttribute,OsInterfaceTheoryAttribute,TestCategories}.cs`; `Softwareschmiede.IntegrationTests.csproj` hat **keine** ProjectReference auf `Softwareschmiede.Tests`, bindet aber `ScmDispatchFixture.cs` per `<Compile Include … Link>` ein (Zeile 35) |
| `[Trait("Category","OsInterface")]`-Muster im Integrationsprojekt | `StartPs1IntegrationTests.cs` Zeile 10 |
| Kein `Flaky`-Trait | Volltextsuche `flaky` über `src/`, `docs/help/`, `scripts/` → keine Treffer |

## 5. Umgebungsbeobachtungen aus dem Baseline-Lauf

- Die 10 ungetaggten RestrictedProcess-Fälle liefen in dieser Sandbox **erfolgreich** (Lauf 1) — die Umgebung kann eingeschränkte Prozesse starten; der PR-#427-Fehlschlag ist last-/umgebungsabhängig, nicht deterministisch.
- `cmd.exe /c timeout /t 30`-Kindprozesse von `KiAusfuehrungsServiceTests.StartTestProcess` (Zeile 1036) lösten in dieser Sandbox das MSYS-GNU-`timeout` auf (`timeout: invalid time interval '/t'` im Lauf-4-Output); Tests bestanden dennoch. Beleg dafür, dass auch „triviale" Prozessstarts in Test-Helfern PATH-/Umgebungsannahmen treffen.
