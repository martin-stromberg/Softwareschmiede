← [Zurück zur Übersicht](index.md)

# Aufgaben — API

## Übersicht

Die Task-Detail-Ansicht exponiert öffentliche Service-Schnittstellen für die Verwaltung von Aufgaben, zeitgesteuerten Prompt-Versänden, Aufgaben-Pausen und dem Export der CLI-Rohausgabe:

- `AufgabeService` — Persistente Datenbankoperationen für Aufgaben, einschließlich `SetPauseAsync` und der Laufdaten-Operationen
- `KiAusfuehrungsService` / `IRunningAutomationStatusSource` — Live-Laufquelle und `CliLaufAktiv`-Signal für die Status-Synchronisation
- `KiPluginLimitService` — Persistenz und Anwendung von KI-Plugin-Session-Limits
- `PromptZeitVersandService` — Laufzeit-Verwaltung zeitgesteuerter Prompts (inkl. Persistenz, Pausen-Verschiebung und Nachzustellung)
- `ZeitgesteuerterPromptStore` — Scoped Service für den EF-Zugriff auf persistierte geplante Prompts
- `ProtokollService` — Persistente Protokolleinträge, einschließlich automatischer CLI-Ausgaben und Rate-Limit-Marker
- `ICliRawExportService` — Exportiert die gespeicherte CLI-Rohausgabe einer Aufgabe als `.raw`
- `IDialogService` — UI-Abstraktion für Dialoge (Speichern-Dialog, Pause-Dialog)
- `AufgabenListenModusService` / `AufgabenListenModusBindung` — Persistenz und Zustand des Anzeigemodus der aktiven Aufgabenliste je Ansicht

## ProtokollService — CLI-Ausgabeprotokoll

### `AddCliOutputAsync(Guid aufgabeId, string outputLine, CancellationToken ct = default) : Task`

**Beschreibung:** Speichert eine einzelne CLI-Ausgabezeile als Protokolleintrag vom Typ `ProtokollTyp.CliOutput`.

**Parameter:**

| Name | Typ | Beschreibung |
|------|-----|--------------|
| `aufgabeId` | `Guid` | ID der Aufgabe, deren CLI die Ausgabe erzeugt hat. |
| `outputLine` | `string` | Dekodierte Ausgabezeile aus dem Terminal-Output. |
| `ct` | `CancellationToken` | Optionales Abbruch-Token. |

**Verhalten:**

- Erstellt einen `Protokolleintrag` mit `Typ = ProtokollTyp.CliOutput`.
- Wird im interaktiven Terminal-Pfad (ConPTY oder Pipe-Fallback) automatisch durch `CliOutputProtokollWriter` aufgerufen.
- Erkennt Rate-Limit-Marker in der Ausgabezeile und speichert dann zusätzlich einen `ProtokollTyp.RateLimit`-Eintrag (`"Rate-Limit erkannt. Weiter ab: …"` bzw. `"(kein Zeitstempel)"`).
- Speichert pro Aufruf eine Zeile.
- Der Aufrufer `CliOutputProtokollWriter` reicht Marker mit gültigem Zeitstempel anschließend an `KiPluginLimitService.VerarbeiteRateLimitAsync` weiter.

### `TryParseRateLimitMarker(string outputLine, out DateTimeOffset? resetUtc) : bool` (statisch)

**Beschreibung:** Parst den Session-Limit-Marker `[[SOFTWARESCHMIEDE_RATE_LIMIT:<ISO8601>]]` aus einer CLI-Ausgabezeile.

**Verhalten:**

- Gibt `true` zurück, wenn das Marker-Präfix und die schließende `]]`-Klammer gefunden werden — auch ohne gültigen Zeitstempel (`resetUtc = null`).
- Der Marker darf auch eingebettet in einer längeren Zeile vorkommen.
- Ein gültiger ISO-8601-Zeitstempel wird als `DateTimeOffset` in `resetUtc` geliefert.

### `ParseRateLimitMarker(string outputLine) : (bool Found, string? Prompt, DateTimeOffset? ResetUtc)` (statisch)

**Beschreibung:** Kompatibilitäts-Wrapper um `TryParseRateLimitMarker`; liefert ein Tuple. `Prompt` ist aktuell immer `null`.

## AufgabeService — Pause

### `SetPauseAsync(Guid aufgabeId, DateTimeOffset? pausiertBisUtc, CancellationToken ct = default) : Task`

**Beschreibung:** Setzt oder leert den Pausen-Endzeitpunkt (`Aufgabe.PausiertBisUtc`) einer regulären Aufgabe.

**Parameter:**

| Name | Typ | Beschreibung |
|------|-----|--------------|
| `aufgabeId` | `Guid` | ID der Aufgabe. |
| `pausiertBisUtc` | `DateTimeOffset?` | UTC-Zeitpunkt, bis zu dem pausiert wird; `null` hebt eine bestehende Pause auf. |
| `ct` | `CancellationToken` | Optionales Abbruch-Token. |

**Verhalten:**

- Erlaubte Stati: `Neu`, `Gestartet`, `Wartend` — andere Stati werden mit `InvalidOperationException` abgelehnt.
- Beim Setzen muss `pausiertBisUtc` in der Zukunft liegen; andernfalls `InvalidOperationException`.
- Schreibt pro Aufruf einen `ProtokollTyp.SystemMeldung`-Eintrag („Aufgabe pausiert bis …" / „Pause aufgehoben").
- Wirft `InvalidOperationException`, wenn die Aufgabe nicht gefunden wird.

