← [Zurück zur Übersicht](index.md)

# Aufgaben — API

## Übersicht

Die Task-Detail-Ansicht exponiert öffentliche Service-Schnittstellen für die Verwaltung von Aufgaben, zeitgesteuerten Prompt-Versänden, Aufgaben-Pausen und dem Export der CLI-Rohausgabe:

- `AufgabeService` — Persistente Datenbankoperationen für Aufgaben, einschließlich `SetPauseAsync`
- `KiPluginLimitService` — Persistenz und Anwendung von KI-Plugin-Session-Limits
- `PromptZeitVersandService` — Laufzeit-Verwaltung zeitgesteuerter Prompts (inkl. Pausen-Verschiebung)
- `ProtokollService` — Persistente Protokolleinträge, einschließlich automatischer CLI-Ausgaben und Rate-Limit-Marker
- `ICliRawExportService` — Exportiert die gespeicherte CLI-Rohausgabe einer Aufgabe als `.raw`
- `IDialogService` — UI-Abstraktion für Dialoge (Speichern-Dialog, Pause-Dialog)

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
- Wird im ConPTY-Pfad automatisch durch `CliOutputProtokollWriter` aufgerufen.
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

- Erlaubte Stati: `Neu`, `Gestartet`, `Wartend` — andere Stati sowie Autonome Aufgaben werden mit `InvalidOperationException` abgelehnt.
- Beim Setzen muss `pausiertBisUtc` in der Zukunft liegen; andernfalls `InvalidOperationException`.
- Schreibt pro Aufruf einen `ProtokollTyp.SystemMeldung`-Eintrag („Aufgabe pausiert bis …" / „Pause aufgehoben").
- Wirft `InvalidOperationException`, wenn die Aufgabe nicht gefunden wird.

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
- Kandidaten: Aufgaben mit `Status.IstAktivOderWartend()`, `AusfuehrungsStatus == Aktiv`, ohne `AutonomKonfiguration`, Prefix-Vergleich `OrdinalIgnoreCase`.
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

`PromptZeitVersandService` ist ein Singleton-Service, der zeitgesteuerte Prompts pro Aufgabe in einer Laufzeit-Warteschlange verwaltet. Der Service ist registriert in der Dependency Injection und wird vom `TaskDetailViewModel` genutzt.

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

- Liegt `targetTime` in der Vergangenheit oder Gegenwart: Prompt wird sofort an die Session geschrieben (kein Eintrag in der Warteschlange).
- Liegt `targetTime` in der Zukunft: `ScheduledPromptInfo`-Eintrag wird im internen Dictionary abgelegt; ein `ITimer` wird mit der Restlaufzeit gestartet.
- **Pausen-Berücksichtigung:** Über die optionale `IServiceScopeFactory` wird der aktuelle `Aufgabe.PausiertBisUtc`-Wert geladen. Endet eine aktive Pause später als `targetTime`, wird die Zielzeit auf das Pausenende verschoben statt verworfen — sowohl bei der Planung als auch erneut im Timer-Callback (`HandleTimerElapsedAsync`), falls die Pause nachträglich gesetzt wurde. Ohne registrierte Scope-Factory (z. B. Unit-Tests) entfällt die Prüfung.
- Ein bereits geplanter Prompt für dieselbe `aufgabeId` wird ersetzt (Timer wird abgebrochen).
- Bei Erfolg wird kein Event ausgelöst, solange die Planung (nicht der Versand) stattfindet.

**Rückgabe:** `Task` — abwartet die Planung (nicht den tatsächlichen Versand).

**Fehlerbehandlung:**

- Exceptions beim Schreiben auf die Session werden geloggt. Bei stillschweigendem Verwerfen (Session `null` bei Fälligkeit) gibt es keine Exception, nur eine `LogWarning`.

**Beispiel:**

```csharp
var targetTime = DateTimeOffset.Now.AddHours(2); // in 2 Stunden
await _promptZeitVersandService.SchedulePromptAsync(
    aufgabeId: aufgabeId,
    promptText: "Analysiere den Bug in LoginService",
    targetTime: targetTime);
```

---

#### `CancelScheduledPrompt(Guid aufgabeId) : void`

**Beschreibung:** Bricht einen für die Aufgabe geplanten Prompt-Versand ab, falls vorhanden.

**Parameter:**

| Name | Typ | Beschreibung |
|------|-----|--------------|
| `aufgabeId` | `Guid` | ID der Aufgabe, deren geplanter Prompt storniert werden soll. |

**Verhalten:**

