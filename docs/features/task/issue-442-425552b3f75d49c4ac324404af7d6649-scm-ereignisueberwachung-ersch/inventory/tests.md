# Tests — Bestandsaufnahme Issue #442

## Test-Ausgangszustand vor der Umsetzung

- **Zeitpunkt (mit Zeitzone):** Build 2026-11-21 ~15:04–15:05 (lokale Zeit, UTC+1); Testläufe 2026-11-21 ~15:05–15:10 lokale Zeit (≈ 14:04–14:10 UTC)
- **Branch und Commit-ID:** `task/issue-442-425552b3f75d49c4ac324404af7d6649-scm-ereignisueberwachung-ersch`, Commit `be3d21854950ea078ad4091deed58d655b4dc196`
- **Uncommittete Änderungen im getesteten Stand:** nur untracked Verzeichnis `docs/features/task/issue-442-…/` (die Ergebnisdateien dieser Bestandsaufnahme selbst); keine Code-/Teständerungen
- **Testumgebung und Runtime-/SDK-Versionen:** Windows, Git Bash (Agent-Sandbox); .NET SDK `11.0.100` (zusätzlich `10.0.404` installiert); Ziel-Frameworks `net11.0` (IntegrationTests), `net11.0-windows10.0.17763.0` (App/Tests); `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` bei jedem `dotnet test` gesetzt (Sandbox-Pflicht laut CLAUDE.md — betrifft nur ConPTY-E2E-Tests der OsInterface-Lane)
- **Ermittelte Testsuiten und Quellen der Testbefehle:** `CLAUDE.md` definiert die stabile Lane `dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category!=OsInterface"` und die separate, umgebungssensitive Lane `--filter "Category=OsInterface"` (E2E/FlaUI, ConPTY, Clipboard, echte Prozessstarts). `.github/workflows/pr-staging-ci.yml` (Zeilen 121–132, 217–226) und `staging-ci.yml`/`release.yml` fahren identisch **beide** Testprojekte (`Softwareschmiede.Tests` + `Softwareschmiede.IntegrationTests`) in beiden Lanes. Voraussetzung pro Projektregel: voller Build vor `dotnet test`, kein `--no-build`.

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| Build | `dotnet build Softwareschmiede.slnx` | Repo-Root | 0 | Build erfolgreich (0 Warnungen, 0 Fehler, ~33 s) | — | — | [build.log](test-results/build.log) |
| Regular Tests | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category!=OsInterface" --logger "trx;LogFileName=regular-tests.trx" --logger "console;verbosity=normal" --results-directory docs/features/task/issue-442-…/inventory/test-results` | Repo-Root | 0 | 2721 | 0 | 3 | [regular-tests.trx](test-results/regular-tests.trx) / [regular-tests.log](test-results/regular-tests.log) |
| Regular IntegrationTests | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj --filter "Category!=OsInterface" --logger "trx;LogFileName=regular-integration.trx" --logger "console;verbosity=normal" --results-directory docs/features/task/issue-442-…/inventory/test-results` | Repo-Root | 0 | 357 | 0 | 0 | [regular-integration.trx](test-results/regular-integration.trx) / [regular-integration.log](test-results/regular-integration.log) |

### Nachgewiesene bestehende Testfehler

**Keine.** In beiden ausgeführten Lanes (2721 + 357 ausgeführte Tests) ist kein Test fehlgeschlagen. Es existiert damit kein nachgewiesener Pre-existing-Fehler, auf den sich spätere Regressionsvergleiche stützen könnten — jeder Fehlschlag nach der Umsetzung ist als neu zu untersuchen.

### Übersprungene Tests (Regular-Lane)

| Test-ID | Suite / Dateipfad | Grund |
|---------|------------------|-------|
| `Softwareschmiede.Tests.Infrastructure.Services.SystemShutdownServiceTests.ResolveShutdownCommand_MacOS_RemainsUnchanged` | `src/Softwareschmiede.Tests/Infrastructure/Services/` | Plattformbedingt (nur macOS) |
| `Softwareschmiede.Tests.Infrastructure.Services.SystemShutdownServiceTests.ResolveShutdownCommand_Linux_RemainsUnchanged` | `src/Softwareschmiede.Tests/Infrastructure/Services/` | Plattformbedingt (nur Linux) |
| `Softwareschmiede.Tests.Application.Services.WorkingDirectoryOpenServiceTests.Open_OnNotWindows_ThrowsPlatformNotSupportedException` | `src/Softwareschmiede.Tests/Application/Services/` | Plattformbedingt (nur Nicht-Windows) |

### Testlücken und Ausführungsprobleme

- **`Category=OsInterface`-Lane nicht ausgeführt** (Umfang: 87 `OsInterface`-Markierungen an Tests in `Softwareschmiede.Tests`, 4 in `Softwareschmiede.IntegrationTests` — u. a. `E2E_*`-FlaUI-Tests, `E2E_ConPty*`, Clipboard-, Prozessstart-Tests). Grund: Umgebungssensitivität dieser Agent-Sandbox — ConPTY-Isolation nachweislich defekt und FlaUI-E2E benötigt eine interaktive Desktopsitzung (beides in `CLAUDE.md` dokumentiert). `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` würde die 14 ConPTY-Tests sauber überspringen, verbleibende E2E-Starts wären trotzdem Sandbox-abhängig. **Der Ausgangszustand dieser Lane ist unbekannt** — spätere OsInterface-Fehlschläge gelten nicht als preexisting, solange kein eigener dokumentierter Ausgangslauf existiert.
- Build-/Setup-/Infrastrukturfehler: keine aufgetreten (Build Exit 0).

## Testklassen

### `GitHubEventSourceTests` — `src/Softwareschmiede.Tests/Infrastructure/Plugins/GitHubEventSourceTests.cs`

Reale Providerlogik gegen `FakeGitHubEventApi` (Warteschlangen-Fake des `IGitHubEventApiClient`; jeder Transportaufruf muss eingeplant sein).

- `RepositoryContext_UsesStableIdsAndPermissions` (Theory, 4 Alias-Formen: `https://…​.git`, `git@…`, `ssh://…`, `owner/repo`) — `GetEventRepositoryAsync` liefert identisches `ScmEventRepository`; **erwartet aktuell, dass alle Requests `GET` sind** (2 REST-Calls: `Identity(api)` plant `user` + `repos/owner/repo`)
- `Failures_AreTypedAndSanitized` (Theory 401/403/429/503/404) — Status→`FailureKind`-Matrix inkl. `RetryAfterUtc` aus `Retry-After: 90`; 403 mit `Retry-After` → `RateLimit`
- `AccessFailures_ExplainTokenSettingsAndRecovery` — Wiederherstellungstexte 401/403
- `GraphErrors_RejectPartialData` — GraphQL `errors[]` → typisierte Exception
- `GetEventRepositoryAsync_SeparatesAccount_UsesAgentToken` — **`Assert.Equal(2, agent.Requests.Count)`**: pinnt die heutigen 2 REST-Calls je Auflösung fest (Fixture-Anpassung nötig bei GraphQL-Umstellung/Cache)
- `ChangedViewer_BlocksResponseBeforePost`, `ExistingReaction_IsReconciledAcrossAllPages`, `CommentTimeout_ReportsUncertain_AndReconcilesMarker`, `PullRequestResponse_PreservesOriginAndUsesJsonBody`, `IssueBodyUpdate_*` (6 Tests) — `ExecuteResponseAsync`-Revalidierung/Reconcile (profitieren vom `Identity`-Fixture = Auflösung)
- `CommentAttachments_*` (12 Tests) — Attachment-Pfad inkl. `NumericRepositoryId`-Nachladen (`CommentAttachments_MissingNumericId_ReloadsRepository`)
- `ReadEventsAsync_RoutesByReferenceAccount`, `Response_RoutesByReferenceAccount` — Konto-Routing User↔Agent-Client
- Weitere: Pagination/Zyklus-/Fremdziel-Schutz, Watermark/Revisions-Dedup, Enumeration, `ReadIssue`/`ReadPullRequest`, `HttpTransport_SendsJsonBodyAndBearerToken_InRequest`

