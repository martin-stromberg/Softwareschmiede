# Umsetzungsplan: SCM-Ereignis an aktive Aufgabe desselben Kontexts zustellen statt Doppelaufgabe

## Übersicht

`ScmEventTaskResolver.ResolvePullRequestAsync` wird um eine Kontextsuche erweitert: Nach dem
bestehenden `PullRequestReferenzen`-Lookup und vor `CreateFromPullRequestAsync` werden **aktive
Aufgaben** im Repository-Scope gesucht, deren `BranchName` dem Head-Branch des Pull Requests
entspricht oder deren `IssueReferenz.IssueNummer` von einer Closing-Direktive (`Closes #N` u. a.)
im PR-Body referenziert wird. Genau ein Kandidat → `Assigned`; mehrere unterschiedliche Kandidaten
→ `Ambiguous`; kein Kandidat → bisherige Anlage einer `ReviewSource`-Aufgabe. Betroffen ist
ausschließlich `src/Softwareschmiede/Application/Services/ScmEventTaskResolver.cs` plus einer
Extraktionshilfe in `PullRequestBodyBuilder`; Datenbankschema, Konfiguration und die
Dispatch-Pipeline bleiben unverändert.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Erweiterungspunkt | Kontextsuche im `ScmEventTaskResolver`, nicht in der `linked`-Liste des `ScmEventMonitoringService` | Der Resolver wird von allen Eingangspfaden erreicht (repository-weite Enumeration, verknüpfte Quellen, Inbox-Notifications im `SeparatesKonto`-Modus). Ein Branch-Match in der `linked`-Liste deckte nur den Enumerationspfad ab und müsste dieselbe Kandidatenlogik zusätzlich duplizieren. |
| Definition „aktive Aufgabe" | `Status ∉ {Beendet, Archiviert}` (d. h. `Neu`, `Gestartet`, `Wartend` und pausierte Aufgaben) | Spiegelt exakt die Startsperre-Semantik, die `ScmEventOrchestrator.BlockReason` (Zeilen 469–471) bereits anwendet, sowie den Kandidatenfilter der `ScmTaskCompletionMonitoringService`. Ein einer pausierten Aufgabe zugeordnetes Ereignis wartet regulär („Aufgabe pausiert; Start gesperrt."), statt eine Doppelaufgabe zu erzeugen. Kein Erfordernis einer lebenden CLI-Sitzung oder `AusfuehrungsStatus == Aktiv`: `DispatchCoreAsync` startet/neustartet die Sitzung bei Bedarf. Die engere Definition `AktivOderWartendStatus` = {`Gestartet`, `Wartend`} würde `Neu`-Aufgaben ausschließen und so für sie wieder Doppelaufgaben erzeugen — sie ist ein Seitenleisten-Kriterium, keine Startsperre. |
| Referenzbasierte Lookups | `PullRequestTaskIdsAsync` und der `IssueReferenz`-Lookup in `ResolveIssueAsync` bleiben statusagnostisch unverändert | Eine persistierte Referenz ist Identität, kein Kontextschluss. Das bewusste Verhalten, Ereignisse an blockierte Aufgaben zuzuordnen und warten zu lassen (abgesichert durch `Monitor_ReusedBlockedTask_StaysWaitingWithoutStart`), bleibt erhalten. Der Statusfilter gilt nur für die neue, schwächere Kontextsuche. |
| Konkurrierende Signale | Vereinigungsmenge ohne feste Signal-Priorität; zeigen die Signale auf unterschiedliche Aufgaben → `Ambiguous` | Die Anforderung verlangt bei Mehrdeutigkeit die bestehende `AmbiguousAssignmentDiagnostic`. Ein Rang (z. B. Branch vor Issue) könnte eine tatsächlich uneindeutige Lage verschleiern und eine falsche Zuordnung erzeugen. |
| Branch-Match | Nur für Same-Repository-PRs (`PullRequest.SourceRepositoryId` == normalisierte `repositoryId`, `OrdinalIgnoreCase`) und nur bei nicht-leerem `PullRequest.BranchName`; Vergleich `Aufgabe.BranchName == PullRequest.BranchName` mit `OrdinalIgnoreCase` | App-erzeugte Task-Branches werden in das eigene Remote gepusht; gleichnamige Branches in Forks oder Fremd-Repositories sind ein schwaches Signal und würden Fehlzuordnungen erzeugen. |
| Referenz-Persistenz („Selbstheilung") | Keine implizite `PullRequestReferenz`-Anlage bei Kontextzuordnung | Eine vorschnell persistierte Referenz würde `ScmTaskCompletionMonitoringService.DetermineClosureAsync` auf PR-Vorrang umschalten, bevor der PR tatsächlich als Ergebnis der Aufgabe bestätigt ist (Fremd-PR-Gefahr). Die CLI meldet den PR regulär über `pullRequestNumber` in `complete.json` → `SaveReportedAsync`; Folgeereignisse bis dahin finden denselben Treffer deterministisch erneut über die Kontextsuche. |
| `ResolveIssueAsync` | Unverändert | Die Doppelaufgaben-Lücke existiert im Issue-Pfad nicht: Der `IssueReferenz`-Lookup ist bereits eindeutig referenzbasiert. Die Anforderung nennt den PR-Fall; eine nachträgliche „nur-aktiv"-Einschränkung würde bestehendes, testabgesichertes Warteverhalten ändern. |
| Beendete Aufgabe als einziger Kontexttreffer | Zählt nicht als Kandidat → Anlage einer neuen `ReviewSource`-Aufgabe wie bisher | Direkte Folge der gewählten Aktivdefinition. Eine Zuordnung an `Beendet`/`Archiviert` würde das Ereignis mangels Startsperre nie zustellen. |
| Qualifizierte Issue-Direktiven im PR-Body | Extraktion aller Closing-Direktiven; repository-qualifizierte Direktiven (`owner/repo#N`) zählen nur bei Gleichheit mit der normalisierten `repositoryId` | Verhindert Fehlzuordnungen durch Closing-Direktiven, die Issues anderer Repositories meinen. |

## Programmabläufe

### Auflösung eines Pull-Request-Ereignisses mit Kontextsuche

1. `ScmEventMonitoringService.RunOnceAsync` findet eine unzugeordnete Referenz
   (`taskIds.Length == 0`) mit qualifizierter Beobachtung und ruft
   `ScmEventTaskResolver.ResolveAsync` → `ResolvePullRequestAsync` (unverändert).
2. `source.ReadPullRequestAsync(reference, ct)` liefert `pullRequest`; bei `null` oder
   `pullRequest.Nummer != reference.Number` → `ScmTaskResolution.Unassigned` (unverändert).
3. `repositoryId` wird aus `reference.Repository.Owner/Name` via
   `PullRequestRepositoryId.Normalize` normalisiert; `PullRequestTaskIdsAsync` liefert
   persistierte Referenztreffer: `>1` → `Ambiguous`, `==1` → `Assigned` (unverändert, stärkstes
   Signal, wird vor der Kontextsuche ausgewertet).
4. **Neu:** `FindActiveContextTaskIdsAsync(pullRequest, repositories, repositoryId, ct)`:
   1. Lädt Kandidaten: `db.Aufgaben` (`AsNoTracking`, `Include(a => a.IssueReferenz)`) mit
      `repositories.Select(x => x.Id)` enthaltend `a.GitRepositoryId` und
      `a.Status != Beendet && a.Status != Archiviert`.
   2. **Branch-Match:** nur wenn `pullRequest.BranchName` nicht leer ist und
      `pullRequest.SourceRepositoryId` der normalisierten `repositoryId` entspricht
      (`OrdinalIgnoreCase`): Kandidaten mit `BranchName` gleich `pullRequest.BranchName`
      (`OrdinalIgnoreCase`).
   3. **Issue-Match:** `PullRequestBodyBuilder.ExtractClosingIssueNumbers(pullRequest.Body, repositoryId)`
      liefert die referenzierten Issue-Nummern; Kandidaten mit
      `IssueReferenz.IssueNummer` in dieser Menge.
   4. Vereinigung beider Treffermengen, `Distinct()` auf `Aufgabe.Id`.
5. Ergebnissemantik: `>1` → `ScmTaskResolution.Ambiguous` (Monitoring setzt
   `ScmEventStore.AmbiguousAssignmentDiagnostic`); `==1` → `Assigned(taskId)`; `0` → bisheriger
   Pfad: `ChooseRepository`, `ScmRepositoryContext`-Aufbau, `ComposeBody`,
   `aufgaben.CreateFromPullRequestAsync` inkl. `InvalidOperationException`-Retry über
   `PullRequestTaskIdsAsync` als Anlagerennen-Auflösung (unverändert).

Beteiligte Klassen/Komponenten: `ScmEventTaskResolver`, `PullRequestBodyBuilder`,
`AufgabeService` (`CreateFromPullRequestAsync`), `IScmEventSource`, `SoftwareschmiededDbContext`,
`ScmEventMonitoringService`, `ScmEventStore`.

### Zustellung des kontextzugeordneten Ereignisses (unverändert)

1. `ScmEventStore.AcceptBatchAsync` legt das `ScmIncomingEvent` mit `AufgabeId` der gefundenen
   Aufgabe an.
2. Die Dispatch-Schleife des `ScmEventMonitoringService` ruft
   `ScmEventOrchestrator.DispatchAsync`; `ScmTaskCoordinator.Shared.RunAsync` serialisiert,
   `BlockReason` wertet Startsperren, `WaitForInputReadyAsync` wartet bei beschäftigter CLI,
   `ScmEventExchangeProtocol`/`WritePromptAsync` liefern zu — keine Änderung erforderlich.

Beteiligte Klassen/Komponenten: `ScmEventStore`, `ScmEventMonitoringService`,
`ScmEventOrchestrator`, `ScmTaskCoordinator`, `KiAusfuehrungsService`,
`IScmPromptSessionAdapter`, `ScmEventExchangeProtocol`.

## Neue Klassen

Keine.

## Änderungen an bestehenden Klassen

### `ScmEventTaskResolver` (Klasse, `src/Softwareschmiede/Application/Services/ScmEventTaskResolver.cs`)

- **Geänderte Methoden:** `ResolvePullRequestAsync` — nach dem `PullRequestTaskIdsAsync`-Lookup
  (bei `taskIds.Count == 0`) und vor `ChooseRepository`/`CreateFromPullRequestAsync` die
  Kontextsuche aufrufen; `>1` → `Ambiguous`, `==1` → `Assigned`, `0` → bisherige Anlage.
- **Neue Methoden:**
  - `FindActiveContextTaskIdsAsync(PullRequest pullRequest, IReadOnlyList<GitRepository> repositories, string repositoryId, CancellationToken ct)` → `Task<List<Guid>>` — lädt aktive
    Aufgaben (`Status ∉ {Beendet, Archiviert}`) im `repositories`-Scope inkl. `IssueReferenz` und
    vereinigt Branch- und Issue-Match.
  - `MatchesBranch(PullRequest pullRequest, string repositoryId, Aufgabe kandidat)` → `bool`
    (private static) — Same-Repository- und `BranchName`-Vergleich.
  - `MatchesClosingIssue(PullRequest pullRequest, string repositoryId, Aufgabe kandidat)` → `bool`
    (private static) — `IssueReferenz.IssueNummer` gegen `ExtractClosingIssueNumbers`.

### `PullRequestBodyBuilder` (interne Hilfsklasse, `src/Softwareschmiede/Application/Services/PullRequestBodyBuilder.cs`)

- **Neue Methoden:** `ExtractClosingIssueNumbers(string? body, string? repositoryId = null)` →
  `IReadOnlyCollection<int>` — extrahiert alle Issue-Nummern aus Closing-Direktiven
  (`close[sd]?`/`fix(e[sd])?`/`resolve[sd]?` + optionaler `owner/repo`-Qualifizierer + `#N`);
  mit gesetztem `repositoryId` zählen qualifizierte Direktiven nur bei Übereinstimmung
  (`OrdinalIgnoreCase`).
- **Geänderte Methoden:** `ContainsClosingDirectiveForIssue` — intern auf
  `ExtractClosingIssueNumbers` umgestellt; fachliches Verhalten (qualifizierte Direktiven zählen
  ungefiltert) bleibt identisch.

### `ScmEventMonitoringService`, `AufgabeService`, `Aufgabe`

Unverändert — `linked`-Aufbau, deduplizierende Vorab-Lookups und das Datenmodell bleiben wie
bestehend (siehe Designentscheidungen).

## Datenbankmigrationen

Keine.

## Validierungsregeln

Keine — es werden keine neuen Benutzereingaben verarbeitet; die Match-Bedingungen (nicht-leerer
`BranchName`, Same-Repository-Prüfung, Repository-Qualifizierer) sind in den Programmabläufen
beschrieben.

## Konfigurationsänderungen

Keine.

## Seiteneffekte und Risiken

- **Mehrdeutigkeitsdiagnose:** `AmbiguousAssignmentDiagnostic` kann jetzt auch durch
  kontextbasierte Mehrfachtreffer entstehen (z. B. zwei aktive Aufgaben mit gleichem
  `BranchName`, oder Branch-Match auf A und `Closes`-Match auf B). Das Ereignis bleibt dann wie
  bisher unzugeordnet — gewünschtes, konservatives Verhalten.
- **Fehlzuordnungsrisiko Issue-Nummer:** Eine `Closes #N`-Direktive trifft jede aktive Aufgabe im
  selben lokalen Repository-Scope mit `IssueReferenz.IssueNummer == N` — auch wenn der PR nicht
  von ihr stammt. Durch die Same-Repository-Einschränkung des Branch-Matches und die
  Repository-Qualifizierer-Regel ist das Risiko klein; Mehrfachtreffer werden über `Ambiguous`
  abgefedert statt falsch zugeordnet.
- **`Neu`-Aufgaben ohne `BranchName`:** Werden nur über den Issue-Match gefunden; ein fehlender
  `BranchName` erzeugt schlicht keinen Branch-Treffer — kein Risiko.
- **`ScmTaskCompletionMonitoringService`:** Unverändert, da keine implizite
  `PullRequestReferenz`-Persistenz erfolgt (siehe Designentscheidung).
- **Bestehender Test `Monitor_QualifiedPullRequestEvent_CreatesReviewSourceTask`:** Bleibt
  gültig — Fixture-PR nutzt `feature/branch` + Body ohne Closing-Direktive → kein
  Kontexttreffer.

## Umsetzungsreihenfolge

1. **`PullRequestBodyBuilder.ExtractClosingIssueNumbers` anlegen und `ContainsClosingDirectiveForIssue` umstellen**
   - Voraussetzungen: Keine (bestehende interne Klasse und Regex-Logik vorhanden).
   - Beschreibung: Neue interne Extraktionsmethode mit optionalem Repository-Qualifizierer-Filter;
     `ContainsClosingDirectiveForIssue` delegiert ohne Verhaltensänderung.

2. **`ScmEventTaskResolver` erweitern**
   - Voraussetzungen: Schritt 1 (`ExtractClosingIssueNumbers` verfügbar).
   - Beschreibung: `FindActiveContextTaskIdsAsync` sowie die privaten Match-Hilfsmethoden
     `MatchesBranch`/`MatchesClosingIssue` anlegen; Aufruf in `ResolvePullRequestAsync` zwischen
     Referenz-Lookup und `CreateFromPullRequestAsync` einschieben.

3. **Unit-Tests für `ExtractClosingIssueNumbers`**
   - Voraussetzungen: Schritt 1.
   - Beschreibung: Neue Testklasse `PullRequestBodyBuilderTests` unter
     `src/Softwareschmiede.Tests/Application/Services/` (Ordnerkonvention der bestehenden
     Service-Tests; `InternalsVisibleTo("Softwareschmiede.Tests")` in
     `src/Softwareschmiede/Properties/AssemblyInfo.cs` macht die `internal` Klasse erreichbar).

4. **Integrationstests in `ScmEventMonitoringServiceTests_TaskAssignment`**
   - Voraussetzungen: Schritt 2; `ScmDispatchFixture` (SQLite + gemockte `IScmEventSource`)
     vorhanden.
   - Beschreibung: Lokale Hilfsmethode `AddTaskAsync` (legt `Aufgabe` mit `Status`, `BranchName`
     und optionaler `IssueReferenz` in einem vorhandenen `GitRepository` an) und Erweiterung von
     `ConfigurePullRequest` um `branchName`/`body`/`sourceRepositoryId`-Parameter
     (rückwärtskompatibel mit Standardwerten); anschließend die neuen Testfälle (siehe unten).

5. **Vollständiger Build und Testlauf**
   - Voraussetzungen: Schritte 1–4.
   - Beschreibung: `dotnet build`, danach stabile Lane
     `dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category!=OsInterface"`
     sowie `dotnet test src/Softwareschmiede.IntegrationTests` (jeweils mit
     `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`, synchron im Vordergrund).

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `Monitor_PullRequestOnActiveTaskBranch_AssignsWithoutSecondCreation` | `ScmEventMonitoringServiceTests_TaskAssignment` | PR-Kommentar auf PR, dessen `BranchName` dem `BranchName` einer `Gestartet`-Aufgabe ohne `PullRequestReferenz` entspricht → Inbox-Eintrag auf die bestehende Aufgabe, genau eine Aufgabe in der DB (Haupt-Akzeptanzkriterium des Issue). |
| `Monitor_PullRequestClosingActiveTaskIssue_AssignsWithoutSecondCreation` | `ScmEventMonitoringServiceTests_TaskAssignment` | PR-Body mit `Closes #9`, aktive Aufgabe mit `IssueReferenz.IssueNummer == 9` → zugeordnet, keine neue Aufgabe. |
| `Monitor_PullRequestBranchOfEndedTask_CreatesReviewSourceTask` | `ScmEventMonitoringServiceTests_TaskAssignment` | Branch-Match trifft nur `Beendet`/`Archiviert`-Aufgabe → neue `ReviewSource`-Aufgabe wie bisher. |
| `Monitor_PullRequestConflictingContextSignals_ReportsAmbiguityWithoutTask` | `ScmEventMonitoringServiceTests_TaskAssignment` | Branch-Match auf Aufgabe A, `Closes`-Match auf Aufgabe B → `AmbiguousAssignmentDiagnostic` auf den Snapshots, keine neue Aufgabe, kein Inbox-Eintrag. |
| `Monitor_ForkedPullRequestWithSameBranch_CreatesReviewSourceTask` | `ScmEventMonitoringServiceTests_TaskAssignment` | `SourceRepositoryId` ≠ `repositoryId` bei gleichem Branchnamen → kein Kontexttreffer, neue `ReviewSource`-Aufgabe. |
| `Monitor_PullRequestOnPausedTaskBranch_AssignsAndWaits` | `ScmEventMonitoringServiceTests_TaskAssignment` | Pausierte Aufgabe (`PausiertBisUtc` in Zukunft) mit Branch-Match → Ereignis zugeordnet, bleibt `Wartend` („Aufgabe pausiert; Start gesperrt."), keine Doppelaufgabe. |
| `AddTaskAsync` (Hilfsmethode) | `ScmEventMonitoringServiceTests_TaskAssignment` | Legt `Aufgabe` mit wählbarem `Status`, `BranchName`, `PausiertBisUtc` und optionaler `IssueReferenz` in einem per `AddRepositoryAsync` angelegten `GitRepository` an. |
| `ConfigurePullRequest`-Erweiterung | `ScmEventMonitoringServiceTests_TaskAssignment` | Optionale Parameter `branchName` (Standard `"feature/branch"`), `body` (Standard `"PR Beschreibung"`), `sourceRepositoryId` (Standard `"owner/repo"`). |
| `ExtractClosingIssueNumbers`-Fälle | `PullRequestBodyBuilderTests` (neu, `src/Softwareschmiede.Tests/Application/Services/`) | Einfache `Closes #N`, Keywords `fix/fixes/fixed/resolve/resolves/resolved` (Groß-/Kleinschreibung), mehrere Direktiven, `owner/repo#N`-Qualifizierer mit und ohne `repositoryId`-Filter, keine Direktive/`null`-Body → leere Menge. |

### Betroffene bestehende Tests

Keine.

### E2E-Tests (Pflicht)

Keine neuen UI-E2E-Tests erforderlich — die Änderung betrifft keine Benutzerinteraktion in der
App-Oberfläche. Der fachliche End-to-End-Pfad (Enumerierte PR-Referenz → Resolver-Kontextsuche →
`ScmIncomingEvent` auf bestehender Aufgabe → Dispatch) wird durch die neuen Integrationstests in
`ScmEventMonitoringServiceTests_TaskAssignment` mit echter SQLite-Datenbank und echtem
`ServiceProvider` vollständig abgedeckt.

Bestehende E2E-Tests: Keine betroffen.

## Offene Punkte

Keine.
