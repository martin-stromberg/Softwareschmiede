← [Zurück zur Übersicht](index.md)

# Aufgaben — API

## Übersicht

Die Task-Detail-Ansicht exponiert öffentliche Service-Schnittstellen für die Verwaltung von Aufgaben, zeitgesteuerten Prompt-Versänden, Aufgaben-Pausen und dem Export der CLI-Rohausgabe:

- `WorkItemService` — Persistente Datenbankoperationen für Aufgaben, einschließlich `SetPauseAsync` und der Laufdaten-Operationen
- `AiExecutionService` / `IRunningAutomationStatusSource` — Live-Laufquelle und `CliRunActive`-Signal für die Status-Synchronisation
- `AiPluginLimitService` — Persistenz und Anwendung von KI-Plugin-Session-Limits
- `PromptScheduleDispatchService` — Laufzeit-Verwaltung zeitgesteuerter Prompts (inkl. Persistenz, Pausen-Verschiebung und Nachzustellung)
- `ScheduledPromptStore` — Scoped Service für den EF-Zugriff auf persistierte geplante Prompts
- `LogService` — Persistente Protokolleinträge, einschließlich automatischer CLI-Ausgaben und Rate-Limit-Marker
- `ICliRawExportService` — Exportiert die gespeicherte CLI-Rohausgabe einer Aufgabe als `.raw`
- `IDialogService` — UI-Abstraktion für Dialoge (Speichern-Dialog, Pause-Dialog)
- `WorkItemListModeService` / `WorkItemListModeBinding` — Persistenz und Zustand des Anzeigemodus der aktiven Aufgabenliste je Ansicht

## LogService — CLI-Ausgabeprotokoll

### `AddCliOutputAsync(Guid workItemId, string outputLine, CancellationToken ct = default) : Task`

**Beschreibung:** Speichert eine einzelne CLI-Ausgabezeile als Protokolleintrag vom Typ `LogEntryType.CliOutput`.

**Parameter:**

| Name | Typ | Beschreibung |
|------|-----|--------------|
| `workItemId` | `Guid` | ID der Aufgabe, deren CLI die Ausgabe erzeugt hat. |
| `outputLine` | `string` | Dekodierte Ausgabezeile aus dem Terminal-Output. |
| `ct` | `CancellationToken` | Optionales Abbruch-Token. |

**Verhalten:**

- Erstellt einen `LogEntry` mit `Typ = LogEntryType.CliOutput`.
- Wird im interaktiven Terminal-Pfad (ConPTY oder Pipe-Fallback) automatisch durch `CliOutputLogWriter` aufgerufen.
- Erkennt Rate-Limit-Marker in der Ausgabezeile und speichert dann zusätzlich einen `LogEntryType.RateLimit`-Eintrag (`"Rate-Limit erkannt. Weiter ab: …"` bzw. `"(kein Zeitstempel)"`).
- Speichert pro Aufruf eine Zeile.
- Der Aufrufer `CliOutputLogWriter` reicht Marker mit gültigem Zeitstempel anschließend an `AiPluginLimitService.ProcessRateLimitAsync` weiter.

### `TryParseRateLimitMarker(string outputLine, out DateTimeOffset? resetUtc) : bool` (statisch)

**Beschreibung:** Parst den Session-Limit-Marker `[[SOFTWARESCHMIEDE_RATE_LIMIT:<ISO8601>]]` aus einer CLI-Ausgabezeile.

**Verhalten:**

- Gibt `true` zurück, wenn das Marker-Präfix und die schließende `]]`-Klammer gefunden werden — auch ohne gültigen Zeitstempel (`resetUtc = null`).
- Der Marker darf auch eingebettet in einer längeren Zeile vorkommen.
- Ein gültiger ISO-8601-Zeitstempel wird als `DateTimeOffset` in `resetUtc` geliefert.

### `ParseRateLimitMarker(string outputLine) : (bool Found, string? Prompt, DateTimeOffset? ResetUtc)` (statisch)

**Beschreibung:** Kompatibilitäts-Wrapper um `TryParseRateLimitMarker`; liefert ein Tuple. `Prompt` ist aktuell immer `null`.

## AufgabeService — Pause

### `SetPauseAsync(Guid workItemId, DateTimeOffset? pausedUntilUtc, CancellationToken ct = default) : Task`

**Beschreibung:** Setzt oder leert den Pausen-Endzeitpunkt (`WorkItem.PausedUntilUtc`) einer regulären Aufgabe.

**Parameter:**

| Name | Typ | Beschreibung |
|------|-----|--------------|
| `workItemId` | `Guid` | ID der Aufgabe. |
| `pausedUntilUtc` | `DateTimeOffset?` | UTC-Zeitpunkt, bis zu dem pausiert wird; `null` hebt eine bestehende Pause auf. |
| `ct` | `CancellationToken` | Optionales Abbruch-Token. |

**Verhalten:**

- Erlaubte Stati: `New`, `Started`, `Waiting` — andere Stati werden mit `InvalidOperationException` abgelehnt.
- Beim Setzen muss `pausedUntilUtc` in der Zukunft liegen; andernfalls `InvalidOperationException`.
- Schreibt pro Aufruf einen `LogEntryType.SystemMessage`-Eintrag („Aufgabe pausiert bis …" / „Pause aufgehoben").
- Fordert nach dem Speichern die ereignisgesteuerte SCM-Startprüfung an (`ScmStartCheckTrigger.RequestStartCheck`), sofern der optionale Auslöser injiziert ist — ein durch die Pause frei werdender Ausführungsplatz wird ohne Poll-Durchlauf nachgefüllt.
- Wirft `InvalidOperationException`, wenn die Aufgabe nicht gefunden wird.

## KiAusfuehrungsStatusKlassifikation — Pausenkriterium

### `IsCurrentlyPaused(this DateTimeOffset? pausedUntilUtc, DateTimeOffset now) : bool` (Extension, statisch)

