# Übersetzte Anforderung: SCM-Plugin-Schnittstelle zum Aktualisieren einer Issue-Beschreibung (Entwicklungsschritt 4, Grundlage für Issue #412)

**Quelle:** Projektplan `docs/projects/task/issue-410-c69c8e7f6eb14c4cbe407bdbdc9e1cd6-ausfuehrungsbegrenzung/project-plan.md`, „Schritt 4" + Vorgehensentscheidung 5 (Issue-Body-Update über die SCM-Plugin-Schnittstelle/Outbox); GitHub-Issue #412.
**Basisbranch (Diff-Basis):** `task/issue-410-c69c8e7f6eb14c4cbe407bdbdc9e1cd6-ausfuehrungsbegrenzung`
**Vorarbeiten (bereits gemergt):** Schritt 1 (Plugin-Fallback, aktivierungsbewusste Auflösung), Schritt 2 (Ausführungsbegrenzung mit Priorisierung, `ScmEventExecutionLimitService`), Schritt 3 (ereignisgesteuerte Startprüfung, `ScmStartPruefungsService`, `ScmEventOrchestrator.DispatchDueEventsAsync`).

## Fachliche Zusammenfassung

Die Anwendung erhält die Fähigkeit, die **Beschreibung eines Issues** in einem überwachten Repository über den etablierten, abgeglichenen Antwortkanal zu ändern — als neue Antwort-Art im SCM-Vertrag (`ScmResponseKind`), ausführbar über `IScmEventResponder` inklusive Reconcile, und eingebettet in das Outbox-Gerüst mit deterministischer Aktions-ID, Wiederholung und Backoff. Das GitHub-Plugin implementiert den Schreibzugriff (`PATCH issues/{n}`) inklusive des bestehenden typisierten Fehlermodells (`ScmProviderException`: Authentication/Permission → Sperre, Temporary/RateLimit → Wiederholung). Der **Issue-Titel ist von der Schnittstelle ausdrücklich nicht betroffen**. SCM-Plugins ohne diese Fähigkeit liefern einen **ehrlichen Fehler** statt stiller Ignorierung.

Dieser Schritt ist **reine Schnittstellen-/Infrastruktur-Erweiterung**: Die Aufrufer-Funktion (Issue-Aufbereitung, die die Schnittstelle nutzt) folgt in Schritt 6. Die Fähigkeit muss trotzdem durch Tests nachgewiesen sein — Mock-/Integrationsebene genügt, kein echter GitHub-Call.

## Fachlicher Scope

- **Verarbeitungseinheit:** Eine neue Antwort-Art im bestehenden Outbox-/Responder-Rahmen — ein `ScmResponseRequest` mit `Kind == IssueBodyUpdate`, der über `ScmEventOutbox.EnsureAsync`/`ExecuteAsync` persistiert, ausgeführt, bei Bedarf wiederholt und per `ReconcileResponseAsync` abgeglichen wird.
- **Auslösende Ereignisse:** In diesem Schritt noch kein produktiver Aufrufer — die Einbettung wird über Tests nachgewiesen (Enqueue + Dispatch); der fachliche Aufrufer folgt in Schritt 6.
- **Erwartete Wirkung:**
  - `ScmResponseRequest`/`ScmResponseKind` erlauben ein Issue-Beschreibungs-Update; `IScmEventResponder` (`ExecuteResponseAsync` + `ReconcileResponseAsync`) unterstützt die Art abgleichsicher: kein Doppelschreiben, Erkennung bereits angewendeter Aktionen.
  - Das GitHub-Plugin setzt die Beschreibung remote (`PATCH /repos/{owner}/{repo}/issues/{number}`); Fehler werden als typisierte Providerfehler mit korrekter Wiederhol-/Sperrsemantik zurückgemeldet.
  - Ausführung ausschließlich über die Outbox mit deterministischer Aktions-ID (konsistent zu `receipt`/`waiting`-Aktionen); der Orchestrator holt auch diese Host-Aktionsart unabhängig vom Zustellstatus nach.
- **Umgang mit fehlenden Objekten:**
  - Issue remote nicht mehr vorhanden (404) → typisierter `Permanent`-Fehler (kein automatischer Neuversand).
  - Plugin ohne `IssueBodyUpdate`-Fähigkeit → ehrlicher, typisierter Fehler statt stiller Ignorierung.
  - Bereits angewendete Aktion (Marker im Remote-Body) → `AlreadyExisted`, kein zweiter PATCH.
