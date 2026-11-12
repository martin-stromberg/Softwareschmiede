# Bestandsaufnahme: Tests (SCM-Ereignisverarbeitung)

## Test-Ausgangszustand vor der Umsetzung

- **Zeitpunkt (mit Zeitzone):** 2026-11-12, ~05:58–06:23 +01:00 (Mitteleuropäische Zeit)
- **Branch und Commit-ID:** `task/issue-350-7ed6351959b149278ae3413cba417f78-mehrinstanzfaehig` @ `6b6eac1a4e3faa78794e74057b0007f800e0e242` (2026-11-12 05:09:26 +0100)
- **Uncommittete Änderungen im getesteten Stand:** ausschließlich untracked — `docs/features/task/issue-350-…/` (requirement.md, diese Bestandsaufnahme inkl. TRX-Nachweise). Keine Änderungen an `src/` oder `plugins/`.
- **Testumgebung und Runtime-/SDK-Versionen:** Windows 11 (10.0.26300), .NET SDK 10.0.403, .NET Runtime 10.0.15, xUnit.net VSTest Adapter 3.1.5 + Xunit.SkippableFact. Interaktive Desktop-Session (`UserInteractive=True`) — WPF-E2E-Tests laufen in dieser Umgebung tatsächlich. `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` war bei allen vier `dotnet test`-Läufen gesetzt (Sandbox-Auflage gemäß `CLAUDE.md`).
- **Vorbereitung:** `dotnet build Softwareschmiede.slnx -c Debug` — erfolgreich, 0 Warnungen, 0 Fehler (vor dem ersten Testlauf, ~15 s).
- **Ermittelte Testsuiten und Quellen der Testbefehle:** Zwei Testprojekte (`src/Softwareschmiede.Tests`, `src/Softwareschmiede.IntegrationTests`), jeweils mit `Category!=OsInterface` (stabile Lane) und `Category=OsInterface` (E2E/FlaUI, ConPTY, Zwischenablage, echte Prozessstarts). Quellen: `CLAUDE.md` (Abschnitt Testing) und `.github/workflows/pr-staging-ci.yml`/`staging-ci.yml` (identische Lanes).

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| Build | `dotnet build Softwareschmiede.slnx -c Debug` | Repo-Root | 0 | — | — | — | Konsolen-Ausgabe (0 Warnungen, 0 Fehler) |
| Regular Tests | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj -c Debug --filter "Category!=OsInterface" --logger "trx;LogFileName=baseline-tests-regular.trx"` | Repo-Root | 1 | 2276 | 1 | 1 | [baseline-tests-regular.trx](test-results/baseline-tests-regular.trx) |
| Regular IntegrationTests | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj -c Debug --filter "Category!=OsInterface" --logger "trx;LogFileName=baseline-integration-regular.trx"` | Repo-Root | 0 | 276 | 0 | 0 | [baseline-integration-regular.trx](test-results/baseline-integration-regular.trx) |
| OsInterface Tests | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj -c Debug --filter "Category=OsInterface" --logger "trx;LogFileName=baseline-tests-osinterface.trx"` | Repo-Root | 1 | 64 | 1 | 5 | [baseline-tests-osinterface.trx](test-results/baseline-tests-osinterface.trx) |
| OsInterface IntegrationTests | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj -c Debug --filter "Category=OsInterface" --logger "trx;LogFileName=baseline-integration-osinterface.trx"` | Repo-Root | 0 | 9 | 0 | 0 | [baseline-integration-osinterface.trx](test-results/baseline-integration-osinterface.trx) |

Summe ausgeführter Läufe: 2625 bestanden, 2 fehlgeschlagen, 6 übersprungen (von 2633).

### Nachgewiesene bestehende Testfehler

Beide Fehlschläge sind Zwischenablage-Tests in `TerminalControlTests` und stehen in keinem Bezug zur SCM-Ereignisverarbeitung.

