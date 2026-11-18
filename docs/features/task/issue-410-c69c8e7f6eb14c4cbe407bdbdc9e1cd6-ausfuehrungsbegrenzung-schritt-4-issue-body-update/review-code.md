# Code-Review: SCM-Antwort-Art „Issue-Beschreibung aktualisieren" (Schritt 4)

**Status: Keine Befunde**

Geprüfter Diff (Basis `task/issue-410-c69c8e7f6eb14c4cbe407bdbdc9e1cd6-ausfuehrungsbegrenzung`):

- `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs`
- `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IScmEventResponder.cs`
- `plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.EventResponses.cs`
- `src/Softwareschmiede/Application/Services/ScmEventOrchestrator.cs`
- `src/Softwareschmiede.App/ViewModels/ScmEventQueueViewModel.cs`
- `src/Softwareschmiede.Tests/Infrastructure/Plugins/GitHubEventSourceTests.cs`
- `src/Softwareschmiede.IntegrationTests/Services/ScmEventDispatchTests_IssueBodyUpdate.cs` (neu)

## Geprüfte Aspekte

- **Persistenz-Kompatibilität:** `IssueBodyUpdate` steht als letzter Enum-Wert (numerisch 3); persistierte `RequestJson`-Dokumente mit den Werten 0-2 behalten ihre Bedeutung. ✅
- **Validierungsreihenfolge:** `ValidateResponse` läuft in Execute- und Reconcile-Pfad vor jedem Transportaufruf — PR-Referenzen und leere Bodies scheitern als `Permanent` ohne Remotezugriff (Test: `Assert.Empty(api.Requests)`). ✅
- **Reconcile-Korrektheit:** `ReconcileIssueBodyAsync` liest das Issue-Objekt (`GET issues/{n}`) und sucht ausschließlich den Aktions-Marker — bewusst ohne Viewer-Vergleich, weil der Issue-Autor nicht das Ereignis-Konto ist; `Required(issue, "id")` + `SafeResponseUrl` liefern die gleiche Herkunftsprüfung der Ergebnis-URL wie die anderen Arten. Fehler des GET laufen typisiert über `EvaluateResponse` (404 → `Permanent` — ehrlich, kein stilles „nicht gefunden" → Doppelschreiben). ✅
- **Schreibsemantik:** `PATCH` läuft über `EventApiAsync(…, mutation: true)` — ungewisser Ausgang wird als `Temporary`+`OutcomeUncertain` typisiert, die Outbox stellt die Aktion dann auf `Uncertain` (nur Abgleich, kein automatischer Neuversand). `ComposeBodyAsync` liefert `Body + Marker` (Attachments sind für diese Art validiert unmöglich → kein Upload-Pfad betreten). `title` wird im Request-Body nicht gesendet. ✅
- **Orchestrator-Filter:** `!x.Purpose.StartsWith("ai:")` wird von EF Core nach `NOT LIKE 'ai:%'` übersetzt (SQLite-Provider); `ai:`-Aktionen bleiben dem versuchsgebundenen `ReconcileAttemptAsync` vorbehalten, `receipt`/`waiting`/`attachments`/künftige Hostzwecke werden nachgeholt. Der zusätzliche `receipt`-Durchlauf ist durch `ExecuteAsync`-Early-Out (`Succeeded`/nicht fällig) idempotent harmlos. ✅
- **Konto-Routing:** `ClientFor(repository.Konto)` gilt in beiden neuen Pfaden — SeparatesKonto läuft über den Agent-Client (Theory-Test belegt). ✅
- **Vertrag/CS1591:** Neue öffentliche API-Elemente tragen XML-Doku; `dotnet format --verify-no-changes` sauber; Build 0 Warnungen/0 Fehler. ✅
- **Ehrlicher Fehler:** Vertrag festschrieben; GitHub wirft bei unbekannten/unvollständigen Arten weiterhin `Permanent` statt still zu ignorieren. ✅

## Geprüfte und bewusst akzeptierte Punkte (keine Befunde)

- **Exchange-Protokoll (`action.Kind`-Mapping `ReconcileAttemptAsync` Z. 492):** bewusst unverändert — Agenten-Aktionsarten sind ein eigenes Vokabular; das Beschreibungs-Update ist eine Host-Aktion, die Schritt 6 aus dem Analyse-Ergebnis erzeugt (Entscheidung 5: „hostseitig in die Outbox-Aktionen überführt"). Kein neuer Agenten-Trigger.
- **Marker im Issue-Body:** konsistent zum Kommentar-Muster und Voraussetzung für `AlreadyExisted`-Erkennung; Nebeneffekt gewollt — `AddObservation` überspringt `<!-- softwareschmiede:`-Texte des Viewers (Schleifenschutz für Schritt 5/6). Bei `SeparatesKonto` (Issue-Autor ≠ Bot) erzeugt die geänderte Beschreibung weiterhin eine reguläre Beobachtung — die Schleifensicherung liegt dann in der Schritt-5-Erstbeobachtungslogik, nicht in diesem Schritt.
- **Edit-Race:** Entfernt ein Benutzer den Marker nachträglich aus der Beschreibung, würde ein erneuter Abgleich die Aktion erneut schreiben — identische Restsemantik wie bei gelöschten Kommentar-Markern; konsistent und akzeptiert.
- **`ActionLabel`-Fallback:** `issuebody` erhielt ein eigenes Label, damit Fehlerdiagnosen die betroffene Aktion korrekt benennen.

## Befunde

Keine.