## AufgabeService — KI-Laufdaten

Alle Laufdaten-Mutationen laufen unter der statischen Sperrung `AufgabeService.LaufGates` (`ConcurrentDictionary<Guid, SemaphoreSlim>`): Sie serialisiert prozessweit alle Schreibzugriffe auf `AktiveRunId`/`LastHeartbeatUtc`/`LetzterCliStartUtc`/`LaufStatus`/`AusfuehrungsStatus` einer Aufgabe — einschließlich der laufdatenrücksetzenden Lebenszyklus-Pfade `StartenAsync`, `AbschliessenAsync` und `StartZuruecksetzenAsync`. Die optionalen Callbacks `laeuftGuard` und `laufStatusProvider` werden grundsätzlich erst innerhalb der gehaltenen Sperre ausgewertet (TOCTOU-/Stale-Write-Schutz).

### `AktivenLaufSetzenAsync(Guid id, string laufId, CancellationToken ct = default, Func<bool>? laeuftGuard = null) : Task`

**Beschreibung:** Setzt den vollständigen Lauf-Zustand für einen gestarteten CLI-Prozess (`AusfuehrungsStatus = Aktiv`, `AktiveRunId = laufId`, `LastHeartbeatUtc`, `LetzterCliStartUtc`, `LaufStatus = Laeuft`).

**Verhalten:**

- Liefert `laeuftGuard` innerhalb der Sperre `false` (Prozess zwischenzeitlich beendet): `LogDebug`, kein Schreibzugriff — kein Phantom-Lauf nach `AktivenLaufBeendenAsync`.
- Terminale Aufgaben (`Beendet`/`Archiviert`) oder unbekannte ID: `InvalidOperationException`.

### `AktivenLaufSicherstellenAsync(Guid id, Func<AufgabeLaufStatus?>? laufStatusProvider = null, CancellationToken ct = default, Func<bool>? laeuftGuard = null) : Task`

**Beschreibung:** Stellt die Laufdaten eines laufenden CLI-Laufs idempotent sicher: Fehlt `AktiveRunId`, wird der vollständige Lauf-Zustand geschrieben (`AusfuehrungsStatus = Aktiv`, neue `AktiveRunId`, `LastHeartbeatUtc`, `LaufStatus = Provider-Wert ?? Laeuft`; ein bekannter `LetzterCliStartUtc` bleibt erhalten — Sortierstabilität). Bei bestehendem Lauf wird nur `LastHeartbeatUtc` aufgefrischt und `LaufStatus` nur bei geliefertem Wert überschrieben. Zentraler Persistenzpunkt aller Selbstheilungspfade des `CliProcessManager` (`CliLaufAktiv`, `RuntimeStatusChanged`, Heartbeat-Tick, periodischer Abgleich) sowie des Wiederverwendungspfads in `TaskDetailViewModel.StartCliAndUpdateStateAsync`.

**Verhalten:**

- `laeuftGuard` liefert innerhalb der Sperre `false`: `LogDebug`, kein Schreibzugriff.
- `Status` `Beendet`/`Archiviert`: `LogDebug`, Rückkehr ohne Wurf — der periodische Abgleich erzeugt keine Warnungsflut.
- Unbekannte Aufgabe: `InvalidOperationException` (konsistent zu `AktivenLaufSetzenAsync`).

### `AktivenLaufBeendenAsync(Guid id, CancellationToken ct = default) : Task`

**Beschreibung:** Entfernt die Laufdaten (`AktiveRunId`, `LastHeartbeatUtc`, `LetzterCliStartUtc`, `LaufStatus`) und setzt `AusfuehrungsStatus = Beendet` — serialisiert unter `LaufGates`, sodass ein gleichzeitig anstehendes Setzen/Sicherstellen entweder davor (dann greift dessen Guard nicht mehr) oder danach (Beenden hat das letzte Wort) serialisiert.

## KiAusfuehrungsService / IRunningAutomationStatusSource — Live-Laufquelle

### `AufgabeLaufStatus? GetLiveLaufStatus(Guid aufgabeId)` (Interface-Methode)

**Beschreibung:** Live-Substatus aus der Laufzeitquelle: `null`, wenn kein gehandelter Prozess läuft (`IsRunning` `false`); `WartetAufEingabe`, wenn die Session `CliRuntimeStatus.WartetAufEingabe` meldet; sonst `Laeuft` — auch bei laufendem Handle ohne Session oder `RuntimeStatus.Inaktiv`. Datenquelle für `AktiveAufgabePanelItem.LiveLaufStatus`; der Live-Zweig der Kachel-Klassifikation darf bei laufendem Prozess damit nie auf `Bereit` zurückfallen.

### `IReadOnlyCollection<Guid> GetLaufendeAufgabenIds()`

**Beschreibung:** IDs aller Aufgaben mit laufendem Handle (`!HasExited`, Exception-tolerant wie `GetRunningCount`) — Datenquelle des periodischen Abgleichs im `CliProcessManager`.

### `void NotifyLaufAktiv(Guid aufgabeId)`

