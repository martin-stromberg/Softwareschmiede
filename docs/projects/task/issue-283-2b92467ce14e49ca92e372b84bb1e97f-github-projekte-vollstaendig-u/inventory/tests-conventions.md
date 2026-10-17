# Tests, Testinfrastruktur und Projektregeln

Zurück: [../inventory.md](../inventory.md)

## 1. Testprojekte und Frameworks

| Projekt | Zweck | Framework |
|---------|-------|-----------|
| `src/Softwareschmiede.Tests` | Unit-Tests + FlaUI-WPF-E2E | xunit 2.9.3, Moq 4.*, FlaUI.Core/UIA3 5.0.0, Microsoft.NET.Test.Sdk 18.7.0 |
| `src/Softwareschmiede.IntegrationTests` | Service-/Plugin-Integration mit echter SQLite-DB und echten Prozessen | xunit, Moq |

Kategorien: E2E-Tests tragen `[OsInterface]` (`src/Softwareschmiede.Tests/Infrastructure/Testing/OsInterfaceAttribute.cs`) und `[Trait("Category","E2E")]`/`[Trait("Scenario","…")]`/`[Collection("E2E")]`; SCM-E2E nutzt `WpfTestBase`. Stabiler Test-Lane: `dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category!=OsInterface"`; OS-nahe Tests separat mit `Category=OsInterface`.

## 2. SCM-Abdeckung im Bestand

**Unit-Tests (`src/Softwareschmiede.Tests`):**

- `Application/Services/ScmEventOrchestratorTests.cs`: MissingWriteAccess-Reason-Text, fehlende Standard-CLI aktiviert Aufgabe vor Warteschleife, blockierte Aufgaben werden nie aktiviert (Theory über Status+Pause), Bestätigungs-Runbinding nach Kanalfehler.
- `Application/Services/ScmEventValidationServiceTests.cs`: Trigger/Autor/Editor/Owner-Matrix.
- `Application/Services/ScmEventExchangeServiceTests.cs`, `ScmEventQueueViewModelTests.cs`, `App/Views/ScmEventQueueViewTests_Theme.cs`.
- `Infrastructure/Plugins/GitHubEventSourceTests.cs` (389 Zeilen): GitHub-`ReadEventsAsync`-Parsing/Pagination/Watermark/Identität über austauschbaren `IGitHubEventApiClient`.
- `Helpers/ScmDispatchFixture.cs`: zentrales Fixture — echte SQLite-DB, echte Services (`ScmEventStore`, `ScmEventOutbox`, `ScmEventOrchestrator`, `KiAusfuehrungsService`, `EntwicklungsprozessService`), gemockter `IGitPlugin` als `IScmEventSource`+`IScmEventResponder`, gemockter `IKiPlugin` (`Softwareschmiede.KiSimulator`-Prefix) und `ITerminalSessionFactory` mit echtem `cmd.exe`-Trägerprozess und umschaltbarem Prompt-Buffer (`SetPrompt`, `SCM_READY_V1`/`SCM_BUSY_V1`).

**Integrationstests (`src/Softwareschmiede.IntegrationTests`):**

- `Services/ScmEventDispatchTests.cs`: Alias-/Ambiguitätszuordnung, >100 wartende Events, konkurrierender Dispatch+manueller Start (ein Prozess).
- `Services/ScmEventDispatchTests_HostRetry.cs`: Host-seitige Wiederholungen.
- `Services/ScmEventMonitoringServiceTests_AmbiguityRecovery.cs`: Recovery mehrdeutiger Referenzzuordnung.
- `Services/ScmEventPersistenceTests.cs`: Inbox-/Snapshot-/Checkpoint-Persistenz.
- `Services/ScmEventCliExchangeAccessTests(.RestrictedProcess).cs` + `ScmRestrictedWindowsProcess.cs`: Dateitool-Probe und eingeschränkte Prozessidentitäten (echte Windows-Prozesse/ACLs).

**E2E (`src/Softwareschmiede.Tests/E2E`):**

- `E2E_ScmEvents.cs` (543 Zeilen, **eine** konsolidierte Testmethode `ScmEvents_QueueReceptionDispatchAndReplies_UseRealPipeline` + `[Fact]`): komplette Pipeline mit echter WPF-App, echtem `GitHubPlugin` (Parsing), SQLite-Inbox, isoliertem Remote-Transport — Queue-Navigation, Empfangsbestätigung/Wartehinweis, Restart-Persistenz, Plugin-Auswahl/Abbrechen, Zustellung, Annahme/Aktionen/Abschluss, Filter, blockierte Aufgaben, Issue-/PR-Antworten.
- `E2E_ScmRegressionSimulator.cs`, `ScmE2EFixture.cs`, `ScmE2EFixtureTests_RuntimeResult.cs`.
- `ScmE2EFixture` legt 17 Aufgaben in verschiedenen Status (`Neu/Gestartet/Beendet/Archiviert`, pausiert, ohne Klonpfad) mit `IssueReferenz` bzw. `PullRequestReferenz` an (`ScmE2EFixture.cs:46-90`) — die Zahlen-Map `TaskIds` steuert Szenarien.

**E2E-Laufzeit-Infrastruktur in der App** (`src/Softwareschmiede.App/Services/Testing/`):

