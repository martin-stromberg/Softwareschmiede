# Bestandsaufnahme: SCM-Ereignisverarbeitung, Plugin-Verwaltung, Einstellungen

**Zweck:** Tatsächlicher Repository-Stand als Planungsgrundlage für die Issues **#410** (Ausführungsbegrenzung), **#411** (Deaktivierte KI-Plugins) und **#412** (Issue-Aufbereitung). Beschreibt nur den Ist-Zustand mit Belegen; explizite Lücken sind gesondert markiert. Anforderungen siehe `requirement.md` im selben Verzeichnis.

---

## 1. SCM-Ereignisverarbeitung (#410)

### 1.1 Eingang und Persistenz

- **`ScmEventMonitoringService`** (`src/Softwareschmiede/Application/Services/ScmEventMonitoringService.cs`) — Singleton-`PeriodicBackgroundService` mit `ScmEventOptions.PollInterval` (Standard 60 s, `ScmEventOptions.cs:7`). `RunOnceAsync`:
  - Laufzeit-Schalter `GetScmMonitoringEnabledAsync` — bei deaktivierter Überwachung wird weder gelesen noch zugestellt (Z. 38-42).
  - Lädt Aufgaben mit aktivem `GitRepository` inkl. `IssueReferenz`/`PullRequests` sowie alle aktiven Repositories (Z. 47-49).
  - Pro SCM-Plugin mit `IScmEventSource`: Auflösung lokaler Repository-Aliase via `GetEventRepositoryAsync` (Z. 97), danach entweder Notifications-Inbox (`IScmNotificationSource`, nur Modus `ScmKontoModus.SeparatesKonto`, Z. 130-142) oder repository-weite Enumeration via `EnumerateReferencesAsync` (Z. 143-158).
  - Pro Referenz werden Quellen gelesen: Issue → `Description` + `IssueComment`; PullRequest → zusätzlich `ReviewComment` + `PullRequestReview` (Z. 163-165) via `source.ReadEventsAsync` mit Checkpoint-Watermark (`ScmObservationCheckpoints`).
  - Unzugeordnete Quellen mit mindestens einer Trigger-Beobachtung werden via `ScmEventTaskResolver.ResolveAsync` einer Aufgabe zugeordnet bzw. eine Aufgabe angelegt (Z. 185-194).
  - `ScmEventStore.AcceptBatchAsync` (Z. 195) persistiert Batch + Watermark atomar.
- **`ScmEventStore`** (`src/Softwareschmiede/Application/Services/ScmEventStore.cs`):
  - `AcceptBatchAsync` (Z. 44-119): Dedup über `ScmContentSnapshot` (BodyHash) + Unique-Index auf `ScmIncomingEvent.EventKey`; Ablehnungsgründe via `ScmEventValidationService.GetRejection` landen als `Diagnostic` auf Snapshot/Checkpoint, **ohne** Ereignisanlage (Z. 65-81).
  - Neues Ereignis: `Status = "Aufgenommen"`, `ReceivedUtc`, `Sequence` (Entity `ScmIncomingEvent`, `src/Softwareschmiede/Domain/Entities/ScmIncomingEvent.cs`; Default-Status Z. 22).
  - Verzögerungsdirektive wird **nur bei der Aufnahme** ausgewertet (Z. 89-105): `ScmDelayDirective.Parse` → bei `Deferred` `Status = "Verzögert"`, `NextAttemptUtc = Zielzeitpunkt`, Warte-Kommentar über `EnqueueDelayComment` (deterministische Outbox-Aktion, Z. 123-135) und `PauseForDelayAsync` → `Aufgabe.PausiertBisUtc` (Z. 139-154, Max-Semantik, nur Stati Neu/Gestartet/Wartend).
  - Aufgabenleases: `ClaimTaskAsync`/`RenewTaskAsync`/`ReleaseTaskAsync` auf Tabelle `ScmTaskLeases` (Z. 157-170), 60-s-Lease mit 15-s-Renewal im Orchestrator.
- **`ScmDelayDirective`** (`src/Softwareschmiede/Application/Services/ScmDelayDirective.cs`): Default-Keyword `#verzögert` (Z. 12, Einstellung `scm.delay.keyword`), Max. 32 Tage (Z. 15), Formate `<n>min|<n>h|<n>t|d|<n>w` und `m<Tag 1-31>` (Z. 19-21, 69-101).
- **Trigger-Validierung**: `ScmEventValidationService.GetRejection` (`ScmEventValidationService.cs:39-68`) — frei stehendes Keyword `$Softwareschmiede` (Default, `scm.trigger.keyword`) bzw. freie `@<bot>`-Erwähnung im SeparatesKonto-Modus; Owner-Identität, Bearbeiter, `BereitsReagiert`, `<!-- softwareschmiede:`-Marker. **Ohne Trigger wird kein Ereignis angelegt.**

