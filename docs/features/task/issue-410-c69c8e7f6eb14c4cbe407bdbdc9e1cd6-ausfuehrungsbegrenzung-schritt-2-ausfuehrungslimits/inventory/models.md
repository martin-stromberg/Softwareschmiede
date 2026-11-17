# Datenmodell und Einstellungsschlüssel

## `Aufgabe`
Datei: `src/Softwareschmiede/Domain/Entities/Aufgabe.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Status` | `AufgabeStatus` | Fachlicher Lebenszyklus: Neu/Gestartet/Wartend/Beendet/Archiviert (`src/Softwareschmiede/Domain/Enums/AufgabeStatus.cs`). Beendet/Archiviert → permanente `BlockReason`-Sperre. |
| `AusfuehrungsStatus` | `AufgabeAusfuehrungsStatus` | Lauf-Lebenszyklus: NichtGestartet/Aktiv/Beendet (`AufgabeAusfuehrungsStatus.cs`). `Beendet` = letzter Lauf beendet; gesetzt von `AufgabeService.AktivenLaufBeendenAsync`. |
| `PausiertBisUtc` | `DateTimeOffset?` | Pausendziel; `> now` blockiert via `BlockReason`, gesetzt-abgelaufen = Fortsetzungskandidat (Priorität 1). Wird u. a. von `ScmEventStore.PauseForDelayAsync`/`SetPauseAsync`/`KiPluginLimitService.VerarbeiteRateLimitAsync` gesetzt (Max-Semantik, niemals verkürzend). |
| `KiPluginPrefix` | `string?` | Gespeicherte/genommene CLI-Zuordnung; nach erfolgreichem Start der tatsächlich verwendete Prefix (Zurückschreiben in `EntwicklungsprozessService`, Z. 183-188/288-293). Basis der Plugin-Attribution. |
| `ProjektId` | `Guid` | Projektkontext für die Plugin-Auflösung (`ResolveEnabledDevelopmentAutomationPluginWithProjectScopeAsync`). |
| `LokalerKlonPfad` | `string?` | Steuert im Orchestrator `ProzessStartenUndCliStartenAsync` (null) vs. `CliNeustartenAsync`. |

## `ScmIncomingEvent`
Datei: `src/Softwareschmiede/Domain/Entities/ScmIncomingEvent.cs` (Feldbelege über Abfragen in `ScmEventMonitoringService`/`ScmEventOrchestrator`)

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Sequence` | `long` | Monotone Eingangsreihenfolge (Prioritätsklasse 3, Tie-Break). |
| `Status` | `string` | „Aufgenommen", „Verzögert", „Wartend", „ZustellungVorbereitet", „Zugestellt", „InBearbeitung", „ZustellungUngewiss", „VerarbeitungUngeklärt", „Verarbeitet", „Abgebrochen". „Verzögert" wird ausschließlich vom `ScmDelayDirective`-Mechanismus gesetzt → Markierung für Prioritätsklasse 2. |
| `Reason` | `string?` | Wartegrund, in der Ereignisübersicht sichtbar. |
| `NextAttemptUtc` | `long?` | Frühester Wiederversuch (ms); `long.MaxValue` = harte Sperre; Verzögerungsziel für Klasse-2-Sortierung. |
| `DeliveryAttemptId` | `Guid?` | Gesetzt ⇒ begonnener Zustellversuch → Reconcile-Pfad statt Neustart (unbegrenzt). |
| `AufgabeId` | `Guid` | Pflichtfeld; Serialisierung über `ScmTaskCoordinator`-Lease. |
| `CliRunId`, `ExchangePath`, `Attempts`, `ErrorCode`, `DeliveredUtc`, `AcceptedUtc`, `ReadyConfirmedRunId`, `ExchangeRestrictedWriteSidsJson` | diverse | Zustell-/Reconcile-Zustand, vom Limit unberührt. |

## `CliProcessHandle`
Datei: `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs` (innen)

- Kein `PluginPrefix`. Pro Aufgabe genau ein Handle (`_handles` ConcurrentDictionary). Attribution daher indirekt über Aufgabe/Sitzung.

## `AppEinstellung` / Schlüssel
Datei: `src/Softwareschmiede/Application/Services/AppEinstellungService.cs`

Bestehende Muster: `window.*`, `ui.designmode.name`, `ki.plugin.default`, `scm.plugin.default`, `scm.monitoring.*`, `scm.trigger.keyword`, `scm.delay.keyword`, `scm.prompt.append.*`, `scm.taskcompletion.*`, `scm.backmerge.*`, `plugins.enabled.<prefix>` (`PluginActivationService`), `plugins.default.<PluginType>` (+`.project.<id>.*`, `PluginDefaultSettingsService`), `plugins.ide.order`, `plugins.backmerge.project.<id>`, `plugins.sessionlimit.<prefix>` (**belegt**: `KiPluginLimitService.SessionLimitKeyPrefix` → Rate-Limit-Reset-Zeitpunkt als ISO-"O"-String, kein Int!), `autoshutdown.*`, `aktiveaufgaben.*`, `updates.*`.

| Geplanter Schlüssel | Status |
|---------------------|--------|
| `plugins.sessionlimit.<prefix>` für Parallel-Limit | **Verworfen** — bereits belegt (Rate-Limit-Reset). |
| Global-Limit | Neuer Key nötig (Vorschlag: `scm.execution.maxparallel`). |
| Plugin-Limit | Neues Präfix nötig (Vorschlag: `plugins.maxparallel.<prefix>`). |

## `ScmObservationCheckpoint`, `ScmOutgoingAction`, `ScmContentSnapshot`
Vom Limit unberührt — Outbox-Aktionen (`Purpose` „receipt"/"waiting"/"attachments"/"ai:*") laufen unbegrenzt weiter.
