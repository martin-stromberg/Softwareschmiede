# Umsetzungsplan: Automatische Aufgabenanlage und -wiederverwendung aus qualifizierten Ereignissen

## Übersicht

Aufgabenlose SCM-Referenzen erzeugen heute Snapshots mit `UnassignedDiagnostic`. Neu: Enthält ein eingelesener `ScmEventBatch` einer unzugeordneten Referenz mindestens eine qualifizierte Beobachtung (`ScmEventValidationService.GetRejection == null`), löst ein neuer servicenaher Schritt (`ScmEventTaskResolver`) die Referenz zu einer Aufgabe auf — Wiederverwendung bei genau einem Treffer, Anlage bei keinem Treffer, Mehrdeutigkeitsdiagnose bei mehreren. Inbox-Aufnahme, Validierung, Dedup, Checkpoints und Dispatch bleiben unverändert.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Anlagedaten (Titel, PR-Branch) | Zwei neue Methoden `ReadIssueAsync`/`ReadPullRequestAsync` auf `IScmEventSource`, Rückgabe der bestehenden VOs `Issue`/`PullRequest` | `ScmEventObservation` trägt bewusst nur Textstand + Identität; Anlagedaten werden erst bei Bedarf (einmalig pro Referenz) geladen und mappen 1:1 auf die vorhandenen `AufgabeService`-Bausteine |
| Auflösungsort | Neuer scoped Service `ScmEventTaskResolver`, aufgerufen aus `ScmEventMonitoringService` zwischen `ReadEventsAsync` und `AcceptBatchAsync` (nicht innerhalb der Store-Transaktion) | Der Resolver braucht Remote-Aufrufe (Detaildaten) und `AufgabeService`; Remote-Calls gehören nicht in die Inbox-Transaktion |
| Anlass der Auflösung | Nur wenn `taskIds.Length == 0` **und** mindestens eine Beobachtung des Batches `GetRejection == null` liefert | Nicht qualifizierte Ereignisse erzeugen weder Aufgabe noch Inbox-Eintrag; Snapshot-/Diagnoseverhalten bleibt |
| Reihenfolge im Resolver | Erst Detaildaten laden (`ReadIssueAsync`/`ReadPullRequestAsync`), dann Treffer suchen, dann anlegen | `PullRequest.Provider` aus dem VO liefert den Provider für `PullRequestRepositoryId.Normalize` — kein inverses Prefix→Provider-Mapping nötig, auch in Tests mit freiem Prefix nutzbar |
| Rennschutz Issues | Neue nullable `IssueReferenz.SourceKey` (kanonischer Schlüssel `{provider}/{host}/{repositoryId}/Issue/{number}`, Unique-Index, EF-Migration) + `DbUpdateException`-Muster | Entspricht dem `AlertReferenz.SourceKey`-Vorbild; deckt Anlagerennen instanzübergreifend ab |
| Rennschutz PRs | Bestehender Unique-Index `(Provider, RepositoryId, PullRequestNumber)` + `InvalidOperationException` aus `CreateFromPullRequestAsync` wird im Resolver abgefangen → Re-Query → vorhandene Aufgabe | Wiederverwendung des vorhandenen Musters |
| Aufgabenanlage Issues | Neue `AufgabeService.CreateFromIssueEventAsync(projektId, issue, gitRepositoryId, sourceKey, ct)`; bei Unique-Konflikt wird die vorhandene Aufgabe zurückgegeben (Muster von `CreateFromAlertAsync`) | `CreateFromIssueAsync`-Signatur bleibt unverändert; UI-Pfad unberührt |
| Aufgabenanlage PRs | Unverändertes `CreateFromPullRequestAsync` mit `ScmRepositoryContext(gitRepository.Id, gitRepository.PluginTyp, Normalize(provider, "{Owner}/{Name}"))` | Inkl. `CleanPullRequestBody` und Rennschutz |
| Auftragstext | Resolver komponiert `Issue.Body`/`PullRequest.Body` neu: Referenz-Body (falls abweichend vom auslösenden Text), dann auslösende Beobachtung `Body`, dann `Url` als Herkunftsverweis | „Titel plus qualifizierter Text mit Quellverweis" ohne Beschreibungsduplikat bei `Description`-Triggern |
| Lokales Repository für die Anlage | `Dictionary<ScmEventRepository, List<GitRepository>>` im Monitor (Record-Gleichheit), deterministisch erstes Element nach `RepositoryUrl` (OrdinalIgnoreCase), dann `Id` | Mehrere lokale Aliase desselben kanonischen Repos sind möglich; deterministische Wahl, `ProjektId` daraus |
| Verschwundene Referenz (`ReadXxxAsync` → `null`) | Ergebnis `Unassigned` (bisheriger Diagnosepfad) | Selbstkorrigierend: Enumeration liefert die Referenz nicht mehr |
| Fehler bei Detailabruf/Anlage | Propagieren → `RecordFailureAsync(sourceKey)` (bestehender Backoff/Diagnosepfad) | Typisierte Providerfehler und Wiederholung wie bei Lesefehlern |

