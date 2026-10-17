# Übersetzte Anforderung: Repository-weite GitHub-Ereigniserkennung

Schritt 2 von Issue #283 · Branch: `task/issue-283-2b92467ce14e49ca92e372b84bb1e97f-github-projekte-vollstaendig-u-schritt-2-repo-weite-erkennung`

## Fachliche Zusammenfassung

Der `ScmEventMonitoringService` baut seine Beobachtungsliste heute ausschließlich aus SCM-Referenzen, die bereits an lokalen `Aufgaben`-Datensätzen hinterlegt sind. Die Erkennungsseite wird repository-weit erweitert: Für jedes an einem `Projekt` erfasste `GitRepository` mit `Aktiv == true` und GitHub als Provider werden alle relevanten Referenzen (offene Issues und Pull Requests, plus eine fachlich zu begründende Grenze für geschlossene Referenzen) enumeriert und zusätzlich zu den aufgabenbasierten Referenzen in dieselbe Überwachung aufgenommen. Neu erkannte Referenzen müssen dieselben `ScmEventObservation`-Metadaten liefern wie der bestehende Pfad, damit Trigger-/Owner-Validierung, Snapshot-/`EventKey`-Dedup und Checkpoint-/Backoff-Mechanik unverändert greifen. Die Aufgabenzuordnung bleibt in diesem Schritt bewusst unverändert: Ereignisse ohne zuordenbare Aufgabe werden weiterhin nicht als zustellbare Inbox-Einträge wirksam (Folgeschritt).

## Betroffene Klassen und Komponenten

- **Interfaces (Plugin-Verträge, `src/Softwareschmiede.Plugin.Contracts/`):**
  - `IScmEventSource` (`Domain/Interfaces/IScmEventSource.cs`): Erweiterung um eine Enumerationsmethode für alle Referenzen eines Repos, z. B. `EnumerateReferencesAsync(...)` bzw. ein „ReadEventsAsync auf Repo-Ebene", das pro Referenz vollständige Metadaten liefert (nicht die flachen `Issue`/`PullRequest`-Listen von `IGitPlugin`).
  - Value Objects in `Domain/ValueObjects/ScmEvents.cs`: ggf. neues Request-/Result-Typ für die Repo-Enumeration (`ScmEventReadRequest`/`ScmEventBatch`-Erweiterung oder eigener Typ).
- **GitHub-Plugin (`plugins/Softwareschmiede.Plugin.GitHub/`):**
  - `GitHubPlugin.Events.cs`: Implementierung der repo-weiten Enumeration (z. B. GraphQL-Abfrage `repository { issues(...) pullRequests(...) }` mit den `TextFields`, damit Autor-/Editor-Identität, `node_id`, Zeitstempel und URL wie im Referenzpfad vorliegen).
  - `GhEventApiClient`/`IGitHubEventApiClient`: wiederverwendbarer Transport, ggf. Pagination.
- **Anwendungsschicht (`src/Softwareschmiede/Application/Services/`):**
  - `ScmEventMonitoringService.cs` (RunOnceAsync ~Z. 20-140): Beobachtungsliste zusätzlich aus projekt-erfassten `GitRepository`-Datensätzen (`Aktiv == true`, GitHub-Plugin) speisen; Dedup der Referenzliste (Referenzen mit Aufgabe nicht doppelt lesen); bestehender Checkpoint-/Backoff-/Dispatch-Rahmen bleibt.
  - `ScmEventStore.AcceptBatchAsync`: unverändert; Aufgaben-Auflösung für repo-weit erkannte Referenzen (keine Aufgabe → heute „mehrdeutig/nicht auflösbar"-Pfad; bewusste Zwischenstufe).
- **Datenmodell (`src/Softwareschmiede/Domain/Entities/`):** voraussichtlich keine neuen Entitäten; bestehende `ScmContentSnapshot`/`ScmObservationCheckpoint`/`ScmIncomingEvent` werden wiederverwendet. `sourceKey`/Checkpoint-Granularität ggf. an repo-weite Quellen anzupassen (zu prüfen).
- **Tests (`src/Softwareschmiede.Tests/`):** Unit-/Integrationstests für Enumeration, Metadaten-Vollständigkeit, Dedup, Checkpoint-Verhalten, unverändertes Verhalten bei Aufgaben-Referenzen, Überspringen nicht-GitHub/inaktiver Repos.

## Implementierungsansatz

- Erweiterungspunkt: `IScmEventSource` um eine repo-weite Enumeration (Annahme: neuer Vertrag, da `ReadEventsAsync` feste `Number` verlangt). Kandidaten: GraphQL `issues(states:[OPEN,CLOSED])`/`pullRequests(states:[OPEN,...])` mit identischem Feldset wie im `Description`-Pfad, paginiert.
- `ScmEventMonitoringService.RunOnceAsync`: zusätzlich zu den Aufgaben-Referenzen die Projekt-Repositories laden (`GitRepository`-Tabelle, `Aktiv == true`, Projekt-Zuordnung, GitHub-Plugin), pro Repo `GetEventRepositoryAsync` → repo-weite Enumeration → Referenzen mit den vorhandenen `ScmEventReadRequest`-Pfaden zusammenführen (deduplizieren auf (Provider, Host, RepoId, Kind, Number)).
- Die flachen `IGitPlugin`-APIs `GetIssuesAsync`/`GetOpenPullRequestsAsync` werden **nicht** verwendet (fehlende Autor-/Editor-/node_id-Metadaten).
- Geschlossene Referenzen: fachlich begründete Grenze festlegen (z. B. nur offene Issues/PRs, oder geschlossene seit letztem Watermark-Fenster) — Entscheidung im Plan dokumentieren.
- Kein neuer Überwachungsmechanismus: Polling-Intervall, `SinceUtc`-Watermark, Backoff/`NextAttemptUtc`, „Quellen erneut prüfen" gelten unverändert für alle Quellen.
- Nicht-GitHub-Plugins, nicht aktivierte oder nicht an Projekten erfasste Repos werden wie bisher übersprungen.

## Konfiguration

Keine neue Konfiguration gefordert. `ScmEventOptions.PollInterval` (Sektion `ScmEvents`, 60 s) gilt weiterhin für den gesamten Monitorlauf.

## Offene Fragen / Annahmen

- Grenze für geschlossene Referenzen (nur offene? geschlossene innerhalb eines Zeitfensters?) — im Plan fachlich begründen (Vorschlag: nur offene + kürzlich geschlossene via `updatedAt`-Filter, um Kommentare an frisch geschlossenen Referenzen nicht zu verlieren; Aufwand/Nutzen abwägen).
- Exakte Vertragsform der Enumeration (eigene Methode vs. erweiterter `ScmEventReadRequest` ohne `Number`) — Entscheidung im Plan.
- Wie repo-weit erkannte Referenzen ohne Aufgabe im `AcceptBatchAsync`-Pfad behandelt werden (Diagnose-Marker analog „Mehrdeutige Zuordnung" oder neuer Grund) — unter der Zwischenstufen-Regel festlegen, ohne die Aufgabenzuordnung zu ändern.
