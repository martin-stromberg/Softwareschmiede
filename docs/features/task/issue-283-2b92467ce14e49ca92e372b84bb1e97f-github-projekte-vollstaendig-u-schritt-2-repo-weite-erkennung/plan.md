# Umsetzungsplan: Repository-weite GitHub-Ereigniserkennung

## Übersicht

Der `ScmEventMonitoringService` wird um eine repository-weite Referenzermittlung erweitert: `IScmEventSource` erhält eine neue Enumerationsmethode, die für ein aufgelöstes `ScmEventRepository` alle zu überwachenden `ScmEventReference`s liefert (offene Issues/PRs plus geschlossene Referenzen innerhalb eines beweglichen Aktivitätsfensters). Die nummerierten Referenzen laufen anschließend durch den unveränderten `ReadEventsAsync`-/`AcceptBatchAsync`-Pfad — Metadaten-Vollständigkeit, Validierung, Dedup und Checkpoint-/Backoff-Mechanik greifen damit per Konstruktion. Die Aufgabenzuordnung bleibt unverändert: Referenzen ohne lokale Aufgabe erzeugen Snapshots mit Zuordnungsdiagnose, aber keine Inbox-Einträge (bewusste Zwischenstufe).

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Vertragsform der Enumeration | Neue Methode `EnumerateReferencesAsync(ScmEventRepository repository, DateTimeOffset? sinceUtc, CancellationToken)` auf `IScmEventSource`, Rückgabe `IReadOnlyList<ScmEventReference>` | Die Referenzidentitäten werden in den bestehenden referenzbasierten `ReadEventsAsync`-Pfad eingespeist; dadurch liefern neu erkannte Referenzen automatisch dieselben `ScmEventObservation`-Metadaten (Autor/Editor, node_id, Zeitstempel). Die flachen `GetIssuesAsync`/`GetOpenPullRequestsAsync`-APIs bleiben ungenutzt. Kein zweiter Beobachtungsmechanismus. |
| Grenze geschlossener Referenzen | Immer: alle `OPEN`-Issues und `OPEN`-PRs. Zusätzlich: `CLOSED`-Issues und `CLOSED`/`MERGED`-PRs nur, wenn `updatedAt >= sinceUtc`. `sinceUtc` = letztes Enumerations-Watermark − 2×`PollInterval`; beim allerersten Lauf `null` → nur offene Referenzen | Ein Trigger-Kommentar an einer geschlossenen Referenz ändert deren `updatedAt` und wird damit ab dem folgenden Poll entdeckt, ohne die gesamte Repositoriums-Historie zu scannen. Altbestände werden nicht vollständig enumeriert (Performance), neu auftretende Aktivität an Geschlossenem geht nicht verloren. |
| Auflösung der Repo-Aliase | Projekt-`GitRepository`-Zeilen (`Aktiv == true`) werden in dieselbe Alias-Gruppierung/Auflösung wie aufgabenverknüpfte Referenzen aufgenommen (`repository/{prefix}/{alias}`-Checkpoint, `complete=false` bei unauflösbarem Alias) | Exakter bestehender Ambiguitäts-/Diagnosepfad; Token-/Auflösungsfehler landen kontrolliert im selben Sperr- und „Quellen erneut prüfen"-Rahmen. |
| Enumerations-Checkpoint | Eigener `ScmObservationCheckpoint`-Schlüssel `enumeration/{provider}/{host}/{repoId}` mit `WatermarkUtc` (Basis des `sinceUtc`-Fensters) und Backoff über `RecordFailureAsync`; Misserfolg der Enumeration setzt `complete` **nicht** zurück | Enumerationsfehler erzeugen keine Zuordnungsambiguität (Referenzen tragen kanonische Identität); das Überspringen einzelner Repos bis `NextAttemptUtc` ist der bestehende Backoff-Mechanismus. Fehlschlag sperrt nicht die gesamte Provider-Runde. |
| Zuordnung referenzbasierter vs. repo-weiter Referenzen | `resolved`-Liste führt `Guid? TaskId` (`null` = repo-weit ohne lokale Aufgabe); kanonische Gruppierung merged Aufgaben- und Enumerations-Einträge | Dedup und Mehrdeutigkeitserkennung (>1 Task) bleiben identisch; `taskIds` = distinct der nicht-leeren TaskIds. |
| Diagnose „keine Aufgabe" | `AcceptBatchAsync` erhält optionalen `unassigned`-Parameter; `aufgabeId == null && unassigned` → Snapshot-/Checkpoint-Diagnostic „Keine Aufgabe zugeordnet" statt „Mehrdeutige Zuordnung zu Aufgaben" | Fachlich korrekte Diagnose in der Queue-UI; der vorhandene „mehrdeutig"-Pfad bleibt unverändert. `IdentityResolved = false` bewirkt weiterhin Vollscan beim Folgepoll — Auflösung heilt von selbst, sobald eine Aufgabe entsteht. |
| GitHub-Implementierung | GraphQL-Abfragen in `GitHubPlugin.Events.cs`: paginiert `issues`/`pullRequests` mit `states` + `orderBy: UPDATED_AT DESC`, Felder `number`, `updatedAt`, `state` (issues); geschlossene PRs clientseitig abbrechen, sobald `updatedAt < sinceUtc` (Sortierung garantiert Abbruchkorrektheit) | Wiederverwendung von `GraphAsync`, `RequiredArray`, `NextCursor`; `filterBy.since` existiert nur für Issues — einheitlicher clientseitiger Fensterabbruch für beide Typen ist einfacher und korrekt. |

