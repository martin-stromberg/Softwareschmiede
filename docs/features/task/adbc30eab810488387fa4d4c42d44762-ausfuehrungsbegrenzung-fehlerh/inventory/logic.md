# Logik — Bestandsaufnahme

## `ScmEventExecutionLimitService`
Datei: `src/Softwareschmiede/Application/Services/ScmEventExecutionLimitService.cs`

Zulassungskontrolle für neue CLI-Starts aus dem SCM-Ereignis-Dispatch. Scoped-Dienst, aber prozessweiter Reservierungsbestand (`static`). Injiziert: `SoftwareschmiededDbContext db`, `AppEinstellungService einstellungen`, `ICliExecutionLimitSource cli`, `TimeProvider time` (Z. 43–47). Konstanten: `GlobalLimitKey = "scm.execution.maxparallel"` (Z. 50), `PluginLimitKeyPrefix = "plugins.maxparallel."` (Z. 53), `ReservierungsLebensdauer = 5 min` (Z. 57). Prozessweite Sperren: `ZulassungsGate` (SemaphoreSlim 1, serialisiert Check-und-Claim, Z. 64), `ReservierungenSync` + `Reservierungen`-Map (Z. 65–66).

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `PluginLimitKey(pluginPrefix)` | `public static` | Vollständiger Einstellungsschlüssel eines Plugin-Limits (Z. 71) |
| `PruefeUndReserviereStartPlatzAsync(pluginPrefix, ct)` | `public` | Atomare Limit-Prüfung + Platzbelegung unter `ZulassungsGate`; liefert `CliStartPlatzZulassung` mit `Wartegrund` oder `Reservierung` (Z. 85–110). Aufgerufen von `ScmEventOrchestrator.DispatchCoreAsync` (Z. 245) und `DispatchAufbereitungAsync` (Z. 698) |
| `GetStartBlockReasonAsync(pluginPrefix, ct)` | `public` | Reine Prüfung ohne Platzbelegung, ebenfalls unter `ZulassungsGate` (Z. 123–137) |
| `GetStartBlockReason(pluginPrefix, globalLimit, pluginLimit, belegung)` | `internal static` | Reine Auswertung: globales Limit zuerst, dann Plugin-Limit; liefert Wartegrund-Text (Z. 145–153) |
| `ReservierungenZuruecksetzen()` | `internal static` | Test-Hilfsmittel: leert alle offenen Reservierungen (Z. 156–160) |
| `GetLimitsAsync(pluginPrefix, ct)` | `private` | Liest globales + Plugin-Limit in einem `GetSettingsAsync`-Aufruf; `ParseLimit`: ungültig/leer = unbegrenzt (Z. 163–174, 201–202) |
| `WirksameBelegungAsync(ct)` | `private` | `GetBelegungAsync` + offene (nicht verfallene) Reservierungen; verfallene werden entfernt. Muss unter `ZulassungsGate` laufen (Z. 178–199) |
| `GetBelegungAsync(ct)` | `private` | **Kern der Anforderung:** lädt `cli.GetLaufendeAufgabenIds()`; DB-Projektion `new { x.Id, x.KiPluginPrefix }` — **`PausiertBisUtc` wird nicht selektiert/ausgewertet**. Attribution: lebende SCM-Sitzung (`session.IsAlive && session.PluginPrefix`) vor gespeichertem `KiPluginPrefix`; aufgaben ohne auflösbares Prefix zählen nur global. Rückgabe `new(laufendeIds.Count, proPlugin)` (Z. 204–225) |

## `KiAusfuehrungsService`
Datei: `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs`

