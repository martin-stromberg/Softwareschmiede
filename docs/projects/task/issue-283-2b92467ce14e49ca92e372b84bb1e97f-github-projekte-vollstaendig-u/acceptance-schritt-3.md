# Abnahmeprüfung – Entwicklungsschritt 3

**Schritt:** Automatische Aufgabenanlage und -wiederverwendung aus qualifizierten Ereignissen
**Branch:** `task/issue-283-2b92467ce14e49ca92e372b84bb1e97f-github-projekte-vollstaendig-u-schritt-3-aufgabenanlage`
**Diff-Basis:** `task/issue-283-2b92467ce14e49ca92e372b84bb1e97f-github-projekte-vollstaendig-u...HEAD` (27 Dateien, +2.504/−9)

## Ergebnis

**Status:** Anforderung vollständig erfüllt

## Abweichungen

Keine.

## Hinweise

### Prüfung der Akzeptanzkriterien

**Kriterium 1 – Qualifiziertes Ereignis zu neuem Issue/PR → genau eine Aufgabe mit Referenz: erfüllt.**

- `ScmEventMonitoringService.cs:127–138`: Nur bei `taskIds.Length == 0` und einer qualifizierten Beobachtung im Batch (`ScmEventValidationService.GetRejection(x) is null` — unveränderte Regeln: `$Softwareschmiede`-Trigger, Viewer = Repository-Owner = Bearbeiter inkl. Editor-Bei-Bearbeitung, `<!-- softwareschmiede:`-Eigenantwort-Sperre) wird `ScmEventTaskResolver.ResolveAsync` aufgerufen; Ergebnis `Assigned` setzt `aufgabeId` für `AcceptBatchAsync`.
- `ScmEventTaskResolver.cs:50–66` (Issue) / `68–98` (PR): `ReadIssueAsync`/`ReadPullRequestAsync` laden Anlagedaten, Nummerngleichheit zur Referenz wird geprüft (`Nummer != reference.Number` → `Unassigned` mit Warnlog). Anlage über `AufgabeService.CreateFromIssueEventAsync` (Issue-Referenz mit `SourceKey`, `AufgabeService.cs:254–318`) bzw. `CreateFromPullRequestAsync` (`PullRequestReferenz` mit `Rolle.ReviewSource`, `AufgabeService.cs:321–424`).
- `localRepositories`-Map (`ScmEventMonitoringService.cs:45, 71–74`) bildet kanonisch aufgelöste `ScmEventRepository`-Schlüssel auf die zugehörigen lokalen `GitRepository`-Datensätze — Record-Gleichheit greift, da `resolved` und `localRepositories` dasselbe Repository-Objekt teilen. `ChooseRepository` (Z. 113–115) wählt deterministisch (URL, dann Id); `ProjektId` stammt aus `GitRepository.ProjektId`.
- Nachweis: `Monitor_QualifiedCommentOnUnassignedIssue_CreatesTaskAndInboxEntry` (Titel, GitRepositoryId, ProjektId, IssueNummer 9, Auftragstext enthält Trigger-Text und Issue-URL, `ScmIncomingEvents.AufgabeId == task.Id`) und `Monitor_QualifiedPullRequestEvent_CreatesReviewSourceTask` (ReviewSource, Provider, `owner/repo`, Nummer 5, Inbox-Zuordnung).

**Kriterium 2 – Qualifizierter Kommentar/Review zu Referenz ohne Aufgabe → Anlage: erfüllt.**

