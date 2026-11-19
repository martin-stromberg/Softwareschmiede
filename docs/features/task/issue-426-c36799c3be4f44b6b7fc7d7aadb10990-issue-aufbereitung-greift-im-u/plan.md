# Umsetzungsplan: Issue-Aufbereitung im Überwachungsmodus „Separates Konto“

## Übersicht

Die „Issue erstellt“-Erkennung der automatischen Issue-Aufbereitung wird aus dem
repository-weiten Enumerationskanal des `ScmEventMonitoringService` in einen eigenständigen
gehosteten Dienst `ScmIssueAufbereitungMonitoringService` überführt (Vorbild:
`ScmBackmergeMonitoringService`). Der neue `PeriodicBackgroundService` enumeriert
freigeschaltete Repositories kanonisch unter `ScmKontoModus.Benutzerkonto` — unabhängig vom
konfigurierten Überwachungsmodus — und reiht neu angelegte Issues über den bestehenden
`ErkennenUndEinreihenAsync`-Pfad ein. Zusätzlich wird das „bereits ausgewertet“-Kriterium von
der `ScmContentSnapshot`-Erstbeobachtung auf einen eigenen Auswertungs-Checkpoint je Issue
umgestellt, um die Erwähnungs-Verschnitt-Lücke im `SeparatesKonto` zu schließen. Zustellung,
Erwähnungs-/Kommentar-Reaktion und Ausführung bleiben unverändert.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| „Unbekannte Issue“-Strategie (Offene Frage 1) | Auswertungs-Checkpoint je Issue: `issueaufbereitung-issue/{provider}/{host}/{repoId}/{number}` | Ein Nummern-Wasserstand setzt monotone, lückenlose Nummern voraus — bei GitHub teilen sich Issues und Pull Requests die Sequenz, andere Provider garantieren evtl. gar keine Sequentialität. Der je-Issue-Checkpoint ist provider-agnostisch, folgt exakt dem `backmerge/{provider}/{host}/{repoId}/{nummer}`-Präzedenzfall und bringt Backoff-/Fehlerisolation sowie Diagnosesichtbarkeit in der Ereignisübersicht gratis mit. |
| Erstbeobachtungs-/Dedup-Kriterium (Offene Frage 2) | Die `ScmContentSnapshot`-Erstbeobachtungsprüfung in `ErkennenUndEinreihenAsync` wird durch den `issueaufbereitung-issue/`-Auswertungs-Checkpoint ersetzt | Die Snapshot-Prüfung verwirft Issues, deren Beschreibung zuvor über die Erwähnungs-Inbox gelesen wurde — eine echte Lücke (Aufbereitung ginge im `SeparatesKonto` verloren). Der Checkpoint ist das authoritative „ausgewertet“-Kriterium für beide Erkennungspfade; Altbestand bleibt über Baseline + `CreatedAt`-Filter abgedeckt, Doppelanlagen weiterhin über `EventKey`/`SourceKey`-Unique-Indizes. „Ausgewertet“ gilt als: Checkpoint vorhanden ohne offenen Fehler (`ErrorCode == null`); Endzustand `NextAttemptUtc = long.MaxValue`. |
| Konto-Markierung der erzeugten Referenz (Offene Frage 3) | Der neue Dienst taggt die `ScmEventReference` für den Aufbereitungspfad auf den konfigurierten `kontoModus` um (`ScmEventRepository` per `with`-Ausdruck kopieren) | Die Anforderung verlangt „Separates Konto bleibt der Ausführungsweg“: Remote-Aktionen (`IssueBodyUpdate`, `Comment` über die Outbox) wählen ihr Credential über `ScmEventRepository.Konto` der persistierten Referenz. Die kanonische Auflösung selbst läuft weiter unter `Benutzerkonto` (Vollständigkeit der Repository-Sicht); das Umtaggen ist sicher, weil `ContentKey`/`EventKey`/`SourceKey` konto-agnostisch sind. |
| Kopplung an `scm.monitoring.enabled` (Offene Frage 4) | Unabhängig — Laufzeit-Schalter ist ausschließlich `scm.issueaufbereitung.enabled` | Backmerge-Präzedenz (`Monitor_EventMonitoringDisabled_BackmergeStillRuns` belegt die bewusste Entkopplung) und `scm.issueaufbereitung.enabled` ist der dedizierte Funktionsschalter. Konsequenz: Der Dienst ruft am Zyklusende selbst `ScmEventOrchestrator.DispatchDueEventsAsync` auf, damit sofort fällige Aufbereitungsereignisse auch bei deaktivierter Ereignisüberwachung zugestellt werden. |
| „Quellen erneut prüfen“ (Offene Frage 5) | `RetrySourcesCommand` stößt zusätzlich `ScmIssueAufbereitungMonitoringService.RunOnceAsync` an | Backmerge-Präzedenz: manueller Anstoß unabhängig vom Monitoring-Schalter. Da „ausgewertet“ über Checkpoint-Existenz (nicht `NextAttemptUtc`) geprüft wird, löst der globale Checkpoint-Reset keine Massen-Neuauswertung aus — nur fehlerbehaftete Issues werden erneut versucht. |
| Erweiterungspunkt | Neuer gehosteter Dienst statt Eingriff in `ScmEventMonitoringService.RunOnceAsync` | Identisch zur Ablösung der Backmerge-Prüfung aus der Ereignisüberwachung; der `!inboxMode`-Guard am Monitor-Pfad bleibt bestehen (redundant, aber dedup-sicher). |