| Test-ID inkl. Testfall | Suite / Dateipfad | Fehlerbild / Fehlermeldung | Lauf und Nachweis |
|-----------------------|------------------|---------------------------|-------------------|
| `Softwareschmiede.Tests.App.Controls.TerminalControlTests.OnPreviewKeyDown_MausankerDannShiftRechts_ZeichnetAuswahlUndKopiertText` | `src/Softwareschmiede.Tests/App/Controls/TerminalControlTests.KeyInput.cs` (~Z. 107–129) | `Expected System.Windows.Clipboard.GetText() to be "er" with a length of 2, but "" has a length of 0` — STA-Unit-Test kopiert in die echte WPF-Zwischenablage; `GetText()` lieferte leer (Zwischenablage-Race in dieser Umgebung) | Regular Tests; [baseline-tests-regular.trx](test-results/baseline-tests-regular.trx) |
| `Softwareschmiede.Tests.App.Controls.TerminalControlTests.ReadClipboardAndInsertAsync_ClipboardAccessThrows_LogsWarningAndContinues` | `src/Softwareschmiede.Tests/App/Controls/TerminalControlTests.ClipboardPaste.cs` (Z. 203–231) | `Moq.MockException`: erwartete Warning-Log-Invokation nie ausgeführt. Der Test setzt `SetClipboardTextWithRetry("paste-me")` voraus; bei nicht befüllbarer Zwischenablage ist der Paste-Pfad leer und es gibt nichts zu loggen — gleiche Ursachenfamilie wie der erste Fehlschlag (echte Systemzwischenablage verhält sich in dieser Sandbox abweichend) | OsInterface Tests; [baseline-tests-osinterface.trx](test-results/baseline-tests-osinterface.trx) |

### Testlücken und Ausführungsprobleme

Übersprungen (kein Erfolg, kein Fehlschlag):

- `End2EndTest.TerminalText_LiveMarkCopyAndKeepSelection_E2E`, `End2EndTest.ConPty_SessionReattach_KeineDoppelausgabe_E2E`, `End2EndTest.ConPty_WeitereLebenszyklusSzenarien_E2E`, `End2EndTest.ConPty_LifecycleUndReplay_E2E`, `E2E_RepositoryInitialisierungAusfuehrungTests.InitialisierungsskriptAusfuehrung` — alle via `SkipWennConPtyNichtVerfuegbar()` wegen `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` (dokumentierte Sandbox-Limitation: ConPTY-Kindprozess wird nicht in die Pseudo-Konsole isoliert, `CLAUDE.md`).
- `ArbeitsverzeichnisOeffnenServiceTests.Oeffne_AufNichtWindows_WirftPlatformNotSupportedException` (reguläre Lane) — `Skip.If`: gilt nur für Nicht-Windows; Umgebung ist Windows. Erwartbar.

Relevante Abdeckungslücke für die Anforderung:

- **Kein Testmaterial für Viewer-Reaktionen beim Lesen:** `FakeGitHubEventApi`-Antworten und das `ScmE2EApiClient`-`Node()`-Objekt (`ScmE2EApiClient.cs` Z. 154–165) liefern keine `reactionGroups`-Felder; `ScmE2EText`/`ScmE2EScenario` haben keine Reaktionseigenschaft. Ein „Kommentar mit vorhandener Reaktion des Ereigniskontos" ist derzeit weder im Unit-Test-Fake noch im E2E-Szenario darstellbar.
- Reaktionslektüre existiert nur auf dem Antwortpfad (`ReconcileResponseAsync`/`ReconcileReviewReactionAsync`), nicht beim Beobachten (`ReadEventsAsync`).

## Testklassen (bereichsrelevant)

### `ScmEventValidationServiceTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/ScmEventValidationServiceTests.cs`