- Der Resolver-Gate prüft die qualifizierte Beobachtung im jeweils gelesenen Batch (`batch.Observations.FirstOrDefault(...)`); für Issues laufen die Inhaltsarten `Description`/`IssueComment`, für PRs zusätzlich `ReviewComment`/`PullRequestReview` durch denselben Pfad (Z. 105–110 unverändert). Reicht nur eine spätere Inhaltsart einen qualifizierten Text, entsteht die Aufgabe dort; vorgelagerte Snapshots mit `IdentityResolved = false` erzwingen beim Folgepoll `SinceUtc = null` (Z. 122–125) und werden mit dann zugeordneter Aufgabe neu bewertet — selbstheilend, wie bisher.
- `GitHubPlugin.Events.cs:117–145` implementiert die Detailabrufe per REST (`repos/{owner}/{name}/issues|pulls/{n}`); der Issues-Endpunkt-PR-Guard (`pull_request`-Objekt → `null`) verhindert Fehlanlagen. Fehler bleiben typisierte `ScmProviderException`s und laufen in den Quell-Checkpoint-Backoff — Nachweis `Monitor_ResolutionFailure_RecordsCheckpointFailure` (`ErrorCode = "Temporary"`, `NextAttemptUtc` gesetzt).

**Kriterium 3 – Wiederverwendung, keine Duplikate bei Wiederholung/Nebenläufigkeit: erfüllt.**

- Primärpfad: Nach erfolgreicher Anlage erscheint die Aufgabe im nächsten Lauf über `linked` (Issue via `IssueReferenz.IssueNummer`, PR via `PullRequests`-Einträge; `ScmEventMonitoringService.cs:34–37`) in der kanonischen Gruppe → `taskIds.Length == 1` → direkte Zuordnung ohne Resolver. Nachweis: `Monitor_RepeatedPollsAndBatches_CreateSingleTask` (zwei Läufe, eine Aufgabe, zwei Inbox-Einträge auf dieselbe Aufgabe) und `Monitor_ExistingTask_IsReusedWithoutSecondCreation`.
- Resolver-Nebenpfad: `ResolveIssueAsync` sucht Aufgaben über `IssueNummer` + `GitRepositoryId ∈ candidates`; `PullRequestTaskIdsAsync` sucht global über `(Provider, RepositoryId, PullRequestNumber)` — genau der geforderte Referenzschlüssel.
- Rennschutz: `IssueReferenz.SourceKey` (kanonisch `{provider}/{host}/{repositoryId}/Issue/{number}`, `IssueSourceKey` Z. 130–137) trägt einen Unique-Index (`SoftwareschmiededDbContext.cs:311`, Migration `20261017195348_AddIssueReferenzSourceKey` mit korrektem Vorgänger `AddScmEventInbox`); `CreateFromIssueEventAsync` dedupliziert vorab per SourceKey und fängt `DbUpdateException` mit Re-Lookup + Rückgabe der Gewinner-Aufgabe ab (`AufgabeService.cs:266–314`, analog dem `DbUpdateException`-Muster). Für PRs fängt der bestehende Unique-Index `(Provider, RepositoryId, PullRequestNumber)` (`DbContext.cs:358`) das Rennen ab; `CreateFromPullRequestAsync` übersetzt in `InvalidOperationException` (Z. 408–421), die der Resolver auffängt und per Re-Query auflöst (`ScmEventTaskResolver.cs:90–97`).
- Nachweise: `AufgabeServiceTests_IssueEvents` (SourceKey persistiert; gleicher Key → gleiche Aufgabe, kein Duplikat; anderer Key → zweite Aufgabe) und der Migrations-Apply/Rollback-Test in `WorkdirMigrationTests` (nullable, rückwärtskompatibel).

**Kriterium 4 – Nicht-qualifizierte Ereignisse und Mehrdeutigkeit: erfüllt.**

