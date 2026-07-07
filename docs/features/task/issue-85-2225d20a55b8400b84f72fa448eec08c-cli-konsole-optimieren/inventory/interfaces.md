# Interfaces und abstrakte Typen: Bestandsaufnahme

Datei: `src/Softwareschmiede/Domain/Terminal/TerminalEvents.cs` und andere

## `TerminalEvent` (abstrakte Record-Klasse)

**Datei:** `src/Softwareschmiede/Domain/Terminal/TerminalEvents.cs`

| Aspekt | Beschreibung |
|--------|-------------|
| Zweck | Basistyp für alle Terminal-Ereignisse; verwendet als Discriminated Union durch Subklassen |
| Sichtbarkeit | `public abstract record` |
| Implementierung | Keine abstrakten Methoden; Pattern Matching ist möglich über `switch` auf Subklassen |
| Verwendung | Rückgabewert von `AnsiSequenceParser.Parse()`, Parameter für `TerminalBuffer.Apply()` |

**Subklassen:** `TextWrittenEvent`, `CursorMovedEvent`, `CursorMovedRelativeEvent`, `ColorChangedEvent`, `ScreenClearedEvent`, `LineErasedEvent`, `CursorVisibilityChangedEvent`

---

## Enum: `CliRuntimeStatus`

**Datei:** `src/Softwareschmiede/Infrastructure/Terminal/PseudoConsoleSession.cs`

| Wert | Beschreibung |
|------|-------------|
| `Inaktiv` | Kein laufender CLI-Prozess ist aktiv |
| `Laeuft` | Die CLI läuft und hat kürzlich Ausgabe oder Eingabe verarbeitet |
| `WartetAufEingabe` | Die CLI läuft, erzeugt aber seit längerer Zeit keine Ausgabe (wartet vermutlich auf Benutzereingabe) |

**Zweck:** Zustandsanzeige der laufenden CLI-Sitzung. Wird von `CliRuntimeStatusEvaluator.Determine()` berechnet.

---

## Hilfstypes

### `CliRuntimeStatusChangedEventArgs`
**Datei:** `src/Softwareschmiede/Infrastructure/Terminal/PseudoConsoleSession.cs`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `Status` | `CliRuntimeStatus` | Neuer Laufzeitstatus |

**Zweck:** Ereignisargumente für `RuntimeStatusChanged`-Event.

### `CliRuntimeStatusEvaluator` (statische Klasse)
**Datei:** `src/Softwareschmiede/Infrastructure/Terminal/PseudoConsoleSession.cs`

| Methode | Sichtbarkeit | Beschreibung |
|---------|-------------|-------------|
| `Determine(bool isRunning, DateTimeOffset startedUtc, DateTimeOffset? lastOutputUtc, DateTimeOffset? lastInputUtc, DateTimeOffset nowUtc, TimeSpan waitingThreshold)` | Public Static | Bestimmt CLI-Laufzeitstatus aus Prozess- und I/O-Aktivität |

**Logik:** 
- Prozess nicht laufend → `Inaktiv`
- Keine I/O seit `waitingThreshold` → `WartetAufEingabe`
- Sonst → `Laeuft`

---

## Dependency Properties (WPF)

### `TerminalControl.SessionProperty`
**Datei:** `src/Softwareschmiede.App/Controls/TerminalControl.cs`

| Eigenschaft | Wert | Beschreibung |
|-------------|------|-------------|
| Name | `"Session"` | Property-Name |
| Eigentümer-Typ | `TerminalControl` | Klasse, die die Dependency Property besitzt |
| Wert-Typ | `PseudoConsoleSession` | Typ der Session |
| Callback | `OnSessionChanged` | Wird aufgerufen, wenn Session sich ändert |
| Default | `null` | Standardwert |

**Zweck:** Bindet WPF-Control an `PseudoConsoleSession` zur Anzeige und Eingabe-Verarbeitung.