## Programmabläufe

### Auflösung und Anlage im Poll-Lauf

1. `ScmEventMonitoringService.RunOnceAsync` löst Alias-Gruppen kanonisch auf und füllt zusätzlich `localRepositories[ScmEventRepository]` mit den zugeordneten lokalen `GitRepository`-Datensätzen (dedupliziert nach `Id`).
2. Pro Referenzgruppe/ContentKind wird der Batch gelesen. Ist `taskIds.Length == 0` und `batch.Observations` enthält eine qualifizierte Beobachtung (`trigger`), wird `ScmEventTaskResolver.ResolveAsync(reference, source, trigger, candidates, ct)` aufgerufen.
3. `ResolveAsync`:
   - Lädt Detaildaten (`ReadIssueAsync`/`ReadPullRequestAsync`); `null` → `Unassigned`.
   - Issue: Kandidaten = `Aufgaben` mit `IssueReferenz.IssueNummer == reference.Number` und `GitRepositoryId ∈ candidates`.
   - PR: `provider = pull.Provider`; `repositoryId = Normalize(provider, "{Owner}/{Name}")`; Kandidaten = `PullRequestReferenzen` mit Schlüssel `(provider, repositoryId, number)` → AufgabeIds.
   - Genau 1 → `Assigned(id)`; >1 → `Ambiguous`; 0 → Anlage.
   - Anlage Issue: `issue with { Body = Compose(...) }` → `CreateFromIssueEventAsync` (SourceKey) → `Assigned`.
   - Anlage PR: `pull with { Body = Compose(...) }` → `CreateFromPullRequestAsync`; `InvalidOperationException` → Re-Query auf den PR-Schlüssel → `Assigned` oder Rethrow.
4. Monitor bildet das Ergebnis ab: `Assigned` → `AcceptBatchAsync(id, …, unassigned: false)`; `Ambiguous` → `AcceptBatchAsync(null, …, unassigned: false)`; `Unassigned` → `AcceptBatchAsync(null, …, unassigned: true)`.
5. Folgebatches derselben Referenz (andere ContentKinds, Folgepolls) finden die neue Aufgabe über den Resolver-Re-Check bzw. die `linked`-Liste des nächsten Laufs — keine Duplikate.

Beteiligte Klassen: `ScmEventMonitoringService`, `ScmEventTaskResolver`, `ScmEventStore`, `AufgabeService`, `IScmEventSource`, `ScmEventValidationService`.

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `ScmEventTaskResolver` | Service (scoped) | „Finde passende Aufgabe oder lege sie an" je `ScmEventReference` |
| `ScmTaskResolution` + `ScmTaskResolutionKind` | Record + Enum (`Assigned`/`Ambiguous`/`Unassigned`) | Ergebnis der Auflösung |
| `ScmEventMonitoringServiceTests_TaskAssignment` | Testklasse | Anlage/Wiederverwendung/Dedup/nicht qualifizierte Fälle/Fehlerpfad |