## Programmabläufe

### Aufbereitungs-Entdeckungszyklus (`ScmIssueAufbereitungMonitoringService.RunOnceAsync`)

1. `ScmEventStartupGate.WaitAsync(ct)` abwarten.
2. Eigenen Service-Scope öffnen; `AppEinstellungService.GetScmIssueAufbereitungEnabledAsync`
   lesen — bei `false` Early-Return ohne Nebenwirkungen (kein Checkpoint, kein Provider-Aufruf).
3. `GetScmKontoModusAsync` lesen (für das Konto-Tagging der erzeugten Referenzen).
4. Alle `GitRepository` mit `Aktiv && IssueAufbereitungAktiv` laden (Vorfilterung spart
   Enumeration und Beschreibungs-Reads für nicht freigeschaltete Repositories).
5. Plugins über `IPluginManager.GetSourceCodeManagementPlugins()` iterieren; nur
   `IScmEventSource` verwenden; Alias-Gruppen über `RepositoryUrl` und
   `ScmPluginMatcher.MatchesPlugin` bilden.
6. Je Alias: eigener Scope, Checkpoint `issueaufbereitung-repository/{prefix}/{alias}` —
   bei ausstehendem `NextAttemptUtc` überspringen; `GetEventRepositoryAsync` mit
   `new ScmRepositoryContext(aliasGroup[0].Id, plugin.PluginPrefix, alias,
   ScmKontoModus.Benutzerkonto)`; `localRepositories`-Mapping
   `ScmEventRepository → List<GitRepository>`; Erfolg räumt den Checkpoint und stößt
   `ScmEventChangeNotifier.Notify()` an; Fehler via
   `ScmCheckpointSupport.RecordFailureAsync(…, retryPermanent: true)` isolieren nur diesen
   Alias.
7. Laufweites `enumerated`-HashSet; je aufgelöstem `ScmEventRepository`
   `ScmCheckpointSupport.EnumerateRepositoryAsync` mit
   `checkpointKeyPrefix = "issueaufbereitung-enumeration"` aufrufen.