- `ActualAuthorOrEditorMustBeOwner` — Bearbeiter-Identität (Autor/Editor) gegen Owner
- `TriggerMustBeFreeStanding` — Trigger nur frei stehend, nicht zitiert
- `ConfiguredTriggerKeyword_ReplacesDefaultAndStaysFreeStanding` / `MissingTriggerKeyword_FallsBackToDefault` / `TriggerKeyword_IsTrimmedBeforeMatching` — konfiguriertes Keyword
- `LoginCannotOverrideDifferentIds` — IDs schlagen Logins
- `MetadataDoesNotCreateAnotherVersionAndContentNamespacesDo` — `EventKey`-Semantik
- `SeparatesKonto_MentionReplacesKeywordTrigger` / `SeparatesKonto_AuthorOrEditorMustBeOwner_NotViewer` / `SeparatesKonto_ViewerOwnerRuleDoesNotApply` / `SeparatesKonto_OwnResponseMarkerStillRejected` / `Benutzerkonto_KeywordStaysTheOnlyTrigger` — Kontomodus-Regeln inkl. `KeineErwaehnungRejection`-Konstante
- Hilfsmethoden: `Observe(body, author, editor, edited)` (Benutzerkonto-Observation), `ObserveSeparate(...)` (SeparatesKonto) — konstruieren `ScmEventObservation` positional; eine neue Record-Eigenschaft wirkt auf beide.

### `GitHubEventSourceTests`
Datei: `src/Softwareschmiede.Tests/Infrastructure/Plugins/GitHubEventSourceTests.cs`

- `Comments_ReadAllPages_AndBodyEditorFromSameVersion` — REST-Liste + je Kommentar GraphQL-`node`, Editor aus demselben Snapshot
- `FailedSecondPage_ThrowsWithoutGraphOrPartialBatch` / `Pagination_RejectsForeignTargets` — Fehler-/Sicherheitssemantik des Lesepfads
- `Description_VersionsDistinguishTextAndEdits_NotMetadata` / `MissingEditor_RemainsMissing_AndSameVersionCanResolveLater` / `Watermark_ExcludesNewerVersion` — Versions-/Watermark-Semantik
- `Reviews_PaginateAndExcludePending` — `PullRequestReview`-Quelle
- `PullRequestCommentKinds_UseSeparateNamespaces` — `IssueComment` vs. `ReviewComment`
- `OwnMarkedComment_IsExcluded_ButForeignMarkerIsNotTrusted` — `IsViewer`-Eigenantwortfilter in `AddObservation` (Vorbild für ein mögliches Verwerfen beim Lesen)
- `EventKey_SeparatesEveryOriginDimension` — Dedup-Schlüssel
- `ExistingReaction_IsReconciledAcrossAllPages` / `CommentTimeout_ReportsUncertain_AndReconcilesMarker` / `ReplyPermissionFailure_IsNotUncertain` / `ChangedViewer_BlocksResponseBeforePost` / `PullRequestResponse_PreservesOriginAndUsesJsonBody` — Antwortpfad (u. a. Viewer-Reaktionsabgleich über REST `/reactions`)
- `Enumeration_*` — Referenz-Enumeration
- `GetEventRepositoryAsync_SeparatesKonto_UsesAgentToken` / `ReadEventsAsync_RoutesByReferenceKonto` / `Response_RoutesByReferenceKonto` — `ClientFor`-Routing (relevant: `viewerHasReacted` bindet an das Konto des Requests)
- `ReadIssue_*` / `ReadPullRequest_*` / `HttpTransport_*` / `Failures_*` / `GraphErrors_*`
- Hilfsmethoden: `Plugin(api)`/`Plugin(api, store, agent)`, `Identity`, `Node(id, body, edited, editorId, editorLogin, updated)` (baut GraphQL-Textknoten — hier kämen `reactionGroups`-Testdaten hinzu), `Review`, `Reviews`, `Ref`, `Page`, `Read`

### `GitHubNotificationSourceTests`
Datei: `src/Softwareschmiede.Tests/Infrastructure/Plugins/GitHubNotificationSourceTests.cs`

- Inbox-Pfad SeparatesKonto: `EnumerateNotifications_MapsIssueAndPullRequestThreads`, `_UsesAgentClientOnly`, `_SkipsNonReferenceSubjects`, `_PassesSinceParameter`, `_ReadAllPages`, Host-Validierung, typisierte Fehler.