Singleton-Prozessverwalter; implementiert `IRunningAutomationStatusSource` und `ICliExecutionLimitSource` (Z. 18). Bestand: `_handles` (`ConcurrentDictionary<Guid, CliProcessHandle>`, Z. 26).

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `IsRunning(aufgabeId)` | `public` | `!Process.HasExited` des Handles, exception-tolerant (Z. 88–97). Genutzt u. a. von `CliProcessManager`, `AufgabeRecoveryService` |
| `GetRunningProcess(aufgabeId)` | `public` | Laufender `Process` oder `null` (Z. 102–108) |
| `GetRunningCount()` | `public` | Anzahl Handles mit `!HasExited` (Z. 111–116) |
| `NotifyLaufAktiv(aufgabeId)` | `public` | Feuert `CliLaufAktiv` bei laufendem Prozess (Z. 124–130) |
| `GetLaufendeAufgabenIds()` | `public` | **Quelle der Limit-Belegung:** IDs aller Aufgaben mit `!Process.HasExited` — kein Pausenbewusstsein (Z. 135–143). Wird auch von `CliProcessManager.AbgleichAsync` (Laufdaten-Sicherstellung) genutzt |
| `GetLiveLaufStatus(aufgabeId)` | `public` | Live-Substatus aus der Terminal-Session; `Laeuft` als Fallback bei laufendem Handle (Z. 146–158) |
| `StartCliAsync(...)` / `StartTerminalSessionAsync(...)` | `public` | Prozess-/Sessionstart unter `_startLock`; Early-Return bei bereits laufendem Handle; feuert `RaiseRunningCountChanged` + `CliProcessStatusChanged` (Z. 174–255, 272–460) |
| `GetTerminalSession(aufgabeId)` | `public` | `ITerminalSession` des Handles (Z. 472–477) |
| `GetScmPromptSession(aufgabeId)` | `public` | `IScmPromptSessionAdapter` des Handles (`ICliExecutionLimitSource`-Vertrag, Z. 480–481) |
| `StopCliAsync(aufgabeId, ct)` | `public` | SIGTERM → 5 s → Kill (Z. 514ff.) |
| `UpdateHeartbeat(aufgabeId)` | `public` | Heartbeat-Persistenz (Z. 563) |
| `RaiseRunningCountChanged()` | `private` | Vergleicht vorherige/aktuelle `GetRunningCount()` unter `_runningCountLock` und feuert `RunningCountChanged` (Z. 774–786); aufgerufen bei Start (Z. 246, 416) und Prozessende (Z. 710) |

Publizierte Events: `CliProcessStatusChanged` (Z. 68), `CliLaufAktiv` (Z. 82), `RunningCountChanged` (Z. 85) — letzteres abonniert von `ScmStartPruefungsService` (nur Rückgang löst aus) und `AutoShutdownOrchestrator`.

## `ScmEventOrchestrator`
Datei: `src/Softwareschmiede/Application/Services/ScmEventOrchestrator.cs`

Scoped; injiziert u. a. `ScmEventExecutionLimitService executionLimits`, `KiAusfuehrungsService cli`, `AufgabeService tasks`, `ScmEventStore store` (Z. 15–22).

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `DispatchDueEventsAsync(ct)` | `public` | Priorisierte Batch-Prüfung aller fälligen Ereignisse; lädt `AufgabenZustand`-Projektion inkl. `PausiertBisUtc` (Z. 55–59) und ruft `ScmDispatchPrioritaet.OrderForDispatch`. Aufgerufen vom Poll (`ScmEventMonitoringService` Z. 246) und von `ScmStartPruefungsService.RunOnceAsync` (Z. 34–66) |
| `DispatchAsync(eventId, explicitPluginPrefix, ct)` | `public` | Dispatch unter `ScmTaskCoordinator`-Aufgabensperre (Z. 69–74) |
| `OpenFromQueueAsync(eventId, select, ct)` | `public` | UI-Pfad „aus Queue öffnen" mit Lease; `BlockReason`-Prüfung (Z. 100) (Z. 77–118) |
| `DispatchCoreAsync(...)` | `private` | Generischer Zustellpfad; `BlockReason`-Sperre (Z. 189–190), Fortsetzung über lebende Sitzung (`adapter?.IsAlive == true`, Z. 211–217), **Zulassungskontrolle nur bei fehlender/toter Sitzung**: `PruefeUndReserviereStartPlatzAsync` Z. 245, Reservierung per `using` bis nach dem Start (Z. 253–263). `BlockReason`-Recheck nach `db.Entry(task).ReloadAsync` (Z. 269–271) (Z. 120–353) |
| `DispatchAufbereitungAsync(...)` | `private` | Dedizierter Pfad für Issue-Aufbereitung; spiegelt denselben Ablauf inkl. `PruefeUndReserviereStartPlatzAsync` (Z. 698) (Z. 600–797) |
| `ActivateForOpenAsync(eventId, ct)` | `public` | Aktiviert nur neue, unpausierte Aufgaben (`BlockReason is null`) (Z. 356–362) |
| `CancelAsync(eventId, ct)` | `public` | Benutzerabbruch; stuft beim Abbruch eines „Verzögert"-Ereignisses `PausiertBisUtc` auf das nächste verbleibende Direktivenziel zurück bzw. auf `null` — **ohne `ScmStartPruefungsAusloeser`-Meldung** (Z. 442–443) (Z. 422–447) |
| `RetryAsync` / `ConfirmReadyAsync` / `ResendAsync` | `public` | Mutieren + `DispatchCoreAsync` über `MutateAndDispatchAsync` (Z. 365–476) |
| `BlockReason(task)` | `private static` | Referenz-Pausendefinition: `Beendet`/`Archiviert` → „Aufgabe beendet/archiviert; Start gesperrt."; **`task.PausiertBisUtc > DateTimeOffset.UtcNow` → „Aufgabe pausiert; Start gesperrt."** (Z. 991–993) |
| `WaitAsync` / `WaitWithCommentAsync` | `private` | Status „Wartend" + Reason persistieren (Z. 995–1009) |