### `GitHubHttpApiClientTests` — `src/Softwareschmiede.Tests/Infrastructure/Plugins/GitHubHttpApiClientTests.cs`

17 Tests des realen HTTP-Transports über `HttpMessageHandler`-Fake: Host→Basis-URL-Mapping (`SendAsync_MapsHostToCorrectBaseUrl`), `ValidateHost`-Abweisungen, Header/Token-Verhalten (`SendAsync_RequiredHeaders_BearerOnlyWithToken`, `SendAsync_TokenProviderIsEvaluatedPerRequest`, `SendAsync_TokenNeverAppearsInOutputs`), `SendAsync_AdjustResponseHeadersThrough` (reicht RateLimit-Header durch), `SendAsync_ErrorStatus_ReturnsStatusAndBody` (**keine Fehlerklassifikation im Client** — wichtig für Gate-Ansiedlung), Transport-/Timeout-Typisierung, `UploadAsync_*`. Kein Gate-/Cache-Test vorhanden.

### `ScmEventOrchestratorTests` — `src/Softwareschmiede.Tests/Application/Services/ScmEventOrchestratorTests.cs`

14 Tests des Dispatch-Pfads gegen `ScmDispatchFixture` (Moq-`IScmEventSource`); Setup `:25` mockt `GetEventRepositoryAsync` → liefert `fixture.Repository` (RepositoryId-Match, `CanWrite = true`).

