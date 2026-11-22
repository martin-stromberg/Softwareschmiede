# Bestandsaufnahme – Schritt 1: Repository-Identität

Verdichtete Projekt-Bestandsaufnahme liegt unter
`docs/projects/task/issue-456-…-issue-aufbereitung-repository/inventory.md`.
Schritt-relevante Fakten (verifiziert):

- `ScmIssuePreparationMonitoringService.EvaluateIssueAsync` (`:182–200`): Zeile 194
  `issue with { Repository = issue.Repository with { Account = accountMode } }` —
  nur das Flag wird gesetzt, Viewer-Felder bleiben UserAccount-Werte.
- `EvaluateDiscoveredIssuesAsync` (:144–177) übergibt `localRepositories`
  (`IReadOnlyList<GitRepository>` der kanonischen Alias-Gruppe, stets ≥1 Eintrag,
  wenn erreicht); `issue` ist `ScmEventReference` aus der UserAccount-Auflösung.
- `ScmRepositoryContext(Guid gitRepositoryId, string pluginPrefix, string
  repositoryId, ScmAccountMode)` — `repositoryId` ist die kanonische Alias-/URL-Form;
  `ScmEventOrchestrator` baut sie als `https://{host}/{owner}/{name}`.
- `GitHubPlugin.GetEventRepositoryAsync` liefert `ScmEventRepository` mit dem
  Account des Kontexts (Test `GetEventRepositoryAsync_SeparatesAccount_UsesAgentToken`,
  `GitHubEventSourceTests:1148–1162`); `_resolutionCache` je Client/Konto.
- Persistenz: `ScmIncomingEvent.ObservationJson` (serialisierter
  `ScmEventObservation` mit `Reference.Repository`) — Bestandsdaten bleiben nach
  Codefix gemischt (Ops-Reparatur dokumentieren, kein Mechanismus).
- Tests: `ScmIssuePreparationMonitoringServiceTests` (ScmDispatchFixture, Moq-
  `IScmEventSource`; `fixture.Repository` = UserAccount-Snapshot,
  `ScmDispatchFixture.BotRepository` = SeparateAccount-Snapshot mit ViewerId `BOT`).
  Plugin-Ebene: `GitHubEventSourceTests` mit `FakeApi`/`Identity(api, viewer: …)`,
  `Plugin(user, StoreWithAgentToken(), agent)` für Zwei-Konto-Setups.
- `ExecuteResponseAsync`-Schutz (`GitHubPlugin.EventResponses.cs:49–52`) wird durch
  `Response_RoutesByReferenceAccount` (`:1191`) bereits geroutet getestet.
- Doku: `docs/help/plugins/github-ereignisse/business-rules.md` (Modus-Vertrag
  Z. 49/51/76); `troubleshooting.md` für Ops-Reparatur-Anleitung.
