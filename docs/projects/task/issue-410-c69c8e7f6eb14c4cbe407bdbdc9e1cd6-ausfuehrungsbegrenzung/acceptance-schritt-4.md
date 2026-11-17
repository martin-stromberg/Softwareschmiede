# Abnahmeprüfung – Entwicklungsschritt 4

Geprüft: „SCM-Plugin-Schnittstelle zum Aktualisieren einer Issue-Beschreibung"
(#412 Grundlage), Commits `eb199db1` + `389b0702`, Diff gegen
`task/issue-410-c69c8e7f6eb14c4cbe407bdbdc9e1cd6-ausfuehrungsbegrenzung` (Schritte 1-3).

## Ergebnis

**Status:** Anforderung vollständig erfüllt

## Abweichungen

Keine.

## Hinweise

### Beleg der Akzeptanzkriterien (eigene Code-Lektüre + Testlauf)

- **AK 1 (Vertrag + abgleichsicherer Responder):** `ScmResponseKind` erhält
  `IssueBodyUpdate` als vierten Wert **am Enum-Ende** (`ScmEvents.cs:58`,
  numerisch 3 — persistierte `RequestJson`-Dokumente serialisieren das Enum
  numerisch über `JsonSerializerDefaults.Web`, kein `JsonStringEnumConverter`
  in `ScmEventStore.JsonOptions`; Altbestand der Werte 0-2 bleibt korrekt
  lesbar). `IScmEventResponder` ist signaturstabil; die Vertragsdoku schreibt
  den ehrlichen typisierten Fehler statt stiller Ignorierung fest
  (`IScmEventResponder.cs:5-9`). GitHub-Plugin: `ReconcileResponseAsync`
  verzweigt für die neue Art auf `ReconcileIssueBodyAsync`
  (`GitHubPlugin.EventResponses.cs:15-16, 94-104`), `ExecuteResponseAsync`
  auf das `("PATCH", issues/{n}, { body = composed })`-Tupel (Z. 66-83).
- **AK 2 (Remote-Schreiben + typisierte Fehler):** PATCH läuft über denselben
  `EventApiAsync`/`EvaluateResponse`-Pfad wie alle Mutationen
  (`mutation: true`): 401→`Authentication`, 403→`Permission`,
  429→`RateLimit` mit `RetryAfterUtc`, ≥500→`Temporary` mit
  `OutcomeUncertain`, 404→`Permanent` (`GitHubPlugin.Events.cs:362-381`);
  `HttpMethod(request.Method)` im Transport unterstützt PATCH
  (`GitHubHttpApiClient.cs:42`). Die Outbox bildet die Sperr-/Retry-Semantik
  ab (`ScmEventOutbox.cs:82-87`: Authentication/Permission/Permanent →
  `NextAttemptUtc = long.MaxValue`, sonst Backoff via `RetryAt`).
- **AK 3 (Outbox + deterministische ID + Tests):** Zweck `issuebody`,
  Aktions-ID `ScmEventStore.ActionId(eventId, "issuebody")` =
  `SHA256(eventId:D:purpose)[..16]` (`ScmEventStore.cs:34-35`) — gleiche
  Konvention wie `receipt`/`waiting`/`attachments`. Doppel-Enqueue ist
  unmöglich: Aktions-ID ist Primärschlüssel; `EnsureAsync` wiederverwendet
  nur bei identischem Ereignis+Zweck+Inhalt, sonst
  `ScmActionIdCollisionException` (`ScmEventOutbox.cs:18-45`). **Eigenständig
  nachgelaufen:** 13/13 Unit-Tests (`GitHubEventSourceTests.IssueBodyUpdate*`)
  und 5/5 Integrationstests (`ScmEventDispatchTests_IssueBodyUpdate`) grün —
  decken Ausführung, Reconcile ohne Doppelschreiben, Backoff-Wiederholung,
  Sperrfehler mit `RetryAsync`-Heilung und ehrlichen Fehler bei fehlender
  Plugin-Fähigkeit ab.

### Kritikpunkte im Einzelnen

- **(a) Titel wird nie gesendet:** PATCH-Payload ist `new { body = composed }`
  — kein `title`-Feld; `composed` = `Body + "\n\n" + Marker`
  (`ResponseBody`, Z. 177; Attachments sind für diese Art per
  `ValidateResponse` ohnehin unzulässig). Unit-Test
  `IssueBodyUpdate_Execute_PatchesDescriptionWithMarker` assertiert
  `TryGetProperty("title") == false`. ✅
- **(b) Reconcile-Robustheit:** `ReconcileIssueBodyAsync` liest
  `GET issues/{n}` und meldet `AlreadyExisted` nur bei Marker-Treffer im
  Remote-Body (Z. 101-103). Szenario „Body bereits identisch, aber ohne
  Marker": Reconcile liefert `null` → es wird ein PATCH gesendet — das ist
  **kein** Doppelschreiben im Sinne des Kriteriums, denn der Zielzustand
  (Beschreibung **mit** Aktionsmarker) ist noch nicht erreicht; der Marker
  ist zwingend, damit spätere Abgleiche den eigenen Schreibzugriff sicher
  wiederfinden. Semantisch identisch zum Kommentar-Marker, ausführlich im
  Code kommentiert (Z. 91-93). 404 auf dem GET → `Permanent` (ehrlicher
  Fehler). ✅
- **(c) Determinismus/Doppel-Enqueue:** gleiche Eingabe → gleiche GUID
  (SHA-256); PK-Kollision wird entweder konsistent wiederverwendet oder als
  ehrlicher Fehler geworfen — niemals still zweimal eingereiht. ✅
- **(d) Verallgemeinerter Hostaktions-Sweep:** Filter von
  `Purpose ∈ {"waiting","attachments"}` auf `!Purpose.StartsWith("ai:")`
  (`ScmEventOrchestrator.cs:147-151`). Bestandsaufnahme aller Zwecke im
  System: `receipt`, `waiting`, `attachments`, `issuebody`, `ai:*`.
  Neu eingeschlossen ist `receipt` — diese Aktion wird ohnehin unmittelbar
  davor in jeder Dispatch-Runde ausgeführt (Z. 139-146) und
  `ExecuteAsync` early-outed bei `Succeeded`/nicht fällig → kein
  Mehraufruf, keine Regression. `ai:`-Aktionen bleiben versuchsgebunden dem
  Exchange-Abgleich in `ReconcileAttemptAsync` (Z. 497-513) vorbehalten und
  werden weiterhin nicht hostseitig nachgeholt. Die Abschlussmengenprüfung
  (`x.AttemptId == item.DeliveryAttemptId`, Z. 516) bleibt unberührt —
  Hostaktionen tragen `AttemptId = null`. Die äußere Fälligkeitsauswahl
  (`DispatchDueEventsAsync`, Z. 40-44) ist zweckgenerisch und nimmt
  `issuebody`-Aktionen ohne weitere Änderung auf; abgebrochene Ereignisse
  bleiben ausgeschlossen. ✅
- **(e) Fehler-Semantik:** Sperre (401/403 → `long.MaxValue`) und Heilung
  über `RetryAsync` (setzt `NextAttemptUtc` aller nicht-`Succeeded`-Aktionen
  zurück, Z. 356-366) durch `IssueBodyAction_PermissionFailure_LocksUntilRetry`
  belegt; Retry/Backoff durch `TemporaryFailure_RetriesAfterBackoff`;
  „Plugin kann es nicht" durch Vertragsdoku +
  `default: throw Permanent` in `ValidateResponse` (Z. 173) und den
  Integrationstest `IssueBodyAction_UnsupportedPlugin_ReportsHonestError`
  belegt. ✅
- **(f) Persistenzkompatibilität:** Wert am Enum-Ende angehängt und im Code
  als Kompatibilitätsregel dokumentiert (Z. 54-57). ✅
- **(g) Flake `StartPruefung_UeberlappendeAusloeser_KeinDoppelstart`:**
  plausibel nicht vom Diff verursacht. Der einzige Dispatch-Pfad-Eingriff
  dieses Schritts ist der Sweep-Filter; der betroffene Schritt-3-Testpfad
  kennt ausschließlich `receipt`-Aktionen, deren Ausführung sich nicht ändert
  (ein zusätzlicher `ExecuteAsync`-Early-Return, kein Status-Einfluss).
  `Wartend` statt `Zugestellt` entsteht auf den Limit-/Block-Pfaden
  (`WaitAsync`), die der Diff nicht berührt; der Test ist nachweislich
  timingempfindlich (parallele `RunOnceAsync`-Aufrufe) und lief isoliert,
  klassenweise (7/7) und im Gesamt-Zweitlauf (336/336) grün. ✅

### Kleinigkeiten (Beobachtungen, keine Abweichungen)

- Vertragsdoku sagt, `Attachments` blieben bei `IssueBodyUpdate` „ohne
  Wirkung"; `ValidateResponse` lehnt nicht-leere Attachments für alle Arten
  außer `Comment`/`PullRequest` tatsächlich als `Permanent` ab
  (Z. 141-145) — strenger als der Wortlaut, aber konsistent zur
  `Reaction`-Behandlung und im Sinne von „ehrlicher Fehler statt stiller
  Ignorierung" sogar sicherer.
- Pro Ereignis existiert genau eine `issuebody`-Aktion: Ein erneutes
  `EnsureAsync` mit geändertem Body würde eine sichtbare
  `ScmActionIdCollisionException` (`VerarbeitungUngeklärt`) auslösen statt
  still zu ersetzen. Konsistent zur Hostaktions-Konvention; der spätere
  Aufrufer (Schritt 6) muss ggf. einen stabilen Inhalt oder einen anderen
  Zweck wählen — kein Defekt dieses Schritts.
- Kein produktiver Aufrufer der neuen Art — laut `requirement.md`
  ausdrücklich beabsichtigt (reine Schnittstellenerweiterung; Aufrufer folgt
  in Schritt 6). Die Fähigkeit ist ausschließlich über den Outbox-Pfad
  erreichbar, den die Integrationstests vollständig durchspielen.

### Selbst ausgeführte Verifikation

- `dotnet test src/Softwareschmiede.IntegrationTests/… --filter
  "FullyQualifiedName~ScmEventDispatchTests_IssueBodyUpdate"` — **5/5
  bestanden** (mit `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`, synchron,
  inkl. Build 0 Fehler).
- `dotnet test src/Softwareschmiede.Tests/… --filter
  "FullyQualifiedName~GitHubEventSourceTests.IssueBodyUpdate"` — **13/13
  bestanden**.

Kein Voll-Lane-Lauf durch den Prüfagenten; die dokumentierten Gesamtläufe
(`test-results.md`: reguläre Lane 2661 grün / 3 Plattform-Skips,
Integration 336/336 im Zweitlauf, Format-Check sauber) sind in den für
diesen Schritt relevanten Ausschnitten unabhängig bestätigt.