## Änderungen an bestehenden Klassen

### `IScmEventSource` (Interface)

- **Neue Methoden:**
  - `Task<Issue?> ReadIssueAsync(ScmEventReference reference, CancellationToken ct = default)` — Issue-Anlagedaten (Titel, Body, Labels, Milestone, URL); `null` wenn die Referenz nicht (mehr) existiert.
  - `Task<PullRequest?> ReadPullRequestAsync(ScmEventReference reference, CancellationToken ct = default)` — PR-Anlagedaten inkl. Quell-/Ziel-Branch, SHA, Quell-Repository; `null` wenn nicht (mehr) existiert.

### `GitHubPlugin` (`GitHubPlugin.Events.cs`)

- **Neue Methoden:** `ReadIssueAsync` (REST `repos/{o}/{n}/issues/{number}` → `Issue`; `pull_request`-Feld vorhanden → `null`) und `ReadPullRequestAsync` (REST `repos/{o}/{n}/pulls/{number}` → `PullRequest` über den aus `AddOpenPullRequest` extrahierten Parser ohne Open-State-Filter).

### `ScmEventMonitoringService`

- **Geänderte Methode:** `RunOnceAsync` — `localRepositories`-Map beim Auflösen befüllen; im Batch-Schritt bei `taskIds.Length == 0` + qualifizierter Beobachtung den Resolver aufrufen und Ergebnis auf `aufgabeId`/`unassigned` abbilden.

### `AufgabeService`

- **Neue Methode:** `CreateFromIssueEventAsync(Guid projektId, Issue issue, Guid gitRepositoryId, string sourceKey, CancellationToken ct)` — Vorabprüfung per `SourceKey`, Anlage mit `IssueReferenz.SourceKey`, `DbUpdateException` → Re-Query per `SourceKey` → vorhandene Aufgabe zurückgeben (Muster `CreateFromAlertAsync`).

### `IssueReferenz` (Entity)

- **Neue Eigenschaft:** `SourceKey` (`string?`, `HasMaxLength(700)`, Unique-Index) — kanonischer Dedup-Schlüssel `{provider}/{host}/{repositoryId}/Issue/{number}` (segmentweise `Uri.EscapeDataString`, wie `ContentKey`).

### `SoftwareschmiededDbContext`

- `IssueReferenz`-Konfiguration: `SourceKey`-Property + Unique-Index.

### `ScmE2EApiClient` (E2E-Simulator)

- Beantwortet Enumeration (`issues(`/`pullRequests(` mit `variables.cursor`), Issue-/PR-Detail (`title` im Node bzw. `GET …/pulls/{n}`); `ScmE2EText` erhält optionales `Titel`.

### `ScmDispatchFixture` / `App.xaml.cs` (DI)

- `ScmEventTaskResolver` als scoped registrieren; Fixture-Defaults für `ReadIssueAsync`/`ReadPullRequestAsync` (`null`).

## Datenbankmigrationen

| Migrationsname | Betroffene Tabellen/Spalten | Beschreibung |
|----------------|----------------------------|--------------|
| `AddIssueReferenzSourceKey` | `IssueReferenzen.SourceKey` (TEXT, nullable, max 700) + Unique-Index | Dedup-Schlüssel für ereignisgesteuerte Issue-Anlagen; Bestandszeilen bleiben `NULL` |

Generierung: `dotnet ef migrations add AddIssueReferenzSourceKey --project src/Softwareschmiede`.

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| `ScmTaskResolution` | `Assigned` nur mit nicht-leerer `AufgabeId` | Programmierfehler → `InvalidOperationException` |
| Issue-Anlage | `IssueNummer` aus Referenz (Pluginwert wird nicht übernommen, wenn inkonsistent) | Inkonsistente Plugin-Antwort → Fehler via `RecordFailureAsync` |
| PR-Anlage | unveränderte Regeln aus `CreateFromPullRequestAsync` | `InvalidOperationException` → Re-Query/Backoff |