## Programmabläufe

### Monitorlauf mit repo-weiter Erkennung (`ScmEventMonitoringService.RunOnceAsync`)

1. Wie bisher: `startup.WaitAsync`, Scope, `db.Aufgaben` mit aktivem `GitRepository` laden → `linked`-Liste.
2. **Neu:** `db.GitRepositories.Where(x => x.Aktiv)` laden (alle an Projekten erfassten aktiven Repos, Aufgabe nicht erforderlich).
3. Pro Plugin mit `IScmEventSource`: Alias-Gruppen bilden aus der **Vereinigung** von `linked` (pro Aufgaben-Referenz, Alias = `RepositoryId`-Feld) und den Projekt-Repos (Alias = `RepositoryUrl`), gefiltert auf `PluginPrefix`/`PluginName`-Match — Nicht-GitHub-Plugins überspringen implizit alle ihre Repos.
4. Pro Alias-Gruppe: bestehender `repository/`-Checkpoint → bei gesperrtem `NextAttemptUtc` `complete=false`; sonst `GetEventRepositoryAsync` → kanonisches `ScmEventRepository`. Aufgaben-Referenzen der Gruppe → `resolved` mit `TaskId`.
5. **Neu:** Pro erfolgreich aufgelöstem Alias, dessen kanonisches Repo noch nicht enumeriert wurde (`HashSet` auf (Provider, Host, RepositoryId)): `enumeration/`-Checkpoint prüfen; falls fällig `EnumerateReferencesAsync(repo, sinceUtc)` mit `sinceUtc = checkpoint.WatermarkUtc − 2×PollInterval` bzw. `null`. Ergebnis-Referenzen → `resolved` mit `TaskId = null`. Checkpoint: `WatermarkUtc = now`, Failures=0, NextAttemptUtc/ErrorCode/Diagnostic zurücksetzen. Fehler → `RecordFailureAsync(enumerationKey)` (kein `complete=false`).
6. Unverändert: `if (!complete) continue;` — unauflösbare Aliase (auch Repo-Aliase) blockieren die Provider-Runde.
7. Unverändert: Gruppierung `resolved` nach (Provider, Host, RepoId, Kind, Number); `taskIds` = distinct nicht-leere TaskIds; `aufgabeId` = genau eine → TaskId, null sonst; `unassigned = taskIds.Length == 0`. Pro ContentKind `ReadEventsAsync` → `AcceptBatchAsync(aufgabeId, sourceKey, batch, unassigned, ct)`; rescan-Check auf `!IdentityResolved` bleibt.
8. Unverändert: Dispatch-Selektion und `RecordFailureAsync`.

Beteiligte Klassen: `ScmEventMonitoringService`, `ScmEventStore`, `IScmEventSource`, `GitHubPlugin`, `ScmObservationCheckpoint`, `ScmContentSnapshot`, `ScmIncomingEvent`.