**Beschreibung:** Feuert `CliLaufAktiv`, sofern `IsRunning(aufgabeId)` zutrifft — sonst No-Op. Für Aufrufer außerhalb des Service, die einen laufenden Prozess bestätigt haben (produktiv: `EntwicklungsprozessService.CliNeustartenCoordinatedAsync` im Session-Alive-Early-Return).

### `event Action<Guid>? CliLaufAktiv`

**Beschreibung:** Signal, dass eine Startanforderung einen bereits laufenden Prozess zurückgibt (Early-Return „zweiter Start abgewiesen" in `StartCliCoreAsync`/`StartTerminalSessionCoreAsync` — dort feuert kein `CliProcessStatus.Gestartet`) bzw. ein laufender Prozess über `NotifyLaufAktiv` bestätigt wurde. Einziger produktiver Abonnent: `CliProcessManager.OnCliLaufAktiv` → `LaufdatenSicherstellenAsync`. Vertrag wie `CliProcessStatusChanged`: Der Invoke läuft unter der klassenweiten Start-Sperre (`_startLock`) — Abonnenten müssen non-blocking sein und dürfen nicht reentrant `StartCliAsync`/`StartTerminalSessionAsync` aufrufen.

## KiPluginLimitService — Plugin-Session-Limits

**Registrierung:** Scoped in DI; Abhängigkeiten: `SoftwareschmiededDbContext`, `AppEinstellungService`, `AufgabeLaufdatenChangedNotifier`.

**Konstante:** `KiPluginLimitService.SessionLimitKeyPrefix = "plugins.sessionlimit."` — Schlüsselpräfix für persistierte Reset-Zeitpunkte in `AppEinstellung`.

### `VerarbeiteRateLimitAsync(Guid aufgabeId, DateTimeOffset resetUtc, CancellationToken ct = default) : Task`

**Beschreibung:** Verarbeitet einen erkannten Session-Limit-Marker: persistiert den Reset-Zeitpunkt unter `plugins.sessionlimit.<KiPluginPrefix>` der auslösenden Aufgabe und pausiert bei zukünftigem Zeitpunkt alle aktiv laufenden regulären Aufgaben mit demselben Prefix.

**Parameter:**

| Name | Typ | Beschreibung |
|------|-----|--------------|
| `aufgabeId` | `Guid` | ID der Aufgabe, in deren CLI-Ausgabe der Marker erkannt wurde. |
| `resetUtc` | `DateTimeOffset` | Gemeldeter Reset-Zeitpunkt des Session-Limits. |
| `ct` | `CancellationToken` | Optionales Abbruch-Token. |

**Verhalten:**

- Aufgabe nicht gefunden oder `KiPluginPrefix` leer: `LogWarning`, keine Persistenz, keine Pause.
- Persistiert `resetUtc` (normalisiert auf UTC) als ISO-8601-Roundtrip-String (`"O"`) in `AppEinstellung`.
- Liegt `resetUtc` in der Vergangenheit: Persistenz erfolgt, aber es wird keine Pause gesetzt.
- Kandidaten: Aufgaben mit `Status.IstAktivOderWartend()`, `AusfuehrungsStatus == Aktiv`, Prefix-Vergleich `OrdinalIgnoreCase`.
- Max-Semantik: `PausiertBisUtc` wird auf `max(bisheriger Wert, resetUtc)` gesetzt — längere manuelle Pausen werden nicht verkürzt.
- Pro pausierter Aufgabe wird ein `ProtokollTyp.SystemMeldung`-Eintrag geschrieben; nach dem Speichern wird `AufgabeLaufdatenChangedNotifier.NotifyLaufdatenChanged` je Aufgabe ausgelöst.
- Laufende CLI-Prozesse werden nicht angefasst.

### `GetAktiveSessionLimitsAsync(IReadOnlyCollection<string> prefixes, CancellationToken ct = default) : Task<IReadOnlyDictionary<string, DateTimeOffset>>`

**Beschreibung:** Liest die persistierten Session-Limits mehrerer Plugin-Prefixe in einer Abfrage (Batch-Lookup, z. B. für `CliUpdateSafetyService`).

**Verhalten:**

- Übersetzt jedes Prefix auf den Schlüssel `plugins.sessionlimit.<Prefix>` und lädt die Werte über `AppEinstellungService.GetSettingsAsync`.
- Gibt nur zukünftige, gültig geparste (`DateTimeStyles.AssumeUniversal | AdjustToUniversal`) Zeitpunkte zurück; abgelaufene oder ungültige Werte werden ignoriert.
- Das zurückgegebene Dictionary verwendet `OrdinalIgnoreCase`-Schlüsselvergleich.
- Leere Prefix-Liste oder ausschließlich leere Prefixe: leeres Dictionary.

## PromptZeitVersandService — Zeitgesteuerte Prompt-Versendung

### Übersicht

`PromptZeitVersandService` ist ein Singleton-Service, der zeitgesteuerte Prompts pro Aufgabe in einer Laufzeit-Warteschlange verwaltet. Geplante Prompts werden zusätzlich über den scoped `ZeitgesteuerterPromptStore` in der Tabelle `ZeitgesteuertePrompts` persistiert, beim App-Start via `RestoreScheduledPromptsAsync` wiederhergestellt und — wenn zum Fälligkeitszeitpunkt keine Session zustellfähig ist — beim nächsten CLI-Start der Aufgabe nachgestellt (`CliProcessStatusChanged` → `Gestartet`). Der Service ist registriert in der Dependency Injection und wird vom `TaskDetailViewModel` genutzt.

### Öffentliche Methoden

#### `SchedulePromptAsync(Guid aufgabeId, string promptText, DateTimeOffset targetTime) : Task`

**Beschreibung:** Plant den Versand eines Prompts zur angegebenen Zielzeit.

**Parameter:**

| Name | Typ | Beschreibung |
|------|-----|--------------|
| `aufgabeId` | `Guid` | ID der Aufgabe, deren aktive CLI-Session den Prompt erhalten soll. |
| `promptText` | `string` | Der bereits aufgelöste Prompttext (Platzhalter müssen vor Aufruf ersetzt sein). |
| `targetTime` | `DateTimeOffset` | Der Zeitpunkt (in UTC), zu dem der Prompt versendet werden soll. |

**Verhalten:**

- Liegt `targetTime` in der Vergangenheit oder Gegenwart: Prompt wird sofort an die Session geschrieben (kein Eintrag in der Warteschlange, kein persistierter Datensatz). Schlägt auch der Sofortversand fehl, geht der Plan in „wartet auf Session" über und wird persistiert.
- Liegt `targetTime` in der Zukunft: `ZeitgesteuerterPromptStore.UpsertAsync` schreibt zuerst den Datensatz (DB vor Timer-Armierung); danach wird ein `ScheduledPromptInfo`-Eintrag im internen Dictionary abgelegt und ein `ITimer` mit der Restlaufzeit gestartet.
- **Pausen-Berücksichtigung:** Über die optionale `IServiceScopeFactory` wird der aktuelle `Aufgabe.PausiertBisUtc`-Wert geladen. Endet eine aktive Pause später als `targetTime`, wird die Zielzeit auf das Pausenende verschoben statt verworfen — sowohl bei der Planung als auch erneut im Timer-Callback (`HandleTimerElapsedAsync`), falls die Pause nachträglich gesetzt wurde. Ohne registrierte Scope-Factory (z. B. Unit-Tests) entfallen die Prüfung und die Persistenz.
- Ein bereits geplanter Prompt für dieselbe `aufgabeId` wird ersetzt (Timer wird abgebrochen, Datensatz per Upsert überschrieben).
- Nach dem Eintragen wird `ScheduledPromptChanged` ausgelöst.

**Rückgabe:** `Task` — abwartet die Planung (nicht den tatsächlichen Versand).

**Fehlerbehandlung:**

- Exceptions beim Schreiben auf die Session werden geloggt und überführen den Plan in „wartet auf Session" (kein Verwerfen). Fehler beim Persistieren werden geloggt; der Plan bleibt dann sitzungsgebunden.

**Beispiel:**

```csharp
var targetTime = DateTimeOffset.Now.AddHours(2); // in 2 Stunden
await _promptZeitVersandService.SchedulePromptAsync(
    aufgabeId: aufgabeId,
    promptText: "Analysiere den Bug in LoginService",
    targetTime: targetTime);
```

---

#### `CancelScheduledPromptAsync(Guid aufgabeId) : Task`

**Beschreibung:** Bricht einen für die Aufgabe geplanten Prompt-Versand ab, falls vorhanden — entfernt den In-Memory-Eintrag und den persistierten Datensatz.

**Parameter:**

| Name | Typ | Beschreibung |
|------|-----|--------------|
| `aufgabeId` | `Guid` | ID der Aufgabe, deren geplanter Prompt storniert werden soll. |

**Verhalten:**

- Falls ein Prompt für `aufgabeId` geplant ist: Eintrag wird aus der Warteschlange entfernt, Timer wird abgebrochen und disposed.
- Der persistierte `ZeitgesteuerterPrompt`-Datensatz wird via `ZeitgesteuerterPromptStore.LoeschenAsync` gelöscht.
- `ScheduledPromptChanged` wird ausgelöst.
- Falls kein Prompt geplant ist: Keine Aktion (keine Exception).

**Rückgabe:** `Task` — abwartet die Stornierung einschließlich Datensatz-Löschung.

**Beispiel:**

```csharp
await _promptZeitVersandService.CancelScheduledPromptAsync(aufgabeId);
```

---

#### `RestoreScheduledPromptsAsync() : Task`

**Beschreibung:** Stellt persistierte geplante Prompts beim App-Start wieder her; wird in `App.OnStartup` nach `db.Database.MigrateAsync()` aufgerufen.

**Verhalten:**

- Lädt alle `ZeitgesteuerterPrompt`-Datensätze via `ZeitgesteuerterPromptStore.GetAlleAsync`.
- Aufgabe nicht mehr vorhanden → Datensatz wird defensiv gelöscht.
- Aktive Pause jenseits der Zielzeit → Zielzeit wird auf das Pausenende vorgeladen und der Datensatz via `UpdateZielZeitpunktAsync` aktualisiert.
- Zielzeit in Zukunft → In-Memory-Eintrag + armiertem Timer.
- Zielzeit verstrichen → Eintrag im Zustand „wartet auf Session" (kein Timer); Zustellung beim nächsten `CliProcessStatusChanged` → `Gestartet`.
- Fehler werden pro Datensatz isoliert geloggt; ohne `IServiceScopeFactory` ist die Methode ein No-Op.

---

#### `GetScheduledPromptStatus(Guid aufgabeId) : ScheduledPromptInfo?`

**Beschreibung:** Gibt Informationen zum aktuell für die Aufgabe geplanten Prompt zurück, oder `null` wenn keiner geplant ist.

**Parameter:**

| Name | Typ | Beschreibung |
|------|-----|--------------|
| `aufgabeId` | `Guid` | ID der Aufgabe. |

**Rückgabe:**

| Typ | Beschreibung |
|-----|--------------|
| `ScheduledPromptInfo?` | Objekt mit `AufgabeId`, `PromptText`, `TargetTime` und `WartetAufSession`, oder `null` wenn kein Prompt geplant ist. |

**Beispiel:**

```csharp
var scheduled = _promptZeitVersandService.GetScheduledPromptStatus(aufgabeId);
if (scheduled is not null)
{
    Console.WriteLine($"Prompt geplant für {scheduled.TargetTime:HH:mm}");
}
```

---

### Öffentliche Events

#### `PromptSent : event Action<Guid>?`

**Beschreibung:** Wird ausgelöst, nachdem ein zeitgesteuerter Prompt erfolgreich an die CLI-Session versendet wurde.

**Parameter:** `Guid aufgabeId` — ID der Aufgabe, deren Prompt versendet wurde.

**Verhalten:**

- Dieses Event wird ausgelöst **nach** `PseudoConsoleSession.WritePromptAsync()` erfolgreich abgeschlossen hat — sowohl beim Timer-/Sofortversand als auch bei der Nachzustellung nach einem CLI-Start.
- Ist die Session zur Zielzeit nicht zustellfähig oder schlägt der Versand fehl, wird dieses Event **nicht** ausgelöst; der Plan geht in „wartet auf Session" über und bleibt persistiert.
- Die Anzahl der Abonnenten steuert, ob ein `SystemMeldung`-Protokolleintrag geschrieben wird: ohne Abonnenten (keine geöffnete Detailansicht) wird nach Timer-/Sofortversand ein Eintrag geschrieben.
- Das Event wird auf dem Thread-Pool-Thread des Timer-Callbacks bzw. des Session-Start-Handlers ausgelöst, **nicht** auf dem UI-Thread.

**Beispiel:**

```csharp
_promptZeitVersandService.PromptSent += (aufgabeId) =>
{
    Console.WriteLine($"Prompt für Aufgabe {aufgabeId} versendet");
};
```

---

#### `ScheduledPromptChanged : event Action<Guid>?`

**Beschreibung:** Wird ausgelöst, wenn sich der Wartestellungs-Zustand eines geplanten Prompts ändert — beim Planen, beim Storno, nach erfolgreichem Versand und beim Übergang in „wartet auf Session".

**Parameter:** `Guid aufgabeId` — ID der Aufgabe, deren Plan-Zustand sich geändert hat.

**Verhalten:**

- Signal für Beobachter, `GetScheduledPromptStatus` neu zu lesen (z. B. `TaskDetailViewModel.OnScheduledPromptChanged` → `AktualisiereWartestellungsAnzeige`).
- Trägt externe Stornos (Aufgabenabschluss, Ersetzung) und den „wartet auf Session"-Übergang in die UI.

---

### ScheduledPromptInfo — Datencontainer

**Typ:** `sealed record`

**Beschreibung:** Unveränderliches Value-Object zur Verwaltung geplanter Prompts.

**Eigenschaften:**

| Name | Typ | Beschreibung |
|------|-----|--------------|
| `AufgabeId` | `Guid` | ID der Aufgabe, deren Session den Prompt erhalten wird. |
| `PromptText` | `string` | Der bereits aufgelöste Prompttext. |
| `TargetTime` | `DateTimeOffset` | Der Zeitpunkt, zu dem der Prompt versendet werden soll. |
| `WartetAufSession` | `bool` | `true`, wenn die Zielzeit bereits verstrichen ist, der Versand aber mangels zustellfähiger CLI-Session aussteht — der Plan bleibt gültig und wird beim nächsten CLI-Start zugestellt. |

**Beispiel:**

```csharp
var info = new ScheduledPromptInfo(
    aufgabeId: aufgabeId,
    promptText: "Analyt den Bug",
    targetTime: DateTimeOffset.Now.AddHours(1));
```

---

## ZeitgesteuerterPromptStore — Persistenz geplanter Prompts

**Registrierung:** Scoped in DI; Abhängigkeiten: `SoftwareschmiededDbContext`. Wird vom Singleton `PromptZeitVersandService` ausschließlich über einen eigenen Scope (`IServiceScopeFactory` → `MitStoreAsync`) aufgelöst — Fehler beim Store-Zugriff werden geloggt und brechen den Versandpfad nicht.

### `GetAlleAsync(CancellationToken ct = default) : Task<IReadOnlyList<ZeitgesteuerterPrompt>>`

**Beschreibung:** Liefert alle persistierten geplanten Prompts (`AsNoTracking`); Grundlage für `RestoreScheduledPromptsAsync`.

### `UpsertAsync(Guid aufgabeId, string promptText, DateTimeOffset zielZeitpunktUtc, CancellationToken ct = default) : Task`

**Beschreibung:** Schreibt den geplanten Prompt der Aufgabe oder ersetzt einen vorhandenen Datensatz (Ersetzungsregel — pro Aufgabe maximal ein Plan, datenbankseitig über Unique-Index auf `AufgabeId` abgesichert). Aktualisiert bei Ersetzung `PromptText`, `ZielZeitpunktUtc` und `AktualisiertAmUtc`.

### `UpsertWennUebereinstimmendAsync(Guid aufgabeId, string promptText, DateTimeOffset zielZeitpunktUtc, ScheduledPromptInfo? erwartet, CancellationToken ct = default) : Task<bool>`

**Beschreibung:** Schreibt den Plan nur, wenn der vorhandene Datensatz noch dem erwarteten Vorgängerstand entspricht (`PromptText` + `ZielZeitpunktUtc` auf Unix-Millisekunden normiert) bzw. kein Datensatz existiert und keiner erwartet wird — Lost-Update-Schutz für den Sofortversand-Fehlerpfad. Gibt `false` zurück, wenn ein abweichender (neuerer) Datensatz den Schreibzugriff verweigert.

### `LoeschenAsync(Guid aufgabeId, CancellationToken ct = default) : Task`

**Beschreibung:** Entfernt den persistierten Plan der Aufgabe, falls vorhanden (Storno).

### `LoeschenNachVersandAsync(Guid aufgabeId, string promptText, DateTimeOffset zielZeitpunktUtc, bool neuererPlanAktiv, CancellationToken ct = default) : Task`

**Beschreibung:** Entfernt den Datensatz nach erfolgreichem Versand. Entspricht der Datensatz dem versendeten Plan, wird er immer gelöscht; weicht er ab, bleibt er nur bei `neuererPlanAktiv == true` erhalten (Lost-Update-Schutz), sonst wird er als Leiche des versendeten Plans mitgelöscht.

### `UpdateZielZeitpunktAsync(Guid aufgabeId, DateTimeOffset zielZeitpunktUtc, CancellationToken ct = default) : Task`

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

**Befüllung:** Zentral über `AktualisiereWartestellungsAnzeige()` aus `GetScheduledPromptStatus` — beim Laden der Ansicht (`LadenAsync`, auch für Pläne aus früheren Sitzungen/App-Läufen) sowie nach `ScheduledPromptChanged`/`PromptSent`.

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
- Ruft `PromptZeitVersandService.SchedulePromptAsync()` auf
- Aktualisiert `ScheduledPromptStatus` und `ScheduledPromptTimeDisplay` via `AktualisiereWartestellungsAnzeige()`
- Leert Zeitfelder und Vorlage-Auswahl

---

#### `CancelScheduledPromptCommand : ICommand`

**Typ:** `AsyncRelayCommand`

**Bindung:** Button „Wartestellung aufheben" (`AutomationProperties.Name="WartestellungAufheben"`) im Status-`StackPanel`; sichtbar nur solange `ScheduledPromptStatus` gesetzt ist.

**Ausgeführte Aktion:** `PromptZeitVersandService.CancelScheduledPromptAsync(_aufgabeId)` — entfernt Plan und Datensatz; die Statusanzeige wird anschließend über `ScheduledPromptChanged` → `AktualisiereWartestellungsAnzeige` geleert.

---

## Fehlerbehandlung (PromptZeitVersandService)

| Fehler | Verhalten |
|--------|-----------|
| Session nicht gefunden bei Fälligkeit | Log-Warnung; Plan + Datensatz bleiben erhalten und gehen in „wartet auf Session" über (Status „Prompt wartet auf CLI-Start"); Zustellung beim nächsten `CliProcessStatusChanged` → `Gestartet`; **kein** `PromptSent`-Event |
| Session disposed zwischen Planung und Versand | `ObjectDisposedException` wird geloggt; gleiche Behandlung — „wartet auf Session" |
| Schreiboperation auf InputStream schlägt fehl | Exception wird geloggt; Plan + Datensatz bleiben erhalten („wartet auf Session"); erneuter Versuch beim nächsten Session-Start; `PromptSent`-Event wird **nicht** ausgelöst |
| Fehler beim Persistieren des Plans | Store-Ausnahme wird geloggt; der Plan bleibt sitzungsgebunden in der Warteschlange |
| Fehler beim Protokolleintrag (z. B. Aufgabe gelöscht) | Wird geloggt; der erfolgreiche Versand bleibt gültig |
| Ungültige Zeitfelder im ViewModel | `FehlerMeldung` wird gesetzt; Service wird nicht aufgerufen; ViewModel-seitige Validierung |
| Aktive Pause bei Planung oder Fälligkeit | Zielzeit/Timer und persistierter `ZielZeitpunktUtc` werden auf das Pausenende verschoben; **kein** `PromptSent`-Event vor dem Pausenende |

---

## CliRawExportService — CLI-Rohausgabe

### `ExportCliRawAsync(Guid aufgabeId, string zielPfad, CancellationToken ct = default) : Task`

**Beschreibung:** Schreibt die protokollierte CLI-Rohausgabe einer Aufgabe in eine `.raw`-Datei.

**Parameter:**

| Name | Typ | Beschreibung |
|------|-----|--------------|
| `aufgabeId` | `Guid` | ID der Aufgabe, deren CLI-Rohausgabe exportiert werden soll. |
| `zielPfad` | `string` | Zielpfad der zu erzeugenden `.raw`-Datei. |
| `ct` | `CancellationToken` | Optionales Abbruch-Token. |

**Verhalten:**

- Lädt die gespeicherten Protokolle über `ProtokollService.GetByAufgabeAsync(...)`.
- Filtert ausschließlich `ProtokollTyp.CliOutput` und übernimmt nur deren `Inhalt`.
- Verbindet die Zeilen in vorhandener Reihenfolge mit `Environment.NewLine`.
- Schreibt die Datei mit UTF-8 ohne BOM.
- Abbruch und Schreibfehler werden an den Aufrufer weitergereicht.

## CliReplayExportService — CLI-Aufzeichnung (.clireplay)

Exportiert den im `KiAusfuehrungsService` gehaltenen Rohbyte-Mitschnitt einer Aufgabe als `.clireplay`-Binärdatei (Interface `ICliReplayExportService`).

### `ExportCliReplayAsync(Guid aufgabeId, string zielPfad, CancellationToken ct = default) : Task`

**Beschreibung:** Schreibt die aufgezeichnete CLI-Ausgabe einer Aufgabe (Header + zeitgestempelte Chunk-Records) über `CliReplayAufzeichnungStore` in die angegebene `.clireplay`-Datei.

**Parameter:**

| Name | Typ | Beschreibung |
|------|-----|--------------|
| `aufgabeId` | `Guid` | ID der Aufgabe, deren Aufzeichnung exportiert werden soll. |
| `zielPfad` | `string` | Zielpfad der zu erzeugenden `.clireplay`-Datei. |
| `ct` | `CancellationToken` | Optionales Abbruch-Token. |

**Exceptions:**

| Exception | Ursache |
|-----------|---------|
| `ArgumentException` | `zielPfad` leer. |
| `InvalidOperationException` | „Für diese Aufgabe liegt keine Aufzeichnung vor." — `KiAusfuehrungsService.GetCliAufzeichnung` liefert `null`. |

### `HatAufzeichnung(Guid aufgabeId) : bool`

**Beschreibung:** Prüft, ob für die Aufgabe ein Mitschnitt vorliegt — Vorab-Prüfung im `TaskDetailViewModel`, bevor der Speicherdialog geöffnet wird.

## AufgabenListenModusService — Anzeigemodus-Persistenz

Singleton-Service (`src/Softwareschmiede.App/Services/`), der den Anzeigemodus der aktiven Aufgabenliste je Ansicht (Seitenleiste, Dashboard) über `AppEinstellungService` in eigenen DI-Scopes (`IServiceScopeFactory`) liest und schreibt — Muster `DarkModeService`.

### `LadeMenueModusAsync(CancellationToken ct = default) : Task<AufgabenListenModus>`

**Beschreibung:** Lädt den gespeicherten Anzeigemodus der Seitenleisten-Aufgabenliste (`AppEinstellungService.AktiveAufgabenListenModusKey`).

**Rückgabe:** Der gespeicherte `AufgabenListenModus` oder `Datum`, wenn kein gültiger Wert gespeichert ist (`Enum.TryParse`, Groß-/Kleinschreibung tolerant, plus `Enum.IsDefined`).

### `LadeDashboardModusAsync(CancellationToken ct = default) : Task<AufgabenListenModus>`

**Beschreibung:** Wie `LadeMenueModusAsync`, aber für den Dashboard-Schlüssel (`AktiveAufgabenListenModusDashboardKey`).

### `SpeichereMenueModusAsync(AufgabenListenModus modus, CancellationToken ct = default) : Task`

**Beschreibung:** Speichert den Anzeigemodus der Seitenleisten-Aufgabenliste als Enum-Namen (`modus.ToString()`) per `SetSettingAsync`.

### `SpeichereDashboardModusAsync(AufgabenListenModus modus, CancellationToken ct = default) : Task`

**Beschreibung:** Wie `SpeichereMenueModusAsync`, aber für den Dashboard-Schlüssel.

## AufgabenListenModusBindung — Modus-Zustand je Ansicht

Kapselt die gemeinsame Bindungslogik von `MainWindowViewModel` und `DashboardViewModel` für den persistierten Anzeigemodus.

| Member | Typ | Beschreibung |
|--------|-----|--------------|
| `FuerMenue(modusService, logger, dispatch)` | statische Factory | Bindung für die Seitenleiste (Menü-Schlüssel); `dispatch` = UI-Thread-Marshalling der Wiederherstellung |
| `FuerDashboard(modusService, logger)` | statische Factory | Bindung für das Dashboard (Dashboard-Schlüssel) |
| `Wert` | `AufgabenListenModus` | Aktueller Modus; Setzen löst `WertGeaendert` aus, setzt den Race-Guard und persistiert fire-and-forget |
| `WertGeaendert` | `event Action` | Benachrichtigung nach Wertänderung und Wiederherstellung (für `OnPropertyChanged` im ViewModel) |
| `GespeichertenWertLadenAsync(ct)` | `Task` | Lädt den persistierten Modus und wendet ihn ohne Write-Back an; ein zwischenzeitlich gesetzter Wert (Race-Guard, geprüft vor und nach dem `await`) wird nicht überschrieben |

## AufgabenListenModus / AufgabenListenAnsicht

- `AufgabenListenModus` (Enum): `Datum` (flache Liste nach letzter Ausführung), `Repository` (gruppiert nach Repository-Name), `Ki` (gruppiert nach KI).
- `AufgabenListenAnsicht` (statisch): `Konfiguriere(ListCollectionView, AufgabenListenModus)` setzt die zum Modus gehörigen `SortDescriptions` und optional eine `PropertyGroupDescription`; `ErstelleSortierDeskriptoren`/`ErstelleGruppenDeskriptor` liefern dieselben Deskriptoren als Daten (unit-testbar). Konstanten `NichtAusgefuehrt` (`"Nicht ausgeführt"`) und `OhneRepository` (`"Ohne Repository"`) als Fallback-Gruppennamen.
- `ActiveTasksListControl.ListenModus` (Dependency Property): gebunden an `AufgabenListenModus` des jeweiligen ViewModels; Änderungen werden über `OnListenModusChanged` → `AufgabenListenAnsicht.Konfiguriere` auf die instanz-eigene `ListCollectionView` angewendet.
- `IPlugin.PluginSymbol` (`string`, Default-Interface-Member → `IPlugin.FallbackSymbol = "◻"`): Unicode-Glyph des Plugins für die Symbolzeile der Aufgabenkachel; `virtual` an `GitPluginBase<TPlugin>`/`CliKiPluginBase`, von allen Repo-Plugins überschrieben. Binärkompatibel — ältere Plugin-DLLs erhalten den Fallback-Glyph. Details: [Plugin-System — API](../plugins/api.md).

## IDialogService — Dialoge

### `ShowAufgabePausierenDialogAsync(AufgabePausierenDialogViewModel viewModel, CancellationToken ct = default) : Task<AufgabePausierenErgebnis?>`

**Beschreibung:** Zeigt den modalen Dialog **Pause einstellen** zum Setzen oder Aufheben einer Aufgaben-Pause.

**Parameter:**

| Name | Typ | Beschreibung |
|------|-----|--------------|
| `viewModel` | `AufgabePausierenDialogViewModel` | Vorbefülltes Dialog-ViewModel (Vorbelegung: nächste volle Minute; bei bestehender Pause deren Endzeitpunkt). |
| `ct` | `CancellationToken` | Optionales Abbruch-Token. |

**Rückgabe:** `AufgabePausierenErgebnis?` — `null` bei Abbruch; `Aufheben = true` zum Aufheben; andernfalls `PausiertBisUtc` mit dem gewählten UTC-Zeitpunkt.

**`AufgabePausierenErgebnis` (Record):**

| Name | Typ | Beschreibung |
|------|-----|--------------|
| `PausiertBisUtc` | `DateTimeOffset?` | Gewählter Pausen-Endzeitpunkt (bereits nach UTC normalisiert). |
| `Aufheben` | `bool` | `true`, wenn der Anwender **Pause aufheben** gewählt hat. |

**Verhalten:**

- `WpfDialogService` zeigt `AufgabePausierenDialog` modal über `ShowDialogAsync`.
- Die Validierung läuft im ViewModel (`KannBestaetigen`, `ValidierungsFehler`): Datum gesetzt, Stunde 0–23, Minute 0–59, Zeitpunkt in der Zukunft.
- `TaskDetailViewModel.PauseEinstellenAsync` ruft anschließend `AufgabeService.SetPauseAsync` auf (`null` bei `Aufheben`).

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

- Wird vom `KonsolenTestViewModel` verwendet, um eine `.clireplay`-Aufzeichnung zum Laden auszuwählen.
- Die WPF-Implementierung öffnet `OpenFileDialog` auf dem UI-Dispatcher mit dem jeweils aktiven Fenster als Owner (relevant für Aufrufe aus dem nicht-modalen Konsolentestfenster).

### `ShowKonsolenTestDialogAsync(KonsolenTestViewModel viewModel, CancellationToken ct = default) : Task`

**Beschreibung:** Zeigt das Konsolentestfenster (`KonsolenTestDialog`, Titel „Konsolentest") **nicht-modal** an und kehrt nach dem Anzeigen zurück — das Fenster bleibt parallel zur Anwendung nutzbar.

**Verhalten:**

- Wird von `SettingsViewModel.KonsolenTestOeffnenCommand` (Einstellungen → Allgemein → Diagnose) aufgerufen.
- `Owner` ist das Hauptfenster; der Lebenszyklus des ViewModels wird über den `Closed`-Handler des Fensters disponiert.

## Thread-Sicherheit

- `PromptZeitVersandService` ist Thread-safe: Der interne Dictionary wird durch `lock` geschützt.
- Timer-Callbacks laufen auf Thread-Pool-Threads; `PromptSent`-Event wird ebenfalls auf Thread-Pool-Thread ausgelöst.
- UI-Updates im ViewModel (über `PromptSent`) müssen via `Dispatcher.Invoke` gemarshallt werden; das `TaskDetailViewModel` macht dies automatisch.
