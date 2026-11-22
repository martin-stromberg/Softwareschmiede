# Bestandsaufnahme – SCM-Ereignisverarbeitung & Abschlussüberwachung

Verifiziert gegen den aktuellen Code (Basisbranch = `origin/staging`-Stand, Commit `0ff04619`).

## Architekturüberblick

- `PeriodicBackgroundService`-basierte SCM-Dienste im `ScmEventOptions.PollInterval`-Takt
  (Standard 60 s): `ScmEventMonitoringService` (reguläre Ereignisse),
  `ScmIssuePreparationMonitoringService` (Issue-Aufbereitung-Entdeckung),
  `ScmTaskCompletionMonitoringService` (Auto-Abschluss) u. a.
- Ausführung aller SCM-Ereignisse über `ScmEventOrchestrator` (Inbox `ScmIncomingEvent`
  + Outbox `ScmOutgoingAction`); Aufbereitungsereignisse (`ScmIncomingEvent.Purpose ==
  "IssueAufbereitung"`) laufen über den dedizierten Pfad `DispatchPreparationAsync` /
  `ReconcilePreparationAttemptAsync`, alle übrigen über den generischen Zustellpfad.
- Aufgabenzuordnung vor Inbox-Aufnahme: `ScmEventTaskResolver` (Issue-/PR-Auflösung,
  `Ambiguous`-Befund bei Mehrdeutigkeit).
- Plugin-Ebene: `GitHubPlugin` implementiert `IScmEventSource` (Lesen) und
  `IScmEventResponder` (Antwortaktionen); Credentials je `ScmAccountMode` via `ClientFor`.

## Teil A – Repository-Identität (#436/#456) — verifiziert

- `ScmIssuePreparationMonitoringService.cs:84–85` — Auflösung bewusst unter
  `ScmAccountMode.UserAccount` (kommentiert, fachlich korrekt).
- `ScmIssuePreparationMonitoringService.cs:194` — `issue with { Repository =
  issue.Repository with { Account = accountMode } }`: nur das Konto-Flag wird
  gesetzt; `ViewerId`/`ViewerLogin`/`CanWrite` bleiben Benutzerkonto-Werte.
- `GitHubPlugin.EventResponses.cs:49–52` — `ExecuteResponseAsync` löst unter
  `repository.Account` neu auf, vergleicht `RepositoryId` + `ViewerId` (Umleitungsschutz).
- `GitHubPlugin.EventResponses.cs:33` (`ReconcileResponseAsync`) und `:128`
  (`ReconcileReviewReactionAsync`) — `IsViewer`-Vergleiche gegen denselben Snapshot.
- `GitHubPlugin.Events.cs:613–615` — `IsViewer` = ViewerId-Vergleich, Fallback Login.
- `GitHubPlugin.Events.cs:20,49–70,133–135` — `_resolutionCache` je Plugin-Instanz,
  `ResolutionCacheKey` enthält Client-Identität → je Konto eigener Cache-Eintrag,
  TTL `ScmResolutionCacheOptions` (15 min); Neuauflösung nach Warmup transportlos.
- `ScmEventMonitoringService` löst reguläre Ereignisse konsistent unter `accountMode`
  auf (Issue-Text Z. 103 verifiziert im Issue-Kommentar; Pfad nicht betroffen).
- `ScmEventRepository` (Record, `src/Softwareschmiede.Plugin.Contracts/Domain/
  ValueObjects/ScmEvents.cs:16`) trägt u. a. `Account`, `ViewerId`, `ViewerLogin`,
  `CanWrite`, `NumericRepositoryId`, `RepositoryId`, `Owner`, `Name`.
