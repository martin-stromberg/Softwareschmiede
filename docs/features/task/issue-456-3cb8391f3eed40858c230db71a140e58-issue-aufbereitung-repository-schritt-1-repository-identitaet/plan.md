# Umsetzungsplan – Schritt 1: Konsistente SCM-Repository-Identität

## Änderungen

1. **`ScmIssuePreparationMonitoringService.EvaluateIssueAsync`** — vor dem Tagging:
   Ist `accountMode != ScmAccountMode.UserAccount` und `localRepositories` nicht
   leer, wird das Repository unter `accountMode` erneut aufgelöst:
   `new ScmRepositoryContext(localRepositories[0].Id, repository.ProviderPrefix,
   $"https://{host}/{owner}/{name}", accountMode)`. `RepositoryId`-Abgleich gegen
   die Entdeckungs-Auflösung — Mismatch → `ScmProviderException(Permanent)`.
   `tagged` erhält den Neuauflösungs-Snapshot (trägt `Account` bereits); im
   UserAccount-Fall bleibt exakt das bisherige `with { Account = accountMode }`
   (kein Zusatz-Call). Fehlgeschlagene Neuauflösung läuft in den vorhandenen
   Issue-Checkpoint-Fehlerpfad (`RecordFailureAsync`).
2. **Tests** `ScmIssuePreparationMonitoringServiceTests`:
   - SeparatesKonto: `GetEventRepositoryAsync` je Konto beantwortet
     (`fixture.Repository` / `BotRepository`) → persistiertes Ereignis trägt
     ViewerId `BOT`/`scm-bot` + `SeparateAccount`; Neuauflösung unter
     SeparateAccount-Kontext verifiziert.
   - UserAccount: keine SeparateAccount-Kontext-Auflösung (kein Zusatz-Call).
   - `RepositoryId`-Mismatch → Issue-Checkpoint `Permanent`, kein Task/Ereignis.
   - Neuauflösung wirft → Issue-Checkpoint-Fehler, kein Task/Ereignis.
   - Bestehender Test `…_EventCarriesMonitoringAccount` bleibt grün.
3. **Plugin-Test** `GitHubEventSourceTests`: SeparatesKonto-Snapshot mit
   Bot-Viewer (`Identity(agent, viewer: "BOT")`) → `ExecuteResponseAsync`
   bestätigt Aktion ohne Identitäts-Fehlalarm; `ReconcileResponseAsync` erkennt
   Kommentar des Bot-Users als eigenen (Marker + `node_id`-Treffer).
4. **Doku** (`docs/help/plugins/github-ereignisse/`): Konto-Konsistenz-Regel für
   den Aufbereitungs-Snapshot + Ops-Reparaturanleitung für Bestandsdaten
   (Ereignis verwerfen, `issueaufbereitung-issue/…`-Checkpoint löschen,
   Neuaufwertung abwarten; Hinweis Marker-Idempotenz gegen Doppelposts).

## Akzeptanzszenarien (aus requirement.md)

- SeparatesKonto: Aufbereitungsereignis trägt Bot-Viewer → Remote-Ausführung ohne
  Permission-Fehlalarm; Reconcile findet eigene Aktion.
- UserAccount: Verhalten und Transportaufrufe unverändert.
- Fehlerfälle: fehlender Zugriff des separaten Kontos und `RepositoryId`-Mismatch
  enden als ehrlicher Checkpoint-Fehler je Issue.

## Betroffene Dateien

`src/Softwareschmiede/Application/Services/ScmIssuePreparationMonitoringService.cs`,
`src/Softwareschmiede.IntegrationTests/Services/ScmIssuePreparationMonitoringServiceTests.cs`,
`src/Softwareschmiede.Tests/Infrastructure/Plugins/GitHubEventSourceTests.cs`,
`docs/help/plugins/github-ereignisse/{business-rules,troubleshooting}.md`,
`docs/RELEASE_NOTES.md`.

## Offene Punkte

Keine.
