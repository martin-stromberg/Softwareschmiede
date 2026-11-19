# Logik und Dienste

## `PeriodicBackgroundService` (Basisklasse)

Datei: `src/Softwareschmiede/Application/Services/PeriodicBackgroundService.cs`

Abstrakte `BackgroundService`-Basis für alle periodischen SCM-Dienste.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `PeriodicBackgroundService(TimeSpan, TimeProvider, ILogger, string fehlerMeldung)` | `protected` | Konstruktor: Poll-Intervall, testbare Zeitquelle, Logger, Fehlermeldung. |
| `ExecuteAsync(CancellationToken)` | `protected override` | Schleife: `RunOnceAsync` aufrufen, Fehler pro Durchlauf loggen (kein Abbruch), danach `Task.Delay` über `TimeProvider`; `OperationCanceledException` = regulärer Shutdown. |
| `RunOnceAsync(CancellationToken)` | `public abstract` | Einzelner Monitoring-Durchlauf — auch aus der UI direkt anstoßbar (`RetrySourcesCommand`). |

## `ScmBackmergeMonitoringService` — Vorbild für den neuen Dienst

Datei: `src/Softwareschmiede/Application/Services/ScmBackmergeMonitoringService.cs` (124 Zeilen)

Gehosteter `PeriodicBackgroundService` (`options.Value.PollInterval`), der repository-weit
unabhängig vom Überwachungsmodus und immer unter `ScmKontoModus.Benutzerkonto` enumeriert.

Ablauf in `RunOnceAsync` (Z. 32-123):
1. `startup.WaitAsync(ct)` — Startup-Gate abwarten.
2. Scope öffnen; Laufzeit-Schalter `GetScmBackmergeModusAsync` — bei `Deaktiviert` Early-Return
   ohne Nebenwirkungen (Z. 36-43).
3. Alle `GitRepository` mit `Aktiv` laden (Z. 46); Plugins über
   `IPluginManager.GetSourceCodeManagementPlugins()` iterieren, nur `IScmEventSource` nutzen
   (Z. 47-51).
4. Alias-Gruppen über `RepositoryUrl` bilden (`ScmPluginMatcher.MatchesPlugin`, Z. 54-57).
5. Je Alias: eigener Scope, Checkpoint `backmerge-repository/{prefix}/{alias}` (Z. 62-64);
   bei ausstehendem `NextAttemptUtc` überspringen; `GetEventRepositoryAsync` mit
   `new ScmRepositoryContext(aliasGroup[0].Id, plugin.PluginPrefix, alias,
   ScmKontoModus.Benutzerkonto)` (Z. 70-71); `localRepositories`-Mapping
   `ScmEventRepository → List<GitRepository>` (Z. 72-76); Erfolgs-Checkpoint entfernen +
   `ScmEventChangeNotifier.Notify()` (Z. 77-83); Fehler via
   `ScmCheckpointSupport.RecordFailureAsync(…, retryPermanent: true)` isolieren nur dieses
   Repository (Z. 86-94).
6. Laufweites `enumerated`-HashSet; je aufgelöstem Repository
   `ScmCheckpointSupport.EnumerateRepositoryAsync` mit `checkpointKeyPrefix =
   "backmerge-enumeration"` (Z. 98-107); im `verarbeite`-Callback
   `ScmBackmergeService.EvaluateDiscoveredReferencesAsync` mit eigenem try/catch — ein
   Bewertungsfehler setzt den Enumerations-Watermark nicht zurück (Z. 107-120).

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `RunOnceAsync(CancellationToken)` | `public override` | Vollständiger Prüfzyklus wie oben; kein weiterer Member. |

DI: `services.AddSingleton<ScmBackmergeMonitoringService>(); services.AddHostedService(sp =>
sp.GetRequiredService<ScmBackmergeMonitoringService>());` (`App.xaml.cs:238-239`).

## `ScmEventMonitoringService`

Datei: `src/Softwareschmiede/Application/Services/ScmEventMonitoringService.cs` (306 Zeilen)