## `ScmStartPruefungsAusloeser` und `ScmStartPruefungsService`
Datei: `src/Softwareschmiede/Application/Services/ScmStartPruefungsService.cs`

| Element | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ScmStartPruefungsAusloeser.Angefordert` | `public event` | Signal einer angeforderten Startprüfung (Z. 19) |
| `ScmStartPruefungsAusloeser.StartpruefungAnfordern()` | `public` | Non-blocking Invoke; ohne Abonnenten wirkungslos (Z. 22). Aufgerufen von `AufgabeService.SetPauseAsync`, `ScmEventStore.AcceptBatchAsync`, `KiPluginLimitService.VerarbeiteRateLimitAsync` |
| `ScmStartPruefungsService.StandardSammelzeit` | `internal static` | 250 ms Sammelphase vor getriggerter Prüfung (Z. 38) |
| `ScmStartPruefungsService` (ctor) | `public` | Singleton-`BackgroundService`; abonniert `ausloeser.Angefordert` und `laufzaehlung.RunningCountChanged` (`IRunningAutomationStatusSource`) bereits im Konstruktor (Z. 58–74) |
| `RunOnceAsync(ct)` | `public` | Prüft `GetScmMonitoringEnabledAsync` (Laufzeit-Schalter wie Poll) und ruft `ScmEventOrchestrator.DispatchDueEventsAsync` (Z. 94–106) |
| `ExecuteAsync(stoppingToken)` | `protected override` | `ScmEventStartupGate` → Signal-Loop: Sammelzeit, Koaleszieren, `RunOnceAsync` (Z. 109–138) |
| `OnRunningCountChanged(previous, current)` | `private` | **Nur `currentCount < previousCount` löst aus** — frei werdende Plätze, nicht Starts; auch Nicht-SCM-Läufe zählen (Z. 159–166) |
| `Sammelzeit`, `AusstehendePruefungen` | `internal` | Test-/Diagnose-Hooks (Z. 83–86) |

## `ScmEventStore`
Datei: `src/Softwareschmiede/Application/Services/ScmEventStore.cs`

Scoped; optionaler Ctor-Parameter `ScmStartPruefungsAusloeser?` (Z. 18–19).

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `AcceptBatchAsync(aufgabeId, sourceKey, batch, ...)` | `public` | Inbox-Aufnahme in Transaktion; wertet `ScmDelayDirective.Parse` aus (Z. 96): bei `Deferred` Status „Verzögert", `NextAttemptUtc` = Zielzeitpunkt, `PauseForDelayAsync` (Z. 106); nach `SaveChangesAsync` + Commit: `startPruefungsAusloeser?.StartpruefungAnfordern()` bei gesetzter Pause (Z. 123–128) (Z. 50–129) |
| `PauseForDelayAsync(aufgabeId, untilUtc, ct)` | `private` | **Pausen-Setzpfad der Verzögerungsdirektive:** setzt `task.PausiertBisUtc = untilUtc` nur für `Neu`/`Gestartet`/`Wartend` und niemals verkürzend (`PausiertBisUtc >= untilUtc` → No-Op); SystemMeldung-Protokolleintrag (Z. 150–166) |
| `ClaimTaskAsync` / `RenewTaskAsync` / `ReleaseTaskAsync` | `public` | Instanzübergreifende Aufgabenleases (Z. 169–183) |

## `AufgabeService`
Datei: `src/Softwareschmiede/Application/Services/AufgabeService.cs`

Scoped; optionaler `ScmStartPruefungsAusloeser?` (Feld Z. 38, Ctor Z. 51).

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `SetPauseAsync(aufgabeId, pausiertBisUtc, ct)` | `public` | **Pausen-Setzpfad (manuell/UI):** nur `Neu`/`Gestartet`/`Wartend`; Zeitpunkt muss in Zukunft liegen oder `null` (Aufheben); setzt `PausiertBisUtc` (Z. 900), SystemMeldung, `SaveChangesAsync` (Z. 913), danach `_startPruefungsAusloeser?.StartpruefungAnfordern()` (Z. 918) — gilt für Setzen wie Aufheben (Z. 875–919) |

## `KiPluginLimitService`
Datei: `src/Softwareschmiede/Application/Services/KiPluginLimitService.cs`

Scoped; `SessionLimitKeyPrefix = "plugins.sessionlimit."` (Z. 20); optionaler `ScmStartPruefungsAusloeser?` (Z. 26, 40).

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `VerarbeiteRateLimitAsync(aufgabeId, resetUtc, ct)` | `public` | **Pausen-Setzpfad (Session-Limit):** persistiert Reset-Zeitpunkt als Einstellung; pausiert alle aktiv laufenden regulären Aufgaben (`AktivOderWartendStatus` + `AusfuehrungsStatus.Aktiv`) mit gleichem `KiPluginPrefix` — Max-Semantik (verkürzt nie, Z. 114–117). Nach `SaveChangesAsync` (Z. 128): `NotifyLaufdatenChanged` je Aufgabe, dann `_startPruefungsAusloeser?.StartpruefungAnfordern()` (Z. 143). Laufende Prozesse werden nicht angefasst (Z. 59–144) |
| `GetAktiveSessionLimitsAsync(prefixes, ct)` | `public` | Liest persistierte, noch zukünftige Session-Limits (Z. 153–181) |

## `ScmDispatchPrioritaet`
Datei: `src/Softwareschmiede/Application/Services/ScmDispatchPrioritaet.cs`

Statische Priorisierung der Dispatch-Reihenfolge.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `PrioritaetsKlasse(item, task)` | `public static` | Klasse 1 (Fortsetzung): Aufgabe nicht `Beendet`/`Archiviert` **und (`PausiertBisUtc is not null` — gesetzt reicht, auch abgelaufen — oder `AusfuehrungsStatus.Beendet` mit Status `Neu`/`Gestartet`/`Wartend`)**; Klasse 2: Ereignisstatus „Verzögert"; sonst 3 (Z. 36–46) |
| `OrderForDispatch(items, aufgabenZustaende)` | `public static` | Sortiert nach Klasse, Klasse 2 zusätzlich nach `NextAttemptUtc` aufsteigend, dann `Sequence` (Z. 52–60). Aufgerufen von `ScmEventOrchestrator.DispatchDueEventsAsync` |

## `ScmEventMonitoringService`
Datei: `src/Softwareschmiede/Application/Services/ScmEventMonitoringService.cs`

`PeriodicBackgroundService` mit `ScmEventOptions.PollInterval` (**60 s**, `ScmEventOptions` Z. 7). `RunOnceAsync` (Z. 30–247): Laufzeit-Schalter `GetScmMonitoringEnabledAsync` (Z. 38), Quellenbeobachtung/Inbox-Aufnahme, danach `ScmEventOrchestrator.DispatchDueEventsAsync` (Z. 246) — identische priorisierte Auswahl wie die ereignisgesteuerte Startprüfung.

## `ScmDelayDirective`
Datei: `src/Softwareschmiede/Application/Services/ScmDelayDirective.cs`

Statische Auswertung der `#verzögert`-Direktive (konfigurierbar, `DefaultDelayKeyword` Z. 12, `MaxDelay` 32 Tage Z. 15). `Parse(body, delayKeyword, now)` (Z. 58–102) liefert `Result` (`None`/`Deferred`/`Invalid`); `Deferred` trägt `TargetUtc` — Quelle des `PausiertBisUtc`-Werts auf dem Direktiven-Pausenpfad.

