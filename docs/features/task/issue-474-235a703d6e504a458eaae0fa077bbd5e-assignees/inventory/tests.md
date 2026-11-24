# Tests — Bestandsaufnahme Assignee-Auslöser (Issue #474)

## Test-Ausgangszustand vor der Umsetzung

- **Zeitpunkt (mit Zeitzone):** 2026-11-24, ca. 04:14–04:21 Uhr (+01:00, CET)
- **Branch und Commit-ID:** `task/issue-474-235a703d6e504a458eaae0fa077bbd5e-assignees`, Commit `5fff3a78026e5b1c3d90d5bfb88f0ec95a675126`
- **Uncommittete Änderungen im getesteten Stand:** Keine Änderungen an Produktivcode, Tests oder Konfiguration. Einzig untracked: das Feature-Verzeichnis `docs/features/task/issue-474-235a703d6e504a458eaae0fa077bbd5e-assignees/` (`requirement.md`, `todo.md` sowie die neu angelegten Inventory-Artefakte inkl. Testnachweise). Der getestete Code-Stand entspricht exakt dem genannten Commit.
- **Testumgebung und Runtime-/SDK-Versionen:** Windows (MSYS/Git-Bash), .NET SDK `11.0.100`; Ziel-Frameworks `net11.0-windows10.0.17763.0` (`Softwareschmiede.Tests`) und `net11.0` (`Softwareschmiede.IntegrationTests`). Agent-Sandbox: `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` inline gesetzt (siehe „Testlücken").
- **Ermittelte Testsuiten und Quellen der Testbefehle:** Projektdokumentation (`CLAUDE.md`, Abschnitt „Testing") definiert die stabile Lane `dotnet test --filter "Category!=OsInterface"` je Testprojekt; die `OsInterface`-Lane (E2E, ConPTY, Clipboard, echte Prozessstarts) läuft separat. Für die Bestandsaufnahme wurden gemäß Vorgabe der volle Build und beide stabilen Lanes ausgeführt.
- **Build:** `dotnet build Softwareschmiede.slnx` — erfolgreich, 0 Warnungen, 0 Fehler, ~19,5 s → [build.log](test-results/build.log)

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| 1 (Unit) | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category!=OsInterface"` | Repo-Wurzel | 0 | 2800 | 0 | 3 | [dotnet-test-unittests.log](test-results/dotnet-test-unittests.log) |
| 2 (Integration) | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj --filter "Category!=OsInterface"` | Repo-Wurzel | 0 | 417 | 0 | 0 | [dotnet-test-integrationtests.log](test-results/dotnet-test-integrationtests.log) |

### Nachgewiesene bestehende Testfehler

Keine. Beide Läufe endeten mit Exit-Code 0 und 0 Fehlern. Für spätere „preexisting"-Bewertungen gilt: der dokumentierte Ausgangslauf enthält keinen einzigen Fehlschlag — jeder künftige Fehler in diesen Lanes ist neu oder eine Umgebungsabweichung, kein bekannter Bestandsfehler.

### Testlücken und Ausführungsprobleme

- **Übersprungene Tests (3, Lauf 1, kein Fehler):**
  - `Softwareschmiede.Tests.Application.Services.WorkingDirectoryOpenServiceTests.Open_OnNotWindows_ThrowsPlatformNotSupportedException` — plattformbedingt (Nicht-Windows-Pfad).
  - `Softwareschmiede.Tests.Infrastructure.Services.SystemShutdownServiceTests.ResolveShutdownCommand_MacOS_RemainsUnchanged` — plattformbedingt.
  - `Softwareschmiede.Tests.Infrastructure.Services.SystemShutdownServiceTests.ResolveShutdownCommand_Linux_RemainsUnchanged` — plattformbedingt.
- **Nicht ausgeführte Suites:** Die `OsInterface`-Lane (Kategorie `OsInterface`: WPF-E2E inkl. `E2E_ScmEvents`, ConPTY-, Clipboard- und echte Prozessstart-Tests) wurde entsprechend den Projektvorgaben (separate Lane) nicht ausgeführt. Über deren Ausgangszustand liegt daher **kein** Nachweis vor; insbesondere ist der konsolidierte SCM-E2E-Lauf (`E2E_ScmEvents.cs`, inkl. `BotModeAsync`-Phase) ungetestet.
- **`SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`:** In dieser Agent-Sandbox funktionieren die ConPTY-E2E-Tests grundsätzlich nicht (bekanntes, dokumentiertes Umgebungsproblem, kein Code-Bug); innerhalb der `Category!=OsInterface`-Lanes waren ohnehin keine ConPTY-Tests enthalten — die Variable ist als Sandbox-Vorschrift mitgelaufen und hatte hier keine Skip-Wirkung.
- Keine Build-, Setup- oder Infrastrukturfehler aufgetreten.

## Testklassen

### `GitHubNotificationSourceTests`
Datei: `src/Softwareschmiede.Tests/Infrastructure/Plugins/GitHubNotificationSourceTests.cs`

Transport-Mock-Tests des Notification-Pfads (`IScmNotificationSource`, `FakeGitHubEventApi`).

- `EnumerateNotifications_MapsIssueAndPullRequestThreads` — Mapping Thread → `ScmNotification`; `reason` wird unverändert durchgereicht (Test nutzt `mention` und `review_requested`)
- `EnumerateNotifications_SkipsNonReferenceSubjects` — Release/CheckSuite/Discussion-Subjects werden übersprungen
- `EnumerateNotifications_AdjustSinceParameter` — `since` minus 2 h Sicherheitsabstand
- `EnumerateNotifications_ReadsAllPages` — Link-Header-Pagination
- `EnumerateNotifications_TypedFailures` — HTTP 503 → `ScmProviderFailureKind.Temporary`
- `EnumerateNotifications_UsesAgentClientOnly` — Inbox läuft ausschließlich über den Agent-Client
- `EnumerateNotifications_WithoutAgentToken_ThrowsAuthentication` — fehlender AgentToken → `Authentication` vor dem ersten Aufruf
- `EnumerateNotifications_InvalidHost_ThrowsPermanent` / `EnumerateNotifications_HostWithForeignComponents_ThrowsPermanent` — Host-Validierung

### `ScmEventMonitoringServiceTests_Notifications`
Datei: `src/Softwareschmiede.IntegrationTests/Services/ScmEventMonitoringServiceTests_Notifications.cs`

Integrationstests des `SeparateAccount`-Inbox-Pfads (echte Services auf SQLite, `ScmDispatchFixture`).

- `Monitor_SeparatesAccount_MentionNotification_EntersInboxViaResolver` — mention → Referenz → Resolver-Aufgabe → Inbox-Ereignis; keine repository-weite Enumeration; Inbox-Checkpoint mit `Inbox:`-Diagnose
- `Monitor_GitHubActionsAuthoredMention_CreatesTaskAndDispatches` — github-actions-Autor über Vertrauensliste → Aufgabe + Zustellung
- `Monitor_SeparatesAccount_ForeignNotification_DiscardedWithDiagnostic` — Fremd-Repository → verworfen + Zählerdiagnose, kein Backoff
- `Monitor_SeparatesAccount_NonMentionReasons_AreIgnored` — `review_requested`/`subscribed` werden ignoriert, kein Read (**direkt relevant: derzeit gilt dasselbe für `assign`**)
- `Monitor_SeparatesAccount_InboxFailure_KeepsWatermarkForRetry` — Inbox-Fehler → eigener Backoff-Checkpoint, Watermark bleibt
- `Monitor_SeparatesAccount_InboxCommit_WithPreexistingCheckpoint_UpdatesWatermark` — vorhandener Checkpoint wird aktualisiert
- `Monitor_SeparatesAccount_NotificationFromForeignHost_DiscardedDespiteMatchingNodeId` — Node-IDs sind instanzlokal (Host-Match)
- `Monitor_ModeSwitch_BackToEnumeration` — Rückschaltung auf `UserAccount` reaktiviert die Enumeration

### `ScmEventMonitoringServiceTests_TaskAssignment`
Datei: `src/Softwareschmiede.IntegrationTests/Services/ScmEventMonitoringServiceTests_TaskAssignment.cs`

Aufgabenauflösung/-anlage im `UserAccount`-Enumerationspfad; für die Anforderung relevante Muster:

- `Monitor_QualifiedCommentOnUnassignedIssue_CreatesTaskAndInboxEntry` — Trigger-Beobachtung → Resolver-Anlage
- `Monitor_RepeatedPollsAndBatches_CreateSingleTask` — Dedup: genau eine Aufgabe über Wiederholungen
- `Monitor_ExistingTask_IsReusedWithoutSecondCreation` — Wiederverwendung ohne Doppelanlage (**das Muster, das assign-Folgeereignisse benötigen**)
- `Monitor_UnqualifiedEvent_KeepsUnassignedWithoutTask`, `Monitor_ReactedCommentOnUnassignedReference_CreatesNoTask` — Ablehnungs-/Verwerfungspfade
- Diverse PR-/Aufbereitungs-/Mehrdeutigkeitsfälle (`Ambiguous`, Purpose-Aufgaben, Branch-/Closing-Issue-Kontext)

### `ScmEventValidationServiceTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/ScmEventValidationServiceTests.cs`

Reine `GetRejection`-Prüfungen: frei stehender Trigger/Keyword/`@mention`, Owner-/Editor-Identität, `TrustedAutomationLogins`-Akzeptanz (`github-actions[bot]`, `app/github-actions`, case-insensitive), Fremd-Logins-Ablehnung, `AlreadyReactedRejection` (Kommentar-Arten, Description ausgenommen), `NoMentionRejection`, `EventKey`-Versionierung. **Keine assign-Fälle vorhanden.**

### `E2E_ScmEvents` (BotModeAsync-Phase)
Datei: `src/Softwareschmiede.Tests/E2E/E2E_ScmEvents.cs` (Z. 383-482, Kategorie `OsInterface` — in dieser Bestandsaufnahme nicht ausgeführt)

Konsolidierter SCM-E2E: `BotModeAsync` deckt Token-UI-Validierung, Inbox-Polling am Agent-Client, mention → Aufgabe → Dispatch → Antwort, Viewer-Reactions-Dedup, Fremd-/Nicht-mention-Verwerfungsdiagnose und Rückschaltung ab. Bemerkenswert: `fixture.AddNotification(21, reason: "assign")` (Z. 452) dient heute als **Ignoriert-Fall** — die assign-Notification erzeugt weder Ereignis noch Aufgabe.

## Hilfsmethoden

### `ScmDispatchFixture`
Datei: `src/Softwareschmiede.Tests/Helpers/ScmDispatchFixture.cs` (Namespace `Softwareschmiede.TestSupport`, `internal`)

- `AddNotification(ScmNotification)` — registriert einen Inbox-Eintrag, den das `IScmNotificationSource`-Mock liefert (Reason frei wählbar)
- `ConfigureMentionObservations(authorId, authorLogin)` — `ReadEventsAsync` liefert eine `@scm-bot`-Mention-Beobachtung auf `IssueComment`
- `ConfigureIssue(number?)` — `ReadIssueAsync` liefert Standard-Issue („Beschreibungstext"-Body)
- `Git.As<IScmNotificationSource>()` / `Git.As<IScmEventSource>()` / `Git.As<IScmEventResponder>()` / `Git.As<IScmAgentAccess>()` — Moq-Interfaces des Plugin-Mocks (neue Vertragsfähigkeiten müssten analog ergänzt werden)
- `RunMonitorAsync`, `Monitor`, `SetAccountModeAsync`, `WaitForTriggeredDeliveryAsync`, `AdvancePastStabilityPeriod`, `DispatchAsync`, `Prompts`, `Responses`, `BotRepository`, `SetSettingAsync`

### `FakeGitHubEventApi`
Datei: `src/Softwareschmiede.Tests/Helpers/FakeGitHubEventApi.cs`

Warteschlangenbasierter `IGitHubEventApiClient`: `AddJson`/`Add`/`AddGraph`/`AddStatus`/`AddFailure` je erwartetem Endpunkt-Fragment, `Requests`-Protokoll, `AssertComplete`. Für eine Zuweisender-Abfrage (z. B. `issues/{n}/events`) wären zusätzliche eingeplante Endpunkte nötig.

### `ScmE2EFixture` / `ScmE2ETestConfiguration` / `ScmE2EApiClient`
Dateien: `src/Softwareschmiede.Tests/E2E/ScmE2EFixture.cs`, `src/Softwareschmiede.App/Services/Testing/ScmE2ETestConfiguration.cs`, `src/Softwareschmiede.App/Services/Testing/ScmE2EApiClient.cs`

- `ScmE2EFixture.AddNotification(number, repositoryNodeId, reason, pullRequest)` — legt `ScmE2ENotification` (Reason frei wählbar, `ThreadId = "THREAD_" + number`) im Szenario an
- `ScmE2EFixture.AddText(number, id, body, kind, viewerReactions)` — Szenario-Text (`Kind`, u. a. `"Description"`, `"IssueComment"`)
- `ScmE2EApiClient` bildet `GET notifications` auf `scenario.Notifications` (liefert `reason` unverändert) und alle REST-/GraphQL-Endpunkte der E2E-Fixture; **kein Timeline-/Events-Endpunkt** implementiert (unbekannte Endpunkte → `InvalidOperationException`)
- Warte-/Prüf-Helfer: `WaitEventAsync`/`WaitEventForTaskAsync`, `WaitForCallsAsync`, `WaitForDatabaseAsync`, `Responses()`, `Prompts()`, `Calls()`, `SetAccountMode`

### `ScmTestServices`
Datei: `src/Softwareschmiede.Tests/Helpers/ScmTestServices.cs`

`AddScmTestServices` — DI-Registrierung des kompletten SCM-Dienststapels der Fixtures (echte Services, SQLite, gemockte Grenzen).