- Ohne qualifizierte Beobachtung läuft der Resolver gar nicht erst (`Monitor_UnqualifiedEvent_KeepsUnassignedWithoutTask` verifiziert `ReadIssueAsync` `Times.Never`, keine Aufgabe, keine Inbox, `UnassignedDiagnostic` auf allen Snapshots) — Diagnoseverhalten identisch zu Schritt 2.
- `resolution.Kind == Ambiguous` → `unassigned = false`, `aufgabeId = null` → `AcceptBatchAsync` schreibt `AmbiguousAssignmentDiagnostic` („Mehrdeutige Zuordnung zu Aufgaben", `ScmEventStore.cs:48, 52`) ohne Inbox-Eintrag; keine Automatik-Auswahl. `Unassigned` (z. B. remote verschwundene Referenz, `ReadIssueAsync → null`) → `unassigned = true` → `UnassignedDiagnostic`; Nachweis `Monitor_VanishedReference_StaysUnassigned`.
- Gruppen-Mehrdeutigkeit (`taskIds.Length > 1` aus `linked`) bleibt der bestehende Pfad ohne Resolver — Bestandstests (`ScmEventMonitoringServiceTests_AmbiguityRecovery`, `Monitor_EnumeratedReferenceWithTask_EntersInboxUnchanged`) unverändert und weiterhin konsistent.

**Kriterium 5 – Sichtbarkeit in der Aufgabenverwaltung: erfüllt.**

- Angelegte Aufgaben tragen `ProjektId` des aufgelösten Repositories, `Titel` aus Issue/PR, `Status.Neu`/`NichtGestartet` (Anlage startet nicht — verifiziert) und erscheinen über `GetByProjektAsync` (nicht archiviert) in der Aufgabenverwaltung des richtigen Projekts.
- Quellverweis: `ComposeBody` (Z. 121–128) kombiniert Referenzbeschreibung (ohne Selbst-Duplikat des Triggertextes), auslösende Beobachtung und deren URL; für PRs nutzt `BuildPullRequestAnforderungsBeschreibung` zusätzlich `pullRequest.Url` und die bestehende `CleanPullRequestBody`-Bereinigung (Z. 426–464) — Baustein-Wiederverwendung erfüllt.
- E2E-Nachweis: neuer Schritt in der konsolidierten `ScmEventsE2ETests`-Methode (`E2E_ScmEvents.cs`, E7-Anlage): qualifizierter Kommentar an Issue 20 → genau eine Aufgabe mit Issue-Referenz, Titel, Auftragstext mit Trigger-Text, ein Inbox-Eintrag — ohne neue FlaUI-Methode (konsolidierungskonform). `ScmE2EApiClient` beantwortet dafür Enumeration und REST-Detailendpunkte (`issues/{n}`/`pulls/{n}`) szenariogesteuert.

**Kriterium 6 – Bestandsschutz: erfüllt.**

- `ScmEventStore.AcceptBatchAsync` signatur- und logikunverändert (Datei nicht im Diff); `AufgabeId`-Invariante intakt — Inbox-Einträge entstehen nur bei `rejection is null`, und `aufgabeId == null` impliziert stets nicht-leere Diagnose (`ScmEventStore.cs:52, 67–75`); `ScmIncomingEvent.AufgabeId` bleibt `Guid` nicht-nullable.
- Validierung, Snapshot-/`EventKey`-Dedup, Watermark-/Backoff-Checkpoints, Auflösungs-/Enumerationspfade unverändert; der Aufnahmepfad für Referenzen mit Aufgabe (`taskIds.Length == 1` bzw. `> 1`) ist semantisch identisch. `ScmEventMonitoringServiceTests_RepoWide` benötigte keine Anpassung und bleibt unter dem neuen Verhalten korrekt (qualifizierte Beobachtungen ohne anlegbare Referenz laufen weiter in `UnassignedDiagnostic`, da Fixture-Defaults `null` liefern).
- DI-Verkabelung konsistent: `ScmEventTaskResolver` scoped in `App.xaml.cs:262` und `ScmDispatchFixture` registriert; `AufgabeService` scoped — Resolver, Service und `batchDb` teilen dadurch denselben `DbContext`, die Anlage wird vor `AcceptBatchAsync` sequentiell committed (Aufgabe steht persistiert, bevor die Inbox-Transaktion beginnt).

### Weitere Feststellungen

- **Reihenfolge-Invariante bestätigt:** Die Aufgabenanlage erfolgt sequentiell `await`ed vor `AcceptBatchAsync` (`ScmEventMonitoringService.cs:129–138`) und committed im selben DbContext noch vor dessen Inbox-Transaktion — `ScmIncomingEvent.AufgabeId` verweist damit garantiert auf eine persistierte Aufgabe.
- **Startverhalten (abgegrenzt, kein Verstoß):** Die Anlage selbst erzeugt `Status.Neu`/`NichtGestartet`. Der bestehende Orchestrator setzt die Aufgabe anschließend im selben `RunOnceAsync`-Durchlauf über den regulären Dispatch-Pfad auf `Gestartet` (`ScmEventOrchestrator.cs:53` ff.; der Integrationstest assertiert `Gestartet` nach dem Lauf). Das ist Bestandspipeline-Verhalten für qualifiziert zugeordnete Ereignisse, keine Startlogik des Anlageschritts; „durchgängiger" Start inkl. Wartend ohne Standard-CLI bleibt Schritt 4.
- **Resolver-Mehrdeutigkeit ohne dedizierten Test:** Der `Ambiguous`-Rückgabepfad des Resolvers ist für Issues durch die vorgeschaltete `linked`-Gruppierung praktisch unerreichbar (Mehrdeutigkeit wird bereits über `taskIds.Length > 1` diagnostiziert) und für PRs ein Safety-Net über den globalen Referenzschlüssel. Der Code-Pfad ist korrekt verdrahtet (`unassigned = false` → Ambiguous-Diagnose ohne Inbox); ein dedizierter Test existiert nicht — als Abdeckungsnotiz, nicht als Verstoß gewertet, da das fachliche Mehrdeutigkeitskriterium über den Bestandspfad nachgewiesen ist.
- **Detailabruf vor Bestandssuche:** `ResolveIssueAsync`/`ResolvePullRequestAsync` rufen den Provider-Detailendpunkt auf, bevor die lokale Aufgabensuche läuft — in der Praxis nur bei `taskIds.Length == 0` erreichbar und bei Mehrfach-Inhaltsarten einmalig je Batch; zusätzlicher API-Call pro Resolver-Lauf, fachlich unbedenklich.
- **Aufgaben ohne aktives Repository:** Die Wiederverwendung setzt — wie die bestehende Verknüpfungssemantik in `linked` — ein aktives, kanonisch auflösbares `GitRepository` voraus. Aufgaben mit Issue-/PR-Referenz, aber ohne `GitRepositoryId` oder auf inaktivem Repository, werden weder von `linked` noch vom Resolver gefunden und könnten fachlich zu einer Parallelanlage führen; konsistent zur Schritt-2-Semantik und von der Anforderung („Referenz gehört zu genau einer Aufgabe … über `GitRepository`") abgedeckt.
- **Testnachweise:** Alle gemeldeten Testartefakte existieren und sind konsistent: 7 Methoden in `ScmEventMonitoringServiceTests_TaskAssignment`, 3 in `AufgabeServiceTests_IssueEvents`, 4 neue in `GitHubEventSourceTests`, 1 Migrations-Test, E2E-Schritt in der konsolidierten E2E-Methode. Die gemeldeten Gesamtzahlen (1986/1987 Unit, 150/150 Integration) liegen nicht als auswertbares Artefakt (TRX/Bericht) im Repository vor und wurden nicht eigenständig nachvollzogen — entspricht dem Befund aus Schritt 2. Die Testdateien kompilieren konsistent gegen die vorhandenen Verträge (`ScmEventRepository`, `ScmEventObservation`, `Issue`, `PullRequest`, `ScmRepositoryContext`-Signaturen verifiziert).
- **Doku-Kleinigkeit:** `…-schritt-3-aufgabenanlage-tasks.md` Zeile 17 führt `ScmEventMonitoringServiceTests_RepoWide` als „angepasst" — die Datei ist im Diff unverändert (die Fixture-Defaults `ReadIssueAsync`/`ReadPullRequestAsync → null` machten eine Anpassung unnötig). Dokumentarische Ungenauigkeit ohne fachliche Auswirkung — identischer Befund wie in Schritt 2.
- **Dokumentation konsistent:** `README.md`, `changes.log` und `docs/help/plugins/github-ereignisse/{beschreibung,ablauf-technisch,business-rules}.md` beschreiben Anlage/Wiederverwendung, Dedup-Schlüssel und Mehrdeutigkeitsverhalten korrekt.