8. Im `verarbeite`-Callback nur Referenzen mit `Kind == ScmReferenceKind.Issue` betrachten
   (Muster: `ScmBackmergeService.EvaluateDiscoveredReferencesAsync` — eigener try/catch je
   Issue, damit ein Auswertungsfehler den Enumerations-Watermark nicht zurücksetzt):
   a. Fehlt der Baseline-Checkpoint `ScmIssueAufbereitungsService.BaselineKey(repository)`:
      Baseline mit `WatermarkUtc = jetzt` anlegen **und** alle enumerierten
      Issue-Referenzen als ausgewertet markieren (`issueaufbereitung-issue/…`-Checkpoint,
      Endzustand) — kein Retro-Scan, null Beschreibungs-Reads.
   b. Baseline vorhanden — je Issue-Referenz:
      - `issueaufbereitung-issue/…`-Checkpoint ohne offenen Fehler vorhanden ⇒ bereits
        ausgewertet, überspringen (kein Beschreibungs-Read → Rate-Limit-Schonung).
      - Checkpoint mit Fehler/Backoff und `NextAttemptUtc` noch nicht erreicht ⇒
        überspringen.
      - Sonst: Referenz per `with`-Ausdruck auf den konfigurierten `kontoModus` umtaggen,
        `ReadEventsAsync` mit `ScmEventReadRequest(reference, ScmContentKind.Description,
        null, WatermarkUtc)` lesen und als `ScmEventBatch` an
        `ScmIssueAufbereitungsService.ErkennenUndEinreihenAsync(source, reference, batch,
        lokaleRepositories, ct)` übergeben.
      - Erfolg/Misserfolg der Einreihung entscheidet `ErkennenUndEinreihenAsync`; der Dienst
        schreibt den Auswertungs-Checkpoint (Endzustand) bzw. bei Exception
        `RecordFailureAsync` auf dem Issue-Checkpoint (Standard-Backoff; `Permanent`/
        `Permission`/`Authentication` ⇒ dauerhafte Sperre nur dieses Issues).
9. Am Zyklusende `ScmEventOrchestrator.DispatchDueEventsAsync(ct)` aufrufen (in eigenem
   Scope aufgelöst), damit sofort fällige Aufbereitungsereignisse auch bei deaktiviertem
   `scm.monitoring.enabled` zugestellt werden.

Beteiligte Klassen/Komponenten: `ScmIssueAufbereitungMonitoringService`,
`PeriodicBackgroundService`, `ScmEventStartupGate`, `AppEinstellungService`,
`IPluginManager`, `IScmEventSource`, `ScmPluginMatcher`, `ScmCheckpointSupport`,
`ScmIssueAufbereitungsService`, `ScmEventChangeNotifier`, `ScmEventOrchestrator`,
`ScmEventOptions`, `GitRepository`, `ScmObservationCheckpoint`, `ScmEventRepository`,
`ScmEventReference`, `ScmRepositoryContext`, `ScmKontoModus`.

### Anlage eines Aufbereitungsauftrags (`ErkennenUndEinreihenAsync`, angepasst)

1. `repositories` auf `IssueAufbereitungAktiv` filtern, deterministisch sortieren; leerer
   Batch ⇒ Rückkehr (unverändert).
2. Baseline-Logik unverändert (fehlender `issueaufbereitung/…`-Checkpoint ⇒ anlegen und
   auslöserlos beenden).
3. `CreatedAt <= baselineUtc` ⇒ Altbestand, übersprungen — danach Auswertungs-Checkpoint
   setzen (siehe Schritt 6).
4. **Geändert:** Statt `ScmContentSnapshot`-Prüfung: `issueaufbereitung-issue/…`-Checkpoint
   ohne offenen Fehler vorhanden ⇒ bereits ausgewertet, überspringen.
5. `ScmIncomingEvents.AnyAsync(EventKey)` ⇒ bereits eingereiht, überspringen.
6. `ReadIssueAsync` → `AufgabeService.CreateFromIssueEventAsync` mit
   `AufbereitungsSourceKey` + `Zweck` → internes `ScmIncomingEvent` (`EventKey` =
   `{ContentKey}/IssueAufbereitung`, sofort fällig) + `Protokolleintrag`;
   `DbUpdateException`-Anlagerennen-Behandlung unverändert.
7. **Neu:** Nach abgeschlossener Auswertung (Einreihung oder begründeter Skip —
   inkl. `ReadIssueAsync == null`) den `issueaufbereitung-issue/…`-Checkpoint mit
   Endzustand (`NextAttemptUtc = long.MaxValue`, `ErrorCode = null`) schreiben, damit der
   Entdeckungszyklus die Referenz künftig ohne Beschreibungs-Read überspringt.

