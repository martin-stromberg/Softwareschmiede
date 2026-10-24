# Test-Ausgangszustand

Ermittelt vor jeder Änderung an Produktivcode/Tests. Projektvorgaben: `CLAUDE.md` (stabile Lane `Category!=OsInterface`, OsInterface separat; `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` in dieser Sandbox; vollständiger Build vor Tests) und CI `.github/workflows/pr-staging-ci.yml` (vier Lanes: Tests + IntegrationTests × regular + OsInterface).

## Ausgangszustand

| Merkmal | Wert |
|---|---|
| Datum/Uhrzeit | 2026-10-24 10:33–10:56 MESZ (UTC+02:00) |
| Branch / Commit | `task/d539595c84dd4931834bbafa2af45a39-fehlerhafte-event-automatisier` @ `f18f506e2dba0c4892df65c6f641d833b060b046` |
| Uncommittete Änderungen | nur untracked: `docs/features/task/d539595c84dd4931834bbafa2af45a39-fehlerhafte-event-automatisier/` |
| .NET SDK / Runtime | 10.0.402 / Microsoft.NETCore.App 10.0.13 (Ziel `net10.0`, App `net10.0-windows10.0.17763.0`) |
| Arbeitsverzeichnis aller Läufe | Repo-Root `D:\Repositories\softwareschmiede\d539595c-84dd-4931-834b-bafa2af45a39` |
| Umgebung | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` inline bei jedem `dotnet test` |

## Build (vor den Tests)

| Befehl | Ergebnis |
|---|---|
| `dotnet build Softwareschmiede.slnx -c Debug` | Erfolg, 0 Warnungen, 0 Fehler, 00:18 — inkl. `Softwareschmiede.App` und beider Testprojekte; kein Dateilock (keine laufende `Softwareschmiede.App.exe`) |

## Testläufe

| # | Befehl (gekürzt) | Exit | Gesamt | Bestanden | Fehlgeschlagen | Übersprungen | Dauer | Log |
|---|---|---|---|---|---|---|---|---|
| 1 | `dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category!=OsInterface"` | 0 | 2161 | 2160 | 0 | 1 | 2,9 min | [regular-tests.log](test-results/regular-tests.log) |
| 2 | `dotnet test src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj --filter "Category!=OsInterface"` | 0 | 219 | 219 | 0 | 0 | 26 s | [regular-integration.log](test-results/regular-integration.log) |
| 3 | `dotnet test src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj --filter "Category=OsInterface"` | 0 | 9 | 9 | 0 | 0 | 4 s | [os-interface-integration.log](test-results/os-interface-integration.log) |
| 4 | `dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category=OsInterface"` | 1 | 71 | 64 | **2** | 5 | 13,5 min | [os-interface-tests.log](test-results/os-interface-tests.log) |

Alle Aufrufe mit `--logger "console;verbosity=normal"`, synchron im Vordergrund abgewartet (das Tool-Harness hat die Prozesse nach 10 s Leerlauf intern in den Hintergrund verschoben; sie liefen unverändert zu Ende, Ergebnisse vollständig im Log). Kein `--no-build` verwendet — der Build lag bereits vor.

## Fehlgeschlagene Tests (preexisting, Ausgangslauf #4 — OsInterface-Lane)

Beide Fehler liegen **außerhalb** des Anforderungsbereichs (kein SCM-/Versionsbezug) und gelten damit als preexisting.

### 1. `Softwareschmiede.Tests.E2E.E2E_AutonomAufgabenFeatureFlagDisabled.AutonomAufgabeInitialisieren_ZeigtFehlermeldungStattDialog_WennFeatureFlagDeaktiviert`

- **Suite/Datei:** `src/Softwareschmiede.Tests/E2E/E2E_AutonomAufgabenFeatureFlagDisabled.cs:41` (E2E, FlaUI)
- **Fehlermeldung:** `TextBox 'ProjektName' enthält nach der Eingabe nicht den erwarteten Text. Erwartet: 'Auto', tatsächlich: 'Aut'.`
- **Stacktrace (Auszug):** `ElementWaitHelper.<ReplaceTextBoxText>g__WaitForText|10_0` → `ReplaceTextBoxText` (`src/Softwareschmiede.Tests/E2E/ElementWaitHelper.cs:288,312`) → `ProjectDetailView.SetProjectName` (`Views/ProjectDetailView.cs:134`) → `ProjectListView.CreateProject` → `WpfTestBase.SetupProjectMitNeuerAufgabe*` → Testzeile 41.
- **Einordnung:** UI-Tastatureingabe-Race beim Projektnamen („Auto" getippt, „Aut" angekommen) in einem Test zum Autonom-Aufgaben-Feature-Flag — kein Bezug zur SCM-Erkennung.