- `ScmE2ETestConfiguration` (Env `SOFTWARESCHMIEDE_SCM_TEST_CONFIG` → JSON-Datei; verlangt `SOFTWARESCHMIEDE_TEST_DB_PATH`): Pfade `scm-scenario.json`, `scm-responses.jsonl`, `scm-calls.jsonl`, `simulator/`.
- `ScmE2EPluginManager`: ersetzt `IPluginManager`; gibt genau ein `GitHubPlugin` mit `ScmE2EApiClient` (Datei-Fixture statt `gh`) und `ScmE2EGitRunner` zurück; KI-Plugins = nur KiSimulator (abschaltbar via `PluginAvailable`).
- `ScmE2ETerminalSessionFactory`, `ScmE2ERuntimeControl`, `ScmE2EGitRunner`.
- Aktivierung in `App.xaml.cs:395-402` (nur bei gesetzter Env-Variable); PollInterval wird auf 300 ms verkürzt.
- `KiSimulatorPlugin` (`plugins/Softwareschmiede.Plugin.KiSimulator/KiSimulatorPlugin.cs`): `--scm-exchange`-Parameter oder `SOFTWARESCHMIEDE_SCM_SIMULATOR_FIXTURE` schaltet das PowerShell-Simulatorprogramm an (`SCM_SIMULATOR_PROTOCOL_V1`, `SCM_READY_V1`, schreibt Prompts nach `prompts.jsonl`, bedient `.softwareschmiede-scm-exchange`); `SOFTWARESCHMIEDE_SCM_SIMULATOR=1`/`…_READONLY=1` werden vom Evaluator ausgewertet.

## 3. Relevante Env-Variablen

| Variable | Wirkung |
|----------|---------|
| `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` | Muss bei jedem `dotnet test` in dieser Sandbox gesetzt sein (14 ConPTY-E2E-Tests schlagen sonst mit Timeout fehl — Umgebungsproblem, kein Codebug). Nie für menschliche Läufe setzen. |
| `SOFTWARESCHMIEDE_SCM_TEST_CONFIG` | Aktiviert SCM-E2E-Fixture-DI in der App |
| `SOFTWARESCHMIEDE_TEST_DB_PATH` | Isolierte Test-Datenbank (Voraussetzung für SCM/Update-E2E) |
| `SOFTWARESCHMIEDE_UPDATE_TEST_CONFIG` | Update-E2E-Fixture |
| `SOFTWARESCHMIEDE_SCM_SIMULATOR` / `_FIXTURE` / `_READONLY` | Simulator-Kanalsteuerung (Produktionscode) |
| `CODEX_HOME`, `CLAUDE_CONFIG_DIR` | Vom `ScmCliAccessEvaluator` für Konfigurationspfade ausgewertet |

## 4. Konventionen und Regeln (aus CLAUDE.md + Repo)

- Vor Tests immer voller Build (kein `--no-build`); stabiler Lane `--filter "Category!=OsInterface"`; OS-Interface-Tests separat.
- `dotnet test` **nie** im Hintergrund starten (Build-Lock/Stop-Hook korruptiert `bin/`).
- FlaUI-E2E: möglichst **wenige, lange** Testmethoden auf derselben App-Instanz (Laufzeit, nicht Methodenzahl, ist die Beschränkung); temporäre Fokus-Runner während der Entwicklung erlaubt, danach Konsolidierung.
- Bei E2E-Timeouts zuerst das App-Log der Testinstanz prüfen (`src/Softwareschmiede.App/bin/<Config>/<TFM>/logs/softwareschmiede-*.log`), nicht vorschnell „kein interaktiver Desktop" annehmen.
- **Niemals `Softwareschmiede.App.exe` beenden** (Self-Hosting-Risiko); bei MSB3027-Build-Locks Teil-Build ohne App-Projekt (`dotnet build src/Softwareschmiede/Softwareschmiede.csproj`).
- Formatierung: `dotnet format Softwareschmiede.slnx --verify-no-changes` (pre-push-Hook spiegelt PR-CI).
- Plugin-Projekte liegen unter `plugins/` (nicht `src/`); Contracts in `src/Softwareschmiede.Plugin.Contracts` (Assembly-Name weicht vom Ordner ab — `Domain.Interfaces`/`Domain.ValueObjects`-Namespaces werden gemeinsam genutzt).
- Deutsche Sprache in UI-Texten, Kommentaren und Statuswerten; SCM-Status sind englische String-Keys (persistiert), deutsche Anzeige erst im ViewModel.

## 5. Wiederverwendbare Bausteine für die Umsetzung

- `IScmEventSource`/`IScmEventResponder`-Verträge + `GitHubPlugin`-Transport (gh api, typisierte Fehler, Pagination, Owner-Identität) — Repo-Enumeration kann darauf aufsetzen (neuer Vertrag oder neuer `ScmContentKind`-freier Read).
- `ScmEventStore.AcceptBatchAsync` + Snapshots/EventKey-Dedup + Checkpoint-Backoff — direkt wiederverwendbar, wenn `aufgabeId`-Auflösung/Anlage vorgeschaltet wird.
- `ScmEventOrchestrator`-Dispatch (Owner-/CanWrite-Reprüfung, CLI-Start, Lease, Wartegründe) — arbeitet bereits „aufgabenzentriert"; für neue Aufgaben muss nur die Aufgabe vor Dispatch sicher existieren.
- `AufgabeService.CreateFromIssueAsync`/`CreateFromPullRequestAsync` + `IsPullRequestLinkedAsync` als Dedup-Bausteine (Issue-Dedup fehlt servicenah — nur UI-HashSet).
- `ScmDispatchFixture`/`ScmE2EFixture` für neue Tests (neues Szenario „Ereignis ohne Aufgabe" gut abbildbar: Fixture müsste Aufgabenlos-Einträge in `scm-scenario.json` unterstützen bzw. der Monitor muss Repo-Texte ohne Aufgabe aufnehmen).
- `ScmTaskCoordinator.Shared`-Sperre garantiert, dass Aufgabenanlage+Start+Zustellung für dieselbe Aufgabe serialisiert werden kann.