### GitHub-Enumeration (`GitHubPlugin.EnumerateReferencesAsync`)

1. `ValidateReference`-ähnliche Repo-Prüfung (ProviderPrefix/Host/Segmente via `ParseEventRepository`).
2. Paginierte GraphQL-Abfrage `repository{issues(first:100,states:[OPEN],orderBy:...,after:$c){nodes{number} pageInfo{...}}}` → `ScmEventReference(Issue, number)`.
3. Analog `pullRequests(states:[OPEN])` → `ScmEventReference(PullRequest, number)`.
4. Falls `sinceUtc` gesetzt: `issues(states:[CLOSED],orderBy:{field:UPDATED_AT,direction:DESC})` paginieren, `updatedAt`-Feld lesen, Abbruch sobald `updatedAt < sinceUtc`; gleiches für `pullRequests(states:[CLOSED,MERGED],...)`.
5. Fehler laufen über `GraphAsync`/`SendEventApiAsync` → typisierte `ScmProviderException` (bestehender Pfad).

Beteiligte Klassen: `GitHubPlugin`, `IGitHubEventApiClient`, `ScmEventReference`, `ScmEventRepository`.

## Neue Klassen

Keine neuen Entitäten/Klassen. Neue Member:

| Klasse/Interface | Typ | Zweck |
|------------------|-----|-------|
| `IScmEventSource.EnumerateReferencesAsync` | Interface-Methode | Repo-weite Referenzermittlung |
| `GitHubPlugin.EnumerateReferencesAsync` (+ private Helfer) | Methode | GitHub-GraphQL-Implementierung |
| `ScmEventStore.AcceptBatchAsync`-Parameter `unassigned` + Diagnostic-Konstanten | Methode/Konstanten | Unterscheidung „keine Aufgabe" vs. „mehrdeutig" |

## Änderungen an bestehenden Klassen

### `IScmEventSource` (Interface)

- **Neue Methoden:** `EnumerateReferencesAsync(ScmEventRepository repository, DateTimeOffset? sinceUtc, CancellationToken ct = default)` → `Task<IReadOnlyList<ScmEventReference>>` — liefert Referenzen inkl. kanonischer `ScmEventRepository`-Identität; `sinceUtc == null` ⇒ nur offene Referenzen.

### `GitHubPlugin` (Plugin, `GitHubPlugin.Events.cs`)

- **Neue Methoden:** `EnumerateReferencesAsync` — siehe Programmablauf; interne Helfer für paginierte Referenzlisten (`number`/`updatedAt`/`state`).

### `ScmEventMonitoringService` (Service)

- **Geänderte Methoden:** `RunOnceAsync` — zusätzliche `db.GitRepositories`-Abfrage; Alias-Gruppen um Repo-Einträge erweitern; Enumerationsaufruf + `enumeration/`-Checkpoint-Verwaltung; `resolved` trägt `Guid? TaskId`; `taskIds`-Bildung filtert `null`; `AcceptBatchAsync`-Aufruf mit `unassigned`-Flag. Klassenkommentar aktualisieren.
- Kein neuer Mechanismus: Polling-Intervall, `SinceUtc`-Konvention, Backoff, „Quellen erneut prüfen" (bestehende Checkpoint-Tabelle) gelten unverändert.

### `ScmEventStore` (Service)