- **Scope-Ausschlüsse:**
  - Der Issue-Titel wird nicht verändert (explizites Nicht-Ziel). *(Status: bestätigt — verbindliche Projektplan-/Issue-Vorgabe.)*
  - Pull-Request-Beschreibungen sind nicht Teil dieser Antwort-Art (eigener Remote-Endpunkt, nicht vom Issue gefordert). *(Status: bestätigt — die Anforderung spricht ausschließlich von Issues; Annahme A1.)*
  - Kein Aufrufer der neuen Art in Produktivcode — die Aufbereitungs-Funktion folgt in Schritt 6; in diesem Schritt liegt die Auslösung bei der Outbox-Infrastruktur bzw. Tests. *(Status: bestätigt — Aufgabenstellung: „reine Schnittstellen-/Infrastruktur-Erweiterung".)*
  - Keine UI-Änderung, keine neue Einstellung, keine EF-Migration. *(Status: bestätigt — betroffene Bereiche sind Vertrag, Plugin, Outbox/Reconcile, Tests.)*

## Betroffene Klassen und Komponenten

- `ScmEvents.cs` (`src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs`): `ScmResponseKind` (Z. 55, derzeit `Comment, Reaction, PullRequest`), `ScmResponseRequest` (Z. 61-68, `Body`, `ActionId`, `Marker`), `ScmResponseResult` (Z. 71), `ScmProviderException`/`ScmProviderFailureKind` (Z. 83-103).
- `IScmEventResponder` (`src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IScmEventResponder.cs`): `ReconcileResponseAsync`/`ExecuteResponseAsync` — signaturstabil, Vertragsdokumentation erweitern.
- `GitHubPlugin.EventResponses.cs` (`plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.EventResponses.cs`): `ValidateResponse` (Z. 114-148), `ReconcileResponseAsync` (Z. 12-38), `ExecuteResponseAsync` (Z. 41-82), `ResponseBody` (Z. 150), `SafeResponseUrl` (Z. 342-350).
- `GitHubPlugin.Events.cs` (`plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.Events.cs`): `EventApiAsync`/`SendEventApiAsync`/`EvaluateResponse` (Z. 301-382 — typisierte Statusmatrix, OutcomeUncertain bei Mutationen), `RepositoryEndpoint` (Z. 450), `ClientFor` (Z. 334-341, Konto-Routing).
- `ScmEventOutbox` (`src/Softwareschmiede/Application/Services/ScmEventOutbox.cs`): `EnsureAsync` (deterministische ID, Kollisionsschutz), `ExecuteAsync` (Reconcile-vor-Schreiben, Status `Sending`/`Uncertain`/`Failed`/`Succeeded`, Backoff über `ScmEventStore.RetryAt`, harte Sperre bei Authentication/Permission/Permanent).
- `ScmEventOrchestrator.DispatchCoreAsync` (`src/Softwareschmiede/Application/Services/ScmEventOrchestrator.cs` Z. 139-149): Hostaktionen-Nachhol-Schleife — derzeit nur `Purpose ∈ {"waiting","attachments"}`; eine neue Host-Aktionsart muss miterfasst werden.
- `ScmEventStore.ActionId` (`src/Softwareschmiede/Application/Services/ScmEventStore.cs` Z. 33-35): deterministische Aktions-ID `SHA256(eventId:D:purpose)[..16]` — Muster für die neue Art.
- Tests: `src/Softwareschmiede.Tests/Infrastructure/Plugins/GitHubEventSourceTests.cs` + `src/Softwareschmiede.Tests/Helpers/FakeGitHubEventApi.cs` (Plugin-Unit-Tests mit ausgetauschtem Transport), `src/Softwareschmiede.IntegrationTests/Services/ScmEventDispatchTests*.cs` + `src/Softwareschmiede.Tests/Helpers/ScmDispatchFixture.cs` (Outbox-/Dispatch-Integrationstests).

## Implementierungsansatz

- Neuer `ScmResponseKind`-Wert **am Ende** der Enum (persistierte `RequestJson` serialisiert das Enum numerisch — Umnummerierung würde Altbestand falsch lesen); `Body` trägt die neue Beschreibung, `Title` bleibt ungenutzt.
- GitHub-Plugin: `ValidateResponse` fordert Issue-Referenz + nicht-leeren `Body`; `ReconcileResponseAsync` lädt `GET issues/{number}` und erkennt die bereits angewendete Aktion am Aktions-**Marker** im Remote-Body (gleiches Prinzip wie Kommentar-Marker — nebenbei markiert der Marker die selbst geschriebene Fassung für die Selbst-Ausschluss-Regel `<!-- softwareschmiede:` in `AddObservation`); `ExecuteResponseAsync` sendet `PATCH issues/{number}` mit `body = Beschreibung + Marker` unter der bestehenden Mutationen-Fehlersemantik (OutcomeUncertain bei ungewissem Schreibausgang).
- Orchestrator: Die Nachhol-Schleife für Hostaktionen wird von der Zweck-Whitelist (`waiting`/`attachments`) auf „alle Nicht-`ai:`-Zwecke" umgestellt, damit jede hostseitige Aktion — inklusive der neuen Art und künftiger Zwecke — unabhängig vom Zustellstatus wiederholt/abgeglichen wird; `ai:`-Aktionen bleiben dem versuchgebundenen Exchange-Abgleich vorbehalten.
- Kein Aufrufpunkt in Produktivcode; Tests legen die Aktion über `ScmEventOutbox.EnsureAsync` mit deterministischer ID (`ScmEventStore.ActionId(eventId, "issuebody")` — Zweck-Konvention `"issuebody"`, die Schritt 6 übernimmt) an und dispatchen.

## Konfiguration

Keine neue Konfiguration, keine Migration, keine UI-Änderung.

## Offene Fragen

Keine — alle Entscheidungen sind durch den verbindlichen Projektplan (Schritt 4 + Entscheidung 5) abgedeckt; Detailentscheidungen (Enum-Name, Marker-Trägerschaft, Zweck-Filter) werden in `plan.md` als dokumentierte Annahmen festgelegt.