### 2. `Softwareschmiede.Tests.App.Controls.TerminalControlTests.OnPreviewKeyDown_CtrlShiftCOhneAuswahl_LaesstClipboardUndCliInputUnveraendert`

- **Suite/Datei:** `src/Softwareschmiede.Tests/App/Controls/TerminalControlTests.KeyInput.cs:46`
- **Fehlermeldung:** `Expected System.Windows.Clipboard.GetText() to be "unverändert" with a length of 11, but "" has a length of 0, differs near "" (index 0).`
- **Stacktrace (Auszug):** `WpfUnitTestHelpers.RunOnSta` (`src/Softwareschmiede.Tests/Helpers/WpfUnitTestHelpers.cs:31`) → `TerminalControlTests.OnPreviewKeyDown_CtrlShiftCOhneAuswahl_LaesstClipboardUndCliInputUnveraendert` (`TerminalControlTests.KeyInput.cs:46`).
- **Einordnung:** Echter WPF-Clipboard-Zugriff — die gesetzte Zwischenablage war zum Assert-Zeitpunkt leer (umgebungsabhängiger OS-Interface-Test, Kategorie OsInterface); kein Bezug zur SCM-Erkennung.

## Übersprungene Tests

| Lauf | Test | Grund |
|---|---|---|
| #1 | `Softwareschmiede.Tests.Application.Services.ArbeitsverzeichnisOeffnenServiceTests.Oeffne_AufNichtWindows_WirftPlatformNotSupportedException` | `Skip.If(OperatingSystem.IsWindows())` — Test gilt nur für Nicht-Windows (By-Design-Skip) |
| #4 | `Softwareschmiede.Tests.E2E.E2E_RepositoryInitialisierungAusfuehrungTests.InitialisierungsskriptAusfuehrung` | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` — bestätigte Sandbox-Limitation (ConPTY-Kind nicht pseudo-konsolen-isoliert), kein Code-Fehler |
| #4 | `Softwareschmiede.Tests.E2E.End2EndTest.ConPty_LifecycleUndReplay_E2E` | dto. |
| #4 | `Softwareschmiede.Tests.E2E.End2EndTest.TerminalText_LiveMarkCopyAndKeepSelection_E2E` | dto. |
| #4 | `Softwareschmiede.Tests.E2E.End2EndTest.ConPty_WeitereLebenszyklusSzenarien_E2E` | dto. |
| #4 | `Softwareschmiede.Tests.E2E.End2EndTest.ConPty_SessionReattach_KeineDoppelausgabe_E2E` | dto. |

## Build-/Infrastrukturfehler

Keine. (Getrennt von Testfehlern: nichts vorzutragen.)

## Für die Anforderung relevante Tests — alle im Ausgangslauf bestanden