- Falls ein Prompt für `aufgabeId` geplant ist: Eintrag wird aus der Warteschlange entfernt, Timer wird abgebrochen und disposed.
- Falls kein Prompt geplant ist: Keine Aktion (keine Exception).

**Rückgabe:** Kein Rückgabewert.

**Beispiel:**

```csharp
_promptZeitVersandService.CancelScheduledPrompt(aufgabeId);
```

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
| `ScheduledPromptInfo?` | Objekt mit `AufgabeId`, `PromptText` und `TargetTime`, oder `null` wenn kein Prompt geplant ist. |

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

- Dieses Event wird ausgelöst **nach** `PseudoConsoleSession.WritePromptAsync()` erfolgreich abgeschlossen hat.
- Falls die Session zur Zielzeit nicht mehr vorhanden ist, wird dieses Event **nicht** ausgelöst; der Prompt wird still verworfen.
- Das Event wird immer auf dem Thread-Pool-Thread des Timer-Callbacks ausgelöst, **nicht** auf dem UI-Thread.

**Beispiel:**

```csharp
_promptZeitVersandService.PromptSent += (aufgabeId) =>
{
    Console.WriteLine($"Prompt für Aufgabe {aufgabeId} versendet");
};
```

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

**Beispiel:**

```csharp
var info = new ScheduledPromptInfo(
    aufgabeId: aufgabeId,
    promptText: "Analyt den Bug",
    targetTime: DateTimeOffset.Now.AddHours(1));
```

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

**Wert:** `"Prompt in Wartestellung"` während ein Prompt geplant ist, oder `null`.

**Sichtbarkeit:** TextBlock ist sichtbar nur wenn dieser Wert nicht `null` ist (via `NullOrEmptyToVisibilityConverter`).

---

#### `ScheduledPromptTimeDisplay : string?`

**Bindung:** TextBlock zur Anzeige der Zielzeit.

**Wert:** Zielzeit im Format `HH:mm`, oder `null`.

---

#### `CanSchedulePrompt : bool`

**Bedingung:** `true` wenn alle diese Bedingungen erfüllt sind:
- CLI läuft (`IsCliRunning == true`)
- Eine Promptvorlage ist ausgewählt (`SelectedPromptVorlage != null`)
- Die Vorlage hat einen nicht-leeren Prompttext
- Mindestens eines der Zeitfelder (`ScheduledPromptTargetHours` oder `ScheduledPromptTargetMinutes`) ist gesetzt

---

### Neues Command

#### `SchedulePromptCommand : ICommand`

**Typ:** `AsyncRelayCommand`

**CanExecute:** `CanSchedulePrompt` (Button ist disabled wenn `false`)

**Ausgeführte Aktion:** `SchedulePromptAsync()` (private Methode)
- Validiert Zeitfelder
- Berechnet `targetTime`
- Ruft `PromptZeitVersandService.SchedulePromptAsync()` auf
- Setzt `ScheduledPromptStatus` und `ScheduledPromptTimeDisplay`
- Leert Zeitfelder und Vorlage-Auswahl

---

## Fehlerbehandlung (PromptZeitVersandService)

| Fehler | Verhalten |
|--------|-----------|
| Session nicht gefunden bei Fälligkeit | Prompt wird still verworfen; Log-Warnung wird geschrieben; **kein** `PromptSent`-Event |
| Session disposed zwischen Planung und Versand | `ObjectDisposedException` wird geloggt; Prompt verworfen; **kein** Event |
| Schreiboperation auf InputStream schlägt fehl | Exception wird geloggt; kein UI-Feedback; `PromptSent`-Event wird **nicht** ausgelöst |
| Ungültige Zeitfelder im ViewModel | `FehlerMeldung` wird gesetzt; Service wird nicht aufgerufen; ViewModel-seitige Validierung |
| Aktive Pause bei Planung oder Fälligkeit | Zielzeit/Timer werden auf das Pausenende verschoben; Eintrag in `ScheduledPromptInfo` bleibt bestehen; **kein** `PromptSent`-Event vor dem Pausenende |

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

## Thread-Sicherheit

- `PromptZeitVersandService` ist Thread-safe: Der interne Dictionary wird durch `lock` geschützt.
- Timer-Callbacks laufen auf Thread-Pool-Threads; `PromptSent`-Event wird ebenfalls auf Thread-Pool-Thread ausgelöst.
- UI-Updates im ViewModel (über `PromptSent`) müssen via `Dispatcher.Invoke` gemarshallt werden; das `TaskDetailViewModel` macht dies automatisch.
