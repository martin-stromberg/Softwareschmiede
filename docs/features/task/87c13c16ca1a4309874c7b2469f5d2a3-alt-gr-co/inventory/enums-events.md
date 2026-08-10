# Enums und Events

## `CliRuntimeStatus` Enum

Datei: `src/Softwareschmiede/Infrastructure/Terminal/PseudoConsoleSession.cs`

Laufzeitstatus einer aktiven CLI-Sitzung.

| Wert | Beschreibung |
|------|-------------|
| `Inaktiv` | Kein laufender CLI-Prozess ist aktiv |
| `Laeuft` | Die CLI läuft und hat kürzlich Ausgabe oder Eingabe verarbeitet |
| `WartetAufEingabe` | Die CLI läuft, erzeugt aber seit längerer Zeit keine Ausgabe und wartet vermutlich auf Benutzereingabe |

---

## Events in PseudoConsoleSession

Datei: `src/Softwareschmiede/Infrastructure/Terminal/PseudoConsoleSession.cs`

### `RuntimeStatusChanged`

```csharp
public event EventHandler<CliRuntimeStatusChangedEventArgs>? RuntimeStatusChanged;
```

- **Auslöser:** In `SetRuntimeStatus()` wenn sich `_runtimeStatus` ändert
- **Argumente:** `CliRuntimeStatusChangedEventArgs` mit neuer `Status`
- **Zweck:** Benachrichtigung über Statuswechsel (z. B. von "Läuft" zu "Wartet auf Eingabe")

### `BufferChanged`

```csharp
public event EventHandler? BufferChanged;
```

- **Auslöser:** In `ReadLoopAsync()` nach jedem erfolgreichen Verarbeiten eines Output-Chunks
- **Argumente:** Standard `EventArgs`
- **Zweck:** Signalisiert dass der Terminal-Buffer aktualisiert wurde; `TerminalControl` abonniert dieses Event in `OnSessionChanged()` und ruft `InvalidateVisual()` auf

---

## Events in TerminalControl

Datei: `src/Softwareschmiede.App/Controls/TerminalControl.cs`

### `OnPreviewKeyDown` (geschützter Override)

- **Trigger:** WPF-Event beim Drücken einer Taste
- **Verarbeitung:**
  - Spezialfall Ctrl+V: `ReadClipboardAndInsertAsync()` wird aufgerufen
  - Allgemein: `KeyToVt100Encoder.Encode()` wird aufgerufen
  - Bei Erfolg: Event als behandelt markieren
- **Keine benutzerdefinierten Events**, aber verwendet WPF `KeyEventArgs.Handled`

### `OnTextInput` (geschützter Override)

- **Trigger:** WPF-Event für Texteingabe (nach Tastenkombinationen und Komposition)
- **Verarbeitung:**
  - `KeyToVt100Encoder.EncodeText()` wird aufgerufen
  - Bytes werden in `InputStream` geschrieben
  - Event als behandelt markieren
- **Keine benutzerdefinierten Events**

---

## Hilfsklasse: CliRuntimeStatusChangedEventArgs

Datei: `src/Softwareschmiede/Infrastructure/Terminal/PseudoConsoleSession.cs`

```csharp
public sealed class CliRuntimeStatusChangedEventArgs : EventArgs
{
    public CliRuntimeStatus Status { get; }
    
    public CliRuntimeStatusChangedEventArgs(CliRuntimeStatus status)
    {
        Status = status;
    }
}
```

- **Zweck:** Argumente für `RuntimeStatusChanged`-Event
- **Status-Property:** Neuer Laufzeitstatus

---

## Hilfsklasse: CliRuntimeStatusEvaluator

Datei: `src/Softwareschmiede/Infrastructure/Terminal/PseudoConsoleSession.cs`

```csharp
public static class CliRuntimeStatusEvaluator
{
    public static CliRuntimeStatus Determine(
        bool isRunning,
        DateTimeOffset startedUtc,
        DateTimeOffset? lastOutputUtc,
        DateTimeOffset? lastInputUtc,
        DateTimeOffset nowUtc,
        TimeSpan waitingThreshold)
    {
        // Logik zur Statusbestimmung
    }
}
```

- **Zweck:** Logik zur Bestimmung des nächsten Status basierend auf Prozess- und I/O-Aktivität
- **Aufgerufen von:** `RefreshRuntimeStatus()` in `PseudoConsoleSession`
- **Rückgabe:** `CliRuntimeStatus` (Inaktiv, Laeuft, WartetAufEingabe)