Repository-weite Ereignisüberwachung; `PeriodicBackgroundService` im selben
`PollInterval`-Rhythmus.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `RunOnceAsync(CancellationToken)` | `public override` | Hauptzyklus (Z. 30-247), siehe Details unten. |
| `EnumerateNotificationsAsync(…)` | `private` | Inbox-Verarbeitung des `SeparatesKonto` (Z. 252-298): Watermark-Commit erst nach fehlerfreiem Zyklus (`pendingInboxCommits`), Fremd-Notifications verworfen mit `Inbox:`-Diagnose. |
| `IsInaccessible(string?)` | `private static` | `Permanent`/`Permission`-Fehlercodes = Repository für das Konto nicht sichtbar (Z. 304-305). |

Zentrale Stellen in `RunOnceAsync`:

- Z. 38: `GetScmMonitoringEnabledAsync` (`scm.monitoring.enabled`) — Early-Return.
- Z. 43-46: liest Trigger-/Delay-Keyword, `kontoModus` (`GetScmKontoModusAsync`) und
  `issueAufbereitungAktiv` (`GetScmIssueAufbereitungEnabledAsync`) pro Zyklus.
- Z. 50-51: lädt Aufgaben ohne `Zweck` (`x.Zweck == null`) als verknüpfte Quellen.
- Z. 76: `inboxMode = kontoModus == ScmKontoModus.SeparatesKonto && plugin is IScmNotificationSource`.
- Z. 86-130: Alias-Auflösung mit Checkpoint `repository/{prefix}/{alias}` — analog zum
  Backmerge-Muster, aber unter dem konfigurierten `kontoModus` und mit `complete`-Gate
  (Z. 132: unaufgelöste Aliase stoppen den Plugin-Durchlauf, außer im Inbox-Modus bei
  „inaccessible“-Fehlern).
- Z. 133-161: Im `SeparatesKonto` ersetzt `EnumerateNotificationsAsync` die Enumeration;
  sonst `EnumerateRepositoryAsync` mit `checkpointKeyPrefix = "enumeration"`.
- **Z. 198-207 — der entscheidende Guard:** Die „Issue erstellt“-Erkennung läuft vor
  `AcceptBatchAsync`, nur wenn `!inboxMode && issueAufbereitungAktiv &&
  reference.Kind == ScmReferenceKind.Issue && kind == ScmContentKind.Description &&
  batch.Observations.Count > 0` und lokale Repositories zugeordnet sind → Aufruf von
  `ScmIssueAufbereitungsService.ErkennenUndEinreihenAsync(source, reference, batch,
  aufbereitungRepositories, ct)`. Im `SeparatesKonto` entfällt dieser Pfad komplett.
- Z. 246: Am Ende jedes Zyklus `ScmEventOrchestrator.DispatchDueEventsAsync(ct)`.

DI: `AddSingleton` + `AddHostedService` (`App.xaml.cs:236-237`).

## `ScmIssueAufbereitungsService`

Datei: `src/Softwareschmiede/Application/Services/ScmIssueAufbereitungsService.cs` (144 Zeilen)

Scoped-Service; einziger Anlagepfad für Aufbereitungsaufträge.

| Methode/Member | Sichtbarkeit | Kurzbeschreibung |
|----------------|-------------|------------------|
| `Zweck` (`const string = "IssueAufbereitung"`) | `public` | Einziger Zweck-Token des Systems (für `Aufgabe.Zweck`/`ScmIncomingEvent.Zweck`). |
| `BaselineKey(ScmEventRepository)` | `public static` | Checkpoint-Key `issueaufbereitung/{provider}/{host}/{repoId}` (Z. 35-37). |
| `AufbereitungsSourceKey(ScmEventReference)` | `public static` | SourceKey `{provider}/{host}/{repoId}/IssueAufbereitung/{number}` — eigener Namensraum, kollidiert nie mit `…/Issue/{number}` (Z. 43-47). |
| `AufbereitungsEventKey(ScmEventObservation)` | `public static` | `{ContentKey}/IssueAufbereitung` — deterministischer EventKey (Z. 52-53). |
| `ErkennenUndEinreihenAsync(IScmEventSource, ScmEventReference, ScmEventBatch, IReadOnlyList<GitRepository>, CancellationToken)` | `public` | Auswertung eines Beschreibungs-Batches (Z. 68-143), Details unten. |