## `CliProcessManager`
Datei: `src/Softwareschmiede/Application/Services/CliProcessManager.cs`

Verwaltung laufender CLI-Prozesse; abonniert `CliProcessStatusChanged` und `CliLaufAktiv` (Z. 73–74).

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `AbgleichAsync()` | `private` | Alle 30 s (`AbgleichIntervall`): iteriert `_kiService.GetLaufendeAufgabenIds()` und stellt pro Aufgabe Laufdaten sicher — **geteilte Nutzung der Handle-Quelle**, die laut Anforderung unverändert bleiben soll; erfasst auch pausierte, aber laufende Prozesse (Z. 410–423) |
| `LaufdatenSicherstellenAsync(aufgabeId)` | `private` | Heartbeat-Timer + RuntimeStatus-Subscription + `AufgabeService.AktivenLaufSicherstellenAsync` mit `IsRunning`-Guard (TOCTOU) (Z. 357–386) |

## `KiAusfuehrungsStatusKlassifikation`
Datei: `src/Softwareschmiede/Application/Services/KiAusfuehrungsStatusKlassifikation.cs`

Statischer Klassifikator (Anzeige + Auto-Shutdown-Zählung). `Klassifiziere(status, out pausiertRestzeit)` (Z. 19–68): **Pausen-Check steht an erster Stelle** — `status.PausiertBisUtc is { } bis && bis > DateTimeOffset.UtcNow` → `KiAusfuehrungsZustand.Pausiert` (Z. 25–29); danach `PromptInWartestellung`, Live-Substatus (`Laeuft`/`Wartet`), `AufgabeAusfuehrungsStatus`, `AufgabeStatus.Wartend`, `Bereit`.

