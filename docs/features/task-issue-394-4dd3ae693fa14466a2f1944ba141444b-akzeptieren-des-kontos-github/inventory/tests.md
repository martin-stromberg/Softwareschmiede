# Tests — Bestandsaufnahme

## Test-Ausgangszustand vor der Umsetzung

- Zeitpunkt (mit Zeitzone): 2026-11-15, ca. 20:09–20:14 Uhr +01:00 (W. Europe Standard Time)
- Branch und Commit-ID: `task/issue-394-4dd3ae693fa14466a2f1944ba141444b-akzeptieren-des-kontos-github` @ `3bc325c9a71e28d0eda03100cb37fd8141627a7a` („SHA-256-Verifikation des Update-Pakets vor dem Entpacken (aus #374) (#392)")
- Uncommittete Änderungen im getesteten Stand: ausschließlich das unversionierte Feature-Verzeichnis `docs/features/task-issue-394-4dd3ae693fa14466a2f1944ba141444b-akzeptieren-des-kontos-github/` (requirement.md, inventory-Artefakte); kein Produktiv- oder Testcode geändert
- Testumgebung und Runtime-/SDK-Versionen: Windows-Sandbox (agentischer Lauf, nicht-interaktiv); .NET SDK `10.0.403`, Laufzeit `.NET 10.0.15`, xUnit.net VSTest Adapter `4.0.1`; `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` gesetzt (sandbox-bedingter ConPTY-Skip gemäß Projektregeeln); Debug-Konfiguration
- Ermittelte Testsuiten und Quellen der Testbefehle: zwei Testprojekte (`src/Softwareschmiede.Tests`, `src/Softwareschmiede.IntegrationTests`), je zwei Lanes aus den CI-Workflows (`.github/workflows/pr-staging-ci.yml`, `staging-ci.yml`, `release.yml`): reguläre Lane `--filter "Category!=OsInterface"` (stabile Lane laut `CLAUDE.md`) und OS-Interface-Lane `--filter "Category=OsInterface"` (separat, umgebungssensitiv: WPF-E2E via FlaUI, ConPTY, Clipboard, echte Prozessstarts). Voll-Build vorab: `dotnet build Softwareschmiede.slnx` → 0 Warnungen, 0 Fehler (Exit 0).

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| Unit/Regular | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category!=OsInterface" --logger "console;verbosity=normal"` | Repo-Root | 0 | 2446 | 0 | 3 | [unit-tests-regular.log](test-results/unit-tests-regular.log) |
| Integration/Regular | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj --filter "Category!=OsInterface" --logger "console;verbosity=normal"` | Repo-Root | 0 | 280 | 0 | 0 | [integration-tests-regular.log](test-results/integration-tests-regular.log) |

### Nachgewiesene bestehende Testfehler

Keine. Beide regulären Lanes liefen ohne Fehlschlag durch; über nicht ausgeführte Lanes wird keine Aussage getroffen (siehe unten).

Übersprungene Tests im Unit-Lauf (alle plattformbedingt, begründet im Log):
- `SystemShutdownServiceTests.ResolveShutdownCommand_Linux_BleibtUnveraendert` — Nicht-Windows-Test
- `SystemShutdownServiceTests.ResolveShutdownCommand_MacOS_BleibtUnveraendert` — Nicht-Windows-Test
- `ArbeitsverzeichnisOeffnenServiceTests.Oeffne_AufNichtWindows_WirftPlatformNotSupportedException` — Nicht-Windows-Test

### Testlücken und Ausführungsprobleme

- **`Category=OsInterface`-Lane nicht ausgeführt** (beide Projekte; in `src/Softwareschmiede.Tests` ca. 45 Quelldateien u. a. `E2E/` mit ~53 FlaUI-E2E-Dateien sowie Terminal-/Prozess-Tests, in `src/Softwareschmiede.IntegrationTests` die Datei `Scripts/StartPs1IntegrationTests.cs`). Grund: Die Lane ist laut Projektdokumentation die separate, umgebungssensitive Lane — WPF-E2E-Tests starten echte App-Fenster und setzen eine interaktive Windows-Desktop-Session voraus (ausdrücklich im Dateikopf der E2E-Tests, z. B. `src/Softwareschmiede.Tests/E2E/E2E_ConPtyLifecycle.cs`, vermerkt); die ConPTY-Teilmenge wäre in dieser Sandbox ohnehin nur über `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` als „Skipped" lauffähig. Ausgangszustand dieser Lane ist damit **unbekannt** — spätere Fehlschläge darin dürfen nicht als „preexisting" behauptet werden. Für die anforderungsbetroffene Backend-Validierungslogik ist die Lane nicht maßgeblich (kein UI-Verhalten geändert).
- Kein Build-, Setup- oder Infrastrukturfehler aufgetreten.

## Testklassen

### `ScmEventValidationServiceTests` (`src/Softwareschmiede.Tests/Application/Services/ScmEventValidationServiceTests.cs`)

Direkte Unit-Tests von `GetRejection` ohne Provideraufrufe. Die privaten Fabrikmethoden `Observe(...)` (Z. 183–190, Modus `Benutzerkonto`, Owner `"42"`/`"owner"`) und `ObserveSeparate(...)` (Z. 192–201, Modus `SeparatesKonto`, Viewer `"bot-id"`/`"scm-bot"`, Owner `"42"`/`"OWNER"`) bauen die Beobachtungen; Autor/Bearbeiter sind je Fall parametrisierbar — die natürliche Erweiterungsstelle für `github-actions`-Fälle.

- `ActualAuthorOrEditorMustBeOwner` — Akteur (Autor bzw. Bearbeiter) muss dem Owner entsprechen; Fremd-/Leer-Identitäten scheitern
- `TriggerMustBeFreeStanding` — Keyword nur frei stehend, nicht zitiert
- `ConfiguredTriggerKeyword_ReplacesDefaultAndStaysFreeStanding` / `MissingTriggerKeyword_FallsBackToDefault` / `TriggerKeyword_IsTrimmedBeforeMatching` — konfigurierbares Keyword
- `LoginCannotOverrideDifferentIds` — IDs haben Vorrang vor ähnlich geschriebenen Logins
- `MetadataDoesNotCreateAnotherVersionAndContentNamespacesDo` — `EventKey`-Stabilität
- `SeparatesKonto_MentionReplacesKeywordTrigger` — `@`-Erwähnung statt Keyword im SeparatesKonto
- `SeparatesKonto_AuthorOrEditorMustBeOwner_NotViewer` — Owner-Prüfung gegen `OwnerId`/`Owner`, nicht gegen den Bot-Viewer (`bot-id` als Autor wird abgelehnt — engster bestehender Fall zur `github-actions`-Ablehnung)
- `SeparatesKonto_ViewerOwnerRuleDoesNotApply` — Viewer-Owner-Regel entfällt im SeparatesKonto
- `SeparatesKonto_OwnResponseMarkerStillRejected` — Marker-Regel „Eigene Antwort"
- `Benutzerkonto_KeywordStaysTheOnlyTrigger` — Regression: `@`-Mention triggert nicht im Benutzerkonto
- `ViewerReaction_RejectsCommentKinds` / `SeparatesKonto_ViewerReaction_Rejects` / `ViewerReaction_CheckRunsAfterTrigger` / `ViewerReaction_DescriptionNotRejected` — `BereitsReagiertRejection`-Regel

### `GitHubEventSourceTests` (`src/Softwareschmiede.Tests/Infrastructure/Plugins/GitHubEventSourceTests.cs`)

Plugin-Lesepfad mit ausgetauschtem API-Transport (`FakeApi : FakeGitHubEventApi`). Helfer `Node(id, body, edited, editorId, editorLogin, ...)` (Z. 624–637) baut GraphQL-Knoten mit `author { id login }` — für Bot-Autoren direkt wiederverwendbar.

- `Comments_ReadAllPages_AndBodyEditorFromSameVersion` — Autor/Editor landen in `AuthorId`/`EditorId`
- `MissingEditor_RemainsMissing_AndSameVersionCanResolveLater` — leerer Editor, spätere Auflösung
- `OwnMarkedComment_IsExcluded_ButForeignMarkerIsNotTrusted` — Viewer-Filter in `AddObservation`
- `ViewerReactions_*`, `ReactionGroups_RequestedInQuery`, `MissingReactionGroups_ThrowsPermanent` — Reaktionspfad
- `Description_VersionsDistinguishTextAndEdits_NotMetadata`, `Watermark_ExcludesNewerVersion`, `Reviews_PaginateAndExcludePending`, `PullRequestCommentKinds_UseSeparateNamespaces`, `EventKey_SeparatesEveryOriginDimension` — Lesepfad-/Dedup-Verhalten
- `RepositoryContext_UsesStableIdsAndPermissions`, `Failures_*`, `AccessFailures_*`, `ExistingReaction_*`, `CommentTimeout_*`, `ChangedViewer_*` — Identitäts- und Antwortpfad

### `ScmEventOrchestratorTests` (`src/Softwareschmiede.Tests/Application/Services/ScmEventOrchestratorTests.cs`)

Dispatch-Revalidierung gegen persistierte Ereignisse mit `ScmDispatchFixture` — u. a. `Dispatch_ReactedStoredObservation_Waits` (gespeicherte Beobachtung mit `ViewerReactions` parkt beim Dispatch). Die manuell gebaute Beobachtung (Z. 54–56) zeigt das Muster für weitere Revalidierungsfälle.

### Integrations-Testklassen (`src/Softwareschmiede.IntegrationTests/Services/`)

- `ScmEventDispatchTests` (+ `*_AgentActions`, `*_Cancel`, `*_Devin`, `*_HostRetry`, `*_PromptAppendix`) — End-to-End Zustellpfad über `ScmDispatchFixture`; Helfer `ConfigureObservations` (Z. 190–197) liefert Owner-Autor `"U_1"`/`"owner"`
- `ScmEventMonitoringServiceTests_RepoWide` / `*_RepositoryResolution` / `*_TaskAssignment` / `*_Settings` / `*_AmbiguityRecovery` / `*_AutoStartDelivery` — Poll-/Enumerationspfade mit eigener `ConfigureObservations`-Variante, jeweils Owner-Autor
- `ScmEventMonitoringServiceTests_Notifications` — `SeparatesKonto`-Inbox-Pfad; `BotRepository` (Z. 18–19, Viewer `"BOT"`/`"scm-bot"`), `ConfigureBotResolution`, `ConfigureMentionObservations` (Z. 268–276, `@scm-bot`-Trigger mit Owner-Autor `"U_1"`)
- `ScmEventPersistenceTests` — `AcceptBatchAsync` gegen echte SQLite-DB; `ObservationJson_WithoutViewerReactions_Deserializes` ruft `GetRejection` auf Altbestand (Z. 70); `MissingEditorCanResolveLaterWithoutAcceptingForeignEditor` (Z. 74–88) zeigt den Wiederanlauf für nicht bestimmbare/veränderte Akteure

## Hilfsmethoden

### `ScmDispatchFixture` (`src/Softwareschmiede.Tests/Helpers/ScmDispatchFixture.cs`)

- `Repository` (Z. 47) — `ScmEventRepository("test-scm", "github.com", "R_1", "owner", "repo", "U_1", "U_1", "owner", true)` (Owner = Viewer = `U_1`/`owner`, Modus `Benutzerkonto`)
- `AddAsync(...)` (Z. 168–188) — legt Projekt/Repository/Aufgabe + Beobachtung mit Autor `"U_1"`/`"owner"` an und speist `AcceptBatchAsync`; Parameter `eventRepository` erlaubt abweichende Kontexte
- `Git` (Moq `IGitPlugin` als `IScmEventSource`/`IScmEventResponder`/`IScmAgentAccess`/`IScmNotificationSource`) — `ReadEventsAsync`-Setup je Test überschreibbar; `Responses`, `Prompts`, `Sessions`, `Notifications` als Beobachtungspunkte
- `DispatchAsync`, `EventAsync`, `AddNotification`, `SetPrompt`, `CompleteProbeAsync` — Dispatch-/Session-Hilfen

### Testbeobachtungs-Helfer

- `ScmEventValidationServiceTests.Observe`/`ObserveSeparate` — parametrische Beobachtungen (Body, Autor-ID, Editor-ID, Edit-Flag, Reaktionen, `ContentKind`)
- `GitHubEventSourceTests.Node`/`Identity`/`Read` — GraphQL-Antwortknoten inkl. `author`/`editor`/`reactionGroups`
- `ScmEventMonitoringServiceTests_Notifications.BotRepository`/`ConfigureBotResolution`/`ConfigureMentionObservations` — `SeparatesKonto`-Kontext mit Bot-Viewer

**Feststellung:** Es existiert kein Testfall mit `github-actions`-Autor oder -Bearbeiter; sämtliche Beobachtungs-Helfer setzen den Owner (`U_1`/`owner` bzw. `42`/`owner`) als Autor. Die Ablehnung von Fremd-Identitäten ist über `SeparatesKonto_AuthorOrEditorMustBeOwner_NotViewer` (`bot-id`) und `LoginCannotOverrideDifferentIds` (`other`) indirekt abgesichert.