**Beschreibung:** Referenzdefinition des Pausenkriteriums für alle Pausen-Auswertungen der Anwendung.

**Parameter:**

| Name | Typ | Beschreibung |
|------|-----|--------------|
| `pausedUntilUtc` | `DateTimeOffset?` | UTC-Pausenende der Aufgabe (`WorkItem.PausedUntilUtc`). |
| `now` | `DateTimeOffset` | Der als „jetzt" zu verwendende UTC-Zeitpunkt. |

**Rückgabe:** `true`, wenn `pausedUntilUtc` gesetzt ist und strikt in der Zukunft liegt (`pausedUntilUtc > now`); `null` oder ein abgelaufener bzw. exakt aktueller Zeitpunkt gelten als nicht pausiert.

**Verhalten:**

- Kapselt den früher an mehreren Stellen duplizierten Direktvergleich, damit das Pausenkriterium nicht divergiert.
- Verwendet u. a. von `Classify` (Kachel-Zustand `AiExecutionState.Paused`), `ScmEventExecutionLimitService.GetOccupancyAsync` (Limit-Belegung), `ScmEventOrchestrator.BlockReason` (Start-Sperrgrund), `WorkItemRecoveryService` und `DevelopmentProcessService.ThrowIfPaused`.

## AufgabeService — KI-Laufdaten

Alle Laufdaten-Mutationen laufen unter der statischen Sperrung `WorkItemService.RunGates` (`ConcurrentDictionary<Guid, SemaphoreSlim>`): Sie serialisiert prozessweit alle Schreibzugriffe auf `ActiveRunId`/`LastHeartbeatUtc`/`LastCliStartUtc`/`RunStatus`/`ExecutionStatus` einer Aufgabe — einschließlich der laufdatenrücksetzenden Lebenszyklus-Pfade `StartAsync`, `CompleteAsync` und `ResetStartAsync`. Die optionalen Callbacks `runningGuard` und `runStatusProvider` werden grundsätzlich erst innerhalb der gehaltenen Sperre ausgewertet (TOCTOU-/Stale-Write-Schutz).

### `SetActiveRunAsync(Guid id, string laufId, CancellationToken ct = default, Func<bool>? runningGuard = null) : Task`

**Beschreibung:** Setzt den vollständigen Lauf-Zustand für einen gestarteten CLI-Prozess (`ExecutionStatus = Active`, `ActiveRunId = laufId`, `LastHeartbeatUtc`, `LastCliStartUtc`, `RunStatus = Running`).

**Verhalten:**

- Liefert `runningGuard` innerhalb der Sperre `false` (Prozess zwischenzeitlich beendet): `LogDebug`, kein Schreibzugriff — kein Phantom-Lauf nach `EndActiveRunAsync`.
- Terminale Aufgaben (`Completed`/`Archived`) oder unbekannte ID: `InvalidOperationException`.

### `EnsureActiveRunAsync(Guid id, Func<WorkItemRunStatus?>? runStatusProvider = null, CancellationToken ct = default, Func<bool>? runningGuard = null) : Task`

**Beschreibung:** Stellt die Laufdaten eines laufenden CLI-Laufs idempotent sicher: Fehlt `ActiveRunId`, wird der vollständige Lauf-Zustand geschrieben (`ExecutionStatus = Active`, neue `ActiveRunId`, `LastHeartbeatUtc`, `RunStatus = Provider-Wert ?? Running`; ein bekannter `LastCliStartUtc` bleibt erhalten — Sortierstabilität). Bei bestehendem Lauf wird nur `LastHeartbeatUtc` aufgefrischt und `RunStatus` nur bei geliefertem Wert überschrieben. Zentraler Persistenzpunkt aller Selbstheilungspfade des `CliProcessManager` (`CliRunActive`, `RuntimeStatusChanged`, Heartbeat-Tick, periodischer Abgleich) sowie des Wiederverwendungspfads in `TaskDetailViewModel.StartCliAndUpdateStateAsync`.

**Verhalten:**

- `runningGuard` liefert innerhalb der Sperre `false`: `LogDebug`, kein Schreibzugriff.
- `Status` `Completed`/`Archived`: `LogDebug`, Rückkehr ohne Wurf — der periodische Abgleich erzeugt keine Warnungsflut.
- Unbekannte Aufgabe: `InvalidOperationException` (konsistent zu `SetActiveRunAsync`).

### `EndActiveRunAsync(Guid id, CancellationToken ct = default) : Task`

**Beschreibung:** Entfernt die Laufdaten (`ActiveRunId`, `LastHeartbeatUtc`, `LastCliStartUtc`, `RunStatus`) und setzt `ExecutionStatus = Completed` — serialisiert unter `RunGates`, sodass ein gleichzeitig anstehendes Setzen/Sicherstellen entweder davor (dann greift dessen Guard nicht mehr) oder danach (Beenden hat das letzte Wort) serialisiert.

## AiExecutionService / IRunningAutomationStatusSource — Live-Laufquelle

### `WorkItemRunStatus? GetLiveRunStatus(Guid workItemId)` (Interface-Methode)

**Beschreibung:** Live-Substatus aus der Laufzeitquelle: `null`, wenn kein gehandelter Prozess läuft (`IsRunning` `false`); `WaitingForInput`, wenn die Session `CliRuntimeStatus.WaitingForInput` meldet; sonst `Running` — auch bei laufendem Handle ohne Session oder `CliRuntimeStatus.Inactive`. Datenquelle für `ActiveWorkItemPanelItem.LiveRunStatus`; der Live-Zweig der Kachel-Klassifikation darf bei laufendem Prozess damit nie auf `Ready` zurückfallen.

### `IReadOnlyCollection<Guid> GetRunningWorkItemIds()`

**Beschreibung:** IDs aller Aufgaben mit laufendem Handle (`!HasExited`, Exception-tolerant wie `GetRunningCount`) — Datenquelle des periodischen Abgleichs im `CliProcessManager`.