- `MissingWriteAccess_ExplainsTokenSettingsBeforeCliStart` — CanWrite=false-Pfad (`:20`)
- Dispatch-/Warte-/Bestätigungsfälle (`Dispatch_ReactedStoredObservation_Waits`, `UnavailablePlugins_*`, `ConfirmReady_*`, `TrustDialog_*`, …)

### `ScmEventMonitoringServiceTests_RepositoryResolution` — `src/Softwareschmiede.IntegrationTests/Services/ScmEventMonitoringServiceTests_RepositoryResolution.cs`

- `Monitor_SeparatesAccount_InaccessibleRepository_DoesNotBlockMentionsOfOtherRepositories` — `Permanent`/`Permission`-Isolierung im Inbox-Modus (`IsInaccessible`)
- `Monitor_RepositoryResolutionNotFound_IsRetriedAfterBackoffInsteadOfBlockedForever` — `retryPermanent: true`-Backoff der `repository/*`-Checkpoints

### `ScmEventMonitoringServiceTests_RepoWide` — `src/Softwareschmiede.IntegrationTests/Services/ScmEventMonitoringServiceTests_RepoWide.cs`

9 Tests: repository-weite Enumeration/Dedup (`Monitor_RepeatedPolls_ProduceNoDuplicates`, `Monitor_EnumerationFailure_UsesBackoffWithoutBlockingLinkedReferences` `:176` — `GetEventRepositoryAsync`-Verify), Inaktiv-/Fremd-Provider-Filter (`:155`), Autoren-Trigger-Regeln.

### `ScmEventMonitoringServiceTests_Notifications` — `src/Softwareschmiede.IntegrationTests/Services/ScmEventMonitoringServiceTests_Notifications.cs`

8 Tests des Inbox-Modus (`SeparateAccount`): Mention→Inbox, Fremd-Notifications/Diagnose, Nicht-Mention-Ignorieren, Inbox-Failure/Watermark-Retry (`:135`), Host-Fremd-Node-ID (`:214`), Moduswechsel zurück zur Enumeration (`:252`); `GetEventRepositoryAsync`-Setups `:225/:266/:280`.

### `ScmIssuePreparationMonitoringServiceTests` — `src/Softwareschmiede.IntegrationTests/Services/ScmIssuePreparationMonitoringServiceTests.cs`

14 Tests: Baseline-Erstlauf, `PreparationMonitor_RepeatedCycle_NoDescriptionReread` (Read-Schonung je-Issue-Checkpoint), `PreparationMonitor_RepositoryResolution_UsesUseraccountContext` (`Verify` `UserAccount`, `:209`), `PreparationMonitor_AliasResolutionFailure_IsolatesRepository` (`:346`), Read-Fehler-Isolation je Issue, Moduswechsel-Dedup.