Beteiligte Klassen/Komponenten: `ScmIssueAufbereitungsService`, `AufgabeService`,
`ScmObservationCheckpoint`, `ScmIncomingEvent`, `IssueReferenz`, `Aufgabe`,
`SoftwareschmiededDbContext`, `ScmEventChangeNotifier`.

### Manueller Anstoß über „Quellen erneut prüfen“

1. `RetrySourcesCommand` setzt wie bisher alle `NextAttemptUtc` der
   `ScmObservationCheckpoints` zurück.
2. Zusätzlich zum bestehenden `ScmBackmergeMonitoringService.RunOnceAsync` wird nun auch
   `ScmIssueAufbereitungMonitoringService.RunOnceAsync` angestoßen — unabhängig von
   `scm.monitoring.enabled` (der Dienst prüft seinen eigenen Schalter).
3. Bereits ausgewertete Issues (Checkpoint ohne Fehler) werden nicht erneut gelesen; nur
   Issues mit offenem Fehler werden durch den `NextAttemptUtc`-Reset früher wiederholt.

Beteiligte Klassen/Komponenten: `ScmEventQueueViewModel`,
`ScmIssueAufbereitungMonitoringService`.

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `ScmIssueAufbereitungMonitoringService` | Klasse (`PeriodicBackgroundService`, Singleton + Hosted Service) | Eigenständiger Entdeckungszyklus für „Issue erstellt“ — modusunabhängig, kanonisch unter `ScmKontoModus.Benutzerkonto`, mit eigenen Checkpoint-Namensräumen `issueaufbereitung-repository/`, `issueaufbereitung-enumeration/` und `issueaufbereitung-issue/`. Datei: `src/Softwareschmiede/Application/Services/ScmIssueAufbereitungMonitoringService.cs`. |

Keine neuen Enums, Interfaces oder Entitäten erforderlich — `ScmObservationCheckpoint.SourceKey`
ist ein freier Namensraum, alle benötigten Interfaces existieren.

## Änderungen an bestehenden Klassen

### `ScmIssueAufbereitungsService` (Scoped-Service)

- **Neue Methoden:** `AuswertungsCheckpointKey(ScmEventReference)` (`public static`) —
  liefert den SourceKey `issueaufbereitung-issue/{provider}/{host}/{repoId}/{number}`
  (Muster: `BaselineKey`, `AufbereitungsSourceKey`).
- **Geänderte Methoden:** `ErkennenUndEinreihenAsync` —
  - Die `ScmContentSnapshot`-Erstbeobachtungsprüfung (kein Snapshot zum `ContentKey`) wird
    durch die Prüfung „`issueaufbereitung-issue/…`-Checkpoint ohne offenen Fehler vorhanden“
    ersetzt. Begründung: schließt die Erwähnungs-Verschnitt-Lücke (im `SeparatesKonto`
    schreibt der Monitor Snapshots mention-verknüpfter Beschreibungen, ohne dass die
    Aufbereitung je ausgewertet wurde).
  - Nach jeder abgeschlossenen Auswertung einer Issue-Referenz (Einreihung, Altbestand-Skip,
    bereits-eingereiht-Skip, `ReadIssueAsync == null`) wird der Auswertungs-Checkpoint mit
    Endzustand geschrieben. Damit markieren auch Monitor-Pfad-Auswertungen die Referenz für
    den neuen Zyklus als erledigt.

### `ScmEventQueueViewModel` (ViewModel)

- **Geänderte Methoden:** `RetrySourcesCommand` — ruft zusätzlich
  `ScmIssueAufbereitungMonitoringService.RunOnceAsync` auf (Backmerge-Präzedenz,
  unabhängig von `scm.monitoring.enabled`).
- **Konstruktor:** neue Dependency `ScmIssueAufbereitungMonitoringService` (Singleton,
  analog `ScmBackmergeMonitoringService`).

### `App` (`App.xaml.cs`, DI-Registrierung)

- **Neue Registrierung:** `services.AddSingleton<ScmIssueAufbereitungMonitoringService>();`
  und `services.AddHostedService(sp =>
  sp.GetRequiredService<ScmIssueAufbereitungMonitoringService>());` — direkt neben der
  `ScmBackmergeMonitoringService`-Registrierung (~Z. 238-239).

