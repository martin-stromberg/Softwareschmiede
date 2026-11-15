# Tasks: Konto `github-actions` als akzeptierte Autor-Identität der SCM-Ereignisvalidierung

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Logik | Whitelist `TrustedAutomationLogins` (`"github-actions[bot]"`, `"app/github-actions"`) als `private static readonly`-Sammlung in `ScmEventValidationService` anlegen | Offen | — |
| 2 | Logik | Private Hilfsmethode `IsTrustedAutomationLogin(string? login)` in `ScmEventValidationService` anlegen (case-insensitive `OrdinalIgnoreCase`-Match, `null`/leer → `false`) | Offen | — |
| 3 | Logik | Akteursregel in `ScmEventValidationService.GetRejection` erweitern: `SameIdentity(...) \|\| IsTrustedAutomationLogin(actorLogin)` (Ablehnungstext unverändert); XML-Doc des erweiterten Vertrags ergänzen | Offen | — |
| 4 | Tests | Unit-Test-Helper `Observe`/`ObserveSeparate` in `ScmEventValidationServiceTests` um optionale `authorLogin`/`editorLogin`-Parameter erweitern | Offen | — |
| 5 | Tests | Unit-Test `GithubActionsAuthor_IsAccepted` in `ScmEventValidationServiceTests` schreiben (beide Schreibweisen + Großschreibung, Modus `Benutzerkonto`) | Offen | — |
| 6 | Tests | Unit-Test `SeparatesKonto_GithubActionsAuthor_IsAccepted` in `ScmEventValidationServiceTests` schreiben | Offen | — |
| 7 | Tests | Unit-Test `GithubActionsEditor_IsAccepted` in `ScmEventValidationServiceTests` schreiben (beide Modi) | Offen | — |
| 8 | Tests | Unit-Test `ForeignBotLogins_StayRejected` in `ScmEventValidationServiceTests` schreiben (`dependabot[bot]` etc., beide Modi) | Offen | — |
| 9 | Tests | `GitHubEventSourceTests.Node`-Helper um optionale `authorId`/`authorLogin`-Parameter erweitern | Offen | — |
| 10 | Tests | Test `BotAuthor_LoginFlowsIntoObservation` in `GitHubEventSourceTests` schreiben | Offen | — |
| 11 | Tests | `ScmDispatchFixture.AddAsync` um optionale `authorId`/`authorLogin`-Parameter erweitern | Offen | — |
| 12 | Tests | `ConfigureMentionObservations` (`ScmEventMonitoringServiceTests_Notifications`) bzw. `ConfigureObservations` um Author-Parameter erweitern | Offen | — |
| 13 | Tests | Integrationstest `Monitor_GithubActionsAuthoredMention_CreatesTaskAndDispatches` in `ScmEventMonitoringServiceTests_Notifications` schreiben (SeparatesKonto-Referenzfall) | Offen | — |
| 14 | Tests | Integrationstest `Monitor_GithubActionsAuthoredTrigger_CreatesTask` in `ScmEventMonitoringServiceTests_RepoWide` schreiben (Benutzerkonto) | Offen | — |
| 15 | Tests | Integrationstest `Dispatch_GithubActionsAuthoredObservation_Delivers` in `ScmEventDispatchTests` schreiben (Dispatch-Revalidierung) | Offen | — |
| 16 | Tests | Integrationstest `Monitor_ForeignBotAuthoredTrigger_StillRejected` in `ScmEventMonitoringServiceTests_RepoWide` schreiben | Offen | — |
| 17 | Dokumentation | `docs/help/plugins/github-ereignisse/business-rules.md` aktualisieren (§„Trigger und Repository-Owner", §„Kontomodus und Notifications-Inbox", dokumentierte Betriebsregel „das Repository selbst ruft den Agenten") | Offen | — |
| 18 | Dokumentation | `docs/help/plugins/github-ereignisse/troubleshooting.md` aktualisieren (`github-actions[bot]`-Autorenschaft ist keine Fehlerursache mehr; `REVIEW_ISSUE_TOKEN`-Workaround für diese Ursache entfällt; Ursachenlisten in §„Es erscheint kein Ereignis" und §„Im Modus ‚Separates Konto' erscheint kein Ereignis" ergänzen) | Offen | — |
| 19 | Dokumentation | `docs/help/plugins/github-ereignisse/ablauf-technisch.md` aktualisieren (Validierungsbeschreibung Z. 38 um vertrauenswürdige Automatisierungs-Logins ergänzen) | Offen | — |
| 20 | Verifikation | `dotnet format Softwareschmiede.slnx --verify-no-changes`, Voll-Build `dotnet build Softwareschmiede.slnx`, dann synchron (nicht im Hintergrund) beide regulären Test-Lanes mit `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` und `--filter "Category!=OsInterface"` ausführen | Offen | — |