### `void NotifyRunActive(Guid workItemId)`

**Beschreibung:** Feuert `CliRunActive`, sofern `IsRunning(workItemId)` zutrifft — sonst No-Op. Für Aufrufer außerhalb des Service, die einen laufenden Prozess bestätigt haben (produktiv: `DevelopmentProcessService.RestartCliCoordinatedAsync` im Session-Alive-Early-Return).

### `event Action<Guid>? CliRunActive`

**Beschreibung:** Signal, dass eine Startanforderung einen bereits laufenden Prozess zurückgibt (Early-Return „zweiter Start abgewiesen" in `StartCliCoreAsync`/`StartTerminalSessionCoreAsync` — dort feuert kein `CliProcessStatus.Started`) bzw. ein laufender Prozess über `NotifyRunActive` bestätigt wurde. Einziger produktiver Abonnent: `CliProcessManager.OnCliRunActive` → `EnsureRunDataAsync`. Vertrag wie `CliProcessStatusChanged`: Der Invoke läuft unter der klassenweiten Start-Sperre (`_startLock`) — Abonnenten müssen non-blocking sein und dürfen nicht reentrant `StartCliAsync`/`StartTerminalSessionAsync` aufrufen.

## AiPluginLimitService — Plugin-Session-Limits

**Registrierung:** Scoped in DI; Abhängigkeiten: `SoftwareschmiedeDbContext`, `AppSettingService`, `WorkItemRunDataChangedNotifier`.

**Konstante:** `AiPluginLimitService.SessionLimitKeyPrefix = "plugins.sessionlimit."` — Schlüsselpräfix für persistierte Reset-Zeitpunkte in `AppSetting`.

### `ProcessRateLimitAsync(Guid workItemId, DateTimeOffset resetUtc, CancellationToken ct = default) : Task`

**Beschreibung:** Verarbeitet einen erkannten Session-Limit-Marker: persistiert den Reset-Zeitpunkt unter `plugins.sessionlimit.<AiPluginPrefix>` der auslösenden Aufgabe und pausiert bei zukünftigem Zeitpunkt alle aktiv laufenden regulären Aufgaben mit demselben Prefix.

**Parameter:**

| Name | Typ | Beschreibung |
|------|-----|--------------|
| `workItemId` | `Guid` | ID der Aufgabe, in deren CLI-Ausgabe der Marker erkannt wurde. |
| `resetUtc` | `DateTimeOffset` | Gemeldeter Reset-Zeitpunkt des Session-Limits. |
| `ct` | `CancellationToken` | Optionales Abbruch-Token. |

**Verhalten:**

- Aufgabe nicht gefunden oder `AiPluginPrefix` leer: `LogWarning`, keine Persistenz, keine Pause.
- Persistiert `resetUtc` (normalisiert auf UTC) als ISO-8601-Roundtrip-String (`"O"`) in `AppSetting`.
- Liegt `resetUtc` in der Vergangenheit: Persistenz erfolgt, aber es wird keine Pause gesetzt.
- Kandidaten: Aufgaben mit `Status.IsActiveOrWaiting()`, `ExecutionStatus == Active`, Prefix-Vergleich `OrdinalIgnoreCase`.
- Max-Semantik: `PausedUntilUtc` wird auf `max(bisheriger Wert, resetUtc)` gesetzt — längere manuelle Pausen werden nicht verkürzt.
- Pro pausierter Aufgabe wird ein `LogEntryType.SystemMessage`-Eintrag geschrieben; nach dem Speichern wird `WorkItemRunDataChangedNotifier.NotifyRunDataChanged` je Aufgabe ausgelöst.
- Laufende CLI-Prozesse werden nicht angefasst.

### `GetActiveSessionLimitsAsync(IReadOnlyCollection<string> prefixes, CancellationToken ct = default) : Task<IReadOnlyDictionary<string, DateTimeOffset>>`

**Beschreibung:** Liest die persistierten Session-Limits mehrerer Plugin-Prefixe in einer Abfrage (Batch-Lookup, z. B. für `CliUpdateSafetyService`).

**Verhalten:**

- Übersetzt jedes Prefix auf den Schlüssel `plugins.sessionlimit.<Prefix>` und lädt die Werte über `AppSettingService.GetSettingsAsync`.
- Gibt nur zukünftige, gültig geparste (`DateTimeStyles.AssumeUniversal | AdjustToUniversal`) Zeitpunkte zurück; abgelaufene oder ungültige Werte werden ignoriert.
- Das zurückgegebene Dictionary verwendet `OrdinalIgnoreCase`-Schlüsselvergleich.
- Leere Prefix-Liste oder ausschließlich leere Prefixe: leeres Dictionary.

## PromptScheduleDispatchService — Zeitgesteuerte Prompt-Versendung

### Übersicht

`PromptScheduleDispatchService` ist ein Singleton-Service, der zeitgesteuerte Prompts pro Aufgabe in einer Laufzeit-Warteschlange verwaltet. Geplante Prompts werden zusätzlich über den scoped `ScheduledPromptStore` in der Tabelle `ZeitgesteuertePrompts` persistiert, beim App-Start via `RestoreScheduledPromptsAsync` wiederhergestellt und — wenn zum Fälligkeitszeitpunkt keine Session zustellfähig ist — beim nächsten CLI-Start der Aufgabe nachgestellt (`CliProcessStatusChanged` → `Started`). Der Service ist registriert in der Dependency Injection und wird vom `TaskDetailViewModel` genutzt.

### Öffentliche Methoden

#### `SchedulePromptAsync(Guid workItemId, string promptText, DateTimeOffset targetTime) : Task`

**Beschreibung:** Plant den Versand eines Prompts zur angegebenen Zielzeit.

**Parameter:**

| Name | Typ | Beschreibung |
|------|-----|--------------|
| `workItemId` | `Guid` | ID der Aufgabe, deren aktive CLI-Session den Prompt erhalten soll. |
| `promptText` | `string` | Der bereits aufgelöste Prompttext (Platzhalter müssen vor Aufruf ersetzt sein). |
| `targetTime` | `DateTimeOffset` | Der Zeitpunkt (in UTC), zu dem der Prompt versendet werden soll. |

**Verhalten:**

- Liegt `targetTime` in der Vergangenheit oder Gegenwart: Prompt wird sofort an die Session geschrieben (kein Eintrag in der Warteschlange, kein persistierter Datensatz). Schlägt auch der Sofortversand fehl, geht der Plan in „wartet auf Session" über und wird persistiert.
- Liegt `targetTime` in der Zukunft: `ScheduledPromptStore.UpsertAsync` schreibt zuerst den Datensatz (DB vor Timer-Armierung); danach wird ein `ScheduledPromptInfo`-Eintrag im internen Dictionary abgelegt und ein `ITimer` mit der Restlaufzeit gestartet.
- **Pausen-Berücksichtigung:** Über die optionale `IServiceScopeFactory` wird der aktuelle `WorkItem.PausedUntilUtc`-Wert geladen. Endet eine aktive Pause später als `targetTime`, wird die Zielzeit auf das Pausenende verschoben statt verworfen — sowohl bei der Planung als auch erneut im Timer-Callback (`HandleTimerElapsedAsync`), falls die Pause nachträglich gesetzt wurde. Ohne registrierte Scope-Factory (z. B. Unit-Tests) entfallen die Prüfung und die Persistenz.
- Ein bereits geplanter Prompt für dieselbe `workItemId` wird ersetzt (Timer wird abgebrochen, Datensatz per Upsert überschrieben).
- Nach dem Eintragen wird `ScheduledPromptChanged` ausgelöst.

**Rückgabe:** `Task` — abwartet die Planung (nicht den tatsächlichen Versand).

**Fehlerbehandlung:**

- Exceptions beim Schreiben auf die Session werden geloggt und überführen den Plan in „wartet auf Session" (kein Verwerfen). Fehler beim Persistieren werden geloggt; der Plan bleibt dann sitzungsgebunden.

**Beispiel:**

```csharp
var targetTime = DateTimeOffset.Now.AddHours(2); // in 2 Stunden
await _promptScheduleDispatchService.SchedulePromptAsync(
    workItemId: workItemId,
    promptText: "Analysiere den Bug in LoginService",
    targetTime: targetTime);
```

---

#### `CancelScheduledPromptAsync(Guid workItemId) : Task`

**Beschreibung:** Bricht einen für die Aufgabe geplanten Prompt-Versand ab, falls vorhanden — entfernt den In-Memory-Eintrag und den persistierten Datensatz.

**Parameter:**

| Name | Typ | Beschreibung |
|------|-----|--------------|
| `workItemId` | `Guid` | ID der Aufgabe, deren geplanter Prompt storniert werden soll. |

**Verhalten:**

- Falls ein Prompt für `workItemId` geplant ist: Eintrag wird aus der Warteschlange entfernt, Timer wird abgebrochen und disposed.
- Der persistierte `ScheduledPrompt`-Datensatz wird via `ScheduledPromptStore.DeleteAsync` gelöscht.
- `ScheduledPromptChanged` wird ausgelöst.
- Falls kein Prompt geplant ist: Keine Aktion (keine Exception).

**Rückgabe:** `Task` — abwartet die Stornierung einschließlich Datensatz-Löschung.

**Beispiel:**

```csharp
await _promptScheduleDispatchService.CancelScheduledPromptAsync(workItemId);
```

---

#### `RestoreScheduledPromptsAsync() : Task`

**Beschreibung:** Stellt persistierte geplante Prompts beim App-Start wieder her; wird in `App.OnStartup` nach `db.Database.MigrateAsync()` aufgerufen.

**Verhalten:**

- Lädt alle `ScheduledPrompt`-Datensätze via `ScheduledPromptStore.GetAllAsync`.
- Aufgabe nicht mehr vorhanden → Datensatz wird defensiv gelöscht.
- Aktive Pause jenseits der Zielzeit → Zielzeit wird auf das Pausenende vorgeladen und der Datensatz via `UpdateTargetTimeAsync` aktualisiert.
- Zielzeit in Zukunft → In-Memory-Eintrag + armiertem Timer.
- Zielzeit verstrichen → Eintrag im Zustand „wartet auf Session" (kein Timer); Zustellung beim nächsten `CliProcessStatusChanged` → `Started`.
- Fehler werden pro Datensatz isoliert geloggt; ohne `IServiceScopeFactory` ist die Methode ein No-Op.

---

#### `GetScheduledPromptStatus(Guid workItemId) : ScheduledPromptInfo?`

**Beschreibung:** Gibt Informationen zum aktuell für die Aufgabe geplanten Prompt zurück, oder `null` wenn keiner geplant ist.

**Parameter:**

| Name | Typ | Beschreibung |
|------|-----|--------------|
| `workItemId` | `Guid` | ID der Aufgabe. |

**Rückgabe:**

| Typ | Beschreibung |
|-----|--------------|
| `ScheduledPromptInfo?` | Objekt mit `WorkItemId`, `PromptText`, `TargetTime` und `WaitingForSession`, oder `null` wenn kein Prompt geplant ist. |

**Beispiel:**

```csharp
var scheduled = _promptScheduleDispatchService.GetScheduledPromptStatus(workItemId);
if (scheduled is not null)
{
    Console.WriteLine($"Prompt geplant für {scheduled.TargetTime:HH:mm}");
}
```

---

### Öffentliche Events

#### `PromptSent : event Action<Guid>?`

**Beschreibung:** Wird ausgelöst, nachdem ein zeitgesteuerter Prompt erfolgreich an die CLI-Session versendet wurde.

**Parameter:** `Guid workItemId` — ID der Aufgabe, deren Prompt versendet wurde.

**Verhalten:**

- Dieses Event wird ausgelöst **nach** `PseudoConsoleSession.WritePromptAsync()` erfolgreich abgeschlossen hat — sowohl beim Timer-/Sofortversand als auch bei der Nachzustellung nach einem CLI-Start.
- Ist die Session zur Zielzeit nicht zustellfähig oder schlägt der Versand fehl, wird dieses Event **nicht** ausgelöst; der Plan geht in „wartet auf Session" über und bleibt persistiert.
- Die Anzahl der Abonnenten steuert, ob ein `SystemMessage`-Protokolleintrag geschrieben wird: ohne Abonnenten (keine geöffnete Detailansicht) wird nach Timer-/Sofortversand ein Eintrag geschrieben.
- Das Event wird auf dem Thread-Pool-Thread des Timer-Callbacks bzw. des Session-Start-Handlers ausgelöst, **nicht** auf dem UI-Thread.

**Beispiel:**

```csharp
_promptScheduleDispatchService.PromptSent += (workItemId) =>
{
    Console.WriteLine($"Prompt für Aufgabe {workItemId} versendet");
};
```

---

#### `ScheduledPromptChanged : event Action<Guid>?`

**Beschreibung:** Wird ausgelöst, wenn sich der Wartestellungs-Zustand eines geplanten Prompts ändert — beim Planen, beim Storno, nach erfolgreichem Versand und beim Übergang in „wartet auf Session".

**Parameter:** `Guid workItemId` — ID der Aufgabe, deren Plan-Zustand sich geändert hat.

**Verhalten:**

- Signal für Beobachter, `GetScheduledPromptStatus` neu zu lesen (z. B. `TaskDetailViewModel.OnScheduledPromptChanged` → `UpdateQueueDisplay`).
- Trägt externe Stornos (Aufgabenabschluss, Ersetzung) und den „wartet auf Session"-Übergang in die UI.

---

### ScheduledPromptInfo — Datencontainer

**Typ:** `sealed record`

**Beschreibung:** Unveränderliches Value-Object zur Verwaltung geplanter Prompts.

**Eigenschaften:**

| Name | Typ | Beschreibung |
|------|-----|--------------|
| `WorkItemId` | `Guid` | ID der Aufgabe, deren Session den Prompt erhalten wird. |
| `PromptText` | `string` | Der bereits aufgelöste Prompttext. |
| `TargetTime` | `DateTimeOffset` | Der Zeitpunkt, zu dem der Prompt versendet werden soll. |
| `WaitingForSession` | `bool` | `true`, wenn die Zielzeit bereits verstrichen ist, der Versand aber mangels zustellfähiger CLI-Session aussteht — der Plan bleibt gültig und wird beim nächsten CLI-Start zugestellt. |

**Beispiel:**

```csharp
var info = new ScheduledPromptInfo(
    workItemId: workItemId,
    promptText: "Analysiere den Bug",
    targetTime: DateTimeOffset.Now.AddHours(1));
```

---

## ScheduledPromptStore — Persistenz geplanter Prompts

**Registrierung:** Scoped in DI; Abhängigkeiten: `SoftwareschmiedeDbContext`. Wird vom Singleton `PromptScheduleDispatchService` ausschließlich über einen eigenen Scope (`IServiceScopeFactory` → `WithStoreAsync`) aufgelöst — Fehler beim Store-Zugriff werden geloggt und brechen den Versandpfad nicht.

### `GetAllAsync(CancellationToken ct = default) : Task<IReadOnlyList<ScheduledPrompt>>`

**Beschreibung:** Liefert alle persistierten geplanten Prompts (`AsNoTracking`); Grundlage für `RestoreScheduledPromptsAsync`.

### `UpsertAsync(Guid workItemId, string promptText, DateTimeOffset targetTimeUtc, CancellationToken ct = default) : Task`

**Beschreibung:** Schreibt den geplanten Prompt der Aufgabe oder ersetzt einen vorhandenen Datensatz (Ersetzungsregel — pro Aufgabe maximal ein Plan, datenbankseitig über Unique-Index auf `WorkItemId` abgesichert). Aktualisiert bei Ersetzung `PromptText`, `ZielZeitpunktUtc` und `AktualisiertAmUtc`.

### `UpsertIfMatchingAsync(Guid workItemId, string promptText, DateTimeOffset targetTimeUtc, ScheduledPromptInfo? erwartet, CancellationToken ct = default) : Task<bool>`

**Beschreibung:** Schreibt den Plan nur, wenn der vorhandene Datensatz noch dem expecteden Vorgängerstand entspricht (`PromptText` + `ZielZeitpunktUtc` auf Unix-Millisekunden normiert) bzw. kein Datensatz existiert und keiner expected wird — Lost-Update-Schutz für den Sofortversand-Fehlerpfad. Gibt `false` zurück, wenn ein abweichender (neuerer) Datensatz den Schreibzugriff verweigert.

### `DeleteAsync(Guid workItemId, CancellationToken ct = default) : Task`

**Beschreibung:** Entfernt den persistierten Plan der Aufgabe, falls vorhanden (Storno).

### `DeleteAfterDispatchAsync(Guid workItemId, string promptText, DateTimeOffset targetTimeUtc, bool newerScheduleActive, CancellationToken ct = default) : Task`

**Beschreibung:** Entfernt den Datensatz nach erfolgreichem Versand. Entspricht der Datensatz dem versendeten Plan, wird er immer gelöscht; weicht er ab, bleibt er nur bei `newerScheduleActive == true` erhalten (Lost-Update-Schutz), sonst wird er als Leiche des versendeten Plans mitgelöscht.

### `UpdateTargetTimeAsync(Guid workItemId, DateTimeOffset targetTimeUtc, CancellationToken ct = default) : Task`

**Beschreibung:** Aktualisiert `ZielZeitpunktUtc` (und `AktualisiertAmUtc`) des persistierten Plans, z. B. bei Pausenverschiebung; No-Op, wenn kein Datensatz existiert.

---

## TaskDetailViewModel — Zeitgesteuerte Prompt-Integration

### Neue Properties

#### `ScheduledPromptTargetHours : int?`

**Bindung:** TextBox für Stunden-Eingabe (0–23).

**Typ:** `int?` (null = Feld leer)

**Two-Way-Binding:** `UpdateSourceTrigger=PropertyChanged`

---

#### `ScheduledPromptTargetMinutes : int?`

**Bindung:** TextBox für Minuten-Eingabe (0–59).

**Typ:** `int?` (null = Feld leer)

**Two-Way-Binding:** `UpdateSourceTrigger=PropertyChanged`

---

#### `ScheduledPromptStatus : string?`

**Bindung:** TextBlock zur Anzeige des Status während der Wartestellung.

**Wert:** `"Prompt in Wartestellung"` während ein Prompt geplant ist, `"Prompt wartet auf CLI-Start"` wenn die Zielzeit verstrichen ist, aber noch keine zustellfähige CLI-Session existiert, oder `null`.

**Sichtbarkeit:** TextBlock (und der „Wartestellung aufheben"-Button) sind nur sichtbar, wenn dieser Wert nicht `null` ist (via `NullOrEmptyToVisibilityConverter`).

**Befüllung:** Zentral über `UpdateQueueDisplay()` aus `GetScheduledPromptStatus` — beim Laden der Ansicht (`LoadAsync`, auch für Pläne aus früheren Sitzungen/App-Läufen) sowie nach `ScheduledPromptChanged`/`PromptSent`.

---

#### `ScheduledPromptTimeDisplay : string?`

**Bindung:** TextBlock zur Anzeige der Zielzeit.

**Wert:** Zielzeit im Format `HH:mm` (lokal), oder `null` — im Zustand „Prompt wartet auf CLI-Start" bewusst leer (keine vergangene Uhrzeit).

---

#### `CanSchedulePrompt : bool`

**Bedingung:** `true` wenn alle diese Bedingungen erfüllt sind:
- CLI läuft (`IsCliRunning == true`)
- Eine Promptvorlage ist ausgewählt (`SelectedPromptVorlage != null`)
- Die Vorlage hat einen nicht-leeren Prompttext
- Mindestens eines der Zeitfelder (`ScheduledPromptTargetHours` oder `ScheduledPromptTargetMinutes`) ist gesetzt

---

### Neue Commands

#### `SchedulePromptCommand : ICommand`

**Typ:** `AsyncRelayCommand`

**CanExecute:** `CanSchedulePrompt` (Button ist disabled wenn `false`)

**Ausgeführte Aktion:** `SchedulePromptAsync()` (private Methode)
- Validiert Zeitfelder
- Berechnet `targetTime`
- Ruft `PromptScheduleDispatchService.SchedulePromptAsync()` auf
- Aktualisiert `ScheduledPromptStatus` und `ScheduledPromptTimeDisplay` via `UpdateQueueDisplay()`
- Leert Zeitfelder und Vorlage-Auswahl

---

#### `CancelScheduledPromptCommand : ICommand`

**Typ:** `AsyncRelayCommand`

**Bindung:** Button „Wartestellung aufheben" (`AutomationProperties.Name="ReleaseWaiting"`) im Status-`StackPanel`; sichtbar nur solange `ScheduledPromptStatus` gesetzt ist.

**Ausgeführte Aktion:** `PromptScheduleDispatchService.CancelScheduledPromptAsync(_workItemId)` — entfernt Plan und Datensatz; die Statusanzeige wird anschließend über `ScheduledPromptChanged` → `UpdateQueueDisplay` geleert.

---

## Fehlerbehandlung (PromptScheduleDispatchService)

| Fehler | Verhalten |
|--------|-----------|
| Session nicht gefunden bei Fälligkeit | Log-Warnung; Plan + Datensatz bleiben erhalten und gehen in „wartet auf Session" über (Status „Prompt wartet auf CLI-Start"); Zustellung beim nächsten `CliProcessStatusChanged` → `Started`; **kein** `PromptSent`-Event |
| Session disposed zwischen Planung und Versand | `ObjectDisposedException` wird geloggt; gleiche Behandlung — „wartet auf Session" |
| Schreiboperation auf InputStream schlägt fehl | Exception wird geloggt; Plan + Datensatz bleiben erhalten („wartet auf Session"); erneuter Versuch beim nächsten Session-Start; `PromptSent`-Event wird **nicht** ausgelöst |
| Fehler beim Persistieren des Plans | Store-Ausnahme wird geloggt; der Plan bleibt sitzungsgebunden in der Warteschlange |
| Fehler beim Protokolleintrag (z. B. Aufgabe gelöscht) | Wird geloggt; der erfolgreiche Versand bleibt gültig |
| Ungültige Zeitfelder im ViewModel | `ErrorMessage` wird gesetzt; Service wird nicht aufgerufen; ViewModel-seitige Validierung |
| Aktive Pause bei Planung oder Fälligkeit | Zielzeit/Timer und persistierter `ZielZeitpunktUtc` werden auf das Pausenende verschoben; **kein** `PromptSent`-Event vor dem Pausenende |

---

## CliRawExportService — CLI-Rohausgabe

### `ExportCliRawAsync(Guid workItemId, string targetPath, CancellationToken ct = default) : Task`

**Beschreibung:** Schreibt die protokollierte CLI-Rohausgabe einer Aufgabe in eine `.raw`-Datei.

**Parameter:**

| Name | Typ | Beschreibung |
|------|-----|--------------|
| `workItemId` | `Guid` | ID der Aufgabe, deren CLI-Rohausgabe exportiert werden soll. |
| `targetPath` | `string` | Zielpfad der zu erzeugenden `.raw`-Datei. |
| `ct` | `CancellationToken` | Optionales Abbruch-Token. |

**Verhalten:**

- Lädt die gespeicherten Protokolle über `LogService.GetByWorkItemAsync(...)`.
- Filtert ausschließlich `LogEntryType.CliOutput` und übernimmt nur deren `Inhalt`.
- Verbindet die Zeilen in vorhandener Reihenfolge mit `Environment.NewLine`.
- Schreibt die Datei mit UTF-8 ohne BOM.
- Abbruch und Schreibfehler werden an den Aufrufer weitergereicht.

## CliReplayExportService — CLI-Aufzeichnung (.clireplay)

Exportiert den im `AiExecutionService` gehaltenen Rohbyte-Mitschnitt einer Aufgabe als `.clireplay`-Binärdatei (Interface `ICliReplayExportService`).

### `ExportCliReplayAsync(Guid workItemId, string targetPath, CancellationToken ct = default) : Task`

**Beschreibung:** Schreibt die aufgezeichnete CLI-Ausgabe einer Aufgabe (Header + zeitgestempelte Chunk-Records) über `CliReplayRecordingStore` in die angegebene `.clireplay`-Datei.

**Parameter:**

| Name | Typ | Beschreibung |
|------|-----|--------------|
| `workItemId` | `Guid` | ID der Aufgabe, deren Aufzeichnung exportiert werden soll. |
| `targetPath` | `string` | Zielpfad der zu erzeugenden `.clireplay`-Datei. |
| `ct` | `CancellationToken` | Optionales Abbruch-Token. |

**Exceptions:**

| Exception | Ursache |
|-----------|---------|
| `ArgumentException` | `targetPath` leer. |
| `InvalidOperationException` | „Für diese Aufgabe liegt keine Aufzeichnung vor." — `AiExecutionService.GetCliRecording` liefert `null`. |

### `HasRecording(Guid workItemId) : bool`

**Beschreibung:** Prüft, ob für die Aufgabe ein Mitschnitt vorliegt — Vorab-Prüfung im `TaskDetailViewModel`, bevor der Speicherdialog geöffnet wird.

## AufgabenListModeService — Anzeigemodus-Persistenz

Singleton-Service (`src/Softwareschmiede.App/Services/`), der den Anzeigemodus der aktiven WorkItemsList je Ansicht (Seitenleiste, Dashboard) über `AppSettingService` in eigenen DI-Scopes (`IServiceScopeFactory`) liest und schreibt — Muster `DarkModeService`.

### `LoadMenuModeAsync(CancellationToken ct = default) : Task<WorkItemListMode>`

**Beschreibung:** Lädt den gespeicherten Anzeigemodus der Seitenleisten-WorkItemsList (`AppSettingService.ActiveWorkItemsListModeKey`).

**Rückgabe:** Der gespeicherte `WorkItemListMode` oder `Datum`, wenn kein gültiger Wert gespeichert ist (`Enum.TryParse`, Groß-/Kleinschreibung tolerant, plus `Enum.IsDefined`).

### `LoadDashboardModeAsync(CancellationToken ct = default) : Task<WorkItemListMode>`

**Beschreibung:** Wie `LoadMenuModeAsync`, aber für den Dashboard-Schlüssel (`ActiveWorkItemsListModeDashboardKey`).

### `SaveMenuModeAsync(WorkItemListMode modus, CancellationToken ct = default) : Task`

**Beschreibung:** Speichert den Anzeigemodus der Seitenleisten-WorkItemsList als Enum-Namen (`modus.ToString()`) per `SetSettingAsync`.

### `SaveDashboardModeAsync(WorkItemListMode modus, CancellationToken ct = default) : Task`

**Beschreibung:** Wie `SaveMenuModeAsync`, aber für den Dashboard-Schlüssel.

## AufgabenListModeBindung — Modus-Zustand je Ansicht

Kapselt die gemeinsame Bindungslogik von `MainWindowViewModel` und `DashboardViewModel` für den persistierten Anzeigemodus.

| Member | Typ | Beschreibung |
|--------|-----|--------------|
| `ForMenu(modeService, logger, dispatch)` | statische Factory | Bindung für die Seitenleiste (Menü-Schlüssel); `dispatch` = UI-Thread-Marshalling der Wiederherstellung |
| `FuerDashboard(modeService, logger)` | statische Factory | Bindung für das Dashboard (Dashboard-Schlüssel) |
| `Wert` | `WorkItemListMode` | Aktueller Modus; Setzen löst `ValueChanged` aus, setzt den Race-Guard und persistiert fire-and-forget |
| `ValueChanged` | `event Action` | Benachrichtigung nach Wertänderung und Wiederherstellung (für `OnPropertyChanged` im ViewModel) |
| `LoadSavedValueAsync(ct)` | `Task` | Lädt den persistierten Modus und wendet ihn ohne Write-Back an; ein zwischenzeitlich gesetzter Wert (Race-Guard, geprüft vor und nach dem `await`) wird nicht überschrieben |

## AufgabenListMode / AufgabenListenAnsicht

- `WorkItemListMode` (Enum): `Datum` (flache Liste nach letzter Ausführung), `Repository` (gruppiert nach Repository-Name), `Ki` (gruppiert nach KI).
- `WorkItemListLayout` (statisch): `Konfiguriere(ListCollectionView, WorkItemListMode)` setzt die zum Modus gehörigen `SortDescriptions` und optional eine `PropertyGroupDescription`; `CreateSortDescriptors`/`CreateGroupDescriptor` liefern dieselben Deskriptoren als Daten (unit-testbar). Konstanten `NotExecuted` (`"Nicht ausgeführt"`) und `WithoutRepository` (`"Ohne Repository"`) als Fallback-Gruppennamen.
- `ActiveTasksListControl.ListMode` (Dependency Property): gebunden an `WorkItemListMode` des jeweiligen ViewModels; Änderungen werden über `OnListModeChanged` → `WorkItemListLayout.Configure` auf die instanz-eigene `ListCollectionView` angewendet.
- `IPlugin.PluginSymbol` (`string`, Default-Interface-Member → `IPlugin.FallbackSymbol = "◻"`): Unicode-Glyph des Plugins für die Symbolzeile der Aufgabenkachel; `virtual` an `GitPluginBase<TPlugin>`/`CliAiPluginBase`, von allen Repo-Plugins überschrieben. Binärkompatibel — ältere Plugin-DLLs erhalten den Fallback-Glyph. Details: [Plugin-System — API](../plugins/api.md).

## IDialogService — Dialoge

### `ShowWorkItemPauseDialogAsync(PauseWorkItemDialogViewModel viewModel, CancellationToken ct = default) : Task<PauseWorkItemResult?>`

**Beschreibung:** Zeigt den modalen Dialog **Pause einstellen** zum Setzen oder Aufheben einer Aufgaben-Pause.

**Parameter:**

| Name | Typ | Beschreibung |
|------|-----|--------------|
| `viewModel` | `PauseWorkItemDialogViewModel` | Vorbefülltes Dialog-ViewModel (Vorbelegung: nächste volle Minute; bei bestehender Pause deren Endzeitpunkt). |
| `ct` | `CancellationToken` | Optionales Abbruch-Token. |

**Rückgabe:** `PauseWorkItemResult?` — `null` bei Abbruch; `Lift = true` zum Aufheben; andernfalls `PausedUntilUtc` mit dem gewählten UTC-Zeitpunkt.

**`PauseWorkItemResult` (Record):**

| Name | Typ | Beschreibung |
|------|-----|--------------|
| `PausedUntilUtc` | `DateTimeOffset?` | Gewählter Pausen-Endzeitpunkt (bereits nach UTC normalisiert). |
| `Aufheben` | `bool` | `true`, wenn der Anwender **Pause aufheben** gewählt hat. |

**Verhalten:**

- `WpfDialogService` zeigt `WorkItemPauseDialog` modal über `ShowDialogAsync`.
- Die Validierung läuft im ViewModel (`CanConfirm`, `ValidationError`): Datum gesetzt, Stunde 0–23, Minute 0–59, Zeitpunkt in der Zukunft.
- `TaskDetailViewModel.SetPauseAsync` ruft anschließend `WorkItemService.SetPauseAsync` auf (`null` bei `Aufheben`).

### `ShowSaveFileDialogAsync(string title, string filter, string defaultFileName, string? initialDirectory = null, CancellationToken ct = default) : Task<string?>`

**Beschreibung:** Öffnet einen nativen Speichern-Dialog und gibt den gewählten Dateipfad zurück, oder `null` wenn der Benutzer abbricht.

**Parameter:**

| Name | Typ | Beschreibung |
|------|-----|--------------|
| `title` | `string` | Fenstertitel des Dialogs. |
| `filter` | `string` | Dateifilter, z. B. `Raw files (*.raw)|*.raw`. |
| `defaultFileName` | `string` | Vorgeschlagener Dateiname. |
| `initialDirectory` | `string?` | Optionales Startverzeichnis. |
| `ct` | `CancellationToken` | Optionales Abbruch-Token. |

**Verhalten:**

- Wird von `TaskDetailViewModel.ExportCliRawAsync` verwendet, um den Zielpfad für den Export abzufragen.
- Bei Dialogabbruch wird `null` zurückgegeben; es erfolgt kein Schreibvorgang.
- Die konkrete WPF-Implementierung öffnet `SaveFileDialog` auf dem UI-Dispatcher.

### `ShowOpenFileDialogAsync(string title, string filter, string? initialDirectory = null, CancellationToken ct = default) : Task<string?>`

**Beschreibung:** Öffnet einen nativen Öffnen-Dialog und gibt den gewählten Dateipfad zurück, oder `null` wenn der Benutzer abbricht.

**Parameter:**

| Name | Typ | Beschreibung |
|------|-----|--------------|
| `title` | `string` | Fenstertitel des Dialogs. |
| `filter` | `string` | Dateifilter, z. B. `CLI-Replay-Dateien (*.clireplay)|*.clireplay`. |
| `initialDirectory` | `string?` | Optionales Startverzeichnis. |
| `ct` | `CancellationToken` | Optionales Abbruch-Token. |

**Verhalten:**

- Wird vom `ConsoleTestViewModel` verwendet, um eine `.clireplay`-Aufzeichnung zum Laden auszuwählen.
- Die WPF-Implementierung öffnet `OpenFileDialog` auf dem UI-Dispatcher mit dem jeweils aktiven Fenster als Owner (relevant für Aufrufe aus dem nicht-modalen Konsolentestfenster).

### `ShowConsoleTestDialogAsync(ConsoleTestViewModel viewModel, CancellationToken ct = default) : Task`

**Beschreibung:** Zeigt das Konsolentestfenster (`ConsoleTestDialog`, Titel „Konsolentest") **nicht-modal** an und kehrt nach dem Anzeigen zurück — das Fenster bleibt parallel zur Anwendung nutzbar.

**Verhalten:**

- Wird von `SettingsViewModel.OpenConsoleTestCommand` (Einstellungen → Allgemein → Diagnose) aufgerufen.
- `Owner` ist das Hauptfenster; der Lebenszyklus des ViewModels wird über den `Closed`-Handler des Fensters disponiert.

## Thread-Sicherheit

- `PromptScheduleDispatchService` ist Thread-safe: Der interne Dictionary wird durch `lock` geschützt.
- Timer-Callbacks laufen auf Thread-Pool-Threads; `PromptSent`-Event wird ebenfalls auf Thread-Pool-Thread ausgelöst.
- UI-Updates im ViewModel (über `PromptSent`) müssen via `Dispatcher.Invoke` gemarshallt werden; das `TaskDetailViewModel` macht dies automatisch.