### `ScmEventOrchestratorTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/ScmEventOrchestratorTests.cs`

- `MissingWriteAccess_ExplainsTokenSettingsBeforeCliStart` — `GetEventRepositoryAsync`-Revalidierung im Dispatch
- `NoDefault_ActivatesTaskBeforeWaitingForSelection`, `SettingsDefaultCli_StartsTaskAndDelivers`, `UnresolvableSettingsDefaultCli_KeepsWaitingState`, `BlockedTask_IsNeverActivated`
- `Confirmation_*`, `TrustDialog_*`, `ConfirmReady_*`, `UnknownCliFormat_*` — Bestätigungs-/Bereitschaftspfade
- Nutzt `ScmDispatchFixture` (echte SQLite + gemockte `IScmEventSource`/`IScmEventResponder`).

### `ScmEventMonitoringServiceTests_*` (IntegrationTests, echte SQLite + Moq-Quelle)
Dateien: `src/Softwareschmiede.IntegrationTests/Services/ScmEventMonitoringServiceTests_{RepoWide,TaskAssignment,Notifications,Settings,AutoStartDelivery,RepositoryResolution,AmbiguityRecovery}.cs`

- `_RepoWide`: `Monitor_ActiveRepositoryWithoutTask_EnumeratesAndStoresSnapshotsWithoutInbox`, `Monitor_EnumeratedReferenceWithTask_EntersInboxUnchanged`, `Monitor_RepeatedPolls_ProduceNoDuplicates`, `Monitor_InactiveOrNonGitHubRepositories_AreNotEnumerated`, `Monitor_EnumerationFailure_UsesBackoffWithoutBlockingLinkedReferences` — **direkte Nachbarn für „kein Inbox-Eintrag bei Kontoreaktion"**; `ConfigureObservations` (Z. 203–211) baut die `ReadEventsAsync`-Antwort.
- `_TaskAssignment`: `Monitor_QualifiedCommentOnUnassignedIssue_CreatesTaskAndInboxEntry`, `Monitor_UnqualifiedEvent_KeepsUnassignedWithoutTask`, PR-Zuordnungsvarianten (Branch, Closing-Issue, Fork, Pause, Mehrdeutigkeit).
- `_Notifications`: SeparatesKonto-Inbox inkl. Verwerfungsdiagnose, Watermark-Verhalten bei Fehlern, Moduswechsel.
- `_Settings`: Monitoring-Schalter, `Monitor_CustomTriggerKeyword_AcceptsNewAndRejectsOld`, manueller Retry bei deaktivierter Überwachung.
- `_AutoStartDelivery`: Zustellung an konfigurierte/Standard-CLI, Queue-Auswahl, gesperrte Aufgaben.
- `_RepositoryResolution`/`_AmbiguityRecovery`: Repository-Backoff und Alias-Wiederherstellung.

### `ScmEventPersistenceTests` (IntegrationTests)
Datei: `src/Softwareschmiede.IntegrationTests/Services/ScmEventPersistenceTests.cs`

- `MigrationAndReplayPreserveOldTasksAndObserveVersions` — migrierte DB + `AcceptBatchAsync`-Verhalten (berührt `ObservationJson`-Deserialisierung: relevante Regressionsstelle für eine neue Record-Eigenschaft)
- `MissingEditorCanResolveLaterWithoutAcceptingForeignEditor`, `AtomicLeaseExcludesOtherConsumers`

### `ScmEventDispatchTests*` (IntegrationTests)
Dateien: `src/Softwareschmiede.IntegrationTests/Services/ScmEventDispatchTests{,_AgentActions,_Cancel,_Devin,_HostRetry,_PromptAppendix}.cs`

- Zustellungs-, Abbruch-, Retry- und Remote-Aktionspfade über `ScmDispatchFixture` (u. a. `Receipt`-/`waiting`-Antworten werden in `fixture.Responses` als `ScmResponseRequest` mitprotokolliert — prüfbar, ob eine receipt-Reaktion trotz abgelehnter Beobachtung versucht würde: im Ablehnungspfad existiert gar kein Inbox-Eintrag, also auch keine `receipt`-Aktion).