## Konfigurationsänderungen

Keine.

## Seiteneffekte und Risiken

- **API-Budget:** Pro unzugeordneter Referenz mit qualifiziertem Ereignis ein zusätzlicher Detailabruf — einmalig bis zur Anlage (danach `linked`-Pfad).
- **Dispatch:** Neu angelegte `Neu`-Aufgaben werden von der bestehenden Dispatch-Pipeline gestartet (Clone/CLI); ohne Standard-CLI → bestehender „Keine Standard-CLI konfiguriert"-Wartekommentar.
- **E2E-Simulator:** Bisher nicht beantwortete Enumerationsaufrufe schlugen im E2E kontrolliert fehl; nach Erweiterung ändern sich Aufrufzahlen (`CallsPath`) — `WaitForPollAsync` (≥ +8 Aufrufe) bleibt kompatibel.
- **Restrisiko Issue-Anlagerennen:** Eine parallel über den UI-Pfad (`CreateFromIssueAsync`, ohne `SourceKey`) angelegte Aufgabe wird vom Unique-Index nicht erfasst; das Fenster ist interaktiv und durch die UI-seitige Duplikatprüfung abgedeckt.
- **Bestandsschutz:** `taskIds.Length ≥ 1`-Pfad, Validierung, `EventKey`-/Snapshot-Dedup und Checkpoint-Verhalten bleiben unverändert.

## Umsetzungsreihenfolge

1. **`IssueReferenz.SourceKey` + DbContext-Konfiguration + EF-Migration**
   - Voraussetzungen: `dotnet ef` (vorhanden), `SoftwareschmiededDbContextDesignTimeFactory`
   - Entity-Property, `OnModelCreating`-Konfiguration, `dotnet ef migrations add AddIssueReferenzSourceKey --project src/Softwareschmiede`
2. **`IScmEventSource` erweitern + `GitHubPlugin` implementieren**
   - Voraussetzungen: keine
   - `ReadIssueAsync`/`ReadPullRequestAsync`; Parser-Extraktion aus `AddOpenPullRequest`/Issue-Parser
3. **`AufgabeService.CreateFromIssueEventAsync`**
   - Voraussetzungen: Schritt 1 (SourceKey)
4. **`ScmEventTaskResolver` + DI-Registrierung (App + `ScmDispatchFixture`)**
   - Voraussetzungen: Schritte 2–3
5. **`ScmEventMonitoringService` verdrahten**
   - Voraussetzungen: Schritt 4
6. **Tests: Integrationstests `ScmEventMonitoringServiceTests_TaskAssignment`, `AufgabeServiceTests`-Erweiterung, `GitHubEventSourceTests`-Erweiterung, Migrations-Test**
   - Voraussetzungen: Schritte 1–5
7. **E2E-Simulator erweitern + E2E-Szenario Aufgabenanlage**
   - Voraussetzungen: Schritte 1–6