### 1.2 Status-Modell `ScmIncomingEvent.Status` (Strings, kein Enum)

`"Aufgenommen"` → `"Wartend"` → `"ZustellungVorbereitet"` → `"Zugestellt"` → `"InBearbeitung"` → `"Verarbeitet"`; daneben `"Verzögert"`, `"ZustellungUngewiss"`, `"VerarbeitungUngeklärt"`, `"Abgebrochen"`. Gesetzt in `ScmEventOrchestrator` (u. a. `WaitAsync` → `"Wartend"`, Orchestrator Z. 535-540; `"ZustellungVorbereitet"` Z. 217; `"Zugestellt"` Z. 232; `"InBearbeitung"` Z. 409; `"Verarbeitet"` Z. 441; `"Abgebrochen"` in `CancelAsync` Z. 337-342).

### 1.3 Auslöser der Ausführung (Ist-Zustand: „jedes Ereignis direkt")

- **Polling-Dispatch** (`ScmEventMonitoringService.cs:229-248`): Am Ende jedes Zyklus werden **alle** Ereignisse mit `Status != "Abgebrochen"` und (`Status != "Verarbeitet"` und `NextAttemptUtc <= jetzt`) **oder** offenen Outbox-Aktionen — geordnet nach `Sequence` (= Eingangsreihenfolge), 100 pro Durchlauf — nacheinander (`await`) an `ScmEventOrchestrator.DispatchAsync` übergeben. **Kein Limit, keine Priorisierung außer Sequence.**
- **`ScmEventExchangeService`** (`ScmEventExchangeService.cs`): 2-s-Poll, dispatcht alle Ereignisse mit `DeliveryAttemptId != null` und nicht-finalem Status (Zustellungs-/Abschlussabgleich).
- **UI**: `ScmEventQueueViewModel` (`src/Softwareschmiede.App/ViewModels/ScmEventQueueViewModel.cs`) — `OpenFromQueueAsync` (Öffnen mit CLI-Auswahl, Z. 269), `RetryAsync`, `CheckStatusCommand` → `DispatchAsync` (Z. 71), `ConfirmReadyAsync`, `ResendAsync`, `CancelAsync`; Refresh über `ScmEventChangeNotifier`.
- **Serialisierung**: `ScmTaskCoordinator.Shared` (`ScmTaskCoordinator.cs`) — prozessweite `SemaphoreSlim`-Sperre **pro Aufgabe** (AsyncLocal-Reentrancy); Dispatch, CLI-Start, Abschluss und Terminaleingaben laufen unter derselben Aufgabensperre. Verschiedene Aufgaben laufen dadurch **parallel** zueinander.

### 1.4 Dispatch-Kern (`ScmEventOrchestrator.DispatchCoreAsync`, Z. 74-264)