### `ScmDispatchFixture` (Testinfrastruktur)

- **Neue Registrierung:** `services.AddSingleton<ScmIssueAufbereitungMonitoringService>();`
  (Muster: `AddSingleton<ScmBackmergeMonitoringService>`).

## Datenbankmigrationen

Keine. `ScmObservationCheckpoint.SourceKey` ist ein freier Namensraum — die neuen Präfixe
`issueaufbereitung-repository/`, `issueaufbereitung-enumeration/` und
`issueaufbereitung-issue/` benötigen keine Schema-Änderung.

## Validierungsregeln

Keine. Es werden keine neuen Benutzereingaben oder Konfigurationswerte eingeführt; die
bestehenden Schalter (`scm.issueaufbereitung.enabled`, `GitRepository.IssueAufbereitungAktiv`)
bleiben die einzigen Eingriffspunkte.

## Konfigurationsänderungen

Keine. Laufzeit-Schalter `scm.issueaufbereitung.enabled` (Standard `false`, opt-in),
Repository-Freischaltung `IssueAufbereitungAktiv`, `scm.issueaufbereitung.plugin`,
`scm.issueaufbereitung.comment.enabled` und `scm.monitoring.accountmode` werden wie bisher
pro Zyklus gelesen. `ScmEventOptions.PollInterval` bestimmt den Zyklusrhythmus.

## Seiteneffekte und Risiken

- **Doppelte Erkennungspfade:** Monitor (`!inboxMode`-Guard) und neuer Dienst können
  dasselbe Issue finden; `EventKey`-/`SourceKey`-Unique-Indizes, der neue
  Auswertungs-Checkpoint und die bestehende `DbUpdateException`-Anlagerennen-Behandlung
  verhindern Doppelanlagen. Der zusätzliche Beschreibungs-Read im Renner-Fall ist
  vernachlässigbar.
- **Geändertes Erstbeobachtungs-Kriterium:** Ohne die Snapshot-Prüfung wird ein Issue, das
  nach der Baseline angelegt, aber zuvor schon (z. B. über Erwähnungen) gelesen wurde, nun
  korrekt aufbereitet statt verworfen — gewünschter Verhaltenswechsel. Der theoretische Fall
  „Issue nach Baseline angelegt, während der Funktionsschalter kurz deaktiviert war“ wird
  jetzt nachträglich aufbereitet — fachlich konsistent mit „neu angelegt seit Baseline“.
- **Rate-Limit:** Der je-Issue-Checkpoint begrenzt Beschreibungs-Reads strikt auf neue bzw.
  fehlerbehaftete Referenzen; der Erstlauf liest keine Beschreibungen. Die
  `NextAttemptUtc`-Resets durch „Quellen erneut prüfen“ lösen wegen der
  Existenz-/Fehler-Prüfung keine Massen-Reads aus.
- **Konto-Tagging:** Die umgetaggte Referenz fließt in `ObservationJson` des internen
  `ScmIncomingEvent`; die Ausführung (`DispatchAufbereitungAsync`) wählt danach das
  Monitoring-Credential — unverändertes Verhalten des Ausführungspfads, nur die Herkunft der
  `Konto`-Angabe ist neu.
- **Dispatch-Timing bei `scm.monitoring.enabled = false`:** Der neue Dienst stößt
  `DispatchDueEventsAsync` selbst an; ohne diesen Aufruf würden fällige
  Aufbereitungsereignisse liegen bleiben.
- **Checkpoint-Menge:** Ein `ScmObservationCheckpoint`-Datensatz je evaluiertem Issue —
  lineares, fachlich begrenztes Wachstum (nur freigeschaltete Repositories).

## Umsetzungsreihenfolge

1. **Auswertungs-Checkpoint in `ScmIssueAufbereitungsService` verankern**
   - Voraussetzungen: Keine (bestehende Klasse, `SoftwareschmiededDbContext` bereits injiziert).
   - Beschreibung: `AuswertungsCheckpointKey`-Hilfsmethode hinzufügen; in
     `ErkennenUndEinreihenAsync` die Snapshot-Erstbeobachtung durch die Checkpoint-Prüfung
     ersetzen und den Checkpoint nach abgeschlossener Auswertung (inkl. Skips) mit
     Endzustand schreiben.