8. **Dokumentation (`docs/help/plugins/github-ereignisse/`, `changes.log`, README)**
   - Voraussetzungen: Schritte 1–7

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `Monitor_QualifiedEventOnNewIssue_CreatesTaskAndInboxEntry` | `ScmEventMonitoringServiceTests_TaskAssignment` | Issue-Beschreibung mit Trigger → genau eine Aufgabe (IssueReferenz 9, Projekt/GitRepository korrekt, Titel aus `ReadIssueAsync`, Beschreibung mit Text+URL), Inbox-Eintrag verknüpft |
| `Monitor_QualifiedCommentOnUnassignedIssue_CreatesTaskWithSource` | dito | Kommentar-Trigger → Aufgabe enthält Referenz, Beschreibung enthält Kommentartext + Herkunfts-URL |
| `Monitor_QualifiedPullRequestEvent_CreatesReviewSourceTask` | dito | PR-Ereignis → Aufgabe mit `PullRequestReferenz` (`ReviewSource`, Provider/RepoId/Number) |
| `Monitor_RepeatedPollsAndBatches_CreateSingleTask` | dito | Zwei `RunOnceAsync`-Läufe + qualifizierte Beschreibung und Kommentar → genau eine Aufgabe, keine EventKey-/Aufgaben-Duplikate |
| `Monitor_UnqualifiedEvent_KeepsUnassignedWithoutTask` | dito | Kein Trigger → keine Aufgabe, `UnassignedDiagnostic`, `ReadIssueAsync` nie aufgerufen |
| `Monitor_ResolutionFailure_RecordsCheckpointFailure` | dito | `ReadIssueAsync` wirft `ScmProviderException(Temporary)` → Checkpoint `ErrorCode`/`NextAttemptUtc`, keine Aufgabe |
| `Monitor_VanishedReference_StaysUnassigned` | dito | `ReadIssueAsync` → `null` → `UnassignedDiagnostic`, keine Aufgabe |
| `CreateFromIssueEventAsync_*` | `AufgabeServiceTests` (oder `AufgabeServiceTests_IssueEvents`) | Anlage mit `SourceKey`; zweiter Aufruf mit gleichem Schlüssel liefert dieselbe Aufgabe; abweichender Schlüssel legt neue an |
| `ReadIssue_ReadsTitleLabelsMilestone` / `ReadIssue_PullRequestNode_ReturnsNull` / `ReadPullRequest_ReadsHeadAndBase` | `GitHubEventSourceTests` | Detail-Parsing inkl. PR-Guard |
| `MigrateAsync_IssueReferenzSourceKey_*` | `WorkdirMigrationTests` (Konvention prüfen) | Spalte + Unique-Index auf-/abwärts |
| Fixture-Defaults `ReadIssueAsync`/`ReadPullRequestAsync` | `ScmDispatchFixture` | `null`-Defaults, überschreibbar |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `ScmDispatchFixture` | Neue Interface-Methoden brauchen Mock-Defaults (loose Mock liefert sonst `null`-Tasks) |
| `ScmE2EApiClient` / `ScmE2EFixture` | Neue Transportaufrufe (Enumeration, Detail) müssen beantwortet werden |
| `ScmEventMonitoringServiceTests_RepoWide.Monitor_ActiveRepositoryWithoutTask_…` | Erwartung „keine Inbox" bleibt nur für nicht qualifizierte Beobachtungen gültig — Fixture dort liefert qualifizierte Kommentare (`$Softwareschmiede test`); Verhalten ändert sich: Es wird jetzt eine Aufgabe angelegt. Test muss auf nicht qualifizierte Beobachtungen oder neue Erwartung umgestellt werden. |

### E2E-Tests (Pflicht)

Hintergrundfeature ohne neue UI-Interaktion; die E2E-Absicherung wird in die bestehende konsolidierte Methode integriert:

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| Qualifizierter Text auf unzugeordneter Issue-Referenz → Aufgabe wird angelegt (DB), erscheint in der Aufgabenverwaltung des Projekts, Ereignis läuft in Inbox/Dispatch | `E2E_ScmEvents` (`ScmEventsE2ETests`), als Schritt in der bestehenden konsolidierten Methode | Anlage genau einer Aufgabe mit Referenz + Sichtbarkeit |

| Bestehende E2E-Anpassung | Grund |
|--------------------------|-------|
| `ScmE2EApiClient` | Enumeration- und Detail-Endpunkte + `title` beantworten |
| `ScmE2EText`/`ScmE2EFixture` | Optionales `Titel`; neue Szenario-Referenz ohne Aufgabe |

## Offene Punkte

Keine.