## `LaufendeAutomatisierungenZaehlung`
Datei: `src/Softwareschmiede/Application/Services/LaufendeAutomatisierungenZaehlung.cs`

Singleton-Zähler für den Auto-Shutdown: `ZaehleAsync` klassifiziert aktive Aufgaben über `KiAusfuehrungsStatusKlassifikation` (nur `Laeuft` zählt — **pausierte Aufgaben zählen hier bereits nicht als laufend**) und addiert SCM-Ereignisse im Status „Verzögert" (Z. 39–63).

## `AutoShutdownOrchestrator`
Datei: `src/Softwareschmiede/Application/Services/AutoShutdownOrchestrator.cs`

Abonniert `IRunningAutomationStatusSource.RunningCountChanged` (Z. 65); `OnRunningCountChanged` → `EvaluiereAsync` (Z. 179–189). Nutzt die fachliche Zählung (`LaufendeAutomatisierungenZaehlung`), nicht die rohe Handle-Zahl.

## `AufgabeRecoveryService`
Datei: `src/Softwareschmiede/Application/Services/AufgabeRecoveryService.cs`

`ScanForRecoveryCandidatesAsync` (Z. 37–56): schließt Aufgaben mit `PausiertBisUtc > now` als Recovery-Kandidaten aus (Z. 51) und prüft `IsRunning`. `RecoverManuellAsync` lehnt pausierte Aufgaben ab (Z. 86–91).

## `EntwicklungsprozessService`
Datei: `src/Softwareschmiede/Application/Services/EntwicklungsprozessService.cs`

`WirfWennPausiert(aufgabe)` (private static, Z. 987–994): `PausiertBisUtc > DateTimeOffset.UtcNow` → `InvalidOperationException` — Pause blockiert (Neu-)Starts auf diesem Pfad. Startpfade dieses Service durchlaufen die Zulassungskontrolle **nicht** direkt (das geschieht im `ScmEventOrchestrator` vor dem Aufruf); manuelle CLI-Starts unterliegen dem Limit bewusst nicht.

## `PromptZeitVersandService`
Datei: `src/Softwareschmiede/Application/Services/PromptZeitVersandService.cs`

Pausenbewusster Prompt-Versand: lädt `PausiertBisUtc` vor dem Versand (Z. 388–398), verschiebt geplante Prompts auf das Pausenende (Z. 203, 344–369). Reiner Leser, kein Pausen-Schreiber.

## `ScmTaskCompletionMonitoringService`
Datei: `src/Softwareschmiede/Application/Services/ScmTaskCompletionMonitoringService.cs`

`PeriodicBackgroundService` (Poll-Intervall); Abschlussüberwachung über `ScmAbschlussErkanntUtc` + konfigurierte Wartezeit (`GetScmTaskCompletionWaitMinutesAsync`, Z. 45), danach `EntwicklungsprozessService.AbschliessenAsync` (Z. 138). **Setzt kein `PausiertBisUtc`** — die vom Kunden beschriebene „konfigurierte Wartezeit" vor `Beendet` ist dieser Mechanismus, nicht die Pause.