- **Geänderte Methoden:** `AcceptBatchAsync(Guid? aufgabeId, string sourceKey, ScmEventBatch batch, bool unassigned = false, CancellationToken ct = default)` — bei `aufgabeId == null` Diagnostic = `UnassignedDiagnostic` („Keine Aufgabe zugeordnet") falls `unassigned`, sonst bisherige „Mehrdeutige Zuordnung zu Aufgaben"; Snapshot-/Checkpoint-Logik analog.
- **Neue Konstanten:** `public const string AmbiguousAssignmentDiagnostic` / `UnassignedDiagnostic` (ersetzen die gestreuten Literale; Rescan-Check im Monitor nutzt die Konstanten bzw. `!IdentityResolved`).

### `ScmDispatchFixture` (Testinfrastruktur)

- `Git.As<IScmEventSource>()` um `EnumerateReferencesAsync`-Setup (Default: leere Liste) ergänzen — sonst `null`-Task → `NullReferenceException` in `RunOnceAsync`-nutzenden Tests.

## Datenbankmigrationen

Keine — Enumerations-Checkpoints nutzen die vorhandene `ScmObservationCheckpoints`-Tabelle mit neuem `SourceKey`-Präfix.

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| `ScmRepositoryContext`/`ScmEventRepository` | bestehende `ValidateReference`/`ParseEventRepository`-Regeln | `ScmProviderException(Permanent)` — bestehender Diagnosepfad |
| Enumerierte `number` | `> 0`, sonst Referenz verwerfen/Permanent-Fehler | ungültige Providerantwort |
| Geschlossene Referenzen | nur `updatedAt >= sinceUtc` | außerhalb des Fensters → nicht enumeriert |

## Konfigurationsänderungen

Keine.

## Seiteneffekte und Risiken

- **API-Volumen:** Pro aktivem Projekt-Repo zusätzliche paginierte GraphQL-Abfragen pro Poll plus `ReadEventsAsync` für alle neu entdeckten Referenzen (für jede neue Referenz vollständiger Content-Read). Rate-Limit-Fehler laufen in den bestehenden Backoff; `sinceUtc`-Fenster begrenzt die geschlossene Menge.
- **Unzugeordnete Referenzen** erzeugen `IdentityResolved = false`-Snapshots → Vollscan bei jedem Folgepoll (bestehende Semantik für unaufgelöste Zuordnungen; absichtlich, damit nachträglich angelegte Aufgaben die Referenz übernehmen).
- **`ScmDispatchFixture`:** neue Interface-Methode muss gemockt werden, sonst schlagen alle `RunOnceAsync`-nutzenden Tests fehl.
- **Bestehende Integrationstests** (`ScmEventDispatchTests`, `ScmEventMonitoringServiceTests_AmbiguityRecovery`): projekt-erfasste Repos werden nun enumeriert — mit leerem Enumerations-Mock bleibt das Verhalten gleich; zusätzlicher `enumeration/`-Checkpoint verändert ggf. Checkpoint-Anzahl-Assertionen (`Assert.Equal(2, checkpoints.Count)` bzw. `4` müssen den neuen Schlüssel tolerieren oder gefiltert zählen); `Assert.All(checkpoints, Diagnostic == ...)` muss den `enumeration/`-Schlüssel ausnehmen.

## Umsetzungsreihenfolge

1. **`IScmEventSource.EnumerateReferencesAsync` deklarieren**
   - Voraussetzungen: keine.
   - Beschreibung: Interface-Methode + XML-Doku in `IScmEventSource.cs`.
2. **`GitHubPlugin.EnumerateReferencesAsync` implementieren**
   - Voraussetzungen: Schritt 1.
   - Beschreibung: GraphQL-Enumeration open/closed mit Fenster, Pagination, typisierte Fehler.
3. **`ScmEventStore.AcceptBatchAsync` um `unassigned` + Konstanten erweitern**
   - Voraussetzungen: keine.
   - Beschreibung: optionale Signaturerweiterung, Diagnostic-Auswahl, vorhandene Literale durch Konstanten ersetzen.
4. **`ScmEventMonitoringService.RunOnceAsync` erweitern**
   - Voraussetzungen: Schritte 1–3.
   - Beschreibung: Repo-Laden, Alias-Vereinigung, Enumeration + Checkpoint, `Guid?`-TaskIds, `unassigned`-Flag, Klassenkommentar.
5. **Testinfrastruktur anpassen** (`ScmDispatchFixture`, betroffene Integrationstest-Assertionen)
   - Voraussetzungen: Schritt 1 (Mock-Setup), Schritt 4 (Checkpoint-Zählungen).
   - Beschreibung: `EnumerateReferencesAsync`-Default-Setup; `enumeration/`-Checkpoints in Anzahl-/All-Assertionen berücksichtigen.
6. **Neue Tests schreiben** (Plugin-Enumeration, Monitor-Integration)
   - Voraussetzungen: Schritte 2, 4, 5.
   - Beschreibung: siehe Tests-Abschnitt.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|---------------------|------------|-------------------------------------|
| `Enumeration_ReturnsOpenIssuesAndPullRequests` | `GitHubEventSourceTests` (oder neue `GitHubEventEnumerationTests`) | `EnumerateReferencesAsync` liefert `Issue`/`PullRequest`-Referenzen mit kanonischem Repo; Pagination über `pageInfo`/`after`. |
| `Enumeration_IncludesRecentlyUpdatedClosed_Only` | dto. | `sinceUtc`-Fenster: geschlossene Referenzen mit `updatedAt >= since` kommen mit, ältere nicht; `sinceUtc == null` → nur offene. |
| `Enumeration_TypedFailure` | dto. | HTTP-/GraphQL-Fehler → `ScmProviderException` (bestehender Pfad). |
| `Monitor_EnumeratesRepoWithoutTask_StoresSnapshotsWithoutInbox` | neue `ScmEventMonitoringServiceTests` (Integration, `ScmDispatchFixture`) | Aktives Projekt-Repo ohne Aufgabe: `EnumerateReferencesAsync` wird aufgerufen, `ReadEventsAsync` für entdeckte Referenzen läuft, Snapshots mit „Keine Aufgabe zugeordnet"-Diagnostic, **keine** `ScmIncomingEvents`. |
| `Monitor_EnumeratedRefWithTask_EntersInboxUnchanged` | dto. | Repo-Enumeration findet Referenz, die per `IssueReferenz` einer Aufgabe gehört → Inbox-Eintrag mit `AufgabeId`, normale Validierung. |
| `Monitor_RepeatedPolls_ProduceNoDuplicates` | dto. | Zweiter `RunOnceAsync` erzeugt keine doppelten Snapshots/Events; `enumeration/`-Checkpoint-Watermark fortgeschrieben. |
| `Monitor_InactiveOrNonGitHubRepo_NotEnumerated` | dto. | `Aktiv == false` bzw. Plugin ohne `IScmEventSource` → kein Enumerations-/Read-Aufruf. |
| `Monitor_EnumerationFailure_UsesBackoffWithoutBlockingRefs` | dto. | `EnumerateReferencesAsync` wirft `Temporary` → `enumeration/`-Checkpoint mit `NextAttemptUtc`, aufgabenverknüpfte Referenzen werden trotzdem gelesen. |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `ScmEventDispatchTests` (`Monitor_RepositoryAliasesForDifferentTasks_…`, `…_UnresolvedAlias_…`, `…_DifferentReferences_…`) | Zusätzlicher `enumeration/`-Checkpoint-Zeile in `ScmObservationCheckpoints`; Fixture-Mock für `EnumerateReferencesAsync` nötig. Assertionen auf Checkpoint-Anzahl/Diagnostic-„All" filtern auf nicht-`enumeration`-Schlüssel. |
| `ScmEventMonitoringServiceTests_AmbiguityRecovery` | Gleiches: `Assert.All(checkpoints …)` darf den neuen Enumerations-Checkpoint nicht mitprüfen (Diagnostic dort `null`). |
| `ScmEventPersistenceTests` / `ScmDispatchFixture.AddAsync` | `AcceptBatchAsync`-Signatur erhält optionalen Parameter — Aufrufe kompilierkompatibel, kein Zwang zur Änderung (optionaler Parameter). |
| `ScmEventOrchestratorTests` (`IScmEventSource`-Mock) | Neues Interface-Member: Setup nur nötig, wenn `RunOnceAsync` erreicht wird — Fixture deckt Standard ab; ggf. lokales Setup ergänzen. |

### E2E-Tests (Pflicht)

Keine neuen Benutzerinteraktionen in diesem Schritt (Zwischenstufe ohne UI-/Verhaltensänderung an zustellbaren Ereignissen). Die fachliche Abdeckung erfolgt über die oben genannten Integrations- und Plugin-Tests; die bestehenden `E2E_ScmEvents`-Tests bleiben unverändert und decken den End-to-End-Pfad der Aufgaben-Referenzen weiter ab.

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| Bestehender SCM-Ereignis-E2E-Pfad (Aufgaben-Referenzen) | `E2E_ScmEvents` (unverändert) | Unverändertes Verhalten für Referenzen mit Aufgabe |

Bestehende E2E-Tests müssen nicht angepasst werden.

## Offene Punkte

Keine.
