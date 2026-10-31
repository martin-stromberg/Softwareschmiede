# Logik: GitHub-Notifications als Eingangskanal

Betroffene Logikklassen in `src/Softwareschmiede/Application/Services/`, `plugins/Softwareschmiede.Plugin.GitHub/` und `src/Softwareschmiede.App/`.

## `ScmEventMonitoringService`
Datei: `src/Softwareschmiede/Application/Services/ScmEventMonitoringService.cs`

Erbt von `PeriodicBackgroundService` (Poll-Intervall `ScmEventOptions.PollInterval`). Einziger Akquisitionspfad heute: lokale Referenzverknüpfungen + repository-weite Enumeration über `IScmEventSource`; anschließend Dispatch-Schleife über die Inbox. **Kein Notifications-/Kontomodus-Pfad vorhanden.**

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `RunOnceAsync` | `public override` (Z. 21–180) | Ein Poll-Durchlauf: Startup-Gate → `GetScmMonitoringEnabledAsync` (Abbruch wenn deaktiviert, Inbox/Checkpoints bleiben) → `GetScmTriggerKeywordAsync` → verknüpfte Referenzen aus `Aufgaben` (IssueReferenz/PullRequests) sammeln → pro SCM-Plugin mit `IScmEventSource`: Alias-Auflösung via `GetEventRepositoryAsync` (Checkpoint `repository/{prefix}/{alias}`) → `EnumerateRepositoryAsync` je aufgelöstem Repository → je (Provider/Host/RepoId/Kind/Number) × `ScmContentKind` `ReadEventsAsync` mit Checkpoint `{provider}/{host}/{repoId}/{kind}/{number}/{contentKind}` → bei unzugeordneter Referenz mit Trigger-Treffer `ScmEventTaskResolver.ResolveAsync` → `ScmEventStore.AcceptBatchAsync` → Dispatch-Schleife über `ScmIncomingEvents` (Sequenz-Chunks à 100) → `ScmEventOrchestrator.DispatchAsync` |
| `MatchesPlugin` | `private static` (Z. 182–184) | Plugin-Zuordnung über `PluginPrefix`/`PluginName` ↔ `GitRepository.PluginTyp` |
| `EnumerateRepositoryAsync` | `private` (Z. 188–235) | Pro Repository einmal je Lauf `EnumerateReferencesAsync` mit eigenem Watermark-/Backoff-Checkpoint `enumeration/{provider}/{host}/{repoId}`; danach `ScmBackmergeService.EvaluateDiscoveredReferencesAsync` (Fehler isoliert) |
| `RecordFailureAsync` | `private` (Z. 237–255) | Checkpoint-Fehlerbuchung: `Failures++`, `ErrorCode` aus `ScmProviderFailureKind`, `NextAttemptUtc` = `long.MaxValue` bei Authentication/Permission/Permanent sonst `ScmEventStore.RetryAt`; danach `ScmEventChangeNotifier.Notify()` |

Abonnierte Events: keine. Publizierte Events: indirekt `ScmEventChangeNotifier.Changed` (via `Notify()`).
Querverweise: liest `AppEinstellungService` (Schalter + Keyword je Zyklus, wirkt ohne Neustart); ruft `IScmEventSource` (Plugin), `ScmEventTaskResolver`, `ScmEventStore`, `ScmBackmergeService`, `ScmEventOrchestrator`.

## `ScmEventStore`
Datei: `src/Softwareschmiede/Application/Services/ScmEventStore.cs`

Atomare Inboxaufnahme, Wiederholungsplanung und Aufgabenleases.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `AcceptBatchAsync` | `public` (Z. 38–95) | Transaktional: Checkpoint-Watermark setzen, `ScmContentSnapshot` pflegen, `ScmEventValidationService.GetRejection` je Observation (bei `aufgabeId == null` stattdessen `Unassigned`/`AmbiguousAssignment`-Diagnose), dedupliziert `ScmIncomingEvent` anlegen, `Notify()` bei sichtbarer Änderung |
| `ClaimTaskAsync` | `public` (Z. 98–104) | Atomare 60-s-Aufgabenlease (`INSERT ... ON CONFLICT`) |
| `RenewTaskAsync` / `ReleaseTaskAsync` | `public` (Z. 106–111) | Lease verlängern/freigeben (nur eigener Owner) |
| `Observation` | `public static` (Z. 21–22) | Deserialisiert `ObservationJson` |
| `ActionId` | `public static` (Z. 24–25) | Deterministische Aktions-ID (SHA-256) |
| `RetryAt` | `public static` (Z. 27–28) | Backoff: 30 s/2 min/10 min/30 min + Jitter, optional `RetryAfterUtc` |
| Konstanten | `public const` | `AmbiguousAssignmentDiagnostic`, `UnassignedDiagnostic` (Z. 15–17); `JsonOptions` (Z. 19) |

