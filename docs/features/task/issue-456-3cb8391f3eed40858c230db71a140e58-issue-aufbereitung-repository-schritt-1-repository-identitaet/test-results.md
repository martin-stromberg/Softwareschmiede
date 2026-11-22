# Test Results: Konsistente SCM-Repository-Identität (Schritt 1)

## Integrationstests (`Softwareschmiede.IntegrationTests`)

`dotnet test src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj --filter "FullyQualifiedName~ScmIssuePreparationMonitoringServiceTests"`

Ergebnis: **18 bestanden, 0 fehlgeschlagen** — darunter die neuen Tests:

- `PreparationMonitor_SeparatesAccount_EventCarriesExecutionViewer` — PASS:
  persistierter Snapshot trägt `ViewerId = BOT`, `ViewerLogin = scm-bot`,
  `Account = SeparatesKonto`; Neuauflösung unter SeparateAccount-Kontext verifiziert,
  Entdeckung weiterhin unter UserAccount.
- `PreparationMonitor_UserAccount_NoSecondResolution` — PASS: keine
  Nicht-UserAccount-Auflösung, Ereignis wie bisher eingereiht.
- `PreparationMonitor_ResolutionMismatch_RecordsIssueFailure` — PASS:
  `RepositoryId`-Mismatch → Issue-Checkpoint-Fehler `Permanent`, kein Ereignis,
  kein Beschreibungs-Read.
- `PreparationMonitor_ResolutionFailure_RecordsIssueFailure` — PASS:
  `Permission`-Fehler der Neuauflösung → Issue-Checkpoint-Fehler `Permission`,
  kein Ereignis, kein Beschreibungs-Read.
- Bestehender `PreparationMonitor_SeparatesKonto_EventCarriesMonitoringKonto`
  sowie alle übrigen Bestandstests — PASS.

## Unit-Tests (`Softwareschmiede.Tests`)

`dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "FullyQualifiedName~GitHubEventSourceTests"`

Ergebnis: **101 bestanden, 0 fehlgeschlagen** — darunter neu:

- `Response_SeparatesAccount_BotViewer_PassesIdentityAndReconciles` — PASS:
  konto-konsistenter SeparatesKonto-Snapshot (ViewerId `BOT`) passiert die
  Identitätsprüfung von `ExecuteResponseAsync`; der Marker-Abgleich erkennt den
  Agent-Kommentar als eigenen (`AlreadyExisted`), der Benutzer-Client wird nicht
  berührt. Damit ist der End-to-end-Nachweis erbracht, dass der korrigierte
  Snapshot die frühere Fehlermeldung „Repository- oder Benutzeridentität hat
  sich geändert" nicht mehr auslöst.

## Format / Build

- `dotnet format Softwareschmiede.slnx --verify-no-changes` — sauber.
- `dotnet build` der betroffenen Projekte (`Softwareschmiede`, IntegrationTests,
  Tests) — 0 Warnungen, 0 Fehler.

## Zusammenfassung

Alle Akzeptanzszenarien des requirement.md sind testbelegt: konto-konsistenter
Snapshot im SeparatesKonto, unverändertes UserAccount-Verhalten, ehrliche
Fehlerfälle (fehlender Zugriff, Identitäts-Mismatch) und funktionierende
Remote-Ausführung inkl. Marker-Reconcile unter dem Bot-Konto. Die Ops-Reparatur
für Bestandsdaten ist dokumentiert.