### `ScmBackmergeMonitoringServiceTests` — `src/Softwareschmiede.IntegrationTests/Services/ScmBackmergeMonitoringServiceTests.cs`

10 Tests: `Monitor_RepositoryResolution_UsesUseraccountContext` (`:110`, Verify `UserAccount`), Disabled-Schalter, Backmerge-Bewertung/Merge-Pfade, `Monitor_UnmergeableBackmerge_RetriesNextPoll`.

### `ScmTaskCompletionMonitoringServiceTests` — `src/Softwareschmiede.IntegrationTests/Services/ScmTaskCompletionMonitoringServiceTests.cs`

15 Tests: `CompletionService_IssueCheck_UsesUseraccountContext` (`:163` — Issue-Pfad via `GetEventRepositoryAsync`+`ReadIssueAsync`), PR-Status-Pfad (ohne Auflösung), Wartezeit-/Countdown-Logik (eigener `FakeTimeProvider` `:525`), CheckMode-Varianten.

### Weitere betroffene Testdateien

- `ScmEventMonitoringServiceTests_Settings` / `_TaskAssignment` / `_AmbiguityRecovery` / `_AutoStartDelivery` / `_IssuePreparation` / `ScmEventPersistenceTests` / `ScmEventDispatchTests*` (15 Dateien) / `ScmEventDelayIntakeTests` / `ScmEventCliExchangeAccessTests` — laufen alle über `ScmDispatchFixture` bzw. denselben Auflösungspfad.
- `ScmEventWireCompatibilityTests`, `ScmEventValidationServiceTests`, `AppSettingServiceTests_ScmMonitoring`, `ScmEventQueueViewModelTests`, `GitHubNotificationSourceTests`, `GitHubSinceWindowTests`, `GitHubPluginTests*` — angrenzende Einheiten, nicht direkt betroffen.

## Hilfsmethoden

### `FakeGitHubEventApi` — `src/Softwareschmiede.Tests/Helpers/FakeGitHubEventApi.cs`

- `Add(endpoint, body, headers?)` / `AddJson` / `AddGraph(data)` — REST-/GraphQL-Antworten einplanen (Endpoint-Fragment-Match, FIFO)
- `AddStatus(endpoint, status, headers?)` — Fehlerstatus mit `{"message":"secret"}`-Body (Sanitizing-Nachweis)
- `AddFailure(endpoint, exception)` — Transportausnahme
- `AddUpload*` — eigene Upload-Warteschlange
- `SendAsync`/`UploadAsync` — dequeeue + `Assert.Contains`-Endpoint-Prüfung; `Requests`/`UploadRequests` zeichnen alle Aufrufe auf; `AssertComplete()` verlangt leere Warteschlangen
- `GitHubEventSourceTests.Identity(api, viewer, numericId)` — plant genau die **zwei** Identitäts-Calls (`user` + `repos/owner/repo`)

### `ScmDispatchFixture` — `src/Softwareschmiede.Tests/Helpers/ScmDispatchFixture.cs` (in IntegrationTests per `<Compile Link>` eingebunden)

- Konstruktor `:63–198`: Moq-`IGitPlugin` mit `IScmEventSource`/`IScmEventResponder`/`IScmAgentAccess`/`IScmNotificationSource`; **`GetEventRepositoryAsync` → `ReturnsAsync(Repository)` (`:73`)** — immer erfolgreiche Auflösung, `Repository` = `ScmEventRepository("test-scm", "github.com", "R_1", "owner", "repo", "U_1", "U_1", "owner", CanWrite: true)` (`:52`), `BotRepository` für `SeparateAccount` (`:55`)
- `AddAsync` — Projekt+Repo+Aufgabe+Ereignis über `ScmEventStore.AcceptBatchAsync` anlegen
- `AddNotification`, `ConfigureIssue`, `ConfigureMentionObservations` — Inbox-/Quellen-Stubs
- `Monitor`/`Trigger`/`StartCheck`/`Cli`/`DispatchAsync`/`EventAsync` — Servicezugriff; echtes SQLite pro Fixture (`events.db` im Temp-Root), `ScmEventStartupGate.Ready()` in `InitializeAsync`