### `ScmEventsE2ETests` (E2E, FlaUI, `[OsInterface]`, `[Collection("E2E")]`)
Datei: `src/Softwareschmiede.Tests/E2E/E2E_ScmEvents.cs`

- `ScmEvents_QueueReceptionDispatchAndReplies_UseRealPipeline` — **konsolidierter SCM-E2E-Test** (eine Methode, ~14 Phasen): Empfang/Inbox, `eyes`-Receipt + `waiting`-Kommentar, Restart-Erhalt, Dialog/CLI-Auswahl, Zustellung/Annahme/Abschluss, Folgeereignis, Filter, gesperrte/pausierte Aufgaben, Startfehler-Retry, Issue-/PR-/Description-Varianten inkl. Receipt-Assertions (`Assert.Equal(receiptCount + (kind == "Description" ? 0 : 1), …)` Z. 163), 403-Wiederaufnahme, Bereitschaftsbestätigung, Kanalwiederherstellung, 401/403-Retry, fehlende Varianten, ungewisse/verspätete Zustellung, Aufgabenanlage (Issue 20). **Baseline: bestanden** (~16-Minuten-Lane).
- Fixture: `ScmE2EFixture` (`src/Softwareschmiede.Tests/E2E/ScmE2EFixture.cs`) — `AddText` (Szenario-Text), `AddNotification`, `SetKontoModus`, `WaitEventAsync`/`WaitEventForTaskAsync`, `WaitForPollAsync`/`WaitForCallsAsync`, `Responses()` (gepostete Remoteaktionen), `Prompts()`, `Hold`/`Control` (Simulator-Gates), `OpenDatabase`.
- Fixture-Transport: `ScmE2EApiClient` (`src/Softwareschmiede.App/Services/Testing/ScmE2EApiClient.cs`) — GitHub-Parser bleibt produktiv; GraphQL-`node`-Antwort über `Node(ScmE2EText)` (Z. 154–165) ohne `reactionGroups`; `Remote()` stempelt gepostete Antworten mit der lesenden Konto-Identität; `ScmE2EPluginManager` verdrahtet zwei Fixture-Identitäten (`scm-owner`/`OWNER` und `scm-bot`/`BOT`).

## Hilfsmethoden

### `ScmDispatchFixture`
Datei: `src/Softwareschmiede.Tests/Helpers/ScmDispatchFixture.cs`

- `AddAsync(...)` — Aufgabe+Repository anlegen und eine `ScmEventObservation` über `AcceptBatchAsync` einspielen (positionaler Record-Aufruf, Z. 184)
- `AddNotification`, `DispatchAsync`, `EventAsync`, `CompleteProbeAsync`, `SetPrompt`, `CloneGate`
- Gemockte Flächen: `Git.As<IScmEventSource>()` (`ReadEventsAsync` liefert standardmäßig leeren Batch, überschreibbar), `Git.As<IScmEventResponder>()` (zeichnet `ScmResponseRequest`s in `Responses` auf), `Git.As<IScmNotificationSource>()` (liefert `Notifications`), `Git.As<IScmAgentAccess>()`
- `Repository` — feste `ScmEventRepository` (`test-scm`, Viewer `U_1`/`owner`)

### `FakeGitHubEventApi`
Datei: `src/Softwareschmiede.Tests/Helpers/FakeGitHubEventApi.cs`

- Warteschlangen-Fake für `IGitHubEventApiClient`: `Add`/`AddJson`/`AddGraph`/`AddStatus`/`AddFailure`, `AssertComplete`, `Requests` (alle Aufrufe inkl. `JsonBody` — ermöglicht Assertions auf das GraphQL-Fragment, z. B. das Vorhandensein von `reactionGroups` in der Query)

### `TestDbContextFactory` / weitere
- `src/Softwareschmiede.Tests/Helpers/TestDbContextFactory.cs` — SQLite-Testkontexte
