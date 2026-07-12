← [Zurück zur Übersicht](index.md)

# Aufgaben — API

## Übersicht

Die Task-Detail-Ansicht exponiert zwei öffentliche Service-Schnittstellen für die Verwaltung von Aufgaben und zeitgesteuerten Prompt-Versänden:

- `AufgabeService` — Persistente Datenbankoperationen für Aufgaben
- `PromptZeitVersandService` — Laufzeit-Verwaltung zeitgesteuerter Prompts

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

---

## Thread-Sicherheit

- `PromptZeitVersandService` ist Thread-safe: Der interne Dictionary wird durch `lock` geschützt.
- Timer-Callbacks laufen auf Thread-Pool-Threads; `PromptSent`-Event wird ebenfalls auf Thread-Pool-Thread ausgelöst.
- UI-Updates im ViewModel (über `PromptSent`) müssen via `Dispatcher.Invoke` gemarshallt werden; das `TaskDetailViewModel` macht dies automatisch.