## `ScmEventValidationService`
Datei: `src/Softwareschmiede/Application/Services/ScmEventValidationService.cs`

Statische Validierung der Provider-Identität je beobachteter Textversion — heute hart verdrahtet auf „authentifizierter Nutzer = Repository-Owner" und Trigger-Keyword-Pflicht.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetRejection` | `public static` (Z. 24–41) | Ablehnungsgrund oder `null`: „Kein Trigger" (Keyword nicht frei stehend), „Angemeldeter Benutzer ist nicht Repository-Owner" (`OwnerId`/`Owner` vs. `ViewerId`/`ViewerLogin`), „Bearbeiter nicht bestimmbar", „Textversion stammt nicht vom Repository-Owner" (Autor bzw. Editor = Viewer), „Eigene Antwort" (`<!-- softwareschmiede:`-Marker) |
| `NormalizeTriggerKeyword` | `public static` (Z. 17–18) | Leere Werte → `DefaultTriggerKeyword` (`$Softwareschmiede`, Z. 10), sonst Trim |
| `HasFreeStandingTrigger` | `private static` (Z. 44–56) | Case-insensitive Suche; von QuoteChars (`'"`´‚‘’„“”«»`) flankierte Vorkommen zählen nicht |
| `SameIdentity` | `private static` (Z. 58–62) | ID-Vergleich (ordinal) hat Vorrang vor Login-Vergleich (ordinalIgnoreCase) |

Querverweise: aufgerufen von `ScmEventMonitoringService` (Trigger-Suche für Task-Resolution, Z. 142), `ScmEventStore.AcceptBatchAsync` (Z. 59), `ScmEventOrchestrator` (Revalidierung, Z. 136–140), `AppEinstellungService.GetScmTriggerKeywordAsync`.

## `ScmEventOrchestrator`
Datei: `src/Softwareschmiede/Application/Services/ScmEventOrchestrator.cs`

Gemeinsamer Dispatch-Pfad für Polling und Queue-Bedienung; modusunabhängig wiederverwendbar.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `DispatchAsync` | `public` (Z. 23–28) | Einstieg je Ereignis; serialisiert über `ScmTaskCoordinator.Shared` |
| `OpenFromQueueAsync` | `public` (Z. 31–72) | Queue-Öffnen unter Aufgabenlease mit Plugin-Auswahl-Callback |
| `DispatchCoreAsync` | `private` (Z. 74–250) | Kern-Dispatch: Abbruch-Check → Plugin-Auflösung (`IScmEventSource` + `IScmEventResponder`) → Empfangsreaktion (`eyes`) und ausstehende `waiting`-Aktionen via `ScmEventOutbox` → Reconcile laufender Versuche → Serialitätsregel (ältere Ereignisse zuerst) → Repository-/CanWrite-Revalidierung via `GetEventRepositoryAsync` → `GetRejection`-Revalidierung (außer „Kein Trigger") → Aufgabe starten → CLI-Sitzung sicherstellen (`EntwicklungsprozessService`/`KiAusfuehrungsService`) → `IScmPromptSessionAdapter`-Bereitschaft/Exchange-Probe → `ScmPromptAppendix.Resolve` → `ScmEventExchangeProtocol.PrepareAsync` → Prompt schreiben → Statuspflege; typisierte Fehler → Backoff/`WaitAsync` |
| `ActivateForOpenAsync` | `public` (Z. 253–259) | Öffnen aktiviert nur neue, unpausierte Aufgaben |
| `RetryAsync` | `public` (Z. 262–272) | Hebt `NextAttemptUtc`-Sperren von Ereignis, Outbox-Aktionen und allen Checkpoints auf |
| `ConfirmReadyAsync` | `public` (Z. 275 ff.) | Bestätigung gilt nur für die angezeigte lebende Sitzung |

## `ScmEventOutbox`
Datei: `src/Softwareschmiede/Application/Services/ScmEventOutbox.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `EnsureAsync` | `public` (Z. 14) | Legt `ScmOutgoingAction` mit deterministischer ID an bzw. liefert bestehende |
| `ExecuteAsync` | `public` (Z. 33) | Führt `IScmEventResponder.ExecuteResponseAsync` aus mit Retry-/Statuspflege |

## `ScmEventTaskResolver`
Datei: `src/Softwareschmiede/Application/Services/ScmEventTaskResolver.cs`

Löst SCM-Referenz zu genau einer Aufgabe auf (Wiederverwendung/Anlage/Diagnose) — läuft vor der Inbox-Aufnahme.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ResolveAsync` | `public` (Z. 46–50) | Dispatch nach `ScmReferenceKind` |
| `ResolveIssueAsync` | `private` (Z. 52–74) | `ReadIssueAsync` → vorhandene Aufgabe suchen → sonst `AufgabeService.CreateFromIssueEventAsync` mit `ComposeBody` |
| `ResolvePullRequestAsync` | `private` (Z. 76–106) | `ReadPullRequestAsync` → `PullRequestReferenzen`-Match → sonst `CreateFromPullRequestAsync` (Anlagerennen abgefangen) |
| `ComposeBody` | `private static` (Z. 120–129) | Auftragstext = Referenzbeschreibung + auslösende Observation + URL |
| `IssueSourceKey` | `private static` (Z. 132–136) | Kanonischer Dedup-Schlüssel der Issue-Referenz |

Zugehörige Typen: `enum ScmTaskResolutionKind` (`Assigned`/`Ambiguous`/`Unassigned`, Z. 13–21), `record ScmTaskResolution` (Z. 24–35).

## `ScmBackmergeService`
Datei: `src/Softwareschmiede/Application/Services/ScmBackmergeService.cs`

Vom Enumerationspfad aus aufgerufen (`EvaluateDiscoveredReferencesAsync`, Monitoring Z. 210): bewertet gefundene PR-Referenzen auf Backmerge-Muster und mergt modusabhängig (`ScmBackmergeModus` via `AppEinstellungService.GetScmBackmergeModusAsync`, projektbezogene Freigabe via `GetBackmergeProjektAktivAsync`).

## `ScmTaskCoordinator`
Datei: `src/Softwareschmiede/Application/Services/ScmTaskCoordinator.cs`

Prozessweite Serialisierung (`Shared`, `RunAsync` pro `taskId` via `SemaphoreSlim` + `AsyncLocal`-Reentrancy) für SCM-Dispatch, manuelle Starts und Terminaleingaben.

## `ScmEventExchangeProtocol` / `ScmEventExchangeService`
Dateien: `src/Softwareschmiede/Application/Services/ScmEventExchangeProtocol.cs`, `ScmEventExchangeService.cs`

Dateibasierter Antwortkanal zur CLI-Sitzung: `PrepareAccessProbeAsync` (Z. 96), `ReadAccessProbeAsync` (Z. 116), `BuildAccessProbePrompt` (Z. 138), `PrepareAsync` (Z. 171), `ReadAsync` (Z. 229), `WriteResultAsync` (Z. 274), `BuildPrompt` (Z. 296), `EnsureSafePath` (Z. 391). Der Orchestrator schreibt `observation.Body` in den Prompt — der Texteingang bleibt im neuen Modus unverändert.

## `AppEinstellungService`
Datei: `src/Softwareschmiede/Application/Services/AppEinstellungService.cs`

Generischer Key-Value-Einstellungsservice; SCM-Keys und das `ScmBackmergeModus`-Muster für den neuen Kontomodus-Schlüssel.

| Member | Sichtbarkeit | Kurzbeschreibung |
|--------|-------------|------------------|
| `ScmMonitoringEnabledKey = "scm.monitoring.enabled"` | `public const` (Z. 43) | Laufzeit-Schalter Überwachung |
| `ScmTriggerKeywordKey = "scm.trigger.keyword"` | `public const` (Z. 46) | Trigger-Keyword |
| `ScmPromptAppend*Key` (5 Schlüssel) | `public const` (Z. 49–61) | Prompt-Anhänge je Ereignisart |
| `ScmTaskCompletionEnabledKey` / `ScmTaskCompletionWaitMinutesKey` / `ScmTaskCompletionDefaultWaitMinutes` | `public const` (Z. 64–70) | Abschlussüberwachung |
| `ScmBackmergeModeKey = "scm.backmerge.mode"` | `public const` (Z. 88) | Enum-Einstellung als Zahl — Vorlage für `scm.monitoring.accountmode` |
| `BackmergeProjektKeyPrefix` / `BackmergeProjektKey` | `public const` / `public static` (Z. 91, 178) | Projektbezogene Freigabe-Keys |
| `GetSettingAsync` / `GetIntSettingAsync` / `GetBoolSettingAsync` | `public` (Z. 106–131) | Generische Leser (`null` bei fehlendem/ungültigem Wert) |
| `SetSettingAsync` / `SetIntSettingAsync` / `SetBoolSettingAsync` | `public` (Z. 134–162) | Generische Schreiber |
| `GetScmMonitoringEnabledAsync` | `public` (Z. 172–173) | Default `true` ohne Eintrag |
| `GetScmBackmergeModusAsync` | `public` (Z. 187–193) | Ungültig/fehlend → `Deaktiviert` — Muster für Modus-Getter |
| `SetScmBackmergeModusAsync` | `public` (Z. 199–205) | `Enum.IsDefined`-Validierung, `ArgumentOutOfRangeException` — Muster für Modus-Setter |
| `GetBackmergeProjektAktivAsync` / `SetBackmergeProjektAktivAsync` | `public` (Z. 211–219) | Projektflag, Default `false` |
| `GetScmTaskCompletionEnabledAsync` / `GetScmTaskCompletionWaitMinutesAsync` | `public` (Z. 229–244) | Defaults `true` / 60 min, Clamp 0–43200 |
| `GetScmTriggerKeywordAsync` | `public` (Z. 254–255) | Normalisiertes Keyword |
| `GetScmPromptAppendicesAsync` / `GetSettingsAsync` | `public` (Z. 266–282) | Gebündelte Mehrfach-Lesung |
| `GetWindowGeometryAsync` / `SetWindowGeometryAsync` / `GetUpdateSettingsAsync` / `SetUpdateSettingsAsync` | `public` (Z. 285 ff.) | Nicht SCM-relevant |

**Nicht vorhanden:** Kein Schlüssel `scm.monitoring.accountmode`, kein `ScmKontoModus`-Enum, keine `IScmNotificationSource`-Fähigkeit.

## `GitHubPlugin` (partial)
Dateien: `plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.cs`, `GitHubPlugin.Events.cs`, `GitHubPlugin.EventResponses.cs`

Einziger SCM-Provider mit Ereignisfähigkeit: `GitPluginBase<GitHubPlugin>, IScmEventSource, IScmEventResponder, IScmAgentAccess` (Z. 15). BitBucket- und LocalDirectory-Plugin implementieren keine der drei Interfaces (einzige Treffer im Repo).

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GitHubTokenCredentialKey = "Softwareschmiede.GitHub.Token"` | `private const` (Z. 17) | Einziger Credential-Key heute — Feld `Token` in `GetSettingGroups` |
| `GetSettingGroups` | `public override` (Z. 38–79) | Gruppen „Authentifizierung" (Feld `Token`, `PluginSettingFieldType.Secret`, `IsRequired`) und „Pull Requests" (AutoComplete/Strategie/Merge-Methode/Bypass) |
| `GetRepositoryLinkFields` | `public override` (Z. 82–98) | `RepositoryUrl`, `RepositoryName` (beide required) |
| Konstruktoren | `public` (Z. 101–115) | Optional injizierbarer `IGitHubEventApiClient` (Testtransport); Default `GhEventApiClient` mit `GetGhEnvironment` |
| `GetGhEnvironment` | `private` (Z. 117–126) | `GH_TOKEN` aus `ICredentialStore` für alle Plugin-eigenen `gh`-Aufrufe |
| `GetAgentEnvironmentAsync` | `public` (Z. 138–158, `IScmAgentAccess`) | `GIT_TERMINAL_PROMPT=0` immer; bei Token zusätzlich `GH_TOKEN` + `GIT_CONFIG_*` für `http.https://github.com/.extraheader` (Basic `x-access-token`) |
| `GetGitEnvironment` | `private` (Z. 160–183) | git-Umgebung der Host-Operationen (Clone/Push/Pull) |
| `SanitizeSensitiveOutput` | `private static` (Z. 202–219) | Token und eingebettete `oauth2:`-Credentials in Fehlertexten maskieren |
| `GetEventRepositoryAsync` | `public` (Events Z. 15–31, `IScmEventSource`) | `GET user` + `GET repos/{owner}/{name}` → kanonischer `ScmEventRepository` inkl. `ViewerId`/`ViewerLogin`/`CanWrite` |
| `ReadEventsAsync` | `public` (Events Z. 34–94) | Je `ScmContentKind`: Description/Reviews via GraphQL (`TextFields`), Kommentare via REST-Pagination + GraphQL-Nodesnapshot; Watermark-Filter; Sortierung nach `Revision`,`EventKey` |
| `EnumerateReferencesAsync` | `public` (Events Z. 97–114) | Offene Issues+PRs immer; geschlossene/merged nur mit `sinceUtc` (`CollectReferencesAsync`, Z. 152–180) |
| `ReadIssueAsync` / `ReadPullRequestAsync` | `public` (Events Z. 117–149) | Anlagedaten für `ScmEventTaskResolver` |
| `ReconcileResponseAsync` / `ExecuteResponseAsync` | `public` (EventResponses Z. 10–75, `IScmEventResponder`) | Abgeglichene Antworten (Kommentar/Reaktion/PR); Revalidierung `RepositoryId` + `ViewerId` + `CanWrite` vor POST; Marker-basierte Dedup |
| `SendEventApiAsync` / `EventApiAsync` / `GraphAsync` / `RestPagesAsync` | `private` (Events Z. 210–281) | Transport-Fehlerklassifizierung in `ScmProviderException` (RateLimit/Auth/Permission/Temporary/Permanent, `Retry-After`, `OutcomeUncertain` bei Mutationen) |
| `ParseEventRepository` / `ValidateReference` / `IsViewer` / `RepositoryEndpoint` | `private` (Events Z. 304–352) | URL-/Referenz-Validierung; `IsViewer` gleicht Autor mit Viewer-Identität ab |

**Nicht vorhanden:** kein Zugriff auf `GET /notifications`, kein `reason`-Filter, keine Inbox-Auflösung Thread→Referenz, kein zweiter Credential-Key.

## `GhEventApiClient` / `IGitHubEventApiClient`
Datei: `plugins/Softwareschmiede.Plugin.GitHub/IGitHubEventApiClient.cs`

| Member | Sichtbarkeit | Kurzbeschreibung |
|--------|-------------|------------------|
| `IGitHubEventApiClient.SendAsync` | `public` (Z. 14) | Einzelner API-Aufruf (`GitHubEventApiRequest` → `GitHubEventApiResponse` inkl. Header) |
| `GhEventApiClient.SendAsync` | `public` (Z. 28–86) | Produktivtransport über `gh api --hostname … --method … --include`; JSON-Body via Temp-Datei (`--input`), nie als Argument; `GH_ENTERPRISE_TOKEN`-Spiegelung für Nicht-github.com-Hosts; Header-Parsing aus `--include`-Ausgabe; keine Credentials in Fehlertexten |

## `KiAusfuehrungsService`
Datei: `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `WithScmAgentEnvironmentAsync` | `private` (Z. 367–397) | Ergänzt beim CLI-Start `IScmAgentAccess.GetAgentEnvironmentAsync` des Repository-SCM-Plugins in `TerminalSessionStartSpec.EnvironmentVariables`; Fehler/leere Anbindung blockieren den Start nicht; Werte werden nie protokolliert und sind kein Bestandteil des `ScmCliAccessEvaluator`-Fingerprints |
| `GetScmPromptSession` | `public` (Z. 410–411) | Liefert den an den Start gebundenen `IScmPromptSessionAdapter` |
| `GetTerminalSession` | `public` (Z. 402–407) | Rohsitzung einer Aufgabe |

## `PluginSettingsService`
Datei: `src/Softwareschmiede/Application/Services/PluginSettingsService.cs`

`GetValue`/`SetValue`/`DeleteValue`/`HasValue` für `PluginSettingField`; `BuildKey` = `<PluginPrefix>.<FieldKey>` im `ICredentialStore` (Z. 65–66). Ein zusätzliches Secret-Feld in `GitHubPlugin.GetSettingGroups()` landet damit automatisch als eigener Credential-Eintrag.

## `PeriodicBackgroundService`
Datei: `src/Softwareschmiede/Application/Services/PeriodicBackgroundService.cs`

Basisklasse: `ExecuteAsync`-Schleife ruft `RunOnceAsync`, fängt Fehler je Durchlauf ab, wartet `Task.Delay(PollInterval, TimeProvider)`.

## `SettingsViewModel` / `SettingsView`
Dateien: `src/Softwareschmiede.App/ViewModels/SettingsViewModel.cs`, `src/Softwareschmiede.App/Views/SettingsView.xaml`

| Member | Sichtbarkeit | Kurzbeschreibung |
|--------|-------------|------------------|
| `IsScmMonitoringEnabled` / `IsScmTaskCompletionEnabled` / `ScmTaskCompletionWaitMinutes` / `ScmTriggerKeyword` / `ScmPromptAppendix*` | `public` Properties (Z. 222–288) | Bindings des Abschnitts „SCM-Ereignisse" |
| `BackmergeModusOptionen` / `SelectedBackmergeModus` | `public` (Z. 82–89) | ComboBox-Binding über `ScmBackmergeModusOption.Alle` — Muster für Optionsliste des Kontomodus |
| `LadenAsync` | `private` (u. a. Z. 426–445) | Lädt alle SCM-Einstellungen über `AppEinstellungService` |
| `SpeichernAsync` | `private` (Z. 462–559) | Persistiert alle SCM-Einstellungen; bricht bei `SelectedBackmergeModus == null` mit Fehlermeldung ab (Z. 478–482) — Muster für Pflichtauswahl |
| `ValidierePflichtfelder` | `private` (Z. 725–732) | Verkettet Plugin-Aktivierung, Plugin-Felder (`IsRequired`, Integer/Enum-Prüfung, Z. 785 ff.), Promptvorlagen, `ValidiereScmTriggerKeyword` (Z. 751), `ValidiereScmTaskCompletionWait` |
| `SpeicherePluginEinstellungen` | `private` (Z. 711) | Schreibt Plugin-Setting-Werte über `PluginSettingsService` |

View: Abschnitt „SCM-Ereignisse" in `SettingsView.xaml` Z. 278–345 (CheckBox `IsScmMonitoringEnabled`, `IsScmTaskCompletionEnabled`, TextBox `ScmTaskCompletionWaitMinutes`, `ScmTriggerKeyword`, ComboBox `Backmerge-Modus`, Prompt-Anhänge) — hier würde die Kontomodus-Auswahl ergänzt.

## `ScmBackmergeModusOptionen`
Datei: `src/Softwareschmiede.App/ViewModels/ScmBackmergeModusOptionen.cs`

`ScmBackmergeModusTexte` (Label-Konstanten) + `ScmBackmergeModusOption(Modus, Label)` mit `Alle`/`FuerModus`/`ToString` — direkte Vorlage für eine Kontomodus-Auswahloption.

## Dokumentationsbestand
`docs/help/plugins/github-ereignisse/` (`architektur.md`, `ablauf-technisch.md`, `business-rules.md`) beschreibt den aktuellen Ereignisfluss inkl. `IScmAgentAccess`-Verhalten (GH_TOKEN-Übergabe an die Agenten-Sitzung) — bei Umsetzung mitzupflegen.
