# Tests: Bestehende Abdeckung und Test-Ausgangszustand

## Test-Ausgangszustand vor der Umsetzung

- Zeitpunkt (mit Zeitzone): 2026-11-27, Testläufe zwischen 22:38 und 22:45 +01:00 (CET; 21:38–21:45 UTC)
- Branch und Commit-ID: `task/issue-483-3af4e493df854f9092f8dc42c2d638ce-assign-ausloeser-funktioniert` @ `26e438cfbf575c7a9c4aae3411dbab3825ca2f0a` („feat: Automatisierte StatusServer-Updates über msTools.Updater (#501)", 2026-11-27 21:16:29 +0100)
- Uncommittete Änderungen im getesteten Stand: nur die neue Feature-Dokumentation `docs/features/task/issue-483-…/requirement.md` (untracked); **keine** Änderungen an Produktivcode, Tests oder Testkonfiguration
- Testumgebung und Runtime-/SDK-Versionen: Windows, .NET SDK 11.0.100, xUnit.net VSTest Adapter 4.0.1, SQLite-In-Process (Integrationstests via `ScmDispatchFixture`)
- Ermittelte Testsuiten und Quellen der Testbefehle: `CLAUDE.md` (stabile Lane `Category!=OsInterface`, `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` für Sandbox-Läufe) und `.github/workflows/pr-staging-ci.yml`/`staging-ci.yml` (drei reguläre Lanes + separate OsInterface-Lane). Vor jedem Lauf wurde implizit durch `dotnet test` gebaut (kein `--no-build`).

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| Unit-Tests (regulär) | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj -c Debug --filter "Category!=OsInterface" --logger "trx;LogFileName=inventory-baseline-tests.trx"` | Repo-Root | 0 | 2878 | 0 | 3 | [TRX](test-results/inventory-baseline-tests.trx), [Konsole](test-results/console-unittests.log) |
| Integrationstests (regulär) | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj -c Debug --filter "Category!=OsInterface" --logger "trx;LogFileName=inventory-baseline-integration.trx"` | Repo-Root | 0 | 436 | 0 | 0 | [TRX](test-results/inventory-baseline-integration.trx), [Konsole](test-results/console-integrationtests.log) |
| StatusServer-Tests | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test server/Softwareschmiede.StatusServer.Tests/Softwareschmiede.StatusServer.Tests.csproj -c Debug --logger "trx;LogFileName=inventory-baseline-statusserver.trx"` | Repo-Root | 0 | 154 | 0 | 0 | [TRX](test-results/inventory-baseline-statusserver.trx), [Konsole](test-results/console-statusserver.log) |

### Nachgewiesene bestehende Testfehler

Keine. Alle drei regulären Lanes liefen fehlerfrei (0 fehlgeschlagene Tests).

### Testlücken und Ausführungsprobleme

- **OsInterface-Lane nicht ausgeführt:** Die Kategorie `OsInterface` (`src/Softwareschmiede.Tests/E2E/*.cs`, FlaUI-/ConPTY-/Clipboard-/echte Prozessstarts; Umfang im TRX-Filter `Category=OsInterface`, lt. `CLAUDE.md` separate Lane) wurde in dieser Sandbox nicht ausgeführt — die Tests starten reale WPF-Fenster und sind umgebungsabhängig. Über ihren Zustand ist hier keine Aussage möglich; sie gilt weder als erfolgreich noch als bekannt fehlerhaft.
- **Übersprungene Tests (3, Lane Unit-Tests, plattformbedingt):**
  - `Softwareschmiede.Tests.Application.Services.WorkingDirectoryOpenServiceTests.Open_OnNotWindows_ThrowsPlatformNotSupportedException` — nicht auf Windows ausführbar.
  - `Softwareschmiede.Tests.Infrastructure.Services.SystemShutdownServiceTests.ResolveShutdownCommand_MacOS_RemainsUnchanged` — nicht auf Windows ausführbar.
  - `Softwareschmiede.Tests.Infrastructure.Services.SystemShutdownServiceTests.ResolveShutdownCommand_Linux_RemainsUnchanged` — nicht auf Windows ausführbar.
- **ConPTY-E2E-Tests:** über `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` in dieser Sandbox ausgesetzt (dokumentiertes Umgebungsverhalten, kein Code-Befund). In Lane 1 führte das zu keinem zusätzlichen „Übersprungen"-Zählerstand (die betroffenen Tests liegen ohnehin in `Category=OsInterface`).
- Keine Build-, Setup- oder Infrastrukturfehler aufgetreten.

## Testklassen (bereichsbezogen)

### `ScmEventValidationServiceTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/ScmEventValidationServiceTests.cs` — reine Logiktests ohne Provideraufrufe.

- `ActualAuthorOrEditorMustBeOwner` — Akteur-/Trigger-Regeln des Texttrigger-Pfads.
- `TriggerMustBeFreeStanding` / `ConfiguredTriggerKeyword_*` / `MissingTriggerKeyword_*` / `TriggerKeyword_IsTrimmedBeforeMatching` — Frei-stehend-Regel und Keyword-Konfiguration.
- `SeparatesAccount_MentionReplacesKeywordTrigger` / `SeparatesAccount_AuthorOrEditorMustBeOwner_NotViewer` / `SeparatesAccount_ViewerOwnerRuleDoesNotApply` / `Useraccount_KeywordStaysTheOnlyTrigger` — modusabhängiger Trigger-/Akteurvertrag.
- `SeparatesAccount_OwnResponseMarkerStillRejected` (Z. 132–134) — Marker-Regel „Eigene Antwort" im `SeparateAccount`-Texttrigger-Pfad.
- `ViewerReaction_RejectsCommentKinds` / `SeparatesAccount_ViewerReaction_Rejects` / `ViewerReaction_CheckRunsAfterTrigger` / `ViewerReaction_DescriptionNotRejected` — `AlreadyReactedRejection`, nur Kommentar-Arten.
- `GitHubActionsAuthor_IsAccepted` / `SeparatesAccount_GitHubActionsAuthor_IsAccepted` / `GitHubActionsEditor_IsAccepted` / `ForeignBotLogins_StayRejected` — `TrustedAutomationLogins`.
- **Assign-Block (Z. 232–296):**
  - `GetRejection_AssignOwnerAssigner_Accepts` — Owner-Zuweisung ohne Texttrigger akzeptiert.
  - `GetRejection_AssignViewerOrAutomation_Accepts` — Bot-/Automation-Zuweisende akzeptiert.
  - `GetRejection_AssignForeignAssigner_Rejected` — `AssignerNotAllowedRejection`.
  - `GetRejection_AssignForeignAssignerWithMention_AcceptsViaTextTrigger` / `GetRejection_AssignUndeterminedAssignerWithMention_AcceptsViaTextTrigger` — Texttrigger-Fallback.
  - `GetRejection_AssignForeignAssignerWithMentionAndMarker_StaysRejected` — Marker im Fallback bleibt abgelehnt.
  - `GetRejection_AssignEmptyDescription_Rejected` — `AssignEmptyDescriptionRejection`.
  - `GetRejection_AssignUndeterminedAssigner_Rejected` — `AssignerUnknownRejection`.
  - **`GetRejection_AssignOwnResponseMarker_Rejected` (Z. 294–296)** — erwartet aktuell `"Eigene Antwort"` für Assign + Marker-Body; die Anforderung kehrt diese Erwartung bei berechtigtem Zuweisenden um.
- Hilfsmethode `ObserveAssign(body, actorId, actorLogin)` (Z. 298–307): Assign-Beobachtung mit `ScmEventTrigger("assign", …)` im `SeparateAccount`.
- Hilfsmethoden `Observe`/`ObserveSeparate` (Z. 309–329): Beobachtungen je Kontomodus.

### `GitHubEventSourceTests`
Datei: `src/Softwareschmiede.Tests/Infrastructure/Plugins/GitHubEventSourceTests.cs` — Plugin-Logik gegen `FakeGitHubEventApi`-Transport.

- `Comments_ReadAllPages_AndBodyEditorFromSameVersion` / `BotAuthor_LoginFlowsIntoObservation` / `Watermark_ExcludesNewerVersion` / `Reviews_PaginateAndExcludePending` / `PullRequestCommentKinds_UseSeparateNamespaces` / `Description_VersionsDistinguishTextAndEdits_NotMetadata` / `MissingEditor_RemainsMissing_AndSameVersionCanResolveLater` — Lesepfad aller `ContentKind`-Arten.
- **`OwnMarkedComment_IsExcluded_ButForeignMarkerIsNotTrusted` (Z. 181–191)** — deckt den Vorfilter Z. 384 ab: eigener marker-tragender Kommentar entfällt, fremder Marker-Text bleibt Beobachtung. Aktuell nur `IssueComment`; ein `Description`-Gegenstück existiert **nicht**.
- `ViewerReactions_*` / `MissingReactionGroups_ThrowsPermanent` / `ReactionGroups_RequestedInQuery` — Reaktionsdaten.
- `FailedSecondPage_*` / `Pagination_RejectsForeignTargets` / `GraphErrors_RejectPartialData` / `Failures_AreTypedAndSanitized` / `AccessFailures_ExplainTokenSettingsAndRecovery` / `RateLimit_*` / `Cache_*` — Fehlersemantik, Rate-Limit-Gates, Auflösungscache.
- `CommentTimeout_ReportsUncertain_AndReconcilesMarker` / `CommentAttachments_*` / `ExistingReaction_IsReconciledAcrossAllPages` — Antwortpfad und Marker-Idempotenz.
- `EventKey_SeparatesEveryOriginDimension` / `NormalizeRepositoryAlias_CanonicalizesAllForms` — Schlüssel-/Aliasmodell.
- Hilfsmethoden (Z. 1690–1763): `Plugin(FakeApi …)`, `Identity`/`IdentityRest`, `Node`, `Review`, `Reviews`, `Ref`, `Page`, `Read`, `StoreWithAgentToken`; `FakeApi : FakeGitHubEventApi`.

### `GitHubAssignmentSourceTests`
Datei: `src/Softwareschmiede.Tests/Infrastructure/Plugins/GitHubAssignmentSourceTests.cs` — `IScmAssignmentSource` gegen Fake-Transport.

- `GetViewerAssignment_MapsLatestViewerAssignedEvent` / `_LatestEventAndAssignerFallback` / `_ReadsAllPages` — Auswahl und Pagination des `issues/{n}/events`-Endpunkts.
- `GetViewerAssignment_UnassignedLatest_ReturnsNull_Reassigned_ReturnsEvent` / `_NoViewerEvent_ReturnsNull` — Jüngst-Regel und `null`-Fälle.
- `GetViewerAssignment_PullRequest_UsesIssueEventsEndpoint` — PR-Referenzen über denselben Endpunkt.
- `GetViewerAssignment_UsesAgentClientOnly` / `GetViewerAssignment_TypedFailures` — Konto-Grenze und Fehlertypisierung.

### `GitHubNotificationSourceTests`
Datei: `src/Softwareschmiede.Tests/Infrastructure/Plugins/GitHubNotificationSourceTests.cs`

- `EnumerateNotifications_MapsIssueAndPullRequestThreads` / `_SkipsNonReferenceSubjects` / `_AdjustSinceParameter` / `_ReadsAllPages` / `_TypedFailures` / `_UsesAgentClientOnly` / `_WithoutAgentToken_ThrowsAuthentication` / `_InvalidHost_ThrowsPermanent` / `_HostWithForeignComponents_ThrowsPermanent` — Inbox-Lesepfad (`reason` wird roh gemappt; kein assign-spezifischer Mapping-Test nötig, da Host-seitig gefiltert wird).

### `ScmEventMonitoringServiceTests_Assignments`
Datei: `src/Softwareschmiede.IntegrationTests/Services/ScmEventMonitoringServiceTests_Assignments.cs` — End-to-end mit `ScmDispatchFixture`.

- `Monitor_SeparatesAccount_AssignNotification_CreatesTaskAndDispatches` — Referenzfall: assign → Description → Resolver → `Zugestellt` mit `AssigneePromptPrefix`, persistierter `Trigger`, bitgleicher Wiederanlauf.
- `Monitor_SeparatesAccount_AssignOnPullRequest_CreatesTask` — Assign-Auslöser gilt auch für PRs.
- `Monitor_SeparatesAccount_Reassignment_DeliversToExistingTask` — neue `EV_n`-Identität ⇒ neues Ereignis (ContentKey-Suffix).
- `Monitor_SeparatesAccount_AssignEmptyDescription_CreatesNoTask` — `AssignEmptyDescriptionRejection` auf Snapshot, `Inbox:`-Diagnose auf Checkpoint.
- `Monitor_SeparatesAccount_AssignByForeignLogin_Rejected` — `AssignerNotAllowedRejection`.
- `Monitor_SeparatesAccount_ForeignAssignWithMention_CreatesTaskViaTextTrigger` — Texttrigger-Fallback.
- `Monitor_SeparatesAccount_AssignUndeterminable_Rejected` / `Monitor_SeparatesAccount_AssignWithoutCapability_Rejected` — `AssignerUnknownRejection`.
- `Monitor_SeparatesAccount_AssignByAutomation_Accepted` — Vertrauensliste als Zuweisende.
- `Monitor_SeparatesAccount_AssignmentQueryFailure_KeepsInboxWatermarkForRetry` — Backoff und Inbox-Watermark bei Zuweisungsabfrage-Fehler.
- **`Monitor_SeparatesAccount_AssignEmptyDescriptionBatch_Discarded` (Z. 368–391)** — leerer Description-Batch ⇒ `Inbox: Keine verwertbare Referenzbeschreibung` — genau das heutige Fehlerbild des Issues; bleibt für echte Leer-Batches bestehen.
- `Monitor_SeparatesAccount_AssignRejection_RaisesInboxCheckpointDiagnostic` — `Inbox:`-präfixierte Ablehnungsdiagnose vs. `AmbiguousAssignmentDiagnostic`.
- Hilfsmethoden: `AddAmbiguousTaskAsync` (Z. 442–467), `ConfigureBotResolution` (Z. 469–473), `AddRepositoryAsync` (Z. 475–492).
- **Lücke:** kein Integrationsfall „assign-Notification + Viewer-autorierte, marker-tragende Beschreibung ⇒ Ereignis/Aufgabe" — die Fixture kann Autor (`authorId`/`authorLogin`) über `ConfigureDescriptionObservations` bereits steuern.

### `ScmEventMonitoringServiceTests_IssuePreparation`
Datei: `src/Softwareschmiede.IntegrationTests/Services/ScmEventMonitoringServiceTests_IssuePreparation.cs`

- `Monitor_DescriptionChangeAfterPreparation_CreatesNoLoop` (Z. 235–266) — eine spätere marker-tragende Beschreibungsänderung löst keinen zweiten Aufbereitungsauftrag aus (Auswertungs-Checkpoint).
- `Monitor_TriggerOnPreparedIssue_AssignsToPreparationTask` (Z. 273–319) — Trigger-Kommentar auf aufbereitetem Issue läuft in die Zweck-Aufgabe.
- Weitere Methoden decken Baseline, Altbestand, Schalter und Bestandssuche ab.

## Hilfsmethoden

### `ScmDispatchFixture` (`src/Softwareschmiede.Tests/Helpers/ScmDispatchFixture.cs`, Namespace `Softwareschmiede.TestSupport`)
- `ScmDispatchFixture(…, bool assignmentSource = false)` — komplette In-Process-Umgebung: echte SQLite, echte Services, gemockte `IGitPlugin`-Fähigkeiten (`IScmEventSource`, `IScmEventResponder`, `IScmNotificationSource`, `IScmAgentAccess`, optional `IScmAssignmentSource`), Terminal-Fake, `TestTimeProvider`.
- `AddNotification(ScmNotification)` — registriert Inbox-Einträge für die gemockte `IScmNotificationSource`.
- `ConfigureDescriptionObservations(body, authorId = "U_1", authorLogin = "owner")` — Description-Batch mit steuerbarem Body **und Autor** (Viewer-Autor über `BotRepository`-Identität `BOT`/`scm-bot` abbildbar).
- `ConfigureAssignmentSource(ScmAssignment?)` — steuert `GetLatestViewerAssignmentAsync` (benötigt `assignmentSource: true`).
- `ConfigureIssue(int?)` / `ConfigurePullRequestRead(int?, body)` — Anlagedaten für Resolver.
- `ConfigureMentionObservations(authorId, authorLogin)` — Mention-Kommentar-Batch.
- `SetAccountModeAsync(ScmAccountMode)` / `SetSettingAsync(key, value)` — Laufzeit-Einstellungen.
- `RunMonitorAsync` / `Monitor` / `DispatchAsync` / `EventAsync` / `WaitForTriggeredDeliveryAsync` / `AdvancePastStabilityPeriod` / `Trigger`/`StartCheck` — Zyklus- und Dispatch-Steuerung.
- `Repository` (`U_1`/`owner`, UserAccount) und `BotRepository` (`BOT`/`scm-bot`, SeparateAccount) — Referenzidentitäten.
- `AddScmTestServices` — Service-Collection-Bootstrap (`src/Softwareschmiede.Tests/Helpers/ScmTestServices.cs`); `TestTimeProvider` in `src/Softwareschmiede.Tests/Helpers/TestTimeProvider.cs`.

### `FakeGitHubEventApi` (`src/Softwareschmiede.Tests/Helpers/FakeGitHubEventApi.cs`)
- `Add(endpoint, obj, headers?)` / `AddJson(endpoint, json, headers?)` / `AddGraph(obj)` / `AddStatus(endpoint, status, headers?)` / `AddFailure(endpoint, ex)` / `AddUpload(…)` / `AddUploadJson(…)` / `AddUploadStatus(…)` — geplante REST-/GraphQL-Antworten; `Requests`/`UploadRequests`/`AssertComplete()` zur Verifikation. Anmerkung: Der Vorfilter in `AddObservation` wird über `Node(..., authorId/authorLogin)` gegen `Repository.ViewerId`/`ViewerLogin` getestet — ein Assign-Beschreibungsfall benötigt eine Fake-Antwort mit Viewer-Autor (z. B. `authorId: "BOT"` auf `BotRepository`-artiger Identität bzw. `U_1` im Unit-Repository).