- Persistenz der Referenz: `ScmIncomingEvent.ObservationJson` (serialisierter
  `ScmEventObservation` inkl. `ScmEventReference.Repository`) → Bestandsdaten-Problem
  (#436) ist real; reine Codekorrektur heilt persistierte Ereignisse nicht.
- Tests: `src/Softwareschmiede.IntegrationTests/Services/
  ScmIssuePreparationMonitoringServiceTests.cs` (u. a.
  `AufbereitungMonitor_SeparatesKonto_EventCarriesMonitoringKonto` — prüft laut #436
  nur `Repository.Account`); Plugin-Ebene via `GitHubPluginTests`/`ScmDispatchFixture`.

## Teil B – Geschlossen-Re-Checks (#452) — verifiziert

- `ScmEventOrchestrator.DispatchPreparationAsync` (`:615–812`):
  - `:626–628` Hostaktionen (nicht `ai:`-gebunden) werden je Durchlauf nachgeholt.
  - `:630–645` `DeliveryAttemptId != null` → `ReconcilePreparationAttemptAsync`.
  - `:662–666` Repo-Neuauflösung + `RepositoryId`/`CanWrite`-Prüfung vor Zustellung.
  - `:667–680` **bereits vorhandener Re-Check**: `ReadIssueAsync`, `null` → warten,
    `IsClosed` → `Verarbeitet`, `Reason = null`, SystemMessage-Logeintrag.
  - `:681–757` Task-Status, CLI-Adapter, Health/Start/Slot, `WaitForInputReadyAsync`,
    Access-Probe → langes Fenster zwischen Re-Check (`:667`) und `PrepareAsync` (`:762`).
  - `:758–777` `exchange.PrepareAsync` schreibt `request.json`, danach
    `DeliveryAttemptId`, `ZustellungVorbereitet` → `WritePromptAsync` → `Zugestellt`.
- `ReconcilePreparationAttemptAsync` (`:832–979`): liest Exchange-Messages, erzeugt
  `ScmOutgoingAction`s (`ai:`-Präfix) und führt sie via `outbox.ExecuteAsync` remote
  aus (`:900–903`) — **kein Issue-Status-Re-Check**; Statusübergänge
  `InBearbeitung`/`Verarbeitet`/`VerarbeitungUngeklärt` mit Abschlusskriterien
  (`:917–958`, IssueBodyUpdate + ggf. Kommentar nötig).
- `ScmIssuePreparationService.DetectAndEnqueueAsync` (`:145+`): liest Issue via
  `ReadIssueAsync` (Anlagedaten), wertet `IsClosed` **nicht** aus → geschlossenes
  Issue erzeugt Aufgabe + Ereignis; `Purpose = "IssueAufbereitung"` (Konst. `:30`).
- `ScmIncomingEventStatus` (statische Klasse, Literal-Stati): kein eigener
  „Verworfen"-Status; `Verarbeitet`/`Abgebrochen`/`VerarbeitungUngeklärt`/
  `ZustellungUngewiss` bilden `NonBlocking`. Verwerfen-Kennzeichnung muss über
  `Reason` oder einen neuen Statuswert entschieden werden (Persistenztest
  `ScmIncomingEventStatusTests` hält Literale fest).

## Teil C+D – Auftragsart, Routing, Doppelaufgaben (#451/#457) — verifiziert

- `WorkItem.Purpose` (`WorkItem.cs:120`, `string?`) = Isolationsmerkmal;
  `ScmIncomingEvent.Purpose` (`ScmIncomingEvent.cs:69`) = Dispatch-Verzweigung.
  Konstante `ScmIssuePreparationService.Purpose = "IssueAufbereitung"`.
- `ScmEventTaskResolver.ResolveIssueAsync` (`:52–75`): Kandidatenfilter
  `a.Purpose == null` (`:65`) → Aufbereitungs-Aufgabe unsichtbar; Trigger auf Issue
  mit nur Aufbereitungs-Aufgabe → `CreateFromIssueEventAsync` → **zweite Aufgabe**
  (das #457-Symptom). `FindActiveContextTaskIdsAsync` (`:126–146`) hat denselben
  Filter (`:135`) im PR-Pfad.
- IssueSourceKey-Namensräume: kanonisch `…/Issue/{n}` (Resolver `:174–178`) vs.
  Aufbereitung `…/IssueAufbereitung/{n}` (`PreparationSourceKey`) → kollidieren nie;
  Aufbereitungs-Ursprung ist aus `IssueReference.SourceKey` ableitbar.
- Isolation dokumentiert in `docs/help/plugins/github-ereignisse/business-rules.md`
  (:43): „Zweck-markierte Aufgaben werden … ausdrücklich ausgeschlossen — Trigger-
  Ereignisse auf demselben Issue lösen weiterhin die kanonische Aufgabe … aus."
- UI-Gruppierung: `WorkItemListMode` (Enum `Date`/`Repository`/`Ai`,
  `src/Softwareschmiede.App/ViewModels/WorkItemListMode.cs`);
  `WorkItemListLayout` (:26–86) baut Sort-/GroupDescriptions auf
  `ActiveWorkItemPanelItem`-Properties (`RepositoryGroupName`, `AiGroupName`,
  `*GroupIsFallback` für Fallback-Gruppen — Muster für weitere Werte vorhanden);
  Persistenz je Liste über `WorkItemListModeService`/`WorkItemListModeBinding`;
  Anzeige `ActiveTasksListControl` (Seitenleiste + Dashboard).
- Aufgaben-Anlagepfade: `CreateFromIssueEventAsync` (SCM-Issue),
  `CreateFromPullRequestAsync` (PR), `CreateFromAlertAsync` (Alerts), manuell.
- Ereignis „Issue geschlossen" (#457-Ablauf 4) ist heute über
  `ScmTaskCompletionMonitoringService` abgedeckt (Wartezeit → CLI-Stop → Complete).

## Teil E – Retry/Backoff Abschlussüberwachung (#433) — verifiziert

- `ScmTaskCompletionMonitoringService` (:25–228):
  - `RunOnceAsync` (:32–76): Schalter-Check, Kandidatenliste, `CheckTaskAsync` je
    Aufgabe; catch (:71–74) loggt `[ERR] SCM-Abschlussprüfung … fehlgeschlagen` —
    **Fall 2**: jede nicht-`OperationCanceled`-Exception von `CheckTaskAsync`
    (z. B. `DetermineClosureAsync`-Plugin-/Netzwerkfehler, DB-Fehler).
  - `CheckTaskAsync` (:78–155): Countdown über persistiertes
    `ScmCompletionDetectedUtc`; unter `ScmTaskCoordinator`-Sperre
    `DevelopmentProcessService.CompleteAsync` (:141); catch (:147–153) schluckt
    `InvalidOperationException`/`IOException`/`UnauthorizedAccessException` mit
    `[WRN]` — **Fall 1** → Endlos-Retry im Poll-Takt, `ScmCompletionDetectedUtc`
    bleibt → jeder Zyklus voller Wiederholversuch.
- `DevelopmentProcessService.CompleteAsync` (`:437–473`): `StopCliAsync`, dann
  `DeleteDirectoryForce(LocalClonePath)` (:470), `WorkItemService.CompleteAsync`.
  `DeleteDirectoryForce` (:964): Attribut-Normalisierung + `Directory.Delete` —
  keine Diagnose des blockierenden Prozesses.
- Kein bestehender Fehlerzähler/Backoff im Dienst; `NonBlocking`-Ereignislogik
  steuert nur die Beobachtung, nicht den Abschluss-Retry.
- Kandidaten für Fehlerzustand: `WorkItem`-Feld (Migration nötig) vs. in-memory
  `ConcurrentDictionary<Guid, …>` im Dienst + `LogEntry`-Eskalation (sichtbar über
  Aufgabenprotokoll, kein Schema-Eingriff).

## Konventionen & Tests

- Tests: `src/Softwareschmiede.Tests` (Unit, Filter `Category!=OsInterface`),
  `src/Softwareschmiede.IntegrationTests` (SCM-Services, Fixtures mit Fake-Sources);
  Stabile Lane: `dotnet test src/Softwareschmiede.Tests/…csproj --filter
  "Category!=OsInterface"`; `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` in dieser
  Sandbox; voller Build vor Tests; `dotnet test` niemals im Hintergrund.
- Formatierung: `dotnet format Softwareschmiede.slnx --verify-no-changes` (CI-Schritt,
  lokaler pre-push-Hook optional).
- Doku der Geschäftsregeln: `docs/help/plugins/github-ereignisse/business-rules.md`.
- Staging-Workflow: Feature-Branch → PR gegen `staging` → Promotion nach `main`;
  Issues schließen über Closing-Keywords bei Erreichen von `main`.
- Self-Hosting-Regel: laufende `Softwareschmiede.App.exe` niemals beenden; bei
  MSB3027-Lock Teil-Build ohne App-Projekt.
