# Plan-Review: SCM-Antwort-Art „Issue-Beschreibung aktualisieren" (Schritt 4)

**Status: Vollständig umgesetzt**

Geprüft wird die Implementierung gegen `plan.md` und die verbindlichen Akzeptanzkriterien des Projektplan-Schritts 4.

## Umsetzungsstand je Planelement

| Planelement | Stand | Beleg |
|-------------|-------|-------|
| §1 `ScmResponseKind.IssueBodyUpdate` am Enum-Ende + Vertragsdoku | ✅ | `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs` Z. 54-58 (Enum-Wert als vierter/letzter Eintrag), Z. 63-69 (Request-Doku: Body trägt neue Beschreibung, Title/Branches/Reaction/Attachments wirkungslos) |
| §1 `IScmEventResponder`-Doku: ehrlicher Fehler statt stiller Ignorierung | ✅ | `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IScmEventResponder.cs` Z. 5-9 |
| §2 `ValidateResponse`: Issue-Referenz + Body-Pflicht | ✅ | `plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.EventResponses.cs` Z. 160-164 |
| §2 `ReconcileIssueBodyAsync`: GET `issues/{n}`, Marker-Treffer → `AlreadyExisted` | ✅ | `GitHubPlugin.EventResponses.cs` Z. 91-104 + Einsprung Z. 15-16 |
| §2 `ExecuteResponseAsync`: `PATCH issues/{n}` mit markiertem Body | ✅ | `GitHubPlugin.EventResponses.cs` Z. 66-83 (`composed` inkl. `IssueBodyUpdate`, Methoden-Tupel `("PATCH", …)`) |
| §3 Nachhol-Schleife „alle Nicht-`ai:`-Zwecke" | ✅ | `src/Softwareschmiede/Application/Services/ScmEventOrchestrator.cs` Z. 147-151 (`!x.Purpose.StartsWith("ai:")`) |
| §4 Unit-Tests (5 Fälle) | ✅ | `GitHubEventSourceTests.cs` Z. 834-962 — 13 Fälle inkl. Theory-Ausprägungen, alle grün |
| §4 Integrationstests (5 Fälle) | ✅ | `src/Softwareschmiede.IntegrationTests/Services/ScmEventDispatchTests_IssueBodyUpdate.cs` — 5 Fälle, alle grün |

## Akzeptanzkriterien (Anforderung)

- ✅ `ScmResponseRequest`/`ScmResponseKind` erlauben das Issue-Beschreibungs-Update; `ExecuteResponseAsync` + `ReconcileResponseAsync` abgleichsicher (Reconcile-vor-Schreiben in `ScmEventOutbox.ExecuteAsync` + Marker-Abgleich im Plugin; kein Doppelschreiben — `AlreadyApplied`-Tests).
- ✅ GitHub-Plugin setzt die Beschreibung remote; Fehler als typisierte Providerfehler (401→Authentication, 403→Permission, 429→RateLimit+RetryAfter, 500→Temporary+OutcomeUncertain, 404→Permanent — nachgewiesen in `IssueBodyUpdate_Failures_TypedProviderErrors`; Sperr-/Wiederholsemantik in den Integrationstests `PermissionFailure_LocksUntilRetry`/`TemporaryFailure_RetriesAfterBackoff`).
- ✅ Ausführung ausschließlich über die Outbox mit deterministischer Aktions-ID (`ScmEventStore.ActionId(eventId, "issuebody")`, `EnsureAsync`-Kollisionsschutz ererbt); Ausführung, Reconcile und Fehlerfälle durch Unit-/Integrationstests belegt.

## Zusätzlich umgesetzt (kleine, scope-nahe Ergänzung)

- `ScmEventQueueViewModel.ActionLabel`: Beschreibungstext für den Zweck `issuebody` in der Ereignisübersicht-Diagnose — ohne die Ergänzung wären Fehler dieser Art fälschlich als „KI-Antwort" beschriftet worden (`src/Softwareschmiede.App/ViewModels/ScmEventQueueViewModel.cs` Z. 338).

## Akzeptanzszenarien-Status

1. Happy Path (deterministische ID, ein Schreibaufruf, `Succeeded`+`RemoteId`) — ✅ nachgewiesen.
2. Reconcile nach Neustart/Ausfall (kein Doppelschreiben) — ✅ nachgewiesen (Unit + Integration).
3. Ungewisser Ausgang → `Uncertain`, nur Abgleich — ✅ Outbox-Semantik unverändert geerbt; 500-Fall `OutcomeUncertain` unit-gedeckt.
4. Sperrfehler → `long.MaxValue`, Freigabe per `RetryAsync` — ✅ nachgewiesen.
5. Plugin ohne Fähigkeit → ehrlicher typisierter Fehler — ✅ nachgewiesen (Vertragsdoku + Integrationstest).

## Offene Aufgaben

Keine.