2. **`ScmIssueAufbereitungMonitoringService` anlegen**
   - Voraussetzungen: Schritt 1; im Repo vorhanden: `PeriodicBackgroundService`,
     `ScmCheckpointSupport`, `IScmEventSource`, `IPluginManager`, `ScmPluginMatcher`,
     `ScmEventStartupGate`, `ScmEventChangeNotifier`, `AppEinstellungService`,
     `ScmEventOptions`, `ScmEventOrchestrator`.
   - Beschreibung: `RunOnceAsync` gemäß Programmablauf implementieren — Laufzeit-Schalter,
     Alias-Auflösung unter `Benutzerkonto` mit `issueaufbereitung-repository/`-Checkpoints,
     Enumeration mit `issueaufbereitung-enumeration/`-Prefix, Baseline-Erstlauf mit
     Retro-Markierung, je-Issue-Auswertung mit Konto-Tagging und Fehlerisolation,
     abschließend `DispatchDueEventsAsync`.

3. **DI-Registrierung in `App.xaml.cs`**
   - Voraussetzungen: Schritt 2.
   - Beschreibung: `AddSingleton` + `AddHostedService` nach dem
     `ScmBackmergeMonitoringService`-Muster (~Z. 238-239).

4. **UI-Anstoß in `ScmEventQueueViewModel.RetrySourcesCommand`**
   - Voraussetzungen: Schritt 3 (Singleton muss injizierbar sein).
   - Beschreibung: Neue Dependency im Konstruktor, `RunOnceAsync`-Aufruf neben dem
     Backmerge-Aufruf ergänzen.

5. **Testinfrastruktur erweitern**
   - Voraussetzungen: Schritt 2.
   - Beschreibung: `ScmDispatchFixture` um `AddSingleton<ScmIssueAufbereitungMonitoringService>`
     ergänzen; neue Testdatei `ScmIssueAufbereitungMonitoringServiceTests` samt privaten
     Hilfsmethoden (Muster: `ScmBackmergeMonitoringServiceTests`,
     `ScmEventMonitoringServiceTests_IssueAufbereitung`) anlegen.

6. **Integrationstests schreiben** (Schritte 5 vorausgesetzt; Testliste siehe unten).