Details `ErkennenUndEinreihenAsync`:

- Z. 71-74: filtert `repositories` auf `IssueAufbereitungAktiv`, sortiert
  `OrderBy(RepositoryUrl).ThenBy(Id)`; ohne Freischaltung oder leeren Batch: Rückkehr.
- Z. 75-89: Baseline-Logik — fehlt der `issueaufbereitung/…`-Checkpoint, wird er mit
  `WatermarkUtc = jetzt` angelegt und auslöserlos beendet (kein Retro-Scan).
- Z. 92-95: `CreatedAt <= baselineUtc` ⇒ Altbestand, übersprungen.
- Z. 96-97: bereits vorhandener `ScmContentSnapshot` zum `ContentKey` ⇒ keine Erstbeobachtung.
- Z. 98-100: `ScmIncomingEvents.AnyAsync(EventKey)` ⇒ bereits eingereiht.
- Z. 101-106: `source.ReadIssueAsync(reference)` lädt die Anlagedaten; `null` ⇒ Warnung + Skip.
- Z. 107-109: `AufgabeService.CreateFromIssueEventAsync(repository.ProjektId, issue,
  repository.Id, AufbereitungsSourceKey(reference), ct, Zweck, pluginPrefix)` —
  `pluginPrefix` aus `GetScmIssueAufbereitungPluginPrefixAsync`.
- Z. 112-129: internes `ScmIncomingEvent` (`EventKey`, `ObservationJson`, `Zweck`, `Reason`,
  `NextAttemptUtc` leer = sofort fällig) + `Protokolleintrag` (`SystemMeldung`).
- Z. 130-139: `DbUpdateException` beim `SaveChangesAsync` = Anlagerennen über den
  `EventKey`-Unique-Index → `ChangeTracker.Clear()` + `continue`.
- Z. 140-141: `notifier.Notify()` + Log.

Konstruktor-Dependencies: `SoftwareschmiededDbContext`, `AufgabeService`,
`AppEinstellungService`, `ScmEventChangeNotifier`, `TimeProvider`,
`ILogger<ScmIssueAufbereitungsService>`. DI: `AddScoped` (`App.xaml.cs:234`).

## `ScmCheckpointSupport`

Datei: `src/Softwareschmiede/Application/Services/ScmCheckpointSupport.cs` — `internal static`,
gemeinsame Checkpoint-Verwaltung aller zyklischen SCM-Beobachtungsdienste.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `RecordFailureAsync(IServiceScopeFactory, TimeProvider, string sourceKey, Exception, CancellationToken, string diagnosticSuffix = "", bool retryPermanent = false)` | `internal static` | Eigener Scope; legt Checkpoint bei Bedarf an und schreibt Fehler via `ApplyFailure`; benachrichtigt `ScmEventChangeNotifier` (Z. 23-40). |
| `ApplyFailure(ScmObservationCheckpoint, Exception, string diagnostic, long nowUtcMs, bool retryPermanent = false)` | `internal static` | Erhöht `Failures`, setzt `ErrorCode`/`Diagnostic`; `Authentication`/`Permission` (und `Permanent` ohne `retryPermanent`) ⇒ `NextAttemptUtc = long.MaxValue` (dauerhaft), sonst Backoff via `ScmEventStore.RetryAt` unter Berücksichtigung von `RetryAfterUtc` (Z. 51-62). |
| `EnumerateRepositoryAsync(IServiceScopeFactory, TimeProvider, TimeSpan pollInterval, IScmEventSource, ScmEventRepository, string checkpointKeyPrefix, HashSet<string> enumerated, Func<IReadOnlyList<ScmEventReference>, CancellationToken, Task> verarbeite, CancellationToken)` | `internal static` | Enumeration mit eigenem Checkpoint `{prefix}/{provider}/{host}/{repoId}`: `enumerated`-Dedup je Lauf, Backoff-Skip, `sinceUtc = Watermark - 2*pollInterval`, `source.EnumerateReferencesAsync`, `verarbeite`-Callback läuft **innerhalb** des Watermark-Commits (Fehler verhindern den Commit und zählen als Enumerationsfehler via `RecordFailureAsync` ohne `retryPermanent`), Erfolg setzt Watermark/räumt Fehler + `Notify()`. Rückgabe `false` bei Fehlschlag (Z. 78-120). |

