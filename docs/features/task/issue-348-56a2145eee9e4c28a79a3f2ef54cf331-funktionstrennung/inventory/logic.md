# Logik — Bestandsaufnahme

## `ScmEventMonitoringService`
Datei: `src/Softwareschmiede/Application/Services/ScmEventMonitoringService.cs`

`sealed`, erbt `PeriodicBackgroundService` (Intervall = `ScmEventOptions.PollInterval`, Fehlermeldung „SCM-Ereignisüberwachung fehlgeschlagen."). DI: `AddSingleton` + `AddHostedService` in `App.xaml.cs` (Zeilen 228–229). = Funktion „Aufgaben ausführen".

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `RunOnceAsync(ct)` | `public override` | Ein Polling-Durchlauf: `startup.WaitAsync` → `GetScmMonitoringEnabledAsync` (bei `false` kompletter Abbruch, Zeilen 38–42) → Trigger-Keyword + `GetScmKontoModusAsync` lesen → Aufgaben/Repositories/Plugins laden → je Plugin: Alias-Auflösung aller Repositories (`GetEventRepositoryAsync` mit dem **konfigurierten** `kontoModus`, Zeile 96) → bei `SeparatesKonto` + `IScmNotificationSource`: `EnumerateNotificationsAsync` statt Enumeration (Zeilen 127–139, `resolved.Clear()` — verknüpfte Referenzen verworfen) → sonst `EnumerateRepositoryAsync` je Repository (Zeilen 140–152) → Quellen lesen/validieren/`ScmEventStore.AcceptBatchAsync` → Inbox-Watermark-Commits nur bei fehlerfreiem Zyklus → Dispatch-Runde über `ScmEventOrchestrator.DispatchAsync`. |
| `EnumerateRepositoryAsync(...)` | `private` | Enumeriert ein Repository einmal pro Lauf (Dedup über `enumerated`-HashSet + Checkpoint `enumeration/...` mit Backoff): `EnumerateReferencesAsync`, dann **`ScmBackmergeService.EvaluateDiscoveredReferencesAsync` (einziger Aufruf im System, Zeile 272)** — Fehler werden nur geloggt und berühren den Enumerations-Checkpoint nicht (Zeilen 268–278). |
| `EnumerateNotificationsAsync(...)` | `private` | Liest die Notifications-Inbox eines Hosts (`notifications/{plugin}/{host}`-Checkpoint), leitet Mentions auf registrierte Repositories ab, verwirft Fremde mit Diagnose `InboxDiscardDiagnosticPrefix`; Watermark-Commit erfolgt erst am Zyklusende. |
| `RecordFailureAsync(...)` | `private` | Persistiert Fehlerdiagnose + Backoff-`NextAttemptUtc` im Checkpoint; `blockedForever` bei `Authentication`/`Permission`/nicht-retrybarem `Permanent`; setzt `cycle.HadFailure` (blockiert Inbox-Commit), wenn `blocksCycle`. |
| `MatchesPlugin`, `IsInaccessible` | `private static` | Plugin↔Repository-Matching (Prefix oder Name); `IsInaccessible` kennzeichnet 404/403-ähnliche Fehler des separaten Kontos. |

Abonnierte Events: keine. Publizierte Events: ruft `ScmEventChangeNotifier.Notify()` nach Checkpoint-/Inbox-Änderungen.

## `ScmTaskCompletionMonitoringService`
Datei: `src/Softwareschmiede/Application/Services/ScmTaskCompletionMonitoringService.cs`

`sealed`, erbt `PeriodicBackgroundService` (Intervall = `ScmEventOptions.PollInterval`). DI: `AddSingleton` + `AddHostedService` (App.xaml.cs Zeilen 297–298). = Funktion „Aufgaben beenden".

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `RunOnceAsync(ct)` | `public override` | `startup.WaitAsync` → `GetScmTaskCompletionEnabledAsync` (bei `false` Abbruch, persistierter `ScmAbschlussErkanntUtc` bleibt) → Wartezeit `GetScmTaskCompletionWaitMinutesAsync` → **Kandidaten = alle Aufgaben mit Status ≠ Beendet/Archiviert UND mindestens einem `ScmIncomingEvent`** (Zeilen 46–50) → `CheckTaskAsync` je Kandidat (Fehler je Aufgabe isoliert). |
| `CheckTaskAsync(taskId, wait, ct)` | `private` | Laufende Ereignisverarbeitung blockiert (Status ≠ `Verarbeitet`/`Abgebrochen`); lädt Aufgabe inkl. `GitRepository`, `IssueReferenz`, `PullRequests`; `DetermineClosureAsync` → offen: Countdown zurücksetzen (`ScmAbschlussErkanntUtc = null` + Protokoll) → geschlossen: Erstkennung setzen → nach Wartezeit unter `ScmTaskCoordinator.Shared`-Sperre `EntwicklungsprozessService.AbschliessenAsync` (beendet ggf. laufende KI-Ausführung); erwartbare Abschlussfehler werden geloggt und im nächsten Lauf erneut versucht. |
| `DetermineClosureAsync(services, db, task, ct)` | `private` | Schließkriterium: **PR-Verknüpfungen haben Vorrang** — alle müssen `Closed`/`Merged` sein (`Merged` lokal als final gewertet; Status via `IGitPlugin.GetPullRequestStatusAsync`, nur wenn `PullRequestMonitoringPolicy.CanMonitor`); ohne PRs gilt das verknüpfte Issue (`IssueReferenz.IssueNummer > 0` + `GitRepository`): `IScmEventSource.GetEventRepositoryAsync` mit `GetScmKontoModusAsync` (konfigurierter Modus!) + `ReadIssueAsync`, `IstGeschlossen`. Liefert Anzeigelabel oder `null`. |
| `ResolvePlugin`, `MatchesPlugin` | `private static` | Plugin-Auflösung über `PullRequestProviderDescriptor.GetPluginPrefix` bzw. `PluginTyp`-Vergleich. |

## `ScmBackmergeService`
Datei: `src/Softwareschmiede/Application/Services/ScmBackmergeService.cs`

`sealed`, DI: `AddScoped` (App.xaml.cs Zeile 227). = Funktion „Prozessabläufe". **Besitzt heute keinen eigenen zyklischen Aufrufer** — wird ausschließlich aus `ScmEventMonitoringService.EnumerateRepositoryAsync` aufgerufen und erbt damit dessen Voraussetzungen (Monitoring-Schalter aktiv, `kontoModus`-Auflösung; im `SeparatesKonto`-Pfad findet keine Enumeration statt → keine Backmerge-Entdeckung).

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `EvaluateDiscoveredReferencesAsync(plugin, repository, discovered, lokaleRepositories, ct)` | `public` | `GetScmBackmergeModusAsync` (Deaktiviert → sofort raus) → `repository.CanWrite` → nur `PullRequest`-Referenzen → Kandidaten-Filter (`FilterAktivierteRepositoriesAsync`) → je Referenz eigener Checkpoint `backmerge/...` mit `NextAttemptUtc`-Sperre → `BewertePullRequestAsync`, Fehler → Backoff/`long.MaxValue`. |
| `BewertePullRequestAsync(...)` | `private` | `GetPullRequestStatusAsync` → nicht `Open`: `Entscheide` (final). Hauptbranch je Kandidat via `GetDefaultBranchAsync` (Cache `hauptbranches`); Match: `status.SourceBranch == Hauptbranch && status.TargetBranch == DefaultSourceBranchName`. Kein Treffer: `Entscheide` („Kein Backmerge") bzw. Retry bei unbekanntem Hauptbranch. Nicht `Mergeable`: `WaitingForMergeable` + Retry im Poll-Intervall. Sonst `CompletePullRequestAsync` (Admin-Merge, `AllowProtectedBranchBypass`) → `Merged`/`Decided` oder `Blocked`/`Temporary`-Backoff. |
| `FilterAktivierteRepositoriesAsync(kandidaten, modus, ct)` | `private` | `Aktiv && DefaultSourceBranchName`-Filter; `Global` → alle; sonst Flags `plugins.backmerge.project.{projektId}` gebündelt via `GetSettingsAsync`. |
| `Entscheide`, `NextRetryUtc`, `CheckpointKey` | `private`/`public static` | Checkpoint final setzen (`NextAttemptUtc = long.MaxValue`); Retry = `now + PollInterval`; Checkpoint-Schlüssel `backmerge/{provider}/{host}/{repoId}/{nummer}`. |

## `AppEinstellungService`
Datei: `src/Softwareschmiede/Application/Services/AppEinstellungService.cs`

`sealed`, DI: `AddScoped`. Key-Value-Persistenz auf `db.AppEinstellungen`. SCM-Automatisierungs-Schlüssel und Getter/Setter:

| Mitglied | Sichtbarkeit | Kurzbeschreibung |
|----------|-------------|------------------|
| `ScmMonitoringEnabledKey = "scm.monitoring.enabled"` | `public const` | Laufzeit-Schalter „Aufgaben automatisch starten" (heutiges Label: „SCM-Ereignisüberwachung aktivieren"). |
| `GetScmMonitoringEnabledAsync(ct)` | `public` | `GetBoolSettingAsync ?? true` — Default **aktiviert**. |
| `ScmKontoModusKey = "scm.monitoring.accountmode"` | `public const` | Überwachungsmodus (heutiges Label: „Konto-Modus"). |
| `GetScmKontoModusAsync(ct)` / `SetScmKontoModusAsync(modus, ct)` | `public` | `Enum.IsDefined`-Validierung; Fallback `Benutzerkonto`; Setter wirft `ArgumentOutOfRangeException`. **Vorbild für den neuen Prüfmodus-Schlüssel.** |
| `ScmTaskCompletionEnabledKey = "scm.taskcompletion.enabled"` | `public const` | Laufzeit-Schalter „Aufgaben automatisch beenden". |
| `GetScmTaskCompletionEnabledAsync(ct)` | `public` | `GetBoolSettingAsync ?? true` — Default **aktiviert**. |
| `ScmTaskCompletionWaitMinutesKey = "scm.taskcompletion.wait.minutes"`, `ScmTaskCompletionDefaultWaitMinutes = 60` | `public const` | Wartezeit „Wartezeit für Beendigung". |
| `GetScmTaskCompletionWaitMinutesAsync(ct)` | `public` | Fallback 60; `Math.Clamp(0, 43200)`. |
| `ScmBackmergeModeKey = "scm.backmerge.mode"` | `public const` | Backmerge-Modus. |
| `GetScmBackmergeModusAsync(ct)` / `SetScmBackmergeModusAsync(modus, ct)` | `public` | `Enum.IsDefined`-Validierung; Fallback `Deaktiviert`. **Vorbild für den neuen Prüfmodus-Schlüssel.** |
| `BackmergeProjektKeyPrefix = "plugins.backmerge.project."`, `BackmergeProjektKey(projektId)`, `GetBackmergeProjektAktivAsync`, `SetBackmergeProjektAktivAsync` | `public` | Projektbezogene Backmerge-Freigabe (Scope-Ausschluss: bleibt unverändert). |
| `ScmTriggerKeywordKey = "scm.trigger.keyword"` + `GetScmTriggerKeywordAsync` | `public` | Getrimmt; Fallback `ScmEventValidationService.DefaultTriggerKeyword` (`$Softwareschmiede`). |
| `ScmPromptAppendGeneralKey` (`scm.prompt.append.general`), `...IssueKey`, `...IssueCommentKey`, `...PullRequestKey`, `...PullRequestCommentKey` + `GetScmPromptAppendicesAsync` | `public` | Fünf Prompt-Anhänge, gebündelt in einer Abfrage (`ScmPromptAppendix.SettingKeys`). |
| `GetSettingAsync` / `GetIntSettingAsync` / `GetBoolSettingAsync` / `GetSettingsAsync` / `SetSettingAsync` / `SetIntSettingAsync` / `SetBoolSettingAsync` | `public` | Generische Key-Value-Primitives (string/int/bool, gebündeltes Lesen). |

**Es existiert noch kein Schlüssel/Getter für einen Abschluss-Prüfmodus** (Anforderung: neu, z. B. `scm.taskcompletion.scope`).

## `PeriodicBackgroundService`
Datei: `src/Softwareschmiede/Application/Services/PeriodicBackgroundService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ExecuteAsync(stoppingToken)` | `protected override` | Endlosschleife: `RunOnceAsync` (Fehler je Durchlauf geloggt) → `Task.Delay(_pollingInterval, _timeProvider)`. Basisklasse beider Monitoring-Dienste; ein dritter Dienst (Backmerge) könnte dasselbe Muster nutzen. |
| `RunOnceAsync(ct)` | `public abstract` | Ein Durchlauf. |

## `ScmEventOptions` / `ScmEventChangeNotifier` / `ScmEventStartupGate`
Datei: `src/Softwareschmiede/Application/Services/ScmEventOptions.cs`

- `ScmEventOptions`: `PollInterval` (Default 60 s; E2E-Modus 300 ms via `services.Configure` in App.xaml.cs Zeile 374), `AcceptanceTimeout`, `CompletionTimeout`.
- `ScmEventChangeNotifier`: `event Action Changed` + `Notify()`; `ScmEventQueueViewModel` abonniert `Changed`.
- `ScmEventStartupGate`: `Ready()`/`WaitAsync(ct)` — hält Hintergrunddienste bis nach `MigrateAsync` an (`App.xaml.cs` Zeile 114).

## `ScmEventValidationService`
Datei: `src/Softwareschmiede/Application/Services/ScmEventValidationService.cs`

| Mitglied | Sichtbarkeit | Kurzbeschreibung |
|----------|-------------|------------------|
| `DefaultTriggerKeyword = "$Softwareschmiede"` | `public const` | Standard-Trigger. |
| `KeineErwaehnungRejection` | `public const` | Ablehnungsgrund im `SeparatesKonto` (frei stehende `@`-Erwähnung fehlt). |
| `NormalizeTriggerKeyword` / `GetRejection` | `public static` | Normalisierung + Trigger-/Bearbeiter-Validierung einer Observation (Keyword-Modus vs. `@ViewerLogin`-Modus). Prüflogik bleibt laut Anforderung unverändert. |

## `ScmTaskCoordinator`
Datei: `src/Softwareschmiede/Application/Services/ScmTaskCoordinator.cs`

`Shared`-Singleton (DI in App.xaml.cs Zeile 219); `RunAsync(taskId, operation, ct)` serialisiert Aufgabenoperationen pro `taskId` prozessweit (SemaphoreSlim + AsyncLocal-Reentrancy); vom Abschlussdienst für den eigentlichen `AbschliessenAsync`-Aufruf genutzt.

## `ScmPromptAppendix`
Datei: `src/Softwareschmiede/Application/Services/ScmPromptAppendix.cs`

Liefert `SettingKeys` (die fünf Anhang-Schlüssel) und das Mapping Ereignisart → Anhang-Reihenfolge für `GetScmPromptAppendicesAsync`.

## `SettingsViewModel`
Datei: `src/Softwareschmiede.App/ViewModels/SettingsViewModel.cs`

`sealed : ViewModelBase, IDisposable`; DI: `AddTransient` (App.xaml.cs Zeile 354). SCM-Automatisierungs-relevante Member:

| Member | Sichtbarkeit | Kurzbeschreibung |
|--------|-------------|------------------|
| `IsScmMonitoringEnabled` | `public` (Standard `true`) | Schalter „SCM-Ereignisüberwachung" → persistiert `ScmMonitoringEnabledKey`. |
| `IsScmTaskCompletionEnabled` | `public` (Standard `true`) | Schalter Abschlussüberwachung → `ScmTaskCompletionEnabledKey`. |
| `ScmTaskCompletionWaitMinutes` | `public string` | Wartezeit-Textfeld → `ScmTaskCompletionWaitMinutesKey` (int.Parse beim Speichern). |
| `ScmTriggerKeyword` | `public string` | → `ScmTriggerKeywordKey` (getrimmt). |
| `ScmPromptAppendixGeneral/Issue/IssueComment/PullRequest/PullRequestComment` | `public string` | Fünf Anhang-Textfelder → `scm.prompt.append.*` (`NullWennLeer`). |
| `SelectedBackmergeModus` + `BackmergeModusOptionen` | `public` | ComboBox-Bindung → `SetScmBackmergeModusAsync`. |
| `SelectedKontoModus` + `KontoModusOptionen` | `public` | ComboBox-Bindung → `SetScmKontoModusAsync`. |
| `LadenAsync` | `private` | Lädt alle SCM-Werte (Zeilen 452–468). |
| `SpeichernAsync` | `private` | `ValidierePflichtfelder` → ComboBox-Null-Prüfungen → persistiert alle SCM-Schlüssel (Zeilen 538–575). |
| `ValidiereScmKontoModus` | `private` | `SeparatesKonto` verlangt effektives `AgentToken` des GitHub-Plugins — inkl. gerade eingetippter, ungespeicherter Formularwert (Fehlermeldung verweist auf Plugins → GitHub → Authentifizierung). |
| `ValidiereScmTriggerKeyword`, `ValidiereScmTaskCompletionWait` | `private` | Nicht-leer-Keyword bzw. nicht-negative Ganzzahl. |

**Es existieren noch keine Properties für Abschluss-Prüfmodus, Sichtbarkeits-/Enabled-Flags oder Panel-Aktivierungsstatus** (Anforderung: neu).

## `ScmEventQueueViewModel`
Datei: `src/Softwareschmiede.App/ViewModels/ScmEventQueueViewModel.cs`

| Member | Sichtbarkeit | Kurzbeschreibung |
|--------|-------------|------------------|
| `RetrySourcesCommand` | `public AsyncRelayCommand` | Manueller „Quellen erneut prüfen"-Pfad: prüft `GetScmMonitoringEnabledAsync` (deaktiviert → Hinweis „Einstellungen → Allgemein → SCM-Ereignisse", **Verweis auf altes Register**, Zeile 52) → setzt alle `ScmObservationCheckpoints.NextAttemptUtc` auf `null` → ruft `ScmEventMonitoringService.RunOnceAsync` (Zeile 57) — **einziger manueller Trigger, der heute implizit auch die Backmerge-Bewertung mit auslöst.** |
| `RefreshAsync` | `public` | Lädt Inbox + Diagnosen; `MonitoringNotice` verweist ebenfalls auf „Einstellungen → Allgemein → SCM-Ereignisse" (Zeile 195). |

## `App` (DI-Registrierung)
Datei: `src/Softwareschmiede.App/App.xaml.cs`

| Zeile | Registrierung |
|-------|---------------|
| 218 | `services.Configure<ScmEventOptions>(config "ScmEvents")` |
| 219–226 | `ScmTaskCoordinator.Shared`, `ScmEventStartupGate`, `ScmEventChangeNotifier`, `ScmEventExchangeProtocol`, `ScmEventStore`, `ScmEventOutbox`, `ScmEventOrchestrator`, `ScmEventTaskResolver` |
| 227 | `services.AddScoped<ScmBackmergeService>()` |
| 228–229 | `AddSingleton<ScmEventMonitoringService>` + `AddHostedService` |
| 230–231 | `ScmEventExchangeService` + hosted |
| 297–298 | `AddSingleton<ScmTaskCompletionMonitoringService>` + `AddHostedService` |
| 373–374 | E2E-Testkonfiguration: `ScmE2ERuntimeControl` + `PollInterval = 300 ms` |
| 114 | `ScmEventStartupGate.Ready()` nach `MigrateAsync` |

**Es existiert noch kein dritter gehosteter Dienst für den unabhängigen Backmerge-Prüfzyklus** (Anforderung: ggf. neu).

## `PullRequestMonitoringPolicy`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/PullRequestMonitoringPolicy.cs`

- `CanMonitor(rolle, provider)` → nur `PullRequestProvider.GitHub`; wird in `DetermineClosureAsync` je Referenz geprüft (nicht monitorbar → kein Abschluss über PR-Pfad).
- `CanAutoComplete` / `GetInitialPhase` — vom `PullRequestMonitoringService` (Workflow-Überwachung, nicht Teil dieser Anforderung).