7. **Betroffene bestehende Tests verifizieren und anpassen**
   - Voraussetzungen: Schritte 1–4.
   - Beschreibung: `ScmEventMonitoringServiceTests_IssueAufbereitung` auf das geänderte
     Auswertungs-Kriterium prüfen/angleichen; `ScmEventQueueViewModelTests` an die geänderte
     Konstruktor-Signatur anpassen; anschließend voller Build + stabile Testlane
     (`--filter "Category!=OsInterface"`).

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `AufbereitungMonitor_SeparatesKonto_NewIssue_CreatesPreparationTaskAndInternalEvent` | `ScmIssueAufbereitungMonitoringServiceTests` | Happy Path: bei `SeparatesKonto` wird ein neu angelegtes Issue einer freigeschalteten Repository als `Zweck`-Aufgabe mit `…/IssueAufbereitung/{n}`-SourceKey und sofort fälligem internem `ScmIncomingEvent` eingereiht (Akzeptanzkriterium a). |
| `AufbereitungMonitor_FirstRun_SetsBaselineAndMarksAllReferencesEvaluated` | `ScmIssueAufbereitungMonitoringServiceTests` | Erstlauf: Baseline-Checkpoint `issueaufbereitung/…` wird angelegt, alle enumerierten Issue-Referenzen erhalten `issueaufbereitung-issue/…`-Endzustand-Checkpoints; `ReadEventsAsync` wird nie aufgerufen (kein Retro-Scan). |
| `AufbereitungMonitor_RepeatedCycle_NoDescriptionReread` | `ScmIssueAufbereitungMonitoringServiceTests` | Folgezyklen lesen für ausgewertete Issues keine Beschreibungen erneut (Verify `ReadEventsAsync` nur für neue Referenzen). |
| `AufbereitungMonitor_Disabled_NoProviderWork` | `ScmIssueAufbereitungMonitoringServiceTests` | `scm.issueaufbereitung.enabled = false` ⇒ keinerlei Provider-Aufrufe (Verify `GetEventRepositoryAsync`/`EnumerateReferencesAsync` `Times.Never`). |
| `AufbereitungMonitor_RepositoryFlagDisabled_NoProviderWork` | `ScmIssueAufbereitungMonitoringServiceTests` | Ohne `IssueAufbereitungAktiv` wird die Repository nicht einmal aufgelöst. |
| `AufbereitungMonitor_EventMonitoringDisabled_AufbereitungStillRuns` | `ScmIssueAufbereitungMonitoringServiceTests` | `scm.monitoring.enabled = false` hindert den Aufbereitungs-Zyklus nicht (Offene Frage 4, Backmerge-Präzedenztest). |
| `AufbereitungMonitor_RepositoryResolution_UsesBenutzerkontoContext` | `ScmIssueAufbereitungMonitoringServiceTests` | Bei konfiguriertem `SeparatesKonto` läuft `GetEventRepositoryAsync` dennoch mit `ScmRepositoryContext.Konto == ScmKontoModus.Benutzerkonto`. |
| `AufbereitungMonitor_SeparatesKonto_EventCarriesMonitoringKonto` | `ScmIssueAufbereitungMonitoringServiceTests` | Die persistierte Referenz des internen Ereignisses trägt `Konto == SeparatesKonto` (Offene Frage 3 — Credential-Wahl des Ausführungspfads). |
| `AufbereitungMonitor_MentionPrereadIssue_StillPrepared` | `ScmIssueAufbereitungMonitoringServiceTests` | Beschreibung wurde zuvor gelesen (`ScmContentSnapshot` existiert, Erwähnungs-Simulation) — das Issue wird trotzdem aufbereitet (Offene Frage 2, Lückenschluss). |
| `AufbereitungMonitor_ReadFailure_IsolatesSingleIssue` | `ScmIssueAufbereitungMonitoringServiceTests` | `ReadEventsAsync`-Fehler für ein Issue ⇒ `issueaufbereitung-issue/…`-Checkpoint mit Fehler/Backoff; andere Issues werden weiter ausgewertet; Enumerations-Watermark bleibt wirksam. |
| `AufbereitungMonitor_AliasResolutionFailure_IsolatesRepository` | `ScmIssueAufbereitungMonitoringServiceTests` | `GetEventRepositoryAsync`-Fehler ⇒ `issueaufbereitung-repository/…`-Checkpoint mit `retryPermanent`-Backoff; andere Aliase laufen weiter. |
| `AufbereitungMonitor_NonIssueReferences_Ignored` | `ScmIssueAufbereitungMonitoringServiceTests` | `PullRequest`-Referenzen lösen weder Beschreibungs-Read noch Auswertung aus. |
| `AufbereitungMonitor_ModeSwitch_NoDuplicateTasks` | `ScmIssueAufbereitungMonitoringServiceTests` | Moduswechsel `Benutzerkonto` ↔ `SeparatesKonto` mit paralleler Monitor-Erkennung ⇒ genau eine `Zweck`-Aufgabe je Issue (Akzeptanzkriterium b, `EventKey`/`SourceKey`-Dedup). |
| `AufbereitungMonitor_EnqueuedEvent_IsDispatchedWithinCycle` | `ScmIssueAufbereitungMonitoringServiceTests` | Nach dem Zyklus wurde `DispatchDueEventsAsync` angestoßen (bei deaktiviertem Monitoring-Schalter) — oder Verify auf Orchestrator-Aufruf. |
| Hilfsmethoden `AufbereitungMonitor(fixture)`, `EnableAsync`, `AddRepositoryAsync`, `ConfigureEnumeration`, `ConfigureDescriptions`, `AddSnapshotAsync` | `ScmIssueAufbereitungMonitoringServiceTests` | Muster aus `ScmBackmergeMonitoringServiceTests` / `ScmEventMonitoringServiceTests_IssueAufbereitung`; `AddSnapshotAsync` simuliert den Erwähnungs-Vorgriff. |
| `RetrySourcesCommand_StillRunsAufbereitungCycle` (neu) | `ScmEventQueueViewModelTests` | „Quellen erneut prüfen“ stößt den neuen Zyklus an — auch bei deaktiviertem `scm.monitoring.enabled` (Präzedenz: `RetrySourcesCommand_MonitoringDisabled_StillRunsBackmergeCycle`). |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `ScmEventMonitoringServiceTests_IssueAufbereitung` | Das Erstbeobachtungs-Kriterium wechselt von `ScmContentSnapshot` zum Auswertungs-Checkpoint; insbesondere `Monitor_RepeatedPolls_CreateNoSecondPreparation` und `Monitor_DescriptionChangeAfterPreparation_CreatesNoLoop` müssen gegen den neuen Mechanismus verifiziert werden (Dedup erfolgt weiterhin über `EventKey`/Checkpoint). |
| `ScmEventQueueViewModelTests` | Neue Konstruktor-Dependency `ScmIssueAufbereitungMonitoringService` — alle bestehenden Setups anpassen. |
| `ScmDispatchFixture`-Konsumenten allgemein | Neue Singleton-Registrierung ist additiv; Seiteneffekte nur prüfen, falls ein Test alle registrierten Dienste enumeriert. |