## `ScmEventStore` (relevante Member)

Datei: `src/Softwareschmiede/Application/Services/ScmEventStore.cs`

| Member | Kurzbeschreibung |
|--------|------------------|
| `AmbiguousAssignmentDiagnostic` (`const`) | `"Mehrdeutige Zuordnung zu Aufgaben"`. |
| `UnassignedDiagnostic` (`const`) | `"Keine Aufgabe zugeordnet"`. |
| `InboxDiscardDiagnosticPrefix` (`const`) | `"Inbox:"` — nicht-alarmierende Verwerfungsdiagnose. |
| `JsonOptions` (`static readonly`) | `JsonSerializerDefaults.Web` — Serialisierung der Observation. |
| `Observation(ScmIncomingEvent)` (`static`) | Deserialisiert `ObservationJson`. |
| `RetryAt(int failures, long now, DateTimeOffset? retryAfter)` (`static`) | Backoff-Berechnung für `NextAttemptUtc`. |
| `AcceptBatchAsync(Guid? aufgabeId, string sourceKey, ScmEventBatch, bool unassigned, string? triggerKeyword, string? delayKeyword, CancellationToken)` | Transaktionale Annahme eines Batches samt Watermark-Checkpoint und `ScmContentSnapshot`-Schreibung — der Aufruf **nach** `ErkennenUndEinreihenAsync` (Z. 50 ff.). |

## `ScmBackmergeService`

Datei: `src/Softwareschmiede/Application/Services/ScmBackmergeService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `EvaluateDiscoveredReferencesAsync(IGitPlugin plugin, ScmEventRepository repository, IReadOnlyList<ScmEventReference> discovered, IReadOnlyList<GitRepository> lokaleRepositories, CancellationToken)` | `public` | Bewertet enumerierte Referenzen (nur `PullRequest`) auf das Backmerge-Muster; je PR ein `backmerge/{provider}/{host}/{repoId}/{number}`-Checkpoint mit eigener Fehlerisolation (Z. 37 ff.). Vorbild für die Referenzverarbeitung im `verarbeite`-Callback. |

## `ScmEventOrchestrator` (Aufbereitungs-Ausführungspfad)

Datei: `src/Softwareschmiede/Application/Services/ScmEventOrchestrator.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `DispatchDueEventsAsync(CancellationToken)` | `public` | Priorisierte Auswahl fälliger Ereignisse; wird am Ende jedes Monitoring-Zyklus aufgerufen. |
| `DispatchAsync(Guid eventId, string? explicitPluginPrefix, CancellationToken)` | `public` | Expliziter Dispatch eines Ereignisses. |
| `DispatchCoreAsync(Guid, Guid, string?, CancellationToken, string?)` | `private` | Generischer Zustellpfad; Z. 135-137: `item.Zweck == ScmIssueAufbereitungsService.Zweck` → `DispatchAufbereitungAsync`. |
| `DispatchAufbereitungAsync(ScmIncomingEvent, string?, CancellationToken)` | `private` | Dedizierter Aufbereitungspfad (Z. 607 ff.): prüft `GetScmIssueAufbereitungEnabledAsync` und `GitRepository.IssueAufbereitungAktiv` zur Laufzeit (Z. 650-653), geschlossenes Issue ⇒ sauber beenden (Z. 670), Plugin-Auswahl über `GetScmIssueAufbereitungPluginPrefixAsync ?? task.KiPluginPrefix` (Z. 685-702), Analyse-Prompt via `ScmIssueAufbereitungPrompt.Build` (Z. 753), Exchange-Ergebnis als `IssueBodyUpdate` + optionaler Kommentar (`GetScmIssueAufbereitungKommentarEnabledAsync`, Z. 844). |
| `ReconcileAufbereitungAttemptAsync(…)` | `private` | Abgleich des Aufbereitungsversuchs gegen die Exchange-Dateien (Z. 824 ff.). |
| `ProtokolliereAufbereitungsFehlerAsync(…)` | `private` | Ehrliche Fehlerprotokollierung ohne Fehlerkommentar (Z. 810-815). |
| `WaitAsync` / `WaitWithCommentAsync` | `private` | Ehrliche Wartegründe (`Wartend` mit `Reason`). |