Reihenfolge der Prüfungen je Ereignis: Abbruch (Z. 86) → Plugin-Auflösung `IScmEventSource`/`IScmEventResponder` am `ProviderPrefix` (Z. 88-93) → Hostaktionen (Empfangs-Reaktion Z. 97-102, fällige `waiting`/`attachments`-Outbox-Aktionen Z. 103-105) → Reconcile bestehender Versuche (Z. 107-123) → `NextAttemptUtc`-Sperre (Z. 126) → ältere unerledigte Ereignisse derselben Aufgabe sperren (Z. 127-131) → `BlockReason` der Aufgabe (Z. 134-135, 181-182; `Beendet`/`Archiviert` bzw. `PausiertBisUtc > jetzt` → „Start gesperrt", Z. 531-533) → Repository-Revalidierung + `CanWrite` (Z. 138-142) → Eingangs-Revalidierung (Z. 145-154) → CLI-Session (`cli.GetScmPromptSession`, Z. 156-178):
- Prefix-Auflösung `ResolveDevelopmentAutomationPluginWithProjectScopeAsync(task.KiPluginPrefix, task.ProjektId)` (Z. 164), `ResolveDevelopmentAutomationPluginStrictAsync` (Z. 167) — wirft `InvalidOperationException` bei nicht installiertem/deaktiviertem Plugin → `catch` Z. 250-255 setzt `NextAttemptUtc = long.MaxValue` (**harte Sperre, kein Fallback**).
- Health-Check (Z. 168), Start via `development.ProzessStartenUndCliStartenAsync` bzw. `CliNeustartenAsync` mit `strictPluginSelection: true` (Z. 170-174).
- `WaitForInputReadyAsync` (Z. 187-201), Exchange-Vorbereitung `exchange.PrepareAsync` (Z. 211), Statusübergänge und `WritePromptAsync` (Z. 230-232).
- `ReconcileAttemptAsync` (Z. 389-504): Accepted → `"InBearbeitung"`, KI-Aktionen → Outbox (`ai:`-Purpose), Complete → `"Verarbeitet"` inkl. Screenshot-Nachweis-Upload und `LinkReportedPullRequestAsync`.
- Fehlerbehandlung: `ScmProviderException` → `NextAttemptUtc` per Backoff (`RetryAt`, Store Z. 31-32) bzw. `long.MaxValue` bei Auth/Permission (Z. 246-247); `InvalidOperationException`/`IOException` → `long.MaxValue` (Z. 253).

### 1.5 „Aufgabe beendet/pausiert" — Erkennung (Ist)

- **Beendet**: `EntwicklungsprozessService.AbschliessenAsync` (Z. 436-476, unter `ScmTaskCoordinator`) stoppt CLI (`KiAusfuehrungsService.StopCliAsync`), bricht zeitgesteuerte Prompts ab, `AufgabeService.AbschliessenAsync` setzt `Status = Beendet`, `AusfuehrungsStatus = Beendet`, räumt `AktiveRunId`/`LaufStatus` (`AufgabeService.cs` ~Z. 896-916). Ausgelöst manuell oder automatisch durch `ScmTaskCompletionMonitoringService` (Z. 136-141) nach konfigurierbarer Wartezeit (`scm.taskcompletion.wait.minutes`, Default 60 min) bei geschlossener/gemergter SCM-Referenz.
- **Pausiert**: `Aufgabe.PausiertBisUtc` (Zukunftswert). Setter: `AufgabeService.SetPauseAsync` (Z. 853-889, erlaubte Stati Neu/Gestartet/Wartend), `ScmEventStore.PauseForDelayAsync` (Verzögerungsdirektive), `KiPluginLimitService.VerarbeiteRateLimitAsync` (Session-Limit). Ein laufender CLI-Prozess wird durch eine Pause **nicht** unterbrochen (`Aufgabe.cs:97-103`).
- **Lücke (relevant für #410):** Es existiert **kein ereignisgesteuerter Benachrichtigungsweg** „Aufgabe wurde beendet/pausiert" in Richtung SCM-System. Pausen/Ende werden nur indirekt wirksam: beim nächsten Poll oder beim nächsten Dispatch-Versuch des konkreten Ereignisses (`BlockReason`). `AufgabeLaufdatenChangedNotifier` (`AufgabeLaufdatenChangedNotifier.cs`) ist ein UI-orientierter Change-Notifier, kein Start-Trigger.

---

## 2. Aufgaben-/CLI-Ausführung

- **`KiAusfuehrungsService`** (`src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs`) — Singleton, zentrale Wahrheit für „CLI läuft": `ConcurrentDictionary<Guid, CliProcessHandle> _handles` (Z. 26). Relevante API:
  - `IsRunning(aufgabeId)` (Z. 88-97), `GetRunningCount()` (Z. 111-116), `GetLaufendeAufgabenIds()` (Z. 135-143) — zählen nur Handles mit `!Process.HasExited`.
  - `GetTerminalSession`/`GetScmPromptSession(aufgabeId)` — die SCM-Promptsitzung (`ScmPromptSessionAdapter`, enthält `PluginPrefix`, gesetzt Z. 377).
  - `IRunningAutomationStatusSource` (`src/Softwareschmiede/Domain/Interfaces/IRunningAutomationStatusSource.cs`): `RunningCountChanged`, `GetRunningCount`, `IsRunning`, `GetLiveLaufStatus`.
  - Startpfade: `StartCliAsync` (klassischer Prozess, Z. 174-183) und `StartTerminalSessionAsync` (PTY-Session mit `ScmPromptSessionAdapter`, Z. 272-282); beide unter `ScmTaskCoordinator` + `_startLock`, Doppel-Start-Schutz (Z. 191-203).
  - **`CliProcessHandle`** (Z. 853+): `CliRunId`, `AufgabeId`, `Process`, `ScmPromptSession` (optional), `Session`, `OutputSink` — **kein PluginPrefix-Feld**; der Prefix steckt nur im `ScmPromptSessionAdapter` (Terminalpfad).
- **Persistenter Ausführungsstatus**: `Aufgabe.AusfuehrungsStatus` (`AufgabeAusfuehrungsStatus`: `NichtGestartet`/`Aktiv`/`Beendet`, `AufgabeAusfuehrungsStatus.cs`), `AktiveRunId`, `LastHeartbeatUtc`, `LetzterCliStartUtc`, `LaufStatus` (`Aufgabe.cs:70-86`). `CliProcessManager` (`CliProcessManager.cs`, Singleton) persistiert Heartbeats/Laufstatus und abonniert `CliProcessStatusChanged`/`CliLaufAktiv`/`RuntimeStatusChanged`.
- **Statusmodell Aufgabe**: `AufgabeStatus` = `Neu|Gestartet|Wartend|Beendet|Archiviert` (`AufgabeStatus.cs`); `AufgabeStatusExtensions.AktivOderWartendStatus`, `DarfAusfuehrungStarten` (`AufgabeAusfuehrungsStatusExtensions.cs:7-9`).
- **Plugin-Verknüpfung**: `Aufgabe.KiPluginPrefix` (`Aufgabe.cs:62`), gesetzt bei Anlage/Update (`AufgabeService.UpdateAsync`, `AufgabeService.cs:575`) und vom Dispatcher für den Session-Start verwendet.
- **`LaufendeAutomatisierungenZaehlung`** (`LaufendeAutomatisierungenZaehlung.cs`, Singleton, für Auto-Shutdown): zählt fachlich laufende Automatisierungen via `KiAusfuehrungsStatusKlassifikation` über aktive Aufgaben **plus** `ScmIncomingEvents` im Status `"Verzögert"` (Z. 60). Es existiert also bereits eine globale Laufzählung — jedoch **keine Zählung pro KI-Plugin**.

---

## 3. KI-Plugin-Verwaltung (#411)

- **`PluginActivationService`** (`PluginActivationService.cs`): Aktivierungsstatus als `AppEinstellung` `plugins.enabled.<prefix>`; **fehlender/ungültiger Eintrag = aktiviert** (`IsEnabledValue`, Z. 82-88). `GetEnabledDevelopmentAutomationPluginsAsync` etc. filtern `IPluginManager`-Listen.
- **`PluginDefaultSettingsService`** (`PluginDefaultSettingsService.cs`): Schlüssel `plugins.default.<PluginType>` (global, Z. 109) und `plugins.default.project.<projektId>.<PluginType>` (projektbezogen, Z. 111); Wert = `PluginPrefix`.
- **`PluginSelectionService`** (`PluginSelectionService.cs`):
  - `ResolveDevelopmentAutomationPluginWithProjectScopeAsync` (Z. 103-126): Aufgaben-Prefix → Projekt-Default → Global-Default (`plugins.default.*`) → `ki.plugin.default`-Einstellung (`AppEinstellungService.DefaultKiPluginKey`, Z. 141; Match auf PluginName **oder** PluginPrefix, Z. 148-156). **Prüft den Aktivierungsstatus nicht** — liefert den Prefix zurück, auch wenn das Plugin deaktiviert ist (relevant für #411).
  - `ResolveDevelopmentAutomationPluginStrictAsync` (Z. 88-94): nur expliziter Prefix, nur wenn **aktiviert**; sonst `InvalidOperationException("… nicht mehr installiert oder aktiviert.")`.
  - `GetAvailableKiPluginPrefixesAsync` (Z. 62-70): Prefixe aller aktivierten KI-Plugins.
- **`KiPluginLimitService`** (`KiPluginLimitService.cs`): **Kein Parallelitätslimit** — verwaltet vom Provider gemeldete Session-Limit-Reset-Zeitpunkte (`plugins.sessionlimit.<prefix>`), pausiert laufende Aufgaben desselben Prefix bis zum Reset (`VerarbeiteRateLimitAsync`, Z. 50-131), `GetAktiveSessionLimitsAsync` (Z. 140-168).
- **`PluginType`** (`Plugin.Contracts/Domain/Enums/PluginType.cs`): `SourceCodeManagement`, `DevelopmentAutomation`, `Ide`.
- **`PluginManager`** (`src/Softwareschmiede/Infrastructure/Plugins/PluginManager.cs`, Singleton): `GetSourceCodeManagementPlugins()`/`GetDevelopmentAutomationPlugins()`/`GetIdePlugins()`/`GetDefault*` — ungefiltert (inkl. deaktivierter).
- **`IKiPlugin`** (`Plugin.Contracts/Domain/Interfaces/IKiPlugin.cs`): ausschließlich **interaktive** CLI-Starts (`StartCliAsync`, `GetTerminalStartSpecAsync`, `CheckHealthAsync`, `SupportsSessionContinuation`).
- **`IIssueTemplateTextGenerator`** (`IIssueTemplateTextGenerator.cs`): **Einmal-Textgenerierung** (`FillIssueTemplateAsync(templateBody, originalRequirement)` → `Task<string>`); implementiert von allen KI-Plugins (`ClaudeCliPlugin.cs:105`, `CodexPlugin.cs:86`, `DevinPlugin.cs:113`, `GitHubCopilotPlugin.cs:98`, `KiSimulatorPlugin.cs:49`). Hilfsmittel in `CliKiPluginBase` (`CliKiPluginBase.cs:249-325`): `BuildIssueTemplateFillPrompt`, `RunOneShotTextGenerationAsync` (Prozess mit stdin-Prompt → stdout, non-interaktiv).

---

## 4. Einstellungen (#410, #411, #412)

- **Persistenzmuster**: Key-Value-Tabelle `AppEinstellung` (`Domain/Entities/AppEinstellung.cs`: `Schluessel`/`Wert`/`AktualisiertAm`); `AppEinstellungService` (`AppEinstellungService.cs`) hält Schlüssel-Konstanten + typsichere Getter/Setter (`GetSettingAsync`, `GetBoolSettingAsync`, `GetIntSettingAsync`, `SetSettingAsync` als Upsert, Z. 133-189). Bestehende SCM-Schlüssel (Z. 43-103): `scm.monitoring.enabled`, `scm.trigger.keyword`, `scm.delay.keyword`, `scm.prompt.append.*` (5 Keys), `scm.taskcompletion.enabled|wait.minutes|scope`, `scm.backmerge.*`, `scm.monitoring.accountmode`; `ki.plugin.default`, `scm.plugin.default`, `plugins.ide.order`, `plugins.enabled.*`, `plugins.default.*`, `plugins.sessionlimit.*`, `plugins.backmerge.project.<id>` (projektbezogenes Präfix-Muster, Z. 106/205).
- **UI-Konvention**: `SettingsViewModel` (`src/Softwareschmiede.App/ViewModels/SettingsViewModel.cs`, 1146 Z.): private Felder + `SetProperty`-Properties, abgeleitete Sichtbarkeits-/Status-Properties (z. B. `IsTriggerKeywordSichtbar`, Z. 315); `LadenAsync` (Z. 502-580) und `SpeichernAsync` (Z. 582-711) lesen/schreiben alle Schlüssel über `_einstellungService`; `ValidierePflichtfelder` (Z. 876+). Enum-Auswahl über `*Option`-Klassen (`ScmBackmergeModusOptionen.cs`, `ScmKontoModusOptionen.cs`, `ScmTaskCompletionPruefmodusOptionen.cs` in `ViewModels/`).
- **View**: `SettingsView.xaml` (`src/Softwareschmiede.App/Views/SettingsView.xaml`) — Abschnitt „Automatisierung" (Z. 351) mit `Expander`n `AufgabenAusfuehrenExpander` (Z. 357), `AufgabenBeendenExpander` (Z. 502), `ProzessablaeufeExpander` (Z. 566); `AutomationProperties.Name`-Konvention für E2E.
- **Plugin-Einstellungen**: `PluginActivationEntry`/`PluginSettingEntry`/`PluginSettingGroupEntry` + `PluginSettingsService` + Entity `PluginKonfiguration` — plugindeklarierte Felder (`IPlugin.GetSettingGroups()`), getrennt von `AppEinstellung`.

---

## 5. Repository-/Projekt-Konfiguration & SCM-Plugin-Schnittstellen (#412)

- **`GitRepository`** (`Domain/Entities/GitRepository.cs`): `PluginTyp` (= SCM-Plugin-Prefix), `RepositoryUrl`, `Aktiv`, `DefaultSourceBranchName`, `StartKonfiguration`, `InitialisierungKonfiguration`; gehört zu `Projekt` (`Projekt.cs`). `ScmPluginMatcher` (`ScmPluginMatcher.cs`, 13 Z.) matcht Plugin ↔ Repository.
- **SCM-Ereignisfähigkeiten**: `IScmEventSource` (Repo-Auflösung, `ReadEventsAsync`, `EnumerateReferencesAsync`, `ReadIssueAsync`, `ReadPullRequestAsync`), `IScmEventResponder` (Reconcile + Execute von `ScmResponseRequest`), `IScmNotificationSource`, `IScmAgentAccess` — **implementiert ausschließlich vom GitHub-Plugin** (`plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.cs:19`; Event-Pfade in `GitHubPlugin.Events.cs`, Response-Pfade in `GitHubPlugin.EventResponses.cs`). BitBucket und LocalDirectory implementieren **keine** dieser Interfaces.
- **Ereignistypen**: `ScmContentKind { Description, IssueComment, ReviewComment, PullRequestReview }`, `ScmReferenceKind { Issue, PullRequest }` (`ScmEvents.cs:9-12`). Ein **neu angelegtes Issue** erscheint als `Description`-Beobachtung einer enumerierten Issue-Referenz — aber nur mit Trigger-Keyword wird daraus ein `ScmIncomingEvent` (Validierung, s. 1.1); es gibt **keinen dedizierten „Issue wurde erstellt"-Ereignistyp** und keine unzugeordnete Verarbeitung ohne Trigger.
- **Rückschreiben (Ist)**:
  - Kommentare/Reaktionen/PR-Erstellung: `IScmEventResponder.ExecuteResponseAsync` mit `ScmResponseKind { Comment, Reaction, PullRequest }` (`ScmEvents.cs:55`); GitHub: `POST issues/{n}/comments` (`GitHubPlugin.EventResponses.cs:69`). Outbox-Gerüst (`ScmEventOutbox`/`ScmOutgoingAction`) existiert mit deterministischen IDs, Reconcile und Backoff.
  - Issue-Anlage: `IIssueCreateProvider` (`CreateIssueAsync`, `CanCreateIssueAsync`) — GitHub (`GitHubPlugin.cs:743-747`) und BitBucket (`BitBucketPlugin.cs:698-706`) via `GitPluginBase`.
  - **Lücke:** **Keine Schnittstelle zum Ändern einer Issue-Beschreibung** (kein `UpdateIssue*`/`EditIssue*`/`PATCH`-Aufruf irgendwo — Suche ergebnislos). `ScmResponseKind` kennt kein Body-Update; `IGitPlugin` hat keine Update-Methode.
  - **`Issue`-Record** (`Plugin.Contracts/Domain/ValueObjects/Issue.cs`): `Nummer`, `Titel`, `Body`, `Labels`, `Milestone`, `IssueUrl`, `IstGeschlossen`.
- **Aufgaben-Anlage aus Ereignissen**: `ScmEventTaskResolver` (`ScmEventTaskResolver.cs`) legt bei Trigger auf unzugeordneten Referenzen automatisch Aufgaben an (`AufgabeService.CreateFromIssueEventAsync`, Z. 71; `CreateFromPullRequestAsync`, Z. 99) — inkl. Mehrdeutigkeits-/Einmaligkeits-Dedup (`IssueReferenz.SourceKey`, Unique-Index).

---

## 6. Tests

- **`src/Softwareschmiede.Tests`** (`Softwareschmiede.Tests.csproj`): xUnit 2.9.3, Moq 4.21, FluentAssertions, EF Core InMemory, `Microsoft.Extensions.TimeProvider.Testing`, FlaUI (E2E), Xunit.SkippableFact; `net10.0-windows`, `DisableTestParallelization` (`AssemblyInfo.cs`); `CS1591` als Fehler.
  - **Kategorien**: `OsInterfaceAttribute`/`OsInterfaceFactAttribute` (`Infrastructure/Testing/`, Trait `Category=OsInterface`). Reguläre Lane: `--filter "Category!=OsInterface"`; OS-Lane separat (siehe CLAUDE.md: voller Build vor Tests; `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` in der Sandbox; `dotnet test` nie im Hintergrund).
  - Struktur spiegelt Produktions-Namespaces (`Application/Services`, `App/ViewModels`, `Domain`, `E2E`, `Helpers`); Testnamen gemischt `Methode_SollVerhalten_WennBedingung` und englische Descriptives.
  - Beispiele nahe den Themen: `KiPluginLimitServiceTests`, `PluginActivationServiceTests`, `PluginDefaultSettingsServiceTests`, `PluginSelectionServiceTests(_IdePlugin)`, `LaufendeAutomatisierungenZaehlungTests`, `AppEinstellungServiceTests_*`, `SettingsViewModelTests*`, `AufgabeServiceTests*`, `ScmCliPromptProfileTests`, `ScmEventOrchestratorTests`.
- **`src/Softwareschmiede.IntegrationTests`**: echte SQLite-DB + echte Service-Komposition; SCM-Tests `ScmEventDispatchTests*` (10+ Dateien), `ScmEventMonitoringServiceTests_*`, `ScmEventDelayIntakeTests`, `ScmTaskCompletionMonitoringServiceTests`, `ScmEventPersistenceTests`, `ScmEventCliExchangeAccessTests*`.
- **`ScmDispatchFixture`** (`src/Softwareschmiede.Tests/Helpers/ScmDispatchFixture.cs`, Namespace `Softwareschmiede.TestSupport`, 291 Z.): Mocking-Ansatz für Plugins — `Mock<IGitPlugin>` mit `Git.As<IScmEventSource>()`, `.As<IScmEventResponder>()`, `.As<IScmAgentAccess>()`, `.As<IScmNotificationSource>()`; `Mock<IKiPlugin>` + `Mock<ITerminalSessionFactory>` mit skriptbaren Sessions (`ScmKiProfile`: Simulator/Devin-Banner); `Mock<IPluginManager>`; echte `ServiceProvider`-Komposition mit echten Domain-Services.
- **E2E-SCM**: `src/Softwareschmiede.Tests/E2E/E2E_ScmEvents.cs`, `E2E_ScmRegressionSimulator.cs`; App-seitige E2E-Infrastruktur `src/Softwareschmiede.App/Services/Testing/ScmE2E*` (ersetzt `IPluginManager`, `ITerminalSessionFactory`, `ICredentialStore` per Umgebungsvariable, `App.xaml.cs:388-400`); `Softwareschmiede.Plugin.KiSimulator` als deterministisches Test-KI-Plugin.

---

## 7. Konventionen

- **DI**: zentrale Registrierung in `src/Softwareschmiede.App/App.xaml.cs` `ConfigureServices` (ab Z. 177). Muster: Domain-Services `AddScoped` (Z. 213-272); Hintergrunddienste `AddSingleton<X>(); AddHostedService(sp => sp.GetRequiredService<X>())` (Z. 232-237, 306-309); ViewModels `AddTransient` (Z. 371-386); `IServiceScopeFactory` für Zugriff aus Singletons; `TimeProvider.System` injizierbar (Z. 304); `IOptions<ScmEventOptions>` aus Konfigurationssektion `"ScmEvents"` (Z. 222).
- **Persistenz**: EF Core + SQLite (`UseSqlite`, Z. 193-195); Migrations unter `src/Softwareschmiede/Migrations/` (timestamped `yyyyMMddHHmmss_Name.cs` + `.Designer.cs` + `SoftwareschmiededDbContextModelSnapshot.cs`); Design-Time-Factory `SoftwareschmiededDbContextDesignTimeFactory.cs`; Anwendung beim App-Start via `db.Database.MigrateAsync()` (`App.xaml.cs:96`), danach `ScmEventStartupGate.Ready()` — Hintergrunddienste warten via `startup.WaitAsync`.
- **Neue Entities/Felder**: Entity in `Domain/Entities`, `DbSet` + `OnModelCreating`-Konfiguration in `SoftwareschmiededDbContext.cs` (Z. 25-315; SCM-Tabellen Z. 94-124), Migration via `dotnet ef migrations add` (Referenz: `docs/features/task/issue-265-…/plan.md:212`).
- **Hintergrunddienste**: Basisklasse `PeriodicBackgroundService` (`PeriodicBackgroundService.cs`) — `RunOnceAsync`-Überschreibung, Fehler pro Zyklus geloggt, `TimeProvider` für testbares Delay.
- **Code-Konventionen**: `CS1591` (XML-Doc auf öffentlichen Membern) ist **Build-Fehler** in allen Projekten; deutsche Fachbegriffe (`Aufgabe`, `Einstellung`, `PausiertBisUtc`, `Protokolleintrag`) neben englischen SCM-Integrationsnamen (`ScmIncomingEvent`, `DispatchAsync`); XML-Docs auf Deutsch; primary constructors bei neueren Services; explizites Fehlermodell (`ScmProviderException` mit `ScmProviderFailureKind`).
- **Commits/Doku**: Conventional Commits (`CONTRIBUTING.md`); Closing-Keyword `Closes #N`/`Fixes #N` im Commit nötig (globale Regeln); Feature-Dokus unter `docs/features/task/<id>/inventory.md` (Beispiel: `docs/features/task/a66a5c27-…/inventory.md`), Projekt-Dokus unter `docs/projects/task/<branch>/`; Anwender-Hilfe unter `docs/help/` (u. a. `docs/help/plugins/github-ereignisse/` mit beschreibung/architektur/datenmodell/business-rules/troubleshooting).
- **Self-Hosting-Regel**: `Softwareschmiede.App.exe` nie eigenständig beenden (CLAUDE.md); bei Build-Locks Teil-Build ohne App-Projekt.

---

## 8. Explizite Lücken (für die Anforderungen fehlende Bausteine)

| # | Fehlende Fähigkeit | Beleg |
|---|---|---|
| L1 | **Keine Ausführungsbegrenzung**: Die Dispatch-Schleife (`ScmEventMonitoringService.cs:229-248`) startet jedes fällige Ereignis; es existiert kein Limit (global/pro Plugin), keine Startprüfung mit Slot-Logik und keine Priorisierung nach (pausiert/beendet → Verzögerung fällig → Eingang). Aktuelle Auswahl: strikt `Sequence`-aufsteigend. | Z. 235-240 |
| L2 | **Keine Zählung „laufende CLI-Ausführungen pro Plugin"**: `KiAusfuehrungsService.GetRunningCount()`/`GetLaufendeAufgabenIds()` liefern nur globale Werte; `CliProcessHandle` hat kein `PluginPrefix`-Feld (Prefix nur im optionalen `ScmPromptSessionAdapter`, Z. 377). Pro-Plugin-Attribution müsste über `Aufgabe.KiPluginPrefix` oder ein neues Handle-Feld erfolgen. | `KiAusfuehrungsService.cs:111-143, 853+` |
| L3 | **Kein Auslöser „Aufgabe beendet/pausiert → Startprüfung"**: Pausen-/Ende-Erkennung existiert (`PausiertBisUtc`, `Status`, `AusfuehrungsStatus`, `ScmTaskCompletionMonitoringService`), aber kein Notification-Hook, der die SCM-Ausführungsprüfung anstößt — nur der 60-s-Poll prüft erneut. | s. Abschnitt 1.5 |
| L4 | **Kein Plugin-Fallback bei deaktiviertem Projekt-/Global-Default (#411)**: `ResolveDevelopmentAutomationPluginWithProjectScopeAsync` gibt deaktivierte Prefixe ungeprüft zurück; `ResolveDevelopmentAutomationPluginStrictAsync` wirft dann → `NextAttemptUtc = long.MaxValue` (harte Sperre mit Warte-Kommentar). Kein Fallback auf das globale Standard-Plugin und kein Unterschied zwischen „deaktiviert" und „nicht installiert". | `PluginSelectionService.cs:103-126,88-94`; `ScmEventOrchestrator.cs:164-168,250-255` |
| L5 | **Keine Schnittstelle zum Ändern einer Issue-Beschreibung (#412)**: `IScmEventResponder`/`ScmResponseKind` decken nur `Comment|Reaction|PullRequest` ab; `IGitPlugin`/`IIssueCreateProvider` nur Anlage. Kein `PATCH issues/{n}` o. ä. in Plugins. Kommentar-Posten dagegen existiert (`GitHubPlugin.EventResponses.cs:69`). | `ScmEvents.cs:55`, `IGitPlugin.cs`, `IScmEventResponder.cs` |
| L6 | **Kein „Issue erstellt"-Ereigniskanal (#412)**: Neue Issues erscheinen als `Description`-Beobachtung, aber ohne Trigger-Keyword verwirft `ScmEventValidationService.GetRejection` sie vor der Inbox (`"Kein Trigger"`), und `ScmIncomingEvent.AufgabeId` ist nicht-nullbar — es gibt keinen aufgabenlosen Ereignistyp und keine Repository-weite Beobachtung „alle neuen Issues" ohne Trigger. | `ScmEventValidationService.cs:39-68`, `ScmEventStore.cs:80-84`, `ScmIncomingEvent.cs:16` |
| L7 | **Kein konfigurierbares Standard-KI-Plugin *für eine Funktion*** (#412): Es existieren Standard-Plugin-Schlüssel je `PluginType` (`plugins.default.DevelopmentAutomation`, `ki.plugin.default`) und projektbezogene Defaults — aber kein Mechanismus „Standard-Plugin für Funktion X". | `PluginDefaultSettingsService.cs:109-111` |
| L8 | **Keine generische „Analysiere-Text"-KI-Fähigkeit jenseits des Template-Füllens**: `IIssueTemplateTextGenerator.FillIssueTemplateAsync` ist ein Einmal-Prompt (Titel+Text rein, Text raus) und fachlich auf Issue-Templates zugeschnitten; ein generisches Analyse-Interface existiert nicht. Alle KI-Plugins implementieren es bereits — wiederverwendbares Fundament. | `IIssueTemplateTextGenerator.cs`, `CliKiPluginBase.cs:249-325` |
| L9 | **SCM-Ereignisse nur GitHub**: BitBucket/LocalDirectory implementieren `IScmEventSource`/`IScmEventResponder` nicht — Monitoring/Issue-Aufbereitung wäre faktisch GitHub-only, sofern nicht erweitert. | `GitHubPlugin.cs:19`; grep über `plugins/` |
| L10 | **`ScmIncomingEvent`-Verarbeitung ist aufgabengebunden**: `AufgabeId` ist Pflichtfeld, Dispatch setzt eine Aufgabe + Lease voraus; eine aufgabenlose Verarbeitung (Issue-Aufbereitung ohne Aufgabenanlage) passt nicht in das bestehende Inbox-/Dispatch-Modell. | `ScmIncomingEvent.cs:16`, `ScmEventOrchestrator.cs:25-34` |

---

## 9. Die 5 wichtigsten Befunde (Kurzfassung)

1. **Ausführung ist sofort-sequentiell nach Eingangsreihenfolge**: Die einzige Auswahlstelle ist die Dispatch-Schleife in `ScmEventMonitoringService.RunOnceAsync` (Z. 229-248) — dort müsste die Limit-/Prioritätslogik von #410 ansetzen; die Serialisierung pro Aufgabe (`ScmTaskCoordinator` + `ScmTaskLeases`) ist bereits vorhanden.
2. **„CLI läuft" ist zentral und zählbar, aber nicht pluginattribuiert**: `KiAusfuehrungsService._handles` + `IsRunning`/`GetRunningCount`/`GetLaufendeAufgabenIds`; Plugin-Bezug nur über `Aufgabe.KiPluginPrefix` oder `ScmPromptSession.PluginPrefix` (Terminalpfad).
3. **Plugin-Auflösung hat heute genau den von #411 beschriebenen Defekt**: Deaktiviertes Projekt-Default → `InvalidOperationException` → permanente Sperre (`NextAttemptUtc = long.MaxValue`); kein Fallback-Pfad.
4. **Für #412 fehlt der kritischste Baustein**: keine API zum Ändern einer Issue-Beschreibung (nur Kommentar/Reaction/PR via `IScmEventResponder`, nur Anlage via `IIssueCreateProvider`) und kein Trigger-loser Ereigniskanal für neue Issues.
5. **Wiederverwendbare Fundamente für #412 sind vorhanden**: `IIssueTemplateTextGenerator` (Einmal-KI-Aufruf, von allen KI-Plugins implementiert), `ReadIssueAsync` (Titel+Body), Outbox-/Kommentar-Infrastruktur, Repository-Enumeration sowie das Einstellungs-/UI-Muster (`AppEinstellung`-Keys + `SettingsViewModel`/`SettingsView.xaml`-Expander).