### E2E-Tests (Pflicht)

Das Feature führt keine neue Benutzerinteraktion ein — kein neues Fenster, kein neuer
Dialog, kein neues UI-Element. Die einzige UI-Berührung ist die Erweiterung des bestehenden
„Quellen erneut prüfen“-Befehls um einen zusätzlichen Hintergrund-Anstoß (Backend-Wirkung).
Der fachliche Happy Path (Erkennung → Einreihung → Ausführung) ist ein reiner
Hintergrundprozess; die SCM-Monitoring-Features des Projekts werden etabliert über die
realistischen Integrationstests des `ScmDispatchFixture` (echter Provider-Durchlauf mit
gemockter Plugin-Boundary) abgedeckt, nicht über FlaUI. Entsprechend:

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| Neu angelegtes Issue im `SeparatesKonto` wird erkannt, eingereiht und vom Aufbereitungs-Ausführungspfad verarbeitet (Ende-zu-Ende über Erkennungszyklus + `DispatchDueEventsAsync`) | `ScmIssueAufbereitungMonitoringServiceTests` (Integrationstest, `ScmDispatchFixture`) | Akzeptanzkriterium (a) |
| Moduswechsel ohne Erkennungslücke und ohne Doppelaufträge | `ScmIssueAufbereitungMonitoringServiceTests` (`AufbereitungMonitor_ModeSwitch_NoDuplicateTasks`) | Akzeptanzkriterium (b) |
| Vollständige Erkennungs- und Ausführungsabdeckung analog `ScmEventMonitoringServiceTests_IssueAufbereitung` / `ScmEventDispatchTests_IssueAufbereitung` | `ScmIssueAufbereitungMonitoringServiceTests` + bestehende `ScmEventDispatchTests_IssueAufbereitung` (Ausführungspfad unverändert) | Akzeptanzkriterium (c) |

Welche bestehenden E2E-Tests müssen angepasst werden?

Keine — es existieren keine FlaUI-E2E-Tests für die SCM-Ereignisüberwachung; die UI-Änderung
ist auf den `RetrySourcesCommand`-Anstoß beschränkt und wird über
`ScmEventQueueViewModelTests` abgedeckt.

## Offene Punkte

Keine. Alle fünf offenen Fragen der Anforderung wurden als Designentscheidungen getroffen
(siehe Tabelle oben): je-Issue-Auswertungs-Checkpoint statt Nummern-Wasserstand,
Checkpoint-basiertes „ausgewertet“-Kriterium statt Snapshot-Erstbeobachtung, Konto-Tagging
auf den konfigurierten `kontoModus`, Unabhängigkeit von `scm.monitoring.enabled` sowie
Anbindung an „Quellen erneut prüfen“.