## `ScmPluginMatcher`

Datei: `src/Softwareschmiede/Application/Services/ScmPluginMatcher.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `MatchesPlugin(IGitPlugin, GitRepository)` | `internal static` | Ordnet ein lokales Repository dem Plugin zu — `PluginPrefix` oder `PluginName` == `PluginTyp` (OrdinalIgnoreCase). |

## `ScmEventOptions` / `ScmEventChangeNotifier` / `ScmEventStartupGate`

Datei: `src/Softwareschmiede/Application/Services/ScmEventOptions.cs`

| Member | Kurzbeschreibung |
|--------|------------------|
| `ScmEventOptions.PollInterval` (`TimeSpan`, Standard 60 s) | Abstand zwischen Remoteabfragen; Konfigurationssektion `"ScmEvents"` (`App.xaml.cs:222`). |
| `ScmEventOptions.AcceptanceTimeout` / `CompletionTimeout` | Fristen für Annahme/Abschluss. |
| `ScmEventChangeNotifier.Changed` / `Notify()` | Benachrichtigt Ansichten über dauerhafte Änderungen (Singleton). |
| `ScmEventStartupGate.WaitAsync(CancellationToken)` / `Ready()` | Hält neue Hintergrunddienste bis nach der Datenbankmigration an; `Ready()` wird in `App.xaml.cs:114` gesetzt. |

## `ScmTaskCompletionMonitoringService` (dritter Dienst nach demselben Muster)

Datei: `src/Softwareschmiede/Application/Services/ScmTaskCompletionMonitoringService.cs`

Weiterer `PeriodicBackgroundService` (`PollInterval`), ebenfalls `startup.WaitAsync` +
eigenem Laufzeit-Schalter (`GetScmTaskCompletionEnabledAsync`, Z. 34-44). DI:
`AddSingleton` + `AddHostedService` (`App.xaml.cs:312-313`) — bestätigt das etablierte
Registrierungsmuster für zusätzliche gehostete Dienste.

## `AppEinstellungService` (relevante SCM-Einstellungen)

Datei: `src/Softwareschmiede/Application/Services/AppEinstellungService.cs` — Scoped-Service.

| Member | Kurzbeschreibung |
|--------|------------------|
| `ScmMonitoringEnabledKey` = `"scm.monitoring.enabled"` | Globaler Schalter der Ereignisüberwachung (Z. 43). |
| `ScmIssueAufbereitungEnabledKey` = `"scm.issueaufbereitung.enabled"` | Funktionsschalter Issue-Aufbereitung (Z. 46). |
| `ScmIssueAufbereitungPluginKey` = `"scm.issueaufbereitung.plugin"` | Plugin-Zuordnung der Aufbereitung (Z. 49). |
| `ScmIssueAufbereitungKommentarEnabledKey` = `"scm.issueaufbereitung.comment.enabled"` | Kommentar-Schalter (Z. 52). |
| `ScmKontoModusKey` = `"scm.monitoring.accountmode"` | Überwachungsmodus (Z. 112). |
| `GetScmMonitoringEnabledAsync(ct)` → `bool` | Standard `true` (Z. 208-209). |
| `GetScmIssueAufbereitungEnabledAsync(ct)` → `bool` | Standard `false`, opt-in (Z. 217-218). |
| `GetScmIssueAufbereitungPluginPrefixAsync(ct)` → `string?` | Fehlende/leere Werte ⇒ `null` (Z. 228-233). |
| `GetScmIssueAufbereitungKommentarEnabledAsync(ct)` → `bool` | Standard `true` (Z. 240-241). |
| `GetScmBackmergeModusAsync(ct)` → `ScmBackmergeModus` | Standard `Deaktiviert` (Z. 255-260). |
| `GetScmKontoModusAsync(ct)` → `ScmKontoModus` | Standard `Benutzerkonto` (Z. 282-287). |
| `GetScmTriggerKeywordAsync` / `GetScmDelayKeywordAsync` | Keywords der Ereignisvalidierung (Z. 407, 418). |

## `AufgabeService` (relevante Methode)

Datei: `src/Softwareschmiede/Application/Services/AufgabeService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `CreateFromIssueEventAsync(Guid projektId, Issue issue, Guid gitRepositoryId, string sourceKey, CancellationToken ct = default, string? zweck = null, string? kiPluginPrefix = null)` | `public` | Dedup über `IssueReferenz.SourceKey`-Unique-Index: bestehende Aufgabe bei Treffer bzw. `DbUpdateException`-Anlagerennen zurückgeben; legt `Aufgabe` (`Zweck`, `KiPluginPrefix`) + `IssueReferenz` an (Z. 282-344). |