| Test (Suite, Datei) | Bezug | Ergebnis |
|---|---|---|
| `Softwareschmiede.IntegrationTests.Services.ScmEventCliExchangeAccessTests` (alle Fakten/Theorien: `ActualStartSpec_AllowsWorkspaceChannelWithoutAddingBypass`, `ActualStartSpec_RejectsOverridesWithoutMutatingThem`, `ConfigurationFixtures_EnforceRestrictionsAndPreserveFiles`, `LivingRun_RequiresUnchangedCapturedConfiguration`, `ReadOnlySimulator_ReportsChannelDenialWithoutLosingPromptRecognition`, `PendingProbe_ResendsAfterDelay_UpToCap`, `ProbeLostToTrustDialog_ResumesAfterAnswer`, `TrustPrompt_NeverReceivesProbeOrConfirmation`, `RestrictedWindowsProcess_*`, `RestrictingSid_RejectsAccountsAndBroadPrincipals`) — `src/Softwareschmiede.IntegrationTests/Services/ScmEventCliExchangeAccessTests{.cs,.PendingProbe.cs,.RestrictedProcess.cs}` | InlineData-Banner mit gepinnten Versionen (Zeilen 50-52, 22-24, 56-58, 94-96, 155-157; `Banner(provider)` 263-268) | bestanden (Lauf #2) |
| `Softwareschmiede.IntegrationTests.Services.ScmEventDispatchTests_Devin` (`DevinTask_WithGrantingStartContract_DeliversEvent`, `DevinTask_WithAutoPermissionMode_WaitsWithoutPrompt`, `DevinEnvironmentPermissionMode_IsEvaluated`, `DevinPermissionModeArgument_OverridesEnvironment`) — `src/Softwareschmiede.IntegrationTests/Services/ScmEventDispatchTests_Devin.cs` | Devin-End-to-End-Dispatch über `ScmKiProfile.Devin` (gepinntes Banner) | bestanden (Lauf #2) |
| `Softwareschmiede.Tests.Application.Services.ScmEventOrchestratorTests` (5 Fakten/Theorien; Reason-Assertions „Eingabebereitschaft unbekannt" Zeile 144, `DoesNotContain` Zeile 164, „Vertrauenswürdigkeit" Zeile 163) — `src/Softwareschmiede.Tests/Application/Services/ScmEventOrchestratorTests.cs` | Warte-Grund- und `ConfirmReadyAsync`-Semantik | bestanden (Lauf #1) |
| `Softwareschmiede.Tests.E2E.ScmEventsE2ETests.ScmEvents_QueueReceptionDispatchAndReplies_UseRealPipeline` — `src/Softwareschmiede.Tests/E2E/E2E_ScmEvents.cs` | WaitReason-Assertions „Eingabebereitschaft unbekannt" (Zeilen 191, 394, 406, 421) und `ScmEventConfirmReady`-Klicks (197, 399, 410, 427) über `fixture.Control("unknown-ready", …)` | **bestanden** (Lauf #4, 1 m 55 s) |

## Betroffene Testartefakte (Bestand)

- `src/Softwareschmiede.Tests/Helpers/ScmDispatchFixture.cs` — `ScmKiProfile.Devin` (Zeilen 29-31) mit gepinntem Banner `Devin CLI\nv3000.11.92`; `ScmKiProfile.Simulator`; `SetPrompt`/`Screen`-Hilfen; wird per `<Compile Include … Link>` auch in `Softwareschmiede.IntegrationTests` verwendet.
- `plugins/Softwareschmiede.Plugin.KiSimulator/KiSimulatorPlugin.cs:131-133` — Fixture-Gate `unknown-ready` steuert `SCM_READY_V1`; `SCM_SIMULATOR_PROTOCOL_V1`-Vertrag bleibt laut Anforderung unberührt.
- `src/Softwareschmiede.IntegrationTests/Services/ScmRestrictedWindowsProcess.cs` + `RestrictedDirectory`-ACL-Hilfen — Prozess-/SID-Nachweis, unberührt.
- Nicht vorhanden: Tests für abweichende Versions-Banner oder „CLI unbekannt"-Grundtexte; kein Test benennt eine erkannte Versionszeichenfolge im Reason.

## Hinweise

- Die E2E-Test `E2E_ScmEvents` deckt „unknown-ready" nur über den **Simulator** ab (kein Ready-Token), nicht über ein abweichendes Banner einer echten CLI — der Anforderungs-Fehlerpfad („erkannt, aber Version außerhalb") ist heute gar nicht darstellbar.
- Lauf #4 dauerte 13,5 min inkl. realer WPF-Fenster — die prozess-scoped Win32-Fensterliste (Commit `f18f506`) ist aktiv.
- **Ältere Artefakte:** `test-results/` enthält zusätzlich `baseline-*.console.log`/`.trx` eines fragmentierten früheren Laufs am selben Tag (08:04–08:12; regular identisch grün: 2160/2161 + 219/219 + 9/9; OsInterface in Teilstücken: `none2e` 44/44, `scmevents` 1/1, `e2e-rest` unvollständig). Dort zeigte sich mit `E2E_RepositoryManagementTests.BasisBranchVerwaltung` (`Expected: "staging", Actual: "istaging"`) bereits dieselbe Klasse Tastatureingabe-Race wie Fehler 1 oben — stützt die Einordnung als preexisting, umgebungsbedingtes Flake-Muster.
- Verifizierte Testbefehle stammen aus `.github/workflows/pr-staging-ci.yml` (Zeilen 128, 132, 222, 226) bzw. `CLAUDE.md`; die CI nutzt `--no-build` nach separatem Build-Schritt — hier wurde stattdessen vorab vollständig gebaut und ohne `--no-build` getestet.