## `ScmEventQueueViewModel` — „Quellen erneut prüfen“

Datei: `src/Softwareschmiede.App/ViewModels/ScmEventQueueViewModel.cs`

| Member | Kurzbeschreibung |
|--------|------------------|
| `RetrySourcesCommand` (`AsyncRelayCommand`, Z. 47-65) | Setzt **alle** `NextAttemptUtc` der `ScmObservationCheckpoints` zurück (Z. 52-53), ruft `ScmEventMonitoringService.RunOnceAsync` nur bei aktivierter `scm.monitoring.enabled` (Z. 54-61) und `ScmBackmergeMonitoringService.RunOnceAsync` unabhängig davon (Z. 62-64, Backmerge-Präzedenz für den neuen Dienst). |
| `RefreshAsync` | Lädt Queue, Quellen-Diagnosen (`ScmObservationCheckpoints` mit `Diagnostic`) und Outbox-Fehler; baut `MonitoringNotice`/`Diagnostics` (Z. 159-240). |

## DI-Registrierung (`App.xaml.cs`)

Datei: `src/Softwareschmiede.App/App.xaml.cs`, Methode `ConfigureServices` (~Z. 220-241):

- Z. 222: `services.Configure<ScmEventOptions>(context.Configuration.GetSection("ScmEvents"))`.
- Z. 224-225: `ScmEventStartupGate`, `ScmEventChangeNotifier` als Singletons.
- Z. 227-235: `ScmEventStore`, `ScmEventOutbox`, `ScmEventOrchestrator`,
  `ScmEventExecutionLimitService`, `ScmEventTaskResolver`, `ScmIssueAufbereitungsService`,
  `ScmBackmergeService` — jeweils Scoped.
- Z. 236-239: `ScmEventMonitoringService` und `ScmBackmergeMonitoringService` — jeweils
  `AddSingleton` + `AddHostedService(sp => sp.GetRequiredService<…>())` — **das Muster für den
  neuen Dienst**.
- Z. 240-241: `ScmEventExchangeService` (Singleton + Hosted).
- Z. 312-317: `ScmTaskCompletionMonitoringService`, `ScmStartPruefungsAusloeser`,
  `ScmStartPruefungsService` (Hosted).
- Z. 114: `_host.Services.GetRequiredService<ScmEventStartupGate>().Ready()` nach der Migration.
- Z. 398-409: E2E-Testkonfiguration (`ScmE2ETestConfiguration`, `PollInterval = 300 ms`).
